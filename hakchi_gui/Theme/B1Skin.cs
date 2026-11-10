using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme.Controls;

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
        [ThreadStatic] private static bool managerRendererSet;      // ToolStripManager.Renderer est propre au thread UI
        private static readonly ConditionalWeakTable<ToolStripItem, object> openingHooked = new ConditionalWeakTable<ToolStripItem, object>();
        private static readonly B1ToolStripRenderer renderer = new B1ToolStripRenderer();

        // Polices B1 deja creees (les Font ne sont pas liberees : des controles les referencent).
        private static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        /// <summary>Exclut ce controle et ses descendants de tout Apply.</summary>
        public static void Skip(Control c)
        {
            object o;
            if (c != null && !skipped.TryGetValue(c, out o)) skipped.Add(c, new object());
        }

        private sealed class KindBox { public B1ButtonKind Kind; }
        private static readonly ConditionalWeakTable<ButtonBase, KindBox> kinds = new ConditionalWeakTable<ButtonBase, KindBox>();

        /// <summary>Force le style « bouton primaire » (sinon : le AcceptButton de la fenetre est primaire, les autres secondaires).</summary>
        public static void MarkPrimary(Button b) { SetKind(b, B1ButtonKind.Primary); }

        /// <summary>Choisit le style d'un bouton standard : Primary, Secondary, Quiet ou Danger (flash, effacement). Sans appel : Primary pour le AcceptButton, sinon Secondary.</summary>
        public static void SetKind(ButtonBase b, B1ButtonKind kind)
        {
            if (b == null) return;
            KindBox box;
            if (kinds.TryGetValue(b, out box)) box.Kind = kind; else kinds.Add(b, new KindBox { Kind = kind });
            b.Invalidate();
        }

        internal static B1ButtonKind KindOf(ButtonBase b)
        {
            KindBox box;
            if (kinds.TryGetValue(b, out box)) return box.Kind;
            var button = b as Button;
            var form = b.FindForm();
            return button != null && form != null && ReferenceEquals(form.AcceptButton, button) ? B1ButtonKind.Primary : B1ButtonKind.Secondary;
        }

        internal static void ForgetFonts()
        {
            lock (fonts) fonts.Clear();
        }

        public static void Apply(Control root)
        {
            if (root == null || IsSkipped(root)) return;

            // Renderer par defaut du thread UI (une fois par thread) : menus et barres crees plus tard, hors arbre, sont plats aussi.
            if (!managerRendererSet) { managerRendererSet = true; ToolStripManager.Renderer = renderer; }

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

            // controles B1 dessines sur mesure : ils se peignent eux-memes, seules les couleurs de base sont harmonisees
            if (c is B1Button || c is B1CheckBox || c is B1Chip || c is B1ComboBox || c is B1GroupBox || c is B1TabControl || c is B1ProgressBar)
            {
                c.BackColor = BackKeepTransparent(c.BackColor, B1Palette.Slate);
                c.ForeColor = Fore(c.ForeColor, B1Palette.Text);
                return;
            }

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
                if (text.Multiline || text is RichTextBox) DarkScrollBars(text);
                if (text.BorderStyle == BorderStyle.FixedSingle) NcBorderWindow.Hook(text);
                return;
            }

            var combo = c as ComboBox;
            if (combo != null)
            {
                combo.FlatStyle = FlatStyle.Standard;
                combo.BackColor = Back(combo.BackColor, B1Palette.RowActive);
                combo.ForeColor = Fore(combo.ForeColor, B1Palette.Text);
                ComboPaint.HookStock(combo);              // champ, fleche et lignes de la liste dessines par le theme
                return;
            }

            var updown = c as UpDownBase;                      // NumericUpDown, DomainUpDown
            if (updown != null)
            {
                updown.BackColor = Back(updown.BackColor, B1Palette.RowActive);
                updown.ForeColor = Fore(updown.ForeColor, B1Palette.Text);
                if (updown.BorderStyle == BorderStyle.Fixed3D) updown.BorderStyle = BorderStyle.FixedSingle;
                SpinWindow.Hook(updown);                           // fleches et bordure dessinees par le theme
                return;
            }

            var listBox = c as ListBox;                        // ListBox, CheckedListBox
            if (listBox != null)
            {
                listBox.BackColor = Back(listBox.BackColor, B1Palette.RowActive);
                listBox.ForeColor = Fore(listBox.ForeColor, B1Palette.Text);
                if (listBox.BorderStyle == BorderStyle.Fixed3D) listBox.BorderStyle = BorderStyle.FixedSingle;
                ListPaint.HookListBox(listBox);
                DarkScrollBars(listBox);
                NcBorderWindow.Hook(listBox);
                return;
            }

            var listView = c as ListView;
            if (listView != null)
            {
                listView.BackColor = Back(listView.BackColor, B1Palette.Slate);
                listView.ForeColor = Fore(listView.ForeColor, B1Palette.Text);
                if (listView.BorderStyle == BorderStyle.Fixed3D) listView.BorderStyle = BorderStyle.FixedSingle;
                DarkScrollBars(listView);
                NcBorderWindow.Hook(listView);
                ListViewWindow.Hook(listView);             // lignes : selection RowActive + barre d'accent
                HeaderWindow.Hook(listView);               // en-tete de colonnes
                return;
            }

            var tree = c as TreeView;
            if (tree != null)
            {
                tree.BackColor = Back(tree.BackColor, B1Palette.Slate);
                tree.ForeColor = Fore(tree.ForeColor, B1Palette.Text);
                tree.LineColor = Back(tree.LineColor, B1Palette.Dashed);
                if (tree.BorderStyle == BorderStyle.Fixed3D) tree.BorderStyle = BorderStyle.FixedSingle;
                DarkScrollBars(tree);
                NcBorderWindow.Hook(tree);
                ListPaint.HookTree(tree);
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
                ProgressWindow.Hook(progress);                 // piste Rule / remplissage Accent de 6 px
                return;
            }

            var picture = c as PictureBox;
            if (picture != null)
            {
                picture.BackColor = BackKeepTransparent(picture.BackColor, B1Palette.Slate);
                picture.ForeColor = Fore(picture.ForeColor, B1Palette.Text);
                if (picture.BorderStyle == BorderStyle.Fixed3D) picture.BorderStyle = BorderStyle.FixedSingle;
                if (picture.BorderStyle == BorderStyle.FixedSingle) NcBorderWindow.Hook(picture);
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
            if (panel != null && panel.BorderStyle == BorderStyle.FixedSingle) NcBorderWindow.Hook(panel);
            if (panel != null && panel.AutoScroll) DarkScrollBars(panel);

            // Form, UserControl, Panel, TableLayoutPanel, FlowLayoutPanel, SplitContainer, autres : fond et texte du theme
            c.BackColor = Back(c.BackColor, B1Palette.Slate);
            c.ForeColor = Fore(c.ForeColor, B1Palette.Text);
        }

        /// <summary>Barres de defilement sombres (theme Windows « DarkMode_Explorer », Windows 10 1809 et plus ; sans effet avant).</summary>
        private static void DarkScrollBars(Control c)
        {
            NativeTheme.WithHandle(c, "b1scroll", h => NativeTheme.SetTheme(h, "DarkMode_Explorer", null));
        }

        private static void StyleLabel(Label l)
        {
            l.BackColor = BackKeepTransparent(l.BackColor, B1Palette.Slate);
            l.ForeColor = Fore(l.ForeColor, B1Palette.Text);
            if (l.BorderStyle == BorderStyle.Fixed3D) l.BorderStyle = BorderStyle.FixedSingle;
            if (l.BorderStyle == BorderStyle.FixedSingle) NcBorderWindow.Hook(l);
        }

        private static void StyleButton(ButtonBase b, Form owner)
        {
            bool prim = KindOf(b) == B1ButtonKind.Primary || (b is Button && owner != null && ReferenceEquals(owner.AcceptButton, b));
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = prim ? B1Palette.Primary : Back(b.BackColor, B1Palette.RowActive);
            b.ForeColor = prim ? B1Palette.TextOnPrimary : Fore(b.ForeColor, B1Palette.Text);
            var fa = b.FlatAppearance;
            fa.BorderSize = 1;
            fa.BorderColor = prim ? B1Palette.Accent : B1Palette.Dashed;
            fa.MouseOverBackColor = prim ? B1Palette.Accent : B1Palette.Rule;
            fa.MouseDownBackColor = B1Palette.Slate;
            fa.CheckedBackColor = B1Palette.Primary;
            HookPaint(b);
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
            HookPaint(b);
        }

        // Boutons, cases et radios standard : le dessin de Windows (focus pointille, glyphe gris, texte desactive illisible)
        // est entierement recouvert, dans la meme peinture, par celui des controles B1 (B1Draw).
        private static readonly ConditionalWeakTable<Control, object> paintHooked = new ConditionalWeakTable<Control, object>();

        private static void HookPaint(ButtonBase b)
        {
            object o;
            if (paintHooked.TryGetValue(b, out o)) return;
            paintHooked.Add(b, new object());
            b.Paint += StockPaint;
            b.MouseEnter += InvalidateOnEvent; b.MouseLeave += InvalidateOnEvent;
            b.GotFocus += InvalidateOnEvent; b.LostFocus += InvalidateOnEvent;
            b.MouseDown += InvalidateOnMouse; b.MouseUp += InvalidateOnMouse;
        }

        private static void InvalidateOnEvent(object sender, EventArgs e) { ((Control)sender).Invalidate(); }
        private static void InvalidateOnMouse(object sender, MouseEventArgs e) { ((Control)sender).Invalidate(); }

        private static void StockPaint(object sender, PaintEventArgs e)
        {
            var b = (ButtonBase)sender;
            var st = B1Draw.StockState(b);
            B1VisualState forced;
            bool preview = B1Preview.TryGet(b, out forced);
            var cb = b as CheckBox;
            var rb = b as RadioButton;
            if (!preview)
            {
                if (cb != null) { if (cb.CheckState == CheckState.Checked) st |= B1VisualState.Checked; else if (cb.CheckState == CheckState.Indeterminate) st |= B1VisualState.Indeterminate; }
                else if (rb != null && rb.Checked) st |= B1VisualState.Checked;
            }
            if (cb != null && cb.Appearance == Appearance.Normal) B1Draw.PaintToggle(e.Graphics, b, st, false, cb.CheckAlign);
            else if (rb != null && rb.Appearance == Appearance.Normal) B1Draw.PaintToggle(e.Graphics, b, st, true, rb.CheckAlign);
            else if (cb != null || rb != null) B1Draw.PaintChip(e.Graphics, b, st);
            else B1Draw.PaintButton(e.Graphics, b, KindOf(b), st);
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
            if (dd != null)
            {
                // Les elements ajoutes apres Apply (menus remplis a l'execution) sont habilles a l'ouverture du menu.
                object o;
                if (!openingHooked.TryGetValue(dd, out o)) { openingHooked.Add(dd, new object()); dd.DropDownOpening += OnDropDownOpening; }
                if (dd.HasDropDownItems) StyleDropDown(dd);
            }
        }

        private static void OnDropDownOpening(object sender, EventArgs e)
        {
            StyleDropDown((ToolStripDropDownItem)sender);
        }

        private static void StyleDropDown(ToolStripDropDownItem dd)
        {
            var menu = dd.DropDown;
            StyleStrip(menu);
            var tf = menu.Font;
            var themed = Themed(tf);
            if (!SameFont(tf, themed)) menu.Font = themed;
        }
    }
}
