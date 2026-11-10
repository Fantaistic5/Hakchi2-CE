using System;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Barre de progression B1 : piste Rule, remplissage Accent, 6 px de haut au centre du controle. Le mode Marquee est anime ici.</summary>
    public class B1ProgressBar : ProgressBar
    {
        private readonly Timer timer = new Timer { Interval = 33 };

        public B1ProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            timer.Tick += (s, e) => { if (IsHandleCreated && Visible && Style == ProgressBarStyle.Marquee) Invalidate(); };
        }

        /// <summary>Phase (0 a 1) de la barre en mode Marquee, forcee pour la galerie ; negatif = horloge.</summary>
        public float PreviewPhase { get; set; } = -1f;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ControlNative.StripBorder(Handle);          // la classe native ajoute un cadre en relief de 1 px
            timer.Start();
        }
        protected override void OnHandleDestroyed(EventArgs e) { timer.Stop(); base.OnHandleDestroyed(e); }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            float phase = PreviewPhase >= 0f ? PreviewPhase : ProgressWindow.Phase();
            B1Draw.PaintProgress(e.Graphics, ClientRectangle, B1Draw.Backdrop(Parent ?? this), Minimum, Maximum, Value, Style == ProgressBarStyle.Marquee, phase, B1Dpi.Scale(this));
        }
    }
}
