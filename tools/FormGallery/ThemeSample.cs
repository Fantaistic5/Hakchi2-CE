using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;

namespace FormGallery
{
    /// <summary>
    /// Fenetre d'essai du theme B1 : un exemplaire de chaque controle WinForms standard, habille par B1Skin.
    /// Sert a juger le rendu (--only ThemeSample) ; n'appartient pas a hakchi.exe.
    /// </summary>
    internal static class ThemeSample
    {
        public const string Name = "ThemeSample";

        public static Form Build()
        {
            var f = new Form { Text = "Theme B1", ClientSize = new Size(760, 520), AutoScaleMode = AutoScaleMode.Font, AutoScaleDimensions = new SizeF(6F, 13F) };
            f.Font = new Font("Microsoft Sans Serif", 8.25F);

            var menu = new MenuStrip();
            var file = new ToolStripMenuItem("Fichier");
            var open = new ToolStripMenuItem("Ouvrir...") { ShortcutKeys = Keys.Control | Keys.O };
            var recent = new ToolStripMenuItem("Recents");
            recent.DropDownItems.Add(new ToolStripMenuItem("Super Mario World.sfrom"));
            recent.DropDownItems.Add(new ToolStripMenuItem("Zelda ALttP.sfrom"));
            file.DropDownItems.AddRange(new ToolStripItem[] { open, recent, new ToolStripSeparator(), new ToolStripMenuItem("Option cochee") { Checked = true }, new ToolStripMenuItem("Indisponible") { Enabled = false }, new ToolStripSeparator(), new ToolStripMenuItem("Quitter") });
            menu.Items.AddRange(new ToolStripItem[] { file, new ToolStripMenuItem("Edition"), new ToolStripMenuItem("Aide") });

            var tools = new ToolStrip();
            tools.Items.AddRange(new ToolStripItem[] { new ToolStripButton("Synchroniser"), new ToolStripButton("Exporter") { Checked = true }, new ToolStripSeparator(), new ToolStripLabel("Console : en ligne") });

            var status = new StatusStrip();
            status.Items.Add(new ToolStripStatusLabel("Pret"));

            var title = new Label { Text = "Titre de section", Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold), Location = new Point(12, 60), AutoSize = true };
            var hint = new Label { Text = "Texte secondaire en gris systeme", ForeColor = SystemColors.GrayText, Location = new Point(12, 86), AutoSize = true };
            var link = new LinkLabel { Text = "Un lien hypertexte", Location = new Point(12, 106), AutoSize = true };

            var group = new GroupBox { Text = "Options", Location = new Point(12, 130), Size = new Size(230, 130) };
            group.Controls.Add(new CheckBox { Text = "Case cochee", Checked = true, Location = new Point(12, 22), AutoSize = true });
            group.Controls.Add(new CheckBox { Text = "Case vide", Location = new Point(12, 44), AutoSize = true });
            group.Controls.Add(new CheckBox { Text = "Case desactivee", Enabled = false, Location = new Point(12, 66), AutoSize = true });
            group.Controls.Add(new RadioButton { Text = "Choix A", Checked = true, Location = new Point(12, 88), AutoSize = true });
            group.Controls.Add(new RadioButton { Text = "Choix B", Location = new Point(110, 88), AutoSize = true });

            var edit = new TextBox { Text = "Champ de texte", Location = new Point(260, 60), Width = 200 };
            var editRo = new TextBox { Text = "Lecture seule", ReadOnly = true, Location = new Point(260, 88), Width = 200 };
            var combo = new ComboBox { Location = new Point(260, 116), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(new object[] { "Premier choix", "Deuxieme choix" }); combo.SelectedIndex = 0;
            var num = new NumericUpDown { Location = new Point(260, 144), Width = 90, Value = 42 };
            var mask = new MaskedTextBox { Mask = "00/00/0000", Text = "10112026", Location = new Point(360, 144), Width = 100 };

            var ok = new Button { Text = "Enregistrer", Location = new Point(260, 180), Size = new Size(100, 28) };
            var cancel = new Button { Text = "Annuler", Location = new Point(366, 180), Size = new Size(100, 28) };
            var off = new Button { Text = "Desactive", Enabled = false, Location = new Point(260, 214), Size = new Size(100, 28) };
            f.AcceptButton = ok;

            var progress = new ProgressBar { Value = 60, Location = new Point(260, 252), Size = new Size(200, 16) };

            var list = new ListView { View = View.Details, FullRowSelect = true, Location = new Point(490, 60), Size = new Size(258, 160) };
            list.Columns.Add("Jeu", 150); list.Columns.Add("Taille", 90);
            for (int i = 1; i <= 8; i++) list.Items.Add(new ListViewItem(new[] { "Jeu numero " + i, (i * 313 % 900 + 100) + " Ko" }));
            list.Items[2].Selected = true;

            var tree = new TreeView { Location = new Point(490, 230), Size = new Size(258, 100) };
            var root = tree.Nodes.Add("Dossiers"); root.Nodes.Add("Action"); root.Nodes.Add("RPG"); root.Expand();

            var tabs = new TabControl { Location = new Point(12, 280), Size = new Size(448, 190) };
            var p1 = new TabPage("General"); p1.Controls.Add(new Label { Text = "Contenu du premier onglet", Location = new Point(12, 12), AutoSize = true });
            p1.Controls.Add(new ListBox { Location = new Point(12, 40), Size = new Size(200, 90), Items = { "Element un", "Element deux" } });
            tabs.TabPages.Add(p1); tabs.TabPages.Add(new TabPage("Avance")); tabs.TabPages.Add(new TabPage("Mods"));

            var rich = new RichTextBox { Text = "Zone de texte riche\nsur plusieurs lignes", Location = new Point(490, 340), Size = new Size(258, 60) };
            var picture = new PictureBox { Location = new Point(490, 410), Size = new Size(60, 60), BorderStyle = BorderStyle.FixedSingle };

            f.Controls.AddRange(new Control[] { title, hint, link, group, edit, editRo, combo, num, mask, ok, cancel, off, progress, list, tree, tabs, rich, picture, tools, status, menu });
            f.MainMenuStrip = menu;
            return f;
        }

        /// <summary>Applique le theme comme le ferait B1Form.</summary>
        public static void Skin(Form f)
        {
            B1Skin.Apply(f);
            DarkTitleBar.Enable(f);
        }

        /// <summary>Un menu deroulant ouvert, rendu dans un bitmap (un menu contextuel ne se capture pas avec la fenetre).</summary>
        public static Bitmap RenderMenu()
        {
            using (var menu = new ContextMenuStrip())
            {
                var sub = new ToolStripMenuItem("Sous-menu");
                sub.DropDownItems.Add(new ToolStripMenuItem("Element"));
                menu.Items.AddRange(new ToolStripItem[] { new ToolStripMenuItem("Copier") { ShortcutKeys = Keys.Control | Keys.C }, new ToolStripMenuItem("Coller") { Checked = true }, new ToolStripSeparator(), sub, new ToolStripMenuItem("Indisponible") { Enabled = false } });
                B1Skin.Apply(menu);
                var h = menu.Handle;
                menu.PerformLayout();
                menu.Size = menu.GetPreferredSize(Size.Empty);
                menu.PerformLayout();
                menu.Items[1].Select();
                var bmp = new Bitmap(menu.Width, menu.Height, PixelFormat.Format24bppRgb);
                menu.DrawToBitmap(bmp, new Rectangle(0, 0, menu.Width, menu.Height));
                return bmp;
            }
        }
    }
}
