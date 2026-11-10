using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;

namespace FormGallery
{
    /// <summary>
    /// Planche des controles B1 (--only Controls) : chaque controle dans chacun de ses etats (normal, survol, appui, focus,
    /// desactive, coche), puis les controles standard habilles par B1Skin. Rendue a 96 et 144 dpi (echelle emulee par B1Dpi).
    /// Fenetre d'essai : n'appartient pas a hakchi.exe.
    /// </summary>
    internal static class ControlsBoard
    {
        public const string Name = "Controls";

        private sealed class CapsLabel : Control
        {
            private readonly float s;
            public CapsLabel(string text, float scale, bool strong)
            {
                Text = text; s = scale; Strong = strong;
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = B1Palette.Slate;
            }
            public bool Strong;
            protected override void OnPaint(PaintEventArgs e)
            {
                var caps = B1Draw.CapsFont(Font);
                B1Draw.Fill(e.Graphics, B1Palette.Slate, ClientRectangle);
                B1Draw.DrawTracked(e.Graphics, Text.ToUpperInvariant(), caps, Strong ? B1Palette.Lavender : B1Palette.TextFaint, 0, 0, B1Fonts.CapsTracking);
            }
        }

        /// <summary>Liste deroulante ouverte (un menu deroulant natif ne se capture pas) : les memes lignes que B1ComboBox.</summary>
        private sealed class OpenList : Control
        {
            private readonly float s;
            private readonly string[] items;
            public OpenList(string[] items, float scale) { this.items = items; s = scale; SetStyle(ControlStyles.OptimizedDoubleBuffer, true); }
            protected override void OnPaint(PaintEventArgs e)
            {
                int rh = Font.Height + B1Draw.Px(4, s);
                B1Draw.Fill(e.Graphics, B1Palette.Slate, ClientRectangle);
                for (int i = 0; i < items.Length; i++)
                    B1Draw.PaintListRow(e.Graphics, new Rectangle(1, 1 + i * rh, Width - 2, rh), items[i], Font, i == 1, true, B1Palette.Slate, false, s);
                B1Draw.Frame(e.Graphics, B1Palette.Rule, ClientRectangle, B1Draw.Px(1, s));
            }
        }

        private static readonly string[] StateNames = { "Normal", "Survol", "Appui", "Focus", "Desactive" };
        private static readonly B1VisualState[] States =
        {
            B1VisualState.Normal, B1VisualState.Hover, B1VisualState.Hover | B1VisualState.Pressed, B1VisualState.Focus, B1VisualState.Disabled,
        };

        public static Form Build(float s)
        {
            Func<int, int> P = v => (int)Math.Round(v * s);
            var f = new Form
            {
                Text = "Controles B1",
                FormBorderStyle = FormBorderStyle.None,
                AutoScaleMode = AutoScaleMode.None,
                BackColor = B1Palette.Slate,
                ForeColor = B1Palette.Text,
                Font = B1Fonts.Body(9f * s),
                ClientSize = new Size(P(1060), P(1010)),
            };
            f.MaximumSize = new Size(6000, 6000);
            f.SuspendLayout();
            Action<Control, int, int, int, int> place = (c, x, y, w, h) => { c.SetBounds(P(x), P(y), P(w), P(h)); f.Controls.Add(c); };
            Func<string, int, int, int, bool, CapsLabel> cap = (t, x, y, w, strong) =>
            {
                var l = new CapsLabel(t, s, strong) { Font = f.Font };
                place(l, x, y, w, 16);
                return l;
            };

            // ---- colonne de gauche : boutons, cases, radios, puces
            int x0 = 24, colw = 112, lab = 92;
            int y = 18;
            cap("Boutons", x0, y, 300, true); y += 24;
            for (int i = 0; i < StateNames.Length; i++) cap(StateNames[i], x0 + lab + i * colw, y, colw, false);
            y += 20;
            var kinds = new[] { B1ButtonKind.Primary, B1ButtonKind.Secondary, B1ButtonKind.Quiet, B1ButtonKind.Danger };
            var kindNames = new[] { "Primary", "Secondary", "Quiet", "Danger" };
            for (int k = 0; k < kinds.Length; k++)
            {
                cap(kindNames[k], x0, y + 8, lab, false);
                for (int i = 0; i < States.Length; i++)
                {
                    var b = new B1Button { Text = k == 3 ? "Effacer" : "Enregistrer", Kind = kinds[k], Enabled = i != 4 };
                    place(b, x0 + lab + i * colw, y, 100, 28);
                    B1Preview.Set(b, States[i]);
                }
                y += 38;
            }
            y += 10;

            cap("Cases et radios", x0, y, 300, true); y += 24;
            for (int i = 0; i < StateNames.Length; i++) cap(StateNames[i], x0 + lab + i * colw, y, colw, false);
            y += 20;
            string[] checkRows = { "Decochee", "Cochee", "Mixte" };
            for (int k = 0; k < 3; k++)
            {
                cap(checkRows[k], x0, y + 3, lab, false);
                for (int i = 0; i < States.Length; i++)
                {
                    var c = new B1CheckBox { Text = "Option", ThreeState = true, CheckState = (CheckState)k, Enabled = i != 4 };
                    place(c, x0 + lab + i * colw, y, 100, 22);
                    B1Preview.Set(c, States[i] | (k == 1 ? B1VisualState.Checked : k == 2 ? B1VisualState.Indeterminate : 0));
                }
                y += 28;
            }
            string[] radioRows = { "Radio off", "Radio on" };
            for (int k = 0; k < 2; k++)
            {
                cap(radioRows[k], x0, y + 3, lab, false);
                for (int i = 0; i < States.Length; i++)
                {
                    var r = new RadioButton { Text = "Choix", Checked = k == 1, Enabled = i != 4 };
                    place(r, x0 + lab + i * colw, y, 100, 22);
                    B1Preview.Set(r, States[i] | (k == 1 ? B1VisualState.Checked : 0));
                }
                y += 28;
            }
            y += 10;

            cap("Puces", x0, y, 300, true); y += 24;
            string[] chipRows = { "Inactive", "Active" };
            for (int k = 0; k < 2; k++)
            {
                cap(chipRows[k], x0, y + 5, lab, false);
                for (int i = 0; i < States.Length; i++)
                {
                    var c = new B1Chip { Text = k == 0 ? "Europe" : "Region", Enabled = i != 4 };
                    place(c, x0 + lab + i * colw, y, 100, 26);
                    B1Preview.Set(c, States[i] | (k == 1 ? B1VisualState.Checked : 0));
                }
                y += 34;
            }
            int leftEnd = y;

            // ---- colonne de droite : listes, groupes, progression, onglets
            int rx = x0 + lab + 5 * colw + 40;           // ~ 676
            int ry = 18;
            cap("Liste deroulante", rx, ry, 300, true); ry += 24;
            string[] names = { "Normal", "Survol", "Focus", "Desactive" };
            B1VisualState[] cs = { B1VisualState.Normal, B1VisualState.Hover, B1VisualState.Focus, B1VisualState.Disabled };
            for (int i = 0; i < 4; i++)
            {
                cap(names[i], rx, ry + 5, 64, false);
                var cb = new B1ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = i != 3 };
                cb.Items.AddRange(new object[] { "Super Nintendo", "Super Famicom", "Nintendo 64" });
                cb.SelectedIndex = 0;
                place(cb, rx + 70, ry, 220, 26);
                B1Preview.Set(cb, cs[i]);
                ry += 32;
            }
            var open = new OpenList(new[] { "Super Nintendo", "Super Famicom", "Nintendo 64" }, s) { Font = f.Font };
            cap("Ouverte", rx, ry + 5, 64, false);
            open.SetBounds(P(rx + 70), P(ry), P(220), 3 * (f.Font.Height + B1Draw.Px(4, s)) + 2);
            f.Controls.Add(open);
            ry += 78;
            cap("Groupes", rx, ry, 300, true); ry += 24;
            var g1 = new B1GroupBox { Text = "Options de flash" };
            g1.Controls.Add(new B1CheckBox { Text = "Garder les sauvegardes", Checked = true, Location = new Point(P(4), P(26)), Size = new Size(P(200), P(22)) });
            place(g1, rx, ry, 300, 62);
            var g2 = new B1GroupBox { Text = "Indisponible", Enabled = false };
            g2.Controls.Add(new B1CheckBox { Text = "Option", Location = new Point(P(4), P(26)), Size = new Size(P(200), P(22)) });
            ry += 72;
            place(g2, rx, ry, 300, 62);
            ry += 72;

            cap("Progression", rx, ry, 300, true); ry += 24;
            int[] vals = { 0, 35, 70, 100 };
            foreach (var v in vals)
            {
                var pb = new B1ProgressBar { Value = v };
                place(pb, rx, ry, 300, 14);
                ry += 18;
            }
            var mq = new B1ProgressBar { Style = ProgressBarStyle.Marquee, PreviewPhase = 0.45f };
            place(mq, rx, ry, 300, 14);
            ry += 28;

            cap("Onglets", rx, ry, 300, true); ry += 24;
            var tabs = new B1TabControl { PreviewHotTab = 2 };
            tabs.TabPages.Add(new TabPage("General") { BackColor = B1Palette.Slate });
            tabs.TabPages.Add(new TabPage("Mods") { BackColor = B1Palette.Slate });
            tabs.TabPages.Add(new TabPage("Avance") { BackColor = B1Palette.Slate });
            tabs.TabPages[0].Controls.Add(new Label { Text = "Contenu de l'onglet", AutoSize = true, Location = new Point(P(8), P(10)) });
            place(tabs, rx, ry, 330, 96);
            ry += 110;
            int rightEnd = ry;

            // ---- section basse : controles standard habilles par B1Skin
            int by = Math.Max(leftEnd, rightEnd) + 8;
            cap("Controles standard habilles (B1Skin)", x0, by, 500, true); by += 26;
            int bx = x0;
            // boutons
            var sb = new Button { Text = "Primaire" }; B1Skin.MarkPrimary(sb); place(sb, bx, by, 100, 28);
            var sb2 = new Button { Text = "Secondaire" }; place(sb2, bx + 108, by, 100, 28);
            var sb3 = new Button { Text = "Discret" }; B1Skin.SetKind(sb3, B1ButtonKind.Quiet); place(sb3, bx + 216, by, 100, 28);
            var sb4 = new Button { Text = "Effacer" }; B1Skin.SetKind(sb4, B1ButtonKind.Danger); place(sb4, bx + 324, by, 100, 28);
            var sb5 = new Button { Text = "Desactive", Enabled = false }; place(sb5, bx + 432, by, 100, 28);
            by += 40;
            // compteurs
            var nu1 = new NumericUpDown { Value = 42 }; place(nu1, bx, by, 90, 24);
            var nu2 = new NumericUpDown { Value = 7 }; place(nu2, bx + 100, by, 90, 24); B1Preview.Set(nu2, B1VisualState.Hover);
            var nu3 = new NumericUpDown { Value = 3, Enabled = false }; place(nu3, bx + 200, by, 90, 24);
            // listes deroulantes standard
            var c1 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }; c1.Items.AddRange(new object[] { "Premier choix", "Deuxieme choix" }); c1.SelectedIndex = 0; place(c1, bx + 310, by, 150, 24);
            var c2 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Text = "Editable" }; c2.Items.Add("Un"); place(c2, bx + 470, by, 130, 24);
            var c3 = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false }; c3.Items.Add("Desactivee"); c3.SelectedIndex = 0; place(c3, bx + 610, by, 130, 24);
            by += 36;
            var ck1 = new CheckBox { Text = "Case cochee", Checked = true }; place(ck1, bx, by, 130, 22);
            var ck2 = new CheckBox { Text = "Case vide" }; place(ck2, bx + 140, by, 130, 22);
            var rd1 = new RadioButton { Text = "Radio actif", Checked = true }; place(rd1, bx + 280, by, 130, 22);
            var rd2 = new RadioButton { Text = "Radio" }; place(rd2, bx + 420, by, 130, 22);
            var tg = new CheckBox { Text = "Bouton bascule", Appearance = Appearance.Button, Checked = true, TextAlign = ContentAlignment.MiddleCenter }; place(tg, bx + 560, by, 130, 26);
            by += 38;

            var lv = new ListView { View = View.Details, FullRowSelect = true };
            lv.Columns.Add("Jeu", 150); lv.Columns.Add("Taille", 80);
            for (int i = 1; i <= 6; i++) lv.Items.Add(new ListViewItem(new[] { "Jeu numero " + i, (i * 313 % 900 + 100) + " Ko" }));
            lv.Items[2].Selected = true;
            place(lv, bx, by, 270, 130);
            var tv = new TreeView { HideSelection = false }; var root = tv.Nodes.Add("Dossiers"); root.Nodes.Add("Action"); root.Nodes.Add("RPG"); root.Expand(); tv.SelectedNode = root.Nodes[0];
            place(tv, bx + 290, by, 180, 130);
            var lb = new ListBox();
            for (int i = 1; i <= 14; i++) lb.Items.Add("Element " + i);
            lb.SelectedIndex = 2;
            place(lb, bx + 490, by, 170, 130);
            var mt = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Text = string.Join("\r\n", new[] { "Ligne 1", "Ligne 2", "Ligne 3", "Ligne 4", "Ligne 5", "Ligne 6", "Ligne 7", "Ligne 8", "Ligne 9", "Ligne 10", "Ligne 11" }) };
            place(mt, bx + 680, by, 160, 130);
            by += 146;

            var gbox = new GroupBox { Text = "Groupe standard" };
            gbox.Controls.Add(new CheckBox { Text = "Contenu", Location = new Point(P(4), P(26)), Size = new Size(P(120), P(22)) });
            place(gbox, bx, by, 270, 70);
            var tabs2 = new TabControl();
            tabs2.TabPages.Add(new TabPage("General")); tabs2.TabPages.Add(new TabPage("Avance"));
            place(tabs2, bx + 290, by, 300, 96);
            by += 110;

            f.ClientSize = new Size(P(1060), P(by + 10));
            f.Tag = f.ClientSize;           // la fenetre sera plafonnee a la hauteur de l ecran une fois creee : la capture garde cette taille
            f.ResumeLayout(true);
            return f;
        }

        /// <summary>Habille comme le ferait B1Form.</summary>
        public static void Skin(Form f)
        {
            B1Skin.Apply(f);
        }

        public static Bitmap Compose(Bitmap a, Bitmap b)
        {
            int gap = 24;
            var bmp = new Bitmap(a.Width + gap + b.Width, Math.Max(a.Height, b.Height), PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(B1Palette.Slate);
                g.DrawImageUnscaled(a, 0, 0);
                g.DrawImageUnscaled(b, a.Width + gap, 0);
            }
            return bmp;
        }
    }
}
