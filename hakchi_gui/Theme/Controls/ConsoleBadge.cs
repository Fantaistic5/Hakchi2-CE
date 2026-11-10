using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Properties;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Etat de la console a droite de la barre du haut : pastille (verte en ligne, TextFaint sinon), modele, version de hakchi.
    /// </summary>
    public class ConsoleBadge : Control
    {
        private bool online;
        private string model = string.Empty;
        private string version = string.Empty;
        private readonly ShellFonts fonts = new ShellFonts();

        public ConsoleBadge()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            BackColor = B1Palette.Slate;
            Size = new Size(200, 24);
        }

        public void SetState(bool online, string model, string version)
        {
            this.online = online;
            this.model = model ?? string.Empty;
            this.version = version ?? string.Empty;
            AccessibleName = BadgeText;
            Invalidate();
            if (Parent != null) Parent.PerformLayout();   // la largeur suit le texte
        }

        [Browsable(false)]
        public bool Online { get { return online; } }

        /// <summary>Texte affiche : « modele · hakchi version », ou « Aucune console » sans modele.</summary>
        [Browsable(false)]
        public string BadgeText
        {
            get
            {
                string head = model.Length > 0 ? model : (online ? string.Empty : Resources.ConsoleBadgeOffline);
                string tail = version.Length > 0 ? "hakchi " + version : string.Empty;
                if (head.Length > 0 && tail.Length > 0) return head + " · " + tail;
                return head.Length > 0 ? head : tail;
            }
        }

        [Browsable(false)]
        public Color DotColor { get { return online ? B1Palette.Online : B1Palette.TextFaint; } }

        private float S { get { return B1Dpi.Scale(this); } }
        private int Dot { get { return B1Draw.Px(7, S); } }
        private int Gap { get { return B1Draw.Px(8, S); } }

        /// <summary>Rectangle de la pastille, en coordonnees du controle.</summary>
        [Browsable(false)]
        public Rectangle DotBounds
        {
            get
            {
                bool rtl = RightToLeft == RightToLeft.Yes;
                int d = Dot;
                return new Rectangle(rtl ? Width - d : 0, (Height - d) / 2, d, d);
            }
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var f = fonts.Get(ShellFonts.Kind.Body, 9f, S);
            return new Size(Dot + Gap + ShellText.Width(BadgeText, f) + B1Draw.Px(2, S), f.Height + B1Draw.Px(4, S));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = S;
            g.Clear(Parent != null ? B1Draw.Backdrop(this) : B1Palette.Slate);
            var f = fonts.Get(ShellFonts.Kind.Body, 9f, s);
            bool rtl = RightToLeft == RightToLeft.Yes;
            var dot = DotBounds;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(DotColor)) g.FillEllipse(br, dot);
            g.SmoothingMode = old;
            var tr = rtl ? new Rectangle(0, 0, Width - Dot - Gap, Height) : new Rectangle(Dot + Gap, 0, Width - Dot - Gap, Height);
            TextRenderer.DrawText(g, BadgeText, f, tr, online ? B1Palette.Text : B1Palette.TextDim,
                ShellText.Line | (rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : 0));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fonts.Dispose();
            base.Dispose(disposing);
        }
    }
}
