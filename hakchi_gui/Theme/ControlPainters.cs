using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme.Controls;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>
    /// Finitions des controles dont le cadre est peint par Windows (clair, non configurable) : TabControl (bande des onglets),
    /// GroupBox (titre et filet). Chaque finition repeint, apres le dessin par defaut (WM_PAINT et WM_PRINT, donc aussi DrawToBitmap),
    /// uniquement avec des couleurs de la palette. Le dessin est celui des controles B1 (B1Draw).
    /// </summary>
    internal abstract class FramePainter : NativeWindow
    {
        protected readonly Control Owner;

        protected FramePainter(Control owner) { Owner = owner; }

        protected abstract void PaintFrame(Graphics g);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (Owner.IsDisposed || !Owner.IsHandleCreated) return;
            Dispatch(Owner, Handle, ref m, PaintFrame);
        }

        /// <summary>Apres le dessin par defaut d'un message de peinture : repeint le decor. Cosmetique, ne leve jamais.</summary>
        internal static void Dispatch(Control owner, IntPtr handle, ref Message m, Action<Graphics> paint)
        {
            if (owner.IsDisposed || !owner.IsHandleCreated) return;
            try
            {
                if (m.Msg == ControlNative.WM_PAINT)
                {
                    using (var g = Graphics.FromHwnd(handle)) paint(g);
                }
                else if ((m.Msg == ControlNative.WM_PRINT || m.Msg == ControlNative.WM_PRINTCLIENT) && m.WParam != IntPtr.Zero)
                {
                    using (var g = Graphics.FromHdc(m.WParam)) paint(g);
                }
            }
            catch (Exception) { /* cosmetique : un echec de repeinte ne doit jamais casser la fenetre */ }
        }

        private static readonly ConditionalWeakTable<Control, FramePainter> attached = new ConditionalWeakTable<Control, FramePainter>();

        protected static void Attach(Control c, string key, Func<FramePainter> create)
        {
            FramePainter p;
            if (!attached.TryGetValue(c, out p)) { p = create(); attached.Add(c, p); }
            NativeTheme.WithHandle(c, key, h =>
            {
                if (p.Handle != h) { if (p.Handle != IntPtr.Zero) p.ReleaseHandle(); p.AssignHandle(h); }
            });
        }

        public static void AttachTabs(TabControl t) { Attach(t, "b1frame", () => new TabsPainter(t)); }
        public static void AttachGroup(GroupBox g) { Attach(g, "b1frame", () => new GroupPainter(g)); }
    }

    internal sealed class TabsPainter : FramePainter
    {
        private readonly TabControl tabs;
        public TabsPainter(TabControl t) : base(t) { tabs = t; }

        protected override void PaintFrame(Graphics g)
        {
            int hot = -1;
            var p = tabs.PointToClient(Cursor.Position);
            for (int i = 0; i < tabs.TabCount; i++) if (tabs.GetTabRect(i).Contains(p)) { hot = i; break; }
            B1Draw.PaintTabs(g, tabs, hot);
        }
    }

    internal sealed class GroupPainter : FramePainter
    {
        private readonly GroupBox group;
        public GroupPainter(GroupBox g) : base(g) { group = g; }

        protected override void PaintFrame(Graphics g)
        {
            var back = group.BackColor == Color.Transparent || !B1Draw.IsPalette(group.BackColor) ? B1Draw.Backdrop(group.Parent ?? group) : group.BackColor;
            B1Draw.PaintGroup(g, group, group.ClientRectangle, back, B1Dpi.Scale(group));
        }
    }
}
