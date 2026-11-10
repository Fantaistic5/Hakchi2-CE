using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using Xunit;

namespace hakchi_gui.Tests.Theme
{
    /// <summary>Une fenetre de test avec un exemplaire de chaque type de controle liste par la tache 1.</summary>
    internal sealed class SampleForm : Form
    {
        public Panel Panel; public Label Label; public LinkLabel Link; public Button Button, AcceptBtn; public TextBox Edit, Multi; public MaskedTextBox Masked;
        public ComboBox Combo; public NumericUpDown Num; public CheckBox Check; public RadioButton Radio; public GroupBox Group;
        public ListView List; public TreeView Tree; public TabControl Tabs; public TabPage Page; public ProgressBar Progress; public PictureBox Picture;
        public MenuStrip Menu; public ToolStrip Tools; public ContextMenuStrip Ctx; public StatusStrip Status; public ListBox ListBox; public RichTextBox Rich;
        public ToolStripMenuItem FileItem, OpenItem, SubItem; public ToolStripButton ToolButton;
        public int Clicks, Checks, Texts, MenuClicks;

        public SampleForm(bool fontAutoScale)
        {
            if (fontAutoScale) { AutoScaleDimensions = new SizeF(6F, 13F); AutoScaleMode = AutoScaleMode.Font; }
            ClientSize = new Size(640, 480);
            Panel = new Panel { Name = "panel", Bounds = new Rectangle(8, 30, 300, 200), BackColor = SystemColors.Control };
            Label = new Label { Name = "label", Text = "Un libelle", AutoSize = false, Bounds = new Rectangle(4, 4, 120, 20) };
            Link = new LinkLabel { Name = "link", Text = "Un lien", AutoSize = false, Bounds = new Rectangle(130, 4, 100, 20) };
            Button = new Button { Name = "button", Text = "OK", Bounds = new Rectangle(4, 30, 80, 26) };
            AcceptBtn = new Button { Name = "accept", Text = "Valider", Bounds = new Rectangle(90, 30, 80, 26) };
            Edit = new TextBox { Name = "textBox", Text = "texte", Bounds = new Rectangle(4, 62, 120, 23) };
            Multi = new TextBox { Name = "multi", Multiline = true, Text = "a\r\nb", Bounds = new Rectangle(130, 62, 120, 40) };
            Masked = new MaskedTextBox { Name = "masked", Mask = "00/00", Bounds = new Rectangle(4, 92, 80, 23) };
            Combo = new ComboBox { Name = "combo", Bounds = new Rectangle(4, 120, 120, 23) }; Combo.Items.AddRange(new object[] { "un", "deux" });
            Num = new NumericUpDown { Name = "num", Bounds = new Rectangle(130, 120, 80, 23) };
            Check = new CheckBox { Name = "check", Text = "Case", AutoSize = false, Bounds = new Rectangle(4, 150, 100, 20) };
            Radio = new RadioButton { Name = "radio", Text = "Radio", AutoSize = false, Bounds = new Rectangle(110, 150, 100, 20) };
            Group = new GroupBox { Name = "group", Text = "Groupe", Bounds = new Rectangle(320, 30, 200, 80) };
            Group.Controls.Add(new Label { Name = "inGroup", Text = "dans", AutoSize = false, Bounds = new Rectangle(8, 20, 100, 20) });
            List = new ListView { Name = "list", View = View.Details, Bounds = new Rectangle(320, 120, 200, 80) };
            List.Columns.Add("Col", 100); List.Items.Add("ligne 1");
            Tree = new TreeView { Name = "tree", Bounds = new Rectangle(320, 210, 200, 80) }; Tree.Nodes.Add("noeud");
            Page = new TabPage { Name = "page", Text = "Onglet" };
            Tabs = new TabControl { Name = "tabs", Bounds = new Rectangle(8, 240, 300, 100) }; Tabs.TabPages.Add(Page);
            Progress = new ProgressBar { Name = "progress", Value = 40, Bounds = new Rectangle(8, 350, 200, 16) };
            Picture = new PictureBox { Name = "picture", Bounds = new Rectangle(220, 350, 40, 40), BackColor = SystemColors.Control };
            ListBox = new ListBox { Name = "listbox", Bounds = new Rectangle(530, 30, 90, 60) }; ListBox.Items.Add("x");
            Rich = new RichTextBox { Name = "rich", Bounds = new Rectangle(530, 100, 90, 60) };

            Menu = new MenuStrip { Name = "menu" };
            FileItem = new ToolStripMenuItem("Fichier") { Name = "fileItem" };
            OpenItem = new ToolStripMenuItem("Ouvrir") { Name = "openItem", Checked = true };
            SubItem = new ToolStripMenuItem("Sous-menu") { Name = "subItem" };
            OpenItem.DropDownItems.Add(SubItem);
            FileItem.DropDownItems.AddRange(new ToolStripItem[] { OpenItem, new ToolStripSeparator(), new ToolStripMenuItem("Quitter") { Enabled = false } });
            Menu.Items.Add(FileItem);
            Tools = new ToolStrip { Name = "tools", Dock = DockStyle.None, Location = new Point(0, 400) };
            ToolButton = new ToolStripButton("Bouton") { Name = "toolButton" };
            Tools.Items.Add(ToolButton);
            Status = new StatusStrip { Name = "status" };
            Status.Items.Add(new ToolStripStatusLabel("pret"));
            Ctx = new ContextMenuStrip { Name = "ctx" };
            Ctx.Items.Add(new ToolStripMenuItem("Copier"));
            Panel.ContextMenuStrip = Ctx;

            Panel.Controls.AddRange(new Control[] { Label, Link, Button, AcceptBtn, Edit, Multi, Masked, Combo, Num, Check, Radio });
            Controls.AddRange(new Control[] { Panel, Group, List, Tree, Tabs, Progress, Picture, ListBox, Rich, Tools, Status, Menu });
            MainMenuStrip = Menu;
            AcceptButton = AcceptBtn;

            Button.Click += (s, e) => Clicks++;
            Check.CheckedChanged += (s, e) => Checks++;
            Edit.TextChanged += (s, e) => Texts++;
            OpenItem.Click += (s, e) => MenuClicks++;
        }
    }

    internal static class Sample
    {
        public static IEnumerable<Control> All(Control root)
        {
            yield return root;
            foreach (Control c in root.Controls)
                foreach (var d in All(c)) yield return d;
        }

        public static IEnumerable<ToolStripItem> Items(ToolStrip strip)
        {
            foreach (ToolStripItem i in strip.Items)
                foreach (var j in Items(i)) yield return j;
        }

        private static IEnumerable<ToolStripItem> Items(ToolStripItem item)
        {
            yield return item;
            var dd = item as ToolStripDropDownItem;
            if (dd != null)
                foreach (ToolStripItem i in dd.DropDownItems)
                    foreach (var j in Items(i)) yield return j;
        }

        /// <summary>Toutes les barres : celles de l'arbre, leurs menus contextuels, et les menus deroulants des elements.</summary>
        public static IEnumerable<ToolStrip> Strips(Control root)
        {
            foreach (var c in All(root))
            {
                var ts = c as ToolStrip;
                if (ts != null) yield return ts;
                if (c.ContextMenuStrip != null) yield return c.ContextMenuStrip;
            }
        }

        public static bool InPalette(Color c) => c.A == 255 && B1Palette.All.Any(p => p.ToArgb() == c.ToArgb());

        public static bool Checked(ToolStripItem i) { var m = i as ToolStripMenuItem; return m != null && m.Checked; }

        public static string Dump(Control root)
        {
            var sb = new StringBuilder();
            foreach (var c in All(root))
            {
                sb.Append(c.GetType().Name).Append('|').Append(c.Name).Append('|').Append(c.BackColor.ToArgb()).Append('|').Append(c.ForeColor.ToArgb())
                  .Append('|').Append(c.Font.Name).Append(c.Font.SizeInPoints).Append(c.Font.Style);
                var b = c as ButtonBase;
                if (b != null) sb.Append('|').Append(b.FlatStyle).Append(b.FlatAppearance.BorderColor.ToArgb()).Append(b.FlatAppearance.MouseOverBackColor.ToArgb())
                                  .Append(b.FlatAppearance.MouseDownBackColor.ToArgb()).Append(b.FlatAppearance.CheckedBackColor.ToArgb()).Append(b.FlatAppearance.BorderSize);
                var tb = c as TextBoxBase;
                if (tb != null) sb.Append('|').Append(tb.BorderStyle);
                sb.Append('|').Append(c.Bounds).AppendLine();
            }
            foreach (var s in Strips(root))
            {
                sb.Append("strip ").Append(s.Name).Append(s.Renderer.GetType().Name).Append(s.BackColor.ToArgb()).Append(s.Font.Name).AppendLine();
                foreach (var i in Items(s))
                    sb.Append("  ").Append(i.Name).Append('|').Append(i.Text).Append('|').Append(i.BackColor.ToArgb()).Append('|').Append(i.ForeColor.ToArgb()).Append('|').Append(i.Font.Name).Append(i.Font.SizeInPoints).AppendLine();
            }
            return sb.ToString();
        }
    }

    [Collection("B1Static")]
    public class B1SkinTests
    {
        [StaFact]
        public void SkinRecolorsEveryStockControl()
        {
            using (var f = new SampleForm(false))
            {
                B1Skin.Apply(f);

                // Un exemplaire de chaque type liste est bien present dans la fenetre de test.
                var types = Sample.All(f).Select(c => c.GetType()).Concat(Sample.Strips(f).Select(s => s.GetType())).ToList();
                foreach (var t in new[] { typeof(SampleForm), typeof(Panel), typeof(Label), typeof(Button), typeof(TextBox), typeof(MaskedTextBox), typeof(ComboBox),
                                          typeof(NumericUpDown), typeof(CheckBox), typeof(RadioButton), typeof(GroupBox), typeof(ListView), typeof(TreeView), typeof(TabControl),
                                          typeof(ProgressBar), typeof(PictureBox), typeof(MenuStrip), typeof(ToolStrip), typeof(ContextMenuStrip) })
                    Assert.Contains(t, types);

                foreach (var c in Sample.All(f))
                {
                    // TabControl ignore BackColor (toujours SystemColors.Control) : ses onglets sont dessines par B1Skin, ses pages sont verifiees ici
                    if (!(c is TabControl))
                        Assert.True(Sample.InPalette(c.BackColor), c.GetType().Name + " " + c.Name + " BackColor " + c.BackColor);
                    Assert.True(Sample.InPalette(c.ForeColor), c.GetType().Name + " " + c.Name + " ForeColor " + c.ForeColor);
                    var fam = c.Font.FontFamily.Name;
                    Assert.True(fam == "Jost" || fam == "Jost Medium" || fam == "Jost SemiBold" || fam == "Segoe UI", c.Name + " police " + fam);
                    var bb = c as ButtonBase;
                    if (bb != null)
                    {
                        Assert.Equal(FlatStyle.Flat, bb.FlatStyle);
                        Assert.True(Sample.InPalette(bb.FlatAppearance.BorderColor), c.Name + " BorderColor");
                        Assert.True(Sample.InPalette(bb.FlatAppearance.MouseOverBackColor), c.Name + " MouseOverBackColor");
                        Assert.True(Sample.InPalette(bb.FlatAppearance.MouseDownBackColor), c.Name + " MouseDownBackColor");
                        Assert.True(Sample.InPalette(bb.FlatAppearance.CheckedBackColor), c.Name + " CheckedBackColor");
                    }
                }
                Assert.True(Sample.InPalette(f.Tree.LineColor));
                Assert.True(Sample.InPalette(f.Link.LinkColor) && Sample.InPalette(f.Link.ActiveLinkColor) && Sample.InPalette(f.Link.VisitedLinkColor) && Sample.InPalette(f.Link.DisabledLinkColor));
                Assert.Equal(B1Palette.Primary.ToArgb(), f.AcceptBtn.BackColor.ToArgb());   // bouton par defaut = primaire
                Assert.NotEqual(B1Palette.Primary.ToArgb(), f.Button.BackColor.ToArgb());
                Assert.Equal(B1Palette.Slate.ToArgb(), f.BackColor.ToArgb());

                // barres de menus / d'outils et leurs elements, y compris les sous-menus
                foreach (var s in Sample.Strips(f))
                {
                    Assert.IsType<B1ToolStripRenderer>(s.Renderer);
                    Assert.True(Sample.InPalette(s.BackColor) && Sample.InPalette(s.ForeColor), s.Name);
                    foreach (var i in Sample.Items(s))
                    {
                        Assert.True(Sample.InPalette(i.BackColor), i.Name + " " + i.Text + " BackColor");
                        Assert.True(Sample.InPalette(i.ForeColor), i.Name + " " + i.Text + " ForeColor");
                    }
                }
                Assert.Contains(f.SubItem, Sample.Items(f.Menu));
                Assert.IsType<B1ToolStripRenderer>(f.FileItem.DropDown.Renderer);
                Assert.IsType<B1ToolStripRenderer>(f.OpenItem.DropDown.Renderer);
                Assert.True(Sample.InPalette(f.FileItem.DropDown.BackColor));
            }
        }

        [StaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void SkinPreservesBehaviour(bool fontAutoScale)
        {
            using (var f = new SampleForm(fontAutoScale))
            {
                f.Tree.Enabled = false; f.Picture.Visible = false; f.Link.Enabled = false; f.Multi.Visible = false;
                var before = Sample.All(f).Select(c => new { c, c.Text, c.Enabled, c.Visible, c.Bounds, c.Dock, c.Anchor, c.TabIndex }).ToList();
                var itemsBefore = Sample.Strips(f).SelectMany(s => Sample.Items(s)).Select(i => new { i, i.Text, i.Enabled, i.Visible, Chk = Sample.Checked(i) }).ToList();

                B1Skin.Apply(f);

                foreach (var b in before)
                {
                    Assert.Equal(b.Text, b.c.Text);
                    Assert.Equal(b.Enabled, b.c.Enabled);
                    Assert.Equal(b.Visible, b.c.Visible);
                    Assert.Equal(b.Dock, b.c.Dock);
                    Assert.Equal(b.Anchor, b.c.Anchor);
                    Assert.Equal(b.TabIndex, b.c.TabIndex);
                }
                // La taille et la position ne bougent pas, sauf la hauteur des champs a une ligne et des barres, qui suit la police.
                Func<Control, bool> heightFollowsFont = c => (c is TextBoxBase && !((TextBoxBase)c).Multiline) || c is ComboBox || c is UpDownBase || c is ToolStrip
                                                             || c.Parent is UpDownBase;       // UpDownEdit / UpDownButtons internes
                foreach (var b in before)
                {
                    if (heightFollowsFont(b.c))
                    {
                        Assert.Equal(b.Bounds.Location, b.c.Bounds.Location);
                        Assert.Equal(b.Bounds.Width, b.c.Bounds.Width);
                    }
                    else
                        Assert.True(b.Bounds == b.c.Bounds, b.c.GetType().Name + " " + b.c.Name + " : " + b.Bounds + " -> " + b.c.Bounds);
                }
                foreach (var b in itemsBefore)
                {
                    Assert.Equal(b.Text, b.i.Text);
                    Assert.Equal(b.Enabled, b.i.Enabled);
                    Assert.Equal(b.Visible, b.i.Visible);
                    Assert.Equal(b.Chk, Sample.Checked(b.i));
                }

                // les evenements branches par le code existant fonctionnent toujours, une seule fois
                // PerformClick exige une fenetre visible : on declenche OnClick directement
                typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(f.Button, new object[] { EventArgs.Empty });
                f.Check.Checked = !f.Check.Checked;
                f.Edit.Text = "autre";
                f.OpenItem.PerformClick();
                Assert.Equal(1, f.Clicks);
                Assert.Equal(1, f.Checks);
                Assert.Equal(1, f.Texts);
                Assert.Equal(1, f.MenuClicks);
            }
        }

        [StaFact]
        public void SkinRespectsSkip()
        {
            using (var f = new SampleForm(false))
            {
                var red = Color.FromArgb(255, 255, 0, 0);
                f.Panel.BackColor = red;
                f.Label.ForeColor = red;
                f.Label.BackColor = red;
                f.Picture.BackColor = red;
                f.Label.Font = new Font("Arial", 11f, FontStyle.Italic);     // police propre (une police heritee suit la fenetre habillee)
                var fontBefore = f.Label.Font;
                var flatBefore = f.Button.FlatStyle;
                B1Skin.Skip(f.Panel);       // la racine d'un sous-arbre ignore : tout le sous-arbre l'est
                B1Skin.Skip(f.Picture);
                B1Skin.Apply(f);
                Assert.Equal(red.ToArgb(), f.Panel.BackColor.ToArgb());
                Assert.Equal(red.ToArgb(), f.Label.ForeColor.ToArgb());
                Assert.Equal(red.ToArgb(), f.Label.BackColor.ToArgb());
                Assert.Same(fontBefore, f.Label.Font);
                Assert.Equal(flatBefore, f.Button.FlatStyle);
                Assert.Equal(red.ToArgb(), f.Picture.BackColor.ToArgb());
                // le reste est habille
                Assert.Equal(B1Palette.Slate.ToArgb(), f.Group.BackColor.ToArgb());
                Assert.Equal(B1Palette.RowActive.ToArgb(), f.Rich.BackColor.ToArgb());
            }
        }

        // B1Form : habille a la creation du handle (fenetre jamais chargee) et reste sans effet de bord
        [StaFact]
        public void B1FormSkinsWhenHandleIsCreated()
        {
            using (var f = new B1Form())
            {
                var b = new Button { Text = "OK" };
                var l = new Label { Text = "x" };
                f.Controls.AddRange(new Control[] { b, l });
                var h = f.Handle;
                Assert.Equal(B1Palette.Slate.ToArgb(), f.BackColor.ToArgb());
                Assert.Equal(FlatStyle.Flat, b.FlatStyle);
                Assert.Equal(B1Palette.Text.ToArgb(), l.ForeColor.ToArgb());
                Assert.Equal("OK", b.Text);
            }
        }

        private static Form ScaledForm(string fontName, float fontPt)
        {
            var f = new Form();
            f.SuspendLayout();                      // comme InitializeComponent : la mise a l'echelle reste en attente jusqu'au premier affichage
            f.AutoScaleDimensions = new SizeF(6F, 13F); f.AutoScaleMode = AutoScaleMode.Font; f.ClientSize = new Size(300, 200);
            f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-30000, -30000); f.ShowInTaskbar = false;
            f.Controls.Add(new Label { Name = "l", AutoSize = false, Text = "abc", Bounds = new Rectangle(10, 10, 100, 20) });
            f.Controls.Add(new Button { Name = "b", Text = "ok", Bounds = new Rectangle(10, 50, 80, 26) });
            f.Controls.Add(new Panel { Name = "p", Bounds = new Rectangle(120, 50, 100, 60) });
            if (fontName != null) f.Font = new Font(fontName, fontPt);
            f.ResumeLayout(true);
            return f;
        }

        // Mise a l'echelle automatique : une fenetre habillee AVANT son premier affichage prend les memes Bounds qu'une jumelle non habillee.
        [StaTheory]
        [InlineData(null, 0f)]
        [InlineData("Segoe UI", 9f)]
        [InlineData("Segoe UI", 12f)]
        public void SkinBeforeShowKeepsNormalAutoScaling(string fontName, float pt)
        {
            using (var twin = ScaledForm(fontName, pt))
            using (var skinned = ScaledForm(fontName, pt))
            {
                B1Skin.Apply(skinned);
                twin.Show(); skinned.Show();
                Application.DoEvents();
                Assert.Equal(twin.ClientSize, skinned.ClientSize);
                foreach (Control c in twin.Controls)
                    Assert.True(c.Bounds == skinned.Controls[c.Name].Bounds, c.Name + " " + c.Bounds + " vs " + skinned.Controls[c.Name].Bounds);
            }
        }

        // Polices fonctionnelles (code, symboles) : conservees, seule la couleur change
        [StaFact]
        public void SkinKeepsNonUiFonts()
        {
            using (var f = new Form())
            using (var mono = new Font("Consolas", 10f))
            using (var sym = new Font("Marlett", 11f))
            using (var courier = new Font("Courier New", 9f, FontStyle.Bold))
            {
                var tb = new TextBox { Multiline = true, Font = mono, Text = "code" };
                var lb = new Label { Font = sym, Text = "a" };
                var rt = new RichTextBox { Font = courier };
                var normal = new Label { Text = "x" };
                f.Controls.AddRange(new Control[] { tb, lb, rt, normal });
                B1Skin.Apply(f);
                Assert.Equal("Consolas", tb.Font.Name); Assert.Equal(10f, tb.Font.SizeInPoints, 1);
                Assert.Equal("Marlett", lb.Font.Name); Assert.Equal(11f, lb.Font.SizeInPoints, 1);
                Assert.Equal("Courier New", rt.Font.Name); Assert.Equal(FontStyle.Bold, rt.Font.Style);
                Assert.True(Sample.InPalette(tb.ForeColor) && Sample.InPalette(tb.BackColor));
                Assert.True(Sample.InPalette(lb.ForeColor));
                Assert.NotEqual("Marlett", normal.Font.Name);       // les polices d'interface, elles, sont converties (ou heritent)
                Assert.StartsWith("Jost", normal.Font.Name);
                Assert.True(B1Fonts.IsUiFamily("Segoe UI") && B1Fonts.IsUiFamily("Microsoft Sans Serif") && B1Fonts.IsUiFamily("Tahoma"));
                Assert.False(B1Fonts.IsUiFamily("Consolas") || B1Fonts.IsUiFamily("Wingdings") || B1Fonts.IsUiFamily("Symbol"));
            }
        }

        private sealed class DesignSite : ISite
        {
            public IComponent Component { get; set; }
            public IContainer Container => null;
            public bool DesignMode => true;
            public string Name { get; set; }
            public object GetService(Type t) => null;
        }

        // Concepteur Visual Studio : B1Form ne doit rien habiller (sinon les valeurs finissent dans le .Designer.cs)
        [StaFact]
        public void B1FormDoesNothingInDesignMode()
        {
            using (var f = new B1Form())
            {
                var b = new Button { Text = "OK" };
                f.Controls.Add(b);
                f.Site = new DesignSite { Component = f };
                var fontBefore = f.Font;
                var h = f.Handle;
                typeof(Form).GetMethod("OnLoad", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(f, new object[] { EventArgs.Empty });
                Assert.Equal(SystemColors.Control.ToArgb(), f.BackColor.ToArgb());
                Assert.NotEqual(FlatStyle.Flat, b.FlatStyle);
                Assert.Same(fontBefore, f.Font);
            }
        }

        // Menu rempli apres Apply : habille a l'ouverture ; renderer par defaut du processus
        [StaFact]
        public void MenuFilledAfterApplyIsSkinnedOnOpening()
        {
            using (var f = new Form())
            {
                var menu = new MenuStrip();
                var top = new ToolStripMenuItem("Regions");
                menu.Items.Add(top);
                f.Controls.Add(menu);
                B1Skin.Apply(f);
                Assert.IsType<B1ToolStripRenderer>(ToolStripManager.Renderer);

                var late = new ToolStripMenuItem("Europe");                 // couleurs systeme : ajoute apres Apply
                late.DropDownItems.Add(new ToolStripMenuItem("France"));
                top.DropDownItems.Add(late);
                Assert.False(Sample.InPalette(late.ForeColor) && Sample.InPalette(late.BackColor));
                top.ShowDropDown();
                try
                {
                    Assert.True(Sample.InPalette(late.ForeColor) && Sample.InPalette(late.BackColor), "Europe " + late.ForeColor + "/" + late.BackColor);
                    Assert.IsType<B1ToolStripRenderer>(top.DropDown.Renderer);
                    Assert.StartsWith("Jost", top.DropDown.Font.Name);
                    late.ShowDropDown();
                    var sub = late.DropDownItems[0];
                    Assert.True(Sample.InPalette(sub.ForeColor) && Sample.InPalette(sub.BackColor));
                }
                finally { top.HideDropDown(); }
            }
        }

        [StaFact]
        public void SkinIsIdempotent()
        {
            using (var f = new SampleForm(false))
            {
                B1Skin.Apply(f);
                var first = Sample.Dump(f);
                B1Skin.Apply(f);
                B1Skin.Apply(f);
                Assert.Equal(first, Sample.Dump(f));
            }
        }

        private static Bitmap RenderMenu(bool withText)
        {
            using (var menu = new ContextMenuStrip())
            {
                Func<string, string> t = s => withText ? s : " ";
                var a = new ToolStripMenuItem(t("Alpha")) { Checked = true };
                var b = new ToolStripMenuItem(t("Beta"));
                b.DropDownItems.Add(new ToolStripMenuItem(t("Sous")));
                var c = new ToolStripMenuItem(t("Gamma")) { Enabled = false };
                var d = new ToolStripMenuItem(t("Delta"));
                if (withText) d.ShortcutKeys = Keys.Control | Keys.S;
                menu.Items.AddRange(new ToolStripItem[] { a, new ToolStripSeparator(), b, c, d });
                B1Skin.Apply(menu);
                var h = menu.Handle;
                menu.PerformLayout();
                menu.Size = menu.GetPreferredSize(Size.Empty);
                menu.PerformLayout();
                b.Select();                       // un element en survol
                var bmp = new Bitmap(menu.Width, menu.Height, PixelFormat.Format24bppRgb);
                menu.DrawToBitmap(bmp, new Rectangle(0, 0, menu.Width, menu.Height));
                var dump = Environment.GetEnvironmentVariable("B1_DUMP_DIR");     // inspection visuelle : B1_DUMP_DIR=<dossier>
                if (!string.IsNullOrEmpty(dump)) bmp.Save(System.IO.Path.Combine(dump, withText ? "menu_text.png" : "menu_notext.png"));
                return bmp;
            }
        }

        // anticrenelage du texte tolere : le pixel est un melange de deux couleurs de la palette
        // (chaque canal entre ceux des deux couleurs, a 1 pres ; ClearType melange canal par canal)
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

        [StaFact]
        public void ToolStripRendererUsesNoGradient()
        {
            // 1) sans texte : chaque pixel est exactement dans la palette (donc ni degrade ni ombre)
            using (var bmp = RenderMenu(false))
            {
                var bad = new Dictionary<int, int>();
                int total = bmp.Width * bmp.Height;
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        var p = bmp.GetPixel(x, y);
                        if (!Sample.InPalette(Color.FromArgb(255, p.R, p.G, p.B)))
                        { int k = p.ToArgb(); int n; bad.TryGetValue(k, out n); bad[k] = n + 1; }
                    }
                int nonPalette = bad.Values.Sum();
                Assert.True(nonPalette == 0, "pixels hors palette : " + nonPalette + "/" + total + " ex. " + string.Join(",", bad.Take(6).Select(kv => (kv.Key & 0xFFFFFF).ToString("X6") + "x" + kv.Value)));
            }
            // 2) avec texte : tout pixel est dans la palette ou un melange (anticrenelage) de deux couleurs de la palette
            using (var bmp = RenderMenu(true))
            {
                int exact = 0, blend = 0;
                var offenders = new List<string>();
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        var p = bmp.GetPixel(x, y);
                        if (Sample.InPalette(Color.FromArgb(255, p.R, p.G, p.B))) exact++;
                        else if (IsBlend(p)) blend++;
                        else if (offenders.Count < 5) offenders.Add(x + "," + y + "=" + p.R.ToString("X2") + p.G.ToString("X2") + p.B.ToString("X2"));
                    }
                Assert.True(offenders.Count == 0, "pixels ni palette ni melange : " + string.Join(" ", offenders));
                Assert.True(exact > blend * 2, "trop de pixels melanges (degrade ?) : exacts " + exact + ", melanges " + blend);
            }
        }
    }
}
