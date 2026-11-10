using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Appels Win32 des controles dessines (peinture, GetComboBoxInfo, en-tete de ListView). Tout est cosmetique : un echec ne doit jamais lever.</summary>
    internal static class ControlNative
    {
        internal const int WM_ERASEBKGND = 0x14, WM_PAINT = 0x0F, WM_PRINT = 0x317, WM_PRINTCLIENT = 0x318;
        internal const int WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202, WM_MOUSELEAVE = 0x2A3, WM_CAPTURECHANGED = 0x215;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int Left, Top, Right, Bottom; public Rectangle ToRectangle() { return Rectangle.FromLTRB(Left, Top, Right, Bottom); } }

        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public int fErase;
            public RECT rcPaint;
            public int fRestore;
            public int fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct COMBOBOXINFO
        {
            public int cbSize;
            public RECT rcItem;
            public RECT rcButton;
            public int stateButton;
            public IntPtr hwndCombo, hwndItem, hwndList;
        }

        [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hwnd, out PAINTSTRUCT ps);
        [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hwnd, ref PAINTSTRUCT ps);
        [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(IntPtr hwnd, ref COMBOBOXINFO info);
        [DllImport("user32.dll")] internal static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point p);
        [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hwnd, ref Point p);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref RECT lParam);
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        internal const int WS_BORDER = 0x800000, WS_EX_WINDOWEDGE = 0x100, WS_EX_CLIENTEDGE = 0x200, WS_EX_STATICEDGE = 0x20000;

        /// <summary>Retire le cadre natif (1 px en relief) d'une fenetre : le client occupe alors toute la fenetre.</summary>
        internal static void StripBorder(IntPtr hwnd)
        {
            try
            {
                int style = GetWindowLong(hwnd, -16), ex = GetWindowLong(hwnd, -20);
                int ns = style & ~WS_BORDER, ne = ex & ~(WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE);
                if (ns == style && ne == ex) return;
                SetWindowLong(hwnd, -16, ns);
                SetWindowLong(hwnd, -20, ne);
                NativeTheme.RedrawFrame(hwnd);
            }
            catch (Exception) { }
        }

        internal static Rectangle ClientRect(IntPtr hwnd)
        {
            RECT r;
            return GetClientRect(hwnd, out r) ? new Rectangle(0, 0, r.Right, r.Bottom) : Rectangle.Empty;
        }

        /// <summary>Position du curseur dans le repere client de la fenetre, ou (-1,-1).</summary>
        internal static Point CursorIn(IntPtr hwnd)
        {
            Point p;
            if (!GetCursorPos(out p)) return new Point(-1, -1);
            ScreenToClient(hwnd, ref p);
            return p;
        }

        internal static bool TryComboInfo(ComboBox c, out COMBOBOXINFO info)
        {
            info = new COMBOBOXINFO { cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO)) };
            try { return c.IsHandleCreated && GetComboBoxInfo(c.Handle, ref info); } catch (Exception) { return false; }
        }

        private const int HDM_GETITEMRECT = 0x1207, HDM_GETITEMCOUNT = 0x1200;

        internal static int HeaderItemCount(IntPtr header)
        {
            try { return (int)SendMessage(header, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero); } catch (Exception) { return 0; }
        }

        internal static bool HeaderItemRect(IntPtr header, int index, out Rectangle r)
        {
            RECT rc = new RECT();
            r = Rectangle.Empty;
            try
            {
                if (SendMessage(header, HDM_GETITEMRECT, (IntPtr)index, ref rc) == IntPtr.Zero) return false;
                r = rc.ToRectangle();
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Peint la fenetre dans un tampon puis le copie : WM_PAINT (BeginPaint) si hdc vaut Zero, sinon dans le contexte
        /// recu (WM_PRINT / WM_PRINTCLIENT). exclude : zone laissee a une fenetre fille.
        /// </summary>
        internal static void Paint(IntPtr hwnd, IntPtr hdc, Rectangle client, Rectangle exclude, Action<Graphics> draw)
        {
            PAINTSTRUCT ps = new PAINTSTRUCT();
            bool own = hdc == IntPtr.Zero;
            if (own) hdc = BeginPaint(hwnd, out ps);
            try
            {
                if (client.Width <= 0 || client.Height <= 0) return;
                using (var g = Graphics.FromHdc(hdc))
                {
                    if (!exclude.IsEmpty) g.ExcludeClip(exclude);
                    using (var bg = BufferedGraphicsManager.Current.Allocate(g, client))
                    {
                        draw(bg.Graphics);
                        bg.Render(g);
                    }
                }
            }
            finally { if (own) EndPaint(hwnd, ref ps); }
        }
    }
}
