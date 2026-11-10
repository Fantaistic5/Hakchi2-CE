using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Bouton B1 plat. Primary : fond Primary ; Secondary : filet et texte Lavender sur Slate ; Quiet : texte TextSecondary sans cadre ;
    /// Danger : filet Error (flash, effacement). Survol : un pas vers Accent. Focus : filet 1 px Lavender decale de 2 px.
    /// Tout est dessine ici ; les metriques suivent l'echelle (B1Dpi).
    /// </summary>
    public class B1Button : Button
    {
        private readonly B1Tracker tracker;
        private B1ButtonKind kind = B1ButtonKind.Secondary;

        public B1Button()
        {
            tracker = new B1Tracker(this);
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        [DefaultValue(B1ButtonKind.Secondary), Category("Appearance"), Description("Style du bouton B1.")]
        public B1ButtonKind Kind
        {
            get { return kind; }
            set { if (kind != value) { kind = value; Invalidate(); } }
        }

        protected override Size DefaultSize { get { return new Size(88, 28); } }

        public override Size GetPreferredSize(Size proposedSize)
        {
            float s = B1Dpi.Scale(this);
            var tsz = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            int w = tsz.Width + 2 * B1Draw.Px(14, s), h = Math.Max(B1Draw.Px(28, s), tsz.Height + B1Draw.Px(10, s));
            if (Image != null) { w += Image.Width + B1Draw.Px(4, s); h = Math.Max(h, Image.Height + B1Draw.Px(10, s)); }
            return new Size(Math.Max(w, B1Draw.Px(64, s)), h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            B1Draw.PaintButton(e.Graphics, this, kind, tracker.State());
        }
    }
}
