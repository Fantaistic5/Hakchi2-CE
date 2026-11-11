using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Rattache un NativeWindow (sous-classement) a un controle, recree avec son handle. Une instance par (controle, cle).</summary>
    internal static class Subclasser
    {
        private static readonly ConditionalWeakTable<Control, Dictionary<string, NativeWindow>> table = new ConditionalWeakTable<Control, Dictionary<string, NativeWindow>>();

        internal static void Attach(Control c, string key, Func<NativeWindow> create, Func<Control, IntPtr> targetOf)
        {
            Dictionary<string, NativeWindow> d;
            if (!table.TryGetValue(c, out d)) { d = new Dictionary<string, NativeWindow>(); table.Add(c, d); }
            NativeWindow w;
            if (!d.TryGetValue(key, out w)) { w = create(); d[key] = w; }
            NativeTheme.WithHandle(c, "sub-" + key, h =>
            {
                var target = targetOf == null ? h : targetOf(c);
                if (target == IntPtr.Zero || w.Handle == target) return;
                if (w.Handle != IntPtr.Zero) w.ReleaseHandle();
                w.AssignHandle(target);
            });
        }
    }

    // ================================================================================== ComboBox

    internal static class ComboPaint
    {
        /// <summary>Messages que le theme remplace entierement (le dessin natif est gris). Retourne true si traite.</summary>
        internal static bool Before(ComboBox c, ref Message m)
        {
            if (!c.IsHandleCreated) return false;
            try
            {
                if (m.Msg == ControlNative.WM_ERASEBKGND) { m.Result = (IntPtr)1; return true; }
                if (m.Msg == ControlNative.WM_PAINT && m.WParam == IntPtr.Zero)
                {
                    DrawInto(c, IntPtr.Zero);
                    m.Result = IntPtr.Zero;
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>Apres le dessin natif (WM_PRINT / WM_PRINTCLIENT, y compris les enfants) : on repeint le decor par-dessus.</summary>
        internal static void After(ComboBox c, ref Message m)
        {
            if (m.WParam == IntPtr.Zero || !c.IsHandleCreated) return;
            if (m.Msg != ControlNative.WM_PRINT && m.Msg != ControlNative.WM_PRINTCLIENT) return;
            try { DrawInto(c, m.WParam); } catch (Exception) { }
        }

        private static void DrawInto(ComboBox c, IntPtr hdc)
        {
            ControlNative.COMBOBOXINFO info;
            bool has = ControlNative.TryComboInfo(c, out info);
            var client = c.ClientRectangle;
            Rectangle exclude = Rectangle.Empty;
            bool editText = false;
            if (has && info.hwndItem != IntPtr.Zero && c.DropDownStyle != ComboBoxStyle.DropDownList)
            {
                // la zone d'edition est une fenetre fille : on ne la recouvre pas (sauf dans une capture WM_PRINT, ou elle n'est pas imprimee)
                if (hdc == IntPtr.Zero) exclude = info.rcItem.ToRectangle(); else editText = true;
            }
            ControlNative.Paint(c.Handle, hdc, client, exclude, g =>
            {
                Draw(c, g, has ? info.rcButton.ToRectangle() : Rectangle.Empty);
                if (editText) DrawEditText(c, g, info.rcItem.ToRectangle());
            });
        }

        private static B1VisualState StateOf(ComboBox c)
        {
            B1VisualState forced;
            if (B1Preview.TryGet(c, out forced)) return forced;
            if (!c.Enabled) return B1VisualState.Disabled;
            var st = B1VisualState.Normal;
            if (c.DroppedDown || c.Focused) st |= B1VisualState.Focus;
            var p = ControlNative.CursorIn(c.Handle);
            if (c.ClientRectangle.Contains(p)) st |= B1VisualState.Hover;
            return st;
        }

        internal static void Draw(ComboBox c, Graphics g, Rectangle button)
        {
            float s = B1Dpi.Scale(c);
            var r = c.ClientRectangle;
            var st = StateOf(c);
            bool dis = (st & B1VisualState.Disabled) != 0;
            Color fill = dis ? B1Palette.Slate : B1Draw.IsPalette(c.BackColor) ? c.BackColor : B1Palette.RowActive;
            B1Draw.FieldFrame(g, r, fill, st, s);
            int bw = Math.Max(button.Width, B1Draw.Px(20, s));         // la zone native ne suit pas l'echelle
            button = new Rectangle(r.Right - bw, r.Top, bw, r.Height);
            var arrow = Rectangle.Inflate(button, -1, -1);
            Color chev = dis ? B1Palette.TextFaint : (st & (B1VisualState.Focus | B1VisualState.Hover)) != 0 ? B1Palette.Lavender : B1Palette.TextSecondary;
            B1Draw.Chevron(g, arrow, chev, s, false);
            if (c.DropDownStyle == ComboBoxStyle.DropDownList)
            {
                string text = "";
                try { text = c.SelectedIndex >= 0 ? c.GetItemText(c.Items[c.SelectedIndex]) : c.Text; } catch (Exception) { }
                bool rtl = c.RightToLeft == RightToLeft.Yes;
                var tr = new Rectangle(r.Left + B1Draw.Px(6, s), r.Top, Math.Max(0, button.Left - r.Left - B1Draw.Px(6, s)), r.Height);
                TextRenderer.DrawText(g, text, c.Font, tr, dis ? B1Palette.TextFaint : B1Draw.IsPalette(c.ForeColor) ? c.ForeColor : B1Palette.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | (rtl ? TextFormatFlags.RightToLeft : 0));
            }
        }

        private static void DrawEditText(ComboBox c, Graphics g, Rectangle item)
        {
            bool dis = !c.Enabled;
            TextRenderer.DrawText(g, c.Text, c.Font, Rectangle.Inflate(item, -1, 0), dis ? B1Palette.TextFaint : B1Draw.IsPalette(c.ForeColor) ? c.ForeColor : B1Palette.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        /// <summary>Ligne de la liste deroulante (OwnerDrawFixed).</summary>
        internal static void DrawItem(ComboBox c, DrawItemEventArgs e)
        {
            float s = B1Dpi.Scale(c);
            var g = e.Graphics;
            if (e.Index < 0) { B1Draw.Fill(g, B1Palette.Slate, e.Bounds); return; }
            string text = "";
            try { text = c.GetItemText(c.Items[e.Index]); } catch (Exception) { }
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool active = !edit && (e.State & DrawItemState.Selected) != 0;
            B1Draw.PaintListRow(g, e.Bounds, text, c.Font, active, c.Enabled, edit ? B1Palette.RowActive : B1Palette.Slate, c.RightToLeft == RightToLeft.Yes, s);
        }

        /// <summary>Passe une liste deroulante au theme (dessin des lignes, barre de defilement sombre) sans changer son type.</summary>
        internal static void HookStock(ComboBox c)
        {
            if (c.DrawMode == DrawMode.Normal)
            {
                int h = c.ItemHeight;                          // hauteur native, conservee : la mise en page ne bouge pas
                c.DrawMode = DrawMode.OwnerDrawFixed;
                // Le setter ItemHeight cree le handle (ComboBox.UpdateItemHeight -> CreateControl), et avec lui ceux de tous
                // les parents, meme caches : ~20-50 ms par liste au demarrage de MainForm (fiche cachee). La meme valeur est
                // donc posee a la creation du handle, quand elle a lieu.
                if (c.IsHandleCreated) { try { c.ItemHeight = h; } catch (Exception) { } }
                else NativeTheme.WithHandle(c, "b1itemheight", _ => { try { c.ItemHeight = h; } catch (Exception) { } });
                c.DrawItem += StockDrawItem;
                c.DropDown += StockDropDown;
            }
            Subclasser.Attach(c, "combo", () => new ComboWindow(c), null);
        }

        private static void StockDrawItem(object sender, DrawItemEventArgs e) { DrawItem((ComboBox)sender, e); }

        private static void StockDropDown(object sender, EventArgs e) { ThemeList((ComboBox)sender); }

        internal static void ThemeList(ComboBox c)
        {
            ControlNative.COMBOBOXINFO info;
            if (ControlNative.TryComboInfo(c, out info) && info.hwndList != IntPtr.Zero)
                NativeTheme.SetTheme(info.hwndList, "DarkMode_Explorer", null);
        }
    }

    internal sealed class ComboWindow : NativeWindow
    {
        private readonly ComboBox combo;
        public ComboWindow(ComboBox c) { combo = c; }

        protected override void WndProc(ref Message m)
        {
            if (combo.IsDisposed) { base.WndProc(ref m); return; }
            if (ComboPaint.Before(combo, ref m)) return;
            base.WndProc(ref m);
            ComboPaint.After(combo, ref m);
            if (m.Msg == ControlNative.WM_MOUSEMOVE || m.Msg == ControlNative.WM_MOUSELEAVE) ControlNative.InvalidateRect(Handle, IntPtr.Zero, false);
        }
    }

    // ================================================================================== NumericUpDown / DomainUpDown

    internal sealed class SpinWindow : NativeWindow
    {
        private readonly UpDownBase owner;
        private readonly Control buttons;
        public SpinWindow(UpDownBase o, Control inner) { owner = o; buttons = inner; }

        internal static void Hook(UpDownBase u)
        {
            foreach (Control inner in u.Controls)
            {
                if (inner is TextBox) continue;           // UpDownEdit : champ de saisie, deja habille
                var captured = inner;
                Subclasser.Attach(captured, "spin", () => new SpinWindow(u, captured), null);
            }
            // bordure : UpDownBase la dessine avec la couleur systeme (gris clair) ; on la repeint apres
            Subclasser.Attach(u, "spinframe", () => new SpinFrameWindow(u), null);
        }

        protected override void WndProc(ref Message m)
        {
            if (owner.IsDisposed || buttons.IsDisposed) { base.WndProc(ref m); return; }
            try
            {
                if (m.Msg == ControlNative.WM_ERASEBKGND) { m.Result = (IntPtr)1; return; }
                if (m.Msg == ControlNative.WM_PAINT && m.WParam == IntPtr.Zero) { DrawInto(IntPtr.Zero); m.Result = IntPtr.Zero; return; }
                if ((m.Msg == ControlNative.WM_PRINT || m.Msg == ControlNative.WM_PRINTCLIENT) && m.WParam != IntPtr.Zero) { DrawInto(m.WParam); m.Result = IntPtr.Zero; return; }
            }
            catch (Exception) { }
            base.WndProc(ref m);
            if (m.Msg == ControlNative.WM_MOUSEMOVE || m.Msg == ControlNative.WM_MOUSELEAVE || m.Msg == ControlNative.WM_LBUTTONDOWN
                || m.Msg == ControlNative.WM_LBUTTONUP || m.Msg == ControlNative.WM_CAPTURECHANGED) ControlNative.InvalidateRect(Handle, IntPtr.Zero, false);
        }

        private void DrawInto(IntPtr hdc)
        {
            ControlNative.Paint(Handle, hdc, buttons.ClientRectangle, Rectangle.Empty, Draw);
        }

        private void Draw(Graphics g)
        {
            float s = B1Dpi.Scale(owner);
            var r = buttons.ClientRectangle;
            B1VisualState forced;
            bool preview = B1Preview.TryGet(owner, out forced);
            bool dis = preview ? (forced & B1VisualState.Disabled) != 0 : !owner.Enabled;
            Color back = B1Draw.IsPalette(owner.BackColor) ? owner.BackColor : B1Palette.RowActive;
            B1Draw.Fill(g, dis ? B1Palette.Slate : back, r);
            int half = r.Height / 2;
            var up = new Rectangle(r.Left, r.Top, r.Width, half);
            var down = new Rectangle(r.Left, r.Top + half, r.Width, r.Height - half);
            var p = ControlNative.CursorIn(Handle);
            bool pressed = !dis && (Control.MouseButtons & MouseButtons.Left) != 0 && r.Contains(p);
            for (int i = 0; i < 2; i++)
            {
                var cell = i == 0 ? up : down;
                bool hot = !dis && cell.Contains(p) || (preview && (forced & B1VisualState.Hover) != 0 && i == 0);
                bool prs = hot && (pressed || (preview && (forced & B1VisualState.Pressed) != 0));
                Color fill = prs ? B1Palette.Primary : hot ? B1Palette.Rule : dis ? B1Palette.Slate : back;
                if (fill != back) B1Draw.Fill(g, fill, Rectangle.Inflate(cell, 0, i == 0 ? 0 : 0));
                var inner = new Rectangle(cell.Left, cell.Top + (i == 0 ? B1Draw.Px(2, s) : 0), cell.Width, cell.Height - B1Draw.Px(2, s));
                Color chev = dis ? B1Palette.TextFaint : prs ? B1Palette.TextOnPrimary : hot ? B1Palette.Lavender : B1Palette.TextSecondary;
                B1Draw.Chevron(g, inner, chev, s * 0.75f, i == 0);
            }
            B1Draw.Fill(g, B1Palette.Rule, r.Left, r.Top, B1Draw.Px(1, s), r.Height);          // filet entre le champ et les fleches
        }
    }

    /// <summary>Bordure d'un NumericUpDown / DomainUpDown (dessinee dans le client par UpDownBase), repeinte aux couleurs du theme.</summary>
    internal sealed class SpinFrameWindow : NativeWindow
    {
        private readonly UpDownBase owner;
        public SpinFrameWindow(UpDownBase o) { owner = o; }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (owner.IsDisposed || owner.BorderStyle == BorderStyle.None) return;
            FramePainter.Dispatch(owner, Handle, ref m, g =>
            {
                B1VisualState forced;
                bool preview = B1Preview.TryGet(owner, out forced);
                var st = preview ? forced : !owner.Enabled ? B1VisualState.Disabled : owner.ContainsFocus ? B1VisualState.Focus : B1VisualState.Normal;
                Color border = (st & B1VisualState.Disabled) != 0 ? B1Palette.Rule : (st & B1VisualState.Focus) != 0 ? B1Palette.Accent : (st & B1VisualState.Hover) != 0 ? B1Palette.Dashed : B1Palette.Rule;
                B1Draw.Frame(g, border, owner.ClientRectangle, B1Draw.Px(1, B1Dpi.Scale(owner)));
            });
        }
    }

    /// <summary>
    /// Bordure native (non client, 1 px) des champs, listes et arbres en BorderStyle.FixedSingle : gris clair sous Windows,
    /// bleue au survol et au focus. Repeinte en Rule (Dashed au survol, Accent au focus) apres le dessin natif, qui garde les barres de defilement.
    /// </summary>
    internal sealed class NcBorderWindow : NativeWindow
    {
        private const int WM_NCPAINT = 0x85, WM_SETFOCUS = 0x7, WM_KILLFOCUS = 0x8, PRF_NONCLIENT = 0x2, WS_BORDER = 0x800000, WS_EX_CLIENTEDGE = 0x200, GWL_STYLE = -16, GWL_EXSTYLE = -20;

        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out ControlNative.RECT r);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetCursorPos")] private static extern bool GetCursor(out Point p);

        private readonly Control owner;
        private bool lastHover;
        public NcBorderWindow(Control c) { owner = c; }

        internal static void Hook(Control c)
        {
            Subclasser.Attach(c, "ncborder", () => new NcBorderWindow(c), null);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (owner.IsDisposed || !owner.IsHandleCreated) return;
            try
            {
                if (m.Msg == WM_NCPAINT || m.Msg == WM_SETFOCUS || m.Msg == WM_KILLFOCUS)
                    Draw(IntPtr.Zero);
                else if (m.Msg == ControlNative.WM_MOUSEMOVE || m.Msg == ControlNative.WM_MOUSELEAVE)
                {
                    // le cadre ne change qu avec l etat de survol : pas de repeinte a chaque mouvement
                    bool hover = HoverNow();
                    if (hover != lastHover) { lastHover = hover; Draw(IntPtr.Zero); }
                }
                else if (m.Msg == ControlNative.WM_PRINT && m.WParam != IntPtr.Zero && ((long)m.LParam & PRF_NONCLIENT) != 0)
                    Draw(m.WParam);
            }
            catch (Exception) { }
        }

        private static BorderStyle LogicalBorder(Control c)
        {
            if (c is TextBoxBase) return ((TextBoxBase)c).BorderStyle;
            if (c is ListBox) return ((ListBox)c).BorderStyle;
            if (c is ListView) return ((ListView)c).BorderStyle;
            if (c is TreeView) return ((TreeView)c).BorderStyle;
            if (c is Panel) return ((Panel)c).BorderStyle;
            if (c is PictureBox) return ((PictureBox)c).BorderStyle;
            if (c is Label) return ((Label)c).BorderStyle;
            return BorderStyle.None;
        }

        private bool HoverNow()
        {
            ControlNative.RECT wr;
            Point p;
            return GetWindowRect(Handle, out wr) && GetCursor(out p) && wr.ToRectangle().Contains(p);
        }

        private void Draw(IntPtr hdc)
        {
            int thick = (GetWindowLong(Handle, GWL_EXSTYLE) & WS_EX_CLIENTEDGE) != 0 ? 2 : (GetWindowLong(Handle, GWL_STYLE) & WS_BORDER) != 0 ? 1 : 0;
            // Un TextBox multiligne a defilement n'a parfois plus de WS_BORDER natif (le cadre gris du systeme est alors peint dans la zone
            // cliente, a gauche de la barre de defilement) : le cadre B1 suit alors le BorderStyle et entoure la fenetre entiere, barre comprise.
            if (thick == 0 && LogicalBorder(owner) == BorderStyle.FixedSingle) thick = 1;
            if (thick == 0) return;
            ControlNative.RECT wr;
            if (!GetWindowRect(Handle, out wr)) return;
            int w = wr.Right - wr.Left, h = wr.Bottom - wr.Top;
            bool own = hdc == IntPtr.Zero;
            if (own) hdc = GetWindowDC(Handle);
            if (hdc == IntPtr.Zero) return;
            try
            {
                B1VisualState forced;
                bool preview = B1Preview.TryGet(owner, out forced);
                var st = preview ? forced : !owner.Enabled ? B1VisualState.Disabled : owner.ContainsFocus ? B1VisualState.Focus : B1VisualState.Normal;
                if (!preview && (st & (B1VisualState.Focus | B1VisualState.Disabled)) == 0)
                {
                    Point p;
                    if (GetCursor(out p) && wr.ToRectangle().Contains(p)) st |= B1VisualState.Hover;
                }
                Color c = (st & B1VisualState.Disabled) != 0 ? B1Palette.Rule : (st & B1VisualState.Focus) != 0 ? B1Palette.Accent : (st & B1VisualState.Hover) != 0 ? B1Palette.Dashed : B1Palette.Rule;
                using (var g = Graphics.FromHdc(hdc)) B1Draw.Frame(g, c, new Rectangle(0, 0, w, h), thick);
            }
            finally { if (own) ReleaseDC(Handle, hdc); }
        }
    }

    // ================================================================================== ProgressBar

    internal sealed class ProgressWindow : NativeWindow
    {
        private readonly ProgressBar bar;
        private Timer timer;
        public ProgressWindow(ProgressBar b)
        {
            bar = b;
            // l animation (mode Marquee) demarre avec le handle, y compris recree, et s arrete avec lui
            b.HandleCreated += (s, e) => StartTimer();
            b.HandleDestroyed += (s, e) => StopTimer();
            b.Disposed += (s, e) => StopTimer();
            if (b.IsHandleCreated) StartTimer();
        }

        internal static void Hook(ProgressBar b)
        {
            NativeTheme.WithHandle(b, "b1progressborder", ControlNative.StripBorder);       // plus de cadre en relief natif
            Subclasser.Attach(b, "progress", () => new ProgressWindow(b), null);
        }

        protected override void WndProc(ref Message m)
        {
            if (bar.IsDisposed) { base.WndProc(ref m); return; }
            try
            {
                if (m.Msg == ControlNative.WM_ERASEBKGND) { m.Result = (IntPtr)1; return; }
                if (m.Msg == ControlNative.WM_PAINT && m.WParam == IntPtr.Zero) { DrawInto(IntPtr.Zero); m.Result = IntPtr.Zero; return; }
                if ((m.Msg == ControlNative.WM_PRINT || m.Msg == ControlNative.WM_PRINTCLIENT) && m.WParam != IntPtr.Zero) { DrawInto(m.WParam); m.Result = IntPtr.Zero; return; }
            }
            catch (Exception) { }
            base.WndProc(ref m);
        }

        private void DrawInto(IntPtr hdc)
        {
            ControlNative.Paint(Handle, hdc, bar.ClientRectangle, Rectangle.Empty, g =>
                B1Draw.PaintProgress(g, bar.ClientRectangle, B1Draw.Backdrop(bar.Parent ?? bar), bar.Minimum, bar.Maximum, bar.Value,
                    bar.Style == ProgressBarStyle.Marquee, Phase(), B1Dpi.Scale(bar)));
        }

        internal static float Phase() { return (Environment.TickCount & 0x7FFFFFFF) % 1400 / 1400f; }

        // Sans theme visuel la barre ne s'anime plus : le mode « marquee » est anime ici.
        internal bool TimerRunning { get { return timer != null && timer.Enabled; } }

        private void StartTimer()
        {
            if (timer == null)
            {
                timer = new Timer { Interval = 33 };
                timer.Tick += (s, e) =>
                {
                    if (bar.IsDisposed || !bar.IsHandleCreated) { StopTimer(); return; }
                    if (bar.Style == ProgressBarStyle.Marquee && bar.Visible) bar.Invalidate();       // les autres styles ne se redessinent pas
                };
            }
            timer.Start();
        }

        private void StopTimer()
        {
            if (timer == null) return;
            timer.Stop();
            timer.Dispose();
            timer = null;
        }
    }

    // ================================================================================== ListView / TreeView / ListBox

    /// <summary>En-tete de colonnes : aplat Slate, texte TextDim, filets Rule.</summary>
    internal sealed class HeaderWindow : NativeWindow
    {
        private readonly ListView list;
        public HeaderWindow(ListView l) { list = l; }

        internal static void Hook(ListView l)
        {
            Subclasser.Attach(l, "header", () => new HeaderWindow(l), c => NativeTheme.ListViewHeader(c.Handle));
        }

        protected override void WndProc(ref Message m)
        {
            if (list.IsDisposed) { base.WndProc(ref m); return; }
            try
            {
                if (m.Msg == ControlNative.WM_ERASEBKGND) { m.Result = (IntPtr)1; return; }
                if (m.Msg == ControlNative.WM_PAINT && m.WParam == IntPtr.Zero) { DrawInto(IntPtr.Zero); m.Result = IntPtr.Zero; return; }
                if ((m.Msg == ControlNative.WM_PRINT || m.Msg == ControlNative.WM_PRINTCLIENT) && m.WParam != IntPtr.Zero) { DrawInto(m.WParam); m.Result = IntPtr.Zero; return; }
            }
            catch (Exception) { }
            base.WndProc(ref m);
            if (m.Msg == ControlNative.WM_MOUSEMOVE || m.Msg == ControlNative.WM_MOUSELEAVE || m.Msg == ControlNative.WM_LBUTTONDOWN || m.Msg == ControlNative.WM_LBUTTONUP)
                ControlNative.InvalidateRect(Handle, IntPtr.Zero, false);
        }

        private void DrawInto(IntPtr hdc)
        {
            ControlNative.Paint(Handle, hdc, ControlNative.ClientRect(Handle), Rectangle.Empty, Draw);
        }

        private void Draw(Graphics g)
        {
            float s = B1Dpi.Scale(list);
            var client = ControlNative.ClientRect(Handle);
            B1Draw.Fill(g, B1Palette.Slate, client);
            var p = ControlNative.CursorIn(Handle);
            int n = Math.Min(ControlNative.HeaderItemCount(Handle), list.Columns.Count);
            for (int i = 0; i < n; i++)
            {
                Rectangle r;
                if (!ControlNative.HeaderItemRect(Handle, i, out r)) continue;
                bool hot = r.Contains(p);
                if (hot) B1Draw.Fill(g, B1Palette.RowActive, r);
                B1Draw.Fill(g, B1Palette.Rule, r.Right - B1Draw.Px(1, s), r.Top + B1Draw.Px(5, s), B1Draw.Px(1, s), Math.Max(0, r.Height - 2 * B1Draw.Px(5, s)));
                var col = list.Columns[i];
                var align = col.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : col.TextAlign == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
                var tr = Rectangle.FromLTRB(r.Left + B1Draw.Px(6, s), r.Top, r.Right - B1Draw.Px(6, s), r.Bottom);
                TextRenderer.DrawText(g, col.Text, list.Font, tr, hot ? B1Palette.Text : B1Palette.TextDim,
                    align | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            }
            B1Draw.Fill(g, B1Palette.Rule, client.Left, client.Bottom - B1Draw.Px(1, s), client.Width, B1Draw.Px(1, s));
        }
    }

    /// <summary>
    /// Couleurs des lignes d'un ListView par NM_CUSTOMDRAW (sans OwnerDraw : cases, groupes, images, vues conservent leur comportement) :
    /// ligne selectionnee RowActive sur Text avec barre d'accent de 2 px, jamais le bleu systeme.
    /// </summary>
    internal sealed class ListViewWindow : NativeWindow
    {
        private const int WM_REFLECT_NOTIFY = 0x2000 + 0x4E;
        private const int NM_CUSTOMDRAW = -12;
        private const int CDDS_PREPAINT = 0x1, CDDS_ITEMPREPAINT = 0x10001, CDDS_ITEMPOSTPAINT = 0x10002, CDDS_SUBITEM = 0x20000;
        private const int CDRF_NEWFONT = 0x2, CDRF_SKIPDEFAULT = 0x4, CDRF_NOTIFYPOSTPAINT = 0x10, CDRF_NOTIFYITEMDRAW = 0x20;
        private const int LVCDI_GROUP = 1, LVM_GETGROUPRECT = 0x1062, LVGGR_HEADER = 1;
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref ControlNative.RECT rect);
        [DllImport("gdi32.dll")] private static extern uint GetLayout(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern uint SetLayout(IntPtr hdc, uint layout);
        private const uint LAYOUT_RTL = 1;
        private const int CDIS_SELECTED = 0x1, CDIS_FOCUS = 0x10, CDIS_HOT = 0x40, CDIS_MARKED = 0x80;

        [StructLayout(LayoutKind.Sequential)]
        private struct NMHDR { public IntPtr hwndFrom; public IntPtr idFrom; public int code; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NMCUSTOMDRAW
        {
            public NMHDR hdr;
            public int dwDrawStage;
            public IntPtr hdc;
            public ControlNative.RECT rc;
            public IntPtr dwItemSpec;
            public int uItemState;
            public IntPtr lItemlParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NMLVCUSTOMDRAW
        {
            public NMCUSTOMDRAW nmcd;
            public int clrText;
            public int clrTextBk;
            public int iSubItem;
            // extension Vista : en-tetes de groupe (dwItemType = LVCDI_GROUP)
            public int dwItemType;
            public int clrFace;
            public int iIconEffect;
            public int iIconPhase;
            public int iPartId;
            public int iStateId;
            public ControlNative.RECT rcText;
            public uint uAlign;
        }

        private static readonly int offState = (int)Marshal.OffsetOf(typeof(NMCUSTOMDRAW), "uItemState");
        private static readonly int offText = (int)Marshal.OffsetOf(typeof(NMLVCUSTOMDRAW), "clrText");
        private static readonly int offBack = (int)Marshal.OffsetOf(typeof(NMLVCUSTOMDRAW), "clrTextBk");
        private static readonly int sizeVista = Marshal.SizeOf(typeof(NMLVCUSTOMDRAW));

        private readonly ListView list;
        public ListViewWindow(ListView l) { list = l; }

        internal static void Hook(ListView l)
        {
            Subclasser.Attach(l, "rows", () => new ListViewWindow(l), null);
        }

        private static int Bgr(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        protected override void WndProc(ref Message m)
        {
            // liste desactivee : la liste native efface avec COLOR_BTNFACE (gris clair) au lieu de sa couleur de fond
            if (m.Msg == ControlNative.WM_ERASEBKGND && m.WParam != IntPtr.Zero && !list.IsDisposed && !list.Enabled)
            {
                try
                {
                    using (var g = Graphics.FromHdc(m.WParam))
                        B1Draw.Fill(g, B1Draw.IsPalette(list.BackColor) ? list.BackColor : B1Palette.Slate, ControlNative.ClientRect(Handle));
                    m.Result = (IntPtr)1;
                    return;
                }
                catch (Exception) { /* cosmetique : dessin natif */ }
            }
            base.WndProc(ref m);
            if (m.Msg != WM_REFLECT_NOTIFY || list.IsDisposed) return;
            try
            {
                var hdr = (NMHDR)Marshal.PtrToStructure(m.LParam, typeof(NMHDR));
                if (hdr.code != NM_CUSTOMDRAW) return;
                var cd = (NMLVCUSTOMDRAW)Marshal.PtrToStructure(m.LParam, typeof(NMLVCUSTOMDRAW));
                int stage = cd.nmcd.dwDrawStage & ~CDDS_SUBITEM;
                int result = (int)m.Result;
                if (stage == CDDS_PREPAINT && cd.dwItemType == LVCDI_GROUP && cd.nmcd.hdc != IntPtr.Zero)
                {
                    // un groupe : le theme peint l'en-tete (titre bleu systeme) ; les elements du groupe restent dessines par la liste
                    PaintGroupHeader(cd);
                    m.Result = (IntPtr)CDRF_SKIPDEFAULT;
                    return;
                }
                if (stage == CDDS_PREPAINT) { m.Result = (IntPtr)(result | CDRF_NOTIFYITEMDRAW); return; }
                if (stage == CDDS_ITEMPREPAINT)
                {
                    var item = ItemAt((int)cd.nmcd.dwItemSpec);
                    // l'etat natif (uItemState) n'est pas fiable hors fenetre affichee : on lit la selection de l'element
                    bool selected = item != null ? item.Selected : (cd.nmcd.uItemState & CDIS_SELECTED) != 0;
                    Color back = selected ? B1Palette.RowActive : (B1Draw.IsPalette(list.BackColor) ? list.BackColor : B1Palette.Slate);
                    Color fore = selected ? B1Palette.Text : (B1Draw.IsPalette(list.ForeColor) ? list.ForeColor : B1Palette.Text);
                    if (item != null && !selected && item.ForeColor != list.ForeColor && B1Draw.IsPalette(item.ForeColor)) fore = item.ForeColor;
                    // element grise par l'application (SystemColors.GrayText : mod deja installe...) : TextDim, la nuance reste visible
                    else if (item != null && !selected && item.ForeColor.ToArgb() == SystemColors.GrayText.ToArgb()) fore = B1Palette.TextDim;
                    Marshal.WriteInt32(m.LParam, offText, Bgr(fore));
                    Marshal.WriteInt32(m.LParam, offBack, Bgr(back));
                    // pas de surbrillance systeme (bleue) : l'etat selectionne / survol est retire, le fond est peint par clrTextBk
                    Marshal.WriteInt32(m.LParam, offState, cd.nmcd.uItemState & ~(CDIS_SELECTED | CDIS_FOCUS | CDIS_HOT | CDIS_MARKED));
                    m.Result = (IntPtr)(result | CDRF_NEWFONT | (selected || NativeChecks ? CDRF_NOTIFYPOSTPAINT : 0));
                    return;
                }
                if (stage == CDDS_ITEMPOSTPAINT && cd.nmcd.hdc != IntPtr.Zero)
                {
                    float s = B1Dpi.Scale(list);
                    var rc = cd.nmcd.rc.ToRectangle();
                    var item = ItemAt((int)cd.nmcd.dwItemSpec);
                    bool selected = item != null ? item.Selected : (cd.nmcd.uItemState & CDIS_SELECTED) != 0;
                    // Fenetre en miroir (RightToLeftLayout) : le DC est en LAYOUT_RTL et les rectangles sont logiques (origine a droite).
                    // GDI+ ne suit pas ce miroir : on passe le DC en disposition normale le temps du dessin et on reflete les x a la main.
                    uint layout = GetLayout(cd.nmcd.hdc);
                    bool mirrored = layout != 0xFFFFFFFF && (layout & LAYOUT_RTL) != 0;
                    if (mirrored) SetLayout(cd.nmcd.hdc, layout & ~LAYOUT_RTL);
                    try
                    {
                        using (var g = Graphics.FromHdc(cd.nmcd.hdc))
                        {
                            int width = list.ClientSize.Width;
                            if (NativeChecks && item != null) PaintCheck(g, item, rc, selected, s, mirrored ? width : -1);
                            if (selected) B1Draw.Fill(g, B1Palette.Accent, Mirror(rc.Left, B1Draw.Px(2, s), mirrored ? width : -1), rc.Top, B1Draw.Px(2, s), rc.Height);
                        }
                    }
                    finally { if (mirrored) SetLayout(cd.nmcd.hdc, layout); }
                }
            }
            catch (Exception) { /* cosmetique */ }
        }

        // Cases a cocher natives (CheckBoxes sans OwnerDraw) : la case claire du systeme est recouverte par la case B1.
        private bool NativeChecks { get { return list.CheckBoxes && !list.OwnerDraw && list.View == View.Details; } }

        /// <summary>x d'un trait de largeur w : tel quel, ou reflete dans une fenetre en miroir de largeur mirrorWidth (sinon -1).</summary>
        internal static int Mirror(int x, int w, int mirrorWidth) { return mirrorWidth < 0 ? x : mirrorWidth - x - w; }

        /// <summary>
        /// Case B1 (B1Draw.CheckGlyph) a la place de la case native : la zone de l'image d'etat (du bord de l'element a l'icone)
        /// est repeinte du fond de la ligne, puis la case est centree dedans. Le clic reste celui de la liste (meme zone).
        /// </summary>
        private void PaintCheck(Graphics g, ListViewItem item, Rectangle rc, bool selected, float s, int mirrorWidth)
        {
            var bounds = item.Bounds;
            var icon = item.GetBounds(ItemBoundsPortion.Icon);
            int left = bounds.Left, right = icon.Left > bounds.Left + 4 ? icon.Left : bounds.Left + B1Draw.Px(20, s);
            Color back = selected ? B1Palette.RowActive : (B1Draw.IsPalette(list.BackColor) ? list.BackColor : B1Palette.Slate);
            B1Draw.Fill(g, back, Mirror(left, right - left, mirrorWidth), rc.Top, right - left, rc.Height);
            int size = B1Draw.GlyphSize(s);
            var box = new Rectangle(Mirror(left + (right - left - size) / 2, size, mirrorWidth), rc.Top + (rc.Height - size) / 2, size, size);
            var st = item.Checked ? B1VisualState.Checked : B1VisualState.Normal;
            if (!list.Enabled) st |= B1VisualState.Disabled;
            B1Draw.CheckGlyph(g, box, st, s, false);
        }

        private static readonly System.Reflection.PropertyInfo groupId = typeof(ListViewGroup).GetProperty("ID", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        private ListViewGroup GroupById(int id)
        {
            try
            {
                if (groupId == null) return null;
                foreach (ListViewGroup g in list.Groups) if ((int)groupId.GetValue(g, null) == id) return g;
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>En-tete de groupe : fond de la liste, titre en capitales espacees (Lavender) puis filet Rule de 1 px ; jamais le bleu systeme.</summary>
        private void PaintGroupHeader(NMLVCUSTOMDRAW cd)
        {
            var rc = cd.nmcd.rc.ToRectangle();                     // tout le groupe : l'en-tete en est le haut
            var hr = new ControlNative.RECT { Top = LVGGR_HEADER };
            if (SendMessage(list.Handle, LVM_GETGROUPRECT, cd.nmcd.dwItemSpec, ref hr) != IntPtr.Zero && hr.Bottom > hr.Top) rc = hr.ToRectangle();
            else rc = new Rectangle(rc.Left, rc.Top, rc.Width, Math.Min(rc.Height, list.Font.Height + 8));
            if (rc.Width <= 0 || rc.Height <= 0) return;
            float s = B1Dpi.Scale(list);
            var group = GroupById((int)cd.nmcd.dwItemSpec);
            string text = group == null ? "" : (group.Header ?? "").Replace("&", "").ToUpper(System.Globalization.CultureInfo.CurrentUICulture);
            PaintGroupHeader(cd.nmcd.hdc, rc, text, list.Font, B1Draw.IsPalette(list.BackColor) ? list.BackColor : B1Palette.Slate,
                list.RightToLeft == RightToLeft.Yes, s, list.ClientSize.Width);
        }

        /// <summary>
        /// En-tete de groupe dans le DC de la liste. rc est logique (dans une liste en miroir, origine a droite). Si le DC est en
        /// LAYOUT_RTL (RightToLeftLayout), GDI+ ne suit pas le miroir et TextRenderer recoit un decoupage GDI+ non reflete (le titre
        /// disparaissait) : comme pour les cases, le DC passe en disposition normale le temps du dessin et rc est reflete a la main.
        /// </summary>
        internal static void PaintGroupHeader(IntPtr hdc, Rectangle rc, string text, Font listFont, Color back, bool rtl, float s, int clientWidth)
        {
            uint layout = GetLayout(hdc);
            bool mirrored = layout != 0xFFFFFFFF && (layout & LAYOUT_RTL) != 0;
            if (mirrored)
            {
                SetLayout(hdc, layout & ~LAYOUT_RTL);
                rc = new Rectangle(Mirror(rc.Left, rc.Width, clientWidth), rc.Top, rc.Width, rc.Height);
            }
            try
            {
                using (var g = Graphics.FromHdc(hdc))
                {
                    B1Draw.Fill(g, back, rc);
                    int pad = B1Draw.Px(6, s), gap = B1Draw.Px(8, s);
                    int textW = 0;
                    if (text.Length > 0)
                    {
                        var caps = B1Draw.CapsFont(listFont);          // police en cache partagee : jamais liberee ici
                        textW = B1Draw.TrackedWidth(g, text, caps, B1Fonts.CapsTracking);
                        int x = rtl ? rc.Right - pad - textW : rc.Left + pad;
                        int y = rc.Top + Math.Max(0, (rc.Height - caps.Height) / 2);
                        B1Draw.DrawTracked(g, text, caps, B1Palette.Lavender, x, y, B1Fonts.CapsTracking);
                    }
                    int ly = rc.Top + rc.Height / 2;
                    int from = rtl ? rc.Left + pad : rc.Left + pad + textW + (textW > 0 ? gap : 0);
                    int to = rtl ? rc.Right - pad - textW - (textW > 0 ? gap : 0) : rc.Right - pad;
                    if (to > from) B1Draw.Fill(g, B1Palette.Rule, from, ly, to - from, B1Draw.Px(1, s));
                }
            }
            finally { if (mirrored) SetLayout(hdc, layout); }
        }

        private ListViewItem ItemAt(int index)
        {
            try { return !list.VirtualMode && index >= 0 && index < list.Items.Count ? list.Items[index] : null; } catch (Exception) { return null; }
        }
    }

    internal static class ListPaint
    {
        /// <summary>TreeView : texte des noeuds dessine par le theme (selection RowActive, jamais bleue).</summary>
        internal static void HookTree(TreeView t)
        {
            if (t.DrawMode == TreeViewDrawMode.Normal)
            {
                t.DrawMode = TreeViewDrawMode.OwnerDrawText;
                t.DrawNode += TreeDrawNode;
            }
        }

        private static void TreeDrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            var t = (TreeView)sender;
            if (e.Node == null || e.Bounds.Width <= 0) { e.DrawDefault = false; return; }
            float s = B1Dpi.Scale(t);
            bool sel = (e.State & TreeNodeStates.Selected) != 0 || e.Node.IsSelected;
            Color back = B1Draw.IsPalette(t.BackColor) ? t.BackColor : B1Palette.Slate;
            var font = e.Node.NodeFont ?? t.Font;
            var r = e.Bounds;
            B1Draw.Fill(e.Graphics, sel ? B1Palette.RowActive : back, r);
            if (sel) B1Draw.Fill(e.Graphics, B1Palette.Accent, r.Left, r.Top, B1Draw.Px(2, s), r.Height);
            Color fore = !t.Enabled ? B1Palette.TextFaint : sel ? B1Palette.Text : B1Draw.IsPalette(e.Node.ForeColor) ? e.Node.ForeColor : B1Draw.IsPalette(t.ForeColor) ? t.ForeColor : B1Palette.Text;
            TextRenderer.DrawText(e.Graphics, e.Node.Text, font, Rectangle.Inflate(r, -B1Draw.Px(2, s), 0), fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            e.DrawDefault = false;
        }

        /// <summary>ListBox (hors CheckedListBox) : lignes dessinees par le theme, hauteur de ligne inchangee.</summary>
        internal static void HookListBox(ListBox lb)
        {
            if (lb is CheckedListBox || lb.DrawMode != DrawMode.Normal) return;
            int h = Math.Max(lb.ItemHeight, lb.Font.Height);       // la hauteur par defaut peut dater d avant le changement de police
            lb.DrawMode = DrawMode.OwnerDrawFixed;
            try { lb.ItemHeight = h; } catch (Exception) { }
            lb.DrawItem += ListBoxDrawItem;
        }

        private static void ListBoxDrawItem(object sender, DrawItemEventArgs e)
        {
            var lb = (ListBox)sender;
            float s = B1Dpi.Scale(lb);
            Color back = B1Draw.IsPalette(lb.BackColor) ? lb.BackColor : B1Palette.RowActive;
            if (e.Index < 0) { B1Draw.Fill(e.Graphics, back, e.Bounds); return; }
            string text = "";
            try { text = lb.GetItemText(lb.Items[e.Index]); } catch (Exception) { }
            B1Draw.PaintListRow(e.Graphics, e.Bounds, text, lb.Font, (e.State & DrawItemState.Selected) != 0, lb.Enabled, back, lb.RightToLeft == RightToLeft.Yes, s);
        }
    }
}
