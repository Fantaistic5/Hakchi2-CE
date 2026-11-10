using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Properties;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Colonne de gauche : « Systemes » (Tous + un par systeme, avec le nombre de jeux) puis « Collections » (Sans jaquette,
    /// Favoris si hakchi en avait). Une seule ligne active : fond RowActive + barre Accent de 3 px. Le controle ne filtre rien
    /// lui-meme : <see cref="FilterChanged"/> donne un <see cref="GameFilter"/> dont seuls System et Collection comptent
    /// (le reste est a la valeur par defaut) ; l'appelant les copie dans le filtre de la grille.
    /// </summary>
    public class SystemNavList : Control
    {
        private enum RowKind { Header, All, System, Collection }

        private sealed class Row
        {
            public RowKind Kind;
            public string Text;        // libelle affiche
            public string System;      // cle (nom de l'AppInfo) pour Kind.System
            public GameCollection Collection;
            public int Count = -1;     // -1 = pas de nombre
            public Rectangle Bounds;
        }

        private readonly List<Row> rows = new List<Row>();
        private List<KeyValuePair<string, int>> systems = new List<KeyValuePair<string, int>>();
        private readonly Dictionary<GameCollection, int> collectionCounts = new Dictionary<GameCollection, int>();
        private string selectedSystem;                       // null = Tous
        private GameCollection selectedCollection = GameCollection.All;
        private Row hover;
        private int scroll;
        private readonly ShellFonts fonts = new ShellFonts();

        public SystemNavList()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            BackColor = B1Palette.Slate;
            ForeColor = B1Palette.TextSecondary;
            Size = new Size(220, 400);
            AccessibleRole = AccessibleRole.List;
            Rebuild();
        }

        /// <summary>L'utilisateur a choisi une autre ligne (clic, fleches) ou la ligne choisie a disparu.</summary>
        public event EventHandler<GameFilter> FilterChanged;

        // --- donnees -----------------------------------------------------------------------------------------------

        /// <summary>Systemes presents et leur nombre de jeux. Garde la selection si le systeme existe encore, sinon revient a « Tous ».</summary>
        public void SetSystems(IEnumerable<(string name, int count)> list)
        {
            systems = (list ?? Enumerable.Empty<(string name, int count)>())
                .Where(x => !string.IsNullOrEmpty(x.name))
                .Select(x => new KeyValuePair<string, int>(x.name, x.count)).ToList();
            bool lost = selectedSystem != null && !systems.Any(x => x.Key == selectedSystem);
            if (lost) selectedSystem = null;
            Rebuild();
            if (lost) RaiseFilterChanged();
        }

        /// <summary>Nombre de jeux d'une collection (Sans jaquette...). Les collections gardent leur compte entre deux SetSystems.</summary>
        public void SetCollectionCount(GameCollection collection, int count)
        {
            collectionCounts[collection] = count;
            Rebuild();
        }

        public int CountOf(string system)
        {
            foreach (var kv in systems) if (kv.Key == system) return kv.Value;
            return -1;
        }

        public int CountOf(GameCollection collection)
        {
            int n;
            return collectionCounts.TryGetValue(collection, out n) ? n : -1;
        }

        /// <summary>Somme des jeux de tous les systemes (la ligne « Tous »).</summary>
        public int TotalCount { get { return systems.Sum(x => x.Value); } }

        /// <summary>Systeme choisi ; null = tous.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedSystem { get { return selectedSystem; } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public GameCollection SelectedCollection { get { return selectedCollection; } }

        public void SelectAll() { Select(null, GameCollection.All); }
        public void SelectSystem(string system) { Select(system, GameCollection.All); }
        public void SelectCollection(GameCollection c) { Select(null, c); }

        private void Select(string system, GameCollection collection)
        {
            if (collection == GameCollection.Favorites && !GameFilter.FavoritesAvailable) collection = GameCollection.All;
            if (system != null && !systems.Any(x => x.Key == system)) system = null;
            if (system == selectedSystem && collection == selectedCollection) return;
            selectedSystem = system;
            selectedCollection = collection;
            UpdateAccessibleName();
            Invalidate();
            RaiseFilterChanged();
        }

        private void UpdateAccessibleName()
        {
            var r = rows.FirstOrDefault(IsActive);
            AccessibleName = r == null ? Resources.NavSystemsHeader : r.Text + (r.Count >= 0 ? ", " + r.Count : string.Empty);
        }

        private void RaiseFilterChanged()
        {
            var h = FilterChanged;
            if (h != null) h(this, new GameFilter { System = selectedSystem, Collection = selectedCollection });
        }

        // --- lignes ------------------------------------------------------------------------------------------------

        private float S { get { return B1Dpi.Scale(this); } }
        private int RowHeight { get { return B1Draw.Px(26, S); } }
        private int HeaderHeight(bool first) { return B1Draw.Px(first ? 28 : 34, S); }

        private void Rebuild()
        {
            rows.Clear();
            rows.Add(new Row { Kind = RowKind.Header, Text = Resources.NavSystemsHeader });
            rows.Add(new Row { Kind = RowKind.All, Text = Resources.NavAll, Count = TotalCount });
            foreach (var kv in systems)
                rows.Add(new Row { Kind = RowKind.System, Text = ShellText.SystemDisplay(kv.Key), System = kv.Key, Count = kv.Value });
            rows.Add(new Row { Kind = RowKind.Header, Text = Resources.NavCollectionsHeader });
            rows.Add(new Row { Kind = RowKind.Collection, Collection = GameCollection.NoCover, Text = Resources.NavNoCover, Count = CountOf(GameCollection.NoCover) });
            if (GameFilter.FavoritesAvailable)
                rows.Add(new Row { Kind = RowKind.Collection, Collection = GameCollection.Favorites, Text = Resources.NavFavorites, Count = CountOf(GameCollection.Favorites) });
            hover = null;
            UpdateAccessibleName();
            LayoutRows();
            Invalidate();
        }

        private void LayoutRows()
        {
            int maxScroll = Math.Max(0, ContentHeightUnscrolled() - ClientSize.Height);
            scroll = Math.Max(0, Math.Min(scroll, maxScroll));
            int y = B1Draw.Px(8, S) - scroll;
            int w = Math.Max(0, ClientSize.Width - B1Draw.Px(1, S));
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int h = r.Kind == RowKind.Header ? HeaderHeight(i == 0) : RowHeight;
                r.Bounds = new Rectangle(0, y, w, h);
                y += h;
            }
        }

        private int ContentHeightUnscrolled()
        {
            int y = B1Draw.Px(8, S);
            for (int i = 0; i < rows.Count; i++) y += rows[i].Kind == RowKind.Header ? HeaderHeight(i == 0) : RowHeight;
            return y + B1Draw.Px(8, S);
        }

        private bool IsActive(Row r)
        {
            switch (r.Kind)
            {
                case RowKind.All: return selectedSystem == null && selectedCollection == GameCollection.All;
                case RowKind.System: return selectedSystem == r.System;
                case RowKind.Collection: return selectedSystem == null && selectedCollection == r.Collection;
                default: return false;
            }
        }

        private IEnumerable<Row> Selectable { get { return rows.Where(r => r.Kind != RowKind.Header); } }

        public Rectangle GetAllRowBounds()
        {
            var r = rows.FirstOrDefault(x => x.Kind == RowKind.All);
            return r == null ? Rectangle.Empty : r.Bounds;
        }

        /// <summary>Rectangle (coordonnees du controle) de la ligne d'un systeme ; vide s'il n'existe pas.</summary>
        public Rectangle GetSystemRowBounds(string system)
        {
            var r = rows.FirstOrDefault(x => x.Kind == RowKind.System && x.System == system);
            return r == null ? Rectangle.Empty : r.Bounds;
        }

        /// <summary>Rectangle de la ligne d'une collection ; vide si elle est masquee (Favoris : hakchi n'en a pas).</summary>
        public Rectangle GetCollectionRowBounds(GameCollection c)
        {
            var r = rows.FirstOrDefault(x => x.Kind == RowKind.Collection && x.Collection == c);
            return r == null ? Rectangle.Empty : r.Bounds;
        }

        // --- souris et clavier -------------------------------------------------------------------------------------

        private Row RowAt(Point p)
        {
            foreach (var r in Selectable) if (r.Bounds.Contains(p)) return r;
            return null;
        }

        private void Choose(Row r)
        {
            switch (r.Kind)
            {
                case RowKind.All: Select(null, GameCollection.All); break;
                case RowKind.System: Select(r.System, GameCollection.All); break;
                case RowKind.Collection: Select(null, r.Collection); break;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            var r = RowAt(e.Location);
            if (r != null) Choose(r);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var r = RowAt(e.Location);
            if (r != hover) { hover = r; Cursor = r != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null) { hover = null; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int before = scroll;
            scroll -= Math.Sign(e.Delta) * RowHeight * 2;
            LayoutRows();
            if (scroll != before) Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.Home: case Keys.End: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            var list = Selectable.ToList();
            int i = list.FindIndex(IsActive);
            switch (e.KeyCode)
            {
                case Keys.Down: i = Math.Min(list.Count - 1, i + 1); break;
                case Keys.Up: i = Math.Max(0, i - 1); break;
                case Keys.Home: i = 0; break;
                case Keys.End: i = list.Count - 1; break;
                default: return;
            }
            e.Handled = true;
            if (i >= 0 && i < list.Count) { Choose(list[i]); EnsureVisible(list[i]); }
        }

        private void EnsureVisible(Row r)
        {
            var b = r.Bounds;
            if (b.Top < 0) scroll += b.Top;
            else if (b.Bottom > ClientSize.Height) scroll += b.Bottom - ClientSize.Height;
            LayoutRows();
            Invalidate();
        }

        // --- rendu -------------------------------------------------------------------------------------------------

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutRows(); Invalidate(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); LayoutRows(); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnRightToLeftChanged(EventArgs e) { base.OnRightToLeftChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = S;
            g.Clear(B1Palette.Slate);
            var body = fonts.Get(ShellFonts.Kind.Body, 9f, s);
            var caps = fonts.Get(ShellFonts.Kind.Caps, 7.5f, s);
            int rule = B1Draw.Px(1, s);
            int padX = B1Draw.Px(16, s);
            bool rtl = RightToLeft == RightToLeft.Yes;

            foreach (var r in rows)
            {
                var b = r.Bounds;
                if (b.Bottom < 0 || b.Top > ClientSize.Height) continue;
                if (r.Kind == RowKind.Header)
                {
                    // le titre colle au bas de la ligne (padding-top de la maquette)
                    var t = r.Text.ToUpper(System.Globalization.CultureInfo.CurrentUICulture);
                    int th = caps.Height;
                    int tw = B1Draw.TrackedWidth(g, t, caps, 0.14f);
                    int tx = rtl ? b.Right - padX - tw : b.X + padX;
                    B1Draw.DrawTracked(g, t, caps, B1Palette.TextFaint, tx, b.Bottom - th - B1Draw.Px(5, s), 0.14f);
                    continue;
                }
                bool active = IsActive(r);
                if (active)
                {
                    B1Draw.Fill(g, B1Palette.RowActive, b);
                    int bar = B1Draw.Px(3, s);
                    B1Draw.Fill(g, B1Palette.Accent, rtl ? b.Right - bar : b.X, b.Y, bar, b.Height);
                    if (Focused && B1Draw.ShowFocusCues(this))
                        using (var p = new Pen(B1Palette.Dashed, rule) { DashPattern = new[] { 3f, 3f } })
                            g.DrawRectangle(p, b.X + bar + 1, b.Y, b.Width - bar - 2, b.Height - 1);
                }
                Color nameColor = active || r == hover ? B1Palette.Text : B1Palette.TextSecondary;
                int countW = 0;
                string count = r.Count >= 0 ? r.Count.ToString(System.Globalization.CultureInfo.CurrentCulture) : null;
                if (count != null) countW = ShellText.Width(count, body);
                var flags = ShellText.Line | (rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : 0);
                int gap = B1Draw.Px(8, s);
                var nameRect = rtl
                    ? new Rectangle(b.X + padX + countW + (countW > 0 ? gap : 0), b.Y, b.Width - 2 * padX - countW - (countW > 0 ? gap : 0), b.Height)
                    : new Rectangle(b.X + padX, b.Y, b.Width - 2 * padX - countW - (countW > 0 ? gap : 0), b.Height);
                TextRenderer.DrawText(g, r.Text, body, nameRect, nameColor, flags);
                if (count != null)
                {
                    var cr = rtl ? new Rectangle(b.X + padX, b.Y, countW, b.Height) : new Rectangle(b.Right - padX - countW, b.Y, countW, b.Height);
                    TextRenderer.DrawText(g, count, body, cr, B1Palette.TextFaint, flags);
                }
            }
            // filet de separation avec la grille
            B1Draw.Fill(g, B1Palette.Rule, rtl ? 0 : ClientSize.Width - rule, 0, rule, ClientSize.Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) fonts.Dispose();
            base.Dispose(disposing);
        }
    }
}
