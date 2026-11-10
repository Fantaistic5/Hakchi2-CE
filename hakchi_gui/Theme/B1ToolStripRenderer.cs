using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>Table de couleurs B1 : toutes les entrees (y compris les « degrades », identiques de bout en bout) valent une couleur de la palette.</summary>
    internal sealed class B1ColorTable : ProfessionalColorTable
    {
        public B1ColorTable() { UseSystemColors = false; }

        private static Color S => B1Palette.Slate;
        private static Color R => B1Palette.RowActive;
        private static Color P => B1Palette.Primary;

        public override Color ButtonSelectedBorder => B1Palette.Accent;
        public override Color ButtonSelectedHighlight => R;
        public override Color ButtonSelectedHighlightBorder => B1Palette.Accent;
        public override Color ButtonPressedBorder => B1Palette.Accent;
        public override Color ButtonPressedHighlight => P;
        public override Color ButtonPressedHighlightBorder => B1Palette.Accent;
        public override Color ButtonCheckedHighlight => P;
        public override Color ButtonCheckedHighlightBorder => B1Palette.Accent;
        public override Color ButtonSelectedGradientBegin => R;
        public override Color ButtonSelectedGradientMiddle => R;
        public override Color ButtonSelectedGradientEnd => R;
        public override Color ButtonPressedGradientBegin => P;
        public override Color ButtonPressedGradientMiddle => P;
        public override Color ButtonPressedGradientEnd => P;
        public override Color ButtonCheckedGradientBegin => P;
        public override Color ButtonCheckedGradientMiddle => P;
        public override Color ButtonCheckedGradientEnd => P;
        public override Color CheckBackground => P;
        public override Color CheckSelectedBackground => P;
        public override Color CheckPressedBackground => P;
        public override Color GripDark => B1Palette.Dashed;
        public override Color GripLight => S;
        public override Color ImageMarginGradientBegin => S;
        public override Color ImageMarginGradientMiddle => S;
        public override Color ImageMarginGradientEnd => S;
        public override Color ImageMarginRevealedGradientBegin => S;
        public override Color ImageMarginRevealedGradientMiddle => S;
        public override Color ImageMarginRevealedGradientEnd => S;
        public override Color MenuBorder => B1Palette.Dashed;
        public override Color MenuItemBorder => B1Palette.Accent;
        public override Color MenuItemSelected => R;
        public override Color MenuItemSelectedGradientBegin => R;
        public override Color MenuItemSelectedGradientEnd => R;
        public override Color MenuItemPressedGradientBegin => R;
        public override Color MenuItemPressedGradientMiddle => R;
        public override Color MenuItemPressedGradientEnd => R;
        public override Color MenuStripGradientBegin => S;
        public override Color MenuStripGradientEnd => S;
        public override Color OverflowButtonGradientBegin => S;
        public override Color OverflowButtonGradientMiddle => S;
        public override Color OverflowButtonGradientEnd => S;
        public override Color RaftingContainerGradientBegin => S;
        public override Color RaftingContainerGradientEnd => S;
        public override Color SeparatorDark => B1Palette.Rule;
        public override Color SeparatorLight => B1Palette.Rule;
        public override Color StatusStripGradientBegin => S;
        public override Color StatusStripGradientEnd => S;
        public override Color ToolStripBorder => B1Palette.Rule;
        public override Color ToolStripContentPanelGradientBegin => S;
        public override Color ToolStripContentPanelGradientEnd => S;
        public override Color ToolStripDropDownBackground => S;
        public override Color ToolStripGradientBegin => S;
        public override Color ToolStripGradientMiddle => S;
        public override Color ToolStripGradientEnd => S;
        public override Color ToolStripPanelGradientBegin => S;
        public override Color ToolStripPanelGradientEnd => S;
    }

    /// <summary>
    /// Rendu plat des menus et barres d'outils : aplats de la palette, filets de 1 px, coins droits,
    /// aucun degrade, halo ni ombre. Tout est dessine sans anticrenelage (SmoothingMode.None) pour que
    /// chaque pixel soit une couleur de la palette ; seul le texte (ClearType) produit des melanges.
    /// </summary>
    public class B1ToolStripRenderer : ToolStripProfessionalRenderer
    {
        public B1ToolStripRenderer() : base(new B1ColorTable())
        {
            RoundedEdges = false;
        }

        private static void Fill(Graphics g, Color c, Rectangle r)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using (var b = new SolidBrush(c)) g.FillRectangle(b, r);
            g.SmoothingMode = old;
        }

        private static void Outline(Graphics g, Color c, Rectangle r)
        {
            Fill(g, c, new Rectangle(r.Left, r.Top, r.Width, 1));
            Fill(g, c, new Rectangle(r.Left, r.Bottom - 1, r.Width, 1));
            Fill(g, c, new Rectangle(r.Left, r.Top, 1, r.Height));
            Fill(g, c, new Rectangle(r.Right - 1, r.Top, 1, r.Height));
        }

        // --- fonds ---------------------------------------------------------------------------------------

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Fill(e.Graphics, B1Palette.Slate, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var ts = e.ToolStrip;
            var r = new Rectangle(Point.Empty, ts.Size);
            if (ts is ToolStripDropDown) Outline(e.Graphics, B1Palette.Dashed, r);
            else if (ts is StatusStrip) Fill(e.Graphics, B1Palette.Rule, new Rectangle(0, 0, r.Width, 1));
            else Fill(e.Graphics, B1Palette.Rule, new Rectangle(0, r.Height - 1, r.Width, 1));
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            Fill(e.Graphics, B1Palette.Slate, e.AffectedBounds);
        }

        protected override void OnRenderToolStripPanelBackground(ToolStripPanelRenderEventArgs e)
        {
            Fill(e.Graphics, B1Palette.Slate, new Rectangle(Point.Empty, e.ToolStripPanel.Size));
        }

        protected override void OnRenderToolStripContentPanelBackground(ToolStripContentPanelRenderEventArgs e)
        {
            Fill(e.Graphics, B1Palette.Slate, new Rectangle(Point.Empty, e.ToolStripContentPanel.Size));
        }

        protected override void OnRenderGrip(ToolStripGripRenderEventArgs e) { }
        protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e) { }

        // --- elements ------------------------------------------------------------------------------------

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            if (!item.Selected && !item.Pressed) return;
            if (!item.Enabled) return;
            var r = new Rectangle(Point.Empty, item.Size);
            Fill(e.Graphics, B1Palette.RowActive, r);
            if (item.IsOnDropDown) Fill(e.Graphics, B1Palette.Accent, new Rectangle(0, 0, 2, r.Height));      // barre d'accent a gauche
            else Fill(e.Graphics, B1Palette.Accent, new Rectangle(0, r.Height - 2, r.Width, 2));               // soulignement accent
        }

        private void RenderButtonLike(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            var r = new Rectangle(Point.Empty, item.Size);
            var button = item as ToolStripButton;
            bool isChecked = button != null && button.Checked;
            if (item.Pressed || isChecked)
            {
                Fill(e.Graphics, B1Palette.Primary, r);
                Outline(e.Graphics, B1Palette.Accent, r);
            }
            else if (item.Selected && item.Enabled)
            {
                Fill(e.Graphics, B1Palette.RowActive, r);
                Outline(e.Graphics, B1Palette.Rule, r);
            }
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) { RenderButtonLike(e); }
        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) { RenderButtonLike(e); }
        protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e) { RenderButtonLike(e); }

        protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
        {
            RenderButtonLike(e);
            var sb = e.Item as ToolStripSplitButton;
            if (sb == null) return;
            var btn = sb.ButtonBounds;
            var dd = sb.DropDownButtonBounds;
            Fill(e.Graphics, B1Palette.Rule, new Rectangle(btn.Right, 2, 1, e.Item.Height - 4));
            OnRenderArrow(new ToolStripArrowRenderEventArgs(e.Graphics, sb, dd, B1Palette.Text, ArrowDirection.Down));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var item = e.Item;
            var r = new Rectangle(Point.Empty, item.Size);
            if (e.Vertical) Fill(e.Graphics, B1Palette.Rule, new Rectangle(r.Width / 2, 2, 1, Math.Max(0, r.Height - 4)));
            else Fill(e.Graphics, B1Palette.Rule, new Rectangle(0, r.Height / 2, r.Width, 1));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var item = e.Item;
            if (!item.Enabled) e.TextColor = B1Palette.TextFaint;
            else if (item.Selected || item.Pressed) e.TextColor = B1Palette.Text;
            else
            {
                var fc = item.ForeColor;
                e.TextColor = IsPalette(fc) ? fc : B1Palette.Text;
            }
            base.OnRenderItemText(e);
        }

        private static bool IsPalette(Color c)
        {
            foreach (var p in B1Palette.All) if (p.ToArgb() == c.ToArgb()) return true;
            return false;
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = e.ArrowRectangle;
            var color = e.Item != null && !e.Item.Enabled ? B1Palette.TextFaint : (IsPalette(e.ArrowColor) ? e.ArrowColor : B1Palette.Text);
            int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
            Point[] pts;
            switch (e.Direction)
            {
                case ArrowDirection.Left: pts = new[] { new Point(cx + 2, cy - 4), new Point(cx + 2, cy + 4), new Point(cx - 2, cy) }; break;
                case ArrowDirection.Up: pts = new[] { new Point(cx - 4, cy + 2), new Point(cx + 4, cy + 2), new Point(cx, cy - 2) }; break;
                case ArrowDirection.Down: pts = new[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 2) }; break;
                default: pts = new[] { new Point(cx - 2, cy - 4), new Point(cx - 2, cy + 4), new Point(cx + 2, cy) }; break;
            }
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using (var b = new SolidBrush(color)) g.FillPolygon(b, pts);
            g.SmoothingMode = old;
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics;
            var r = e.ImageRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;
            Fill(g, B1Palette.Primary, r);
            Outline(g, B1Palette.Accent, r);
            // coche dessinee a la main (la bitmap d'origine est noire, hors palette), sans anticrenelage
            int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.None;
            using (var pen = new Pen(e.Item.Enabled ? B1Palette.TextOnPrimary : B1Palette.TextFaint, 2f))
                g.DrawLines(pen, new[] { new Point(cx - 4, cy), new Point(cx - 1, cy + 3), new Point(cx + 4, cy - 3) });
            g.SmoothingMode = old;
        }
    }
}
