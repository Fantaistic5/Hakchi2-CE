using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>
    /// Finitions des controles dont le cadre est peint par le theme visuel de Windows (clair, non configurable) :
    /// TabControl (bande des onglets, cadre de la page), GroupBox (filet), Button desactive (texte illisible).
    /// Chaque finition repeint, apres le dessin par defaut (WM_PAINT et WM_PRINT, donc aussi DrawToBitmap),
    /// uniquement avec des couleurs de la palette. Une finition par controle ; elle se rattache a la creation du handle.
    /// </summary>
    internal abstract class FramePainter : NativeWindow
    {
        private const int WM_PAINT = 0x000F, WM_PRINT = 0x0317, WM_PRINTCLIENT = 0x0318;
        protected readonly Control Owner;

        protected FramePainter(Control owner) { Owner = owner; }

        protected abstract void PaintFrame(Graphics g);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (Owner.IsDisposed || !Owner.IsHandleCreated) return;
            try
            {
                if (m.Msg == WM_PAINT)
                {
                    using (var g = Graphics.FromHwnd(Handle)) PaintFrame(g);
                }
                else if ((m.Msg == WM_PRINT || m.Msg == WM_PRINTCLIENT) && m.WParam != IntPtr.Zero)
                {
                    using (var g = Graphics.FromHdc(m.WParam)) PaintFrame(g);
                }
            }
            catch (Exception) { /* cosmetique : un echec de repeinte ne doit jamais casser la fenetre */ }
        }

        protected static void Fill(Graphics g, Color c, int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h);
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
            if (tabs.RightToLeft == RightToLeft.Yes) return;
            if (tabs.Alignment != TabAlignment.Top || tabs.TabCount == 0) return;
            var client = tabs.ClientRectangle;
            var disp = tabs.DisplayRectangle;
            int bottom = 0;
            for (int i = 0; i < tabs.TabCount; i++) bottom = Math.Max(bottom, tabs.GetTabRect(i).Bottom);
            int frameTop = Math.Min(bottom, disp.Top);

            // bande, marges et bords : fond du theme (la page elle-meme est un controle enfant, on ne la touche pas)
            using (var region = new Region(client))
            {
                region.Exclude(disp);
                using (var b = new SolidBrush(B1Palette.Slate)) g.FillRegion(b, region);
            }
            // onglets : selectionne = fond de la page + barre d'accent ; les autres = RowActive
            for (int i = 0; i < tabs.TabCount; i++)
            {
                var r = tabs.GetTabRect(i);
                bool selected = i == tabs.SelectedIndex;
                Fill(g, selected ? B1Palette.Slate : B1Palette.RowActive, r.Left, r.Top, r.Width, r.Height);
                if (selected) Fill(g, B1Palette.Accent, r.Left, r.Top, r.Width, 2);
                TextRenderer.DrawText(g, tabs.TabPages[i].Text, tabs.Font, r, selected ? B1Palette.Text : B1Palette.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
            // filet autour du cadre de la page, interrompu sous l'onglet selectionne
            var sel = tabs.SelectedIndex >= 0 ? tabs.GetTabRect(tabs.SelectedIndex) : Rectangle.Empty;
            int top = frameTop - 1;
            Fill(g, B1Palette.Rule, 0, top, sel.Left, 1);
            Fill(g, B1Palette.Rule, sel.Right, top, client.Width - sel.Right, 1);
            Fill(g, B1Palette.Rule, 0, top, 1, client.Height - top);
            Fill(g, B1Palette.Rule, client.Width - 1, top, 1, client.Height - top);
            Fill(g, B1Palette.Rule, 0, client.Height - 1, client.Width, 1);
        }
    }

    internal sealed class GroupPainter : FramePainter
    {
        private readonly GroupBox group;
        public GroupPainter(GroupBox g) : base(g) { group = g; }

        protected override void PaintFrame(Graphics g)
        {
            if (group.RightToLeft == RightToLeft.Yes) return;
            var font = group.Font;
            int fh = font.Height;
            int w = group.Width, h = group.Height;
            int y = fh / 2;
            // filet de 1 px aux couleurs du theme (le dessin par defaut est un trait clair)
            Fill(g, group.BackColor == Color.Transparent ? B1Palette.Slate : group.BackColor, 0, h - 2, w, 1);   // le trait clair par defaut du bas est une ligne plus haut
            Fill(g, B1Palette.Dashed, 0, y, w, 1);
            Fill(g, B1Palette.Dashed, 0, h - 1, w, 1);
            Fill(g, B1Palette.Dashed, 0, y, 1, h - y);
            Fill(g, B1Palette.Dashed, w - 1, y, 1, h - y);
            if (!string.IsNullOrEmpty(group.Text))
            {
                var flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
                var size = TextRenderer.MeasureText(g, group.Text, font, new Size(int.MaxValue, fh), flags);
                Fill(g, group.BackColor == Color.Transparent ? B1Palette.Slate : group.BackColor, 4, 0, size.Width + 10, fh);
                TextRenderer.DrawText(g, group.Text, font, new Point(8, 0), group.Enabled ? group.ForeColor : B1Palette.TextFaint, flags);
            }
        }
    }
}
