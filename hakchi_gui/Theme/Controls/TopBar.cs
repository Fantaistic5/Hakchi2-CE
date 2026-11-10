using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Barre du haut : marque « HAKCHI » en capitales espacees, un VRAI <see cref="MenuStrip"/> (Jeux, Mods, Pochettes, Outils)
    /// et a droite le <see cref="ConsoleBadge"/>. Filet de 1 px en bas. Comme c'est un MenuStrip natif, MenuActivate,
    /// DropDownOpening, ShortcutKeys, Alt/F10, les fleches et les soulignements de mnemoniques fonctionnent comme avant :
    /// la fenetre fait <c>MainMenuStrip = topBar.Menu</c>.
    /// </summary>
    public class TopBar : ContainerControl
    {
        private readonly MenuStrip menu = new MenuStrip();
        private readonly ConsoleBadge badge = new ConsoleBadge();
        private readonly ShellFonts fonts = new ShellFonts();
        private string brand = "hakchi";
        private Rectangle brandBounds;

        public TopBar()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.StandardClick, true);
            AutoScaleMode = AutoScaleMode.None;   // ContainerControl : relaie les mnemoniques Alt+lettre au MenuStrip
            TabStop = false;
            BackColor = B1Palette.Slate;
            ForeColor = B1Palette.TextDim;

            menu.Dock = DockStyle.None;
            menu.AutoSize = false;
            menu.GripStyle = ToolStripGripStyle.Hidden;
            menu.CanOverflow = false;
            menu.Stretch = false;
            menu.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
            menu.Padding = Padding.Empty;
            menu.BackColor = B1Palette.Slate;
            menu.ForeColor = B1Palette.TextDim;
            menu.Renderer = new B1ToolStripRenderer();
            menu.Font = fonts.Get(ShellFonts.Kind.Body, 9f, 1f);
            menu.ShowItemToolTips = false;
            Controls.Add(menu);
            Controls.Add(badge);
        }

        protected override Size DefaultSize { get { return new Size(800, 36); } }

        private float S { get { return B1Dpi.Scale(this); } }

        /// <summary>Le MenuStrip hebergé : a donner a <c>Form.MainMenuStrip</c>.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MenuStrip Menu { get { return menu; } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ConsoleBadge Badge { get { return badge; } }

        /// <summary>Texte de marque (pas traduit) ; affiche en capitales.</summary>
        [DefaultValue("hakchi")]
        public string Brand { get { return brand; } set { brand = value ?? string.Empty; PerformLayout(); Invalidate(); } }

        public int MenuCount { get { return menu.Items.Count; } }

        /// <summary>Ajoute un menu de premier niveau (le « &amp; » donne Alt+lettre) et rend l'item reel : on y range ses DropDownItems.</summary>
        public ToolStripMenuItem AddMenu(string title)
        {
            return AddMenu(new ToolStripMenuItem(), title ?? string.Empty);
        }

        /// <summary>
        /// Range un item EXISTANT (avec ses sous-menus et ses gestionnaires) en premier niveau, avec le style de la barre.
        /// <paramref name="title"/> null garde son texte. L'item quitte son ancien proprietaire.
        /// </summary>
        public ToolStripMenuItem AddMenu(ToolStripMenuItem item, string title)
        {
            if (item == null) throw new ArgumentNullException("item");
            if (title != null) item.Text = title;
            item.ForeColor = B1Palette.TextDim;
            item.Padding = new Padding(B1Draw.Px(10, S), 0, B1Draw.Px(10, S), 0);
            item.AutoSize = true;
            menu.Items.Add(item);
            PerformLayout();
            return item;
        }

        /// <summary>Rectangle d'un menu de premier niveau (coordonnees de la barre).</summary>
        public Rectangle GetMenuBounds(int index)
        {
            var b = menu.Items[index].Bounds;
            return new Rectangle(b.X + menu.Left, b.Y + menu.Top, b.Width, b.Height);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            float s = S;
            bool rtl = RightToLeft == RightToLeft.Yes;
            int padX = B1Draw.Px(16, s);
            menu.Font = fonts.Get(ShellFonts.Kind.Body, 9f, s);
            var brandFont = fonts.Get(ShellFonts.Kind.Title, 9f, s);

            int bw;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) bw = B1Draw.TrackedWidth(g, brand.ToUpperInvariant(), brandFont, 0.14f);
            brandBounds = new Rectangle(rtl ? ClientSize.Width - padX - bw : padX, 0, bw, ClientSize.Height);

            // le filet du bas est celui du MenuStrip (rendu par B1ToolStripRenderer) : meme couleur que celui de la barre
            int gap = B1Draw.Px(10, s);
            int mw = menu.Items.Count == 0 ? 0 : menu.GetPreferredSize(Size.Empty).Width;
            menu.SetBounds(rtl ? brandBounds.X - gap - mw : brandBounds.Right + gap, 0, mw, ClientSize.Height);

            var ps = badge.GetPreferredSize(Size.Empty);
            int h = Math.Max(1, ClientSize.Height - B1Draw.Px(1, s));
            int avail = rtl ? menu.Left - 2 * padX : ClientSize.Width - menu.Right - 2 * padX;
            int bwd = Math.Min(ps.Width, Math.Max(0, avail));
            badge.SetBounds(rtl ? padX : ClientSize.Width - padX - bwd, (h - ps.Height) / 2, bwd, ps.Height);
        }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); PerformLayout(); }
        protected override void OnRightToLeftChanged(EventArgs e) { base.OnRightToLeftChanged(e); PerformLayout(); Invalidate(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); PerformLayout(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = S;
            g.Clear(B1Palette.Slate);
            var brandFont = fonts.Get(ShellFonts.Kind.Title, 9f, s);
            int rule = B1Draw.Px(1, s);
            B1Draw.DrawTracked(g, brand.ToUpperInvariant(), brandFont, B1Palette.Text, brandBounds.X, (brandBounds.Height - brandFont.Height) / 2, 0.14f);
            B1Draw.Fill(g, B1Palette.Rule, 0, ClientSize.Height - rule, ClientSize.Width, rule);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { menu.Dispose(); fonts.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
