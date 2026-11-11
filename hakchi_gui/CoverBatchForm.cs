using com.clusterrr.hakchi_gui.Covers;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamShinkansen.Scrapers.Covers;

namespace com.clusterrr.hakchi_gui
{
    /// <summary>
    /// Recherche groupee de jaquettes, dessinee comme la grille : vignette 3:4, nom, source et region en TextDim, case B1.
    /// <para>
    /// Chaque jeu est recherche puis son apercu telecharge independamment des autres (4 a la fois) : une annulation garde tout ce
    /// qui a abouti, et une nouvelle recherche ne reprend que les jeux sans resultat. Annulation ou reseau coupe : les jeux restants
    /// passent en « annulee » ou « non trouvee », sans boite d'erreur.
    /// </para>
    /// <para>
    /// Memoire : seuls les octets des images telechargees sont gardes (jamais d'image decodee pleine taille) ; les apercus de la
    /// liste passent par un <see cref="ThumbnailCache"/> (decodage en tache de fond, vignette 3:4). L'image pleine taille n'est
    /// decodee qu'au moment de l'appliquer.
    /// </para>
    /// <para>
    /// Limite connue, hors perimetre : les appels a Google et a TheGamesDB ne sont pas interruptibles. Apres « Annuler », la fenetre
    /// n'attend plus leur reponse (elle est ignoree a son arrivee), mais la requete en cours se termine en arriere-plan
    /// (jusqu'au delai de la source, environ 60 s au pire).
    /// </para>
    /// </summary>
    public partial class CoverBatchForm : com.clusterrr.hakchi_gui.Theme.B1Form
    {
        private const int SearchParallelism = 4;
        private const int MaxFallbackTries = 4;
        private const int MaxAlternatives = 12;
        // cotes a 96 dpi : vignette de ligne et vignette de candidat en 3:4, comme la grille
        private const int ThumbW = 54, ThumbH = 72, RowH = 88, ThumbSlot = 70;
        private const int AltW = 60, AltH = 80;
        private const int StateColumn = 170;

        private enum State { NotSearched, Searching, NotFound, NetworkError, Cancelled, ToCheck, Ready, HasCover, Applied, Failed }

        private sealed class Row
        {
            public NesApplication App;
            public bool HadCover;
            public GameQuery Query;
            public CoverProposal Proposal;
            public CoverCandidate Chosen;
            public byte[] Data;            // octets de l'image choisie (pas d'image decodee)
            public byte[] ShownData;       // octets dont la vignette est en cache
            public bool Searched, Searching, Applied, Failed, Cancelled, Checked;
            public ListViewItem Item;
        }

        private readonly ComponentResourceManager _res = new ComponentResourceManager(typeof(CoverBatchForm));
        private readonly List<NesApplication> _games;
        private readonly Dictionary<NesApplication, Row> _rowByApp = new Dictionary<NesApplication, Row>();
        private readonly Dictionary<string, byte[]> _images = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, Task<byte[]>> _downloads = new Dictionary<string, Task<byte[]>>();
        private readonly ConcurrentDictionary<NesApplication, byte[]> _chosenData = new ConcurrentDictionary<NesApplication, byte[]>();
        private readonly Dictionary<string, Bitmap> _altThumbs = new Dictionary<string, Bitmap>();
        private readonly ThumbnailCache _previews;       // apercu de la jaquette proposee (octets telecharges)
        private readonly ThumbnailCache _covers;         // jaquette actuelle du jeu (avant recherche, jeux qui en ont une)
        private readonly ImageList _thumbs;              // hauteur de ligne et place de la vignette (aucune image)
        private readonly ToolTip _tip = new ToolTip();
        private readonly CancellationTokenSource _closeCts = new CancellationTokenSource();
        private CoverSearchService _service;
        private bool _disposed;
        private CancellationTokenSource _cts;
        private int _altVersion;
        private bool _busy;
        private bool _updating;
        private int _stateIconLeft = -1, _stateIconRight = -1;   // case native (zone cliquable), relative au bord gauche de la ligne

        /// <summary>Source des recherches (tests : sources factices, jamais le reseau).</summary>
        internal Func<CoverSearchService> ServiceFactory = CoverServices.CreateSearchService;
        /// <summary>Telechargement des octets d'une image (tests : octets prepares).</summary>
        internal Func<string, CancellationToken, Task<byte[]>> Downloader = CoverServices.DownloadBytesAsync;
        /// <summary>Ecriture de la jaquette dans le jeu, appelee hors du thread UI avec une image privee.</summary>
        internal Action<NesApplication, Image, string> CoverApplier = (app, img, source) => app.ApplyCover(img, source);

        /// <summary>Nombre de jaquettes appliquees pendant la vie de la fenetre.</summary>
        public int AppliedCount { get; private set; }

        internal bool IsBusy { get { return _busy; } }

        public CoverBatchForm(IEnumerable<NesApplication> games)
        {
            InitializeComponent();
            _games = games.Where(g => g != null).ToList();
            float s = B1Dpi.Scale(this);
            _thumbs = new ImageList { ImageSize = new Size(B1Draw.Px(ThumbSlot, s), Math.Min(256, B1Draw.Px(RowH, s))), ColorDepth = ColorDepth.Depth32Bit };
            _previews = new ThumbnailCache(1024, 2) { Loader = LoadPreview };
            _covers = new ThumbnailCache(1024, 2);
            listViewResults.SmallImageList = _thumbs;
            B1Skin.MarkPrimary(buttonApply);
            RebuildRows();
        }

        private string T(string key) { return _res.GetString(key) ?? key; }

        private float Dpi { get { return B1Dpi.Scale(listViewResults); } }

        private Size ThumbSize { get { float s = Dpi; return new Size(B1Draw.Px(ThumbW, s), B1Draw.Px(ThumbH, s)); } }

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
                    row.Item = new ListViewItem(app.Name ?? string.Empty) { Tag = row, Checked = row.Checked, ImageIndex = -1 };
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

        /// <summary>Resultat deja obtenu (proposition et apercu) : garde par une nouvelle recherche.</summary>
        private static bool HasResult(Row r)
        {
            return r.Searched && !r.Searching && !r.Cancelled && r.Chosen != null && r.Data != null;
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
                    r.Item.SubItems[1].Text = SourceText(r.Chosen);
                    r.Item.SubItems[2].Text = T("Match." + r.Chosen.Match);
                }
                else
                {
                    r.Item.SubItems[1].Text = string.Empty;
                    r.Item.SubItems[2].Text = string.Empty;
                }
                if (!ReferenceEquals(r.Data, r.ShownData))
                {
                    // nouvelle image proposee : la vignette en cache est perimee
                    if (r.Data == null) { byte[] old; _chosenData.TryRemove(r.App, out old); }
                    else _chosenData[r.App] = r.Data;
                    _previews.Forget(r.App);
                    r.ShownData = r.Data;
                }
                InvalidateRow(r.Item);
            }
            finally { _updating = was; }
        }

        private static string SourceText(CoverCandidate c)
        {
            string region = string.IsNullOrEmpty(c.Region) ? string.Empty : " · " + c.Region;
            return c.SourceName + region;
        }

        private void InvalidateRow(ListViewItem item)
        {
            if (item == null || item.ListView == null || !listViewResults.IsHandleCreated) return;
            try { listViewResults.Invalidate(item.Bounds); } catch (Exception) { }
        }

        private void InvalidateApp(NesApplication app)
        {
            Row row;
            if (!_disposed && app != null && _rowByApp.TryGetValue(app, out row)) InvalidateRow(row.Item);
        }

        /// <summary>Lecteur du cache d'apercus (tache de fond) : decode les octets de l'image proposee, ou rien.</summary>
        private Image LoadPreview(NesApplication app)
        {
            byte[] data;
            if (!_chosenData.TryGetValue(app, out data) || data == null) return null;
            return Image.FromStream(new MemoryStream(data, false));
        }

        private void UpdateSummary(bool applied)
        {
            int ready = 0, toCheck = 0, notFound = 0;
            foreach (var r in VisibleRows())
            {
                if (r.Applied) continue;
                if (r.Cancelled) continue;
                if (r.Searched && r.Chosen == null)
                {
                    if (r.Proposal == null || r.Proposal.Error == null) notFound++; // erreurs reseau : dans la ligne d'etat
                }
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
            await RunSearchAsync();
        }

        /// <summary>
        /// Recherche les jeux visibles qui n'ont pas encore de resultat (tous s'ils en ont deja un). Chaque jeu est enregistre des
        /// qu'il aboutit : une annulation garde les resultats obtenus.
        /// </summary>
        internal async Task RunSearchAsync()
        {
            if (_busy) return;
            var all = VisibleRows().Where(r => !r.Applied).ToList();
            if (all.Count == 0) { labelStatus.Text = T("Msg.NoGames"); return; }
            // une nouvelle recherche (apres une annulation, un reseau coupe...) ne reprend que les jeux sans resultat
            var rows = all.Where(r => !HasResult(r)).ToList();
            if (rows.Count == 0) rows = all;    // tout est deja trouve : nouvelle recherche complete, comme avant

            _busy = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            UpdateButtons();
            ClearAlternatives();

            _updating = true;
            foreach (var r in rows)
            {
                r.Proposal = null; r.Chosen = null; r.Data = null; r.Failed = false; r.Cancelled = false;
                r.Searched = false; r.Searching = true;
                r.Item.Checked = false;
                RefreshItem(r);
            }
            _updating = false;

            progressBar.Minimum = 0;
            progressBar.Maximum = Math.Max(1, rows.Count);
            progressBar.Value = 0;
            string message = T("Msg.Done");
            bool cancelled = false;
            int done = 0;
            try
            {
                if (_service == null) _service = ServiceFactory();
                var service = _service;
                using (var gate = new SemaphoreSlim(SearchParallelism))
                {
                    var tasks = rows.Select(async r =>
                    {
                        await gate.WaitAsync(ct);
                        try { await SearchRowAsync(service, r, ct); }
                        finally { gate.Release(); }
                        done++;
                        ShowProgress("Msg.Searching", done, rows.Count);
                    }).ToList();
                    await Task.WhenAll(tasks);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { cancelled = true; message = T("Msg.Cancelled"); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("CoverBatchForm: " + ex.Message);
                message = T("Msg.Error");
            }
            if (_disposed) return;

            // jeux qui n'ont pas abouti : « annulee » (etat distinct) ou, en cas d'erreur, « non trouve »
            _updating = true;
            foreach (var r in rows.Where(x => x.Searching))
            {
                r.Searching = false;
                r.Searched = true;
                r.Cancelled = cancelled;
                r.Chosen = null; r.Data = null;
                r.Item.Checked = false;
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

        /// <summary>Un jeu : proposition, puis apercu du meilleur candidat (ou des suivants s'il echoue). Resultat enregistre aussitot.</summary>
        private async Task SearchRowAsync(CoverSearchService service, Row r, CancellationToken ct)
        {
            CoverProposal proposal;
            try
            {
                // Google / TheGamesDB ne s'interrompent pas : on cesse seulement d'attendre (la reponse tardive est ignoree)
                proposal = await WithCancel(service.SearchAsync(r.Query, ct), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                proposal = new CoverProposal { Query = r.Query, Alternatives = new List<CoverCandidate>(), Error = ex.Message };
            }
            ct.ThrowIfCancellationRequested();

            var candidates = new List<CoverCandidate>();
            if (proposal != null && proposal.Best != null) candidates.Add(proposal.Best);
            if (proposal != null && proposal.Alternatives != null) candidates.AddRange(proposal.Alternatives);
            CoverCandidate chosen = null;
            byte[] data = null;
            foreach (var c in candidates.Take(MaxFallbackTries))
            {
                try { data = await GetDataAsync(c.Url, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // delai depasse (TaskCanceledException), erreur ou image illisible : candidat suivant
                    System.Diagnostics.Trace.WriteLine("CoverBatchForm: " + ex.Message);
                    continue;
                }
                chosen = c;
                break;
            }
            ct.ThrowIfCancellationRequested();
            if (_disposed) return;

            _updating = true;
            try
            {
                r.Proposal = proposal;
                r.Chosen = chosen;
                r.Data = chosen != null ? data : null;
                r.Searching = false;
                r.Searched = true;
                r.Cancelled = false;
                r.Item.Checked = chosen != null && !r.HadCover && IsExact(chosen);
                RefreshItem(r);
            }
            finally { _updating = false; }
            UpdateSummary(false);
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

        // Telechargements dedoublonnes par URL, lies a la vie de la fenetre ; l'appelant peut seulement
        // arreter d'attendre (ct). Toujours appele depuis le thread UI. Rend les octets d'une image lisible.
        private async Task<byte[]> GetDataAsync(string url, CancellationToken ct)
        {
            byte[] cached;
            if (_images.TryGetValue(url, out cached)) return cached;
            Task<byte[]> task;
            if (!_downloads.TryGetValue(url, out task))
            {
                task = DownloadAndStoreAsync(url);
                if (!task.IsCompleted) _downloads[url] = task;
            }
            return await WithCancel(task, ct);
        }

        private async Task<byte[]> DownloadAndStoreAsync(string url)
        {
            try
            {
                var data = await Downloader(url, _closeCts.Token);
                if (data == null || data.Length == 0) throw new InvalidDataException("image vide");
                // en-tete lisible (format reconnu, dimensions) sans decoder les pixels
                await Task.Run(() =>
                {
                    using (var ms = new MemoryStream(data, false))
                    using (var img = Image.FromStream(ms, false, false))
                        if (img.Width <= 0 || img.Height <= 0) throw new InvalidDataException("image vide");
                });
                if (_disposed) throw new OperationCanceledException(_closeCts.Token);
                _images[url] = data;
                return data;
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
                if (await Task.WhenAny(task, tcs.Task) != task)
                {
                    // la tache abandonnee peut encore echouer : son exception est observee, jamais remontee
                    var ignored = task.ContinueWith(t => { var x = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    throw new OperationCanceledException(ct);
                }
            }
            return await task;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            CancelRunning();
        }

        internal void CancelRunning()
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
                c.Dispose();      // l'image appartient a _altThumbs
            }
        }

        private async void listViewResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            ClearAlternatives();
            if (_busy || listViewResults.SelectedItems.Count == 0) return;
            var row = (Row)listViewResults.SelectedItems[0].Tag;
            if (row.Proposal == null) return;

            int version = _altVersion;
            float s = Dpi;
            var list = new List<CoverCandidate>();
            if (row.Proposal.Best != null) list.Add(row.Proposal.Best);
            list.AddRange(row.Proposal.Alternatives ?? new List<CoverCandidate>());
            foreach (var cand in list.Take(MaxAlternatives))
            {
                var box = new PictureBox
                {
                    Size = new Size(B1Draw.Px(AltW, s), B1Draw.Px(AltH, s)),
                    Margin = new Padding(0, 0, B1Draw.Px(8, s), 0),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    BorderStyle = BorderStyle.None,
                    BackColor = B1Palette.RowActive,
                    Cursor = Cursors.Hand,
                    Tag = cand
                };
                var captured = cand;
                box.Paint += (o, a) => PaintAlternativeFrame(a.Graphics, (PictureBox)o, row);
                _tip.SetToolTip(box, SourceText(cand) + " — " + T("Match." + cand.Match));
                box.Click += async (o, a) => await ChooseAlternativeAsync(row, captured);
                flowAlternatives.Controls.Add(box);

                try
                {
                    var img = await GetAlternativeThumbAsync(cand.Url, box.Size);
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

        /// <summary>Vignette 3:4 d'un candidat (decodage et mise a l'echelle de la grille, en tache de fond).</summary>
        private async Task<Bitmap> GetAlternativeThumbAsync(string url, Size size)
        {
            Bitmap thumb;
            string key = url + "|" + size.Width + "x" + size.Height;
            if (_altThumbs.TryGetValue(key, out thumb)) return thumb;
            var data = await GetDataAsync(url, _closeCts.Token);
            thumb = await Task.Run(() =>
            {
                using (var ms = new MemoryStream(data, false))
                using (var img = Image.FromStream(ms))
                    return ThumbnailCache.Scale(img, size.Width, size.Height);
            });
            if (_disposed) { thumb.Dispose(); throw new OperationCanceledException(_closeCts.Token); }
            Bitmap other;
            if (_altThumbs.TryGetValue(key, out other)) { thumb.Dispose(); return other; }
            _altThumbs[key] = thumb;
            return thumb;
        }

        /// <summary>Cadre d'un candidat : Accent (2 px) pour celui qui est choisi, filet Rule sinon.</summary>
        private void PaintAlternativeFrame(Graphics g, PictureBox box, Row row)
        {
            float s = B1Dpi.Scale(box);
            bool chosen = row.Chosen != null && ReferenceEquals(row.Chosen, box.Tag);
            B1Draw.Frame(g, chosen ? B1Palette.Accent : B1Palette.Rule, box.ClientRectangle, B1Draw.Px(chosen ? 2 : 1, s));
        }

        private async Task ChooseAlternativeAsync(Row row, CoverCandidate cand)
        {
            if (_busy || row.Applied) return;
            try
            {
                var data = await GetDataAsync(cand.Url, _closeCts.Token);
                if (_disposed) return;
                row.Chosen = cand;
                row.Data = data;
                row.Failed = false;
                RefreshItem(row);
                row.Item.Checked = true; // choix explicite de l'utilisateur
                RefreshItem(row);
                UpdateSummary(false);
                UpdateButtons();
                foreach (Control c in flowAlternatives.Controls) c.Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("CoverBatchForm: " + ex.Message);
            }
        }

        // ---- application ----

        private async void buttonApply_Click(object sender, EventArgs e)
        {
            await RunApplyAsync();
        }

        /// <summary>
        /// Applique les jaquettes cochees. Une ligne decochee pendant l'application est ignoree mais comptee dans la progression :
        /// la barre finit a 100 %.
        /// </summary>
        internal async Task RunApplyAsync()
        {
            if (_busy) return;
            var targets = VisibleRows().Where(r => !r.Applied && r.Chosen != null && r.Item.Checked).ToList();
            if (targets.Count == 0) return;

            _busy = true;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            UpdateButtons();
            progressBar.Minimum = 0;
            progressBar.Maximum = targets.Count;
            progressBar.Value = 0;
            int done = 0;
            try
            {
                foreach (var r in targets)
                {
                    ct.ThrowIfCancellationRequested();
                    if (r.Item.Checked)  // decochee pendant l'application : ignoree
                    {
                        try
                        {
                            byte[] data = r.Data ?? await GetDataAsync(r.Chosen.Url, ct);
                            string source = r.Chosen.SourceName;
                            var app = r.App;
                            var apply = CoverApplier;
                            // image privee, decodee hors du thread UI, liberee aussitot ecrite
                            await Task.Run(() =>
                            {
                                using (var ms = new MemoryStream(data, false))
                                using (var decoded = Image.FromStream(ms))
                                using (var img = new Bitmap(decoded))
                                    apply(app, img, source);
                            });
                            r.Applied = true;
                            AppliedCount++;
                            _covers.Forget(app);
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Trace.WriteLine("CoverBatchForm: apply failed: " + ex.Message);
                            r.Failed = true;
                        }
                        RefreshItem(r);
                    }
                    done++;
                    ShowProgress("Msg.Applying", done, targets.Count);
                }
                labelStatus.Text = T("Msg.ApplyDone");
                progressBar.Value = progressBar.Maximum;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { labelStatus.Text = T("Msg.ApplyCancelled"); }
            _busy = false;
            UpdateSummary(true);
            UpdateButtons();
        }

        // ---- dessin de la liste (comme la grille) ----

        private void listViewResults_ClientSizeChanged(object sender, EventArgs e)
        {
            FitColumns();
        }

        /// <summary>La premiere colonne prend toute la largeur : la ligne entiere est cliquable, sans barre horizontale.</summary>
        private void FitColumns()
        {
            int w = listViewResults.ClientSize.Width - columnSource.Width - columnMatch.Width - columnState.Width;
            if (w > 0 && columnGame.Width != w) columnGame.Width = w;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FitColumns();
        }

        private void listViewResults_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;   // en-tete masque (HeaderStyle.None)
        }

        private void listViewResults_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = false;  // tout est dessine par DrawItem
        }

        /// <summary>
        /// Zone de la case native (la ou un clic coche) : le glyphe B1 y est dessine. Mesuree une fois par test de position, car
        /// sa place depend de la version de la liste et du DPI.
        /// </summary>
        internal Rectangle CheckBoxBounds(ListViewItem item)
        {
            var b = item.Bounds;
            float s = Dpi;
            int g = B1Draw.GlyphSize(s);
            if (_stateIconLeft < 0 && listViewResults.IsHandleCreated)
            {
                int y = b.Top + b.Height / 2, first = -1, last = -1;
                var icon = item.GetBounds(ItemBoundsPortion.Icon);
                for (int x = b.Left; x < Math.Max(b.Left + 1, icon.Left); x++)
                {
                    var hit = listViewResults.HitTest(x, y);
                    if (hit.Item == item && hit.Location == ListViewHitTestLocations.StateImage) { if (first < 0) first = x; last = x; }
                }
                if (first >= 0) { _stateIconLeft = first - b.Left; _stateIconRight = last + 1 - b.Left; }
            }
            int left = _stateIconLeft >= 0 ? _stateIconLeft : B1Draw.Px(4, s);
            int right = _stateIconLeft >= 0 ? _stateIconRight : left + B1Draw.Px(16, s);
            int cx = b.Left + (left + right) / 2;
            return new Rectangle(cx - g / 2, b.Top + (b.Height - g) / 2, g, g);
        }

        /// <summary>Place de la vignette 3:4 dans la ligne.</summary>
        internal Rectangle ThumbBounds(ListViewItem item)
        {
            var b = item.Bounds;
            var icon = item.GetBounds(ItemBoundsPortion.Icon);
            var size = ThumbSize;
            return new Rectangle(icon.Left + Math.Max(0, (icon.Width - size.Width) / 2), b.Top + (b.Height - size.Height) / 2, size.Width, size.Height);
        }

        private static Color StateColor(State s)
        {
            switch (s)
            {
                case State.Ready:
                case State.Applied: return B1Palette.Lavender;
                case State.ToCheck: return B1Palette.Text;
                case State.Searching: return B1Palette.TextSecondary;
                case State.NetworkError:
                case State.Failed: return B1Palette.Error;
                default: return B1Palette.TextDim;
            }
        }

        private const TextFormatFlags Line = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.Top;

        private void listViewResults_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
            var row = e.Item.Tag as Row;
            var g = e.Graphics;
            float s = Dpi;
            var r = new Rectangle(0, e.Bounds.Top, Math.Max(e.Bounds.Right, listViewResults.ClientSize.Width), e.Bounds.Height);
            bool selected = e.Item.Selected;
            bool rtl = listViewResults.RightToLeft == RightToLeft.Yes;
            B1Draw.Fill(g, selected ? B1Palette.RowActive : B1Palette.Slate, r);
            if (selected) B1Draw.Fill(g, B1Palette.Accent, r.Left, r.Top, B1Draw.Px(2, s), r.Height);
            B1Draw.Fill(g, B1Palette.Rule, r.Left, r.Bottom - B1Draw.Px(1, s), r.Width, B1Draw.Px(1, s));
            if (row == null) return;

            // case B1 a la place de la case native
            var st = e.Item.Checked ? B1VisualState.Checked : B1VisualState.Normal;
            B1Draw.CheckGlyph(g, CheckBoxBounds(e.Item), st, s, false);

            // vignette 3:4 : proposition, sinon jaquette actuelle, sinon hachures « sans jaquette »
            var tr = ThumbBounds(e.Item);
            Image img = null;
            Action<NesApplication> ready = InvalidateApp;
            if (row.Data != null) img = _previews.Get(row.App, tr.Size, ready);
            else if (row.HadCover && !row.Searching) img = _covers.Get(row.App, tr.Size, ready);
            if (img != null) DrawThumb(g, img, tr);
            else PaintHatch(g, tr, s);

            // textes : nom (Text), source · region (TextDim) ; a droite, l'etat (couleur d'etat) et la correspondance (TextDim)
            var font = listViewResults.Font;
            int lh = font.Height, gap = B1Draw.Px(4, s);
            int top = r.Top + (r.Height - 2 * lh - gap) / 2;
            int left = tr.Right + B1Draw.Px(14, s);
            int stateW = B1Draw.Px(StateColumn, s);
            int right = r.Right - B1Draw.Px(12, s);
            var nameRect = new Rectangle(left, top, Math.Max(1, right - stateW - B1Draw.Px(12, s) - left), lh);
            var subRect = new Rectangle(left, top + lh + gap, nameRect.Width, lh);
            var stateRect = new Rectangle(right - stateW, top, stateW, lh);
            var matchRect = new Rectangle(right - stateW, top + lh + gap, stateW, lh);
            var flags = Line | (rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left);
            var flagsRight = Line | TextFormatFlags.Right | (rtl ? TextFormatFlags.RightToLeft : 0);
            TextRenderer.DrawText(g, e.Item.Text, font, nameRect, B1Palette.Text, flags);
            if (row.Chosen != null)
            {
                TextRenderer.DrawText(g, SourceText(row.Chosen), font, subRect, B1Palette.TextDim, flags);
                TextRenderer.DrawText(g, T("Match." + row.Chosen.Match), font, matchRect, B1Palette.TextDim, flagsRight);
            }
            var state = StateOf(row);
            TextRenderer.DrawText(g, T("State." + state), font, stateRect, StateColor(state), flagsRight);
        }

        private static void DrawThumb(Graphics g, Image img, Rectangle r)
        {
            if (img.Width == r.Width && img.Height == r.Height) { g.DrawImageUnscaled(img, r.Location); return; }
            var im = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, r);
            g.InterpolationMode = im;
        }

        /// <summary>Hachures « sans jaquette » de la grille (HatchA/HatchB a 135°, contour pointille Dashed), sans texte a cette taille.</summary>
        private static void PaintHatch(Graphics g, Rectangle r, float s)
        {
            B1Draw.Fill(g, B1Palette.HatchA, r);
            var oldClip = g.Clip;
            g.SetClip(r, CombineMode.Intersect);
            float band = 6f * s * (float)Math.Sqrt(2);
            using (var b = new SolidBrush(B1Palette.HatchB))
                for (float c = band; c < r.Width + r.Height; c += 2 * band)
                {
                    float c2 = c + band;
                    g.FillPolygon(b, new[]
                    {
                        new PointF(r.X + c, r.Y), new PointF(r.X + c2, r.Y),
                        new PointF(r.X + c2 - r.Height, r.Bottom), new PointF(r.X + c - r.Height, r.Bottom),
                    });
                }
            g.Clip = oldClip;
            oldClip.Dispose();
            using (var p = new Pen(B1Palette.Dashed, 1) { DashPattern = new[] { 3f, 3f } })
                g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        private void panelTop_Paint(object sender, PaintEventArgs e)
        {
            var p = (Control)sender;
            float s = B1Dpi.Scale(p);
            B1Draw.Fill(e.Graphics, B1Palette.Rule, 0, p.Height - B1Draw.Px(1, s), p.Width, B1Draw.Px(1, s));
        }

        private void panelRuleTop_Paint(object sender, PaintEventArgs e)
        {
            var p = (Control)sender;
            float s = B1Dpi.Scale(p);
            B1Draw.Fill(e.Graphics, B1Palette.Rule, 0, 0, p.Width, B1Draw.Px(1, s));
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
            if (e.Item == null) return;
            var row = e.Item.Tag as Row;
            if (row != null) row.Checked = e.Item.Checked;
            InvalidateRow(e.Item);
            if (_updating || _busy || !listViewResults.Created) return;
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
            ReleaseResources();
        }

        private void ReleaseResources()
        {
            if (_disposed) return;
            _disposed = true;
            _closeCts.Cancel();
            ClearAlternatives();
            foreach (var img in _altThumbs.Values) img.Dispose();
            _altThumbs.Clear();
            _images.Clear();
            _chosenData.Clear();
            _previews.Dispose();
            _covers.Dispose();
            _thumbs.Dispose();
            _tip.Dispose();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            // fenetre liberee sans avoir ete fermee (tests, galerie) : memes liberations que OnFormClosed
            if (!RecreatingHandle && IsDisposed == false && Disposing) ReleaseResources();
            base.OnHandleDestroyed(e);
        }
    }
}
