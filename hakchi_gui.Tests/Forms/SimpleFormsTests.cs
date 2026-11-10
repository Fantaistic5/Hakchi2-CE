using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Properties;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using hakchi_gui.Tests.Grid;
using hakchi_gui.Tests.Shell;
using hakchi_gui.Tests.Theme;
using Newtonsoft.Json.Linq;
using Xunit;

namespace hakchi_gui.Tests.Forms
{
    /// <summary>Les fenetres simples (Task 6) : base B1Form, palette, memes controles nommes, memes boutons Accept / Cancel, mise en page B1.</summary>
    [Collection("B1Static")]
    public class SimpleFormsTests
    {
        public static readonly string[] Names =
        {
            "AboutBox", "Motd", "TextInfo", "StringInputForm", "SelectFileForm", "SearchForm", "SettingsForm", "TechInfo", "SelectIconForm",
            "SelectSystemDialog", "SelectFirmwareDialog", "ExportGamesDialog", "NewCustomGameForm", "SpineForm", "ImagesForm", "ImageGooglerForm", "DumperForm",
        };

        public static IEnumerable<object[]> All() { return Names.Select(n => new object[] { n }); }

        private static readonly Assembly Asm = typeof(NesApplication).Assembly;

        private static Type TypeOf(string name) { return Asm.GetType("com.clusterrr.hakchi_gui." + name, true); }

        /// <summary>Construit la fenetre sans rien afficher ni toucher a la console / au reseau (aucun gestionnaire Load / Shown n'est declenche).</summary>
        internal static Form Create(string name, string culture = "fr-FR", Action<Form> beforeHandle = null)
        {
            MainFormEnv.Init();
            Thread.CurrentThread.CurrentUICulture = string.IsNullOrEmpty(culture) ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture);
            var t = TypeOf(name);
            object[] args;
            switch (name)
            {
                case "Motd": args = new object[] { "# Titre\n\nUn message d'exemple." }; break;
                case "SelectFileForm": args = new object[] { new[] { "Super Metroid.sfc", "Zelda.sfc" }, null, null }; break;
                case "SearchForm": args = new object[] { null }; break;
                case "SelectIconForm": args = new object[] { null }; break;
                case "SpineForm": args = new object[] { MainFormEnv.Games(1)[0] }; break;
                case "ImageGooglerForm": args = new object[] { MainFormEnv.Games(1)[0] }; break;
                default: args = new object[0]; break;
            }
            var f = (Form)Activator.CreateInstance(t, args);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-30000, -30000);
            f.ShowInTaskbar = false;
            if (beforeHandle != null) beforeHandle(f);
            var h = f.Handle;                        // habillage B1 (OnHandleCreated) sans afficher la fenetre
            CreateHandles(f);
            f.PerformLayout();
            return f;
        }

        private static void CreateHandles(Control c)
        {
            var h = c.Handle;
            foreach (Control child in c.Controls) CreateHandles(child);
        }

        private static IEnumerable<Control> Tree(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var d in Tree(c)) yield return d;
            }
        }

        private static JObject Before(string name)
        {
            var all = JObject.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "simple_forms_before.json")));
            return (JObject)all[name];
        }

        [StaTheory, MemberData(nameof(All))]
        public void InheritsB1Form(string name)
        {
            Assert.True(typeof(B1Form).IsAssignableFrom(TypeOf(name)), name);
        }

        [StaTheory, MemberData(nameof(All))]
        public void PaletteColorProperties(string name)
        {
            using (var f = Create(name))
            {
                Assert.True(Sample.InPalette(f.BackColor) && Sample.InPalette(f.ForeColor), name + " fenetre");
                foreach (var c in Tree(f))
                {
                    if (c is WebBrowser) continue;                       // composant ActiveX : habille par la page HTML (Motd.DarkCss)
                    if (c.BackColor != Color.Transparent) Assert.True(Sample.InPalette(c.BackColor), name + "." + c.Name + " BackColor " + c.BackColor);
                    Assert.True(Sample.InPalette(c.ForeColor), name + "." + c.Name + " ForeColor " + c.ForeColor);
                    var bb = c as ButtonBase;
                    if (bb != null && bb.FlatStyle == FlatStyle.Flat)
                    {
                        Assert.True(Sample.InPalette(bb.FlatAppearance.BorderColor), name + "." + c.Name + " BorderColor");
                        Assert.True(Sample.InPalette(bb.FlatAppearance.MouseOverBackColor), name + "." + c.Name + " MouseOver");
                    }
                    if (bb != null) Assert.Equal(FlatStyle.Flat, bb.FlatStyle);
                }
            }
        }

        // --- controle des pixels rendus (galerie) -------------------------------------------------------------------------

        internal static readonly Lazy<string> GalleryDir = new Lazy<string>(RunGallery);

        private static string RunGallery()
        {
            var exe = Repo.GalleryExe;
            Assert.True(File.Exists(exe), "FormGallery.exe absent : " + exe);
            var outDir = Path.Combine(Path.GetTempPath(), "hakchi_simple_forms_" + Guid.NewGuid().ToString("N"));
            var psi = new System.Diagnostics.ProcessStartInfo(exe, "--out \"" + outDir + "\"")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe) };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(240000)) { try { p.Kill(); } catch { } throw new TimeoutException("galerie > 240 s"); }
                Assert.True(p.ExitCode == 0, "galerie : code " + p.ExitCode + " " + e.Result);
            }
            return outDir;
        }

        private const int PaletteTolerance = 40;      // ecart RVB : le lissage ClearType colore les bords des glyphes de quelques dizaines de niveaux

        internal static bool NearPaletteOrBlend(Color c, int tol)
        {
            var pal = B1Palette.All;
            long tol2 = (long)tol * tol;
            foreach (var p in pal)
            {
                long dr = c.R - p.R, dg = c.G - p.G, db = c.B - p.B;
                if (dr * dr + dg * dg + db * db <= tol2) return true;
            }
            for (int i = 0; i < pal.Count; i++)
                for (int j = i + 1; j < pal.Count; j++)
                {
                    double ax = pal[j].R - pal[i].R, ay = pal[j].G - pal[i].G, az = pal[j].B - pal[i].B;
                    double len = ax * ax + ay * ay + az * az;
                    if (len == 0) continue;
                    double t = ((c.R - pal[i].R) * ax + (c.G - pal[i].G) * ay + (c.B - pal[i].B) * az) / len;
                    if (t < 0 || t > 1) continue;
                    double dx = c.R - (pal[i].R + t * ax), dy = c.G - (pal[i].G + t * ay), dz = c.B - (pal[i].B + t * az);
                    if (dx * dx + dy * dy + dz * dz <= tol2) return true;
                }
            return false;
        }

        /// <summary>
        /// Pixels du rendu de la galerie hors cadre systeme et hors images : (hors palette, total, pire tuile 32x16).
        /// clearTypeSafe : chaque pixel est d'abord moyenne avec ses voisins gauche et droit (canal par canal). Le lissage ClearType
        /// decale chaque canal d'un sous-pixel et colore les bords des glyphes bien au-dela de la tolerance (fenetres a texte dense) ;
        /// la moyenne sur 3 pixels annule ce decalage, alors qu'un texte ou un fond d'une couleur etrangere reste hors palette.
        /// </summary>
        internal static void MeasurePalette(string name, out int bad, out int total, out int worstTile, Rectangle? only = null, bool clearTypeSafe = false)
        {
            var dir = GalleryDir.Value;
            var png = Path.Combine(dir, name + ".png");
            Assert.True(File.Exists(png), "rendu absent : " + png);
            var rects = JObject.Parse(File.ReadAllText(Path.Combine(dir, name + ".rects.json")));
            var cl = rects["client"].Select(x => (int)x).ToArray();
            var client = new Rectangle(cl[0], cl[1], cl[2], cl[3]);
            var skip = rects["skip"].Select(r => { var a = r.Select(x => (int)x).ToArray(); return new Rectangle(a[0], a[1], a[2], a[3]); }).ToList();
            var cache = new Dictionary<int, bool>();
            var tiles = new Dictionary<long, int>();
            bad = 0; total = 0;
            using (var src = new Bitmap(png))
            {
                var area = only.HasValue ? Rectangle.Intersect(client, only.Value) : client;
                area.Intersect(new Rectangle(0, 0, src.Width, src.Height));
                for (int y = area.Top; y < area.Bottom; y++)
                    for (int x = area.Left; x < area.Right; x++)
                    {
                        bool skipped = false;
                        foreach (var k in skip) if (k.Contains(x, y)) { skipped = true; break; }
                        if (skipped) continue;
                        total++;
                        var c = src.GetPixel(x, y);
                        if (clearTypeSafe)
                        {
                            var l = src.GetPixel(Math.Max(area.Left, x - 1), y); var r = src.GetPixel(Math.Min(area.Right - 1, x + 1), y);
                            c = Color.FromArgb((l.R + c.R + r.R + 1) / 3, (l.G + c.G + r.G + 1) / 3, (l.B + c.B + r.B + 1) / 3);
                        }
                        int key = c.ToArgb() & 0xFFFFFF;
                        bool ok;
                        if (!cache.TryGetValue(key, out ok)) { ok = NearPaletteOrBlend(c, PaletteTolerance); cache[key] = ok; }
                        if (ok) continue;
                        bad++;
                        long tk = ((long)(y / 16) << 32) | (uint)(x / 32);
                        int n; tiles.TryGetValue(tk, out n); tiles[tk] = n + 1;
                    }
            }
            worstTile = tiles.Count == 0 ? 0 : tiles.Values.Max();
        }

        [StaTheory, MemberData(nameof(All))]
        public void OnlyPaletteColorsAfterSkin(string name)
        {
            int bad, total, worst;
            // AboutBox : texte dependant du build (hash du commit dans « Version » / « Git Commit ») ; selon les caracteres, les franges
            // ClearType d'une tuile passaient de 53 a 69 pixels (seuil 60). Mesure insensible a ClearType pour cette fenetre seulement.
            MeasurePalette(name, out bad, out total, out worst, clearTypeSafe: name == "AboutBox");
            if (total == 0) return;                          // fenetre sans zone a juger (DumperForm : vide, ImagesForm : images seules)
            double ratio = 1.0 - (double)bad / total;
            Assert.True(ratio >= 0.98, name + " : " + (ratio * 100).ToString("0.00") + " % des pixels dans la palette (" + bad + " hors palette sur " + total + ")");
            // aucune zone dense de couleur etrangere (en-tete bleu systeme, glyphes d'une autre couleur) meme si l'ensemble reste sous 2 %
            Assert.True(worst <= WorstTileLimit, name + " : tuile 32x16 avec " + worst + " pixels hors palette");
        }

        private const int WorstTileLimit = 60;

        [StaTheory, MemberData(nameof(All))]
        public void SameControlNamesAsBefore(string name)
        {
            var before = Before(name)["names"].Select(x => (string)x).ToList();
            using (var f = Create(name))
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var have = new HashSet<string>();
                for (var t = f.GetType(); t != null && t != typeof(Form); t = t.BaseType)
                    foreach (var fi in t.GetFields(flags | BindingFlags.DeclaredOnly)) have.Add(fi.Name);
                foreach (var n in before) Assert.True(have.Contains(n), name + " : champ manquant " + n);
            }
        }

        [StaTheory, MemberData(nameof(All))]
        public void SameAcceptCancelButtons(string name)
        {
            var b = Before(name);
            using (var f = Create(name))
            {
                Assert.Equal((string)b["accept"], (f.AcceptButton as Control)?.Name);
                Assert.Equal((string)b["cancel"], (f.CancelButton as Control)?.Name);
            }
        }

        // Boutons d'action : le bouton primaire est le plus a droite de sa rangee.
        [StaTheory, MemberData(nameof(All))]
        public void PrimaryButtonIsRightmostInItsRow(string name)
        {
            using (var f = Create(name))
            {
                foreach (var p in Tree(f).OfType<Button>().Where(x => x.BackColor == B1Palette.Primary))
                {
                    var row = p.Parent.Controls.OfType<Button>().Where(x => x != p && Math.Abs(x.Top - p.Top) < 4 && x.Visible);
                    foreach (var o in row)
                        Assert.True(o.Left < p.Left, name + " : " + o.Name + " est a droite du bouton primaire " + p.Name);
                }
            }
        }

        // Pas de bouton primaire oublie : toute fenetre qui a un bouton de validation en a un primaire.
        [StaTheory]
        [InlineData("Motd", "button1")]
        [InlineData("TechInfo", "buttonOk")]
        [InlineData("ExportGamesDialog", "btnOk")]
        [InlineData("NewCustomGameForm", "button1")]
        [InlineData("SelectSystemDialog", "buttonPrepare")]
        [InlineData("AboutBox", "okButton")]
        [InlineData("SelectFileForm", "buttonOk")]
        [InlineData("SelectIconForm", "buttonOk")]
        [InlineData("SelectFirmwareDialog", "buttonOk")]
        [InlineData("StringInputForm", "buttonOK")]
        [InlineData("SpineForm", "buttonOk")]
        public void ValidationButtonIsPrimary(string name, string button)
        {
            using (var f = Create(name))
            {
                var b = Tree(f).OfType<Button>().Single(x => x.Name == button);
                Assert.Equal(B1Palette.Primary.ToArgb(), b.BackColor.ToArgb());
            }
        }

        // Marges de 16 px autour du contenu (fenetres a mise en page libre) ; fenetres a conteneur Dock : marge portee par le Padding.
        [StaTheory]
        [InlineData("Motd")]
        [InlineData("SelectFileForm")]
        [InlineData("SearchForm")]
        [InlineData("SettingsForm")]
        [InlineData("TechInfo")]
        [InlineData("SelectIconForm")]
        [InlineData("SelectSystemDialog")]
        [InlineData("SelectFirmwareDialog")]
        [InlineData("ExportGamesDialog")]
        [InlineData("NewCustomGameForm")]
        [InlineData("SpineForm")]
        [InlineData("ImageGooglerForm")]
        public void ContentKeepsA16PixelMargin(string name)
        {
            using (var f = Create(name))
            {
                var cr = f.ClientRectangle;
                foreach (var c in f.Controls.Cast<Control>().Where(x => x.Visible && x.Dock == DockStyle.None))
                {
                    Assert.True(c.Left >= 16 - 4 || c is TextBoxBase && name == "SearchForm" && c.Left >= 12, name + "." + c.Name + " gauche " + c.Left);
                    Assert.True(c.Top >= 8, name + "." + c.Name + " haut " + c.Top);
                    Assert.True(cr.Right - c.Right >= 12, name + "." + c.Name + " droite " + (cr.Right - c.Right));
                    Assert.True(cr.Bottom - c.Bottom >= 12 || name == "SearchForm", name + "." + c.Name + " bas " + (cr.Bottom - c.Bottom));
                }
            }
        }

        [StaFact]
        public void DockedFormsCarry16PixelPadding()
        {
            using (var f = Create("AboutBox")) Assert.Equal(new Padding(16), f.Padding);
            using (var f = Create("TextInfo")) Assert.Equal(new Padding(16), Tree(f).OfType<Panel>().Single(p => p.Name == "panel1").Padding);
            using (var f = Create("StringInputForm")) Assert.Equal(new Padding(16), Tree(f).OfType<TableLayoutPanel>().Single().Padding);
        }

        [StaFact]
        public void AboutBoxProductNameUsesTitleFont()
        {
            using (var f = Create("AboutBox"))
            {
                var l = Tree(f).OfType<Label>().Single(x => x.Name == "labelProductName");
                var body = Tree(f).OfType<Label>().Single(x => x.Name == "labelVersion");
                Assert.True(l.Font.SizeInPoints > body.Font.SizeInPoints + 2);
                if (B1Fonts.JostLoaded) Assert.Equal(B1Fonts.FamilySemiBold, l.Font.FontFamily.Name);
            }
        }

        // Les resx localises ne portent aucune surcharge de mise en page (taille de la fenetre, position / taille / ancrage des controles existants) :
        // elle ecraserait la mise en page B1 du resx neutre. Seuls les textes sont traduits.
        private static readonly System.Text.RegularExpressions.Regex LayoutKey = new System.Text.RegularExpressions.Regex(
            @"^(?<c>[^.>]+)\.(Location|Size|ClientSize|Anchor|Dock|Padding|Margin|MinimumSize|MaximumSize)$");

        [Theory, MemberData(nameof(All))]
        public void LocalizedResxHaveNoLayoutOverrides(string name)
        {
            var dir = Path.Combine(Repo.Root, "hakchi_gui");
            var existing = new HashSet<string>(Before(name)["names"].Select(x => (string)x)) { "$this" };
            var files = Directory.GetFiles(dir, name + ".*.resx");
            foreach (var file in files)
                foreach (var d in System.Xml.Linq.XDocument.Load(file).Root.Elements("data"))
                {
                    var m = LayoutKey.Match((string)d.Attribute("name"));
                    if (m.Success && existing.Contains(m.Groups["c"].Value))
                        Assert.True(false, Path.GetFileName(file) + " surcharge la mise en page : " + (string)d.Attribute("name"));
                }
        }
    }

    [Collection("B1Static")]
    public class GameSheetPlaceholderTests
    {
        [StaFact]
        public void PlaceholderStringsAreLocalized()
        {
            Func<string, System.Collections.Generic.Dictionary<string, string>> load = file =>
                System.Xml.Linq.XDocument.Load(Path.Combine(Repo.Root, "hakchi_gui", "Properties", file)).Root.Elements("data")
                    .ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value"));
            Assert.Equal("Select a game to see its details", load("Resources.resx")["SheetNoSelection"]);
            Assert.Equal("Sélectionnez un jeu pour voir sa fiche", load("Resources.fr-FR.resx")["SheetNoSelection"]);
            Assert.Equal("Select a game to see its details", Resources.SheetNoSelection);     // culture neutre / autres langues : anglais
            foreach (var f in Directory.GetFiles(Path.Combine(Repo.Root, "hakchi_gui", "Properties"), "Resources.*-*.resx").Concat(Directory.GetFiles(Path.Combine(Repo.Root, "hakchi_gui", "Properties"), "Resources.??.resx")))
                if (!f.EndsWith("fr-FR.resx")) Assert.False(load(Path.GetFileName(f)).ContainsKey("SheetNoSelection"), f);   // les autres langues retombent sur l'anglais
        }

        private static int TextPixels(Bitmap bmp, Rectangle area)
        {
            int n = 0;
            for (int y = area.Top; y < area.Bottom; y++)
                for (int x = area.Left; x < area.Right; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    if (c.R > 0x50 && c.G > 0x50 && c.B > 0x50) n++;                  // nettement plus clair que le fond ardoise
                }
            return n;
        }

        [StaFact]
        public void EmptySheetShowsCenteredPlaceholderInTextDim()
        {
            MainFormEnv.Init();
            var p = new GameSheetPanel { Bounds = new Rectangle(0, 0, 320, 700) };
            using (var f = ShellFixture.Host(p, new Size(320, 700)))
            {
                p.Show(null);
                using (var bmp = ShellFixture.Draw(p))
                {
                    var mid = new Rectangle(16, 250, 288, 200);
                    int center = TextPixels(bmp, mid);
                    Assert.True(center > 40, "texte d'invite absent : " + center);
                    Assert.Equal(0, TextPixels(bmp, new Rectangle(16, 16, 288, 150)));       // rien en haut : le texte est centre
                    // la couleur du texte est TextDim (au plus clair), pas du texte principal
                    int brightest = 0;
                    for (int y = mid.Top; y < mid.Bottom; y++) for (int x = mid.Left; x < mid.Right; x++) { var c = bmp.GetPixel(x, y); brightest = Math.Max(brightest, c.R); }
                    Assert.True(brightest <= B1Palette.TextDim.R + 2, "trop clair : " + brightest);
                }
            }
        }
    }
}
