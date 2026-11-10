using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Covers;
using com.clusterrr.hakchi_gui.Properties;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Grille de jaquettes : une VUE sur la ListView des jeux (listViewGames), qui reste la source de verite, invisible.
    /// La grille lit <c>source.Items</c> (Tag = NesApplication, Checked = sur la console, Group) et ne fait jamais rien
    /// elle-meme : clics, Espace, Ctrl/Maj-clic changent <c>ListViewItem.Selected</c> / <c>.Checked</c> de la source, donc
    /// ce sont les evenements existants de la ListView (ItemSelectionChanged, ItemCheck, ItemChecked) qui font le travail
    /// metier. Les autres raccourcis (Suppr, F2, Entree, Echap...) sont relayes au KeyDown de la source ; son menu
    /// contextuel est celui de la grille. Le filtre ne change que l'affichage : Synchroniser et Exporter lisent toujours
    /// tous les items coches de la source.
    /// <para>
    /// Une ListView invisible n'emet ses evenements que si son handle existe ; un controle invisible n'en recoit pas par
    /// CreateControl. <see cref="Bind"/> force donc le handle de la source (des que la grille a le sien).
    /// </para>
    /// </summary>
    public class CoverGridView : Control
    {
        private ListView source;
        private GameFilter filter;
        private List<ListViewItem> items = new List<ListViewItem>();
        private CoverGridLayout layout;
        private int scrollY;
        private ThumbnailCache cache;
        private bool ownsCache = true;
        private ListViewItem anchor, focusItem;
        private int hoverIndex = -1;
        private bool rebuildPosted, rebuildPending;
        private readonly SourceWatcher watcher;
        private Font labelFont, missFont;
        private float fontScale;
        private readonly Action<NesApplication> onThumbReady;

        public CoverGridView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, false);
            TabStop = true;
            BackColor = B1Palette.Slate;
            ForeColor = B1Palette.TextSecondary;
            cache = new ThumbnailCache();
            watcher = new SourceWatcher(this);
            onThumbReady = app =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke(new Action(Invalidate)); else Invalidate();
            };
            Filter = new GameFilter();
        }

        // --- liaison a la source -------------------------------------------------------------------------------

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ListView Source { get { return source; } }

        /// <summary>Branche la grille sur la ListView des jeux (qui peut etre invisible).</summary>
        public void Bind(ListView listView)
        {
            if (source != null)
            {
                source.ItemSelectionChanged -= Source_ItemSelectionChanged;
                source.ItemChecked -= Source_ItemChecked;
                source.HandleCreated -= Source_HandleCreated;
                source.HandleDestroyed -= Source_HandleDestroyed;
                source.Disposed -= Source_Disposed;
                watcher.ReleaseHandle();
            }
            source = listView;
            anchor = focusItem = null;
            if (source != null)
            {
                source.ItemSelectionChanged += Source_ItemSelectionChanged;
                source.ItemChecked += Source_ItemChecked;
                source.HandleCreated += Source_HandleCreated;
                source.HandleDestroyed += Source_HandleDestroyed;
                source.Disposed += Source_Disposed;
                if (IsHandleCreated) EnsureSourceHandle();
                if (source.IsHandleCreated) watcher.Watch(source.Handle);
            }
            Rebuild();
        }

        private void EnsureSourceHandle()
        {
            if (source != null && !source.IsDisposed && !source.IsHandleCreated)
            {
                var h = source.Handle; // invisible : seul l'acces a Handle cree la fenetre (et donc ses evenements)
            }
        }

        private void Source_HandleCreated(object sender, EventArgs e) { watcher.Watch(source.Handle); RequestRebuild(); }
        private void Source_HandleDestroyed(object sender, EventArgs e) { watcher.ReleaseHandle(); }
        private void Source_Disposed(object sender, EventArgs e) { Bind(null); }

        private void Source_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e) { Invalidate(); }

        private void Source_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (filter != null && filter.Scope == GameScope.OnConsole) RequestRebuild(); // « sur la console » depend des coches
            Invalidate();
        }

        /// <summary>Le contenu de la source a change (items ajoutes, retires, renommes, regroupes) : on relit apres coup.</summary>
        internal void RequestRebuild()
        {
            if (rebuildPosted || IsDisposed) return;
            // jamais de relecture pendant que la source est en train d'inserer ou de retirer : toujours apres coup
            if (!IsHandleCreated) { rebuildPending = true; return; }
            rebuildPosted = true;
            BeginInvoke(new Action(() => { rebuildPosted = false; if (!IsDisposed) Rebuild(); }));
        }

        /// <summary>Observe les messages qui modifient les items de la source (il n'existe pas d'evenement ListView pour cela).</summary>
        private sealed class SourceWatcher : NativeWindow
        {
            private const int WM_SETREDRAW = 0x000B, LVM_DELETEITEM = 0x1008, LVM_DELETEALLITEMS = 0x1009,
                LVM_SETITEMW = 0x104C, LVM_INSERTITEMW = 0x104D, LVM_SETITEMTEXTW = 0x1074, LVM_SETITEMA = 0x1006,
                LVM_INSERTITEMA = 0x1007, LVM_SETITEMTEXTA = 0x102E;
            private readonly CoverGridView owner;
            public SourceWatcher(CoverGridView owner) { this.owner = owner; }

            public void Watch(IntPtr h)
            {
                if (Handle == h) return;
                if (Handle != IntPtr.Zero) ReleaseHandle();
                AssignHandle(h);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                switch (m.Msg)
                {
                    case LVM_DELETEITEM: case LVM_DELETEALLITEMS: case LVM_SETITEMW: case LVM_INSERTITEMW: case LVM_SETITEMTEXTW:
                    case LVM_SETITEMA: case LVM_INSERTITEMA: case LVM_SETITEMTEXTA:
                        owner.RequestRebuild();
                        break;
                    case WM_SETREDRAW:
                        if (m.WParam != IntPtr.Zero) owner.RequestRebuild(); // EndUpdate
                        break;
                }
            }
        }

        // --- filtre et contenu ---------------------------------------------------------------------------------

        /// <summary>Filtre d'affichage ; ses changements s'appliquent tout de suite. Il ne retire rien de la source.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GameFilter Filter
        {
            get { return filter; }
            set
            {
                if (filter != null) filter.Changed -= Filter_Changed;
                filter = value ?? new GameFilter();
                filter.Changed += Filter_Changed;
                Rebuild();
            }
        }

        private void Filter_Changed(object sender, EventArgs e) { Rebuild(); }

        /// <summary>Cache de vignettes (partageable entre vues ; celui cree par defaut est libere avec la grille).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ThumbnailCache Cache
        {
            get { return cache; }
            set
            {
                if (value == null || value == cache) return;
                if (ownsCache) cache.Dispose();
                cache = value; ownsCache = false;
                Invalidate();
            }
        }

        public int VisibleCount { get { return items.Count; } }
        public event EventHandler VisibleCountChanged;

        /// <summary>Items de la source affiches, dans l'ordre de la grille.</summary>
        [Browsable(false)]
        public IReadOnlyList<ListViewItem> VisibleItems { get { return items; } }

        [Browsable(false)]
        public CoverGridLayout GridLayout { get { return layout; } }

        /// <summary>Relit la source a travers le filtre. Les items masques sont deselectionnes (une selection invisible
        /// serait supprimee par Suppr sans que l'utilisateur la voie) ; leurs coches ne changent jamais.</summary>
        public void Rebuild()
        {
            int before = items.Count;
            items = filter.Apply(source);
            if (source != null && source.SelectedIndices.Count > 0)
            {
                var visible = new HashSet<ListViewItem>(items);
                var hidden = new List<ListViewItem>();
                foreach (ListViewItem it in source.SelectedItems) if (!visible.Contains(it)) hidden.Add(it);
                foreach (var it in hidden) it.Selected = false;
            }
            if (anchor != null && anchor.ListView != source) anchor = null;
            if (focusItem != null && focusItem.ListView != source) focusItem = null;
            hoverIndex = -1;
            Relayout();
            Invalidate();
            if (items.Count != before)
            {
                var h = VisibleCountChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        // --- mise en page et defilement ------------------------------------------------------------------------

        private float DpiScale { get { return B1Dpi.Scale(this); } }

        private void EnsureFonts()
        {
            float s = DpiScale;
            if (labelFont != null && fontScale == s) return;
            labelFont = B1Fonts.Body(8f * s);
            missFont = B1Fonts.Small(7f * s);
            fontScale = s;
        }

        private int LayoutWidth
        {
            // la largeur de la barre de defilement est toujours reservee : apparaitre ou disparaitre ne change pas les colonnes
            get { return Math.Max(0, Width - SystemInformation.VerticalScrollBarWidth); }
        }

        private void Relayout()
        {
            EnsureFonts();
            layout = CoverGridLayout.Compute(LayoutWidth, items.Count, DpiScale, labelFont.Height);
            SetScroll(scrollY);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Relayout();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            Relayout();
            Invalidate();
        }

        private int MaxScroll { get { return Math.Max(0, layout.ContentHeight - ClientSize.Height); } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ScrollY { get { return scrollY; } set { SetScroll(value); } }

        private void SetScroll(int y)
        {
            int ny = Math.Max(0, Math.Min(MaxScroll, y));
            bool moved = ny != scrollY;
            scrollY = ny;
            UpdateScrollBar();
            if (moved) Invalidate();
        }

        private void UpdateScrollBar()
        {
            if (!IsHandleCreated) return;
            var si = new SCROLLINFO
            {
                cbSize = Marshal.SizeOf(typeof(SCROLLINFO)),
                fMask = SIF_RANGE | SIF_PAGE | SIF_POS,
                nMin = 0,
                nMax = Math.Max(0, layout.ContentHeight - 1),
                nPage = (uint)Math.Max(0, ClientSize.Height),
                nPos = scrollY,
            };
            SetScrollInfo(Handle, SB_VERT, ref si, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= WS_VSCROLL;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { NativeTheme.SetTheme(Handle, "DarkMode_Explorer", null); } catch (Exception) { } // barre de defilement sombre
            EnsureSourceHandle();
            if (rebuildPending) { rebuildPending = false; Rebuild(); } else Relayout();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_VSCROLL)
            {
                int code = (int)((long)m.WParam & 0xFFFF);
                int line = Math.Max(1, layout.RowPitch / 4), page = Math.Max(1, ClientSize.Height - layout.RowPitch / 2);
                switch (code)
                {
                    case SB_LINEUP: SetScroll(scrollY - line); break;
                    case SB_LINEDOWN: SetScroll(scrollY + line); break;
                    case SB_PAGEUP: SetScroll(scrollY - page); break;
                    case SB_PAGEDOWN: SetScroll(scrollY + page); break;
                    case SB_TOP: SetScroll(0); break;
                    case SB_BOTTOM: SetScroll(MaxScroll); break;
                    case SB_THUMBTRACK:
                    case SB_THUMBPOSITION:
                        var si = new SCROLLINFO { cbSize = Marshal.SizeOf(typeof(SCROLLINFO)), fMask = SIF_TRACKPOS };
                        if (GetScrollInfo(Handle, SB_VERT, ref si)) SetScroll(si.nTrackPos);
                        break;
                }
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            SetScroll(scrollY - (int)Math.Round(e.Delta / 120.0 * layout.RowPitch / 2));
        }

        /// <summary>Fait defiler pour que le jeu d'indice visible <paramref name="index"/> soit entierement a l'ecran.</summary>
        public void EnsureVisible(int index)
        {
            if (index < 0 || index >= items.Count) return;
            var r = layout.CellBounds(index);
            int pad = layout.Padding;
            if (r.Top - pad < scrollY) SetScroll(r.Top - pad);
            else if (r.Bottom + pad > scrollY + ClientSize.Height) SetScroll(r.Bottom + pad - ClientSize.Height);
        }

        // --- geometrie publique (coordonnees client) -----------------------------------------------------------

        public Rectangle GetItemRect(int visibleIndex) { var r = layout.CellBounds(visibleIndex); r.Offset(0, -scrollY); return r; }
        public Rectangle GetCoverRect(int visibleIndex) { var r = layout.CoverBounds(visibleIndex); r.Offset(0, -scrollY); return r; }

        /// <summary>Petit interrupteur « sur la console » dans l'angle bas-droit de la jaquette.</summary>
        public Rectangle GetSwitchRect(int visibleIndex)
        {
            var c = GetCoverRect(visibleIndex);
            float s = DpiScale;
            int w = B1Draw.Px(24, s), h = B1Draw.Px(9, s), m = B1Draw.Px(6, s);
            return new Rectangle(c.Right - m - w, c.Bottom - m - h, w, h);
        }

        public int HitTest(Point client) { return layout.HitTest(new Point(client.X, client.Y + scrollY)); }

        private bool SwitchHit(int index, Point p)
        {
            var r = GetSwitchRect(index);
            int e = B1Draw.Px(4, DpiScale);
            r.Inflate(e, e);
            return r.Contains(p);
        }

        private Size CoverSize { get { return new Size(layout.CellWidth, layout.CoverHeight); } }

        /// <summary>Vrai quand chaque jeu a l'ecran a sa vignette (ou est connu sans jaquette) : premier rendu complet.</summary>
        [Browsable(false)]
        public bool AllVisibleThumbnailsReady
        {
            get
            {
                int first, last;
                layout.VisibleRange(scrollY, ClientSize.Height, out first, out last);
                var size = CoverSize;
                for (int i = first; i <= last; i++)
                {
                    var app = items[i].Tag as NesApplication;
                    if (app == null || CoverTargets.NeedsCover(app)) continue;
                    if (!cache.Contains(app, size)) return false;
                }
                return true;
            }
        }

        // --- selection et coches (toujours via les items de la source) -----------------------------------------

        /// <summary>Touches de modification au moment du clic (substituable pour les tests).</summary>
        protected virtual Keys CurrentModifiers { get { return ModifierKeys; } }

        private void SelectOnly(ListViewItem keep)
        {
            if (source == null) return;
            var drop = new List<ListViewItem>();
            foreach (ListViewItem it in source.SelectedItems) if (it != keep) drop.Add(it);
            foreach (var it in drop) it.Selected = false;
            if (keep != null && !keep.Selected) keep.Selected = true;
        }

        private void SelectRange(int from, int to, bool additive)
        {
            if (source == null) return;
            int a = Math.Min(from, to), b = Math.Max(from, to);
            var range = new HashSet<ListViewItem>();
            for (int i = a; i <= b; i++) range.Add(items[i]);
            if (!additive)
            {
                var drop = new List<ListViewItem>();
                foreach (ListViewItem it in source.SelectedItems) if (!range.Contains(it)) drop.Add(it);
                foreach (var it in drop) it.Selected = false;
            }
            for (int i = a; i <= b; i++) if (!items[i].Selected) items[i].Selected = true;
        }

        private int IndexOf(ListViewItem it) { return it == null ? -1 : items.IndexOf(it); }

        /// <summary>Espace : meme regle que listViewGames_KeyDown — tout coche -> tout decoche, sinon tout coche.</summary>
        private void ToggleChecked(IList<ListViewItem> targets)
        {
            if (targets.Count == 0) return;
            bool all = true;
            foreach (var it in targets) if (!it.Checked) { all = false; break; }
            foreach (var it in targets) if (it.Checked == all) it.Checked = !all;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Focused) Focus();
            if (source == null) return;
            int i = HitTest(e.Location);
            var mods = CurrentModifiers;
            bool ctrl = (mods & Keys.Control) != 0, shift = (mods & Keys.Shift) != 0;
            var item = i >= 0 ? items[i] : null;
            if (e.Button == MouseButtons.Left)
            {
                if (item != null && !ctrl && !shift && SwitchHit(i, e.Location))
                {
                    // interrupteur : coche ou decoche ce jeu seul, comme la case d'une ListView (la selection ne bouge pas)
                    item.Checked = !item.Checked;
                    focusItem = item;
                }
                else if (item == null)
                {
                    if (!ctrl && !shift) SelectOnly(null);
                }
                else if (shift)
                {
                    int a = IndexOf(anchor);
                    SelectRange(a >= 0 ? a : i, i, ctrl);
                    focusItem = item;
                }
                else if (ctrl)
                {
                    item.Selected = !item.Selected;
                    anchor = focusItem = item;
                }
                else
                {
                    SelectOnly(item);
                    anchor = focusItem = item;
                }
            }
            else if (e.Button == MouseButtons.Right && item != null && !item.Selected)
            {
                SelectOnly(item); // comme une ListView : le menu contextuel porte sur le jeu clique
                anchor = focusItem = item;
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = HitTest(e.Location);
            if (i != hoverIndex) { hoverIndex = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverIndex != -1) { hoverIndex = -1; Invalidate(); }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                case Keys.Home: case Keys.End: case Keys.PageUp: case Keys.PageDown:
                case Keys.Enter: case Keys.Escape: case Keys.Space:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || source == null) return;
            bool ctrl = e.Control, shift = e.Shift;
            int cols = Math.Max(1, layout.Columns);
            int cur = IndexOf(focusItem);
            int target = int.MinValue;
            switch (e.KeyCode)
            {
                case Keys.Left: target = cur < 0 ? 0 : cur - 1; break;
                case Keys.Right: target = cur < 0 ? 0 : cur + 1; break;
                case Keys.Up: target = cur < 0 ? 0 : cur - cols; break;
                case Keys.Down: target = cur < 0 ? 0 : cur + cols; break;
                case Keys.Home: target = 0; break;
                case Keys.End: target = items.Count - 1; break;
                case Keys.PageUp:
                case Keys.PageDown:
                    {
                        int rowsPerPage = Math.Max(1, ClientSize.Height / Math.Max(1, layout.RowPitch));
                        int d = rowsPerPage * cols * (e.KeyCode == Keys.PageUp ? -1 : 1);
                        target = cur < 0 ? 0 : Math.Max(0, Math.Min(items.Count - 1, cur + d));
                        break;
                    }
                case Keys.Space:
                    if (!ctrl && !shift && !e.Alt)
                    {
                        var sel = new List<ListViewItem>();
                        foreach (ListViewItem it in source.SelectedItems) sel.Add(it);
                        if (sel.Count == 0 && focusItem != null) sel.Add(focusItem);
                        ToggleChecked(sel);
                        e.Handled = e.SuppressKeyPress = true;
                        Invalidate();
                        return;
                    }
                    break;
                case Keys.A:
                    if (ctrl && !shift && !e.Alt)
                    {
                        // Ctrl+A : les jeux VISIBLES seulement (relaye, il selectionnerait aussi les jeux masques par le filtre)
                        if (items.Count > 0) SelectRange(0, items.Count - 1, false);
                        e.Handled = e.SuppressKeyPress = true;
                        Invalidate();
                        return;
                    }
                    break;
            }
            if (target != int.MinValue)
            {
                if (items.Count == 0) { e.Handled = true; return; }
                target = Math.Max(0, Math.Min(items.Count - 1, target));
                var item = items[target];
                if (shift)
                {
                    int a = IndexOf(anchor);
                    SelectRange(a >= 0 ? a : target, target, ctrl);
                }
                else if (!ctrl)
                {
                    SelectOnly(item);
                    anchor = item;
                }
                focusItem = item;
                EnsureVisible(target);
                e.Handled = true;
                Invalidate();
                return;
            }
            RelayKeyDown(e);
        }

        private static readonly MethodInfo onKeyDown = typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>Les raccourcis de la liste (Suppr, F2, Entree, Echap, Ctrl+N...) restent geres par les gestionnaires KeyDown de la source.</summary>
        private void RelayKeyDown(KeyEventArgs e)
        {
            if (source == null || source.IsDisposed) return;
            var copy = new KeyEventArgs(e.KeyData);
            onKeyDown.Invoke(source, new object[] { copy });
            if (copy.Handled) e.Handled = true;
            if (copy.SuppressKeyPress) e.SuppressKeyPress = true;
            Invalidate();
        }

        /// <summary>Menu contextuel : celui de la grille s'il est defini, sinon celui de la source.</summary>
        public override ContextMenuStrip ContextMenuStrip
        {
            get { return base.ContextMenuStrip ?? (source != null ? source.ContextMenuStrip : null); }
            set { base.ContextMenuStrip = value; }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        // --- dessin --------------------------------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            EnsureFonts();
            B1Draw.Fill(g, B1Draw.IsPalette(BackColor) ? BackColor : B1Palette.Slate, ClientRectangle);
            if (items.Count == 0) return;
            int first, last;
            layout.VisibleRange(scrollY, ClientSize.Height, out first, out last);
            if (last < first) return;
            var size = CoverSize;
            int cols = Math.Max(1, layout.Columns);
            // pre-chargement d'une rangee au-dessus et de deux en dessous, demande AVANT les cases visibles :
            // la file de decodage est une pile, les cases visibles passent donc en premier
            int pf0 = Math.Max(0, first - cols), pf1 = Math.Min(items.Count - 1, last + 2 * cols);
            for (int i = pf0; i <= pf1; i++)
            {
                if (i >= first && i <= last) continue;
                var app = items[i].Tag as NesApplication;
                if (app != null && !CoverTargets.NeedsCover(app)) cache.Get(app, size, null);
            }
            for (int i = last; i >= first; i--)
                if (e.ClipRectangle.IntersectsWith(Rectangle.Inflate(GetItemRect(i), layout.Padding, layout.Padding)))
                    PaintCell(g, i, size);
        }

        private void PaintCell(Graphics g, int index, Size size)
        {
            float s = DpiScale;
            var item = items[index];
            var app = item.Tag as NesApplication;
            var cover = GetCoverRect(index);
            bool selected = item.Selected, isChecked = item.Checked, hot = index == hoverIndex;

            Image img = null;
            bool missing = app == null || CoverTargets.NeedsCover(app);
            if (!missing)
            {
                img = cache.Get(app, size, onThumbReady);
                if (img == null && cache.IsMissing(app, size)) missing = true;
            }
            g.SmoothingMode = SmoothingMode.None;
            if (missing) PaintMissing(g, cover, s);
            else if (img != null) g.DrawImageUnscaled(img, cover.Location);
            else B1Draw.Fill(g, B1Palette.RowActive, cover); // en cours de decodage

            var sw = GetSwitchRect(index);
            if (isChecked) PaintSwitch(g, sw, s, true);
            else if (hot) PaintSwitch(g, sw, s, false);

            int off = B1Draw.Px(3, s), t = B1Draw.Px(2, s);
            if (selected)
                B1Draw.Frame(g, B1Palette.Lavender, Rectangle.Inflate(cover, off + t, off + t), t);
            else if (item == focusItem && Focused && B1Draw.ShowFocusCues(this))
                B1Draw.Frame(g, B1Palette.Dashed, Rectangle.Inflate(cover, off + 1, off + 1), B1Draw.Px(1, s));

            var lr = layout.LabelBounds(index);
            lr.Offset(0, -scrollY);
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.TextBoxControl |
                        TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.Top;
            flags |= RightToLeft == RightToLeft.Yes ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
            TextRenderer.DrawText(g, item.Text, labelFont, lr, selected || hot ? B1Palette.Text : B1Palette.TextSecondary, flags);
        }

        /// <summary>Hachures a 135° HatchA/HatchB de 6 px, contour pointille Dashed, « sans jaquette » en Lavender.</summary>
        private void PaintMissing(Graphics g, Rectangle r, float s)
        {
            B1Draw.Fill(g, B1Palette.HatchA, r);
            var oldClip = g.Clip;
            g.SetClip(r, CombineMode.Intersect);
            float band = 6f * s * (float)Math.Sqrt(2); // 6 px mesures perpendiculairement aux hachures
            using (var b = new SolidBrush(B1Palette.HatchB))
            {
                // bandes x + y = constante (« / »), comme repeating-linear-gradient(135deg, A 0 6px, B 6px 12px)
                for (float c = band; c < r.Width + r.Height; c += 2 * band)
                {
                    float c2 = c + band;
                    g.FillPolygon(b, new[]
                    {
                        new PointF(r.X + c, r.Y), new PointF(r.X + c2, r.Y),
                        new PointF(r.X + c2 - r.Height, r.Bottom), new PointF(r.X + c - r.Height, r.Bottom),
                    });
                }
            }
            g.Clip = oldClip;
            oldClip.Dispose();
            using (var p = new Pen(B1Palette.Dashed, 1) { DashPattern = new[] { 3f, 3f } })
                g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);

            string text = Resources.CoverGridNoCover;
            var tr = new Rectangle(r.X + B1Draw.Px(4, s), r.Bottom - (int)(r.Height * 0.08f) - missFont.Height, r.Width - 2 * B1Draw.Px(4, s), missFont.Height);
            TextRenderer.DrawText(g, text, missFont, tr, B1Palette.Lavender,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        /// <summary>Interrupteur strie du motif B1 (POWER/RESET de la console US) : bandes verticales Accent/Primary de 3 px.
        /// Eteint (survol d'un jeu non coche) : cadre Dashed sur Slate, pour montrer ou cliquer.</summary>
        private static void PaintSwitch(Graphics g, Rectangle r, float s, bool on)
        {
            int t = B1Draw.Px(1, s);
            B1Draw.Fill(g, B1Palette.Slate, Rectangle.Inflate(r, t, t));
            if (on)
            {
                int stripe = B1Draw.Px(3, s);
                for (int x = r.X, k = 0; x < r.Right; x += stripe, k++)
                    B1Draw.Fill(g, k % 2 == 0 ? B1Palette.Accent : B1Palette.Primary, x, r.Y, Math.Min(stripe, r.Right - x), r.Height);
            }
            else B1Draw.Frame(g, B1Palette.Dashed, r, t);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Bind(null);
                if (filter != null) filter.Changed -= Filter_Changed;
                if (ownsCache && cache != null) cache.Dispose();
            }
            base.Dispose(disposing);
        }

        // --- natif ---------------------------------------------------------------------------------------------

        private const int WS_VSCROLL = 0x00200000, WM_VSCROLL = 0x0115, SB_VERT = 1;
        private const int SB_LINEUP = 0, SB_LINEDOWN = 1, SB_PAGEUP = 2, SB_PAGEDOWN = 3, SB_THUMBPOSITION = 4, SB_THUMBTRACK = 5, SB_TOP = 6, SB_BOTTOM = 7;
        private const int SIF_RANGE = 0x1, SIF_PAGE = 0x2, SIF_POS = 0x4, SIF_TRACKPOS = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        private struct SCROLLINFO
        {
            public int cbSize;
            public int fMask;
            public int nMin;
            public int nMax;
            public uint nPage;
            public int nPos;
            public int nTrackPos;
        }

        [DllImport("user32.dll")] private static extern int SetScrollInfo(IntPtr hwnd, int bar, ref SCROLLINFO si, bool redraw);
        [DllImport("user32.dll")] private static extern bool GetScrollInfo(IntPtr hwnd, int bar, ref SCROLLINFO si);
    }
}
