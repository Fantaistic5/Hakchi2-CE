using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace com.clusterrr.hakchi_gui.Theme
{
    /// <summary>
    /// Habille recursivement des controles WinForms standard aux couleurs B1 : couleurs, polices, FlatStyle,
    /// moteur de rendu des menus. Ne touche JAMAIS a Text, Enabled, Visible, aux evenements, a l'ordre de tabulation
    /// ni a Dock / Anchor. La taille et la position ne bougent pas, sauf ce que la police impose
    /// (controles AutoSize, hauteur des champs a une ligne : c'est la police qui la determine).
    ///
    /// Regles :
    ///  - idempotent : Apply peut etre rappele (B1Form le fait a Load) ;
    ///  - une couleur deja dans la palette est conservee (un ecran peut choisir RowActive pour une carte) ;
    ///    toute autre couleur est remplacee par celle du theme ;
    ///  - les polices heritees restent heritees : seule la racine et les controles qui ont leur propre police sont changes ;
    ///  - Skip(c) exclut c et tout son sous-arbre ;
    ///  - les formes en AutoScaleMode.Font gardent leur taille : la mise a l'echelle automatique est suspendue
    ///    pendant le changement de police puis la reference de mise a l'echelle est rebasee sur la nouvelle police.
    /// </summary>
    public static class B1Skin
    {
        private static readonly ConditionalWeakTable<Control, object> skipped = new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<Control, object> primary = new ConditionalWeakTable<Control, object>();
        private static readonly B1ToolStripRenderer renderer = new B1ToolStripRenderer();

        // Polices B1 deja creees (les Font ne sont pas liberees : des controles les referencent).
        private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        /// <summary>Exclut ce controle et ses descendants de tout Apply.</summary>
        public static void Skip(Control c)
        {
            object o;
            if (c != null && !skipped.TryGetValue(c, out o)) skipped.Add(c, new object());
        }

        /// <summary>Force le style « bouton primaire » (sinon : le AcceptButton de la fenetre est primaire, les autres secondaires).</summary>
        public static void MarkPrimary(Button b)
        {
            object o;
            if (b != null && !primary.TryGetValue(b, out o)) primary.Add(b, new object());
        }

        internal static void ForgetFonts()
        {
            lock (fonts) fonts.Clear();
        }

        public static void Apply(Control root)
        {
            if (root == null || IsSkipped(root)) return;

            // Mise a l'echelle automatique par la police : suspendue pendant la changement de police (voir resume).
            var scaling = new List<ContainerControl>();
            CollectFontScaling(root, scaling);
            foreach (var cc in scaling) cc.AutoScaleMode = AutoScaleMode.None;

            root.SuspendLayout();
            try
            {
                Visit(root, null, root as Form ?? root.FindForm());
            }
            finally
            {
                root.ResumeLayout(true);
                foreach (var cc in scaling)
                {
                    cc.AutoScaleMode = AutoScaleMode.Font;
                    cc.AutoScaleDimensions = cc.CurrentAutoScaleDimensions;       // nouvelle reference : facteur 1, rien ne bouge
                }
            }
        }

        private static bool IsSkipped(Control c)
        {
            object o;
            return skipped.TryGetValue(c, out o);
        }

        private static void CollectFontScaling(Control c, List<ContainerControl> into)
        {
            if (IsSkipped(c)) return;
            var cc = c as ContainerControl;
            if (cc != null && cc.AutoScaleMode == AutoScaleMode.Font) into.Add(cc);
            foreach (Control child in c.Controls) CollectFontScaling(child, into);
        }

        private static void Visit(Control c, Control fontParent, Form owner)
        {
            if (IsSkipped(c)) return;
            Style(c, fontParent, owner);
            foreach (Control child in c.Controls.Cast<Control>().ToList()) Visit(child, c, owner);
            if (c.ContextMenuStrip != null && !(c is ToolStrip)) Visit(c.ContextMenuStrip, null, owner);
        }

        // --- couleurs -------------------------------------------------------------------------------------

        private static bool IsPalette(Color c)
        {
            if (c.A != 255) return false;
            foreach (var p in B1Palette.All) if (p.ToArgb() == c.ToArgb()) return true;
            return false;
        }

        private static Color Back(Color current, Color wanted) { return IsPalette(current) ? current : wanted; }

        private static Color Fore(Color current, Color wanted)
        {
            if (IsPalette(current)) return current;
            if (current.ToArgb() == SystemColors.GrayText.ToArgb() || current.ToArgb() == SystemColors.ControlDark.ToArgb()) return B1Palette.TextDim;
            return wanted;
        }

        private static Color BackKeepTransparent(Color current, Color wanted)
        {
            return current == Color.Transparent ? current : Back(current, wanted);
        }

        // --- polices --------------------------------------------------------------------------------------

        private static bool SameFont(Font a, Font b)
        {
            return a.Name == b.Name && Math.Abs(a.SizeInPoints - b.SizeInPoints) < 0.01f && a.Style == b.Style;
        }

        private static Font Themed(Font src)
        {
            var wanted = B1Fonts.Restyle(src);
            lock (fonts)
            {
                var key = wanted.Name + "|" + wanted.SizeInPoints.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)wanted.Style;
                Font cached;
                if (fonts.TryGetValue(key, out cached)) { wanted.Dispose(); return cached; }
                fonts[key] = wanted;
                return wanted;
            }
        }

        private static void SetFont(Control c, Control fontParent)
        {
            var f = c.Font;
            // herite du parent (deja habille) : on laisse heriter, la police suit le parent
            if (fontParent != null && f.Equals(fontParent.Font)) return;
            var t = Themed(f);
            if (!SameFont(f, t)) c.Font = t;
        }

        // --- controles ------------------------------------------------------------------------------------

        private static void Style(Control c, Control fontParent, Form owner)
        {
            SetFont(c, fontParent);

            var strip = c as ToolStrip;
            if (strip != null) { StyleStrip(strip); return; }

            var button = c as Button;
            if (button != null) { StyleButton(button, owner); return; }

            var check = c as CheckBox;
            if (check != null)
            {
                if (check.Appearance == Appearance.Button) StyleButton(check, owner);
                else StyleToggle(check);
                return;
            }
            var radio = c as RadioButton;
            if (radio != null)
            {
                if (radio.Appearance == Appearance.Button) StyleButton(radio, owner);
                else StyleToggle(radio);
                return;
            }

            var link = c as LinkLabel;
            if (link != null)
            {
                StyleLabel(link);
                link.LinkColor = Back(link.LinkColor, B1Palette.Lavender);
                link.ActiveLinkColor = Back(link.ActiveLinkColor, B1Palette.Text);
                link.VisitedLinkColor = Back(link.VisitedLinkColor, B1Palette.Lavender);
                link.DisabledLinkColor = Back(link.DisabledLinkColor, B1Palette.TextDim);
                return;
            }
            var label = c as Label;
            if (label != null) { StyleLabel(label); return; }

            var text = c as TextBoxBase;                       // TextBox, MaskedTextBox, RichTextBox
            if (text != null)
            {
                text.BackColor = Back(text.BackColor, B1Palette.RowActive);
                text.ForeColor = Fore(text.ForeColor, B1Palette.Text);
                if (text.BorderStyle == BorderStyle.Fixed3D)
                    // le cadre RichEdit reste en relief clair meme en FixedSingle : sans cadre, le fond RowActive suffit
                    text.BorderStyle = text is RichTextBox ? BorderStyle.None : BorderStyle.FixedSingle;
                return;
            }

            var combo = c as ComboBox;
            if (combo != null)
            {
                combo.FlatStyle = FlatStyle.Standard;     // Flat dessine un bouton clair ; Standard + DarkMode_CFD donne un combo sombre natif
                combo.BackColor = Back(combo.BackColor, B1Palette.RowActive);
                combo.ForeColor = Fore(combo.ForeColor, B1Palette.Text);
                NativeTheme.WithHandle(combo, "b1combo", h => NativeTheme.SetTheme(h, "DarkMode_CFD", "Combobox"));
                return;
            }

            var updown = c as UpDownBase;                      // NumericUpDown, DomainUpDown
            if (updown != null)
            {
                updown.BackColor = Back(updown.BackColor, B1Palette.RowActive);
                updown.ForeColor = Fore(updown.ForeColor, B1Palette.Text);
                if (updown.BorderStyle == BorderStyle.Fixed3D) updown.BorderStyle = BorderStyle.FixedSingle;
                foreach (Control inner in updown.Controls)         // UpDownButtons : fleches natives sombres
                    if (!(inner is TextBox)) NativeTheme.WithHandle(inner, "b1spin", h => NativeTheme.SetTheme(h, "DarkMode_Explorer", null));
                return;
            }

            var listBox = c as ListBox;                        // ListBox, CheckedListBox
            if (listBox != null)
            {
                listBox.BackColor = Back(listBox.BackColor, B1Palette.RowActive);
                listBox.ForeColor = Fore(listBox.ForeColor, B1Palette.Text);
                if (listBox.BorderStyle == BorderStyle.Fixed3D) listBox.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var listView = c as ListView;
            if (listView != null)
            {
                listView.BackColor = Back(listView.BackColor, B1Palette.Slate);
                listView.ForeColor = Fore(listView.ForeColor, B1Palette.Text);
                if (listView.BorderStyle == BorderStyle.Fixed3D) listView.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var tree = c as TreeView;
            if (tree != null)
            {
                tree.BackColor = Back(tree.BackColor, B1Palette.Slate);
                tree.ForeColor = Fore(tree.ForeColor, B1Palette.Text);
                tree.LineColor = Back(tree.LineColor, B1Palette.Dashed);
                if (tree.BorderStyle == BorderStyle.Fixed3D) tree.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var tabs = c as TabControl;
            if (tabs != null) { StyleTabs(tabs); return; }

            var page = c as TabPage;
            if (page != null)
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = Back(page.BackColor, B1Palette.Slate);
                page.ForeColor = Fore(page.ForeColor, B1Palette.Text);
                return;
            }

            var progress = c as ProgressBar;
            if (progress != null)
            {
                progress.BackColor = Back(progress.BackColor, B1Palette.RowActive);
                progress.ForeColor = Back(progress.ForeColor, B1Palette.Accent);
                // sans theme visuel, la barre honore BackColor / ForeColor (sinon : vert systeme)
                NativeTheme.WithHandle(progress, "b1progress", h => NativeTheme.SetTheme(h, "", ""));
                return;
            }

            var picture = c as PictureBox;
            if (picture != null)
            {
                picture.BackColor = BackKeepTransparent(picture.BackColor, B1Palette.Slate);
                picture.ForeColor = Fore(picture.ForeColor, B1Palette.Text);
                if (picture.BorderStyle == BorderStyle.Fixed3D) picture.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var group = c as GroupBox;
            if (group != null)
            {
                group.FlatStyle = FlatStyle.Flat;
                FramePainter.AttachGroup(group);
                group.BackColor = BackKeepTransparent(group.BackColor, B1Palette.Slate);
                group.ForeColor = Fore(group.ForeColor, B1Palette.Text);
                return;
            }

            var panel = c as Panel;
            if (panel != null && panel.BorderStyle == BorderStyle.Fixed3D) panel.BorderStyle = BorderStyle.FixedSingle;

            // Form, UserControl, Panel, TableLayoutPanel, FlowLayoutPanel, SplitContainer, autres : fond et texte du theme
            c.BackColor = Back(c.BackColor, B1Palette.Slate);
            c.ForeColor = Fore(c.ForeColor, B1Palette.Text);
        }

        private static void StyleLabel(Label l)
        {
            l.BackColor = BackKeepTransparent(l.BackColor, B1Palette.Slate);
            l.ForeColor = Fore(l.ForeColor, B1Palette.Text);
            if (l.BorderStyle == BorderStyle.Fixed3D) l.BorderStyle = BorderStyle.FixedSingle;
        }

        private static bool IsPrimary(ButtonBase b, Form owner)
        {
            object o;
            if (primary.TryGetValue(b, out o)) return true;
            var button = b as Button;
            if (button == null) return false;
            var form = owner ?? button.FindForm();
            return form != null && ReferenceEquals(form.AcceptButton, button);
        }

        private static void StyleButton(ButtonBase b, Form owner)
        {
            bool prim = IsPrimary(b, owner);
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = prim ? B1Palette.Primary : Back(b.BackColor, B1Palette.RowActive);
            b.ForeColor = prim ? B1Palette.TextOnPrimary : Fore(b.ForeColor, B1Palette.Text);
            var fa = b.FlatAppearance;
            fa.BorderSize = 1;
            fa.BorderColor = prim ? B1Palette.Accent : B1Palette.Dashed;
            fa.MouseOverBackColor = prim ? B1Palette.Accent : B1Palette.Rule;
            fa.MouseDownBackColor = prim ? B1Palette.Slate : B1Palette.Slate;
            fa.CheckedBackColor = B1Palette.Primary;
            HookDisabledPaint(b);
        }

        // Un bouton Flat desactive dessine son texte en gris tres sombre (illisible sur fond sombre) : on repeint
        // le bouton entier en TextFaint apres le dessin par defaut.
        private static readonly ConditionalWeakTable<Control, object> disabledHooked = new ConditionalWeakTable<Control, object>();

        private static void HookDisabledPaint(ButtonBase b)
        {
            object o;
            if (disabledHooked.TryGetValue(b, out o)) return;
            disabledHooked.Add(b, new object());
            b.Paint += DisabledPaint;
        }

        private static void DisabledPaint(object sender, PaintEventArgs e)
        {
            var b = (ButtonBase)sender;
            if (b.Enabled || b.Image != null) return;
            var r = b.ClientRectangle;
            var g = e.Graphics;
            using (var back = new SolidBrush(B1Palette.Slate)) g.FillRectangle(back, r);
            using (var rule = new Pen(B1Palette.Rule)) g.DrawRectangle(rule, 0, 0, r.Width - 1, r.Height - 1);
            var flags = TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            switch (b.TextAlign)
            {
                case ContentAlignment.TopLeft: case ContentAlignment.MiddleLeft: case ContentAlignment.BottomLeft: flags |= TextFormatFlags.Left; break;
                case ContentAlignment.TopRight: case ContentAlignment.MiddleRight: case ContentAlignment.BottomRight: flags |= TextFormatFlags.Right; break;
                default: flags |= TextFormatFlags.HorizontalCenter; break;
            }
            switch (b.TextAlign)
            {
                case ContentAlignment.TopLeft: case ContentAlignment.TopCenter: case ContentAlignment.TopRight: flags |= TextFormatFlags.Top; break;
                case ContentAlignment.BottomLeft: case ContentAlignment.BottomCenter: case ContentAlignment.BottomRight: flags |= TextFormatFlags.Bottom; break;
                default: flags |= TextFormatFlags.VerticalCenter; break;
            }
            TextRenderer.DrawText(g, b.Text, b.Font, Rectangle.Inflate(r, -3, -1), B1Palette.TextFaint, flags);
        }

        private static void StyleToggle(ButtonBase b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = BackKeepTransparent(b.BackColor, B1Palette.Slate);
            b.ForeColor = Fore(b.ForeColor, B1Palette.Text);
            var fa = b.FlatAppearance;
            fa.BorderSize = 1;
            fa.BorderColor = B1Palette.Dashed;
            fa.MouseOverBackColor = B1Palette.Rule;
            fa.MouseDownBackColor = B1Palette.Slate;
            fa.CheckedBackColor = B1Palette.Primary;
            HookDisabledToggle(b);
        }

        // Meme probleme pour une case / un bouton radio desactive : texte et case gris tres sombre. On repeint.
        private static readonly ConditionalWeakTable<Control, object> disabledToggleHooked = new ConditionalWeakTable<Control, object>();

        private static void HookDisabledToggle(ButtonBase b)
        {
            object o;
            if (disabledToggleHooked.TryGetValue(b, out o)) return;
            disabledToggleHooked.Add(b, new object());
            b.Paint += DisabledTogglePaint;
        }

        private static void DisabledTogglePaint(object sender, PaintEventArgs e)
        {
            var b = (ButtonBase)sender;
            if (b.Enabled || b.Image != null || b.RightToLeft == RightToLeft.Yes) return;
            var cb = b as CheckBox;
            var rb = b as RadioButton;
            if ((cb != null && cb.CheckAlign != ContentAlignment.MiddleLeft) || (rb != null && rb.CheckAlign != ContentAlignment.MiddleLeft)) return;
            var g = e.Graphics;
            var r = b.ClientRectangle;
            var bg = b.BackColor == Color.Transparent && b.Parent != null ? b.Parent.BackColor : b.BackColor;
            if (!IsPalette(bg)) bg = B1Palette.Slate;
            using (var back = new SolidBrush(bg)) g.FillRectangle(back, r);
            int size = 13, y = r.Top + (r.Height - size) / 2;
            using (var pen = new Pen(B1Palette.Rule))
            {
                if (rb != null) g.DrawEllipse(pen, r.Left + 1, y, size - 1, size - 1);
                else g.DrawRectangle(pen, r.Left + 1, y, size - 1, size - 1);
            }
            bool on = cb != null ? cb.CheckState != CheckState.Unchecked : rb.Checked;
            if (on)
            {
                var mark = B1Palette.TextFaint;
                if (rb != null) using (var br = new SolidBrush(mark)) g.FillEllipse(br, r.Left + 5, y + 4, 5, 5);
                else using (var pen = new Pen(mark, 2f)) g.DrawLines(pen, new[] { new Point(r.Left + 4, y + 7), new Point(r.Left + 6, y + 9), new Point(r.Left + 10, y + 4) });
            }
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            if (!b.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, b.Text, b.Font, new Rectangle(r.Left + 16, r.Top, Math.Max(0, r.Width - 16), r.Height), B1Palette.TextFaint, flags);
        }

        // --- onglets --------------------------------------------------------------------------------------

        private static void StyleTabs(TabControl tabs)
        {
            // TabControl ignore BackColor ; la bande des onglets, les onglets et le cadre sont repeints par TabsPainter.
            tabs.ForeColor = Fore(tabs.ForeColor, B1Palette.Text);
            FramePainter.AttachTabs(tabs);
        }

        // --- menus et barres d'outils ---------------------------------------------------------------------

        private static void StyleStrip(ToolStrip strip)
        {
            strip.Renderer = renderer;
            strip.BackColor = Back(strip.BackColor, B1Palette.Slate);
            strip.ForeColor = Fore(strip.ForeColor, B1Palette.Text);
            foreach (ToolStripItem item in strip.Items.Cast<ToolStripItem>().ToList()) StyleItem(item, strip);
        }

        private static void StyleItem(ToolStripItem item, ToolStrip owner)
        {
            if (item is ToolStripSeparator) { item.BackColor = Back(item.BackColor, B1Palette.Slate); item.ForeColor = Back(item.ForeColor, B1Palette.Rule); return; }
            item.BackColor = Back(item.BackColor, B1Palette.Slate);
            item.ForeColor = Fore(item.ForeColor, B1Palette.Text);
            var f = item.Font;
            if (!f.Equals(owner.Font))
            {
                var t = Themed(f);
                if (!SameFont(f, t)) item.Font = t;
            }
            var dd = item as ToolStripDropDownItem;
            if (dd != null && dd.HasDropDownItems)
            {
                var menu = dd.DropDown;
                StyleStrip(menu);
                var tf = menu.Font;
                if (!SameFont(tf, Themed(tf))) menu.Font = Themed(tf);
            }
        }
    }
}
