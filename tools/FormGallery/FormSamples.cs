using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace FormGallery
{
    /// <summary>
    /// Donnees d'exemple posees sur les fenetres simples avant le rendu (leur Load est retire : sinon elles resteraient vides).
    /// Rien ne touche le reseau ni la console : ce sont des chaines et des images dessinees ici.
    /// </summary>
    internal static class FormSamples
    {
        public static void Fill(Control c)
        {
            switch (c.GetType().Name)
            {
                case "SelectFileForm": AddItems(Find<ListBox>(c, "listBoxFiles"), "Super Metroid (USA).sfc", "The Legend of Zelda - A Link to the Past.sfc", "F-ZERO.sfc"); break;
                case "SelectFirmwareDialog": AddItems(Find<ListBox>(c, "listBoxFirmwares"), "Mini SNES USA 1.0.0", "Mini SNES EUR 1.0.1", "Mini SNES JPN 1.0.0"); break;
                case "SelectSystemDialog": break;                // deja rempli par son constructeur
                case "TechInfo": FillTechInfo(c); break;
                case "ImageGooglerForm": FillGoogler(Find<Control>(c, "imageGoogler")); break;
                case "SpineForm":
                    FillGoogler(Find<Control>(c, "imageGoogler1"));
                    AddItems(Find<ListBox>(c, "listBoxTemplates"), "Super Nintendo", "Super Famicom", "Generic");
                    break;
                case "ImagesForm": FillImages(c); break;
                case "TaskerForm":
                    SetText(c, "statusLabel", "Ecriture du noyau personnalise...");
                    SetValue(Find<Control>(c, "progressBarEx1"), 45);
                    break;
                case "TaskerTransferForm":
                    SetText(c, "statusLabel", "Envoi des jeux vers la console...");
                    SetText(c, "labelFileName", "Super Metroid (USA).sfc");
                    SetText(c, "labelTimeLeft", "00:01:24");
                    SetText(c, "labelTransferRate", "1,8 Mo/s");
                    SetValue(Find<Control>(c, "progressBarEx1"), 62);
                    break;
                case "MessageForm":
                    // construite par MessageForm.Get (Host) ; DrawToBitmap peint les enfants dans l'ordre inverse du plan :
                    // la bande des boutons (Dock Bottom, au premier plan) passe en dernier dans la collection, la mise en page reste la meme
                    var strip = Find<Control>(c, "panel1");
                    if (strip != null) strip.SendToBack();
                    break;
                case "WaitingShellForm":
                    // construite sans son constructeur (Host.DesignerOnlyTypes) : on rejoue sa seule mise en page, le bouton a droite de l'etiquette
                    var labelDriver = Find<Control>(c, "labelDriver"); var buttonDriver = Find<Control>(c, "buttonDriver");
                    if (labelDriver != null && buttonDriver != null) buttonDriver.Left = labelDriver.Left + labelDriver.Width;
                    break;
                case "SnesPresetEditor":
                    // meme raison : en vrai les champs (avant dans la collection) recouvrent la fin des etiquettes « 0x », pas l'inverse
                    foreach (var n in new[] { "textBoxPresetID", "textBoxExtra" }) { var box = Find<Control>(c, n); if (box != null) box.SendToBack(); }
                    break;
                case "ErrorForm":
                    SetText(c, "errorLabel", "La console ne repond pas.");
                    SetText(c, "richTextBox1", "System.TimeoutException: La console ne repond pas." + Environment.NewLine + "   a hakchi.Shell.Execute(...)");
                    break;
                case "TextInfo": Find<TextBox>(c, "textBoxInfo").Text = "Licence GPL v3" + Environment.NewLine + "--------------------------------------------------------------------------------" + Environment.NewLine + "Un exemple de texte long, affiche dans une police a chasse fixe."; break;
            }
        }

        private static T Find<T>(Control root, string name) where T : Control
        {
            return root.Controls.Find(name, true).OfType<T>().FirstOrDefault();
        }

        private static void SetText(Control root, string name, string text)
        {
            var c = Find<Control>(root, name);
            if (c != null) c.Text = text;
        }

        // ProgressODoom.ProgressBarEx (assemblage de hakchi, non reference ici) : propriete Value par reflexion
        private static void SetValue(Control bar, int value)
        {
            var p = bar == null ? null : bar.GetType().GetProperty("Value");
            if (p != null) p.SetValue(bar, value, null);
        }

        private static void AddItems(ListBox l, params string[] items)
        {
            if (l == null) return;
            l.Items.Clear();
            foreach (var i in items) l.Items.Add(i);
            if (l.Items.Count > 0) l.SelectedIndex = 0;
        }

        private static void FillTechInfo(Control c)
        {
            var lv = Find<ListView>(c, "listView1");
            var log = Find<RichTextBox>(c, "logContentsRichTextBox");
            if (lv != null)
            {
                var g1 = new ListViewGroup("Informations generales"); var g2 = new ListViewGroup("Console");
                lv.Groups.AddRange(new[] { g1, g2 });
                lv.Items.Add(new ListViewItem(new[] { "Version :", "3.9.3" }, g1));
                lv.Items.Add(new ListViewItem(new[] { "Mode portable :", "Oui" }, g1));
                lv.Items.Add(new ListViewItem(new[] { "Firmware :", "SNES USA 1.0.0" }, g2));
                lv.Items.Add(new ListViewItem(new[] { "Liaison :", "SSH" }, g2));
            }
            if (log != null) log.Text = "12:00:01  hakchi demarre" + Environment.NewLine + "12:00:02  console non connectee";
        }

        private static void FillGoogler(Control g)
        {
            if (g == null) return;
            var lv = (ListView)g.Controls.Find("listView", true).First();
            var il = (ImageList)g.GetType().GetField("imageList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(g);
            lv.Items.Clear(); il.Images.Clear();
            var colors = new[] { Theme.RowActive, Theme.Rule, Theme.Primary, Theme.Accent, Theme.Dashed };
            for (int i = 0; i < 8; i++)
            {
                var bmp = new Bitmap(il.ImageSize.Width, il.ImageSize.Height);      // reste reference par la liste d'images : jamais Dispose
                using (var gr = Graphics.FromImage(bmp))
                {
                    gr.Clear(colors[i % colors.Length]);
                    using (var p = new Pen(Theme.Lavender)) gr.DrawRectangle(p, 0, 0, bmp.Width - 1, bmp.Height - 1);
                    il.Images.Add(bmp);
                }
                lv.Items.Add(new ListViewItem("Exemple " + (i + 1) + "  204x204", i));
            }
        }

        private static void FillImages(Control c)
        {
            int i = 0;
            foreach (var pb in c.Controls.OfType<PictureBox>().OrderBy(p => p.Left))
            {
                var bmp = new Bitmap(pb.Width, pb.Height);
                using (var gr = Graphics.FromImage(bmp)) gr.Clear(new[] { Theme.RowActive, Theme.Rule, Theme.Primary, Theme.Dashed }[i++ % 4]);
                pb.Image = bmp; pb.Visible = true;
            }
        }

        private static class Theme
        {
            public static readonly Color RowActive = Color.FromArgb(0x26, 0x25, 0x2E), Rule = Color.FromArgb(0x2C, 0x2A, 0x34), Primary = Color.FromArgb(0x4A, 0x3C, 0x8C),
                Accent = Color.FromArgb(0x6B, 0x5B, 0xB5), Dashed = Color.FromArgb(0x5C, 0x58, 0x70), Lavender = Color.FromArgb(0xA7, 0x9F, 0xD6);
        }
    }
}
