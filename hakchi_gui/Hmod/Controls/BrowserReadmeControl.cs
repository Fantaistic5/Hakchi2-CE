using CommonMark;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Hmod.Controls
{
    public partial class BrowserReadmeControl : UserControl, IReadmeControl
    {
        private HmodReadme Readme;

        /// <summary>Repere de la feuille B1 (une seule fois par page).</summary>
        internal const string B1CssMarker = "/* B1 readme */";

        /// <summary>
        /// Feuille B1 ajoutee apres la feuille du gabarit, dans le meme bloc &lt;style&gt; (a specificite egale, elle l'emporte) :
        /// fond Slate, texte Text, liens Lavender, filets Rule de 1 px, police Jost (embarquee en @font-face) avec repli Segoe UI.
        /// Elle ne touche qu'a l'apparence : le contenu du readme (converti par CommonMark) est insere tel quel, comme avant.
        /// Meme principe que Motd.DarkCss.
        /// </summary>
        internal static readonly string B1Css = BuildB1Css();

        /// <summary>Derniere page donnee au navigateur (tests).</summary>
        internal string LastHtml { get; private set; }

        private static string FontFace(string file, int weight)
        {
            try
            {
                using (var s = typeof(BrowserReadmeControl).Assembly.GetManifestResourceStream("com.clusterrr.hakchi_gui.Theme.Fonts." + file))
                {
                    if (s == null) return "";
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        return "@font-face { font-family: 'B1 Jost'; font-style: normal; font-weight: " + weight + "; src: url(data:application/x-font-ttf;base64," +
                            Convert.ToBase64String(ms.ToArray()) + ") format('truetype'); }\n";
                    }
                }
            }
            catch (Exception) { return ""; }       // police absente : repli Segoe UI
        }

        private static string BuildB1Css()
        {
            var sb = new StringBuilder();
            sb.Append("\n" + B1CssMarker + "\n");
            sb.Append(FontFace("Jost-Medium.ttf", 500));
            sb.Append(FontFace("Jost-SemiBold.ttf", 600));
            sb.Append(
                "html, body { background-color: #1C1B22 !important; color: #E4E2EA; scrollbar-base-color: #2C2A34; scrollbar-face-color: #2C2A34; scrollbar-track-color: #1C1B22; scrollbar-arrow-color: #B6B2C4; scrollbar-shadow-color: #2C2A34; scrollbar-highlight-color: #2C2A34; scrollbar-3dlight-color: #2C2A34; scrollbar-darkshadow-color: #2C2A34; }\n" +
                "body { font-family: 'B1 Jost', 'Segoe UI', sans-serif; font-weight: 500; font-size: 13px; line-height: 1.5; padding: 0 12px; }\n" +
                "#content { color: #E4E2EA; }\n" +
                "h1, h2, h3, h4, h5, h6 { color: #E4E2EA; font-family: 'B1 Jost', 'Segoe UI', sans-serif; font-weight: 600; }\n" +
                "h1 { font-size: 20px; } h2 { font-size: 16px; } h3 { font-size: 14px; } h4, h5, h6 { font-size: 13px; }\n" +
                "h1, h2 { padding-bottom: 6px; border-bottom: 1px solid #2C2A34; }\n" +
                "strong, b { font-weight: 600; color: #E4E2EA; }\n" +
                "a, a:visited { color: #A79FD6; text-decoration: underline; }\n" +
                "a:hover, a:focus, a:active { color: #E4E2EA; }\n" +
                "hr { border: 0; border-top: 1px solid #2C2A34; background-color: transparent; }\n" +
                "blockquote { color: #B6B2C4; border-left: 2px solid #5C5870; }\n" +
                "small, .small, .text-muted { color: #8E8A9C; }\n" +
                "code, kbd, pre, samp { font-family: Consolas, 'Courier New', monospace; }\n" +
                "code { color: #E4E2EA; background-color: #26252E; border-radius: 0; }\n" +
                "kbd { color: #E4E2EA; background-color: #2C2A34; border-radius: 0; box-shadow: none; }\n" +
                "pre { color: #E4E2EA; background-color: #26252E; border: 1px solid #2C2A34; border-radius: 0; }\n" +
                "pre code { background-color: transparent; }\n" +
                "table { border-collapse: collapse; }\n" +
                "th, td { border: 1px solid #2C2A34; padding: 4px 8px; }\n" +
                "th { color: #B6B2C4; }\n" +
                "img { border: 0; }\n");
            return sb.ToString();
        }

        /// <summary>Page complete : gabarit d'origine, sa feuille puis la feuille B1, contenu inchange.</summary>
        internal static string BuildHtml(string content, Color color)
        {
            return String.Format(Properties.Resources.readmeTemplateHTML, Properties.Resources.readmeTemplateCSS + B1Css, content, $"rgb({color.R},{color.G},{color.B})");
        }

        public BrowserReadmeControl()
        {
            InitializeComponent();

            wbReadme.Navigate("about:blank");
            HtmlDocument doc = wbReadme.Document;
            doc.Write(String.Empty);
            
            clear();
        }

        private string formatReadme(string name, ref HmodReadme hReadme)
        {
            string markdownTitle = (name != null && name.Trim() != "" ? $"# {name}\n\n" : "");
            return CommonMarkConverter.Convert(markdownTitle + String.Join("  \n", hReadme.headingLines) + "\n\n" + (hReadme.isMarkdown || hReadme.readme.Length == 0 ? hReadme.readme : $"```\n{hReadme.readme}\n```"));
        }

        private void setReadmeHTML(string name, ref HmodReadme hReadme)
        {
            Color color = this.BackColor;
            string html = BuildHtml(formatReadme(name, ref Readme), color);
            LastHtml = html;
            wbReadme.DocumentText = html;
        }

        public void setReadme(string name, string readme = "", bool markdown = false)
        {
            setReadme(name, new HmodReadme(readme, markdown));
        }

        public void setReadme(string name, HmodReadme hReadme)
        {
            Readme = hReadme;
            setReadmeHTML(name, ref Readme);
        }

        public void clear()
        {
            Readme = new HmodReadme("");
            
            Color color = this.BackColor;
            string html = BuildHtml("", color);
            LastHtml = html;
            wbReadme.DocumentText = html;
        }

        private void wbReadme_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.ToString() == "about:blank") return;

            //cancel the current event
            e.Cancel = true;

            //this opens the URL in the user's default browser
            Process.Start(e.Url.ToString());
        }
    }
}
