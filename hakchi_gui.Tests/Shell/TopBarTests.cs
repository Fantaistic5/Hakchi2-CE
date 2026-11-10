using System.Drawing;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Shell
{
    [Collection("B1Static")]
    public class TopBarTests
    {
        private static TopBar Bar(out Form f, out ToolStripDropDownMenu[] menus)
        {
            var bar = new TopBar { Bounds = new Rectangle(0, 0, 1000, 36) };
            menus = new[] { new ToolStripDropDownMenu(), new ToolStripDropDownMenu(), new ToolStripDropDownMenu(), new ToolStripDropDownMenu() };
            foreach (var m in menus) m.Items.Add("entree");
            bar.AddMenu("Jeux", menus[0]);
            bar.AddMenu("Mods", menus[1]);
            bar.AddMenu("Pochettes", menus[2]);
            bar.AddMenu("Outils", menus[3]);
            f = ShellFixture.Host(bar, new Size(1000, 200));
            return bar;
        }

        [StaFact]
        public void TopBarShowsDropDownOnClick()
        {
            Form f; ToolStripDropDownMenu[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            {
                f.Location = new Point(60, 60);        // le menu deroulant est borne a l'ecran : fenetre jamais affichee mais en zone visible
                try
                {
                    ShellFixture.Click(bar, ShellFixture.Center(bar.GetMenuBounds(1)));
                    Assert.True(menus[1].Visible);
                    Assert.False(menus[0].Visible);
                    var scr = bar.RectangleToScreen(bar.GetMenuBounds(1));
                    Assert.Equal(scr.Left, menus[1].Left);
                    Assert.True(menus[1].Top >= scr.Bottom - 2);
                }
                finally { foreach (var m in menus) m.Close(); }
            }
        }

        [StaFact]
        public void TopBarMenusInOrderAndBadgeOnTheRight()
        {
            Form f; ToolStripDropDownMenu[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            {
                bar.Badge.SetState(true, "SNES Mini EUR", "1.0.4");
                bar.PerformLayout();
                Assert.Equal(4, bar.MenuCount);
                for (int i = 1; i < 4; i++) Assert.True(bar.GetMenuBounds(i).Left >= bar.GetMenuBounds(i - 1).Right);
                Assert.True(bar.GetMenuBounds(0).Left > 0);
                Assert.True(bar.Badge.Left > bar.GetMenuBounds(3).Right);
                Assert.True(bar.Badge.Right <= bar.Width);
                Assert.InRange(bar.Badge.Right, bar.Width - 40, bar.Width);
            }
        }

        [StaFact]
        public void TopBarPaintsBottomRule()
        {
            Form f; ToolStripDropDownMenu[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            using (var bmp = ShellFixture.Draw(bar))
            {
                Assert.Equal(B1Palette.Rule.ToArgb(), bmp.GetPixel(500, bar.Height - 1).ToArgb());
                Assert.Equal(B1Palette.Slate.ToArgb(), bmp.GetPixel(500, bar.Height - 2).ToArgb());
            }
        }

        [StaFact]
        public void BadgeOfflineUsesFaintColor()
        {
            var badge = new ConsoleBadge();
            var f = ShellFixture.Host(badge, new Size(300, 40));
            using (f)
            {
                badge.SetState(true, "SNES Mini EUR", "1.0.4");
                badge.Size = badge.GetPreferredSize(Size.Empty);
                Assert.Equal(B1Palette.Online, badge.DotColor);
                using (var bmp = ShellFixture.Draw(badge))
                {
                    var c = ShellFixture.Center(badge.DotBounds);
                    Assert.Equal(B1Palette.Online.ToArgb(), bmp.GetPixel(c.X, c.Y).ToArgb());
                }
                Assert.Contains("SNES Mini EUR", badge.BadgeText);
                Assert.Contains("1.0.4", badge.BadgeText);

                badge.SetState(false, "", "");
                badge.Size = badge.GetPreferredSize(Size.Empty);
                Assert.Equal(B1Palette.TextFaint, badge.DotColor);
                using (var bmp = ShellFixture.Draw(badge))
                {
                    var c = ShellFixture.Center(badge.DotBounds);
                    Assert.Equal(B1Palette.TextFaint.ToArgb(), bmp.GetPixel(c.X, c.Y).ToArgb());
                }
                Assert.Equal(com.clusterrr.hakchi_gui.Properties.Resources.ConsoleBadgeOffline, badge.BadgeText);
            }
        }
    }
}
