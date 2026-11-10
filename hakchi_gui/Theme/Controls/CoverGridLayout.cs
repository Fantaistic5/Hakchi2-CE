using System;
using System.Drawing;

namespace com.clusterrr.hakchi_gui.Theme.Controls
{
    /// <summary>
    /// Mise en page pure de la grille de jaquettes (aucun acces a un controle) : jaquette 3:4, largeur de case entre
    /// 112 et 160 px a 96 dpi, goutiere de 14 px, libelle sur 2 lignes au plus. Coordonnees « contenu » : y = 0 en haut
    /// de la grille, sans defilement.
    /// </summary>
    public struct CoverGridLayout
    {
        public const int MinCell = 112, MaxCell = 160, GutterPx = 14;
        /// <summary>Marge autour de la grille : le contour de selection (2 px decale de 3 px) deborde de 5 px de la jaquette.</summary>
        public const int PaddingPx = 6;
        /// <summary>Espace jaquette / libelle (0,35 em de la maquette) et hauteur de ligne du libelle par defaut, a 96 dpi.</summary>
        public const int LabelGapPx = 5, LineHeightPx = 15;

        public int Columns, Rows, ItemCount;
        public int CellWidth, CellHeight, CoverHeight, LabelHeight, LineHeight;
        public int Gutter, Padding, ContentHeight;

        public Size CellSize { get { return new Size(CellWidth, CellHeight); } }

        /// <param name="clientWidth">largeur disponible (barre de defilement deja retiree)</param>
        /// <param name="lineHeight">hauteur d'une ligne du libelle en pixels (0 = 15 px a l'echelle)</param>
        public static CoverGridLayout Compute(int clientWidth, int itemCount, float dpiScale, int lineHeight = 0)
        {
            float s = dpiScale > 0 ? dpiScale : 1f;
            var l = new CoverGridLayout
            {
                ItemCount = Math.Max(0, itemCount),
                Gutter = B1Dpi.Px(GutterPx, s),
                Padding = B1Dpi.Px(PaddingPx, s),
                LineHeight = lineHeight > 0 ? lineHeight : B1Dpi.Px(LineHeightPx, s),
            };
            int min = B1Dpi.Px(MinCell, s), max = B1Dpi.Px(MaxCell, s);
            int avail = Math.Max(0, clientWidth - 2 * l.Padding);

            // Le moins de colonnes possible avec des cases d'au plus `max` : des jaquettes aussi grandes que possible.
            int cols = Math.Max(1, (int)Math.Ceiling((avail + l.Gutter) / (double)(max + l.Gutter)));
            int cell = (avail - (cols - 1) * l.Gutter) / cols;
            if (cell < min && cols > 1)
            {
                cols--; // une colonne de moins : la case depasse `max`, on la plafonne (le reste de largeur reste a droite)
                cell = (avail - (cols - 1) * l.Gutter) / cols;
            }
            l.Columns = cols;
            l.CellWidth = Math.Max(min, Math.Min(max, cell));
            l.CoverHeight = (int)Math.Round(l.CellWidth * 4 / 3.0, MidpointRounding.AwayFromZero);
            l.LabelHeight = B1Dpi.Px(LabelGapPx, s) + 2 * l.LineHeight;
            l.CellHeight = l.CoverHeight + l.LabelHeight;
            l.Rows = l.ItemCount == 0 ? 0 : (l.ItemCount + cols - 1) / cols;
            l.ContentHeight = l.Rows == 0 ? 0 : 2 * l.Padding + l.Rows * l.CellHeight + (l.Rows - 1) * l.Gutter;
            return l;
        }

        public int RowPitch { get { return CellHeight + Gutter; } }

        /// <summary>Case entiere (jaquette + libelle) du jeu d'indice <paramref name="index"/>.</summary>
        public Rectangle CellBounds(int index)
        {
            if (Columns <= 0 || index < 0) return Rectangle.Empty;
            int r = index / Columns, c = index % Columns;
            return new Rectangle(Padding + c * (CellWidth + Gutter), Padding + r * (CellHeight + Gutter), CellWidth, CellHeight);
        }

        public Rectangle CoverBounds(int index)
        {
            var r = CellBounds(index);
            return new Rectangle(r.X, r.Y, CellWidth, CoverHeight);
        }

        public Rectangle LabelBounds(int index)
        {
            var r = CellBounds(index);
            int gap = LabelHeight - 2 * LineHeight;
            return new Rectangle(r.X, r.Y + CoverHeight + gap, CellWidth, 2 * LineHeight);
        }

        /// <summary>Indice du jeu sous le point (coordonnees contenu), -1 dans une goutiere, une marge ou une case vide.</summary>
        public int HitTest(Point p)
        {
            if (Columns <= 0 || ItemCount == 0) return -1;
            int x = p.X - Padding, y = p.Y - Padding;
            if (x < 0 || y < 0) return -1;
            int c = x / (CellWidth + Gutter), r = y / (CellHeight + Gutter);
            if (c >= Columns || r >= Rows) return -1;
            if (x - c * (CellWidth + Gutter) >= CellWidth) return -1;
            if (y - r * (CellHeight + Gutter) >= CellHeight) return -1;
            int i = r * Columns + c;
            return i < ItemCount ? i : -1;
        }

        /// <summary>Premier et dernier indice (inclus) au moins en partie visibles pour un defilement donne ; (0, -1) si aucun.</summary>
        public void VisibleRange(int scrollY, int viewHeight, out int first, out int last)
        {
            first = 0; last = -1;
            if (Columns <= 0 || ItemCount == 0 || viewHeight <= 0) return;
            int pitch = CellHeight + Gutter;
            int r0 = Math.Max(0, (scrollY - Padding) / pitch);
            int r1 = Math.Min(Rows - 1, (scrollY + viewHeight - Padding) / pitch);
            if (r1 < r0) return;
            first = r0 * Columns;
            last = Math.Min(ItemCount - 1, (r1 + 1) * Columns - 1);
        }
    }
}
