using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Covers;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;

namespace FormGallery
{
    /// <summary>
    /// --only CoverGrid : grille de jaquettes (tache 3) sur de vrais jeux de la copie de test (lecture seule), branchee sur une
    /// ListView invisible comme listViewGames. Sorties : CoverGrid.png (96 dpi, ~40 jeux dont des sans jaquette, un selectionne,
    /// des coches), CoverGrid_144.png (echelle 1,5 emulee), et la mesure de perf sur ~257 jeux (CoverGrid_perf.txt).
    /// </summary>
    internal static class CoverGridSample
    {
        public const string Name = "CoverGrid";

        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private static List<NesApplication> LoadGames(int count)
        {
            var dir = Host.SamplesSource;
            var dirs = Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.Ordinal).ToList();
            var apps = new List<NesApplication>();
            foreach (var d in dirs)
            {
                try { apps.Add(NesApplication.FromDirectory(d)); }
                catch (Exception ex) { Console.WriteLine("jeu ignore " + d + " : " + ex.Message); }
            }
            // au-dela des jeux disponibles : on recharge les memes dossiers (nouvelles instances, donc vrais decodages)
            int n = apps.Count;
            for (int i = 0; apps.Count < count && n > 0; i++)
                apps.Add(NesApplication.FromDirectory(dirs[i % dirs.Count]));
            return apps.Take(count).ToList();
        }

        private static Form Build(IList<NesApplication> apps, Size gridSize, out CoverGridView grid, out ListView source, Func<int, bool> isChecked)
        {
            var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-30000, -30000),
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.None,
                BackColor = B1Palette.Slate,
                ClientSize = gridSize,
            };
            source = new ListView { View = View.Details, CheckBoxes = true, HideSelection = false, Visible = false };
            source.Columns.Add("Nom", 200);
            source.Items.AddRange(apps.Select((a, i) => new ListViewItem(a.ToString()) { Tag = a, Checked = isChecked(i) }).ToArray());
            f.Controls.Add(source);
            grid = new CoverGridView { Bounds = new Rectangle(Point.Empty, gridSize) };
            f.Controls.Add(grid);
            return f;
        }

        private static bool Pump(Func<bool> done, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (!done())
            {
                if (sw.ElapsedMilliseconds > timeoutMs) return false;
                Application.DoEvents();
                System.Threading.Thread.Sleep(1);
            }
            return true;
        }

        private static Bitmap Draw(Control c)
        {
            var bmp = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppArgb);
            c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
            return bmp;
        }

        public static int Run(Options o)
        {
            var all = LoadGames(257);
            Console.WriteLine("jeux charges : " + all.Count + " depuis " + Host.SamplesSource + " ; sans jaquette : " + all.Count(CoverTargets.NeedsCover));

            // --- mesure d'abord (fichiers pas encore lus par la planche) : 257 jeux, fenetre type 1180 x 760 ---
            var report = new List<string>();
            report.Add("Grille de jaquettes : " + all.Count + " jeux de " + Host.SamplesSource + " (" + all.Count(CoverTargets.NeedsCover) + " sans jaquette), zone 1180x760, 96 dpi, " + Environment.ProcessorCount + " coeurs");
            for (int run = 1; run <= 2; run++)
            {
                // nouvelles instances a chaque passe : rien en cache cote grille (le cache disque de Windows, lui, est chaud a la 2e passe)
                var apps = run == 1 ? all : LoadGames(257);
                CoverGridView grid; ListView src;
                var f = Build(apps, new Size(1180, 760), out grid, out src, i => i % 2 == 0);
                try
                {
                    var h = f.Handle;
                    ShowWindow(f.Handle, 4);
                    // handle de la ListView source (257 LVM_INSERTITEM) : cout qui existe deja aujourd'hui (listViewGames visible),
                    // mesure a part ; la grille ne fait que le forcer quand la liste devient invisible
                    var sw0 = Stopwatch.StartNew();
                    h = src.Handle;
                    long tList = sw0.ElapsedMilliseconds;
                    var sw = Stopwatch.StartNew();
                    grid.Bind(src);
                    h = grid.Handle;
                    using (Draw(grid)) { }                 // premier rendu : demande les vignettes visibles
                    long firstPaint = sw.ElapsedMilliseconds;
                    bool ok = Pump(() => grid.AllVisibleThumbnailsReady, 20000);
                    using (Draw(grid)) { }                 // rendu complet
                    long visibleReady = sw.ElapsedMilliseconds;
                    int first, last; grid.GridLayout.VisibleRange(0, grid.ClientSize.Height, out first, out last);
                    // tout le reste (defilement jusqu'en bas)
                    var size = new Size(grid.GridLayout.CellWidth, grid.GridLayout.CoverHeight);
                    var want = apps.Where(a => !CoverTargets.NeedsCover(a)).ToList();
                    foreach (var a in want) grid.Cache.Get(a, size, null);
                    bool ok2 = Pump(() => grid.Cache.PendingCount == 0, 60000);
                    long allReady = sw.ElapsedMilliseconds;
                    var line = string.Format(CultureInfo.InvariantCulture,
                        "passe {0} ({1}) : [handle de la ListView source {10} ms, hors mesure] premier rendu {2} ms ; {3} jeux visibles ({4} colonnes) prets en {5} ms{6} ; les {7} vignettes decodees en {8} ms{9}",
                        run, run == 1 ? "disque froid" : "disque chaud", firstPaint, last - first + 1, grid.GridLayout.Columns, visibleReady, ok ? "" : " (EXPIRE)",
                        want.Count, allReady, ok2 ? "" : " (EXPIRE)", tList);
                    report.Add(line);
                    Console.WriteLine("PERF  " + line);
                }
                finally { f.Dispose(); }
            }
            File.WriteAllLines(Path.Combine(o.Out, Name + "_perf.txt"), report);

            // --- planche : ~40 jeux, dont tous les sans-jaquette, un selectionne, des coches ---
            var unique = all.GroupBy(a => a.Code).Select(g => g.First()).ToList();
            var noCover = unique.Where(CoverTargets.NeedsCover).Take(6).ToList();
            var sample = unique.Where(a => !CoverTargets.NeedsCover(a)).Take(40 - noCover.Count).ToList();
            for (int i = 0; i < noCover.Count; i++) sample.Insert(Math.Min(sample.Count, 2 + i * 7), noCover[i]);
            foreach (var dpi in new[] { 96, 144 })
            {
                float s = dpi / 96f;
                B1Dpi.Override = s;
                CoverGridView grid; ListView src;
                var f = Build(sample, new Size((int)(1000 * s), (int)(1500 * s)), out grid, out src, i => i % 3 != 1);
                try
                {
                    var h = f.Handle;
                    grid.Bind(src);
                    h = grid.Handle;
                    src.Items[0].Selected = true;
                    grid.Focus();
                    using (Draw(grid)) { }
                    if (!Pump(() => grid.AllVisibleThumbnailsReady, 20000)) Console.WriteLine("ATTENTION : vignettes pas toutes pretes");
                    using (var bmp = Draw(grid))
                    {
                        var png = Path.Combine(o.Out, Name + (dpi == 96 ? "" : "_" + dpi) + ".png");
                        bmp.Save(png, ImageFormat.Png);
                        Console.WriteLine("OK    " + Name + " " + dpi + " dpi " + bmp.Width + "x" + bmp.Height + " (" + grid.VisibleCount + " jeux, " + grid.GridLayout.Columns + " colonnes) fonte Jost : " + B1Fonts.JostLoaded);
                    }
                }
                finally { f.Dispose(); B1Dpi.Override = 0f; }
            }

            return 0;
        }
    }
}
