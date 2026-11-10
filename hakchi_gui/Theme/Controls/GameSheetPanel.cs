using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Properties;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Colonne de droite : fiche du jeu choisi. L'en-tete (peint par le panneau) montre la jaquette, le titre, « systeme · annee »,
    /// la source de la pochette avec « changer », puis les champs connus (joueurs, editeur, genre, core). Seuls les champs que
    /// hakchi renseigne sont montres : une valeur vide ou la valeur par defaut de hakchi (editeur UNKNOWN, date 1900) est omise.
    /// <para>
    /// Les controles enfants (champs editables de MainForm) se placent SOUS l'en-tete : <see cref="DisplayRectangle"/> exclut
    /// <see cref="HeaderHeight"/>, donc un enfant en Dock=Fill / Top / Bottom commence apres lui.
    /// </para>
    /// </summary>
    public class GameSheetPanel : Panel
    {
        private NesApplication app;
        private string title = string.Empty, meta = string.Empty, year = string.Empty, coverSource = string.Empty;
        private bool coverSourceKnown;
        private readonly List<KeyValuePair<string, string>> fields = new List<KeyValuePair<string, string>>();
        private Rectangle coverBounds, linkBounds;
        private Rectangle titleBounds, metaBounds;
        private Rectangle srcPrefixBounds, srcValueBounds;
        private readonly List<Rectangle[]> fieldBounds = new List<Rectangle[]>();     // par ligne : [libelle, valeur]
        private int headerHeight;
        private bool linkHot;
        private ThumbnailCache cache;
        private readonly ShellFonts fonts = new ShellFonts();
        private readonly Action<NesApplication> onThumbReady;

        public GameSheetPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = B1Palette.Slate;
            ForeColor = B1Palette.Text;
            onThumbReady = a =>
            {
                if (IsDisposed || Disposing || !IsHandleCreated) return;
                try
                {
                    if (InvokeRequired) BeginInvoke(new Action(() => { if (!IsDisposed && a == app) Invalidate(); }));
                    else if (a == app) Invalidate();
                }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            };
        }

        /// <summary>L'utilisateur a clique sur « changer » (source de la pochette). L'appelant ouvre le choix de jaquette.</summary>
        public event EventHandler ChangeCoverRequested;

        // --- donnees ----------------------------------------------------------------------------------------------

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NesApplication Game { get { return app; } }

        public string TitleText { get { return title; } }
        public string MetaText { get { return meta; } }
        public string YearText { get { return year; } }

        /// <summary>Source de la jaquette (<c>Metadata.CoverSource</c>), ou « inconnue ».</summary>
        public string CoverSourceText { get { return coverSource; } }

        public IReadOnlyList<KeyValuePair<string, string>> Fields { get { return fields; } }

        public Rectangle CoverBounds { get { return coverBounds; } }
        public Rectangle ChangeLinkBounds { get { return linkBounds; } }

        /// <summary>Hauteur de l'en-tete peint ; 0 sans jeu.</summary>
        public int HeaderHeight { get { return headerHeight; } }

        /// <summary>La jaquette est decodee et dessinee (sinon aplat d'attente, ou hachures si absente).</summary>
        public bool CoverReady
        {
            get { return app != null && cache != null && !coverBounds.IsEmpty && cache.Contains(app, coverBounds.Size); }
        }

        public override Rectangle DisplayRectangle
        {
            get
            {
                var r = base.DisplayRectangle;
                int h = Math.Min(headerHeight, r.Height);
                return new Rectangle(r.X, r.Y + h, r.Width, r.Height - h);
            }
        }

        /// <summary>Affiche la fiche d'un jeu ; null vide la fiche.</summary>
        public void Show(NesApplication game)
        {
            app = game;
            fields.Clear();
            title = meta = year = coverSource = string.Empty;
            coverSourceKnown = false;
            if (game != null)
            {
                title = game.Name ?? string.Empty;
                var desktop = game.Desktop;
                var md = game.Metadata;

                string system = ShellText.SystemDisplay(GameFilter.SystemOf(game));
                year = ParseYear(desktop == null ? null : desktop.ReleaseDate);
                meta = system.Length > 0 && year.Length > 0 ? system + " · " + year : system.Length > 0 ? system : year;

                string src = md == null ? null : md.CoverSource;
                coverSourceKnown = !string.IsNullOrWhiteSpace(src);
                coverSource = coverSourceKnown ? src : Resources.SheetCoverSourceUnknown;

                if (desktop != null)
                {
                    if (desktop.Players > 0) fields.Add(new KeyValuePair<string, string>(Resources.SheetPlayers, desktop.Players.ToString(CultureInfo.CurrentCulture)));
                    string pub = desktop.Publisher;
                    if (!string.IsNullOrWhiteSpace(pub) && !string.Equals(pub.Trim(), "UNKNOWN", StringComparison.OrdinalIgnoreCase) && pub.Trim('\u0001').Length > 0)
                        fields.Add(new KeyValuePair<string, string>(Resources.SheetPublisher, pub.Trim()));
                    if (!string.IsNullOrWhiteSpace(desktop.Genre))
                        fields.Add(new KeyValuePair<string, string>(Resources.SheetGenre, desktop.Genre.Trim()));
                }
                string core = md == null ? null : md.Core;
                if (string.IsNullOrWhiteSpace(core) && md != null && md.AppInfo != null) core = md.AppInfo.DefaultCore;
                if (!string.IsNullOrWhiteSpace(core))
                    fields.Add(new KeyValuePair<string, string>(Resources.SheetCore, core.Trim()));
            }
            RelayoutHeader();
            Invalidate();
        }

        /// <summary>A appeler apres un changement de jaquette : vide la vignette de ce jeu et relit la fiche.</summary>
        public void ReloadCover()
        {
            if (cache != null && app != null) cache.Forget(app);
            Show(app);
        }

        /// <summary>« 1994-03-19 » -> « 1994 » ; vide si inconnue (valeurs par defaut de hakchi : 1900-01-01, 0000-00-00).</summary>
        internal static string ParseYear(string releaseDate)
        {
            if (string.IsNullOrWhiteSpace(releaseDate)) return string.Empty;
            var d = releaseDate.Trim();
            if (d.Length < 4) return string.Empty;
            int y;
            if (!int.TryParse(d.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out y)) return string.Empty;
            return y < 1950 ? string.Empty : y.ToString(CultureInfo.InvariantCulture);
        }

        // --- mise en page ------------------------------------------------------------------------------------------

        private float S { get { return B1Dpi.Scale(this); } }

        private void RelayoutHeader()
        {
            float s = S;
            int old = headerHeight;
            coverBounds = linkBounds = titleBounds = metaBounds = srcPrefixBounds = srcValueBounds = Rectangle.Empty;
            fieldBounds.Clear();
            if (app == null) headerHeight = 0;
            else ComputeHeader(s);
            if (headerHeight != old) PerformLayout();
        }

        private void ComputeHeader(float s)
        {
            bool rtl = RightToLeft == RightToLeft.Yes;
            var body = fonts.Get(ShellFonts.Kind.Body, 9f, s);
            var head = fonts.Get(ShellFonts.Kind.Title, 13f, s);
            int padX = B1Draw.Px(18, s), padTop = B1Draw.Px(16, s);
            int edge = B1Draw.Px(1, s);                                   // filet de gauche
            int inner = Math.Max(10, ClientSize.Width - 2 * padX - edge);
            int x0 = rtl ? ClientSize.Width - padX - inner : padX + (rtl ? 0 : edge);
            int y = padTop;

            // jaquette 3:4, 62 % de la largeur utile (maquette)
            int cw = (int)Math.Round(inner * 0.62);
            int ch = (int)Math.Round(cw * 4.0 / 3.0);
            coverBounds = new Rectangle(rtl ? x0 + inner - cw : x0, y, cw, ch);
            y += ch + B1Draw.Px(10, s);

            // titre : 2 lignes au plus
            int tw = inner;
            int th = Math.Min(2 * head.Height, TextRenderer.MeasureText(title, head, new Size(tw, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height);
            titleBounds = new Rectangle(x0, y, tw, Math.Max(head.Height, th));
            y += titleBounds.Height + B1Draw.Px(2, s);

            if (meta.Length > 0)
            {
                metaBounds = new Rectangle(x0, y, inner, body.Height);
                y += body.Height;
            }
            y += B1Draw.Px(10, s);

            // « Source de la pochette : <source> · changer »
            string prefix = Resources.SheetCoverSource + " ";
            string link = Resources.SheetChangeCover;
            string sep = " · ";
            int pw = ShellText.Width(prefix, body), sw = ShellText.Width(coverSource, body);
            int dw = ShellText.Width(sep, body), lw = ShellText.Width(link, body);
            int lineH = body.Height;
            if (pw + sw + dw + lw <= inner)
            {
                srcPrefixBounds = new Rectangle(rtl ? x0 + inner - pw : x0, y, pw, lineH);
                srcValueBounds = new Rectangle(rtl ? srcPrefixBounds.X - sw : x0 + pw, y, sw, lineH);
                linkBounds = new Rectangle(rtl ? srcValueBounds.X - dw - lw : x0 + pw + sw + dw, y, lw, lineH);
                y += lineH;
            }
            else
            {
                int avail = Math.Max(10, inner - pw);
                int sv = Math.Min(sw, avail);
                srcPrefixBounds = new Rectangle(rtl ? x0 + inner - pw : x0, y, pw, lineH);
                srcValueBounds = new Rectangle(rtl ? srcPrefixBounds.X - sv : x0 + pw, y, sv, lineH);
                y += lineH + B1Draw.Px(2, s);
                linkBounds = new Rectangle(rtl ? x0 + inner - lw : x0, y, lw, lineH);
                y += lineH;
            }
            y += B1Draw.Px(14, s);

            // champs : colonne de libelles (TextFaint) puis valeurs
            int labelW = 0;
            foreach (var kv in fields) labelW = Math.Max(labelW, ShellText.Width(kv.Key, body));
            int gap = B1Draw.Px(16, s);
            int rowH = body.Height + B1Draw.Px(5, s);
            foreach (var kv in fields)
            {
                int vw = Math.Max(10, inner - labelW - gap);
                var lab = new Rectangle(rtl ? x0 + inner - labelW : x0, y, labelW, rowH);
                var val = new Rectangle(rtl ? x0 : x0 + labelW + gap, y, vw, rowH);
                fieldBounds.Add(new[] { lab, val });
                y += rowH;
            }
            headerHeight = y + B1Draw.Px(14, s);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            RelayoutHeader();
            base.OnResize(eventargs);
        }

        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); RelayoutHeader(); Invalidate(); }
        protected override void OnRightToLeftChanged(EventArgs e) { base.OnRightToLeftChanged(e); RelayoutHeader(); Invalidate(); }

        // --- souris ------------------------------------------------------------------------------------------------

        private Rectangle LinkHit { get { return linkBounds.IsEmpty ? linkBounds : Rectangle.Inflate(linkBounds, B1Draw.Px(4, S), B1Draw.Px(3, S)); } }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hot = !linkBounds.IsEmpty && LinkHit.Contains(e.Location);
            if (hot != linkHot) { linkHot = hot; Cursor = hot ? Cursors.Hand : Cursors.Default; Invalidate(linkBounds); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (linkHot) { linkHot = false; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || app == null || linkBounds.IsEmpty || !LinkHit.Contains(e.Location)) return;
            var h = ChangeCoverRequested;
            if (h != null) h(this, EventArgs.Empty);
        }

        // --- rendu -------------------------------------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            float s = S;
            g.Clear(B1Palette.Slate);
            bool rtl = RightToLeft == RightToLeft.Yes;
            int edge = B1Draw.Px(1, s);
            B1Draw.Fill(g, B1Palette.Rule, rtl ? ClientSize.Width - edge : 0, 0, edge, ClientSize.Height);   // filet cote grille
            if (app == null) return;

            var body = fonts.Get(ShellFonts.Kind.Body, 9f, s);
            var head = fonts.Get(ShellFonts.Kind.Title, 13f, s);
            var dir = rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left;
            const TextFormatFlags Base = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

            PaintCover(g, s);

            TextRenderer.DrawText(g, title, head, titleBounds, B1Palette.Text, Base | TextFormatFlags.WordBreak | dir);
            if (!metaBounds.IsEmpty)
                TextRenderer.DrawText(g, meta, body, metaBounds, B1Palette.TextDim, Base | TextFormatFlags.SingleLine | dir);

            TextRenderer.DrawText(g, Resources.SheetCoverSource, body, srcPrefixBounds, B1Palette.TextDim, Base | TextFormatFlags.SingleLine | dir);
            TextRenderer.DrawText(g, coverSource, body, srcValueBounds, coverSourceKnown ? B1Palette.Lavender : B1Palette.TextDim, Base | TextFormatFlags.SingleLine | dir);
            // separateur « · » entre la source et le lien (ligne commune seulement)
            if (linkBounds.Y == srcValueBounds.Y)
            {
                int dw = ShellText.Width(" · ", body);
                var sepR = rtl ? new Rectangle(linkBounds.X - dw, linkBounds.Y, dw, linkBounds.Height) : new Rectangle(srcValueBounds.Right, linkBounds.Y, dw, linkBounds.Height);
                TextRenderer.DrawText(g, " · ", body, sepR, B1Palette.TextFaint, Base | TextFormatFlags.SingleLine | dir);
            }
            TextRenderer.DrawText(g, Resources.SheetChangeCover, body, linkBounds, B1Palette.Lavender, Base | TextFormatFlags.SingleLine | dir);
            if (linkHot)
                B1Draw.Fill(g, B1Palette.Lavender, linkBounds.X, linkBounds.Bottom - B1Draw.Px(2, s), linkBounds.Width, B1Draw.Px(1, s));

            for (int i = 0; i < fields.Count; i++)
            {
                TextRenderer.DrawText(g, fields[i].Key, body, fieldBounds[i][0], B1Palette.TextFaint, Base | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | dir);
                TextRenderer.DrawText(g, fields[i].Value, body, fieldBounds[i][1], B1Palette.Text, Base | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | dir);
            }
        }

        private void PaintCover(Graphics g, float s)
        {
            var r = coverBounds;
            if (r.IsEmpty) return;
            if (cache == null)
            {
                try { cache = new ThumbnailCache(8); } catch (InvalidOperationException) { cache = null; }
            }
            Image img = cache == null ? null : cache.Get(app, r.Size, onThumbReady);
            if (img != null)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;      // la vignette est deja a la taille exacte
                g.DrawImage(img, r.X, r.Y, r.Width, r.Height);
                return;
            }
            if (cache != null && cache.IsMissing(app, r.Size))
            {
                B1Draw.Fill(g, B1Palette.HatchA, r);
                var state = g.Save();
                g.SetClip(r, CombineMode.Intersect);
                using (var br = new SolidBrush(B1Palette.HatchB))
                {
                    int band = B1Draw.Px(6, s);
                    for (int c = -r.Height; c < r.Width; c += 2 * band)
                        g.FillPolygon(br, new[]
                        {
                            new PointF(r.X + c, r.Y), new PointF(r.X + c + band, r.Y),
                            new PointF(r.X + c + band - r.Height, r.Bottom), new PointF(r.X + c - r.Height, r.Bottom),
                        });
                }
                g.Restore(state);
                using (var p = new Pen(B1Palette.Dashed, B1Draw.Px(1, s)) { DashPattern = new[] { 3f, 3f } })
                    g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
                var f = fonts.Get(ShellFonts.Kind.Small, 8f, s);
                TextRenderer.DrawText(g, Resources.CoverGridNoCover, f, r, B1Palette.Lavender,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            else B1Draw.Fill(g, B1Palette.RowActive, r);                      // decodage en cours
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (cache != null) cache.Dispose();
                fonts.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
