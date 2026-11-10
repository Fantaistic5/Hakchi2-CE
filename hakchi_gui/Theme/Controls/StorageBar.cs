using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Properties;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Barre d'occupation du bas : « Console 6,1 / 9,4 Go » puis une piste Rule de 6 px dont le remplissage est Accent.
    /// Au-dela de 100 % la piste est entierement remplie en Error (le texte passe aussi en Error).
    /// </summary>
    public class StorageBar : Control
    {
        private long used, total;
        private string caption;
        private readonly ShellFonts fonts = new ShellFonts();

        public StorageBar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            BackColor = B1Palette.Slate;
        }

        protected override Size DefaultSize { get { return new Size(360, 24); } }

        /// <summary>Intitule devant les chiffres ; « Console » par defaut.</summary>
        public string Caption
        {
            get { return caption ?? Resources.StorageBarCaption; }
            set { caption = value; Invalidate(); }
        }

        /// <summary>Octets utilises et octets au total.</summary>
        public void SetUsage(long used, long total)
        {
            this.used = Math.Max(0, used);
            this.total = Math.Max(0, total);
            AccessibleName = Caption + " " + UsageText;
            Invalidate();
        }

        public long Used { get { return used; } }
        public long Total { get { return total; } }

        /// <summary>Plus rempli que 100 % (pas de depassement quand la place est pile pleine).</summary>
        [Browsable(false)]
        public bool IsOverflow { get { return total > 0 && used > total; } }

        /// <summary>Capacite inconnue (total 0) : texte « — », piste vide, jamais Error.</summary>
        [Browsable(false)]
        public bool IsUnknown { get { return total <= 0; } }

        [Browsable(false)]
        public Color FillColor { get { return IsOverflow ? B1Palette.Error : B1Palette.Accent; } }

        /// <summary>« 6,1 / 9,4 Go » (Go des 1 Gio, sinon Mo, memes unites des deux cotes).</summary>
        [Browsable(false)]
        public string UsageText
        {
            get
            {
                if (IsUnknown) return "—";
                const double MB = 1024.0 * 1024.0, GB = MB * 1024.0;
                var ci = CultureInfo.CurrentCulture;
                bool gb = total >= GB;
                double div = gb ? GB : MB;
                string fmt = gb ? "0.0" : "0";
                return (used / div).ToString(fmt, ci) + " / " + (total / div).ToString(fmt, ci) + " " + (gb ? Resources.StorageUnitGB : Resources.StorageUnitMB);
            }
        }

        private float S { get { return B1Dpi.Scale(this); } }

        private string LabelText { get { return Caption + " " + UsageText; } }

        /// <summary>Piste (coordonnees du controle).</summary>
        [Browsable(false)]
        public Rectangle TrackBounds
        {
            get
            {
                float s = S;
                var f = fonts.Get(ShellFonts.Kind.Body, 9f, s);
                int tw = ShellText.Width(LabelText, f);
                int gap = B1Draw.Px(14, s);
                int h = B1Draw.Px(6, s);
                int y = (Height - h) / 2;
                int minW = B1Draw.Px(40, s);
                if (RightToLeft == RightToLeft.Yes)
                    return new Rectangle(0, y, Math.Max(minW, Width - tw - gap), h);
                int x = Math.Min(tw + gap, Math.Max(0, Width - minW));
                return new Rectangle(x, y, Math.Max(1, Width - x), h);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = S;
            g.Clear(Parent != null ? B1Draw.Backdrop(this) : B1Palette.Slate);
            var f = fonts.Get(ShellFonts.Kind.Body, 9f, s);
            bool rtl = RightToLeft == RightToLeft.Yes;
            var track = TrackBounds;

            var tr = rtl ? new Rectangle(track.Right + B1Draw.Px(14, s), 0, Math.Max(0, Width - track.Right - B1Draw.Px(14, s)), Height)
                         : new Rectangle(0, 0, Math.Max(0, track.X - B1Draw.Px(14, s)), Height);
            TextRenderer.DrawText(g, LabelText, f, tr, IsOverflow ? B1Palette.Error : B1Palette.TextDim,
                ShellText.Line | (rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : 0));

            B1Draw.Fill(g, B1Palette.Rule, track);
            double ratio = total > 0 ? Math.Min(1.0, (double)used / total) : 0.0;
            int fw = (int)Math.Round(track.Width * ratio);
            if (fw > 0)
                B1Draw.Fill(g, FillColor, rtl ? track.Right - fw : track.X, track.Y, fw, track.Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fonts.Dispose();
            base.Dispose(disposing);
        }
    }
}
