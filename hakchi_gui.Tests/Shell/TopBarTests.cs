using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Shell
{
    [Collection("B1Static")]
    public class TopBarTests
    {
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);

        private static TopBar Bar(out Form f, out ToolStripMenuItem[] menus)
        {
            var bar = new TopBar { Bounds = new Rectangle(0, 0, 1000, 36) };
            menus = new[] { bar.AddMenu("&Jeux"), bar.AddMenu("&Mods"), bar.AddMenu("&Pochettes"), bar.AddMenu("&Outils") };
            foreach (var m in menus) m.DropDownItems.Add("entree");
            f = ShellFixture.Host(bar, new Size(1000, 200));
            f.MainMenuStrip = bar.Menu;
            f.Location = new Point(-30000, -30000);
            ShowWindow(f.Handle, 4);               // Visible vrai (les mnemoniques l'exigent), hors ecran ; les menus sont bornes a l'ecran
            bar.PerformLayout();
            return bar;
        }

        /// <summary>Alt / F10 : le Form entre dans le mode menu du MenuStrip principal (ToolStripManager.ModalMenuFilter, ce que fait ProcessMenuKey), sans clavier reel.</summary>
        private static void PressF10(Form f)
        {
            var filter = typeof(ToolStripManager).GetNestedType("ModalMenuFilter", BindingFlags.NonPublic);
            var set = filter.GetMethod("SetActiveToolStrip", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(ToolStrip), typeof(bool) }, null);
            set.Invoke(null, new object[] { f.MainMenuStrip, true });
        }

        private static void CloseAll(ToolStripMenuItem[] menus) { foreach (var m in menus) m.HideDropDown(); }

        [StaFact]
        public void TopBarHostsRealMenuStripWithItems()
        {
            Form f; ToolStripMenuItem[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            {
                Assert.IsType<MenuStrip>(bar.Menu);
                Assert.Same(f.MainMenuStrip, bar.Menu);
                Assert.Equal(4, bar.MenuCount);
                Assert.Equal("&Jeux", menus[0].Text);
                Assert.Single(menus[0].DropDownItems);
            }
        }

        [StaFact]
        public void MenuActivateFiresWhenTopMenuOpens()
        {
            Form f; ToolStripMenuItem[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            {
                int activated = 0;
                bar.Menu.MenuActivate += (s, e) => activated++;
                try
                {
                    PressF10(f);                       // F10 / Alt : le MenuStrip natif s'active
                    Assert.True(activated >= 1, "MenuActivate");
                    var m = typeof(Control).GetMethod("ProcessDialogChar", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.True((bool)m.Invoke(f, new object[] { 'm' }));
                    Assert.True(menus[1].DropDown.Visible);
                    Assert.True(activated >= 1);
                }
                finally { CloseAll(menus); }
            }
        }

        [StaFact]
        public void AltLetterOpensTheRightMenu()
        {
            Form f; ToolStripMenuItem[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            {
                int opening = 0;
                menus[2].DropDownOpening += (s, e) => opening++;
                try
                {
                    PressF10(f);
                    var m = typeof(Control).GetMethod("ProcessDialogChar", BindingFlags.Instance | BindingFlags.NonPublic);
                    Assert.True((bool)m.Invoke(f, new object[] { 'p' }));
                    Assert.True(menus[2].DropDown.Visible);
                    Assert.False(menus[0].DropDown.Visible);
                    Assert.False(menus[1].DropDown.Visible);
                    Assert.Equal(1, opening);
                }
                finally { CloseAll(menus); }
            }
        }

        [StaFact]
        public void NestedItemDropDownOpeningFires()
        {
            Form f; ToolStripMenuItem[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            {
                var sub = new ToolStripMenuItem("sous-menu");
                sub.DropDownItems.Add("feuille");
                menus[0].DropDownItems.Add(sub);
                int n = 0, top = 0;
                sub.DropDownOpening += (s, e) => n++;
                menus[0].DropDownOpening += (s, e) => top++;
                try
                {
                    menus[0].ShowDropDown();
                    sub.ShowDropDown();
                    Assert.True(top >= 1);
                    Assert.True(n >= 1);
                }
                finally { CloseAll(menus); }
            }
        }

        [StaFact]
        public void ShortcutKeysClickThroughFormProcessCmdKey()
        {
            Form f; ToolStripMenuItem[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            {
                int clicks = 0;
                var item = new ToolStripMenuItem("Rechercher") { ShortcutKeys = Keys.Control | Keys.F };
                item.Click += (s, e) => clicks++;
                menus[0].DropDownItems.Add(item);
                var m = typeof(Form).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
                var msg = Message.Create(f.Handle, 0x100, (IntPtr)(int)Keys.F, IntPtr.Zero);
                var args = new object[] { msg, Keys.Control | Keys.F };
                Assert.True((bool)m.Invoke(f, args));
                Assert.Equal(1, clicks);
            }
        }

        [StaFact]
        public void TopBarMenusInOrderAndBadgeOnTheRight()
        {
            Form f; ToolStripMenuItem[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            {
                bar.Badge.SetState(true, "SNES Mini EUR", "1.0.4");
                bar.PerformLayout();
                for (int i = 1; i < 4; i++) Assert.True(bar.GetMenuBounds(i).Left >= bar.GetMenuBounds(i - 1).Right);
                Assert.True(bar.GetMenuBounds(0).Left > 0);
                Assert.True(bar.Badge.Left > bar.GetMenuBounds(3).Right);
                Assert.InRange(bar.Badge.Right, bar.Width - 40, bar.Width);
            }
        }

        [StaFact]
        public void TopBarPaintsBottomRule()
        {
            Form f; ToolStripMenuItem[] menus;
            var bar = Bar(out f, out menus);
            using (f)
            using (var bmp = ShellFixture.Draw(bar))
            {
                Assert.Equal(B1Palette.Rule.ToArgb(), bmp.GetPixel(900, bar.Height - 1).ToArgb());
                Assert.Equal(B1Palette.Slate.ToArgb(), bmp.GetPixel(900, bar.Height - 2).ToArgb());
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
