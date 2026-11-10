using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>Appels Win32 du theme (DWM, uxtheme, GDI). Tout echoue en silence : le theme est cosmetique.</summary>
    internal static class NativeTheme
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        [DllImport("gdi32.dll")]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int LVM_GETHEADER = 0x101F;

        /// <summary>Handle de l'entete de colonnes d'un ListView (SysHeader32), ou Zero.</summary>
        internal static IntPtr ListViewHeader(IntPtr listView)
        {
            try { return SendMessage(listView, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero); } catch (Exception) { return IntPtr.Zero; }
        }

        private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

        /// <summary>Attribut DWM « immersive dark mode » : 20 depuis Windows 10 20H1 (build 19041), 19 avant (1809 a 1909).</summary>
        internal static bool SetImmersiveDarkMode(IntPtr hwnd, bool dark)
        {
            try
            {
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int)) == 0) return true;
                v = dark ? 1 : 0;
                return DwmSetWindowAttribute(hwnd, 19, ref v, sizeof(int)) == 0;
            }
            catch (Exception) { return false; }       // DllNotFound / EntryPointNotFound : Windows trop ancien
        }

        internal static void RedrawFrame(IntPtr hwnd)
        {
            try { SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED); }
            catch (Exception) { }
        }

        internal static void SetTheme(IntPtr hwnd, string app, string idList)
        {
            try { SetWindowTheme(hwnd, app, idList); } catch (Exception) { }
        }

        /// <summary>Rend la police visible de GDI (TextRenderer, Label, menus) pour la duree du processus.</summary>
        internal static bool AddFontToGdi(IntPtr data, int length)
        {
            try
            {
                uint n = 0;
                return AddFontMemResourceEx(data, (uint)length, IntPtr.Zero, ref n) != IntPtr.Zero;
            }
            catch (Exception) { return false; }
        }

        private static readonly ConditionalWeakTable<Control, object> hooked = new ConditionalWeakTable<Control, object>();

        /// <summary>
        /// Execute action avec le handle du controle : tout de suite s'il existe, et a chaque (re)creation du handle.
        /// Une action par cle et par controle : rappeler avec la meme cle remplace l'action, sans empiler de gestionnaires.
        /// </summary>
        internal static void WithHandle(Control c, string key, Action<IntPtr> action)
        {
            object marker;
            if (!hooked.TryGetValue(c, out marker))
            {
                var actions = new Dictionary<string, Action<IntPtr>>();
                hooked.Add(c, actions);
                c.HandleCreated += (s, e) =>
                {
                    foreach (var a in new List<Action<IntPtr>>(actions.Values)) a(((Control)s).Handle);
                };
                marker = actions;
            }
            ((Dictionary<string, Action<IntPtr>>)marker)[key] = action;
            if (c.IsHandleCreated) action(c.Handle);
        }
    }
}
