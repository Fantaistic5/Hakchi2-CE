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
        public static MainForm Create(IList<NesApplication> apps, int dpi = 96, Size? size = null, bool rtl = false)
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
            if (rtl) { f.RightToLeft = RightToLeft.Yes; f.RightToLeftLayout = true; }     // miroir force (les resx ar-SA ne l'activent pas)
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

        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        /// <summary>
        /// Envoie WM_CONTEXTMENU a la grille comme Windows (coordonnees ecran du centre de <paramref name="clientRect"/>,
        /// ou -1 = clavier si null) et compte les Opening du menu (annules : rien ne s'affiche).
        /// </summary>
        public static int ContextMenuOpenings(MainForm f, ContextMenuStrip menu, Rectangle? clientRect)
        {
            int opened = 0;
            CancelEventHandler h = (s, e) => { opened++; e.Cancel = true; };
            menu.Opening += h;
            try
            {
                IntPtr l = new IntPtr(-1);
                if (clientRect.HasValue)
                {
                    var r = clientRect.Value;
                    var p = f.grid.PointToScreen(new Point(r.X + r.Width / 2, r.Y + r.Height / 3));
                    l = new IntPtr((p.Y << 16) | (p.X & 0xFFFF));
                }
                SendMessage(f.grid.Handle, 0x007B, f.grid.Handle, l);
            }
            finally { menu.Opening -= h; }
            return opened;
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
                // le menu contextuel des jeux s'ouvre VRAIMENT sur la grille (WM_CONTEXTMENU, souris et clavier) : ses items
                // comptent donc comme atteignables ci-dessous ; le dossier « Structure » est ouvert par son bouton
                var gameMenu = (ContextMenuStrip)Field(f, "contextMenuStrip");
                Assert.Equal(1, MainFormEnv.ContextMenuOpenings(f, gameMenu, f.grid.GetItemRect(0)));
                Assert.Equal(1, MainFormEnv.ContextMenuOpenings(f, gameMenu, null));
                // le clic droit a selectionne un jeu : on le retire, sinon Ctrl+J (plus bas) ouvrirait le vrai choix de fichier
                foreach (ListViewItem it in f.listViewGames.Items) it.Selected = false;
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
                    bool reachable = root == f.topBar.Menu || root == gameMenu || root == (ToolStrip)Field(f, "foldersContextMenuStrip");
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

        /// <summary>
        /// Clic droit sur une jaquette : le jeu vise devient la selection AVANT l'ouverture du menu, donc les items du menu
        /// sont prepares pour lui (listViewGames_ItemSelectionChanged les active) ; un jeu deja selectionne garde la selection.
        /// </summary>
        [StaFact]
        public void GridContextMenuSelectsTargetAndPreparesItems()
        {
            var f = MainFormEnv.Create(MainFormEnv.Games(4));
            try
            {
                var gameMenu = (ContextMenuStrip)Field(f, "contextMenuStrip");
                var explorer = (ToolStripMenuItem)Field(f, "explorerToolStripMenuItem");
                foreach (ListViewItem it in f.listViewGames.Items) it.Selected = false;
                Application.DoEvents();
                Assert.False(explorer.Enabled, "sans selection, « ouvrir dans l'explorateur » doit etre grise");

                Assert.True(f.grid.VisibleCount >= 2);
                var target = f.grid.VisibleItems[1];
                Assert.Equal(1, MainFormEnv.ContextMenuOpenings(f, gameMenu, f.grid.GetItemRect(1)));
                Assert.Equal(new[] { target }, f.listViewGames.SelectedItems.Cast<ListViewItem>().ToArray());
                Assert.True(explorer.Enabled, "le menu doit etre prepare pour le jeu vise");

                // deux jeux selectionnes, clic droit sur l'un d'eux : les deux restent selectionnes
                f.grid.VisibleItems[0].Selected = true;
                Assert.Equal(1, MainFormEnv.ContextMenuOpenings(f, gameMenu, f.grid.GetItemRect(1)));
                Assert.Equal(2, f.listViewGames.SelectedItems.Count);
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
                // le menu contextuel compte comme route seulement s'il s'ouvre vraiment (clavier : la selection ne bouge pas)
                Assert.Equal(1, MainFormEnv.ContextMenuOpenings(f, (ContextMenuStrip)Field(f, "contextMenuStrip"), null));
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
                // nombre de jeux coches (ancienne barre d'etat) devant la jauge, meme chaine qu'avant
                Assert.Equal("3 " + Resources.GamesSelected + " · " + Resources.StorageBarCaption, f.storageBar.Caption);
                Assert.StartsWith(f.storageBar.Caption + " ", f.storageBar.AccessibleName);
                crType.GetField("Count").SetValue(cr, 5);
                show.Invoke(f, new[] { cr });
                Assert.StartsWith("5 " + Resources.GamesSelected, f.storageBar.Caption);
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaTheory]
        [InlineData(1440, 900, 96)]
        [InlineData(0, 0, 96)]
        [InlineData(0, 0, 144)]
        public void DescriptionStaysEditable(int w, int h, int dpi)
        {
            var f = MainFormEnv.Create(MainFormEnv.Games(4), dpi, w > 0 ? new Size(w, h) : (Size?)null);
            try
            {
                f.listViewGames.Items[0].Selected = true;
                f.ShowSelected();          // remet panel1 et textBoxDescription a 1x1 (code d'origine)
                f.PerformLayout();
                Application.DoEvents();
                var desc = (TextBox)Field(f, "textBoxDescription");
                float s = dpi / 96f;
                Assert.True(MainFormEnv.Shown(desc));
                Assert.True(desc.Height >= 100 * s, "description haute de " + desc.Height + " px (taille " + f.Size + ")");
                Assert.True(desc.Width >= 150 * s, "description large de " + desc.Width + " px");
                // atteignable par le defilement de la fiche
                f.sheetEditPanel.ScrollControlIntoView(desc);
                var r = f.sheetEditPanel.RectangleToClient(desc.RectangleToScreen(desc.ClientRectangle));
                Assert.True(r.Top >= 0 && r.Top < f.sheetEditPanel.ClientSize.Height, "description hors de la vue : " + r);
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaFact]
        public void AddGamesButtonInFooterWithOriginalTextAndHandler()
        {
            var f = MainFormEnv.Create(MainFormEnv.Games(2), 96, new Size(1440, 900));
            try
            {
                var add = (Button)Field(f, "buttonAddGames");
                Assert.True(MainFormEnv.Shown(add));
                Assert.Same(f.shellFooterRow, add.Parent);
                Assert.True(add.Right <= f.buttonFindCovers.Left, "Ajouter doit etre a gauche de Chercher");
                Assert.True(f.buttonFindCovers.Right <= ((Button)Field(f, "buttonStart")).Left);
                Assert.True(f.storageBar.Right <= add.Left);
                Assert.Equal(new[] { "MainForm.buttonAddGames_Click" }, MainFormEnv.HandlerNames(MainFormEnv.Handlers(add, typeof(Control), "EventClick")).ToArray());
                var res = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
                Assert.Equal(res.GetString("buttonAddGames.Text"), add.Text);
                Assert.Equal(com.clusterrr.hakchi_gui.Theme.Controls.B1ButtonKind.Secondary, com.clusterrr.hakchi_gui.Theme.B1Skin.KindOf(add));
                Assert.True(add.Width >= TextRenderer.MeasureText(add.Text, add.Font).Width, "texte tronque");
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaFact]
        public void ScopeChipsMatchScopeAndIgnoreFocus()
        {
            var f = MainFormEnv.Create(MainFormEnv.Games(4));
            try
            {
                Assert.Equal(GameScope.Library, f.grid.Filter.Scope);
                Assert.True(f.chipLibrary.Checked);
                Assert.False(f.chipOnConsole.Checked);
                Assert.Same(f.grid, f.ActiveControl);
                // un RadioButton qui recoit le focus clavier se cochait : la puce « Sur la console » s'allumait au demarrage
                var onEnter = typeof(Control).GetMethod("OnEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                onEnter.Invoke(f.chipOnConsole, new object[] { EventArgs.Empty });
                f.chipOnConsole.Focus();
                Application.DoEvents();
                Assert.False(f.chipOnConsole.Checked);
                Assert.True(f.chipLibrary.Checked);
                Assert.Equal(GameScope.Library, f.grid.Filter.Scope);
                // le clic (souris ou Espace) choisit, et une seule puce de portee reste allumee
                f.chipOnConsole.PerformClick();
                Assert.True(f.chipOnConsole.Checked);
                Assert.False(f.chipLibrary.Checked);
                Assert.Equal(GameScope.OnConsole, f.grid.Filter.Scope);
                f.chipLibrary.PerformClick();
                Assert.True(f.chipLibrary.Checked);
                Assert.False(f.chipOnConsole.Checked);
                Assert.Equal(GameScope.Library, f.grid.Filter.Scope);
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaFact]
        public void SheetCoverIsTheOnlyCoverTarget()
        {
            var apps = MainFormEnv.Games(4);
            var f = MainFormEnv.Create(apps, 96, new Size(1440, 900));
            try
            {
                f.listViewGames.Items[0].Selected = true;
                f.ShowSelected();
                f.PerformLayout();
                Application.DoEvents();
                var art = (PictureBox)Field(f, "pictureBoxArt");
                var thumb = (PictureBox)Field(f, "pictureBoxThumbnail");
                // l'ancienne grande image n'est plus montree (doublon de l'en-tete) mais garde ses gestionnaires
                Assert.False(MainFormEnv.Shown(art));
                Assert.Contains(art, All(f));
                Assert.Contains("MainForm.pictureBoxArt_MouseClick", MainFormEnv.HandlerNames(MainFormEnv.Handlers(art, typeof(Control), "EventMouseClick")));
                Assert.Contains("MainForm.pictureBoxArt_DragDrop", MainFormEnv.HandlerNames(MainFormEnv.Handlers(art, typeof(Control), "EventDragDrop")));
                Assert.Contains("MainForm.pictureBoxArt_DragEnter", MainFormEnv.HandlerNames(MainFormEnv.Handlers(art, typeof(Control), "EventDragEnter")));
                // la miniature (image distincte) et les illustrations Mega Drive restent
                Assert.True(MainFormEnv.Shown(thumb));
                Assert.True(MainFormEnv.Shown((Control)Field(f, "pictureBoxM2Front")));
                var groupN = (Control)Field(f, "groupBoxArtNintendo");
                Assert.True(groupN.Height < 150, "cadre Nintendo resserre sur la miniature : " + groupN.Height);
                Assert.True(thumb.Bottom <= ((Control)Field(f, "panel2")).ClientSize.Height, "miniature " + thumb.Bounds + " panneau " + ((Control)Field(f, "panel2")).Bounds + " cadre " + groupN.Bounds);
                // les boutons d'illustration restent
                foreach (var b in new[] { "buttonDefaultCover", "buttonBrowseImage", "buttonGoogle", "buttonSpine" })
                    Assert.True(MainFormEnv.Shown((Control)Field(f, b)), b);

                // la jaquette de l'en-tete est la cible : clic, glisser-deposer
                var cover = f.sheet.CoverBounds;
                Assert.False(cover.IsEmpty);
                Assert.True(f.SheetCoverHit(ShellFixture.Center(cover)));
                Assert.False(f.SheetCoverHit(new Point(cover.Right + 4, cover.Bottom + 4)));
                Assert.True(f.sheet.AllowDrop);
                Assert.NotEmpty(MainFormEnv.HandlerNames(MainFormEnv.Handlers(f.sheet, typeof(Control), "EventMouseClick")));

                var png = Path.Combine(HakchiEnv.NewDir(), "cover.png");
                using (var bmp = new Bitmap(120, 160)) { using (var g = Graphics.FromImage(bmp)) g.Clear(Color.OrangeRed); bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png); }
                var data = new DataObject(DataFormats.FileDrop, new[] { png });
                var inside = f.sheet.PointToScreen(ShellFixture.Center(cover));
                var outside = f.sheet.PointToScreen(new Point(cover.Right + 4, cover.Bottom + 4));
                var raise = typeof(Control).GetMethod("OnDragEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                var over = new DragEventArgs(data, 0, outside.X, outside.Y, DragDropEffects.All, DragDropEffects.All);
                raise.Invoke(f.sheet, new object[] { over });
                Assert.Equal(DragDropEffects.None, over.Effect);
                var enter = new DragEventArgs(data, 0, inside.X, inside.Y, DragDropEffects.All, DragDropEffects.All);
                raise.Invoke(f.sheet, new object[] { enter });
                Assert.Equal(DragDropEffects.All, enter.Effect);

                var dir = ((NesApplication)f.listViewGames.Items[0].Tag).BasePath;
                var stamps = Directory.GetFiles(dir, "*.png").ToDictionary(x => x, x => File.ReadAllBytes(x));
                var drop = new DragEventArgs(data, 0, inside.X, inside.Y, DragDropEffects.All, DragDropEffects.All);
                DialogGuard.Closed.Clear();
                using (DialogGuard.Start())
                    typeof(Control).GetMethod("OnDragDrop", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.sheet, new object[] { drop });
                Assert.Empty(DialogGuard.Closed);
                var changed = Directory.GetFiles(dir, "*.png").Where(x => !stamps.ContainsKey(x) || !stamps[x].SequenceEqual(File.ReadAllBytes(x))).ToList();
                Assert.NotEmpty(changed);
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaTheory]
        [InlineData(96)]
        [InlineData(144)]
        public void SheetFieldsHaveNoHiddenRowGap(int dpi)
        {
            var f = MainFormEnv.Create(MainFormEnv.Games(4), dpi, new Size(1440, 900));
            try
            {
                f.listViewGames.Items[0].Selected = true;
                f.ShowSelected();
                f.PerformLayout();
                Application.DoEvents();
                var t = (TableLayoutPanel)Field(f, "tableLayoutPanelGameInfo");
                var rows = t.Controls.Cast<Control>().Where(MainFormEnv.OwnVisible)
                    .GroupBy(c => t.GetRow(c)).OrderBy(g => g.Key)
                    .Select(g => new { Row = g.Key, Top = g.Min(c => c.Top), Bottom = g.Max(c => c.Bottom), Names = string.Join(",", g.Select(c => c.Name + c.Bounds + c.Margin)) }).ToList();
                float s = dpi / 96f;
                var gaps = new List<string>();
                for (int i = 1; i < rows.Count; i++)
                {
                    int gap = rows[i].Top - rows[i - 1].Bottom;
                    if (gap > 24 * s) gaps.Add("lignes " + rows[i - 1].Row + " -> " + rows[i].Row + " : " + gap + " px (" + rows[i - 1].Names + " / " + rows[i].Names + ")");
                }
                // les marges posees sous les listes deroulantes ne s'accumulent pas (ancien trou sous Genre)
                foreach (var c in t.Controls.OfType<ComboBox>())
                    if (c.Margin.Bottom > 8 * s) gaps.Add(c.Name + " marge basse " + c.Margin.Bottom);
                Assert.True(gaps.Count == 0, string.Join("\n", gaps));
            }
            finally { MainFormEnv.Close(f); }
        }

        [StaFact]
        public void NavHeaderLabelsCollectionComboAndSheetScrollsDark()
        {
            var f = MainFormEnv.Create(MainFormEnv.Games(2));
            try
            {
                var group = (GroupBox)Field(f, "groupBoxCurrentGamesCollection");
                var combo = (ComboBox)Field(f, "gamesConsoleComboBox");
                Assert.Equal(group.Text, f.shellCollectionCaption.Text);
                Assert.True(MainFormEnv.Shown(f.shellCollectionCaption));
                Assert.Same(combo.Parent, f.shellCollectionCaption.Parent);
                Assert.True(f.shellCollectionCaption.Bottom <= combo.Top);
                Assert.True(f.shellCollectionCaption.Height >= f.shellCollectionCaption.GetPreferredSize(Size.Empty).Height);
                // le titre est peint en TextFaint sur Slate
                using (var bmp = ShellFixture.Draw(f.shellCollectionCaption))
                {
                    bool faint = false;
                    for (int x = 0; x < bmp.Width && !faint; x++)
                        for (int y = 0; y < bmp.Height && !faint; y++)
                            faint = bmp.GetPixel(x, y).ToArgb() == com.clusterrr.hakchi_gui.Theme.B1Palette.TextFaint.ToArgb();
                    Assert.True(faint, "titre non peint en TextFaint");
                }

                // barres de defilement sombres sur le panneau des champs de la fiche (theme pose a la creation du handle)
                var nt = typeof(MainForm).Assembly.GetType("com.clusterrr.hakchi_gui.Theme.NativeTheme", true);
                var hooked = nt.GetField("hooked", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var args = new object[] { f.sheetEditPanel, null };
                Assert.True((bool)hooked.GetType().GetMethod("TryGetValue").Invoke(hooked, args));
                var keys = ((System.Collections.IDictionary)args[1]).Keys.Cast<string>().ToList();
                Assert.Contains("darkscroll", keys);
                // et sur chaque zone de texte multiligne de la fiche (description, ligne de commande...)
                foreach (var tb in All(f.sheetEditPanel).OfType<TextBoxBase>().Where(x => x.Multiline))
                {
                    var targs = new object[] { tb, null };
                    Assert.True((bool)hooked.GetType().GetMethod("TryGetValue").Invoke(hooked, targs), tb.Name);
                    var tkeys = ((System.Collections.IDictionary)targs[1]).Keys.Cast<string>().ToList();
                    Assert.True(tkeys.Contains("darkscroll") || tkeys.Contains("b1scroll"), tb.Name);
                }
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
        internal static void Check(Control parent, List<string> problems)
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
