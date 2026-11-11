using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.ModHub.Controls
{
    public partial class ModInfoControl : UserControl
    {
        private Bitmap _infoStrips = null;
        public Bitmap infoStrips {
            get
            {
                return _infoStrips;
            }
            set
            {
                _infoStrips = value;
                Refresh();
            }
        }

        private Color _textColor = B1Palette.Text;
        public Color textColor
        {
            get
            {
                return _textColor;
                
            }
            set
            {
                _textColor = value;
                Refresh();
            }
        }

        // B1 : etiquettes en capitales espacees (Jost Regular, TextDim), valeurs en Jost Medium ; nom du module en Jost SemiBold.
        // Polices partagees par toutes les instances (jamais liberees : comme les polices du theme).
        private static Font dataFont, nameFont;
        private static Font DataFont { get { return dataFont ?? (dataFont = B1Fonts.Body(9f)); } }
        private static Font NameFont { get { return nameFont ?? (nameFont = B1Fonts.Title(11f)); } }
        private const int padding = 12;

        private string _ModuleName;
        public string ModuleName
        {
            get
            {
                return _ModuleName;
            }
            set
            {
                _ModuleName = value;
                Refresh();
            }
        }

        private string _Author;
        public string Author
        {
            get
            {
                return _Author;
            }
            set
            {
                _Author = value;
                Refresh();
            }
        }

        private string _LatestVersion;
        public string LatestVersion
        {
            get
            {
                return _LatestVersion;
            }
            set
            {
                _LatestVersion = value;
                Refresh();
            }
        }

        private string _InstalledVersion;
        public string InstalledVersion
        {
            get
            {
                return _InstalledVersion;
            }
            set
            {
                _InstalledVersion = value;
                Refresh();
            }
        }

        public ModInfoControl()
        {
            InitializeComponent();
        }

        public void SetInfo(string Name, string Author, string LatestVersion, string InstalledVersion)
        {
            _ModuleName = Name;
            _Author = Author;
            _LatestVersion = LatestVersion;
            _InstalledVersion = InstalledVersion;
            Refresh();
        }
        public void Clear() => SetInfo(null, null, null, null);

        public bool IsEmpty {
            get
            {
                return _ModuleName == null && _Author == null && _LatestVersion == null && _InstalledVersion == null;
            }
        }

        private static readonly TextFormatFlags ValueFlags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        // Une rubrique : etiquette en capitales (sans les deux-points de la ressource), puis la valeur, retour a la ligne au mot.
        private int DrawField(Graphics g, string label, string value, Font valueFont, int y, float s)
        {
            int pad = B1Dpi.Px(padding, s), width = Width - pad * 2;
            if (width <= 0) return y;
            var caps = B1Draw.CapsFont(DataFont);
            var heading = (label ?? "").Trim().TrimEnd(':', '：').Trim().ToUpper(CultureInfo.CurrentCulture);
            B1Draw.DrawTracked(g, heading, caps, B1Palette.TextDim, pad, y, B1Fonts.CapsTracking);
            y += caps.Height + B1Dpi.Px(4, s);
            var box = new Size(width, int.MaxValue);
            int h = TextRenderer.MeasureText(g, value, valueFont, box, ValueFlags).Height;
            TextRenderer.DrawText(g, value, valueFont, new Rectangle(pad, y, width, h), _textColor, ValueFlags);
            return y + h + B1Dpi.Px(14, s);
        }

        private void ModInfo_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = B1Dpi.Scale(this);

            if (_infoStrips != null)
            {
                for (int i = 0; i < Height; i = i + _infoStrips.Height)
                {
                    g.DrawImage(_infoStrips, Width - _infoStrips.Width, i);
                }
            }

            int y = B1Dpi.Px(padding, s);
            if (_ModuleName != null && _ModuleName.Trim().Length > 0)
                y = DrawField(g, ModHubResources.ModuleName, _ModuleName, NameFont, y, s);

            if (_Author != null && _Author.Trim().Length > 0)
                y = DrawField(g, ModHubResources.Author, _Author, DataFont, y, s);

            if (_LatestVersion != null && _LatestVersion.Trim().Length > 0)
                y = DrawField(g, ModHubResources.LatestVersion, _LatestVersion, DataFont, y, s);

            if (_InstalledVersion != null && _InstalledVersion.Trim().Length > 0)
                y = DrawField(g, ModHubResources.InstalledVersion, _InstalledVersion, DataFont, y, s);

            // filet de 1 px autour de la carte
            B1Draw.Frame(g, B1Palette.Rule, ClientRectangle, B1Dpi.Px(1, s));
        }
    }
}
