using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Shell
{
    [Collection("B1Static")]
    public class SystemNavListTests
    {
        private static SystemNavList Nav(out Form f)
        {
            var nav = new SystemNavList { Bounds = new Rectangle(0, 0, 260, 500) };
            nav.SetSystems(new[] { ("Super Nintendo", 52), ("Arcade", 85), ("Mega Drive", 11) });
            f = ShellFixture.Host(nav, new Size(260, 500));
            return nav;
        }

        [StaFact]
        public void NavRaisesFilterForSystem()
        {
            Form f;
            var nav = Nav(out f);
            using (f)
            {
                var got = new List<GameFilter>();
                nav.FilterChanged += (s, flt) => got.Add(flt);

                ShellFixture.Click(nav, ShellFixture.Center(nav.GetSystemRowBounds("Arcade")));
                Assert.Single(got);
                Assert.Equal("Arcade", got[0].System);
                Assert.Equal(GameCollection.All, got[0].Collection);
                Assert.Equal("Arcade", nav.SelectedSystem);

                ShellFixture.Click(nav, ShellFixture.Center(nav.GetCollectionRowBounds(GameCollection.NoCover)));
                Assert.Equal(2, got.Count);
                Assert.Null(got[1].System);
                Assert.Equal(GameCollection.NoCover, got[1].Collection);

                ShellFixture.Click(nav, ShellFixture.Center(nav.GetAllRowBounds()));
                Assert.Equal(3, got.Count);
                Assert.Null(got[2].System);
                Assert.Equal(GameCollection.All, got[2].Collection);

                // second clic sur la ligne deja active : rien de neuf, pas d'evenement
                ShellFixture.Click(nav, ShellFixture.Center(nav.GetAllRowBounds()));
                Assert.Equal(3, got.Count);
            }
        }

        [StaFact]
        public void NavCountsUpdate()
        {
            Form f;
            var nav = Nav(out f);
            using (f)
            {
                Assert.Equal(52, nav.CountOf("Super Nintendo"));
                Assert.Equal(148, nav.TotalCount);

                nav.SetCollectionCount(GameCollection.NoCover, 23);
                Assert.Equal(23, nav.CountOf(GameCollection.NoCover));

                nav.SetSystems(new[] { ("Super Nintendo", 60), ("Arcade", 85), ("PSP", 7) });
                Assert.Equal(60, nav.CountOf("Super Nintendo"));
                Assert.Equal(152, nav.TotalCount);
                Assert.Equal(7, nav.CountOf("PSP"));
                Assert.Equal(-1, nav.CountOf("Mega Drive"));           // disparu
                Assert.Equal(Rectangle.Empty, nav.GetSystemRowBounds("Mega Drive"));
                Assert.Equal(23, nav.CountOf(GameCollection.NoCover)); // les collections gardent leur compte
            }
        }

        [StaFact]
        public void NavFallsBackToAllWhenSelectedSystemDisappears()
        {
            Form f;
            var nav = Nav(out f);
            using (f)
            {
                nav.SelectSystem("Mega Drive");
                var got = new List<GameFilter>();
                nav.FilterChanged += (s, flt) => got.Add(flt);
                nav.SetSystems(new[] { ("Super Nintendo", 52) });
                Assert.Null(nav.SelectedSystem);
                Assert.Single(got);
                Assert.Null(got[0].System);
            }
        }

        [StaFact]
        public void NavHidesFavoritesBecauseHakchiHasNone()
        {
            Form f;
            var nav = Nav(out f);
            using (f)
            {
                Assert.False(GameFilter.FavoritesAvailable);
                Assert.Equal(Rectangle.Empty, nav.GetCollectionRowBounds(GameCollection.Favorites));
                Assert.NotEqual(Rectangle.Empty, nav.GetCollectionRowBounds(GameCollection.NoCover));
            }
        }

        [StaFact]
        public void NavActiveRowHasFillAndAccentBar()
        {
            Form f;
            var nav = Nav(out f);
            using (f)
            {
                nav.SelectSystem("Arcade");
                var r = nav.GetSystemRowBounds("Arcade");
                using (var bmp = ShellFixture.Draw(nav))
                {
                    int y = r.Y + 2;
                    Assert.Equal(B1Palette.Accent.ToArgb(), bmp.GetPixel(0, y).ToArgb());
                    Assert.Equal(B1Palette.Accent.ToArgb(), bmp.GetPixel(2, y).ToArgb());            // 3 px
                    Assert.Equal(B1Palette.RowActive.ToArgb(), bmp.GetPixel(3, y).ToArgb());
                    Assert.Equal(B1Palette.RowActive.ToArgb(), bmp.GetPixel(120, y).ToArgb());
                    var other = nav.GetSystemRowBounds("Super Nintendo");
                    Assert.Equal(B1Palette.Slate.ToArgb(), bmp.GetPixel(0, other.Y + 2).ToArgb());
                    Assert.Equal(B1Palette.Slate.ToArgb(), bmp.GetPixel(120, other.Y + 2).ToArgb());
                    Assert.Equal(B1Palette.Rule.ToArgb(), bmp.GetPixel(nav.Width - 1, other.Y + 2).ToArgb());   // filet de droite
                }
            }
        }

        [StaFact]
        public void NavKeyboardMovesSelection()
        {
            Form f;
            var nav = Nav(out f);
            using (f)
            {
                var got = new List<GameFilter>();
                nav.FilterChanged += (s, flt) => got.Add(flt);
                ShellFixture.Key(nav, Keys.Down);
                Assert.Equal("Super Nintendo", nav.SelectedSystem);
                ShellFixture.Key(nav, Keys.Down);
                Assert.Equal("Arcade", nav.SelectedSystem);
                ShellFixture.Key(nav, Keys.Up);
                Assert.Equal("Super Nintendo", nav.SelectedSystem);
                Assert.Equal(3, got.Count);
            }
        }

        [StaFact]
        public void NavAccessibleNameFollowsActiveRow()
        {
            Form f;
            var nav = Nav(out f);
            using (f)
            {
                Assert.Equal(AccessibleRole.List, nav.AccessibleRole);
                nav.SelectSystem("Arcade");
                Assert.Contains("Arcade", nav.AccessibleName);
                Assert.Contains("85", nav.AccessibleName);
                nav.SelectCollection(GameCollection.NoCover);
                Assert.Contains(com.clusterrr.hakchi_gui.Properties.Resources.NavNoCover, nav.AccessibleName);
            }
        }

        [StaFact]
        public void NavScalesWithDpi()
        {
            Form f;
            var nav = Nav(out f);
            using (f)
            {
                int h96 = nav.GetAllRowBounds().Height;
                B1Dpi.Override = 1.5f;
                try
                {
                    nav.SetSystems(new[] { ("Super Nintendo", 52) });
                    int h144 = nav.GetAllRowBounds().Height;
                    Assert.InRange(h144, h96 * 3 / 2 - 1, h96 * 3 / 2 + 1);
                }
                finally { B1Dpi.Override = 0f; }
            }
        }
    }
}
