using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    [Flags]
    public enum B1VisualState
    {
        Normal = 0,
        Hover = 1,
        Pressed = 2,
        Focus = 4,
        Disabled = 8,
        Checked = 16,
        Indeterminate = 32,
    }

    public enum B1ButtonKind { Primary, Secondary, Quiet, Danger }

    /// <summary>
    /// Echelle des controles dessines. Le processus n'est pas DPI-aware (Windows met alors l'ecran a l'echelle) :
    /// DeviceDpi vaut 96 et le facteur est 1. Les controles honorent quand meme DeviceDpi, pour qu'un passage futur
    /// en DPI-aware ne demande rien d'autre ; Override sert a l'emulation (galerie, tests).
    /// </summary>
    public static class B1Dpi
    {
        /// <summary>0 = utiliser DeviceDpi ; sinon facteur impose (1,5 = 144 dpi).</summary>
        public static float Override;

        public static float Scale(Control c)
        {
            if (Override > 0f) return Override;
            try { return c == null ? 1f : Math.Max(1f, c.DeviceDpi / 96f); } catch (Exception) { return 1f; }
        }

        /// <summary>Longueur en pixels d'une cote de conception (96 dpi), au moins 1 px.</summary>
        public static int Px(float v, float s) { return Math.Max(1, (int)Math.Round(v * s, MidpointRounding.AwayFromZero)); }
    }

    /// <summary>Etat visuel force d'un controle (galerie, tests) : remplace l'etat reel (souris, focus).</summary>
    public static class B1Preview
    {
        private sealed class Box { public B1VisualState State; }
        private static readonly ConditionalWeakTable<Control, Box> table = new ConditionalWeakTable<Control, Box>();

        public static void Set(Control c, B1VisualState state)
        {
            Box b;
            if (table.TryGetValue(c, out b)) b.State = state; else table.Add(c, new Box { State = state });
            c.Invalidate();
        }

        public static bool TryGet(Control c, out B1VisualState state)
        {
            Box b;
            if (table.TryGetValue(c, out b)) { state = b.State; return true; }
            state = B1VisualState.Normal;
            return false;
        }
    }

    /// <summary>Tout le dessin des controles B1. Aplats et filets de 1 px, jamais de degrade, d'ombre ni de halo ; uniquement des couleurs de B1Palette.</summary>
    public static class B1Draw
    {
        public static int Px(float v, float s) { return B1Dpi.Px(v, s); }

        // --- primitives -----------------------------------------------------------------------------------

        public static void Fill(Graphics g, Color c, int x, int y, int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            using (var b = new SolidBrush(c)) g.FillRectangle(b, x, y, w, h);
        }

        public static void Fill(Graphics g, Color c, Rectangle r) { Fill(g, c, r.X, r.Y, r.Width, r.Height); }

        /// <summary>Cadre de t pixels, dessine vers l'interieur de r.</summary>
        public static void Frame(Graphics g, Color c, Rectangle r, int t)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            t = Math.Min(t, Math.Min(r.Width, r.Height) / 2 + 1);
            Fill(g, c, r.Left, r.Top, r.Width, t);
            Fill(g, c, r.Left, r.Bottom - t, r.Width, t);
            Fill(g, c, r.Left, r.Top + t, t, r.Height - 2 * t);
            Fill(g, c, r.Right - t, r.Top + t, t, r.Height - 2 * t);
        }

        public static bool IsPalette(Color c)
        {
            if (c.A != 255) return false;
            foreach (var p in B1Palette.All) if (p.ToArgb() == c.ToArgb()) return true;
            return false;
        }

        /// <summary>Couleur de fond visible derriere un controle (premier ancetre non transparent), toujours de la palette.</summary>
        public static Color Backdrop(Control c)
        {
            for (var p = c; p != null; p = p.Parent)
                if (p.BackColor.A == 255 && IsPalette(p.BackColor)) return p.BackColor;
            return B1Palette.Slate;
        }

        private static readonly Dictionary<string, Font> capsFonts = new Dictionary<string, Font>();

        /// <summary>Police des etiquettes en capitales : Jost Regular a 83 % de la taille de la police du controle (7,5 pt pour 9 pt).</summary>
        public static Font CapsFont(Font basis)
        {
            float pt = (float)Math.Round(basis.SizeInPoints * 7.5f / 9f, 1);
            var key = pt.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "|" + B1Fonts.JostLoaded;
            lock (capsFonts)
            {
                Font f;
                if (!capsFonts.TryGetValue(key, out f)) { f = B1Fonts.Caps(pt); capsFonts[key] = f; }
                return f;
            }
        }

        private const TextFormatFlags Raw = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.Top;

        private static int CharWidth(Graphics g, string s, Font f)
        {
            return TextRenderer.MeasureText(g, s, f, new Size(int.MaxValue, int.MaxValue), Raw).Width;
        }

        /// <summary>Largeur d'un texte avec espacement (em) ajoute entre les lettres.</summary>
        public static int TrackedWidth(Graphics g, string text, Font f, float trackEm)
        {
            float extra = trackEm * f.SizeInPoints * g.DpiX / 72f;
            float w = 0;
            int space = Math.Max(1, CharWidth(g, "a a", f) - 2 * CharWidth(g, "a", f));
            foreach (char ch in text) w += (ch == ' ' ? space : CharWidth(g, ch.ToString(), f)) + extra;
            return (int)Math.Round(w);
        }

        /// <summary>Texte en capitales espacees (0,14 em) : GDI ne sait pas espacer les lettres, on les pose une a une.</summary>
        public static void DrawTracked(Graphics g, string text, Font f, Color c, int x, int y, float trackEm)
        {
            float extra = trackEm * f.SizeInPoints * g.DpiX / 72f;
            float cx = x;
            int space = Math.Max(1, CharWidth(g, "a a", f) - 2 * CharWidth(g, "a", f));
            foreach (char ch in text)
            {
                if (ch == ' ') { cx += space + extra; continue; }
                var s = ch.ToString();
                TextRenderer.DrawText(g, s, f, new Point((int)Math.Round(cx), y), c, Raw);
                cx += CharWidth(g, s, f) + extra;
            }
        }

        // --- boutons --------------------------------------------------------------------------------------

        public static B1VisualState StockState(ButtonBase b)
        {
            B1VisualState forced;
            if (B1Preview.TryGet(b, out forced)) return forced;
            var st = B1VisualState.Normal;
            if (!b.Enabled) return B1VisualState.Disabled;
            if (b.IsHandleCreated)
            {
                bool over = false;
                try { over = b.ClientRectangle.Contains(b.PointToClient(Cursor.Position)); } catch (Exception) { }
                if (over) st |= B1VisualState.Hover;
                if (over && (Control.MouseButtons & MouseButtons.Left) != 0 && b.Capture) st |= B1VisualState.Pressed;
                if (b.Focused && ShowFocusCues(b)) st |= B1VisualState.Focus;
            }
            return st;
        }

        private static readonly PropertyInfo showFocusCues = typeof(Control).GetProperty("ShowFocusCues", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool ShowFocusCues(Control c)
        {
            try { return showFocusCues == null || (bool)showFocusCues.GetValue(c, null); } catch (Exception) { return true; }
        }

        private static readonly PropertyInfo showKeyboardCues = typeof(Control).GetProperty("ShowKeyboardCues", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool ShowKeyboardCues(Control c)
        {
            try { return showKeyboardCues == null || (bool)showKeyboardCues.GetValue(c, null); } catch (Exception) { return true; }
        }

        private static void FocusRing(Graphics g, Rectangle r, float s)
        {
            int d = Px(2, s);
            Frame(g, B1Palette.Lavender, Rectangle.Inflate(r, -d, -d), Px(1, s));
        }

        /// <summary>Fond, filet et cadre de focus d'un bouton. Retourne la couleur du texte.</summary>
        public static Color ButtonFrame(Graphics g, Rectangle r, B1ButtonKind kind, B1VisualState st, float s, Color backdrop)
        {
            bool dis = (st & B1VisualState.Disabled) != 0, hot = (st & B1VisualState.Hover) != 0, down = (st & B1VisualState.Pressed) != 0;
            Color fill, border, text;
            bool hasBorder = true;
            switch (kind)
            {
                case B1ButtonKind.Primary:
                    fill = dis ? B1Palette.RowActive : down ? B1Palette.Slate : hot ? B1Palette.Accent : B1Palette.Primary;
                    border = down ? B1Palette.Accent : fill;
                    text = dis ? B1Palette.TextFaint : B1Palette.TextOnPrimary;
                    break;
                case B1ButtonKind.Quiet:
                    fill = down ? B1Palette.Rule : hot ? B1Palette.RowActive : backdrop;
                    border = fill; hasBorder = false;
                    text = dis ? B1Palette.TextFaint : hot || down ? B1Palette.Text : B1Palette.TextSecondary;
                    break;
                case B1ButtonKind.Danger:
                    fill = dis ? B1Palette.Slate : down ? B1Palette.Rule : hot ? B1Palette.RowActive : B1Palette.Slate;
                    border = dis ? B1Palette.Rule : B1Palette.Error;
                    text = dis ? B1Palette.TextFaint : B1Palette.Error;
                    break;
                default:
                    fill = dis ? B1Palette.Slate : down ? B1Palette.Rule : hot ? B1Palette.RowActive : B1Palette.Slate;
                    border = dis ? B1Palette.Rule : B1Palette.Lavender;
                    text = dis ? B1Palette.TextFaint : B1Palette.Lavender;
                    break;
            }
            Fill(g, backdrop, r);
            Fill(g, fill, r);
            if (hasBorder) Frame(g, border, r, Px(1, s));
            if ((st & B1VisualState.Focus) != 0 && !dis) FocusRing(g, r, s);
            return text;
        }

        private static TextFormatFlags Align(ContentAlignment a, bool rtl)
        {
            TextFormatFlags f = 0;
            switch (a)
            {
                case ContentAlignment.TopLeft: case ContentAlignment.MiddleLeft: case ContentAlignment.BottomLeft: f |= rtl ? TextFormatFlags.Right : TextFormatFlags.Left; break;
                case ContentAlignment.TopRight: case ContentAlignment.MiddleRight: case ContentAlignment.BottomRight: f |= rtl ? TextFormatFlags.Left : TextFormatFlags.Right; break;
                default: f |= TextFormatFlags.HorizontalCenter; break;
            }
            switch (a)
            {
                case ContentAlignment.TopLeft: case ContentAlignment.TopCenter: case ContentAlignment.TopRight: f |= TextFormatFlags.Top; break;
                case ContentAlignment.BottomLeft: case ContentAlignment.BottomCenter: case ContentAlignment.BottomRight: f |= TextFormatFlags.Bottom; break;
                default: f |= TextFormatFlags.VerticalCenter; break;
            }
            return f;
        }

        private static Rectangle PlaceImage(Rectangle area, Size size, ContentAlignment a)
        {
            int x = area.Left + (area.Width - size.Width) / 2, y = area.Top + (area.Height - size.Height) / 2;
            switch (a)
            {
                case ContentAlignment.TopLeft: case ContentAlignment.MiddleLeft: case ContentAlignment.BottomLeft: x = area.Left; break;
                case ContentAlignment.TopRight: case ContentAlignment.MiddleRight: case ContentAlignment.BottomRight: x = area.Right - size.Width; break;
            }
            switch (a)
            {
                case ContentAlignment.TopLeft: case ContentAlignment.TopCenter: case ContentAlignment.TopRight: y = area.Top; break;
                case ContentAlignment.BottomLeft: case ContentAlignment.BottomCenter: case ContentAlignment.BottomRight: y = area.Bottom - size.Height; break;
            }
            return new Rectangle(x, y, size.Width, size.Height);
        }

        /// <summary>Texte et image d'un bouton, dans la zone interieure du bouton.</summary>
        public static void ButtonContent(Graphics g, Rectangle r, ButtonBase b, Color color, float s)
        {
            bool rtl = b.RightToLeft == RightToLeft.Yes;
            var area = Rectangle.Inflate(r, -Px(6, s), -Px(1, s));
            var mnemo = !b.UseMnemonic ? TextFormatFlags.NoPrefix : ShowKeyboardCues(b) ? 0 : TextFormatFlags.HidePrefix;
            var flags = Align(b.TextAlign, rtl) | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | mnemo;
            bool hasText = !string.IsNullOrEmpty(b.Text);
            var img = b.Image;
            if (img == null)
            {
                if (hasText) TextRenderer.DrawText(g, b.Text, b.Font, area, color, flags);
                return;
            }
            var isz = img.Size;
            if (!hasText) { g.DrawImage(img, PlaceImage(area, isz, b.ImageAlign)); return; }
            int gap = Px(4, s);
            var tsz = TextRenderer.MeasureText(g, b.Text, b.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            switch (b.TextImageRelation)
            {
                case TextImageRelation.ImageBeforeText:
                case TextImageRelation.TextBeforeImage:
                {
                    bool imgFirst = (b.TextImageRelation == TextImageRelation.ImageBeforeText) ^ rtl;
                    int total = isz.Width + gap + tsz.Width;
                    int x0 = area.Left + Math.Max(0, (area.Width - total) / 2);
                    var ir = new Rectangle(imgFirst ? x0 : x0 + tsz.Width + gap, area.Top + (area.Height - isz.Height) / 2, isz.Width, isz.Height);
                    var tr = new Rectangle(imgFirst ? x0 + isz.Width + gap : x0, area.Top, tsz.Width + 2, area.Height);
                    g.DrawImage(img, ir);
                    TextRenderer.DrawText(g, b.Text, b.Font, tr, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | mnemo);
                    break;
                }
                case TextImageRelation.ImageAboveText:
                case TextImageRelation.TextAboveImage:
                {
                    bool imgFirst = b.TextImageRelation == TextImageRelation.ImageAboveText;
                    int total = isz.Height + gap + tsz.Height;
                    int y0 = area.Top + Math.Max(0, (area.Height - total) / 2);
                    var ir = new Rectangle(area.Left + (area.Width - isz.Width) / 2, imgFirst ? y0 : y0 + tsz.Height + gap, isz.Width, isz.Height);
                    var tr = new Rectangle(area.Left, imgFirst ? y0 + isz.Height + gap : y0, area.Width, tsz.Height);
                    g.DrawImage(img, ir);
                    TextRenderer.DrawText(g, b.Text, b.Font, tr, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | mnemo);
                    break;
                }
                default:
                    g.DrawImage(img, PlaceImage(area, isz, b.ImageAlign));
                    TextRenderer.DrawText(g, b.Text, b.Font, area, color, flags);
                    break;
            }
        }

        public static void PaintButton(Graphics g, ButtonBase b, B1ButtonKind kind, B1VisualState st)
        {
            float s = B1Dpi.Scale(b);
            var r = b.ClientRectangle;
            var color = ButtonFrame(g, r, kind, st, s, Backdrop(b));
            ButtonContent(g, r, b, color, s);
        }

        // --- puces (RadioButton en apparence de bouton) ---------------------------------------------------

        public static void PaintChip(Graphics g, ButtonBase b, B1VisualState st)
        {
            float s = B1Dpi.Scale(b);
            var r = b.ClientRectangle;
            bool dis = (st & B1VisualState.Disabled) != 0, hot = (st & B1VisualState.Hover) != 0, down = (st & B1VisualState.Pressed) != 0, on = (st & B1VisualState.Checked) != 0;
            Color border = dis ? (on ? B1Palette.Dashed : B1Palette.Rule) : on ? B1Palette.Lavender : hot || down ? B1Palette.Dashed : B1Palette.Rule;
            Color text = dis ? (on ? B1Palette.TextDim : B1Palette.TextFaint) : on ? B1Palette.Lavender : hot || down ? B1Palette.Text : B1Palette.TextSecondary;
            Color fill = !dis && down ? B1Palette.Rule : !dis && hot ? B1Palette.RowActive : B1Palette.Slate;
            Fill(g, Backdrop(b), r);
            Fill(g, fill, r);
            Frame(g, border, r, Px(1, s));
            if ((st & B1VisualState.Focus) != 0 && !dis) FocusRing(g, r, s);
            ButtonContent(g, r, b, text, s);
        }

        // --- cases et boutons radio -----------------------------------------------------------------------

        public static int GlyphSize(float s) { return Px(12, s); }

        public static void CheckGlyph(Graphics g, Rectangle box, B1VisualState st, float s, bool radio)
        {
            bool dis = (st & B1VisualState.Disabled) != 0, hot = (st & B1VisualState.Hover) != 0, down = (st & B1VisualState.Pressed) != 0;
            bool on = (st & B1VisualState.Checked) != 0, mixed = (st & B1VisualState.Indeterminate) != 0;
            Color border = dis ? B1Palette.Rule : on || mixed || hot || down ? B1Palette.Lavender : B1Palette.Dashed;
            Color fill = dis ? B1Palette.Slate : down ? B1Palette.Rule : B1Palette.RowActive;
            Color mark = dis ? B1Palette.TextFaint : B1Palette.Lavender;
            int t = Px(1, s);
            var old = g.SmoothingMode;
            if (radio)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(fill)) g.FillEllipse(b, box.X, box.Y, box.Width - 1, box.Height - 1);
                using (var p = new Pen(border, t)) g.DrawEllipse(p, box.X + t / 2f, box.Y + t / 2f, box.Width - 1 - t, box.Height - 1 - t);
                if (on) using (var b = new SolidBrush(mark)) { float d = box.Width * 0.5f; g.FillEllipse(b, box.X + (box.Width - d) / 2f - 0.5f, box.Y + (box.Height - d) / 2f - 0.5f, d, d); }
            }
            else
            {
                g.SmoothingMode = SmoothingMode.None;
                Fill(g, fill, box);
                Frame(g, border, box, t);
                if (on)
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    float u = box.Width / 12f;
                    using (var p = new Pen(mark, Math.Max(1.4f, 1.6f * s)) { StartCap = LineCap.Square, EndCap = LineCap.Square, LineJoin = LineJoin.Miter })
                        g.DrawLines(p, new[] { new PointF(box.X + 3f * u, box.Y + 6.2f * u), new PointF(box.X + 5.2f * u, box.Y + 8.4f * u), new PointF(box.X + 9.2f * u, box.Y + 3.6f * u) });
                }
                else if (mixed)
                {
                    g.SmoothingMode = SmoothingMode.None;
                    int w = box.Width / 2, h = Math.Max(2, Px(2, s));
                    Fill(g, mark, box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
                }
            }
            g.SmoothingMode = old;
        }

        /// <summary>Case ou radio (apparence normale) : glyphe, texte, cadre de focus.</summary>
        public static void PaintToggle(Graphics g, ButtonBase b, B1VisualState st, bool radio, ContentAlignment checkAlign)
        {
            float s = B1Dpi.Scale(b);
            var r = b.ClientRectangle;
            Fill(g, Backdrop(b), r);
            bool rtl = b.RightToLeft == RightToLeft.Yes;
            int box = GlyphSize(s), gap = Px(6, s), pad = Px(1, s);
            bool right;
            switch (checkAlign)
            {
                case ContentAlignment.TopRight: case ContentAlignment.MiddleRight: case ContentAlignment.BottomRight: right = !rtl; break;
                default: right = rtl; break;
            }
            int gy;
            switch (checkAlign)
            {
                case ContentAlignment.TopLeft: case ContentAlignment.TopCenter: case ContentAlignment.TopRight: gy = r.Top + Px(2, s); break;
                case ContentAlignment.BottomLeft: case ContentAlignment.BottomCenter: case ContentAlignment.BottomRight: gy = r.Bottom - box - Px(2, s); break;
                default: gy = r.Top + (r.Height - box) / 2; break;
            }
            int gx = right ? r.Right - box - pad : r.Left + pad;
            bool dis = (st & B1VisualState.Disabled) != 0;
            CheckGlyph(g, new Rectangle(gx, gy, box, box), st, s, radio);
            if (string.IsNullOrEmpty(b.Text)) return;
            var tr = right ? new Rectangle(r.Left, r.Top, Math.Max(0, gx - gap - r.Left), r.Height)
                           : new Rectangle(gx + box + gap, r.Top, Math.Max(0, r.Right - (gx + box + gap)), r.Height);
            Color color = dis ? B1Palette.TextFaint : IsPalette(b.ForeColor) ? b.ForeColor : B1Palette.Text;
            var flags = (right ? TextFormatFlags.Right : TextFormatFlags.Left) | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            if (!b.AutoSize && !b.Text.Contains("\n")) flags |= TextFormatFlags.WordBreak; else flags |= TextFormatFlags.SingleLine;
            if (!b.UseMnemonic) flags |= TextFormatFlags.NoPrefix; else if (!ShowKeyboardCues(b)) flags |= TextFormatFlags.HidePrefix;
            TextRenderer.DrawText(g, b.Text, b.Font, tr, color, flags);
            if ((st & B1VisualState.Focus) != 0 && !dis)
            {
                var sz = TextRenderer.MeasureText(g, b.Text, b.Font, new Size(Math.Max(1, tr.Width), int.MaxValue), (flags & ~(TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter)) | TextFormatFlags.Top);
                int fw = Math.Min(tr.Width, sz.Width), fh = Math.Min(tr.Height, sz.Height);
                int fx = right ? tr.Right - fw : tr.Left, fy = tr.Top + (tr.Height - fh) / 2;
                int e = Px(2, s);
                var fr = Rectangle.Intersect(Rectangle.Inflate(new Rectangle(fx, fy, fw, fh), e, e), r);
                Frame(g, B1Palette.Lavender, fr, Px(1, s));
            }
        }

        // --- groupes --------------------------------------------------------------------------------------

        /// <summary>Titre en capitales (TextFaint, 0,14 em) sous un filet Rule en haut ; pas de cadre ferme.</summary>
        public static void PaintGroup(Graphics g, GroupBox gb, Rectangle client, Color back, float s)
        {
            // seule la marge hors de la zone des enfants est repeinte : en WM_PRINT les enfants sont deja dessines
            using (var region = new Region(client))
            {
                region.Exclude(gb.DisplayRectangle);
                using (var br = new SolidBrush(back)) g.FillRegion(br, region);
            }
            Fill(g, B1Palette.Rule, client.Left, client.Top, client.Width, Px(1, s));
            if (string.IsNullOrEmpty(gb.Text)) return;
            var caps = CapsFont(gb.Font);
            string text = gb.Text.Replace("&", "").ToUpper(System.Globalization.CultureInfo.CurrentUICulture);
            int w = TrackedWidth(g, text, caps, B1Fonts.CapsTracking);
            bool rtl = gb.RightToLeft == RightToLeft.Yes;
            int x = rtl ? client.Right - Px(3, s) - w : client.Left + Px(3, s);
            DrawTracked(g, text, caps, B1Palette.TextFaint, x, client.Top + Px(5, s), B1Fonts.CapsTracking);
        }

        // --- barre de progression -------------------------------------------------------------------------

        public static void PaintProgress(Graphics g, Rectangle r, Color back, int min, int max, int value, bool marquee, float marqueePhase, float s)
        {
            Fill(g, back, r);
            int h = Math.Min(Px(6, s), r.Height);
            var track = new Rectangle(r.Left, r.Top + (r.Height - h) / 2, r.Width, h);
            Fill(g, B1Palette.Rule, track);
            if (marquee)
            {
                int seg = Math.Max(1, track.Width * 3 / 10);
                int x = track.Left - seg + (int)((track.Width + seg) * marqueePhase);
                int x0 = Math.Max(track.Left, x), x1 = Math.Min(track.Right, x + seg);
                Fill(g, B1Palette.Accent, x0, track.Top, x1 - x0, track.Height);
                return;
            }
            int range = max - min;
            if (range <= 0) return;
            int w = (int)Math.Round(track.Width * (double)(Math.Max(min, Math.Min(max, value)) - min) / range);
            Fill(g, B1Palette.Accent, track.Left, track.Top, w, track.Height);
        }

        // --- onglets --------------------------------------------------------------------------------------

        public static void PaintTabs(Graphics g, TabControl tabs, int hot)
        {
            if (tabs.RightToLeft == RightToLeft.Yes || tabs.Alignment != TabAlignment.Top || tabs.TabCount == 0) return;
            float s = B1Dpi.Scale(tabs);
            var client = tabs.ClientRectangle;
            var disp = tabs.DisplayRectangle;
            int bottom = 0;
            for (int i = 0; i < tabs.TabCount; i++) bottom = Math.Max(bottom, tabs.GetTabRect(i).Bottom);
            int line = Math.Min(bottom, disp.Top) - 1;
            // bande et marges autour de la page : fond du theme (la page est un controle enfant, on ne la touche pas)
            using (var region = new Region(client))
            {
                region.Exclude(disp);
                using (var b = new SolidBrush(B1Palette.Slate)) g.FillRegion(b, region);
            }
            Fill(g, B1Palette.Rule, 0, line, client.Width, Px(1, s));
            int u = Px(2, s);
            for (int i = 0; i < tabs.TabCount; i++)
            {
                var r = tabs.GetTabRect(i);
                bool selected = i == tabs.SelectedIndex;
                var page = tabs.TabPages[i];
                if (selected) Fill(g, B1Palette.Accent, r.Left, line + 1 - u, r.Width, u);
                var tr = new Rectangle(r.Left, r.Top, r.Width, Math.Max(1, line + 1 - u - r.Top));
                var color = !page.Enabled ? B1Palette.TextFaint : selected || i == hot ? B1Palette.Text : B1Palette.TextSecondary;
                var il = tabs.ImageList;
                if (il != null && page.ImageIndex >= 0 && page.ImageIndex < il.Images.Count)
                {
                    var img = il.Images[page.ImageIndex];
                    g.DrawImage(img, tr.Left + Px(6, s), tr.Top + (tr.Height - img.Height) / 2, img.Width, img.Height);
                    tr = new Rectangle(tr.Left + Px(6, s) + img.Width, tr.Top, Math.Max(0, tr.Width - Px(6, s) - img.Width), tr.Height);
                }
                TextRenderer.DrawText(g, page.Text, tabs.Font, tr, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                if (selected && tabs.Focused && ShowFocusCues(tabs))
                    Frame(g, B1Palette.Lavender, Rectangle.Inflate(r, -Px(2, s), -Px(2, s)), Px(1, s));
            }
        }

        // --- listes ---------------------------------------------------------------------------------------

        /// <summary>Ligne d'une liste : fond de la liste, ou RowActive avec une barre d'accent de 2 px quand elle est active.</summary>
        public static void PaintListRow(Graphics g, Rectangle r, string text, Font font, bool active, bool enabled, Color back, bool rtl, float s)
        {
            Fill(g, active ? B1Palette.RowActive : back, r);
            if (active) Fill(g, B1Palette.Accent, rtl ? r.Right - Px(2, s) : r.Left, r.Top, Px(2, s), r.Height);
            var tr = Rectangle.Inflate(r, -Px(6, s), 0);
            TextRenderer.DrawText(g, text ?? "", font, tr, !enabled ? B1Palette.TextFaint : active ? B1Palette.Text : B1Palette.TextSecondary,
                (rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left) | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        /// <summary>Chevron vers le bas (9 x 5 px a 96 dpi), trait de 1 px.</summary>
        public static void Chevron(Graphics g, Rectangle area, Color c, float s, bool up)
        {
            int w = Px(9, s) | 1, h = (w - 1) / 2 + 1;
            int x = area.Left + (area.Width - w) / 2, y = area.Top + (area.Height - h) / 2;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float half = (w - 1) / 2f;
            using (var p = new Pen(c, Math.Max(1f, 1.2f * s)) { LineJoin = LineJoin.Miter })
            {
                var a = new PointF(x + 0.5f, up ? y + h - 1 + 0.5f : y + 0.5f);
                var m = new PointF(x + half + 0.5f, up ? y + 0.5f : y + h - 1 + 0.5f);
                var e = new PointF(x + w - 1 + 0.5f, a.Y);
                g.DrawLines(p, new[] { a, m, e });
            }
            g.SmoothingMode = old;
        }

        // --- champs (combo, compteur) ---------------------------------------------------------------------

        public static void FieldFrame(Graphics g, Rectangle r, Color fill, B1VisualState st, float s)
        {
            bool dis = (st & B1VisualState.Disabled) != 0;
            Color border = dis ? B1Palette.Rule : (st & B1VisualState.Focus) != 0 ? B1Palette.Accent : (st & B1VisualState.Hover) != 0 ? B1Palette.Dashed : B1Palette.Rule;
            Fill(g, fill, r);
            Frame(g, border, r, Px(1, s));
        }
    }
}
