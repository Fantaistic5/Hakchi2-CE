using System;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Suit survol, appui et focus d'un controle B1 (boutons, cases, puces) et calcule son etat visuel.</summary>
    internal sealed class B1Tracker
    {
        private readonly ButtonBase c;
        private bool hover, down;

        public B1Tracker(ButtonBase control)
        {
            c = control;
            c.MouseEnter += (s, e) => { hover = true; c.Invalidate(); };
            c.MouseLeave += (s, e) => { hover = false; down = false; c.Invalidate(); };
            c.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { down = true; c.Invalidate(); } };
            c.MouseUp += (s, e) => { down = false; c.Invalidate(); };
            c.KeyDown += (s, e) => { if (e.KeyCode == Keys.Space) { down = true; c.Invalidate(); } };
            c.KeyUp += (s, e) => { if (e.KeyCode == Keys.Space) { down = false; c.Invalidate(); } };
            c.GotFocus += (s, e) => c.Invalidate();
            c.LostFocus += (s, e) => { down = false; c.Invalidate(); };
            c.EnabledChanged += (s, e) => c.Invalidate();
            c.TextChanged += (s, e) => c.Invalidate();
            c.RightToLeftChanged += (s, e) => c.Invalidate();
        }

        public B1VisualState State()
        {
            B1VisualState forced;
            if (B1Preview.TryGet(c, out forced)) return forced;
            if (!c.Enabled) return B1VisualState.Disabled;
            var st = B1VisualState.Normal;
            if (hover) st |= B1VisualState.Hover;
            if (down && hover || down && c.Focused) st |= B1VisualState.Pressed;
            if (c.Focused && B1Draw.ShowFocusCues(c)) st |= B1VisualState.Focus;
            return st;
        }
    }
}
