using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Properties;
using com.clusterrr.hakchi_gui.Theme.Controls;
using hakchi_gui.Tests.Grid;
using Newtonsoft.Json.Linq;
using Xunit;

namespace hakchi_gui.Tests.Shell
{
    /// <summary>
    /// Fenetre principale REELLE (MainForm) : construite comme la galerie (sans Load/Shown, donc sans console, ni mise a jour,
    /// ni chargement des jeux), avec de vrais jeux copies de la copie de test dans un dossier jetable.
    /// </summary>
    internal static class MainFormEnv
    {
        private static readonly object gate = new object();
        private static bool done;
        private static List<string> sampleDirs;
        public const string SamplesSource = @"P:\HakchiSNESUSA\app\games";

        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);

        public static void Init()
        {
            lock (gate)
            {
                if (done) return;
                HakchiEnv.Init();
                var prog = typeof(NesApplication).Assembly.GetType("com.clusterrr.hakchi_gui.Program", true);
                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                prog.GetField("isPortable", flags).SetValue(null, true);
                var spine = prog.GetField("_SpineTemplates", flags);
                if (spine.GetValue(null) == null) spine.SetValue(null, Activator.CreateInstance(spine.FieldType));
                // dossiers relus depuis BaseDirectoryExternal (comme tools/FormGallery/Host.cs) : jonctions vers le build
                var build = Path.Combine(Repo.Root, "hakchi_gui", "bin", "Debug", "net48");
                foreach (var d in new[] { "art", "folder_images", "info", "patches", "sfrom_tool", "user_mods", "spine_templates", "data" })
                {
                    var link = Path.Combine(HakchiEnv.Temp, d);
                    var target = Path.Combine(build, d);
                    if (Directory.Exists(link) || !Directory.Exists(target)) continue;
                    var psi = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    using (var p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(); }
                }
                CoreCollection.Load();      // FormInitialize le fait ; NesApplication.FromDirectory en depend
                done = true;
            }
        }

        /// <summary>Copie (lecture seule de la source) de quelques vrais jeux dans le dossier jetable.</summary>
        public static List<NesApplication> Games(int n)
        {
            Init();
            lock (gate)
            {
                if (sampleDirs == null)
                {
                    sampleDirs = new List<string>();
                    if (Directory.Exists(SamplesSource))
                    {
                        foreach (var src in Directory.GetDirectories(SamplesSource).OrderBy(x => x, StringComparer.Ordinal).Take(12))
                        {
                            var dst = Path.Combine(HakchiEnv.Temp, "games", Path.GetFileName(src));
                            CopyDir(src, dst);
                            sampleDirs.Add(dst);
                        }
                    }
                }
            }
            Assert.True(sampleDirs.Count >= n, "jeux d'exemple introuvables dans " + SamplesSource);
            return sampleDirs.Take(n).Select(d => NesApplication.FromDirectory(d)).ToList();
        }

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        /// <summary>
        /// MainForm sans ses gestionnaires Load/Shown/Closing, hors ecran, affichee sans activation (les controles ont un etat visible),
        /// avec ces jeux dans listViewGames. Un garde-fou ferme toute boite de dialogue modale qui bloquerait le test.
        /// </summary>
        public static MainForm Create(IList<NesApplication> apps, int dpi = 96, Size? size = null)
        {
            Init();
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            B1Dpi.Override = dpi == 96 ? 0f : dpi / 96f;
            MainForm f;
            using (DialogGuard.Start())
                f = new MainForm();
            StripHandlers(f);
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-30000, -30000);
            f.ShowInTaskbar = false;
            if (dpi != 96) f.Font = new Font(f.Font.FontFamily, f.Font.Size * dpi / 96f, f.Font.Style, f.Font.Unit);   // AutoScaleMode.Font
            if (apps != null && apps.Count > 0)
            {
                f.listViewGames.BeginUpdate();
                f.listViewGames.Items.AddRange(apps.Select((a, i) => new ListViewItem(a.ToString()) { Tag = a, Checked = i % 2 == 0 }).ToArray());
                f.listViewGames.EndUpdate();
            }
            CreateHandles(f);
            ShowWindow(f.Handle, 4);
            f.Size = size ?? f.MinimumSize;
            f.PerformLayout();
            f.UpdateShellCounts();
            Application.DoEvents();
            return f;
        }

        public static void Close(MainForm f)
        {
            try { f.Dispose(); } finally { B1Dpi.Override = 0f; }
        }

        private static void CreateHandles(Control c)
        {
            var h = c.Handle;
            foreach (Control child in c.Controls) CreateHandles(child);
        }

        private static void StripHandlers(Form f)
        {
            var events = (EventHandlerList)typeof(Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f, null);
            foreach (var name in new[] { "EVENT_LOAD", "EVENT_SHOWN", "EVENT_ACTIVATED", "EVENT_DEACTIVATE", "EVENT_CLOSING", "EVENT_CLOSED", "EVENT_FORMCLOSING", "EVENT_FORMCLOSED" })
            {
                var fi = typeof(Form).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
                if (fi != null) events[fi.GetValue(null)] = null;
            }
        }

        /// <summary>Visibilite propre du controle (sans tenir compte des parents), comme la fixe Visible = x.</summary>
        public static bool OwnVisible(Control c)
        {
            return (bool)typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, new object[] { 0x2 });
        }

        /// <summary>Visible a l'ecran : lui et tous ses parents jusqu'a la fenetre.</summary>
        public static bool Shown(Control c)
        {
            for (var x = c; x != null && !(x is Form); x = x.Parent) if (!OwnVisible(x)) return false;
            return true;
        }

        public static Delegate Handlers(Component c, Type owner, string eventField)
        {
            var key = owner.GetField(eventField, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var events = (EventHandlerList)typeof(Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(c, null);
            return events[key];
        }

        public static IEnumerable<string> HandlerNames(Delegate d)
        {
            return d == null ? Enumerable.Empty<string>() : d.GetInvocationList().Select(x => x.Method.DeclaringType.Name + "." + x.Method.Name);
        }
    }

    /// <summary>Ferme (WM_CLOSE) toute fenetre de dialogue du processus qui apparait pendant la portee, et la signale.</summary>
    internal sealed class DialogGuard : IDisposable
    {
        private delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

        private readonly System.Threading.Timer timer;
        public static readonly List<string> Closed = new List<string>();

        private DialogGuard()
        {
            uint me = (uint)Process.GetCurrentProcess().Id;
            timer = new System.Threading.Timer(_ =>
            {
                EnumWindows((h, l) =>
                {
                    uint pid; GetWindowThreadProcessId(h, out pid);
                    if (pid == me && IsWindowVisible(h))
                    {
                        var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
                        lock (Closed) Closed.Add(sb.ToString());
                        PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    }
                    return true;
                }, IntPtr.Zero);
            }, null, 15000, 2000);
        }

        public static DialogGuard Start() { return new DialogGuard(); }
        public void Dispose() { timer.Dispose(); }
    }

    [Collection("B1Static")]
    public class MainFormLayoutTests
    {
        // Les champs statiques de MainForm (chemins sous Program.BaseDirectoryExternal) peuvent etre initialises des la compilation JIT
        // d'une methode qui les touche : l'environnement doit etre pose avant toute methode de test.
        static MainFormLayoutTests() { MainFormEnv.Init(); }

        private static readonly string[] OriginalButtons =
        {
            "buttonAddGames", "buttonStart", "buttonExport", "buttonBrowseImage", "buttonGoogle",
            "buttonShowGameGenieDatabase", "buttonDefaultCover", "structureButton", "buttonSpine",
        };

        private static object Field(object o, string name)
        {
            var fi = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.True(fi != null, "champ absent : " + name);
            return fi.GetValue(o);
        }

        private static ToolStrip Root(ToolStripItem item)
        {
            ToolStrip owner = item.Owner;
            var it = item;
            while (it.OwnerItem != null) it = it.OwnerItem;
            return it.Owner ?? owner;
        }

        private static IEnumerable<Control> All(Control root)
        {
            foreach (Control c in root.Controls)
            {
                yield return c;
                foreach (var x in All(c)) yield return x;
            }
        }

        [StaFact]
        public void EveryOriginalMenuItemStillReachable()
        {
            var json = JArray.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "menu_items_before.json")));
            Assert.Equal(148, json.Count);
            var f = MainFormEnv.Create(MainFormEnv.Games(4));
            try
            {
                Assert.Same(f.topBar.Menu, f.MainMenuStrip);
                Assert.Equal(new[] { Resources.ShellMenuGames, Resources.ShellMenuMods, Resources.ShellMenuCovers },
                    f.topBar.Menu.Items.Cast<ToolStripItem>().Take(3).Select(i => i.Text).ToArray());
                Assert.Equal(4, f.topBar.MenuCount);
                // un seul MenuStrip dans la fenetre : les raccourcis ne sont pas trouves deux fois
                Assert.Single(All(f).OfType<MenuStrip>());
                // la grille montre le menu contextuel de sa source ; le dossier « Structure » est ouvert par son bouton
                Assert.Same(((ContextMenuStrip)Field(f, "contextMenuStrip")), f.listViewGames.ContextMenuStrip);
                Assert.Same(((ContextMenuStrip)Field(f, "contextMenuStrip")), f.grid.ContextMenuStrip);
                var structure = (Button)Field(f, "structureButton");
                Assert.True(MainFormEnv.Shown(structure));
                Assert.Contains("MainForm.structureButton_Click", MainFormEnv.HandlerNames(MainFormEnv.Handlers(structure, typeof(Control), "EventClick")));
                // MenuActivate du MainForm toujours branche sur la barre reelle
                Assert.Contains("MainForm.MenuStrip_MenuActivate", MainFormEnv.HandlerNames(MainFormEnv.Handlers(f.topBar.Menu, typeof(MenuStrip), "EventMenuActivate")));

                var missing = new List<string>();
                foreach (JObject m in json)
                {
                    string field = (string)m["field"];
                    var item = Field(f, field) as ToolStripMenuItem;
                    if (item == null) { missing.Add(field + " : null"); continue; }
                    var root = Root(item);
                    bool reachable = root == f.topBar.Menu || root == ((ContextMenuStrip)Field(f, "contextMenuStrip")) || root == (ToolStrip)Field(f, "foldersContextMenuStrip");
                    if (!reachable) { missing.Add(field + " : hors des menus (" + (root == null ? "sans proprietaire" : root.Name) + ")"); continue; }
                    var want = ((JArray)m["clickHandlers"]).Select(x => (string)x).ToList();
                    var have = MainFormEnv.HandlerNames(MainFormEnv.Handlers(item, typeof(ToolStripItem), "EventClick")).ToList();
                    foreach (var h in want) if (!have.Contains(h)) missing.Add(field + " : gestionnaire perdu " + h);
                    string sc = (string)m["shortcut"];
                    if (sc != (item.ShortcutKeys == Keys.None ? "" : item.ShortcutKeys.ToString())) missing.Add(field + " : raccourci " + sc + " -> " + item.ShortcutKeys);
                }
                Assert.True(missing.Count == 0, string.Join("\n", missing));

                // route clavier vers le choix de jaquette (la fiche n'a qu'un lien souris)
                Assert.Equal(Keys.Control | Keys.J, f.changeCoverToolStripMenuItem.ShortcutKeys);
                Assert.Same(f.topBar.Menu, Root(f.changeCoverToolStripMenuItem));
                // ProcessCmdKey trouve le raccourci du menu Pochettes (une seule fois) : Ctrl+F12 du developpeur reste au KeyDown
                var pck = typeof(Form).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
                var msg = new Message { HWnd = f.Handle, Msg = 0x0100 };
                Assert.True((bool)pck.Invoke(f, new object[] { msg, Keys.Control | Keys.J }));
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaFact]
        public void NoOriginalButtonHandlerLost()
        {
            var json = JObject.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "controls_before.json")));
            var before = ((JArray)json["MainForm"]["controls"]).Where(c => (string)c["type"] == "System.Windows.Forms.Button").Select(c => (string)c["name"]).OrderBy(x => x).ToList();
            Assert.Equal(OriginalButtons.OrderBy(x => x), before);

            var f = MainFormEnv.Create(MainFormEnv.Games(4));
            try
            {
                f.listViewGames.Items[0].Selected = true;
                f.ShowSelected();
                Application.DoEvents();
                var tree = new HashSet<Control>(All(f));
                // Click d'un bouton, ou MouseClick (Parcourir), et les items de menu qui portent le meme gestionnaire
                var menuHandlers = new HashSet<string>();
                foreach (var it in f.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(x => typeof(ToolStripItem).IsAssignableFrom(x.FieldType)))
                {
                    var item = it.GetValue(f) as ToolStripItem;
                    if (item == null) continue;
                    var root = Root(item);
                    if (root == f.topBar.Menu || root == ((ContextMenuStrip)Field(f, "contextMenuStrip")))
                        foreach (var h in MainFormEnv.HandlerNames(MainFormEnv.Handlers(item, typeof(ToolStripItem), "EventClick"))) menuHandlers.Add(h);
                }
                var report = new List<string>();
                foreach (var name in OriginalButtons)
                {
                    var b = (Button)Field(f, name);
                    if (!tree.Contains(b)) { report.Add(name + " : plus dans la fenetre"); continue; }
                    var handlers = MainFormEnv.HandlerNames(MainFormEnv.Handlers(b, typeof(Control), "EventClick"))
                        .Concat(MainFormEnv.HandlerNames(MainFormEnv.Handlers(b, typeof(Control), "EventMouseClick"))).ToList();
                    if (handlers.Count == 0) { report.Add(name + " : plus de gestionnaire"); continue; }
                    bool reachable = MainFormEnv.Shown(b) || handlers.All(menuHandlers.Contains);
                    if (!reachable && name == "buttonShowGameGenieDatabase")
                    {
                        // Game Genie : masque par GameGenieEnabled, que le code d'origine laisse toujours a false (ShowSelected).
                        // On verifie que le bouton reapparait avec sa ligne quand la fonction est activee.
                        var gg = typeof(MainForm).GetProperty("GameGenieEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
                        gg.SetValue(f, true);
                        f.PerformLayout();
                        reachable = MainFormEnv.Shown(b) && b.Enabled;
                        gg.SetValue(f, false);
                    }
                    if (!reachable) report.Add(name + " : cache et " + string.Join(",", handlers) + " sans autre acces");
                }
                Assert.True(report.Count == 0, string.Join("\n", report));
                // Synchroniser = le bouton de synchro d'origine, en bouton primaire du pied de page
                Assert.True(MainFormEnv.Shown(((Button)Field(f, "buttonStart"))));
                Assert.Same(f.shellFooterRow, ((Button)Field(f, "buttonStart")).Parent);
                Assert.Equal(Resources.ShellSync, ((Button)Field(f, "buttonStart")).Text);
                Assert.Contains("MainForm.findMissingCoversToolStripMenuItem_Click", MainFormEnv.HandlerNames(MainFormEnv.Handlers(f.buttonFindCovers, typeof(Control), "EventClick")));
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaFact]
        public void ListViewGamesStillSourceOfTruth()
        {
            var apps = MainFormEnv.Games(6);
            var f = MainFormEnv.Create(apps, 96, new Size(1440, 900));
            try
            {
                var lv = f.listViewGames;
                Assert.Same(lv, f.grid.Source);
                Assert.False(MainFormEnv.OwnVisible(lv));
                Assert.True(lv.IsHandleCreated);
                Assert.Equal(6, f.grid.VisibleCount);

                // cocher une jaquette dans la grille coche l'item de la source
                var item = f.grid.VisibleItems[1];
                bool was = item.Checked;
                var r = f.grid.GetSwitchRect(1);
                ShellFixture.Click(f.grid, ShellFixture.Center(r));
                Assert.Equal(!was, item.Checked);
                Assert.Equal(!was, lv.CheckedItems.Cast<ListViewItem>().Contains(item));

                // choisir dans la grille = choisir dans la source ; la fiche suit (ShowSelected)
                ShellFixture.Click(f.grid, ShellFixture.Center(f.grid.GetCoverRect(2)));
                Assert.True(f.grid.VisibleItems[2].Selected);
                f.ShowSelected();
                Assert.Same(f.grid.VisibleItems[2].Tag, f.sheet.Game);
                Assert.True(MainFormEnv.OwnVisible(f.sheetEditPanel));

                // puce « Sur la console » : filtre la vue, pas la source ; le jeu choisi masque est deselectionne sans erreur
                // et sans reecrire son .desktop (SaveGameItem : n'ecrit que les changements)
                var hidden = lv.Items.Cast<ListViewItem>().First(i => !i.Checked);
                foreach (ListViewItem i in lv.SelectedItems) i.Selected = false;
                hidden.Selected = true;
                var app = (NesApplication)hidden.Tag;
                var desktop = Directory.GetFiles(app.BasePath, "*.desktop").Single();
                var bytes = File.ReadAllBytes(desktop);
                var stamp = File.GetLastWriteTimeUtc(desktop);
                DialogGuard.Closed.Clear();
                using (DialogGuard.Start())
                {
                    f.chipOnConsole.Checked = true;
                    Assert.True(GridFixture.PumpUntil(() => f.grid.VisibleCount == lv.CheckedItems.Count));
                }
                Assert.Empty(DialogGuard.Closed);
                Assert.False(hidden.Selected);
                Assert.Equal(6, lv.Items.Count);
                Assert.Equal(bytes, File.ReadAllBytes(desktop));
                Assert.Equal(stamp, File.GetLastWriteTimeUtc(desktop));
                Assert.Equal(GameScope.OnConsole, f.grid.Filter.Scope);
                f.chipLibrary.Checked = true;
                Assert.True(GridFixture.PumpUntil(() => f.grid.VisibleCount == 6));

                // A-Z bascule le tri de la vue seulement
                f.chipAz.PerformClick();
                Assert.True(f.chipAz.Checked);
                Assert.True(f.grid.Filter.SortAz);
                Assert.True(f.chipLibrary.Checked);      // pas dans le groupe radio des portees
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaFact]
        public void ConsoleStateUpdatesBadge()
        {
            var f = MainFormEnv.Create(MainFormEnv.Games(2));
            var h = typeof(hakchi);
            var props = new[] { "Connected", "DetectedConsoleType", "RawScriptVersion", "MinimalMemboot" };
            var saved = props.ToDictionary(p => p, p => h.GetProperty(p).GetValue(null));
            try
            {
                var off = typeof(MainForm).GetMethod("Shell_OnDisconnected", BindingFlags.Instance | BindingFlags.NonPublic);
                var on = typeof(MainForm).GetMethod("Shell_OnConnected", BindingFlags.Instance | BindingFlags.NonPublic);

                off.Invoke(f, null);
                Assert.False(f.topBar.Badge.Online);
                Assert.StartsWith(Resources.ConsoleBadgeOffline, f.topBar.Badge.BadgeText);

                // console branchee (recuperation : Shell_OnConnected s'arrete apres SyncConsoleSettings, sans parler a la console)
                h.GetProperty("Connected").SetValue(null, true);
                h.GetProperty("DetectedConsoleType").SetValue(null, (hakchi.ConsoleType?)hakchi.ConsoleType.SNES_EUR);
                h.GetProperty("RawScriptVersion").SetValue(null, "v1.0.4-126");
                h.GetProperty("MinimalMemboot").SetValue(null, true);
                on.Invoke(f, new object[] { null });
                Assert.True(f.topBar.Badge.Online);
                Assert.Equal(Resources.consoleTypeSnesEur + " · hakchi 1.0.4", f.topBar.Badge.BadgeText);

                // debranchee
                h.GetProperty("Connected").SetValue(null, false);
                h.GetProperty("MinimalMemboot").SetValue(null, false);
                off.Invoke(f, null);
                Assert.False(f.topBar.Badge.Online);
                Assert.Equal(B1Palette_TextFaint(), f.topBar.Badge.DotColor);
            }
            finally
            {
                foreach (var kv in saved) h.GetProperty(kv.Key).SetValue(null, kv.Value);
                MainFormEnv.Close(f);
            }
        }

        private static Color B1Palette_TextFaint() { return com.clusterrr.hakchi_gui.Theme.B1Palette.TextFaint; }

        [StaFact]
        public void StorageAndCoverCountsComeFromExistingSources()
        {
            var apps = MainFormEnv.Games(6);
            var f = MainFormEnv.Create(apps);
            try
            {
                int missing = apps.Count(com.clusterrr.hakchi_gui.Covers.CoverTargets.NeedsCover);
                Assert.Equal(string.Format(Resources.ShellFindCovers, missing), f.buttonFindCovers.Text);
                Assert.Equal(missing, f.nav.CountOf(GameCollection.NoCover));
                Assert.Equal(6, f.nav.TotalCount);
                // jauge : alimentee par showStats (le calcul existant des jeux coches)
                var show = typeof(MainForm).GetMethod("showStats", BindingFlags.Instance | BindingFlags.NonPublic);
                var crType = show.GetParameters()[0].ParameterType;
                var cr = Activator.CreateInstance(crType);
                crType.GetField("Count").SetValue(cr, 3);
                crType.GetField("Size").SetValue(cr, 123456789L);
                show.Invoke(f, new[] { cr });
                Assert.Equal(123456789L, f.storageBar.Used);
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaTheory]
        [InlineData(96)]
        [InlineData(144)]
        public void MinimumSize1024x640NoOverlap(int dpi)
        {
            var f = MainFormEnv.Create(MainFormEnv.Games(6), dpi);
            try
            {
                Assert.Equal(new Size(1024, 640), MainForm.ShellMinimumSize);
                f.listViewGames.Items[0].Selected = true;
                f.ShowSelected();
                f.PerformLayout();
                Application.DoEvents();

                var problems = new List<string>();
                Check(f, problems);
                // la coque tient dans la fenetre : colonnes et pied de page entiers, grille utilisable
                foreach (var c in new Control[] { f.topBar, f.shellLeft, f.shellCenter, f.sheet, f.shellFooter, f.grid, f.storageBar, f.buttonFindCovers, ((Button)Field(f, "buttonStart")), f.chipOnConsole, f.chipLibrary, f.chipAz, f.nav })
                {
                    var r = f.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                    if (!f.ClientRectangle.Contains(r)) problems.Add(c.Name + " deborde de la fenetre : " + r);
                    if (r.Width <= 0 || r.Height <= 0) problems.Add(c.Name + " vide : " + r);
                }
                float s = dpi / 96f;
                if (f.grid.Width < 300 * s) problems.Add("grille trop etroite : " + f.grid.Width);
                if (f.storageBar.Width < 120 * s) problems.Add("jauge trop etroite : " + f.storageBar.Width);
                if (f.buttonFindCovers.Width < f.buttonFindCovers.GetPreferredSize(Size.Empty).Width) problems.Add("« Chercher N jaquettes » tronque");
                Assert.True(problems.Count == 0, "taille " + f.Size + " / client " + f.ClientSize + "\n" + string.Join("\n", problems));
            }
            finally { MainFormEnv.Close(f); }
        }

        /// <summary>Aucun controle visible ne chevauche un frere visible ; dans la coque, chacun tient dans son parent.</summary>
        private static void Check(Control parent, List<string> problems)
        {
            var kids = parent.Controls.Cast<Control>().Where(MainFormEnv.OwnVisible).ToList();
            bool scrolls = parent is ScrollableControl && ((ScrollableControl)parent).AutoScroll;
            for (int i = 0; i < kids.Count; i++)
            {
                var a = kids[i];
                if (a.Width <= 0 || a.Height <= 0) continue;
                for (int j = i + 1; j < kids.Count; j++)
                {
                    var b = kids[j];
                    if (b.Width <= 0 || b.Height <= 0) continue;
                    if (a.Bounds.IntersectsWith(b.Bounds)) problems.Add(parent.Name + " : " + a.Name + " " + a.Bounds + " chevauche " + b.Name + " " + b.Bounds);
                }
                if (!scrolls && !(parent is TableLayoutPanel) && !(parent is GroupBox) && parent.Name.Length > 0 && !(parent is TopBar))
                {
                    var room = parent.DisplayRectangle;
                    if (!Rectangle.Inflate(room, 1, 1).Contains(a.Bounds)) problems.Add(parent.Name + " : " + a.Name + " " + a.Bounds + " sort de " + room);
                }
                Check(a, problems);
            }
        }
    }
}
