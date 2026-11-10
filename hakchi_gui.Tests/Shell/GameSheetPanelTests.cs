using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Properties;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using hakchi_gui.Tests.Grid;
using Xunit;

namespace hakchi_gui.Tests.Shell
{
    [Collection("B1Static")]
    public class GameSheetPanelTests
    {
        private static GameSheetPanel Sheet(out Form f)
        {
            HakchiEnv.Init();
            System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            var p = new GameSheetPanel { Bounds = new Rectangle(0, 0, 320, 700) };
            f = ShellFixture.Host(p, new Size(320, 700));
            return p;
        }

        private static string Field(GameSheetPanel p, string label)
        {
            foreach (var kv in p.Fields) if (kv.Key == label) return kv.Value;
            return null;
        }

        [StaFact]
        public void SheetShowsCoverSource()
        {
            Form f; var p = Sheet(out f);
            using (f)
            {
                var app = new TestApp("Super Metroid", null, true);
                app.Metadata.CoverSource = "ScreenScraper · EUR";
                p.Show(app);
                Assert.Equal("ScreenScraper · EUR", p.CoverSourceText);
                Assert.Equal("Super Metroid", p.TitleText);

                var app2 = new TestApp("Sans source", null, false);
                app2.Metadata.CoverSource = null;
                p.Show(app2);
                Assert.Equal(Resources.SheetCoverSourceUnknown, p.CoverSourceText);
            }
        }

        [StaFact]
        public void SheetShowsFieldsAndOmitsUnknownOnes()
        {
            Form f; var p = Sheet(out f);
            using (f)
            {
                var app = new TestApp("Super Metroid", null, true);
                app.Desktop.ReleaseDate = "1994-03-19";
                app.Desktop.Players = 2;
                app.Desktop.Publisher = "NINTENDO";
                app.Desktop.Genre = "Action";
                app.Metadata.Core = "canoe";
                p.Show(app);
                Assert.Equal("1994", p.YearText);
                Assert.Equal("2", Field(p, Resources.SheetPlayers));
                Assert.Equal("NINTENDO", Field(p, Resources.SheetPublisher));
                Assert.Equal("Action", Field(p, Resources.SheetGenre));
                Assert.Equal("canoe", Field(p, Resources.SheetCore));
                Assert.Contains("1994", p.MetaText);
                Assert.Contains("Super Nintendo", p.MetaText);

                // valeurs par defaut de hakchi = inconnu : lignes absentes, pas de tiret ni de valeur inventee
                var bare = new TestApp("Bare", null, false, "");
                bare.Metadata.Core = "";
                p.Show(bare);
                Assert.Equal("", p.YearText);
                Assert.Null(Field(p, Resources.SheetPublisher));
                Assert.Null(Field(p, Resources.SheetGenre));
                Assert.Null(Field(p, Resources.SheetCore));
                Assert.NotNull(Field(p, Resources.SheetPlayers));
            }
        }

        private sealed class BareApp : NesApplication
        {
            public BareApp() { desktop = null; Metadata = null; }
            public override string Name { get { return "Bare"; } set { } }
        }

        [StaFact]
        public void SheetToleratesMissingDesktopAndMetadata()
        {
            Form f; var p = Sheet(out f);
            using (f)
            {
                p.Show(new BareApp());
                Assert.Equal("Bare", p.TitleText);
                Assert.Equal(Resources.SheetCoverSourceUnknown, p.CoverSourceText);
                Assert.Empty(p.Fields);
                Assert.Equal("", p.YearText);
                Assert.True(p.HeaderHeight > 0);
            }
        }

        [StaFact]
        public void SheetChangeLinkRaisesEvent()
        {
            Form f; var p = Sheet(out f);
            using (f)
            {
                int n = 0;
                p.ChangeCoverRequested += (s, e) => n++;
                ShellFixture.Click(p, new Point(5, 5));
                Assert.Equal(0, n);                                  // rien sans jeu
                p.Show(new TestApp("Jeu", null, true));
                ShellFixture.Click(p, new Point(5, 5));
                Assert.Equal(0, n);
                var r = p.ChangeLinkBounds;
                Assert.False(r.IsEmpty);
                ShellFixture.Click(p, ShellFixture.Center(r));
                Assert.Equal(1, n);
            }
        }

        [StaFact]
        public void SheetKeepsChildrenBelowHeader()
        {
            Form f; var p = Sheet(out f);
            using (f)
            {
                var child = new Panel { Dock = DockStyle.Fill };
                p.Controls.Add(child);
                p.Show(new TestApp("Un jeu", null, true));
                p.PerformLayout();
                Assert.True(p.HeaderHeight > 200);
                Assert.Equal(p.HeaderHeight, child.Top);
                Assert.Equal(p.Height, child.Bottom);

                p.Show(null);
                p.PerformLayout();
                Assert.Equal(0, p.HeaderHeight);
                Assert.Equal(0, child.Top);
            }
        }

        [StaFact]
        public void SheetPaintsCoverInsideItsRectangle()
        {
            Form f; var p = Sheet(out f);
            using (f)
            {
                var app = TestApp.WithCover("Rouge", Color.FromArgb(0xD9, 0x77, 0x6A));
                p.Show(app);
                using (ShellFixture.Draw(p)) { }                       // demande la vignette
                for (int i = 0; i < 200 && !p.CoverReady; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                Assert.True(p.CoverReady);
                using (var bmp = ShellFixture.Draw(p))
                {
                    var c = ShellFixture.Center(p.CoverBounds);
                    var px = bmp.GetPixel(c.X, c.Y);
                    Assert.True(px.R > 150 && px.G < 160, "jaquette visible, pixel " + px);
                    Assert.Equal(B1Palette.Slate.ToArgb(), bmp.GetPixel(p.CoverBounds.Right + 3, c.Y).ToArgb());
                }
            }
        }
    }
}
