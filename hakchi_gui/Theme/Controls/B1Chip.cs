using System;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Puce : RadioButton en apparence de bouton. Cadre Rule, texte TextSecondary ; actif : cadre et texte Lavender.</summary>
    public class B1Chip : RadioButton
    {
        private readonly B1Tracker tracker;

        public B1Chip()
        {
            tracker = new B1Tracker(this);
            Appearance = Appearance.Button;
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            TextAlign = ContentAlignment.MiddleCenter;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }

        public override Size GetPreferredSize(Size proposedSize)
        {
            float s = B1Dpi.Scale(this);
            var tsz = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return new Size(tsz.Width + 2 * B1Draw.Px(13, s), Math.Max(B1Draw.Px(24, s), tsz.Height + B1Draw.Px(8, s)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var st = tracker.State();
            B1VisualState forced;
            if (!B1Preview.TryGet(this, out forced) && Checked) st |= B1VisualState.Checked;
            B1Draw.PaintChip(e.Graphics, this, st);
        }
    }
}
