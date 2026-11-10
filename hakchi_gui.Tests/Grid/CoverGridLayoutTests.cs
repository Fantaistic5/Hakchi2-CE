using System.Drawing;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Grid
{
    public class CoverGridLayoutTests
    {
        [Fact]
        public void LayoutColumnsFitWidth()
        {
            var l = CoverGridLayout.Compute(1000, 257, 1f);
            Assert.Equal(6, l.Columns);
            Assert.InRange(l.CellWidth, 112, 160);
            Assert.Equal(14, l.Gutter);
            Assert.Equal(43, l.Rows); // 257 / 6 arrondi au-dessus
            Assert.Equal((int)System.Math.Round(l.CellWidth * 4 / 3.0), l.CoverHeight); // jaquette 3:4
            // tout tient dans la largeur
            Assert.True(l.Padding * 2 + l.Columns * l.CellWidth + (l.Columns - 1) * l.Gutter <= 1000);
            Assert.Equal(l.Padding * 2 + l.Rows * l.CellHeight + (l.Rows - 1) * l.Gutter, l.ContentHeight);
        }

        [Theory]
        [InlineData(300)]
        [InlineData(520)]
        [InlineData(777)]
        [InlineData(1000)]
        [InlineData(1600)]
        [InlineData(2560)]
        public void CellWidthStaysInRange(int width)
        {
            var l = CoverGridLayout.Compute(width, 100, 1f);
            Assert.InRange(l.CellWidth, 112, 160);
            Assert.True(l.Padding * 2 + l.Columns * l.CellWidth + (l.Columns - 1) * l.Gutter <= width);
        }

        [Fact]
        public void LayoutScalesWithDpi()
        {
            var l96 = CoverGridLayout.Compute(1000, 10, 1f);
            var l144 = CoverGridLayout.Compute(1500, 10, 1.5f);
            Assert.Equal(l96.Columns, l144.Columns);
            Assert.Equal(21, l144.Gutter);
            Assert.InRange(l144.CellWidth, 168, 240);
            Assert.InRange(System.Math.Abs(l144.CellWidth - l96.CellWidth * 1.5), 0, 2);
        }

        [Fact]
        public void TinyWidthStillOneColumn()
        {
            var l = CoverGridLayout.Compute(50, 3, 1f);
            Assert.Equal(1, l.Columns);
            Assert.Equal(3, l.Rows);
            Assert.Equal(-1, l.HitTest(new Point(-5, 5)));
        }

        [Fact]
        public void HitTestReturnsItemUnderPoint()
        {
            var l = CoverGridLayout.Compute(1000, 20, 1f);
            for (int i = 0; i < 20; i++)
            {
                var r = l.CellBounds(i);
                Assert.Equal(i, l.HitTest(new Point(r.X + r.Width / 2, r.Y + r.Height / 2)));
                Assert.Equal(i, l.HitTest(new Point(r.X, r.Y)));
                Assert.Equal(i, l.HitTest(new Point(r.Right - 1, r.Bottom - 1)));
            }
            var c0 = l.CellBounds(0);
            Assert.Equal(-1, l.HitTest(new Point(c0.Right + l.Gutter / 2, c0.Y + 10)));   // goutiere horizontale
            Assert.Equal(-1, l.HitTest(new Point(c0.X + 10, c0.Bottom + l.Gutter / 2))); // goutiere verticale
            var last = l.CellBounds(19);
            Assert.Equal(-1, l.HitTest(new Point(last.Right + l.Gutter + 5, last.Y + 5)));  // case vide apres le dernier jeu
            Assert.Equal(-1, l.HitTest(new Point(5, l.ContentHeight + 50)));
            Assert.Equal(1, l.CellBounds(7).Y > l.CellBounds(0).Y ? 1 : 0);
            Assert.Equal(c0.Size, new Size(l.CellWidth, l.CellHeight));
            Assert.Equal(new Rectangle(c0.X, c0.Y, l.CellWidth, l.CoverHeight), l.CoverBounds(0));
        }
    }
}
