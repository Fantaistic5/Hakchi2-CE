using CommonMark;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui
{
    public partial class Motd : com.clusterrr.hakchi_gui.Theme.B1Form
    {
        // Surcharge B1 du gabarit HTML : fond ardoise, filets de 1 px, texte et liens de la palette (aucune autre couleur).
        // Le gabarit redeclare .markdown-body APRES {0} (fond blanc, bord argent, coins de 10 px) : ces trois proprietes sont en !important.
        private const string DarkCss =
            "html, body { background-color: #1C1B22; scrollbar-base-color: #2C2A34; scrollbar-face-color: #2C2A34; scrollbar-track-color: #26252E; scrollbar-arrow-color: #B6B2C4; scrollbar-shadow-color: #2C2A34; scrollbar-highlight-color: #2C2A34; scrollbar-3dlight-color: #2C2A34; scrollbar-darkshadow-color: #2C2A34; }\n" +
            ".markdown-body { color: #E4E2EA; font-family: \"Segoe UI\", sans-serif; font-size: 14px; background-color: #1C1B22 !important; border: 1px solid #2C2A34 !important; border-radius: 0 !important; }\n" +
            ".markdown-body a { color: #A79FD6; }\n" +
            ".markdown-body h1, .markdown-body h2 { border-bottom: 1px solid #2C2A34; }\n" +
            ".markdown-body h6, .markdown-body blockquote { color: #B6B2C4; border-left-color: #5C5870; }\n" +
            ".markdown-body hr { background-color: #2C2A34; border-bottom-color: #2C2A34; }\n" +
            ".markdown-body code, .markdown-body pre, .markdown-body .highlight pre { background-color: #26252E; color: #E4E2EA; border-radius: 0; }\n" +
            ".markdown-body table th, .markdown-body table td { border: 1px solid #2C2A34; }\n" +
            ".markdown-body table tr, .markdown-body table tr:nth-child(2n) { background-color: #1C1B22; border-top: 1px solid #2C2A34; }\n" +
            ".markdown-body img { background-color: #1C1B22; }\n";

        private string html;
        WebBrowser webBrowser;
        TextBox textBox;
        public Motd(string message)
        {
            InitializeComponent();
            com.clusterrr.hakchi_gui.Theme.B1Skin.MarkPrimary(button1);

            var position = new Point(12, 12);
            var size = new Size(472, 304);

            if (Shared.isWindows)
            {
                webBrowser = new WebBrowser() { Location = position, Size = size, Dock = DockStyle.Fill, Url = new Uri("about:blank") };
                webBrowser.Navigating += webBrowser_Navigating;
                panel1.Controls.Add(webBrowser);
                Color color = com.clusterrr.hakchi_gui.Theme.B1Palette.Slate;
                string text = CommonMarkConverter.Convert(message);
                this.html = String.Format(
                    Properties.Resources.motdTemplateHTML,
                    Properties.Resources.motdTemplateCSS + DarkCss,
                    this.Text,
                    text,
                    $"rgb({color.R},{color.G},{color.B})");
            }
            else
            {
                textBox = new TextBox() { Location = position, Size = size, ReadOnly = true, BackColor = SystemColors.Window, Multiline = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill };
                panel1.Controls.Add(textBox);
                textBox.Text = message;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Motd_Shown(object sender, EventArgs e)
        {
            if (webBrowser != null)
            {
                webBrowser.DocumentText = html;
            }
        }

        private void webBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.ToString() == "about:blank") return;

            //cancel the current event
            e.Cancel = true;

            //this opens the URL in the user's default browser
            Process.Start(e.Url.ToString());
        }
    }
}
