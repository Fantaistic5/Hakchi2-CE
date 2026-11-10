using System.Linq;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Covers;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Grid
{
    public class GameFilterTests
    {
        [StaFact]
        public void FilterOnConsoleMatchesChecked()
        {
            var apps = GridFixture.Apps(6);
            ListView src;
            using (GridFixture.Host(out src, apps, i => i % 2 == 0))
            {
                var f = new GameFilter { Scope = GameScope.OnConsole };
                var matched = src.Items.Cast<ListViewItem>().Where(f.Matches).ToList();
                Assert.Equal(src.Items.Cast<ListViewItem>().Where(it => it.Checked), matched);
                Assert.Equal(3, matched.Count);
                f.Scope = GameScope.Library;
                Assert.Equal(6, src.Items.Cast<ListViewItem>().Count(f.Matches));
            }
        }

        [StaFact]
        public void FilterNoCoverUsesCoverTargetsRule()
        {
            var apps = GridFixture.Apps(8, i => i % 3 == 0);
            ((TestApp)apps[7]).Metadata = null; // metadata absente : NeedsCover dit vrai
            ListView src;
            using (GridFixture.Host(out src, apps))
            {
                var f = new GameFilter { Collection = GameCollection.NoCover };
                foreach (ListViewItem it in src.Items)
                    Assert.Equal(CoverTargets.NeedsCover((NesApplication)it.Tag), f.Matches(it));
                Assert.Equal(apps.Count(CoverTargets.NeedsCover), src.Items.Cast<ListViewItem>().Count(f.Matches));
            }
        }

        [StaFact]
        public void FilterBySystemAndSort()
        {
            HakchiEnv.Init();
            var a = new TestApp("Zelda", null, true, "Nintendo - Super Nintendo Entertainment System");
            var b = new TestApp("Abadox", null, true, "Nintendo - Nintendo Entertainment System");
            var c = new TestApp("Contra", null, true, "Nintendo - Super Nintendo Entertainment System");
            ListView src;
            using (GridFixture.Host(out src, new NesApplication[] { a, b, c }))
            {
                var f = new GameFilter();
                Assert.Equal(new[] { "Zelda", "Abadox", "Contra" }, f.Apply(src).Select(i => i.Text)); // ordre de la source
                f.SortAz = true;
                Assert.Equal(new[] { "Abadox", "Contra", "Zelda" }, f.Apply(src).Select(i => i.Text));
                f.System = GameFilter.SystemOf(a);
                Assert.NotNull(f.System);
                Assert.Equal(new[] { "Contra", "Zelda" }, f.Apply(src).Select(i => i.Text));
            }
        }

        [Fact]
        public void FavoritesCollectionIsUnavailable()
        {
            // hakchi n'a aucune notion de favoris : la collection existe dans l'enum mais l'interface la masque.
            Assert.False(GameFilter.FavoritesAvailable);
        }

        [StaFact]
        public void FilterChangedEventFires()
        {
            var f = new GameFilter();
            int n = 0;
            f.Changed += (s, e) => n++;
            f.Scope = GameScope.OnConsole;
            f.Scope = GameScope.OnConsole; // meme valeur : pas d'evenement
            f.SortAz = true;
            f.System = "x";
            f.Collection = GameCollection.NoCover;
            Assert.Equal(4, n);
        }
    }
}
