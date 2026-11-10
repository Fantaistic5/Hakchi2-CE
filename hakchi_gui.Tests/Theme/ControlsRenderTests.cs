using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Theme
{
    /// <summary>Rendu des controles B1 (tache 2) : palette seule, echelle, focus, couleur d'erreur reservee a Danger.</summary>
    [Collection("B1Static")]
    public class ControlsRenderTests
    {
        private static readonly B1VisualState[] AllStates =
        {
            B1VisualState.Normal, B1VisualState.Hover, B1VisualState.Hover | B1VisualState.Pressed, B1VisualState.Focus,
            B1VisualState.Disabled, B1VisualState.Checked, B1VisualState.Checked | B1VisualState.Hover,
        };

        private sealed class Case
        {
            public string Name; public Control Control; public Size Size; public B1VisualState State;
            public override string ToString() { return Name + " " + State; }
        }

        // anticrenelage tolere : le pixel est dans la palette ou un melange de deux couleurs de la palette (ClearType melange canal par canal)
        private static bool IsBlend(Color p)
        {
            var pal = B1Palette.All;
            for (int i = 0; i < pal.Count; i++)
                for (int j = i; j < pal.Count; j++)
                {
                    var a = pal[i]; var b = pal[j];
                    if (p.R >= Math.Min(a.R, b.R) - 1 && p.R <= Math.Max(a.R, b.R) + 1 &&
                        p.G >= Math.Min(a.G, b.G) - 1 && p.G <= Math.Max(a.G, b.G) + 1 &&
                        p.B >= Math.Min(a.B, b.B) - 1 && p.B <= Math.Max(a.B, b.B) + 1) return true;
                }
            return false;
        }

        private static List<Case> Cases(float s)
        {
            Func<int, int> P = v => (int)Math.Round(v * s);
            var list = new List<Case>();
            foreach (var st in AllStates)
            {
                bool dis = (st & B1VisualState.Disabled) != 0;
                foreach (B1ButtonKind k in Enum.GetValues(typeof(B1ButtonKind)))
                    list.Add(Make("B1Button." + k, new B1Button { Text = "Enregistrer", Kind = k, Enabled = !dis }, new Size(P(110), P(30)), st));
                list.Add(Make("B1CheckBox", new B1CheckBox { Text = "Option", Checked = (st & B1VisualState.Checked) != 0, Enabled = !dis }, new Size(P(120), P(24)), st));
                list.Add(Make("B1CheckBox.mixed", new B1CheckBox { Text = "Option", ThreeState = true, CheckState = CheckState.Indeterminate, Enabled = !dis }, new Size(P(120), P(24)), st | B1VisualState.Indeterminate));
                list.Add(Make("B1Chip", new B1Chip { Text = "Europe", Checked = (st & B1VisualState.Checked) != 0, Enabled = !dis }, new Size(P(100), P(28)), st));
                var combo = new B1ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = !dis };
                combo.Items.AddRange(new object[] { "Super Nintendo", "Nintendo 64" }); combo.SelectedIndex = 0;
                list.Add(Make("B1ComboBox", combo, new Size(P(200), P(26)), st));
            }
            var group = new B1GroupBox { Text = "Options de flash" };
            group.Controls.Add(new B1CheckBox { Text = "Garder", Location = new Point(P(4), P(26)), Size = new Size(P(120), P(22)) });
            list.Add(Make("B1GroupBox", group, new Size(P(260), P(70)), B1VisualState.Normal));
            list.Add(Make("B1GroupBox.disabled", new B1GroupBox { Text = "Indisponible", Enabled = false }, new Size(P(260), P(70)), B1VisualState.Disabled));
            foreach (var v in new[] { 0, 40, 100 }) list.Add(Make("B1ProgressBar" + v, new B1ProgressBar { Value = v }, new Size(P(240), P(16)), B1VisualState.Normal));
            list.Add(Make("B1ProgressBar.marquee", new B1ProgressBar { Style = ProgressBarStyle.Marquee, PreviewPhase = 0.4f }, new Size(P(240), P(16)), B1VisualState.Normal));
            var tabs = new B1TabControl { PreviewHotTab = 2 };
            foreach (var t in new[] { "General", "Mods", "Avance" }) tabs.TabPages.Add(new TabPage(t) { BackColor = B1Palette.Slate });
            list.Add(Make("B1TabControl", tabs, new Size(P(300), P(90)), B1VisualState.Normal));
            return list;
        }

        private static Case Make(string name, Control c, Size size, B1VisualState st)
        {
            B1Preview.Set(c, st);
            return new Case { Name = name, Control = c, Size = size, State = st };
        }

        private static Bitmap Render(Case k, float s, Form host)
        {
            var c = k.Control;
            c.Bounds = new Rectangle(8, 8, k.Size.Width, k.Size.Height);
            host.Controls.Add(c);
            var tc = c as TabControl;
            if (tc != null) foreach (TabPage p in tc.TabPages) { var h = p.Handle; }
            var bmp = new Bitmap(c.Width, c.Height, PixelFormat.Format24bppRgb);
            c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
            return bmp;
        }

        private static Form Host(float s, Size size)
        {
            var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ShowInTaskbar = false,
                BackColor = B1Palette.Slate, ForeColor = B1Palette.Text, Font = B1Fonts.Body(9f * s), ClientSize = size,
            };
            var h = f.Handle;
            return f;
        }

        private static void CheckPalette(Bitmap bmp, string label, List<string> problems)
        {
            int exact = 0, blend = 0;
            var offenders = new List<string>();
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var p = bmp.GetPixel(x, y);
                    if (Sample.InPalette(Color.FromArgb(255, p.R, p.G, p.B))) exact++;
                    else if (IsBlend(p)) blend++;
                    else if (offenders.Count < 3) offenders.Add(x + "," + y + "=" + p.R.ToString("X2") + p.G.ToString("X2") + p.B.ToString("X2"));
                }
            if (offenders.Count > 0) problems.Add(label + " : pixels hors palette " + string.Join(" ", offenders));
            else if (exact <= blend * 2) problems.Add(label + " : trop de pixels melanges (degrade ?) exacts " + exact + ", melanges " + blend);
        }

        [StaTheory]
        [InlineData(1.0f)]
        [InlineData(1.5f)]
        public void RendersOnlyPaletteColors(float s)
        {
            var problems = new List<string>();
            B1Dpi.Override = s;
            try
            {
                foreach (var k in Cases(s))
                {
                    using (var host = Host(s, new Size(600, 300)))
                    {
                        host.Show();
                        using (var bmp = Render(k, s, host)) CheckPalette(bmp, k.ToString() + " @" + s, problems);
                    }
                    k.Control.Dispose();
                }
            }
            finally { B1Dpi.Override = 0f; }
            Assert.True(problems.Count == 0, string.Join("\n", problems.Take(12)));
        }

        [StaFact]
        public void ScalesWithDpi()
        {
            try
            {
                int h1, h15;
                using (var f = Host(1f, new Size(300, 100)))
                {
                    B1Dpi.Override = 1f;
                    var b = new B1Button { Text = "Enregistrer" };
                    h1 = b.GetPreferredSize(Size.Empty).Height;
                }
                using (var f = Host(1.5f, new Size(300, 100)))
                {
                    B1Dpi.Override = 1.5f;
                    var b = new B1Button { Text = "Enregistrer" };
                    h15 = b.GetPreferredSize(Size.Empty).Height;
                }
                Assert.InRange(h15, (int)Math.Round(h1 * 1.5) - 1, (int)Math.Round(h1 * 1.5) + 1);
                // le filet de focus suit aussi : 2 px a 96 dpi, 3 px a 144
                foreach (var s in new[] { 1f, 1.5f })
                {
                    B1Dpi.Override = s;
                    using (var host = Host(s, new Size(300, 100)))
                    {
                        host.Show();
                        var k = Make("focus", new B1Button { Text = "Ok", Kind = B1ButtonKind.Primary }, new Size((int)(100 * s), (int)(30 * s)), B1VisualState.Focus);
                        using (var bmp = Render(k, s, host))
                        {
                            int off = (int)Math.Round(2 * s);
                            Assert.Equal(B1Palette.Lavender.ToArgb(), bmp.GetPixel(off, bmp.Height / 2).ToArgb());
                            Assert.NotEqual(B1Palette.Lavender.ToArgb(), bmp.GetPixel(off - 1, bmp.Height / 2).ToArgb());
                        }
                    }
                }
            }
            finally { B1Dpi.Override = 0f; }
        }

        private static int Count(Bitmap bmp, Color c)
        {
            int n = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).ToArgb() == c.ToArgb()) n++;
            return n;
        }

        [StaFact]
        public void FocusCueVisible()
        {
            B1Dpi.Override = 1f;
            try
            {
                var makers = new Dictionary<string, Func<Control>>
                {
                    { "B1Button.Primary", () => new B1Button { Text = "Ok", Kind = B1ButtonKind.Primary } },
                    { "B1Button.Secondary", () => new B1Button { Text = "Ok", Kind = B1ButtonKind.Secondary } },
                    { "B1Button.Quiet", () => new B1Button { Text = "Ok", Kind = B1ButtonKind.Quiet } },
                    { "B1Button.Danger", () => new B1Button { Text = "Ok", Kind = B1ButtonKind.Danger } },
                    { "B1CheckBox", () => new B1CheckBox { Text = "Option" } },
                    { "B1Chip", () => new B1Chip { Text = "Europe" } },
                };
                foreach (var kv in makers)
                {
                    int normal, focus;
                    using (var host = Host(1f, new Size(300, 100)))
                    {
                        host.Show();
                        var a = Make(kv.Key, kv.Value(), new Size(120, 30), B1VisualState.Normal);
                        using (var bmp = Render(a, 1f, host)) normal = Count(bmp, B1Palette.Lavender);
                        host.Controls.Remove(a.Control);
                        var b = Make(kv.Key, kv.Value(), new Size(120, 30), B1VisualState.Focus);
                        using (var bmp = Render(b, 1f, host)) focus = Count(bmp, B1Palette.Lavender);
                    }
                    Assert.True(focus > normal + 40, kv.Key + " : le focus doit ajouter un filet Lavender visible (normal " + normal + ", focus " + focus + ")");
                }
                // liste deroulante : le focus change le filet du champ (Accent)
                int accNormal, accFocus;
                using (var host = Host(1f, new Size(300, 100)))
                {
                    host.Show();
                    var c1 = new B1ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; c1.Items.Add("Un"); c1.SelectedIndex = 0;
                    using (var bmp = Render(Make("combo", c1, new Size(160, 26), B1VisualState.Normal), 1f, host)) accNormal = Count(bmp, B1Palette.Accent);
                    host.Controls.Remove(c1);
                    var c2 = new B1ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; c2.Items.Add("Un"); c2.SelectedIndex = 0;
                    using (var bmp = Render(Make("combo", c2, new Size(160, 26), B1VisualState.Focus), 1f, host)) accFocus = Count(bmp, B1Palette.Accent);
                }
                Assert.True(accFocus > accNormal + 40, "B1ComboBox focus : filet Accent attendu");
            }
            finally { B1Dpi.Override = 0f; }
        }

        [StaFact]
        public void DangerOnlyUsesErrorColor()
        {
            B1Dpi.Override = 1f;
            var problems = new List<string>();
            try
            {
                foreach (var k in Cases(1f))
                {
                    using (var host = Host(1f, new Size(600, 300)))
                    {
                        host.Show();
                        using (var bmp = Render(k, 1f, host))
                        {
                            int err = Count(bmp, B1Palette.Error);
                            bool danger = k.Name == "B1Button.Danger" && (k.State & B1VisualState.Disabled) == 0;
                            if (danger && err < 40) problems.Add(k + " : Danger sans filet Error (" + err + ")");
                            if (!danger && k.Name != "B1Button.Danger" && err > 0) problems.Add(k + " : utilise Error (" + err + ")");
                            if (k.Name == "B1Button.Danger" && (k.State & B1VisualState.Disabled) != 0 && err > 0) problems.Add(k + " : desactive utilise Error (" + err + ")");
                        }
                    }
                    k.Control.Dispose();
                }
            }
            finally { B1Dpi.Override = 0f; }
            Assert.True(problems.Count == 0, string.Join("\n", problems.Take(10)));
        }

        // Controles standard habilles par B1Skin : plus aucune partie native hors palette (fleches, glyphes, fonds, bordures).
        [StaFact]
        public void SkinnedStockControlsRenderOnlyPaletteColors()
        {
            var problems = new List<string>();
            Func<Control>[] makers =
            {
                () => new Button { Text = "Secondaire" },
                () => { var b = new Button { Text = "Primaire" }; B1Skin.MarkPrimary(b); return b; },
                () => { var b = new Button { Text = "Effacer" }; B1Skin.SetKind(b, B1ButtonKind.Danger); return b; },
                () => new CheckBox { Text = "Case cochee", Checked = true },
                () => new CheckBox { Text = "Case desactivee", Enabled = false },
                () => new RadioButton { Text = "Radio", Checked = true },
                () => new ProgressBar { Value = 40 },
                () => new NumericUpDown { Value = 42 },
                () => { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; c.Items.Add("Choix"); c.SelectedIndex = 0; return c; },
                () => { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Text = "Edite" }; return c; },
                () => { var g = new GroupBox { Text = "Groupe" }; g.Controls.Add(new CheckBox { Text = "Contenu", Location = new Point(6, 22) }); return g; },
                () => { var t = new TabControl(); t.TabPages.Add("Un"); t.TabPages.Add("Deux"); return t; },
                () => { var l = new ListBox(); l.Items.AddRange(new object[] { "Un", "Deux", "Trois" }); l.SelectedIndex = 1; return l; },
                () => { var l = new ListView { View = View.Details, FullRowSelect = true }; l.Columns.Add("Jeu", 100); l.Items.Add("Un"); l.Items.Add("Deux"); l.Items[1].Selected = true; return l; },
                () => { var t = new TreeView(); t.Nodes.Add("Racine").Nodes.Add("Fils"); t.ExpandAll(); t.SelectedNode = t.Nodes[0].Nodes[0]; return t; },
                () => new TextBox { Text = "Champ" },
            };
            foreach (var make in makers)
            {
                var c = make();
                using (var host = Host(1f, new Size(400, 200)))
                {
                    c.Bounds = new Rectangle(8, 8, c is Button || c is CheckBox || c is RadioButton ? 140 : c is ProgressBar ? 200 : c is NumericUpDown || c is ComboBox || c is TextBox ? 160 : 220,
                                              c is Button ? 30 : c is CheckBox || c is RadioButton ? 24 : c is ProgressBar ? 16 : c is NumericUpDown || c is ComboBox || c is TextBox ? 26 : c is GroupBox ? 70 : 100);
                    host.Controls.Add(c);
                    B1Skin.Apply(host);
                    host.Show();
                    var tc = c as TabControl;
                    if (tc != null) foreach (TabPage p in tc.TabPages) { var h = p.Handle; }
                    foreach (Control ch in c.Controls) { var h = ch.Handle; }
                    var tb = c as TextBox; if (tb != null) tb.Select(0, 0);              // pas de selection : le bleu de selection d'un champ est celui du systeme
                    var bmp = new Bitmap(c.Width, c.Height, PixelFormat.Format24bppRgb);
                    c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
                    CheckPalette(bmp, c.GetType().Name + " " + (c.Text ?? ""), problems);
                    bmp.Dispose();
                }
                c.Dispose();
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
        }
    }
}
