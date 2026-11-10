using System;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Case a cocher B1 : carre de 12 px, filet Dashed (Lavender au survol et coche), coche Lavender.</summary>
    public class B1CheckBox : CheckBox
    {
        private readonly B1Tracker tracker;

        public B1CheckBox()
        {
            tracker = new B1Tracker(this);
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnCheckStateChanged(EventArgs e) { base.OnCheckStateChanged(e); Invalidate(); }

        public override Size GetPreferredSize(Size proposedSize)
        {
            float s = B1Dpi.Scale(this);
            var tsz = string.IsNullOrEmpty(Text) ? Size.Empty : TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            return new Size(B1Draw.GlyphSize(s) + B1Draw.Px(8, s) + tsz.Width + 2, Math.Max(B1Draw.GlyphSize(s) + B1Draw.Px(6, s), tsz.Height + B1Draw.Px(4, s)));
        }

        internal B1VisualState FullState()
        {
            var st = tracker.State();
            B1VisualState forced;
            if (!B1Preview.TryGet(this, out forced))
            {
                if (CheckState == CheckState.Checked) st |= B1VisualState.Checked;
                else if (CheckState == CheckState.Indeterminate) st |= B1VisualState.Indeterminate;
            }
            return st;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var st = FullState();
            if (Appearance == Appearance.Button) B1Draw.PaintChip(e.Graphics, this, st);
            else B1Draw.PaintToggle(e.Graphics, this, st, false, CheckAlign);
        }
    }
}
