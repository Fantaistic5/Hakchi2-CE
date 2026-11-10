using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Barre du haut : marque « HAKCHI » en capitales espacees, boutons texte (Jeux, Mods, Pochettes, Outils) qui ouvrent
    /// chacun un <see cref="ToolStripDropDown"/> sous le bouton, et a droite le <see cref="ConsoleBadge"/>. Filet de 1 px en bas.
    /// </summary>
    public class TopBar : Control
    {
        private sealed class MenuButton
        {
            public string Title;                 // sans le « & »
            public char Mnemonic;
            public ToolStripDropDown DropDown;
            public Rectangle Bounds;
            public ToolStripDropDownClosedEventHandler Closed;
        }

        private readonly List<MenuButton> menus = new List<MenuButton>();
        private readonly ConsoleBadge badge = new ConsoleBadge();
        private readonly ShellFonts fonts = new ShellFonts();
        private MenuButton hover, open;
        private MenuButton lastClosed;
        private int lastClosedTick;
        private string brand = "hakchi";
        private Rectangle brandBounds;

        public TopBar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = false;
            BackColor = B1Palette.Slate;
            ForeColor = B1Palette.TextDim;
            Controls.Add(badge);
        }

        protected override Size DefaultSize { get { return new Size(800, 36); } }

        private float S { get { return B1Dpi.Scale(this); } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ConsoleBadge Badge { get { return badge; } }

        /// <summary>Texte de marque (pas traduit) ; affiche en capitales.</summary>
        [DefaultValue("hakchi")]
        public string Brand { get { return brand; } set { brand = value ?? string.Empty; PerformLayout(); Invalidate(); } }

        public int MenuCount { get { return menus.Count; } }

        /// <summary>Ajoute un bouton texte. Le « &amp; » du titre donne le raccourci Alt+lettre.</summary>
        public void AddMenu(string title, ToolStripDropDown dropDown)
        {
            if (dropDown == null) throw new ArgumentNullException("dropDown");
            title = title ?? string.Empty;
            var m = new MenuButton { DropDown = dropDown };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < title.Length; i++)
            {
                if (title[i] == '&' && i + 1 < title.Length)
                {
                    i++;
                    if (title[i] != '&' && m.Mnemonic == '\0') m.Mnemonic = char.ToLowerInvariant(title[i]);
                }
                sb.Append(title[i]);
            }
            m.Title = sb.ToString();
            m.Closed = (s, e) =>
            {
                lastClosed = m;
                lastClosedTick = Environment.TickCount;
                if (open == m) { open = null; Invalidate(); }
            };
            dropDown.Closed += m.Closed;
            menus.Add(m);
            PerformLayout();
            Invalidate();
        }

        /// <summary>Rectangle d'un bouton (coordonnees du controle).</summary>
        public Rectangle GetMenuBounds(int index) { return menus[index].Bounds; }

        // --- mise en page ------------------------------------------------------------------------------------------

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            float s = S;
            bool rtl = RightToLeft == RightToLeft.Yes;
            int padX = B1Draw.Px(16, s);
            int padBtn = B1Draw.Px(10, s);
            var menuFont = fonts.Get(ShellFonts.Kind.Body, 9f, s);
            var brandFont = fonts.Get(ShellFonts.Kind.Title, 9f, s);
            int h = Math.Max(1, ClientSize.Height - B1Draw.Px(1, s));      // sans le filet du bas

            // marque
            int bw;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) bw = B1Draw.TrackedWidth(g, brand.ToUpper(), brandFont, 0.14f);
            int x = rtl ? ClientSize.Width - padX - bw : padX;
            brandBounds = new Rectangle(x, 0, bw, h);

            // boutons : la marque est suivie d'un espace de 1,6 em, repris par le remplissage des boutons
            int cursor = rtl ? brandBounds.X - padBtn - B1Draw.Px(10, s) : brandBounds.Right + B1Draw.Px(10, s);
            foreach (var m in menus)
            {
                int tw = ShellText.Width(m.Title, menuFont);
                int w = tw + 2 * padBtn;
                m.Bounds = rtl ? new Rectangle(cursor - w + padBtn, 0, w, h) : new Rectangle(cursor, 0, w, h);
                cursor = rtl ? m.Bounds.X - padBtn : m.Bounds.Right;
            }

            // badge a l'autre extremite
            var ps = badge.GetPreferredSize(Size.Empty);
            int avail = rtl
                ? (menus.Count > 0 ? menus[menus.Count - 1].Bounds.X : brandBounds.X) - 2 * padX
                : ClientSize.Width - (menus.Count > 0 ? menus[menus.Count - 1].Bounds.Right : brandBounds.Right) - 2 * padX;
            int bwd = Math.Min(ps.Width, Math.Max(0, avail));
            badge.SetBounds(rtl ? padX : ClientSize.Width - padX - bwd, (h - ps.Height) / 2, bwd, ps.Height);
        }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); PerformLayout(); }
        protected override void OnRightToLeftChanged(EventArgs e) { base.OnRightToLeftChanged(e); PerformLayout(); Invalidate(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); PerformLayout(); }

        // --- interaction -------------------------------------------------------------------------------------------

        private MenuButton ButtonAt(Point p)
        {
            foreach (var m in menus) if (m.Bounds.Contains(p)) return m;
            return null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var b = ButtonAt(e.Location);
            if (b != hover) { hover = b; Cursor = b != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
            // un menu est ouvert : passer sur un autre bouton ouvre celui-la (comportement d'une barre de menus)
            if (b != null && open != null && b != open && open.DropDown.Visible) Show(b);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null) { hover = null; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var b = ButtonAt(e.Location);
            if (b == null) return;
            // un clic sur le bouton du menu ouvert le ferme d'abord (clic hors du menu) : ne pas le rouvrir aussitot
            if (b == lastClosed && unchecked(Environment.TickCount - lastClosedTick) < 250) { lastClosed = null; return; }
            if (open == b && b.DropDown.Visible) { b.DropDown.Close(); return; }
            Show(b);
        }

        private void Show(MenuButton b)
        {
            if (open != null && open != b && open.DropDown.Visible) open.DropDown.Close();
            open = b;
            Invalidate();
            bool rtl = RightToLeft == RightToLeft.Yes;
            // le menu s'aligne sur le bord gauche du bouton, juste sous la barre (hors filet)
            b.DropDown.Show(this, new Point(rtl ? b.Bounds.Right : b.Bounds.Left, ClientSize.Height - B1Draw.Px(1, S)));
        }

        protected override bool ProcessMnemonic(char charCode)
        {
            char c = char.ToLowerInvariant(charCode);
            foreach (var m in menus)
                if (m.Mnemonic != '\0' && m.Mnemonic == c) { Show(m); return true; }
            return base.ProcessMnemonic(charCode);
        }

        // --- rendu -------------------------------------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = S;
            g.Clear(B1Palette.Slate);
            var menuFont = fonts.Get(ShellFonts.Kind.Body, 9f, s);
            var brandFont = fonts.Get(ShellFonts.Kind.Title, 9f, s);
            bool rtl = RightToLeft == RightToLeft.Yes;
            int rule = B1Draw.Px(1, s);

            B1Draw.DrawTracked(g, brand.ToUpper(), brandFont, B1Palette.Text, brandBounds.X, (brandBounds.Height - brandFont.Height) / 2, 0.14f);

            foreach (var m in menus)
            {
                bool isOpen = m == open && m.DropDown.Visible;
                if (isOpen) B1Draw.Fill(g, B1Palette.RowActive, m.Bounds);
                var color = isOpen || m == hover ? B1Palette.Text : B1Palette.TextDim;
                TextRenderer.DrawText(g, m.Title, menuFont, m.Bounds, color,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    (rtl ? TextFormatFlags.RightToLeft : 0));
            }
            B1Draw.Fill(g, B1Palette.Rule, 0, ClientSize.Height - rule, ClientSize.Width, rule);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var m in menus) m.DropDown.Closed -= m.Closed;
                fonts.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
