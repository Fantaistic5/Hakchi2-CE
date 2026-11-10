using com.clusterrr.hakchi_gui.Covers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamShinkansen.Scrapers.Covers;

namespace com.clusterrr.hakchi_gui
{
    /// <summary>
    /// Recherche groupee de jaquettes : fenetre fonctionnelle, a redessiner au projet 3.
    /// Annulation ou reseau coupe : les jeux restants passent en « non trouve », sans boite d'erreur.
    /// </summary>
    public partial class CoverBatchForm : Form
    {
        private const int SearchChunk = 8;      // SearchManyAsync par lots : une annulation garde les lots termines
        private const int PreviewParallelism = 4;
        private const int MaxFallbackTries = 4;
        private const int MaxAlternatives = 12;
        private const int ThumbSize = 64;

        private enum State { NotSearched, Searching, NotFound, NetworkError, Cancelled, ToCheck, Ready, HasCover, Applied, Failed }

        private sealed class Row
        {
            public NesApplication App;
            public bool HadCover;
            public GameQuery Query;
            public CoverProposal Proposal;
            public CoverCandidate Chosen;
            public Bitmap Full;
            public bool Searched, Searching, Applied, Failed, Cancelled;
            public int ThumbIndex = -1;
            public Bitmap ThumbFor;
            public ListViewItem Item;
        }

        private readonly ComponentResourceManager _res = new ComponentResourceManager(typeof(CoverBatchForm));
        private readonly List<NesApplication> _games;
        private readonly Dictionary<NesApplication, Row> _rowByApp = new Dictionary<NesApplication, Row>();
        private readonly Dictionary<string, Bitmap> _images = new Dictionary<string, Bitmap>();
        private readonly ImageList _thumbs = new ImageList { ImageSize = new Size(ThumbSize, ThumbSize), ColorDepth = ColorDepth.Depth32Bit };
        private readonly ToolTip _tip = new ToolTip();
        private readonly Dictionary<string, Task<Bitmap>> _downloads = new Dictionary<string, Task<Bitmap>>();
        private readonly CancellationTokenSource _closeCts = new CancellationTokenSource();
        private CoverSearchService _service;
        private bool _disposed;
        private CancellationTokenSource _cts;
        private int _altVersion;
        private bool _busy;
        private bool _updating;

        /// <summary>Nombre de jaquettes appliquees pendant la vie de la fenetre.</summary>
        public int AppliedCount { get; private set; }

        public CoverBatchForm(IEnumerable<NesApplication> games)
        {
            InitializeComponent();
            _games = games.Where(g => g != null).ToList();
            listViewResults.SmallImageList = _thumbs;
            RebuildRows();
        }

        private string T(string key) { return _res.GetString(key) ?? key; }

        // ---- lignes ----

        private IEnumerable<NesApplication> VisibleGames()
        {
            return checkBoxIncludeExisting.Checked ? _games : Covers.CoverTargets.DefaultSelection(_games);
        }

        private void RebuildRows()
        {
            _updating = true;
            listViewResults.BeginUpdate();
            try
            {
                listViewResults.Items.Clear();
                foreach (var app in VisibleGames())
                {
                    Row row;
                    if (!_rowByApp.TryGetValue(app, out row))
                    {
                        row = new Row { App = app, HadCover = !Covers.CoverTargets.NeedsCover(app), Query = Covers.CoverTargets.ToQuery(app) };
                        _rowByApp[app] = row;
                    }
                    row.Item = new ListViewItem(app.Name ?? string.Empty) { Tag = row };
                    row.Item.SubItems.Add(string.Empty);
                    row.Item.SubItems.Add(string.Empty);
                    row.Item.SubItems.Add(string.Empty);
                    listViewResults.Items.Add(row.Item);
                    RefreshItem(row);
                }
            }
            finally
            {
                listViewResults.EndUpdate();
                _updating = false;
            }
            UpdateSummary(false);
            UpdateButtons();
        }

        private IEnumerable<Row> VisibleRows()
        {
            // Pendant la creation du handle, ListView rejoue l'etat des cases (ItemChecked)
            // alors que sa liste interne contient encore des entrees nulles.
            return listViewResults.Items.Cast<ListViewItem>().Where(i => i != null).Select(i => i.Tag as Row).Where(r => r != null);
        }

        private State StateOf(Row r)
        {
            if (r.Applied) return State.Applied;
            if (r.Failed) return State.Failed;
            if (r.Searching) return State.Searching;
            if (!r.Searched) return r.HadCover ? State.HasCover : State.NotSearched;
            if (r.Cancelled) return State.Cancelled;
            if (r.Chosen == null) return r.Proposal != null && r.Proposal.Error != null ? State.NetworkError : State.NotFound;
            return r.Item != null && r.Item.Checked ? State.Ready : State.ToCheck;
        }

        private void RefreshItem(Row r)
        {
            if (r.Item == null) return;
            bool was = _updating;
            _updating = true;
            try
            {
                State s = StateOf(r);
                r.Item.SubItems[3].Text = T("State." + s);
                if (r.Chosen != null)
                {
                    string region = string.IsNullOrEmpty(r.Chosen.Region) ? string.Empty : " · " + r.Chosen.Region;
                    r.Item.SubItems[1].Text = r.Chosen.SourceName + region;
                    r.Item.SubItems[2].Text = T("Match." + r.Chosen.Match);
                }
                else
                {
                    r.Item.SubItems[1].Text = string.Empty;
                    r.Item.SubItems[2].Text = string.Empty;
                }
                if (!ReferenceEquals(r.Full, r.ThumbFor))
                {
                    if (r.Full == null) r.ThumbIndex = -1;
                    else r.ThumbIndex = AddThumb(r.Full, r.ThumbIndex);
                    r.ThumbFor = r.Full;
                }
                r.Item.ImageIndex = r.ThumbIndex;
            }
            finally { _updating = was; }
        }

        private int AddThumb(Bitmap src, int replaceIndex)
        {
            var thumb = new Bitmap(ThumbSize, ThumbSize);
            using (var g = Graphics.FromImage(thumb))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float k = Math.Min((float)ThumbSize / src.Width, (float)ThumbSize / src.Height);
                int w = Math.Max(1, (int)(src.Width * k)), h = Math.Max(1, (int)(src.Height * k));
                g.DrawImage(src, (ThumbSize - w) / 2, (ThumbSize - h) / 2, w, h);
            }
            int idx = replaceIndex;
            if (idx >= 0 && idx < _thumbs.Images.Count) _thumbs.Images[idx] = thumb; // remplace, la liste ne grossit pas
            else { _thumbs.Images.Add(thumb); idx = _thumbs.Images.Count - 1; }
            thumb.Dispose();
            return idx;
        }

        private void UpdateSummary(bool applied)
        {
            int ready = 0, toCheck = 0, notFound = 0;
            foreach (var r in VisibleRows())
            {
                if (r.Applied) continue;
                if (r.Cancelled) continue;
                if (r.Searched && r.Chosen == null) notFound++;
                else if (r.Chosen != null)
                {
                    if (r.Item.Checked) ready++; else toCheck++;
                }
            }
            labelSummary.Text = string.Format(T(applied ? "Msg.SummaryApplied" : "Msg.SummarySearch"),
                applied ? AppliedCount : ready, toCheck, notFound);
        }

        private void UpdateButtons()
        {
            buttonSearch.Enabled = !_busy && listViewResults.Items.Count > 0;
            buttonCancel.Enabled = _busy;
            checkBoxIncludeExisting.Enabled = !_busy;
            buttonApply.Enabled = !_busy && VisibleRows().Any(r => !r.Applied && r.Chosen != null && r.Item.Checked);
            buttonClose.Enabled = !_busy;
        }

        // ---- recherche ----

        private async void buttonSearch_Click(object sender, EventArgs e)
        {
            var rows = VisibleRows().Where(r => !r.Applied).ToList();
            if (rows.Count == 0) { labelStatus.Text = T("Msg.NoGames"); return; }

            _busy = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            UpdateButtons();
            ClearAlternatives();

            _updating = true;
            foreach (var r in rows)
            {
                r.Proposal = null; r.Chosen = null; r.Failed = false; r.Cancelled = false;
                r.Full = null; // le bitmap reste dans le cache _images
                r.Searched = false; r.Searching = true;
                r.Item.Checked = false;
                RefreshItem(r);
            }
            _updating = false;

            progressBar.Minimum = 0;
            progressBar.Maximum = rows.Count;
            progressBar.Value = 0;
            string message = T("Msg.Done");
            bool cancelled = false;
            try
            {
                if (_service == null) _service = CoverServices.CreateSearchService();
                var service = _service;
                int offset = 0;
                for (int i = 0; i < rows.Count; i += SearchChunk)
                {
                    var chunk = rows.Skip(i).Take(SearchChunk).ToList();
                    int baseDone = offset;
                    var progress = new Progress<int>(n => ShowProgress("Msg.Searching", baseDone + n, rows.Count));
                    var proposals = await service.SearchManyAsync(chunk.Select(r => r.Query).ToList(), progress, ct);
                    for (int k = 0; k < chunk.Count; k++)
                    {
                        chunk[k].Proposal = proposals[k];
                        chunk[k].Chosen = proposals[k] != null ? proposals[k].Best : null;
                    }
                    offset += chunk.Count;
                    ShowProgress("Msg.Searching", offset, rows.Count);
                }

                var withProposal = rows.Where(r => r.Chosen != null).ToList();
                await LoadPreviewsAsync(withProposal, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelled = true; message = T("Msg.Cancelled"); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("CoverBatchForm: " + ex.Message);
                message = T("Msg.Error");
            }
            if (_disposed) return;

            // tout ce qui n'a pas abouti (annule, reseau coupe) devient « non trouve »
            _updating = true;
            foreach (var r in rows)
            {
                r.Searching = false;
                if (cancelled && (r.Proposal == null || r.Full == null))
                {
                    // annule avant la fin pour ce jeu : etat distinct, pas non trouve
                    r.Cancelled = true; r.Searched = true; r.Chosen = null;
                    r.Item.Checked = false;
                    RefreshItem(r);
                    continue;
                }
                r.Searched = true;
                if (r.Full == null) r.Chosen = null;
                r.Item.Checked = r.Chosen != null && !r.HadCover && IsExact(r.Chosen);
                RefreshItem(r);
            }
            _updating = false;

            if (!cancelled)
            {
                int errors = rows.Count(r => r.Chosen == null && r.Proposal != null && r.Proposal.Error != null);
                if (errors > 0 && errors == rows.Count) message = T("Msg.NetworkDown");
                else if (errors > 0) message = string.Format(T("Msg.NetworkSome"), errors, rows.Count);
            }
            _busy = false;
            labelStatus.Text = message;
            progressBar.Value = progressBar.Maximum;
            UpdateSummary(false);
            UpdateButtons();
        }

        private static bool IsExact(CoverCandidate c)
        {
            return c.Match == MatchKind.Crc || c.Match == MatchKind.RomName;
        }

        private void ShowProgress(string key, int done, int total)
        {
            if (IsDisposed) return;
            labelStatus.Text = string.Format(T(key), done, total);
            if (key == "Msg.Searching" || total > 0)
            {
                progressBar.Maximum = Math.Max(1, total);
                progressBar.Value = Math.Min(done, progressBar.Maximum);
            }
        }

        /// <summary>Telecharge l'apercu de chaque proposition ; si le meilleur echoue, essaie les alternatives.</summary>
        private async Task LoadPreviewsAsync(List<Row> rows, CancellationToken ct)
        {
            int done = 0;
            using (var gate = new SemaphoreSlim(PreviewParallelism))
            {
                var tasks = rows.Select(async r =>
                {
                    await gate.WaitAsync(ct);
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        var candidates = new List<CoverCandidate>();
                        if (r.Proposal.Best != null) candidates.Add(r.Proposal.Best);
                        candidates.AddRange(r.Proposal.Alternatives ?? new List<CoverCandidate>());
                        r.Chosen = null;
                        foreach (var c in candidates.Take(MaxFallbackTries))
                        {
                            Bitmap img;
                            try { img = await GetImageAsync(c.Url, ct); }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                            catch (Exception ex)
                            {
                                // delai depasse (TaskCanceledException) ou erreur : candidat suivant
                                System.Diagnostics.Trace.WriteLine("CoverBatchForm: " + ex.Message);
                                continue;
                            }
                            r.Chosen = c;
                            r.Full = img;
                            break;
                        }
                        RefreshItem(r);
                        done++;
                        ShowProgress("Msg.Previews", done, rows.Count);
                    }
                    finally { gate.Release(); }
                }).ToList();
                await Task.WhenAll(tasks);
            }
        }

        // Telechargements dedoublonnes par URL, lies a la vie de la fenetre ; l'appelant peut seulement
        // arreter d'attendre (ct). Toujours appele depuis le thread UI.
        private async Task<Bitmap> GetImageAsync(string url, CancellationToken ct)
        {
            Bitmap cached;
            if (_images.TryGetValue(url, out cached)) return cached;
            Task<Bitmap> task;
            if (!_downloads.TryGetValue(url, out task))
            {
                task = DownloadAndStoreAsync(url);
                if (!task.IsCompleted) _downloads[url] = task;
            }
            return await WithCancel(task, ct);
        }

        private async Task<Bitmap> DownloadAndStoreAsync(string url)
        {
            try
            {
                var img = await CoverServices.DownloadImageAsync(url, _closeCts.Token);
                if (_disposed) { img.Dispose(); throw new OperationCanceledException(_closeCts.Token); }
                _images[url] = img;
                return img;
            }
            finally
            {
                _downloads.Remove(url); // un echec ne reste pas en cache
            }
        }

        private static async Task<T> WithCancel<T>(Task<T> task, CancellationToken ct)
        {
            if (!ct.CanBeCanceled) return await task;
            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(() => tcs.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, tcs.Task) != task) throw new OperationCanceledException(ct);
            }
            return await task;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            var cts = _cts;
            if (cts != null) cts.Cancel();
        }

        // ---- alternatives ----

        private void ClearAlternatives()
        {
            _altVersion++;
            foreach (Control c in flowAlternatives.Controls.Cast<Control>().ToList())
            {
                flowAlternatives.Controls.Remove(c);
                c.Dispose();
            }
        }

        private async void listViewResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            ClearAlternatives();
            if (_busy || listViewResults.SelectedItems.Count == 0) return;
            var row = (Row)listViewResults.SelectedItems[0].Tag;
            if (row.Proposal == null) return;

            int version = _altVersion;
            var list = new List<CoverCandidate>();
            if (row.Proposal.Best != null) list.Add(row.Proposal.Best);
            list.AddRange(row.Proposal.Alternatives ?? new List<CoverCandidate>());
            foreach (var cand in list.Take(MaxAlternatives))
            {
                var box = new PictureBox
                {
                    Size = new Size(ThumbSize + 24, ThumbSize + 24),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = row.Chosen == cand ? BorderStyle.Fixed3D : BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand,
                    Tag = cand
                };
                string region = string.IsNullOrEmpty(cand.Region) ? string.Empty : " · " + cand.Region;
                _tip.SetToolTip(box, cand.SourceName + region + " — " + T("Match." + cand.Match));
                var captured = cand;
                box.Click += async (s, a) => await ChooseAlternativeAsync(row, captured);
                flowAlternatives.Controls.Add(box);

                try
                {
                    var img = await GetImageAsync(cand.Url, _closeCts.Token);
                    if (_disposed || version != _altVersion || box.IsDisposed) return;
                    box.Image = img;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("CoverBatchForm: " + ex.Message);
                    if (version != _altVersion || box.IsDisposed) return;
                    box.Visible = false;
                }
            }
        }

        private async Task ChooseAlternativeAsync(Row row, CoverCandidate cand)
        {
            if (_busy || row.Applied) return;
            try
            {
                var img = await GetImageAsync(cand.Url, _closeCts.Token);
                if (_disposed) return;
                row.Chosen = cand;
                row.Full = img;
                row.Failed = false;
                RefreshItem(row);
                row.Item.Checked = true; // choix explicite de l'utilisateur
                RefreshItem(row);
                UpdateSummary(false);
                UpdateButtons();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("CoverBatchForm: " + ex.Message);
            }
        }

        // ---- application ----

        private async void buttonApply_Click(object sender, EventArgs e)
        {
            var targets = VisibleRows().Where(r => !r.Applied && r.Chosen != null && r.Item.Checked).ToList();
            if (targets.Count == 0) return;

            _busy = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            UpdateButtons();
            int done = 0;
            try
            {
                foreach (var r in targets)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        Bitmap img = r.Full ?? await GetImageAsync(r.Chosen.Url, ct);
                        string source = r.Chosen.SourceName;
                        var app = r.App;
                        await Task.Run(() => app.ApplyCover(img, source));
                        r.Applied = true;
                        AppliedCount++;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine("CoverBatchForm: apply failed: " + ex.Message);
                        r.Failed = true;
                    }
                    RefreshItem(r);
                    done++;
                    ShowProgress("Msg.Applying", done, targets.Count);
                }
                labelStatus.Text = T("Msg.Done");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { labelStatus.Text = T("Msg.Cancelled"); }
            _busy = false;
            UpdateSummary(true);
            UpdateButtons();
        }

        // ---- divers ----

        private void checkBoxIncludeExisting_CheckedChanged(object sender, EventArgs e)
        {
            if (_busy) return;
            ClearAlternatives();
            RebuildRows();
        }

        private void listViewResults_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (_updating || _busy || !listViewResults.Created || e.Item == null) return;
            var row = e.Item.Tag as Row;
            if (row != null) RefreshItem(row);
            UpdateSummary(false);
            UpdateButtons();
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_busy)
            {
                var cts = _cts;
                if (cts != null) cts.Cancel();
                // fermeture voulue par l'utilisateur : on annule d'abord et la fenetre reste ouverte ;
                // fin de session, arret de l'application... : on laisse fermer
                if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _disposed = true;
            _closeCts.Cancel();
            ClearAlternatives();
            foreach (var img in _images.Values) img.Dispose();
            _images.Clear();
            _thumbs.Dispose();
            _tip.Dispose();
        }
    }
}
