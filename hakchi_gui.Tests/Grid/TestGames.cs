using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;

namespace hakchi_gui.Tests.Grid
{
    /// <summary>
    /// Environnement minimal de hakchi pour construire des NesApplication dans les tests :
    /// Program.BaseDirectoryInternal pointe sur la sortie msbuild (data\desktop_entries.tar, lu par le constructeur
    /// statique de NesApplication ; sinon il affiche une ErrorForm modale), BaseDirectoryExternal sur un dossier jetable,
    /// supprime a la fin du processus (voir TempCleanup).
    /// </summary>
    internal static class HakchiEnv
    {
        private static readonly object gate = new object();
        private static bool done;
        public static string Temp;
        public const string TempPrefix = "hakchi_grid_tests_";

        public static void Init()
        {
            lock (gate)
            {
                if (done) return;
                var prog = typeof(NesApplication).Assembly.GetType("com.clusterrr.hakchi_gui.Program", true);
                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                prog.GetField("BaseDirectoryInternal", flags).SetValue(null, Path.Combine(Repo.Root, "hakchi_gui", "bin", "Debug", "net48"));
                // dossiers d'anciens processus d'abord, puis le notre, supprime a la sortie (jonctions retirees avant le reste)
                TempCleanup.DeleteStale(Path.GetTempPath(), TempPrefix);
                Temp = Path.Combine(Path.GetTempPath(), TempPrefix + Process.GetCurrentProcess().Id);
                Directory.CreateDirectory(Temp);
                TempCleanup.Register(Temp);
                prog.GetField("BaseDirectoryExternal", flags).SetValue(null, Temp);
                done = true;
            }
        }

        public static string NewDir()
        {
            Init();
            var d = Path.Combine(Temp, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(d);
            return d;
        }
    }

    /// <summary>Jeu de test : un NesApplication sans fichiers, dont on choisit le nom, la jaquette et le statut « jaquette perso ».</summary>
    internal sealed class TestApp : NesApplication
    {
        public int LoaderThread;
        public TimeSpan LoadDelay;

        public TestApp(string name, string coverPath, bool customCover, string system = "Nintendo - Super Nintendo Entertainment System")
        {
            desktop.Name = name;
            desktop.Code = "CLV-T-" + name.GetHashCode().ToString("X8");
            iconPath = coverPath ?? Path.Combine(HakchiEnv.Temp ?? Path.GetTempPath(), "absent_" + Guid.NewGuid().ToString("N") + ".png");
            Metadata.CustomCoverArt = customCover;
            Metadata.System = system;
        }

        public override Image Image
        {
            get
            {
                LoaderThread = Thread.CurrentThread.ManagedThreadId;
                if (LoadDelay > TimeSpan.Zero) Thread.Sleep(LoadDelay);
                return base.Image;
            }
            set { base.Image = value; }
        }

        public static TestApp WithCover(string name, Color color, int w = 150, int h = 200)
        {
            var dir = HakchiEnv.NewDir();
            var path = Path.Combine(dir, "cover.png");
            using (var bmp = new Bitmap(w, h))
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(color);
                bmp.Save(path, ImageFormat.Png);
            }
            return new TestApp(name, path, true);
        }

        public static TestApp Corrupt(string name)
        {
            var dir = HakchiEnv.NewDir();
            var path = Path.Combine(dir, "cover.png");
            File.WriteAllBytes(path, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 }); // faux PNG de 10 octets
            return new TestApp(name, path, true);
        }
    }

    internal static class GridFixture
    {
        /// <summary>Fenetre jamais affichee, ListView source invisible (comme listViewGames dans la nouvelle fenetre), handles crees.</summary>
        public static Form Host(out ListView source, IList<NesApplication> apps, Func<int, bool> isChecked = null)
        {
            HakchiEnv.Init();
            // xUnit pose son propre contexte sur le thread de test ; l'application, elle, tourne sous celui de WinForms
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            var f = new Form { ClientSize = new Size(1000, 700), StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ShowInTaskbar = false };
            source = new ListView { View = View.Details, CheckBoxes = true, HideSelection = false, Visible = false };
            source.Columns.Add("Nom", 200);
            for (int i = 0; i < apps.Count; i++)
                source.Items.Add(new ListViewItem(apps[i].ToString()) { Tag = apps[i], Checked = isChecked != null && isChecked(i) });
            f.Controls.Add(source);
            var h = f.Handle;
            return f;
        }

        public static List<NesApplication> Apps(int n, Func<int, bool> custom = null)
        {
            HakchiEnv.Init();
            var list = new List<NesApplication>();
            for (int i = 0; i < n; i++)
                list.Add(new TestApp("Jeu " + i.ToString("000"), null, custom == null || custom(i)));
            return list;
        }

        /// <summary>Pompe les messages jusqu'a ce que la condition soit vraie (ou expiration).</summary>
        public static bool PumpUntil(Func<bool> done, int timeoutMs = 5000)
        {
            var sw = Stopwatch.StartNew();
            while (!done())
            {
                if (sw.ElapsedMilliseconds > timeoutMs) return false;
                Application.DoEvents();
                Thread.Sleep(2);
            }
            return true;
        }

        public static void Invoke(Control c, string method, params object[] args)
        {
            var m = typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            m.Invoke(c, args);
        }
    }
}
