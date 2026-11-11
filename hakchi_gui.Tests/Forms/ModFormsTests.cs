using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Hmod.Controls;
using com.clusterrr.hakchi_gui.ModHub;
using com.clusterrr.hakchi_gui.ModHub.Controls;
using com.clusterrr.hakchi_gui.Properties;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;
using hakchi_gui.Tests.Shell;
using hakchi_gui.Tests.Theme;
using ModInstallPath;
using Newtonsoft.Json.Linq;
using Xunit;
using RepositoryT = com.clusterrr.hakchi_gui.ModHub.Repository.Repository;

namespace hakchi_gui.Tests.Forms
{
    /// <summary>
    /// ModHub et selection des mods (Task 9) : APPARENCE SEULEMENT. Aucun reseau, aucune console : les fenetres sont construites
    /// « comme le concepteur » (sans constructeur applicatif : ni Hmod.GetMods, ni ConfigIni), le depot est construit en memoire
    /// (jamais Repository.Load), les readmes d'exemple n'ont aucune ressource distante. Rien n'est installe ni desinstalle :
    /// le chemin d'installation est verifie par lecture de l'IL, jamais execute.
    /// </summary>
    [Collection("B1Static")]
    public class ModFormsTests
    {
        // nom de type sous com.clusterrr.hakchi_gui
        public static readonly string[] Names = { "ModHub.ModHubForm", "ModHub.Repository.RepoManagementForm", "Hmod.SelectForm" };

        public static readonly string[] UserControls =
        {
            "ModHub.Controls.ModHubTabControl", "ModHub.Controls.ModInfoControl",
            "Hmod.Controls.ReadmeControl", "Hmod.Controls.BrowserReadmeControl", "Hmod.Controls.TextReadmeControl",
        };

        public static IEnumerable<object[]> All() { return Names.Select(n => new object[] { n }); }
        public static IEnumerable<object[]> AllWithControls() { return Names.Concat(UserControls).Select(n => new object[] { n }); }

        private static readonly Assembly Asm = typeof(NesApplication).Assembly;

        private static Type TypeOf(string name) { return Asm.GetType("com.clusterrr.hakchi_gui." + name, true); }

        private static string Short(string name) { return name.Substring(name.LastIndexOf('.') + 1); }

        private static JObject Before(string name)
        {
            return (JObject)JObject.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "mod_forms_before.json")))[name];
        }

        // --- donnees d'exemple (en memoire, sans reseau) -------------------------------------------------------------------

        private static string ModReadme(string name, string creator, string version, string category, string system, string body)
        {
            return "---\nName: " + name + "\nCreator: " + creator + "\nVersion: " + version + "\nCategory: " + category +
                   (system != null ? "\nEmulated System: " + system : "") + "\n---\n" + body;
        }

        internal static RepositoryT SampleRepo()
        {
            var repo = new RepositoryT("https://example.invalid/repo/");
            typeof(RepositoryT).GetProperty("Readme").SetValue(repo, "# Depot\n\nAccueil.", null);
            repo.Items.Add(new RepositoryT.Item("retroarch.hmod", ModReadme("RetroArch", "Cluster", "1.9.0", "Emulators", "Multi-system", "Coeurs RetroArch.\n\n- NES\n- SNES\n"), true));
            repo.Items.Add(new RepositoryT.Item("snes9x.hmod", ModReadme("Snes9x 2010", "Snes9x team", "1.53", "Emulators", "Super Nintendo", "Coeur Snes9x."), true));
            repo.Items.Add(new RepositoryT.Item("advanced_music_hack.hmod", ModReadme("Hakchi Advanced Music Hack", "KMFDManic", "2.1", "UI", null, "Musique de fond."), true));
            return repo;
        }

        /// <summary>
        /// ModHubForm « comme le concepteur » puis remplie comme par MainForm (LoadData avant l'affichage), handle cree ensuite :
        /// l'habillage B1 couvre les onglets ajoutes par LoadData, comme a l'ouverture reelle (OnLoad).
        /// </summary>
        internal static Form CreateHub()
        {
            MainFormEnv.Init();
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            var t = typeof(ModHubForm);
            var f = (ModHubForm)FormatterServices.GetUninitializedObject(t);
            typeof(B1Form).GetConstructor(Type.EmptyTypes).Invoke(f, null);
            t.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, null);
            t.GetField("installedMods", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(f, new List<com.clusterrr.hakchi_gui.Hmod.Hmod>());
            f.LoadData(SampleRepo());
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-30000, -30000);
            f.ShowInTaskbar = false;
            CreateHandles(f);
            f.PerformLayout();
            return f;
        }

        private static Form Create(string name)
        {
            if (name == "ModHub.ModHubForm") return CreateHub();
            return TaskFormsTests.CreateDesigned(name);
        }

        private static Control CreateAny(string name)
        {
            if (Names.Contains(name)) return Create(name);
            MainFormEnv.Init();
            var c = (Control)Activator.CreateInstance(TypeOf(name));
            CreateHandles(c);
            return c;
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

        private static T Field<T>(object o, string name) where T : class
        {
            for (var t = o.GetType(); t != null; t = t.BaseType)
            {
                var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (fi != null) return fi.GetValue(o) as T;
            }
            return null;
        }

        // --- les trois tests de la Task 6 -----------------------------------------------------------------------------------

        [StaTheory, MemberData(nameof(All))]
        public void InheritsB1Form(string name)
        {
            Assert.True(typeof(B1Form).IsAssignableFrom(TypeOf(name)), name);
        }

        // Rendu de la galerie (ModHubForm : depot d'exemple, onglet d'une categorie, liste groupee, carte et readme ; SelectForm : mods
        // coches / deja installes, readme). Les pages HTML (WebBrowser) sont exclues de la mesure : voir ReadmeCssInjectedOnce.
        [StaTheory, MemberData(nameof(All))]
        public void OnlyPaletteColorsAfterSkin(string name)
        {
            int bad, total, worst;
            SimpleFormsTests.MeasurePalette(Short(name), out bad, out total, out worst, clearTypeSafe: true);
            Assert.True(total > 0, name + " : rien a mesurer");
            double ratio = 1.0 - (double)bad / total;
            Assert.True(ratio >= 0.98, name + " : " + (ratio * 100).ToString("0.00") + " % des pixels dans la palette (" + bad + " hors palette sur " + total + ")");
            Assert.True(worst <= 60, name + " : tuile 32x16 avec " + worst + " pixels hors palette");
        }

        [StaTheory, MemberData(nameof(AllWithControls))]
        public void SameControlNamesAsBefore(string name)
        {
            var before = Before(name)["names"].Select(x => (string)x).ToList();
            using (var c = CreateAny(name))
            {
                var have = new HashSet<string>();
                for (var t = c.GetType(); t != null && t != typeof(Form) && t != typeof(UserControl); t = t.BaseType)
                    foreach (var fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) have.Add(fi.Name);
                foreach (var n in before) Assert.True(have.Contains(n), name + " : champ manquant " + n);
            }
        }

        // --- fenetres -----------------------------------------------------------------------------------------------------

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

        [StaTheory, MemberData(nameof(All))]
        public void PaletteColorProperties(string name)
        {
            using (var f = Create(name))
            {
                Assert.True(Sample.InPalette(f.BackColor) && Sample.InPalette(f.ForeColor), name + " fenetre");
                foreach (var c in Tree(f))
                {
                    if (c is WebBrowser) continue;                       // composant ActiveX : habille par la page HTML (BrowserReadmeControl.B1Css)
                    // TabControl ignore BackColor (toujours SystemColors.Control) : onglets dessines par B1Skin, pages verifiees ici (cf. B1SkinTests)
                    if (c.BackColor != Color.Transparent && !(c is TabControl)) Assert.True(Sample.InPalette(c.BackColor), name + "." + c.Name + " BackColor " + c.BackColor);
                    Assert.True(Sample.InPalette(c.ForeColor), name + "." + c.Name + " ForeColor " + c.ForeColor);
                    var bb = c as ButtonBase;
                    if (bb != null) Assert.Equal(FlatStyle.Flat, bb.FlatStyle);
                    var info = c as ModInfoControl;
                    if (info != null)
                    {
                        Assert.Null(info.infoStrips);                    // plus de bandes de pastilles de couleur
                        Assert.True(Sample.InPalette(info.textColor), name + ".modInfo textColor " + info.textColor);
                    }
                }
            }
        }

        [StaTheory, MemberData(nameof(All))]
        public void PrimaryButtonIsRightmostInItsRow(string name)
        {
            using (var f = Create(name))
                foreach (var p in Tree(f).OfType<Button>().Where(x => x.BackColor == B1Palette.Primary))
                    foreach (var o in p.Parent.Controls.OfType<Button>().Where(x => x != p && Math.Abs(x.Top - p.Top) < 4))
                        Assert.True(o.Left < p.Left, name + " : " + o.Name + " est a droite du bouton primaire " + p.Name);
        }

        [StaTheory]
        [InlineData("Hmod.SelectForm", "buttonOk")]
        [InlineData("ModHub.Repository.RepoManagementForm", "addRepoButton")]
        [InlineData("ModHub.ModHubForm", "modDownloadInstallButton")]
        public void ValidationButtonIsPrimary(string name, string button)
        {
            using (var f = Create(name))
            {
                var buttons = Tree(f).OfType<Button>().Where(x => x.Name == button).ToList();
                Assert.NotEmpty(buttons);
                foreach (var b in buttons) Assert.Equal(B1Palette.Primary.ToArgb(), b.BackColor.ToArgb());
            }
        }

        // Le bouton OK de la selection des mods n'occupe plus toute la largeur : a droite, marge de 16 px.
        [StaFact]
        public void SelectFormOkSitsBottomRightWith16PxMargin()
        {
            using (var f = Create("Hmod.SelectForm"))
            {
                var ok = Tree(f).OfType<Button>().Single(x => x.Name == "buttonOk");
                Assert.Equal(16, f.ClientSize.Width - ok.Right);
                Assert.Equal(16, f.ClientSize.Height - ok.Bottom);
                Assert.Equal(AnchorStyles.Bottom | AnchorStyles.Right, ok.Anchor);
                var split = Tree(f).OfType<SplitContainer>().Single();
                Assert.Equal(16, split.Left);
                Assert.Equal(16, split.Top);
                Assert.Equal(16, f.ClientSize.Width - split.Right);
            }
        }

        // Boutons a icone de la gestion des depots : pictogrammes vectoriels B1 (plus d'image en couleurs), memes gestionnaires.
        [StaFact]
        public void RepoButtonsUseB1Glyphs()
        {
            using (var f = Create("ModHub.Repository.RepoManagementForm"))
            {
                var expected = new Dictionary<string, B1Glyph>
                {
                    { "moveUpButton", B1Glyph.ArrowUp }, { "moveDownButton", B1Glyph.ArrowDown }, { "editButton", B1Glyph.Edit }, { "deleteButton", B1Glyph.Remove },
                };
                foreach (var p in expected)
                {
                    var b = Tree(f).OfType<Button>().Single(x => x.Name == p.Key);
                    Assert.Equal(p.Value, B1Skin.GlyphOf(b));
                    Assert.Null(b.BackgroundImage);
                    Assert.True(b.Height + b.Top <= b.Parent.ClientSize.Height, p.Key + " coupe par le bas de son panneau");
                }
            }
        }

        // --- carte du mod -------------------------------------------------------------------------------------------------

        [StaFact]
        public void ModInfoCardPaintsOnlyPalette()
        {
            MainFormEnv.Init();
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            using (var host = new Form { FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000) })
            {
                var info = new ModInfoControl { BackColor = B1Palette.RowActive, textColor = B1Palette.Text, Size = new Size(206, 316) };
                host.ClientSize = info.Size;
                host.Controls.Add(info);
                info.SetInfo("RetroArch", "Cluster", "1.9.0", "1.8.0");
                CreateHandles(host);
                using (var bmp = new Bitmap(info.Width, info.Height))
                {
                    info.DrawToBitmap(bmp, new Rectangle(0, 0, info.Width, info.Height));
                    Assert.Equal(B1Palette.Rule.ToArgb(), bmp.GetPixel(0, 0).ToArgb());               // filet de 1 px
                    Assert.Equal(B1Palette.RowActive.ToArgb(), bmp.GetPixel(2, 2).ToArgb());          // fond de carte
                    int bad = 0, text = 0, dim = 0;
                    for (int y = 0; y < bmp.Height; y++)
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            var c = bmp.GetPixel(x, y);
                            if (!SimpleFormsTests.NearPaletteOrBlend(c, 40)) bad++;
                            if (c.ToArgb() == B1Palette.Text.ToArgb()) text++;
                            if (c.ToArgb() == B1Palette.TextDim.ToArgb()) dim++;
                        }
                    Assert.True(bad <= bmp.Width * bmp.Height / 50, "pixels hors palette : " + bad);
                    Assert.True(text > 0, "aucune valeur peinte en Text");
                    Assert.True(dim > 0, "aucune etiquette peinte en TextDim");
                }
            }
        }

        // --- readme : feuille B1 injectee une fois, contenu inchange -------------------------------------------------------

        private static int Count(string s, string what)
        {
            int n = 0;
            for (int i = s.IndexOf(what, StringComparison.Ordinal); i >= 0; i = s.IndexOf(what, i + what.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        private static string Format(BrowserReadmeControl c, string name, HmodReadme r)
        {
            var m = typeof(BrowserReadmeControl).GetMethod("formatReadme", BindingFlags.Instance | BindingFlags.NonPublic);
            var args = new object[] { name, r };
            return (string)m.Invoke(c, args);
        }

        private static void AssertB1Page(string html, string expectedContent, Color back)
        {
            var css = BrowserReadmeControl.B1Css;
            Assert.Equal(1, Count(html, BrowserReadmeControl.B1CssMarker));
            Assert.Equal(1, Count(html, css));
            // aucun bloc <style> ajoute (CommonMark laisse passer le HTML brut d'un readme, comme avant : on ne compte que le gabarit en plus)
            Assert.Equal(Count(Resources.readmeTemplateHTML, "<style") + Count(expectedContent, "<style"), Count(html, "<style"));
            // feuille B1 dans le bloc <style> du gabarit, apres sa feuille (elle l'emporte a specificite egale)
            int at = html.IndexOf(css, StringComparison.Ordinal);
            Assert.True(at > html.IndexOf("<style", StringComparison.Ordinal) && at < html.IndexOf("</style>", StringComparison.Ordinal), "feuille B1 hors du bloc <style>");
            Assert.True(at > html.IndexOf(Resources.readmeTemplateCSS, StringComparison.Ordinal), "feuille B1 avant la feuille du gabarit");
            // sans la feuille B1, la page est exactement celle d'avant : meme gabarit, meme contenu (rien de l'utilisateur n'est retouche)
            var original = string.Format(Resources.readmeTemplateHTML, Resources.readmeTemplateCSS, expectedContent, "rgb(" + back.R + "," + back.G + "," + back.B + ")");
            Assert.Equal(original, html.Replace(css, ""));
        }

        [StaFact]
        public void ReadmeCssInjectedOnce()
        {
            MainFormEnv.Init();
            using (var c = new BrowserReadmeControl())
            {
                var hostile = "---\nName: X\nCreator: Moi\n---\n# Titre\n\nTexte avec </style><style>body{color:red}</style> et [un lien](https://example.invalid/).\n\n    code\n";
                var first = new HmodReadme(hostile, true);
                c.setReadme("Mon mod", first);
                Assert.Equal(B1Palette.Slate.ToArgb(), c.BackColor.ToArgb());
                AssertB1Page(c.LastHtml, Format(c, "Mon mod", first), c.BackColor);

                // pages suivantes : toujours une seule feuille (rien ne s'accumule), contenu de la nouvelle page
                var second = new HmodReadme("Texte brut\nsur deux lignes", false);
                c.setReadme(null, second);
                AssertB1Page(c.LastHtml, Format(c, null, second), c.BackColor);
                c.setReadme("Plusieurs", "- a\n- b", true);
                Assert.Equal(1, Count(c.LastHtml, BrowserReadmeControl.B1CssMarker));
                c.clear();
                AssertB1Page(c.LastHtml, "", c.BackColor);
            }
        }

        [Fact]
        public void ReadmeCssUsesPaletteAndNoNetwork()
        {
            var css = BrowserReadmeControl.B1Css;
            foreach (Match m in Regex.Matches(css, "#[0-9A-Fa-f]{6}\\b"))
            {
                var c = ColorTranslator.FromHtml(m.Value);
                Assert.True(Sample.InPalette(c), "couleur hors palette dans la feuille B1 : " + m.Value);
            }
            Assert.Contains("background-color: #1C1B22", css);                 // fond Slate
            Assert.Contains("color: #E4E2EA", css);                            // texte Text
            Assert.Contains("a, a:visited { color: #A79FD6", css);             // liens Lavender
            Assert.Contains("'B1 Jost', 'Segoe UI'", css);                     // Jost, repli Segoe UI
            Assert.Contains("data:application/x-font-ttf;base64,", css);       // Jost embarquee, pas de fichier ni d'URL
            Assert.DoesNotContain("http:", css);
            Assert.DoesNotContain("https:", css);
            Assert.DoesNotContain("@import", css);
        }

        [StaFact]
        public void TextReadmeUsesStandardSkin()
        {
            MainFormEnv.Init();
            using (var c = new TextReadmeControl())
            {
                var tb = Tree(c).OfType<TextBox>().Single();
                Assert.Equal(B1Palette.RowActive.ToArgb(), tb.BackColor.ToArgb());
                Assert.Equal(B1Palette.Text.ToArgb(), tb.ForeColor.ToArgb());
                Assert.Equal(B1Palette.Slate.ToArgb(), c.BackColor.ToArgb());
            }
        }

        // --- le chemin d'installation / desinstallation des mods ne change pas ----------------------------------------------

        // Empreinte IL (methodes appelees, chaines, lambdas comprises) des gestionnaires Installer / Desinstaller / Telecharger,
        // comparee a celle d'un hakchi.exe compile depuis snes-usa/theme-b1 (Data/mod_install_before.json, tools/ModInstallPath).
        [Fact]
        public void ModInstallPathUnchanged()
        {
            var before = JObject.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "mod_install_before.json")));
            Assert.Equal("snes-usa/theme-b1", (string)before["_base"]);
            RuntimeHelpers.RunModuleConstructor(Asm.ManifestModule.ModuleHandle);
            var roots = (JObject)before["roots"];
            Assert.Equal(IlCallees.Roots.Length, roots.Count);
            foreach (var r in IlCallees.Roots)
            {
                var key = r[0] + "::" + r[1];
                var b = (JObject)roots[key];
                Assert.True(b != null, key + " absent de la reference");
                List<string> callees, strings;
                IlCallees.Fingerprint(Asm, r[0], r[1], out callees, out strings);
                var expC = b["callees"].Select(x => (string)x).ToList();
                var expS = b["strings"].Select(x => (string)x).ToList();
                Assert.True(expC.SequenceEqual(callees), key + " : appels differents de theme-b1. En moins : " + string.Join(" | ", expC.Except(callees)) + " ; en plus : " + string.Join(" | ", callees.Except(expC)));
                Assert.True(expS.SequenceEqual(strings), key + " : chaines differentes de theme-b1. En moins : " + string.Join(" | ", expS.Except(strings)) + " ; en plus : " + string.Join(" | ", strings.Except(expS)));
            }
        }

        // L'empreinte voit bien ce qui compte : etapes de l'installation (memboot, drapeaux, traitement des mods), y compris dans les lambdas.
        [Fact]
        public void ModInstallFingerprintSeesInstallSteps()
        {
            RuntimeHelpers.RunModuleConstructor(Asm.ManifestModule.ModuleHandle);
            List<string> c, s;
            IlCallees.Fingerprint(Asm, "ModHub.Controls.ModHubTabControl", "handleModDownload", out c, out s);
            Assert.Contains("touch /mod-recovery.flag", s);
            Assert.Contains("touch /user-recovery.flag", s);
            Assert.Contains(c, x => x.StartsWith("com.clusterrr.hakchi_gui.Tasks.MembootTasks::Void .ctor(", StringComparison.Ordinal));
            Assert.Contains(c, x => x.Contains("ShellTasks::") && x.Contains("ShellCommand"));
            IlCallees.Fingerprint(Asm, "ModHub.Controls.ModHubTabControl", "GetItemDownloadTask", out c, out s);
            Assert.Contains(c, x => x.Contains("MainForm::Void AddGames("));               // dans une lambda : la descente fonctionne
            IlCallees.Fingerprint(Asm, "MainForm", "uninstallModulesToolStripMenuItem_Click", out c, out s);
            Assert.Contains(c, x => x.Contains("MainForm::Boolean UninstallMods("));
            Assert.Contains(c, x => x.Contains("Hmod.SelectForm::Void .ctor(Boolean, Boolean, System.String[])"));
        }

        private static readonly object EventClick = typeof(Control).GetField("EventClick", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static IEnumerable<string> ClickHandlers(Control c)
        {
            var events = (EventHandlerList)typeof(Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(c, null);
            var d = events[EventClick];
            return d == null ? Enumerable.Empty<string>() : d.GetInvocationList().Select(x => x.Method.Name);
        }

        // Les boutons Installer / Telecharger / OK sont toujours branches sur les memes gestionnaires (et sur eux seuls).
        [StaFact]
        public void InstallButtonsStillWired()
        {
            MainFormEnv.Init();
            using (var tab = new ModHubTabControl())
            {
                Assert.Equal(new[] { "modDownloadButton_Click" }, ClickHandlers(Field<Button>(tab, "modDownloadButton")).ToArray());
                Assert.Equal(new[] { "modDownloadInstallButton_Click" }, ClickHandlers(Field<Button>(tab, "modDownloadInstallButton")).ToArray());
            }
            using (var f = Create("Hmod.SelectForm"))
                Assert.Equal(new[] { "buttonOk_Click" }, ClickHandlers(Field<Button>(f, "buttonOk")).ToArray());
        }

        // Liste du ModHub : groupes du depot, un element selectionne, la carte remplie (le readme est verifie par ReadmeCssInjectedOnce).
        [StaFact]
        public void HubShowsSampleRepository()
        {
            using (var f = CreateHub())
            {
                var tabs = Tree(f).OfType<TabControl>().Single();
                Assert.Equal(3, tabs.TabPages.Count);            // accueil, Emulators, UI
                var tab = Tree(tabs.TabPages[1]).OfType<ModHubTabControl>().Single();
                var list = Field<ListView>(tab, "modList");
                Assert.Equal(2, list.Items.Count);
                Assert.True(list.Groups.Count >= 2);
                var buttons = new[] { Field<Button>(tab, "modDownloadButton"), Field<Button>(tab, "modDownloadInstallButton") };
                var info = Field<ModInfoControl>(tab, "modInfo");
                Assert.Equal(DockStyle.Fill, info.Dock);
                foreach (var b in buttons) Assert.Equal(DockStyle.Bottom, b.Dock);
            }
        }
    }
}
