using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using hakchi_gui.Tests.Grid;
using hakchi_gui.Tests.Theme;
using Newtonsoft.Json.Linq;
using TeamShinkansen.Scrapers.Covers;
using Xunit;

namespace hakchi_gui.Tests.Forms
{
    /// <summary>
    /// Fenetres de la bibliotheque (Task 8). Construites sans effet de bord : « comme le concepteur » (sans constructeur applicatif :
    /// ni console, ni reseau, ni fichier de jeu), sauf CoverBatchForm, construite par son constructeur sur des jeux factices
    /// (aucun fichier), avec une source de recherche et des telechargements factices (jamais le reseau).
    /// </summary>
    [Collection("B1Static")]
    public class LibraryFormsTests
    {
        public static readonly string[] Names =
        {
            "FoldersManagerForm", "ScraperForm", "SelectCoreDialog", "SaveStateManager", "SelectCoverDialog",
            "ScreenshotForm", "GameImporterForm", "GameGenieCodeForm", "GameGenieCodeAddModForm", "CoverBatchForm",
        };

        public static IEnumerable<object[]> All() { return Names.Select(n => new object[] { n }); }

        private static readonly Assembly Asm = typeof(NesApplication).Assembly;

        private static Type TypeOf(string name) { return Asm.GetType("com.clusterrr.hakchi_gui." + name, true); }

        private static JObject Before(string name)
        {
            return (JObject)JObject.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "library_forms_before.json")))[name];
        }

        private static Form Create(string name)
        {
            if (name == "CoverBatchForm") return Batch(3, new FakeSource()).Form;
            return TaskFormsTests.CreateDesigned(name);
        }

        private static IEnumerable<Control> Tree(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var d in Tree(c)) yield return d;
            }
        }

        private static T Field<T>(object o, string name) where T : class
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (fi != null) return fi.GetValue(o) as T;
            }
            return null;
        }

        // --- les trois tests de la Task 6 -----------------------------------------------------------------------------------

        [StaTheory, MemberData(nameof(All))]
        public void InheritsB1Form(string name)
        {
            Assert.True(typeof(B1Form).IsAssignableFrom(TypeOf(name)), name);
        }

        [StaTheory, MemberData(nameof(All))]
        public void OnlyPaletteColorsAfterSkin(string name)
        {
            int bad, total, worst;
            SimpleFormsTests.MeasurePalette(name, out bad, out total, out worst, clearTypeSafe: true);
            if (total == 0) return;
            double ratio = 1.0 - (double)bad / total;
            Assert.True(ratio >= 0.98, name + " : " + (ratio * 100).ToString("0.00") + " % des pixels dans la palette (" + bad + " hors palette sur " + total + ")");
            Assert.True(worst <= 60, name + " : tuile 32x16 avec " + worst + " pixels hors palette");
        }

        [StaTheory, MemberData(nameof(All))]
        public void SameControlNamesAsBefore(string name)
        {
            var before = Before(name)["names"].Select(x => (string)x).ToList();
            using (var f = Create(name))
            {
                var have = new HashSet<string>();
                for (var t = f.GetType(); t != null && t != typeof(Form); t = t.BaseType)
                    foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) have.Add(fi.Name);
                foreach (var n in before) Assert.True(have.Contains(n), name + " : champ manquant " + n);
            }
        }

        [StaTheory, MemberData(nameof(All))]
        public void SameAcceptCancelButtons(string name)
        {
            var b = Before(name);
            using (var f = Create(name))
            {
                Assert.Equal((string)b["accept"], (f.AcceptButton as Control)?.Name);
                Assert.Equal((string)b["cancel"], (f.CancelButton as Control)?.Name);
            }
        }

        [StaTheory, MemberData(nameof(All))]
        public void PaletteColorProperties(string name)
        {
            using (var f = Create(name))
            {
                Assert.True(Sample.InPalette(f.BackColor) && Sample.InPalette(f.ForeColor), name + " fenetre");
                foreach (var c in Tree(f))
                {
                    if (c.BackColor != Color.Transparent) Assert.True(Sample.InPalette(c.BackColor), name + "." + c.Name + " BackColor " + c.BackColor);
                    Assert.True(Sample.InPalette(c.ForeColor), name + "." + c.Name + " ForeColor " + c.ForeColor);
                    var bb = c as ButtonBase;
                    if (bb != null) Assert.Equal(FlatStyle.Flat, bb.FlatStyle);
                }
            }
        }

        // Boutons d'action : le bouton primaire est le plus a droite de sa rangee.
        [StaTheory, MemberData(nameof(All))]
        public void PrimaryButtonIsRightmostInItsRow(string name)
        {
            using (var f = Create(name))
                foreach (var p in Tree(f).OfType<Button>().Where(x => x.BackColor == B1Palette.Primary))
                    foreach (var o in p.Parent.Controls.OfType<Button>().Where(x => x != p && Math.Abs(x.Top - p.Top) < 4))
                        Assert.True(o.Left < p.Left, name + " : " + o.Name + " est a droite du bouton primaire " + p.Name);
        }

        [StaTheory]
        [InlineData("GameGenieCodeAddModForm", "buttonOk")]
        [InlineData("GameGenieCodeForm", "buttonOk")]
        [InlineData("FoldersManagerForm", "buttonOk")]
        [InlineData("SelectCoverDialog", "buttonAccept")]
        [InlineData("SelectCoreDialog", "buttonApply")]
        [InlineData("ScraperForm", "buttonOk")]
        [InlineData("GameImporterForm", "buttonImport")]
        [InlineData("CoverBatchForm", "buttonApply")]
        public void ValidationButtonIsPrimary(string name, string button)
        {
            using (var f = Create(name))
                Assert.Equal(B1Palette.Primary.ToArgb(), Tree(f).OfType<Button>().Single(x => x.Name == button).BackColor.ToArgb());
        }

        // --- gestionnaire de dossiers ---------------------------------------------------------------------------------------

        [StaFact]
        public void FoldersManagerTreeUsesPalette()
        {
            using (var f = TaskFormsTests.CreateDesigned("FoldersManagerForm"))
            {
                var tree = Tree(f).OfType<TreeView>().Single(t => t.Name == "treeView");
                Assert.Equal(B1Palette.Slate.ToArgb(), tree.BackColor.ToArgb());
                Assert.Equal(B1Palette.Text.ToArgb(), tree.ForeColor.ToArgb());
                Assert.True(Sample.InPalette(tree.LineColor), "LineColor " + tree.LineColor);
                Assert.Equal(TreeViewDrawMode.OwnerDrawText, tree.DrawMode);       // noeuds dessines par le theme : selection RowActive, jamais bleue
                var content = Tree(f).OfType<ListView>().Single(l => l.Name == "listViewContent");
                Assert.True(Sample.InPalette(content.BackColor) && Sample.InPalette(content.ForeColor));

                // rendu : un dossier selectionne et des jeux, aucun pixel hors palette ni bleu systeme
                tree.Dock = DockStyle.None; tree.Parent.Controls.Remove(tree); f.Controls.Add(tree);
                tree.Bounds = new Rectangle(0, 0, 260, 160);
                tree.BeginUpdate();
                var root = tree.Nodes.Add("Ecran d'accueil");
                for (int i = 0; i < 5; i++) root.Nodes.Add("Jeu " + i);
                root.Expand();
                tree.EndUpdate();
                var events = (EventHandlerList)typeof(Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tree, null);
                var key = typeof(TreeView).GetField("EVENT_AFTERSELECT", BindingFlags.NonPublic | BindingFlags.Static);
                if (key != null) events[key.GetValue(null)] = null;               // ShowSelected exige le vrai constructeur
                tree.SelectedNode = root.Nodes[1];
                using (var bmp = new Bitmap(tree.Width, tree.Height))
                {
                    tree.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    int bad = 0, total = 0;
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        int run = 0;                                                    // ClearType frange le texte de pixels bleus isoles :
                        for (int x = 0; x < bmp.Width; x++)                             // une selection systeme est un aplat (suite de pixels)
                        {
                            total++;
                            var c = bmp.GetPixel(x, y);
                            if (!SimpleFormsTests.NearPaletteOrBlend(c, 40)) bad++;
                            run = c.B > 180 && c.R < 80 ? run + 1 : 0;
                            Assert.False(run >= 6, "aplat bleu systeme a " + x + "," + y + " : " + c);
                        }
                    }
                    Assert.True(bad * 50 < total, bad + " pixels hors palette sur " + total);
                }
            }
            // compteur d'elements du dossier : erreur et texte de la palette (plus de rouge ni de couleur systeme)
            var src = File.ReadAllText(Path.Combine(Repo.Root, "hakchi_gui", "FoldersManagerForm.cs"));
            Assert.DoesNotMatch(new Regex(@"labelElementCount\.ForeColor\s*=\s*(Color\.|SystemColors\.)"), src);
            Assert.Matches(new Regex(@"labelElementCount\.ForeColor\s*=\s*[\w.]*B1Palette\.Error;"), src);
            Assert.Matches(new Regex(@"labelElementCount\.ForeColor\s*=\s*[\w.]*B1Palette\.Text;"), src);
        }

        // --- CoverBatchForm ------------------------------------------------------------------------------------------------

        /// <summary>Source factice : jamais le reseau. Repond tout de suite, ou bloque tant que <see cref="Block"/> le demande.</summary>
        internal sealed class FakeSource : ICoverSource
        {
            public readonly ConcurrentDictionary<string, int> Calls = new ConcurrentDictionary<string, int>();
            public volatile Func<GameQuery, bool> Block = q => false;
            public Func<GameQuery, MatchKind> Match = q => MatchKind.Crc;
            public string Name { get { return "fake"; } }
            public bool IsAvailable { get { return true; } }

            public async Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct)
            {
                Calls.AddOrUpdate(q.Title, 1, (k, v) => v + 1);
                if (Block(q)) await Task.Delay(Timeout.Infinite, ct);
                await Task.Yield();
                return new[] { new CoverCandidate { Url = "fake://" + q.Title, SourceName = "fake", Region = "Europe", Match = Match(q), MatchedName = q.Title } };
            }
        }

        internal sealed class BatchFixture
        {
            public CoverBatchForm Form;
            public List<NesApplication> Apps;
            public ListView List;
            public ProgressBar Bar;
            public int Downloads;
            public ListViewItem Item(int i) { return List.Items.Cast<ListViewItem>().Single(it => it.Text == Apps[i].Name); }
        }

        internal static byte[] Png(int w, int h, Color c)
        {
            using (var bmp = new Bitmap(w, h))
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(c);
                using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }
            }
        }

        internal static BatchFixture Batch(int n, FakeSource source, int imageW = 30, int imageH = 40)
        {
            HakchiEnv.Init();
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var fx = new BatchFixture { Apps = GridFixture.Apps(n, i => false) };     // jeux sans fichier ni jaquette perso
            var image = Png(imageW, imageH, B1Palette.Accent);
            var f = new CoverBatchForm(fx.Apps);
            f.ServiceFactory = () => new CoverSearchService(new ICoverSource[] { source });
            f.Downloader = (url, ct) => { Interlocked.Increment(ref fx.Downloads); return Task.FromResult(image); };
            f.CoverApplier = (app, img, s) => { };
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-30000, -30000);
            f.ShowInTaskbar = false;
            var h = f.Handle;
            CreateHandles(f);
            f.PerformLayout();
            fx.Form = f;
            fx.List = Field<ListView>(f, "listViewResults");
            fx.Bar = Field<ProgressBar>(f, "progressBar");
            Assert.Equal(n, fx.List.Items.Count);
            return fx;
        }

        private static void CreateHandles(Control c)
        {
            var h = c.Handle;
            foreach (Control child in c.Controls) CreateHandles(child);
        }

        private static void Wait(Task t, int ms = 15000)
        {
            Assert.True(GridFixture.PumpUntil(() => t.IsCompleted, ms), "tache non terminee");
            if (t.IsFaulted) throw t.Exception.InnerException;
        }

        private static string T(string key) { return new ComponentResourceManager(typeof(CoverBatchForm)).GetString(key); }

        // Projet 1 (parque) : une ligne decochee pendant l'application etait ignoree sans avancer la barre, qui finissait avant 100 %.
        [StaFact]
        public void CoverBatchProgressReachesMaxWhenRowsSkipped()
        {
            var fx = Batch(3, new FakeSource());
            using (fx.Form)
            {
                Wait(fx.Form.RunSearchAsync());
                for (int i = 0; i < 3; i++) fx.Item(i).Checked = true;
                int calls = 0;
                fx.Form.CoverApplier = (app, img, s) =>
                {
                    // pendant l'application de la 1re : l'utilisateur decoche la 2e
                    if (Interlocked.Increment(ref calls) == 1) fx.Form.Invoke(new Action(() => fx.Item(1).Checked = false));
                };
                Wait(fx.Form.RunApplyAsync());
                Assert.Equal(2, fx.Form.AppliedCount);
                Assert.Equal(3, fx.Bar.Maximum);
                Assert.Equal(fx.Bar.Maximum, fx.Bar.Value);
                Assert.Equal(T("State.Applied"), fx.Item(0).SubItems[3].Text);
                Assert.NotEqual(T("State.Applied"), fx.Item(1).SubItems[3].Text);
            }
        }

        // Projet 1 (parque) : une nouvelle recherche apres une annulation repartait de zero et perdait les jaquettes deja trouvees.
        [StaFact]
        public void CoverBatchNewSearchKeepsPreviousResults()
        {
            var source = new FakeSource();
            var fx = Batch(3, source);
            using (fx.Form)
            {
                string first = fx.Apps[0].Name;
                source.Block = q => q.Title != first;                // seule la 1re aboutit, les autres attendent
                var t = fx.Form.RunSearchAsync();
                Assert.True(GridFixture.PumpUntil(() => fx.Item(0).SubItems[1].Text.Length > 0, 10000), "la 1re recherche n'a pas abouti");
                fx.Form.CancelRunning();
                Wait(t, 5000);                                         // l'annulation n'attend pas les sources bloquees
                Assert.Equal("fake · Europe", fx.Item(0).SubItems[1].Text);
                Assert.Equal(T("State.Ready"), fx.Item(0).SubItems[3].Text);
                Assert.Equal(T("State.Cancelled"), fx.Item(1).SubItems[3].Text);
                int downloads = fx.Downloads;

                source.Block = q => false;
                Wait(fx.Form.RunSearchAsync());
                Assert.Equal(1, source.Calls[first]);                 // pas recherchee de nouveau
                Assert.Equal(2, source.Calls[fx.Apps[1].Name]);
                Assert.Equal(2, source.Calls[fx.Apps[2].Name]);
                Assert.Equal(downloads + 2, fx.Downloads);
                Assert.True(fx.Item(0).Checked);                        // resultat et coche conserves
                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal("fake · Europe", fx.Item(i).SubItems[1].Text);
                    Assert.Equal(T("State.Ready"), fx.Item(i).SubItems[3].Text);
                }
                Assert.Equal(fx.Bar.Maximum, fx.Bar.Value);

                // tout est trouve : une nouvelle recherche reprend tous les jeux, comme avant
                Wait(fx.Form.RunSearchAsync());
                Assert.Equal(2, source.Calls[first]);
            }
        }

        // Projet 1 (parque) : les apercus etaient gardes decodes en pleine taille. Ils passent maintenant par ThumbnailCache (vignette 3:4),
        // la fenetre ne garde que les octets telecharges.
        [StaFact]
        public void CoverBatchPreviewsGoThroughThumbnailCache()
        {
            var fx = Batch(2, new FakeSource(), 600, 800);
            using (fx.Form)
            {
                Wait(fx.Form.RunSearchAsync());
                var previews = Field<ThumbnailCache>(fx.Form, "_previews");
                Assert.NotNull(previews);
                var item = fx.Item(0);
                var thumb = fx.Form.ThumbBounds(item);
                Assert.Equal(Math.Round(thumb.Width * 4 / 3.0), thumb.Height, 0);            // 3:4
                fx.List.Invalidate(); fx.List.Update();
                using (var b = new Bitmap(fx.List.Width, fx.List.Height)) fx.List.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size));
                Assert.True(GridFixture.PumpUntil(() => previews.Contains(fx.Apps[0], thumb.Size), 5000), "vignette absente du cache");
                using (var b = new Bitmap(fx.List.Width, fx.List.Height))
                {
                    fx.List.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size));
                    var c = b.GetPixel(thumb.X + thumb.Width / 2, thumb.Y + thumb.Height / 2);
                    Assert.Equal(B1Palette.Accent.ToArgb(), c.ToArgb());
                }
                // aucune image decodee gardee par la fenetre ni par ses lignes : seulement des octets
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var row = typeof(CoverBatchForm).GetNestedType("Row", BindingFlags.NonPublic);
                Assert.DoesNotContain(row.GetFields(flags), fi => typeof(Image).IsAssignableFrom(fi.FieldType));
                Assert.Equal(typeof(Dictionary<string, byte[]>), typeof(CoverBatchForm).GetField("_images", flags).FieldType);
            }
        }

        // La case B1 est dessinee la ou la liste attend le clic de sa case native.
        [StaFact]
        public void CoverBatchCheckGlyphSitsOnNativeCheckBox()
        {
            var fx = Batch(2, new FakeSource());
            using (fx.Form)
            {
                var item = fx.Item(1);
                var box = fx.Form.CheckBoxBounds(item);
                Assert.Equal(B1Draw.GlyphSize(B1Dpi.Scale(fx.List)), box.Width);
                var hit = fx.List.HitTest(box.X + box.Width / 2, box.Y + box.Height / 2);
                Assert.Same(item, hit.Item);
                Assert.Equal(ListViewHitTestLocations.StateImage, hit.Location);
                // la vignette est a droite de la case, sans la chevaucher
                Assert.True(fx.Form.ThumbBounds(item).Left >= box.Right, "vignette sur la case");
            }
        }

        // Recherche groupee : la fenetre est dessinee comme la grille (pas d'en-tete de colonnes, lignes de la hauteur d'une vignette 3:4).
        [StaFact]
        public void CoverBatchListLooksLikeTheGrid()
        {
            var fx = Batch(2, new FakeSource());
            using (fx.Form)
            {
                Assert.True(fx.List.OwnerDraw);
                Assert.Equal(ColumnHeaderStyle.None, fx.List.HeaderStyle);
                Assert.IsType<B1CheckBox>(Field<CheckBox>(fx.Form, "checkBoxIncludeExisting"));
                var item = fx.Item(0);
                Assert.True(item.Bounds.Height >= fx.Form.ThumbBounds(item).Height + 8, "ligne plus basse que la vignette");
                // la ligne occupe toute la largeur : pas de barre horizontale
                Assert.True(item.Bounds.Right <= fx.List.ClientSize.Width && item.Bounds.Right >= fx.List.ClientSize.Width - 2, "largeur " + item.Bounds.Right + " / " + fx.List.ClientSize.Width);
            }
        }
    }
}
