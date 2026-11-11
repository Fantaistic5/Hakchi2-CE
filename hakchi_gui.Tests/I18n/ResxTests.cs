using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using com.clusterrr.hakchi_gui;
using hakchi_gui.Tests.Shell;
using Newtonsoft.Json.Linq;
using Xunit;

namespace hakchi_gui.Tests.I18n
{
    /// <summary>
    /// Langues (Task 10) : la mise en page des fenetres redessinees vient du seul resx neutre ; les resx localises ne portent que
    /// des textes. Les assemblages satellites ne sont pas a cote de l'exe de test : les resx sont lus comme des XML, et la fenetre
    /// principale charge ses satellites par <see cref="Satellites"/>.
    /// </summary>
    public class ResxTests
    {
        /// <summary>Fenetres redessinees par les Tasks 5 (MainForm), 6, 8 et 9, relatives a hakchi_gui. Liste partagee avec tools/gen_resx_keys_before.py.</summary>
        internal static readonly string[] Redesigned =
        {
            "MainForm",
            "AboutBox", "Motd", "TextInfo", "StringInputForm", "SelectFileForm", "SearchForm", "SettingsForm", "TechInfo",
            "SelectIconForm", "SelectSystemDialog", "SelectFirmwareDialog", "ExportGamesDialog", "NewCustomGameForm", "SpineForm",
            "ImagesForm", "ImageGooglerForm", "DumperForm",
            "FoldersManagerForm", "ScraperForm", "SelectCoreDialog", "SaveStateManager", "SelectCoverDialog", "ScreenshotForm",
            "GameImporterForm", "GameGenieCodeForm", "GameGenieCodeAddModForm", "CoverBatchForm",
            "ModHub/ModHubForm", "ModHub/Repository/RepoManagementForm", "Hmod/SelectForm", "ModHub/Controls/ModHubTabControl",
            "ModHub/Controls/ModInfoControl", "Hmod/Controls/ReadmeControl", "Hmod/Controls/BrowserReadmeControl",
            "Hmod/Controls/TextReadmeControl",
        };

        /// <summary>Fichiers de chaines (sans mise en page) dont les cles doivent aussi rester intactes.</summary>
        private static readonly string[] ResourceFiles = { "Properties/Resources", "ModHub/ModHubResources" };

        /// <summary>Fenetres de la Task 7 (flash, installation) : jamais touchees ici.</summary>
        private static readonly string[] Task7 =
        {
            "Tasks/TaskerForm", "Tasks/TaskerTransferForm", "Tasks/MessageForm", "Tasks/ErrorForm", "Tasks/SDFormat", "WaitingFelForm",
            "WaitingShellForm", "WaitingShellCycleForm", "SelectNesButtonsForm", "SelectSnesButtonsForm", "SnesPresetEditor",
        };

        /// <summary>Cles de mise en page : metadonnees du concepteur (>>) et proprietes de position, de taille ou de disposition.</summary>
        internal static readonly Regex LayoutKey = new Regex(
            @"^(>>.*|.+\.(Location|Size|ClientSize|Anchor|Dock|Margin|Padding|TabIndex|AutoSize|MinimumSize|MaximumSize|AutoScaleDimensions|LayoutSettings))$");

        private static readonly Regex Culture = new Regex(@"^[a-z]{2}(-[A-Z][a-z]{3})?(-[A-Z]{2})?$");

        private static string GuiDir => Path.Combine(Repo.Root, "hakchi_gui");

        /// <summary>Resx localises (Nom.culture.resx) d'une fenetre, par culture.</summary>
        private static IEnumerable<KeyValuePair<string, string>> Localized(string form)
        {
            var path = Path.Combine(GuiDir, form.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileName(path);
            foreach (var f in Directory.GetFiles(dir, name + ".*.resx"))
            {
                var culture = Path.GetFileName(f).Substring(name.Length + 1);
                culture = culture.Substring(0, culture.Length - ".resx".Length);
                if (Culture.IsMatch(culture)) yield return new KeyValuePair<string, string>(culture, f);
            }
        }

        private static string Rel(string file) => file.Substring(GuiDir.Length + 1).Replace(Path.DirectorySeparatorChar, '/');

        private static Dictionary<string, string> Strings(string file) =>
            XDocument.Load(file).Root.Elements("data").ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value") ?? "");

        [Fact]
        public void RedesignedListExcludesTask7()
        {
            Assert.Empty(Redesigned.Intersect(Task7));
            foreach (var f in Redesigned.Concat(Task7))
                Assert.True(File.Exists(Path.Combine(GuiDir, f.Replace('/', Path.DirectorySeparatorChar) + ".resx")) ||
                            File.Exists(Path.Combine(GuiDir, f.Replace('/', Path.DirectorySeparatorChar) + ".cs")), f);
        }

        [Fact]
        public void RedesignedLocalizedResxHaveNoLayoutKeys()
        {
            var problems = new List<string>();
            int files = 0;
            foreach (var form in Redesigned)
                foreach (var kv in Localized(form))
                {
                    files++;
                    foreach (var d in XDocument.Load(kv.Value).Root.Elements("data"))
                    {
                        var name = (string)d.Attribute("name");
                        if (LayoutKey.IsMatch(name)) problems.Add(Rel(kv.Value) + " : " + name);
                    }
                }
            Assert.True(files >= 200, "resx localises trouves : " + files);
            Assert.True(problems.Count == 0, problems.Count + " cle(s) de mise en page :\n" + string.Join("\n", problems.Take(40)));
        }

        private static string Hash(XElement d)
        {
            var type = (string)d.Attribute("type") ?? "";
            if (d.Attribute("mimetype") != null || type.StartsWith("System.Drawing.Bitmap") || type.StartsWith("System.Drawing.Icon")) return "bin";
            using (var sha = SHA1.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes((string)d.Element("value") ?? ""));
                return string.Concat(h.Select(b => b.ToString("x2"))).Substring(0, 12);
            }
        }

        /// <summary>
        /// Pour chaque resx localise (et les fichiers de chaines), les cles hors mise en page et leurs valeurs sont celles du commit
        /// de base de la Task 10 (instantane Data/resx_keys_before.json, tools/gen_resx_keys_before.py) : aucun texte perdu ni modifie.
        /// </summary>
        [Fact]
        public void AllTextKeysPreserved()
        {
            var before = JObject.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "resx_keys_before.json")));
            Assert.Equal("f208ba77fa99747047bbe2ec446a51cc972af1c0", (string)before["_base"]);
            var expected = (JObject)before["files"];

            var now = new Dictionary<string, Dictionary<string, string>>();
            foreach (var form in Redesigned.Concat(ResourceFiles))
                foreach (var kv in Localized(form))
                    now[Rel(kv.Value)] = XDocument.Load(kv.Value).Root.Elements("data")
                        .Where(d => !LayoutKey.IsMatch((string)d.Attribute("name")))
                        .ToDictionary(d => (string)d.Attribute("name"), Hash);

            var problems = new List<string>();
            foreach (var p in expected.Properties())
            {
                Dictionary<string, string> cur;
                if (!now.TryGetValue(p.Name, out cur)) { problems.Add(p.Name + " : fichier disparu"); continue; }
                var exp = ((JObject)p.Value).Properties().ToDictionary(x => x.Name, x => (string)x.Value);
                foreach (var k in exp.Keys.Except(cur.Keys)) problems.Add(p.Name + " : cle perdue " + k);
                foreach (var k in cur.Keys.Except(exp.Keys)) problems.Add(p.Name + " : cle en plus " + k);
                foreach (var k in exp.Keys.Intersect(cur.Keys).Where(k => exp[k] != cur[k])) problems.Add(p.Name + " : valeur changee " + k);
            }
            foreach (var f in now.Keys.Except(expected.Properties().Select(x => x.Name))) problems.Add(f + " : fichier nouveau");
            Assert.True(expected.Count >= 200, "instantane : " + expected.Count + " fichiers");
            Assert.True(problems.Count == 0, string.Join("\n", problems.Take(40)));
        }

        /// <summary>Textes ajoutes par les Tasks 5 a 9 (chaines Resources et resx de fenetre) : en anglais (resx neutre) et en francais.</summary>
        internal static readonly string[][] NewStrings =
        {
            new[] { "Properties/Resources",
                "CoverGridNoCover", "NavSystemsHeader", "NavCollectionsHeader", "NavAll", "NavNoCover", "NavFavorites", "ConsoleBadgeOffline",
                "SheetCoverSource", "SheetCoverSourceUnknown", "SheetNoSelection", "SheetChangeCover", "SheetPlayers", "SheetPublisher",
                "SheetGenre", "SheetCore", "StorageBarCaption", "StorageUnitMB", "StorageUnitGB", "ShellMenuGames", "ShellMenuMods",
                "ShellMenuCovers", "ShellChipOnConsole", "ShellChipLibrary", "ShellChipAz", "ShellFindCovers", "ShellSync",
                "ShellCoverChange", "ShellCoverGoogle", "ShellCoverDefault", "ShellCoverSpine" },
            new[] { "CoverBatchForm", "Msg.SummaryMixed" },
        };

        [Fact]
        public void NewStringsHaveFrAndEn()
        {
            var problems = new List<string>();
            foreach (var set in NewStrings)
            {
                var basePath = Path.Combine(GuiDir, set[0].Replace('/', Path.DirectorySeparatorChar));
                var en = Strings(basePath + ".resx");
                var fr = Strings(basePath + ".fr-FR.resx");
                foreach (var k in set.Skip(1))
                {
                    string v;
                    if (!en.TryGetValue(k, out v) || v.Trim().Length == 0) problems.Add(set[0] + " EN manquant : " + k);
                    if (!fr.TryGetValue(k, out v) || v.Trim().Length == 0) problems.Add(set[0] + " FR manquant : " + k);
                    else if (en.ContainsKey(k) && Regex.Matches(en[k], @"\{\d+\}").Count != Regex.Matches(fr[k], @"\{\d+\}").Count)
                        problems.Add(set[0] + " FR sans les memes parametres {n} : " + k);
                }
            }
            Assert.True(problems.Count == 0, string.Join("\n", problems));
            // les chaines de la coque sont toutes listees : rien de neuf dans Resources.resx qui manquerait en francais
            var enAll = Strings(Path.Combine(GuiDir, "Properties", "Resources.resx"));
            var frAll = Strings(Path.Combine(GuiDir, "Properties", "Resources.fr-FR.resx"));
            var shell = enAll.Keys.Where(k => Regex.IsMatch(k, "^(Shell|Nav|Sheet|Storage|ConsoleBadge|CoverGrid)")).ToList();
            Assert.Empty(shell.Except(NewStrings[0].Skip(1)));
            Assert.Empty(shell.Where(k => !frAll.ContainsKey(k)));
        }
    }

    /// <summary>
    /// Titres en capitales espacees (B1Draw.DrawTracked) : poser les lettres une a une casse l'arabe (lettres isolees, ordre inverse,
    /// vu en galerie ar-SA). Un texte a mise en forme complexe est dessine d'un seul tenant, sans espacement.
    /// </summary>
    public class TrackedTextTests
    {
        private const TextFormatFlags Raw = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.Top;

        private static Bitmap Draw(Action<Graphics> paint)
        {
            var bmp = new Bitmap(400, 40);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                paint(g);
            }
            return bmp;
        }

        private static bool Same(Bitmap a, Bitmap b)
        {
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    if (a.GetPixel(x, y) != b.GetPixel(x, y)) return false;
            return true;
        }

        [StaTheory]
        [InlineData("معلومات اللعبة")]       // ar-SA : GAME INFO
        [InlineData("מידע על המשחק")]         // hebreu
        public void ComplexScriptIsDrawnWholeWithoutTracking(string text)
        {
            using (var f = new Font("Segoe UI", 9f))
            using (var tracked = Draw(g => com.clusterrr.hakchi_gui.Theme.Controls.B1Draw.DrawTracked(g, text, f, Color.White, 4, 4, 0.14f)))
            using (var whole = Draw(g => TextRenderer.DrawText(g, text, f, new Point(4, 4), Color.White, Raw)))
            using (var g = Graphics.FromImage(tracked))
            {
                Assert.True(Same(tracked, whole), "texte arabe / hebreu decoupe lettre par lettre");
                Assert.Equal(TextRenderer.MeasureText(g, text, f, new Size(int.MaxValue, int.MaxValue), Raw).Width,
                             com.clusterrr.hakchi_gui.Theme.Controls.B1Draw.TrackedWidth(g, text, f, 0.14f));
            }
        }

        [DllImport("gdi32.dll")] private static extern uint SetLayout(IntPtr hdc, uint layout);

        /// <summary>
        /// En-tete de groupe d'une liste en miroir (galerie ar-SA --rtl, SelectForm) : le titre disparaissait. Peint dans un DC en
        /// LAYOUT_RTL comme le fait la liste, il doit apparaitre a droite (physiquement) et le filet a gauche ; liste normale : l'inverse.
        /// </summary>
        [StaTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void GroupHeaderTitleFollowsMirroring(bool mirrored)
        {
            const int W = 300, H = 24;
            using (var bmp = new Bitmap(W, H))
            using (var font = new Font("Segoe UI", 9f))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    var hdc = g.GetHdc();
                    try
                    {
                        if (mirrored) SetLayout(hdc, 1);
                        // rectangle logique d'un en-tete de groupe pleine largeur ; RightToLeft suit le miroir
                        com.clusterrr.hakchi_gui.Theme.Controls.ListViewWindow.PaintGroupHeader(hdc, new Rectangle(0, 0, W, H), "SYSTEM", font,
                            com.clusterrr.hakchi_gui.Theme.B1Palette.Slate, mirrored, 1f, W);
                    }
                    finally { g.ReleaseHdc(hdc); }
                }
                int textLeft = 0, textRight = 0, ruleLeft = 0, ruleRight = 0;
                var rule = com.clusterrr.hakchi_gui.Theme.B1Palette.Rule.ToArgb();
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        var c = bmp.GetPixel(x, y);
                        if (c.ToArgb() == rule) { if (x < W / 2) ruleLeft++; else ruleRight++; }
                        else if (c.B > 0x70) { if (x < W / 2) textLeft++; else textRight++; }      // titre Lavender (lissage compris)
                    }
                if (mirrored)
                {
                    Assert.True(textRight > 10 && textLeft == 0, "titre : gauche " + textLeft + ", droite " + textRight);
                    Assert.True(ruleLeft > 0, "filet absent a gauche");
                }
                else
                {
                    Assert.True(textLeft > 10 && textRight == 0, "titre : gauche " + textLeft + ", droite " + textRight);
                    Assert.True(ruleRight > 0, "filet absent a droite");
                }
            }
        }

        /// <summary>Lignes (y) ou le texte clair est present, regroupees en bandes separees par au moins une ligne vide.</summary>
        private static int TextBands(Bitmap bmp)
        {
            int bands = 0; bool inBand = false;
            for (int y = 0; y < bmp.Height; y++)
            {
                bool any = false;
                for (int x = 0; x < bmp.Width && !any; x++) any = bmp.GetPixel(x, y).G > 0x80;
                if (any && !inBand) bands++;
                inBand = any;
            }
            return bands;
        }

        /// <summary>
        /// Bouton B1 trop etroit pour son libelle (FoldersManagerForm, de-DE : « Originale Spiele -&gt; Andere Spiele ») : s'il a la
        /// hauteur de deux lignes, le texte passe a la ligne comme sur un bouton natif ; sinon il garde une ligne et des points de suspension.
        /// </summary>
        [StaTheory]
        [InlineData(42, 2)]
        [InlineData(24, 1)]
        public void LongButtonTextWrapsWhenTallEnough(int height, int lines)
        {
            using (var f = new Font("Segoe UI", 9f))
            using (var b = new Button { Text = "Originale Spiele -> Andere Spiele", Font = f, Size = new Size(118, height) })
            using (var bmp = new Bitmap(118, height))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    com.clusterrr.hakchi_gui.Theme.Controls.B1Draw.ButtonContent(g, new Rectangle(0, 0, 118, height), b, Color.White, 1f);
                }
                Assert.Equal(lines, TextBands(bmp));
            }
        }

        /// <summary>Titre de groupe plus large que le groupe (de-DE « ORDNERSTRUKTUR AUTOMATISCH ERSTELLEN ») : il reste dans le groupe.</summary>
        [StaFact]
        public void LongGroupTitleStaysInsideTheGroup()
        {
            using (var gb = new GroupBox { Text = "Ordnerstruktur automatisch erstellen", Font = new Font("Segoe UI", 8.25f), Size = new Size(254, 60) })
            using (var bmp = new Bitmap(300, 60))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    com.clusterrr.hakchi_gui.Theme.Controls.B1Draw.PaintGroup(g, gb, new Rectangle(0, 0, 254, 60), Color.Black, 1f);
                }
                int right = -1;
                for (int y = 4; y < 30; y++)          // sous le filet du haut
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        var c = bmp.GetPixel(x, y);
                        if (c.R + c.G + c.B > 120) right = Math.Max(right, x);
                    }
                Assert.True(right > 100, "titre absent");
                Assert.True(right <= 254 - 3, "titre coupe au bord du groupe : x " + right);
            }
        }

        [StaFact]
        public void LatinCapsStayTracked()
        {
            using (var f = new Font("Segoe UI", 9f))
            using (var bmp = new Bitmap(10, 10))
            using (var g = Graphics.FromImage(bmp))
                Assert.True(com.clusterrr.hakchi_gui.Theme.Controls.B1Draw.TrackedWidth(g, "GAME INFO", f, 0.14f) >
                            TextRenderer.MeasureText(g, "GAME INFO", f, new Size(int.MaxValue, int.MaxValue), Raw).Width + 8);
        }
    }

    /// <summary>
    /// Charge les satellites de hakchi (languages\culture\hakchi.resources.dll du build, comme Program.cs) pendant la portee :
    /// sous xUnit ils ne sont pas a cote de l'exe de test.
    /// </summary>
    internal sealed class Satellites : IDisposable
    {
        private readonly ResolveEventHandler handler;
        public static string Dir => Path.Combine(Repo.Root, "hakchi_gui", "bin", "Debug", "net48", "languages");

        public Satellites()
        {
            handler = (s, e) =>
            {
                var n = new AssemblyName(e.Name);
                if (!string.Equals(n.Name, "hakchi.resources", StringComparison.OrdinalIgnoreCase) || n.CultureInfo == null || n.CultureInfo.Name.Length == 0) return null;
                var f = Path.Combine(Dir, n.CultureInfo.Name, "hakchi.resources.dll");
                return File.Exists(f) ? Assembly.LoadFrom(f) : null;
            };
            AppDomain.CurrentDomain.AssemblyResolve += handler;
        }

        public void Dispose() { AppDomain.CurrentDomain.AssemblyResolve -= handler; }
    }

    /// <summary>
    /// Fenetres redessinees (hors MainForm) construites comme le concepteur (sans constructeur applicatif : ni console, ni fichiers),
    /// habillees B1, dans chaque langue : aucun controle visible ne chevauche un frere et aucun libelle ou bouton n'est tronque.
    /// </summary>
    [Collection("B1Static")]
    public class LocalizedDialogsTests
    {
        static LocalizedDialogsTests() { MainFormEnv.Init(); }

        internal static readonly string[] Dialogs =
        {
            "AboutBox", "Motd", "TextInfo", "StringInputForm", "SelectFileForm", "SearchForm", "SettingsForm", "TechInfo",
            "SelectIconForm", "SelectSystemDialog", "SelectFirmwareDialog", "ExportGamesDialog", "NewCustomGameForm", "SpineForm",
            "ImagesForm", "ImageGooglerForm", "DumperForm",
            "FoldersManagerForm", "ScraperForm", "SelectCoreDialog", "SaveStateManager", "SelectCoverDialog", "ScreenshotForm",
            "GameImporterForm", "GameGenieCodeForm", "GameGenieCodeAddModForm",
            // CoverBatchForm : absente, construite sans son constructeur elle leve une NullReferenceException (il lui faut
            // les donnees de CoverBatchSample de la galerie) ; ses textes sont couverts par la galerie et NewStringsHaveFrAndEn
            "ModHub.ModHubForm", "ModHub.Repository.RepoManagementForm", "Hmod.SelectForm",
        };

        public static IEnumerable<object[]> Cultures() =>
            new[] { "", "ar-SA", "de-DE", "es", "fr-FR", "it", "ko", "pt-BR", "sv" }.Select(c => new object[] { c });

        internal static Form CreateDesigned(string name)
        {
            var t = typeof(MainForm).Assembly.GetType("com.clusterrr.hakchi_gui." + name, true);
            var f = (Form)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
            typeof(com.clusterrr.hakchi_gui.Theme.B1Form).GetConstructor(Type.EmptyTypes).Invoke(f, null);
            t.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f, null);
            for (var tt = t; tt != null && tt != typeof(Form); tt = tt.BaseType)
                foreach (var fi in tt.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    if (typeof(System.Windows.Forms.Timer).IsAssignableFrom(fi.FieldType)) { var tm = (System.Windows.Forms.Timer)fi.GetValue(f); if (tm != null) tm.Enabled = false; }
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-30000, -30000);
            f.ShowInTaskbar = false;
            return f;
        }

        private static void Handles(Control c) { var h = c.Handle; foreach (Control x in c.Controls) Handles(x); }

        /// <summary>
        /// Problemes de mise en page de chaque fenetre dans une langue. Les exceptions d'interface (gestionnaires qui attendent le
        /// constructeur applicatif) sont relevees ici au lieu d'ouvrir la boite d'erreur de .NET.
        /// </summary>
        internal static List<string> Problems(string culture)
        {
            var problems = new List<string>();
            var oldUi = Thread.CurrentThread.CurrentUICulture;
            string current = null;
            System.Threading.ThreadExceptionEventHandler onError = (s, e) => problems.Add(current + " : exception d'interface " + e.Exception.GetType().Name + " " + e.Exception.Message);
            Application.ThreadException += onError;
            using (new Satellites())
                try
                {
                    Thread.CurrentThread.CurrentUICulture = culture.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(culture);
                    foreach (var name in Dialogs)
                    {
                        current = name;
                        Form f = null;
                        try
                        {
                            f = CreateDesigned(name);
                            Handles(f);
                            f.PerformLayout();
                            Application.DoEvents();
                            var list = new List<string>();
                            MainFormLayoutTests.Check(f, list);
                            list.AddRange(LocalizedMainFormTests.Clipped(f));
                            problems.AddRange(list.Select(p => name + " : " + p));
                        }
                        catch (Exception ex) { problems.Add(name + " : construction " + (ex.InnerException ?? ex).Message); }
                        finally { try { if (f != null) f.Dispose(); } catch (Exception ex) { problems.Add(name + " : fermeture " + ex.Message); } }
                        Application.DoEvents();
                    }
                }
                finally
                {
                    Thread.CurrentThread.CurrentUICulture = oldUi;
                    Application.ThreadException -= onError;
                }
            return problems;
        }

        /// <summary>Identite d'un probleme sans les mesures (rectangles, textes, largeurs) : pour le comparer d'une langue a l'autre.</summary>
        internal static string Key(string problem) => Regex.Replace(problem, @"\{[^}]*\}|«.*»|\d+ > \d+", "").Trim();

        private static HashSet<string> neutral;

        /// <summary>
        /// Une langue n'ajoute aucun probleme a ceux du resx neutre (anglais) : un libelle traduit plus long ne chevauche pas son voisin
        /// et ne se fait pas tronquer. Les recouvrements voulus du neutre (panneau de chargement par-dessus la liste...) ne comptent pas.
        /// </summary>
        [StaTheory, MemberData(nameof(Cultures))]
        public void RedesignedDialogsFitInEveryLanguage(string culture)
        {
            if (neutral == null) neutral = new HashSet<string>(Problems("").Select(Key));
            var problems = Problems(culture);
            var added = problems.Where(p => !neutral.Contains(Key(p)) || p.Contains("exception") || p.Contains("construction") || p.Contains("fermeture")).ToList();
            Assert.True(added.Count == 0, (culture.Length == 0 ? "neutre" : culture) + " :\n" + string.Join("\n", added));
        }
    }

    [Collection("B1Static")]
    public class LocalizedMainFormTests
    {
        static LocalizedMainFormTests() { MainFormEnv.Init(); }

        private static T Field<T>(object o, string name) =>
            (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);

        /// <summary>
        /// La vraie fenetre principale, construite avec CurrentUICulture en ar-SA puis en de-DE (satellites du build), a la taille
        /// minimale et en 1440x900 : le test de chevauchement de la Task 5 (MainFormLayoutTests.Check), la coque entiere dans la fenetre,
        /// et aucun libelle de la coque tronque. "ar-SA-rtl" force en plus le miroir (RightToLeft + RightToLeftLayout), que les resx
        /// ar-SA n'activent pas.
        /// </summary>
        [StaTheory]
        [InlineData("ar-SA", 1024, 640)]
        [InlineData("ar-SA", 1440, 900)]
        [InlineData("ar-SA-rtl", 1024, 640)]
        [InlineData("de-DE", 1024, 640)]
        [InlineData("de-DE", 1440, 900)]
        public void MainFormRendersInArSaAndDeDeWithoutOverlap(string culture, int w, int h)
        {
            bool rtl = culture.EndsWith("-rtl");
            var ci = new CultureInfo(rtl ? culture.Substring(0, culture.Length - 4) : culture);
            var oldUi = Thread.CurrentThread.CurrentUICulture;
            var oldCu = Thread.CurrentThread.CurrentCulture;
            MainForm f = null;
            using (new Satellites())
                try
                {
                    Thread.CurrentThread.CurrentUICulture = ci;
                    Thread.CurrentThread.CurrentCulture = ci;
                    f = MainFormEnv.Create(MainFormEnv.Games(6), 96, new Size(w, h), rtl);

                    // la traduction est bien chargee (sinon le test ne verifierait que l'anglais)
                    var loc = XDocument.Load(Path.Combine(Repo.Root, "hakchi_gui", "MainForm." + ci.Name + ".resx")).Root.Elements("data")
                        .ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value"));
                    Assert.Equal(loc["exitToolStripMenuItem.Text"], Field<ToolStripMenuItem>(f, "exitToolStripMenuItem").Text);
                    Assert.Equal(loc["buttonAddGames.Text"], Field<Button>(f, "buttonAddGames").Text);

                    f.listViewGames.Items[0].Selected = true;
                    f.ShowSelected();
                    f.PerformLayout();
                    Application.DoEvents();

                    var problems = new List<string>();
                    MainFormLayoutTests.Check(f, problems);
                    foreach (var c in new Control[] { f.topBar, f.shellLeft, f.shellCenter, f.sheet, f.shellFooter, f.grid, f.storageBar, f.buttonFindCovers, Field<Button>(f, "buttonStart"), Field<Button>(f, "buttonAddGames"), f.chipOnConsole, f.chipLibrary, f.chipAz, f.nav })
                    {
                        var r = f.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                        if (!f.ClientRectangle.Contains(r)) problems.Add(c.Name + " deborde de la fenetre : " + r);
                        if (r.Width <= 0 || r.Height <= 0) problems.Add(c.Name + " vide : " + r);
                    }
                    if (f.grid.Width < 300) problems.Add("grille trop etroite : " + f.grid.Width);
                    problems.AddRange(Clipped(f));
                    Assert.True(problems.Count == 0, culture + " " + f.Size + "\n" + string.Join("\n", problems));
                }
                finally
                {
                    if (f != null) MainFormEnv.Close(f);
                    Thread.CurrentThread.CurrentUICulture = oldUi;
                    Thread.CurrentThread.CurrentCulture = oldCu;
                }
        }

        /// <summary>
        /// Libelles et boutons visibles de la coque (hors fiche defilante) dont le texte d'une ligne ne tient pas dans le controle.
        /// Un controle a hauteur de plusieurs lignes peut passer a la ligne : sa largeur est comparee au mot le plus long.
        /// </summary>
        internal static IEnumerable<string> Clipped(Control root)
        {
            foreach (var c in All(root))
            {
                if (!MainFormEnv.Shown(c) || string.IsNullOrEmpty(c.Text) || c.Width <= 0) continue;
                if (!(c is Label || c is ButtonBase)) continue;
                if (c is Label && ((Label)c).AutoEllipsis) continue;
                var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                int lineH = TextRenderer.MeasureText("Ag", c.Font, Size.Empty, flags).Height;
                // largeur voulue par le controle lui-meme (texte, marges, case) ; sur plusieurs lignes, le mot le plus long
                int need = c.GetPreferredSize(Size.Empty).Width;
                if (c.Height >= 2 * lineH)
                    need = Math.Min(need, c.Text.Replace("&&", "&").Split(' ', '\n').Max(word => TextRenderer.MeasureText(word, c.Font, Size.Empty, flags).Width) + c.Padding.Horizontal);
                if (need > c.Width + 1) yield return c.Name + " tronque : « " + c.Text + " » " + need + " > " + c.Width;
            }
        }

        private static IEnumerable<Control> All(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var x in All(c)) yield return x;
            }
        }
    }
}
