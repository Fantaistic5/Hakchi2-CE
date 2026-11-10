using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
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

        [StaFact]
        public void ContextMenuIsSourcesMenu()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(3)))
            using (var menu = new ContextMenuStrip())
            {
                src.ContextMenuStrip = menu;
                var g = Grid(f, src);
                Assert.Same(menu, g.ContextMenuStrip);
                // clic droit sur un jeu non selectionne : il devient la selection (comme une ListView)
                Click(g, Center(g, 2), Keys.None, MouseButtons.Right);
                Assert.Equal(new[] { 2 }, src.SelectedIndices.Cast<int>());
            }
        }

        [StaFact]
        public void SourceChangesUpdateGrid()
        {
            ListView src;
            using (var f = GridFixture.Host(out src, GridFixture.Apps(3)))
            {
                var g = Grid(f, src);
                // comme LoadGamesTask.UpdateListView : BeginUpdate, Clear, AddRange, EndUpdate
                src.BeginUpdate();
                src.Items.Clear();
                src.Items.AddRange(GridFixture.Apps(7).Select(a => new ListViewItem(a.ToString()) { Tag = a }).ToArray());
                src.EndUpdate();
                Assert.True(GridFixture.PumpUntil(() => g.VisibleCount == 7));
                src.Items.RemoveAt(0);
                Assert.True(GridFixture.PumpUntil(() => g.VisibleCount == 6));
                src.Items[0].Checked = true; // coche faite ailleurs (menu, gestionnaire de dossiers) : la grille suit
                Assert.True(GridFixture.PumpUntil(() => true));
                Assert.True(src.Items[0].Checked);
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
