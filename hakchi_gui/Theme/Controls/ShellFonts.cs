using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>Polices B1 d'un controle de la coque, mises a l'echelle (B1Dpi) et liberees avec lui.</summary>
    internal sealed class ShellFonts : IDisposable
    {
        public enum Kind { Body, Title, Small, Caps }

        private readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        public Font Get(Kind kind, float pt, float scale)
        {
            var key = kind + "|" + Math.Round(pt * scale, 1).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "|" + B1Fonts.JostLoaded;
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            float size = (float)Math.Round(pt * scale, 1);
            switch (kind)
            {
                case Kind.Title: f = B1Fonts.Title(size); break;
                case Kind.Small: f = B1Fonts.Small(size); break;
                case Kind.Caps: f = B1Fonts.Caps(size); break;
                default: f = B1Fonts.Body(size); break;
            }
            fonts[key] = f;
            return f;
        }

        public void Dispose()
        {
            foreach (var f in fonts.Values) f.Dispose();
            fonts.Clear();
        }
    }

    internal static class ShellText
    {
        public const TextFormatFlags Line = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

        /// <summary>Nom de systeme affiche : sans le prefixe de fabricant des noms libretro (« Nintendo - Super Nintendo... »).</summary>
        public static string SystemDisplay(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int i = name.IndexOf(" - ", StringComparison.Ordinal);
            return i > 0 && i + 3 < name.Length ? name.Substring(i + 3) : name;
        }

        public static int Width(string text, Font f)
        {
            return TextRenderer.MeasureText(text, f, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
        }
    }
}
