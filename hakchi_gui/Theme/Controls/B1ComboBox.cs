using System;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Liste deroulante B1 : champ RowActive, chevron, liste Slate dont la ligne active est RowActive avec une barre d'accent.</summary>
    public class B1ComboBox : ComboBox
    {
        public B1ComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Standard;
            BackColor = B1Palette.RowActive;
            ForeColor = B1Palette.Text;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            ItemHeight = Font.Height + B1Draw.Px(4, B1Dpi.Scale(this));
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            ComboPaint.DrawItem(this, e);
        }

        protected override void OnDropDown(EventArgs e)
        {
            base.OnDropDown(e);
            ComboPaint.ThemeList(this);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void WndProc(ref Message m)
        {
            if (ComboPaint.Before(this, ref m)) return;
            base.WndProc(ref m);
            ComboPaint.After(this, ref m);
        }
    }
}
