using System;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Onglets plats B1 : texte TextSecondary, onglet actif en Text souligne Accent sur 2 px, filet Rule sous la bande.</summary>
    public class B1TabControl : TabControl
    {
        private int hot = -1;

        /// <summary>Onglet survole force (galerie, tests) ; -1 = suivre la souris.</summary>
        public int PreviewHotTab { get; set; } = -1;

        public B1TabControl()
        {
            ForeColor = B1Palette.Text;
        }

        private int HotTab { get { return PreviewHotTab >= 0 ? PreviewHotTab : hot; } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = -1;
            for (int i = 0; i < TabCount; i++) if (GetTabRect(i).Contains(e.Location)) { h = i; break; }
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hot != -1) { hot = -1; Invalidate(); }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            FramePainter.Dispatch(this, Handle, ref m, g => B1Draw.PaintTabs(g, this, HotTab));
        }
    }
}
