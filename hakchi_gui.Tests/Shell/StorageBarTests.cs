using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using Xunit;

namespace hakchi_gui.Tests.Shell
{
    [Collection("B1Static")]
    public class StorageBarTests
    {
        private const long MB = 1024L * 1024;

        private static StorageBar Bar(out Form f)
        {
            var b = new StorageBar { Bounds = new Rectangle(0, 0, 600, 24) };
            f = ShellFixture.Host(b, new Size(600, 24));
            return b;
        }

        [StaFact]
        public void StorageBarFillsAccentUnderLimit()
        {
            Form f; var b = Bar(out f);
            using (f)
            {
                b.SetUsage(66 * MB, 100 * MB);
                Assert.False(b.IsOverflow);
                Assert.Equal(B1Palette.Accent, b.FillColor);
                var t = b.TrackBounds;
                int y = t.Y + t.Height / 2;
                using (var bmp = ShellFixture.Draw(b))
                {
                    Assert.Equal(B1Palette.Accent.ToArgb(), bmp.GetPixel(t.X + t.Width * 66 / 100 - 2, y).ToArgb());
                    Assert.Equal(B1Palette.Rule.ToArgb(), bmp.GetPixel(t.X + t.Width * 66 / 100 + 3, y).ToArgb());
                    Assert.Equal(B1Palette.Rule.ToArgb(), bmp.GetPixel(t.Right - 1, y).ToArgb());
                }
            }
        }

        [StaFact]
        public void StorageBarOverflowUsesError()
        {
            Form f; var b = Bar(out f);
            using (f)
            {
                b.SetUsage(105 * MB, 100 * MB);
                Assert.True(b.IsOverflow);
                Assert.Equal(B1Palette.Error, b.FillColor);
                var t = b.TrackBounds;
                int y = t.Y + t.Height / 2;
                using (var bmp = ShellFixture.Draw(b))
                {
                    Assert.Equal(B1Palette.Error.ToArgb(), bmp.GetPixel(t.X + 2, y).ToArgb());
                    Assert.Equal(B1Palette.Error.ToArgb(), bmp.GetPixel(t.Right - 1, y).ToArgb());   // piste pleine, jamais au dela
                }
                b.SetUsage(100 * MB, 100 * MB);                                                       // 100 % pile : pas un depassement
                Assert.False(b.IsOverflow);
            }
        }

        [StaFact]
        public void StorageBarTextShowsUsedAndTotal()
        {
            var ui = Thread.CurrentThread.CurrentUICulture;
            var cu = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                Form f; var b = Bar(out f);
                using (f)
                {
                    b.SetUsage(6246 * MB, 9626 * MB);
                    Assert.Equal("6.1 / 9.4 GB", b.UsageText);
                    b.SetUsage(300 * MB, 512 * MB);
                    Assert.Equal("300 / 512 MB", b.UsageText);
                    b.SetUsage(0, 0);
                    Assert.Equal("\u2014", b.UsageText);                  // capacite inconnue
                    Assert.True(b.IsUnknown);
                    Assert.False(b.IsOverflow);
                    Assert.NotEqual(B1Palette.Error, b.FillColor);
                    var t = b.TrackBounds;
                    using (var bmp = ShellFixture.Draw(b))
                        Assert.Equal(B1Palette.Rule.ToArgb(), bmp.GetPixel(t.X + 2, t.Y + t.Height / 2).ToArgb());   // piste vide
                }
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Form f2; var b2 = Bar(out f2);
                using (f2)
                {
                    b2.SetUsage(6246 * MB, 9626 * MB);
                    // le satellite fr-FR est dans languages\ (pas dans le dossier des tests) : on verifie le format des nombres
                    Assert.Equal("6,1 / 9,4 " + com.clusterrr.hakchi_gui.Properties.Resources.StorageUnitGB, b2.UsageText);
                }
            }
            finally { Thread.CurrentThread.CurrentUICulture = ui; Thread.CurrentThread.CurrentCulture = cu; }
        }
    }
}
