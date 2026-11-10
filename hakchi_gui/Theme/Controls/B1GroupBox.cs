using System;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Groupe B1 : titre en capitales (Caps, TextFaint, 0,14 em) sous un filet Rule ; pas de cadre ferme.</summary>
    public class B1GroupBox : GroupBox
    {
        public B1GroupBox()
        {
            FlatStyle = FlatStyle.Flat;
            ForeColor = B1Palette.TextFaint;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            FramePainter.Dispatch(this, Handle, ref m, g =>
            {
                var back = BackColor == Color.Transparent || !B1Draw.IsPalette(BackColor) ? B1Draw.Backdrop(Parent ?? this) : BackColor;
                B1Draw.PaintGroup(g, this, ClientRectangle, back, B1Dpi.Scale(this));
            });
        }
    }
}
