using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Grid
{
    /// <summary>
    /// La grille est une VUE sur la ListView source (listViewGames, invisible) : chaque clic, touche ou coche passe par les
    /// ListViewItem reels, ce sont donc les evenements existants de la ListView qui font le travail metier.
    /// </summary>
    [Collection("B1Static")]
    public class CoverGridViewTests
    {
        /// <summary>Grille dont les touches de modification (Ctrl, Maj) sont imposees par le test.</summary>
        private sealed class TestGrid : CoverGridView
        {
            public Keys Mods;
            protected override Keys CurrentModifiers { get { return Mods; } }
        }

        private static TestGrid Grid(Form f, ListView src)
        {
            var g = new TestGrid { Bounds = new Rectangle(0, 0, 1000, 700) };
            f.Controls.Add(g);
            g.Bind(src);
            var h = g.Handle;
            return g;
        }

        private static Point Center(CoverGridView g, int visibleIndex)
        {
            var r = g.GetItemRect(visibleIndex);
            return new Point(r.X + r.Width / 2, r.Y + r.Height / 3);
        }

        private static void Click(CoverGridView g, Point p, Keys mods = Keys.None, MouseButtons b = MouseButtons.Left)
        {
            var t = (TestGrid)g;
            t.Mods = mods;
            GridFixture.Invoke(g, "OnMouseDown", new MouseEventArgs(b, 1, p.X, p.Y, 0));
            GridFixture.Invoke(g, "OnMouseUp", new MouseEventArgs(b, 1, p.X, p.Y, 0));
            t.Mods = Keys.None;
        }

        private static void Key(CoverGridView g, Keys k)
        {
            GridFixture.Invoke(g, "OnKeyDown", new KeyEventArgs(k));
        }

        [StaFact]
        public void BindForcesHandleOfInvisibleSource()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(3)))
            {
                Assert.False(src.IsHandleCreated);
                var g = Grid(f, src);
                Assert.True(src.IsHandleCreated);
                Assert.False(src.Visible);
                Assert.Equal(3, g.VisibleCount);
            }
        }

        [StaFact]
        public void ClickSelectsSourceItemAndRaisesItemSelectionChanged()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(12)))
            {
                var g = Grid(f, src);
                var log = new List<string>();
                src.ItemSelectionChanged += (s, e) => log.Add(e.ItemIndex + (e.IsSelected ? "+" : "-"));

                Click(g, Center(g, 4));
                Assert.Equal(new[] { "4+" }, log);
                Assert.Equal(new[] { 4 }, src.SelectedIndices.Cast<int>());

                Click(g, Center(g, 7));
                Assert.Equal(new[] { "4+", "4-", "7+" }, log);

                log.Clear();
                Click(g, Center(g, 9), Keys.Control);
                Assert.Equal(new[] { "9+" }, log);
                Assert.Equal(new[] { 7, 9 }, src.SelectedIndices.Cast<int>());

                Click(g, Center(g, 9), Keys.Control); // Ctrl-clic : bascule
                Assert.Equal(new[] { 7 }, src.SelectedIndices.Cast<int>());

                Click(g, Center(g, 7)); // clic simple : 7 seul, ancre sur 7
                Click(g, Center(g, 10), Keys.Shift); // Maj-clic : plage depuis l'ancre (7)
                Assert.Equal(new[] { 7, 8, 9, 10 }, src.SelectedIndices.Cast<int>());

                var empty = g.GetItemRect(11); // clic dans le vide apres le dernier jeu : tout se deselectionne
                Click(g, new Point(empty.Right + 40, empty.Y + 5));
                Assert.Empty(src.SelectedIndices.Cast<int>());
            }
        }

        [StaFact]
        public void SpaceTogglesSourceChecked()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(6), i => i == 2))
            {
                var g = Grid(f, src);
                var checkedLog = new List<string>();
                src.ItemChecked += (s, e) => checkedLog.Add(e.Item.Index + (e.Item.Checked ? "+" : "-"));

                Click(g, Center(g, 1));
                Click(g, Center(g, 2), Keys.Control);
                Key(g, Keys.Space); // une partie seulement est cochee : tout se coche (meme regle que listViewGames_KeyDown)
                Assert.True(src.Items[1].Checked);
                Assert.True(src.Items[2].Checked);
                Assert.Equal(new[] { "1+" }, checkedLog);

                Key(g, Keys.Space); // tout coche : tout se decoche
                Assert.False(src.Items[1].Checked);
                Assert.False(src.Items[2].Checked);
                Assert.Equal(new[] { "1+", "1-", "2-" }, checkedLog);
            }
        }

        [StaFact]
        public void ClickOnSwitchTogglesCheckedWithoutSelecting()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(4)))
            {
                var g = Grid(f, src);
                var sw = g.GetSwitchRect(3);
                Click(g, new Point(sw.X + sw.Width / 2, sw.Y + sw.Height / 2));
                Assert.True(src.Items[3].Checked);
                Assert.Empty(src.SelectedIndices.Cast<int>());
            }
        }

        [StaFact]
        public void CheckedStateOfHiddenItemsSurvivesFilterChange()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(10), i => i < 5))
            {
                var g = Grid(f, src);
                g.Filter = new GameFilter { Scope = GameScope.OnConsole };
                Assert.Equal(5, g.VisibleCount);

                g.Filter.Scope = GameScope.Library;
                Assert.Equal(10, g.VisibleCount);
                // on coche un jeu de la bibliotheque, puis on revient a « sur la console » : il apparait, rien n'est perdu
                Click(g, Center(g, 8));
                Key(g, Keys.Space);
                g.Filter.Scope = GameScope.OnConsole;
                Assert.Equal(6, g.VisibleCount);
                g.Filter.Collection = GameCollection.NoCover; // tous ont une jaquette perso : rien de visible
                Assert.Equal(0, g.VisibleCount);
                Assert.Equal(6, src.CheckedItems.Count); // ce que Synchroniser lira : tous les coches, visibles ou non
                Assert.Equal(new[] { 0, 1, 2, 3, 4, 8 }, src.CheckedIndices.Cast<int>());
                g.Filter.Collection = GameCollection.All;
                Assert.Equal(6, g.VisibleCount);
            }
        }

        [StaFact]
        public void FilterDoesNotRemoveSourceItems()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(10, i => i % 2 == 0)))
            {
                var g = Grid(f, src);
                int changed = 0;
                g.VisibleCountChanged += (s, e) => changed++;
                g.Filter = new GameFilter { Collection = GameCollection.NoCover };
                Assert.Equal(5, g.VisibleCount);
                Assert.Equal(1, changed);
                Assert.Equal(10, src.Items.Count);
                Assert.All(src.Items.Cast<ListViewItem>(), it => Assert.NotNull(it.ListView));
            }
        }

        [StaFact]
        public void ItemsHiddenByFilterAreDeselected()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(6, i => i != 1)))
            {
                var g = Grid(f, src);
                Click(g, Center(g, 0));
                Click(g, Center(g, 1), Keys.Control);
                g.Filter = new GameFilter { Collection = GameCollection.NoCover }; // seul le jeu 1 reste visible
                // une selection invisible serait supprimee par Suppr sans que l'utilisateur la voie
                Assert.Equal(new[] { 1 }, src.SelectedIndices.Cast<int>());
            }
        }

        [StaFact]
        public void CtrlASelectsOnlyVisibleGames()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(6), i => i >= 3))
            {
                var g = Grid(f, src);
                g.Filter = new GameFilter { Scope = GameScope.OnConsole };
                Key(g, Keys.Control | Keys.A);
                Assert.Equal(new[] { 3, 4, 5 }, src.SelectedIndices.Cast<int>());
            }
        }

        [StaFact]
        public void ShortcutsAreRelayedToSource()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(3)))
            {
                var g = Grid(f, src);
                var keys = new List<Keys>();
                src.KeyDown += (s, e) => keys.Add(e.KeyData);
                Click(g, Center(g, 0));
                Key(g, Keys.Delete);
                Key(g, Keys.F2);
                Key(g, Keys.Enter);
                Key(g, Keys.Escape);
                Key(g, Keys.Space); // traite par la grille, pas relaye
                Assert.Equal(new[] { Keys.Delete, Keys.F2, Keys.Enter, Keys.Escape }, keys);
            }
        }

        [StaFact]
        public void ArrowKeysMoveSelection()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(20)))
            {
                var g = Grid(f, src);
                int cols = g.GridLayout.Columns;
                Click(g, Center(g, 0));
                Key(g, Keys.Right);
                Assert.Equal(new[] { 1 }, src.SelectedIndices.Cast<int>());
                Key(g, Keys.Down);
                Assert.Equal(new[] { 1 + cols }, src.SelectedIndices.Cast<int>());
                Key(g, Keys.Shift | Keys.Left);
                Assert.Equal(new[] { cols, 1 + cols }, src.SelectedIndices.Cast<int>());
                Key(g, Keys.End);
                Assert.Equal(new[] { 19 }, src.SelectedIndices.Cast<int>());
            }
        }

        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        private const int WM_CONTEXTMENU = 0x007B, WM_VSCROLL = 0x0115, SB_TOP = 6;

        /// <summary>WM_CONTEXTMENU comme Windows l'envoie : coordonnees ecran, ou -1 pour le clavier (touche Menu, Maj+F10).</summary>
        private static void ContextMenuMessage(CoverGridView g, Point? client)
        {
            IntPtr l = new IntPtr(-1);
            if (client.HasValue)
            {
                var s = g.PointToScreen(client.Value);
                l = new IntPtr((s.Y << 16) | (s.X & 0xFFFF));
            }
            SendMessage(g.Handle, WM_CONTEXTMENU, g.Handle, l);
        }

        /// <summary>« Atteignable » veut dire « s'ouvre vraiment » : WM_CONTEXTMENU fait partir Opening (annule ici, rien ne s'affiche).</summary>
        [StaFact]
        public void ContextMenuOpensOnWmContextMenuWithListViewSelectionRule()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(40)))
            using (var menu = new ContextMenuStrip())
            {
                menu.Items.Add("x");
                int opened = 0;
                menu.Opening += (s, e) => { opened++; e.Cancel = true; };
                var g = Grid(f, src);
                g.ContextMenuStrip = menu;

                // souris sur un jeu non selectionne : il devient la seule selection, et le menu s'ouvre
                Click(g, Center(g, 0));
                ContextMenuMessage(g, Center(g, 2));
                Assert.Equal(1, opened);
                Assert.Same(g, menu.SourceControl);
                Assert.Equal(new[] { 2 }, src.SelectedIndices.Cast<int>());

                // souris sur un jeu deja selectionne : la selection multiple est gardee
                Click(g, Center(g, 0), Keys.Control);
                Assert.Equal(new[] { 0, 2 }, src.SelectedIndices.Cast<int>());
                ContextMenuMessage(g, Center(g, 2));
                Assert.Equal(2, opened);
                Assert.Equal(new[] { 0, 2 }, src.SelectedIndices.Cast<int>());

                // clavier (lParam -1) : le menu s'ouvre sur le jeu qui a le focus, ramene a l'ecran, selection inchangee
                Key(g, Keys.End);
                Assert.Equal(new[] { 39 }, src.SelectedIndices.Cast<int>());
                SendMessage(g.Handle, WM_VSCROLL, new IntPtr(SB_TOP), IntPtr.Zero);
                Assert.False(g.ClientRectangle.Contains(g.GetItemRect(39)), "le jeu 39 devrait etre hors de la vue apres SB_TOP");
                ContextMenuMessage(g, null);
                Assert.Equal(3, opened);
                Assert.True(g.ClientRectangle.Contains(g.GetItemRect(39)), "le menu clavier doit ramener le jeu qui a le focus a l'ecran");
                Assert.Equal(new[] { 39 }, src.SelectedIndices.Cast<int>());
            }
        }

        [StaFact]
        public void RightClickSelectsUnselectedGameOnly()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(3)))
            {
                var g = Grid(f, src);
                // clic droit sur un jeu non selectionne : il devient la selection (comme une ListView)
                Click(g, Center(g, 2), Keys.None, MouseButtons.Right);
                Assert.Equal(new[] { 2 }, src.SelectedIndices.Cast<int>());
            }
        }

        /// <summary>Remplissage complet comme LoadGamesTask.UpdateListView : groupes et items vides puis remplaces.</summary>
        private static List<NesApplication> Refill(ListView src, int n, Func<int, bool> isChecked = null)
        {
            var apps = GridFixture.Apps(n);
            var groups = new[] { new ListViewGroup("Nouveaux"), new ListViewGroup("Super Nintendo") };
            var newItems = apps.Select((a, i) => new ListViewItem(a.ToString()) { Tag = a, Checked = isChecked != null && isChecked(i), Group = groups[i % 2] }).ToArray();
            src.BeginUpdate();
            src.Groups.Clear();
            src.Items.Clear();
            src.Groups.AddRange(groups);
            src.Items.AddRange(newItems);
            src.EndUpdate();
            return apps;
        }

        [StaFact]
        public void SourceRefillAndRemovalUpdateGrid()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(3)))
            {
                var g = Grid(f, src);
                var apps = Refill(src, 7);
                Assert.True(GridFixture.PumpUntil(() => g.VisibleCount == 7));
                // ordre d'une ListView groupee : le groupe « Nouveaux » (items pairs) puis l'autre
                Assert.Equal(new[] { 0, 2, 4, 6, 1, 3, 5 }.Select(i => apps[i]), g.VisibleItems.Select(it => it.Tag));
                Assert.All(g.VisibleItems, it => Assert.Same(src, it.ListView));
                src.Items.RemoveAt(0);
                Assert.True(GridFixture.PumpUntil(() => g.VisibleCount == 6));
                Assert.DoesNotContain(apps[0], g.VisibleItems.Select(it => it.Tag));
                src.Items[0].Text = "Renomme";
                Assert.True(GridFixture.PumpUntil(() => g.VisibleItems.Any(it => it.Text == "Renomme")));
            }
        }

        [StaFact]
        public void OnConsoleScopeFollowsCheckChangesMadeElsewhere()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(5), i => i < 2))
            {
                var g = Grid(f, src);
                g.Filter = new GameFilter { Scope = GameScope.OnConsole };
                Assert.Equal(2, g.VisibleCount);
                src.Items[4].Checked = true; // coche faite ailleurs (menu, gestionnaire de dossiers)
                Assert.True(GridFixture.PumpUntil(() => g.VisibleCount == 3));
                Assert.Contains(src.Items[4], g.VisibleItems);
                src.Items[0].Checked = false;
                Assert.True(GridFixture.PumpUntil(() => g.VisibleCount == 2));
                Assert.DoesNotContain(src.Items[0], g.VisibleItems);
                using (var bmp = Render(g)) { } // le rendu suit sans exception
            }
        }

        [StaFact]
        public void StaleAnchorAndFocusAfterRefillAreDropped()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(8)))
            {
                var g = Grid(f, src);
                Click(g, Center(g, 5));               // ancre et focus sur un item qui va disparaitre
                Refill(src, 8);
                // pas de pompage : la relecture en attente doit etre appliquee avant d'agir
                Click(g, Center(g, 3), Keys.Shift);   // ancre perimee : plage reduite au jeu clique
                Assert.Single(src.SelectedItems.Cast<ListViewItem>());
                Assert.Same(g.VisibleItems[3], src.SelectedItems[0]);
                Key(g, Keys.Right);
                Assert.Same(g.VisibleItems[4], src.SelectedItems[0]);
                Assert.All(src.SelectedItems.Cast<ListViewItem>(), it => Assert.Same(src, it.ListView));
            }
        }

        [StaFact]
        public void GameHiddenByFilterIsDeselectedExactlyOnce()
        {
            // MainForm.listViewGames_ItemSelectionChanged appelle SaveGameItem a chaque deselection : un seul evenement
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(6, i => i != 1), i => i == 1))
            {
                var g = Grid(f, src);
                Click(g, Center(g, 0));
                Click(g, Center(g, 1), Keys.Control);
                var log = new List<string>();
                src.ItemSelectionChanged += (s, e) => log.Add(e.ItemIndex + (e.IsSelected ? "+" : "-"));
                g.Filter.Collection = GameCollection.NoCover; // seul le jeu 1 reste visible
                g.Filter.Scope = GameScope.OnConsole;           // toujours visible (coche)
                g.Filter.SortAz = true;
                Assert.True(GridFixture.PumpUntil(() => true));
                Assert.Equal(new[] { "0-" }, log);
                Assert.Equal(new[] { 1 }, src.SelectedIndices.Cast<int>());
            }
        }

        [StaFact]
        public void ResizeDoesNotRedecodeCovers()
        {
            HakchiEnv.Init();
            var apps = Enumerable.Range(0, 12).Select(i => (NesApplication)TestApp.WithCover("R" + i, Color.FromArgb(20 * i, 90, 140))).ToList();
            ListView src;
            using (var f = GridFixture.Host(out src, apps))
            {
                var g = Grid(f, src);
                var counts = new System.Collections.Concurrent.ConcurrentDictionary<NesApplication, int>();
                g.Cache.Loader = a => { counts.AddOrUpdate(a, 1, (k, v) => v + 1); return a.Image; };
                for (int w = 420; w <= 1700; w += 37)
                {
                    g.Width = w;
                    using (Render(g)) { }
                    Assert.True(GridFixture.PumpUntil(() => g.AllVisibleThumbnailsReady, 5000), "largeur " + w);
                }
                Assert.Equal(12, counts.Count);
                Assert.All(counts.Values, n => Assert.Equal(1, n)); // une seule lecture par jeu, quelle que soit la largeur
            }
        }

        [StaFact]
        public void DpiChangeKeepsPreviousThumbnailUntilNewOneIsReady()
        {
            HakchiEnv.Init();
            var red = Color.FromArgb(0x7A, 0x2E, 0x28);
            ListView src;
            using (var f = GridFixture.Host(out src, new List<NesApplication> { TestApp.WithCover("Rouge", red) }))
            {
                var g = Grid(f, src);
                using (Render(g)) { }
                Assert.True(GridFixture.PumpUntil(() => g.AllVisibleThumbnailsReady));
                try
                {
                    B1Dpi.Override = 1.5f;
                    g.Width += 1; // relayout a la nouvelle echelle
                    Assert.False(g.AllVisibleThumbnailsReady);
                    using (var bmp = Render(g)) // la vignette 1,5x n'est pas prete : l'ancienne est dessinee, pas l'aplat d'attente
                    {
                        var c = g.GetCoverRect(0);
                        Assert.Equal(red.ToArgb(), bmp.GetPixel(c.X + c.Width / 2, c.Y + c.Height / 2).ToArgb());
                    }
                    Assert.True(GridFixture.PumpUntil(() => g.AllVisibleThumbnailsReady));
                }
                finally { B1Dpi.Override = 0f; }
            }
        }

        // --- rendu -------------------------------------------------------------------------------------------

        private static Bitmap Render(CoverGridView g)
        {
            var bmp = new Bitmap(g.Width, g.Height, PixelFormat.Format32bppArgb);
            g.DrawToBitmap(bmp, new Rectangle(0, 0, g.Width, g.Height));
            return bmp;
        }

        private static HashSet<int> Colors(Bitmap b, Rectangle r)
        {
            var set = new HashSet<int>();
            for (int y = r.Top; y < r.Bottom; y++)
                for (int x = r.Left; x < r.Right; x++)
                    set.Add(b.GetPixel(x, y).ToArgb());
            return set;
        }

        [StaFact]
        public void CorruptCoverYieldsHatchedCell()
        {
            HakchiEnv.Init();
            var apps = new List<NesApplication> { TestApp.Corrupt("Casse"), TestApp.WithCover("Rouge", Color.FromArgb(0x7A, 0x2E, 0x28)) };
            ListView src;
            using (var f = GridFixture.Host(out src, apps))
            {
                var g = Grid(f, src);
                using (Render(g)) { } // premier rendu : demande les jaquettes
                Assert.True(GridFixture.PumpUntil(() => g.AllVisibleThumbnailsReady, 10000));
                using (var bmp = Render(g))
                {
                    var cover0 = g.GetCoverRect(0);
                    var inner = Rectangle.Inflate(cover0, -4, -4);
                    inner.Height = inner.Height / 2; // au-dessus du libelle « sans jaquette »
                    var c0 = Colors(bmp, inner);
                    Assert.Contains(B1Palette.HatchA.ToArgb(), c0);
                    Assert.Contains(B1Palette.HatchB.ToArgb(), c0);
                    Assert.Contains(B1Palette.Dashed.ToArgb(), Colors(bmp, new Rectangle(cover0.X, cover0.Y, cover0.Width, 1)));
                    var label = new Rectangle(cover0.X, cover0.Bottom - cover0.Height / 5, cover0.Width, cover0.Height / 5);
                    // texte anticrenele (ClearType) : on cherche des pixels proches de Lavender, pas forcement exacts
                    var lav = B1Palette.Lavender;
                    Assert.Contains(Colors(bmp, label), argb =>
                    {
                        var p = Color.FromArgb(argb);
                        return Math.Abs(p.R - lav.R) + Math.Abs(p.G - lav.G) + Math.Abs(p.B - lav.B) < 60;
                    });

                    var cover1 = Rectangle.Inflate(g.GetCoverRect(1), -4, -4);
                    cover1.Height /= 2;
                    Assert.Equal(new HashSet<int> { Color.FromArgb(0x7A, 0x2E, 0x28).ToArgb() }, Colors(bmp, cover1));
                }
            }
        }

        [StaFact]
        public void SelectionAndSwitchAreDrawn()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(3, i => false), i => i == 1))
            {
                var g = Grid(f, src);
                Click(g, Center(g, 0));
                using (var bmp = Render(g))
                {
                    var c = g.GetCoverRect(0);
                    // contour Lavender de 2 px decale de 3 px
                    Assert.Equal(B1Palette.Lavender.ToArgb(), bmp.GetPixel(c.X + c.Width / 2, c.Y - 4).ToArgb());
                    Assert.Equal(B1Palette.Lavender.ToArgb(), bmp.GetPixel(c.X + c.Width / 2, c.Y - 5).ToArgb());
                    Assert.NotEqual(B1Palette.Lavender.ToArgb(), bmp.GetPixel(c.X + c.Width / 2, c.Y - 3).ToArgb());
                    Assert.NotEqual(B1Palette.Lavender.ToArgb(), bmp.GetPixel(c.X + c.Width / 2, c.Y - 6).ToArgb());
                    // interrupteur strie (Accent / Primary) sur le jeu coche seulement
                    var s1 = Colors(bmp, g.GetSwitchRect(1));
                    Assert.Contains(B1Palette.Accent.ToArgb(), s1);
                    Assert.Contains(B1Palette.Primary.ToArgb(), s1);
                    Assert.DoesNotContain(B1Palette.Accent.ToArgb(), Colors(bmp, g.GetSwitchRect(0)));
                }
            }
        }
    }
}
