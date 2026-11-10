using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>
    /// Barre de titre sombre (DWM) et barres de defilement sombres (uxtheme) d'une fenetre.
    /// DWM : attribut 20 (Windows 10 20H1+), repli sur 19 (1809 a 1909) ; ignore sur plus ancien.
    /// Barres de defilement : SetWindowTheme(« DarkMode_Explorer ») sur ListView, TreeView, ListBox, RichTextBox,
    /// TextBox multiligne et conteneurs defilants — des que leur handle existe, et a chaque recreation.
    /// </summary>
    public static class DarkTitleBar
    {
        private static readonly ConditionalWeakTable<Form, object> enabled = new ConditionalWeakTable<Form, object>();

        public static void Enable(Form f)
        {
            if (f == null) return;
            object m;
            if (!enabled.TryGetValue(f, out m))
            {
                enabled.Add(f, new object());
                // le handle d'une fenetre peut etre cree plus tard, ou recree : on rejoue a chaque creation
                f.HandleCreated += (s, e) => Apply((Form)s);
            }
            if (f.IsHandleCreated) Apply(f);
            ThemeScrollBars(f);
        }

        private static void Apply(Form f)
        {
            if (NativeTheme.SetImmersiveDarkMode(f.Handle, true) && f.Visible) NativeTheme.RedrawFrame(f.Handle);
        }

        /// <summary>Applique le theme sombre aux barres de defilement de tous les controles qui en ont.</summary>
        public static void ThemeScrollBars(Control root)
        {
            if (root == null) return;
            if (HasScrollBars(root))
            {
                var theme = root is ListView ? "DarkMode_ItemsView" : "DarkMode_Explorer";      // ItemsView assombrit aussi l entete des colonnes
                NativeTheme.WithHandle(root, "darkscroll", h =>
                {
                    NativeTheme.SetTheme(h, theme, null);
                    if (root is ListView)
                    {
                        var header = NativeTheme.ListViewHeader(h);
                        if (header != IntPtr.Zero) NativeTheme.SetTheme(header, "DarkMode_ItemsView", null);
                    }
                });
            }
            foreach (Control c in root.Controls) ThemeScrollBars(c);
        }

        private static bool HasScrollBars(Control c)
        {
            if (c is ListView || c is TreeView || c is ListBox || c is RichTextBox || c is DataGridView) return true;
            var tb = c as TextBox;
            if (tb != null) return tb.Multiline;
            var sc = c as ScrollableControl;
            return sc != null && !(c is Form) && !(c is ToolStrip) && sc.AutoScroll;
        }
    }
}
