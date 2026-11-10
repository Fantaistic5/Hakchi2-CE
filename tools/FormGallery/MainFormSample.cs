using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using com.clusterrr.hakchi_gui.Covers;
using com.clusterrr.hakchi_gui.Theme;
using com.clusterrr.hakchi_gui.Theme.Controls;

namespace FormGallery
{
    /// <summary>
    /// --only MainForm : la VRAIE fenetre principale (tache 5), construite sans console ni gestionnaires Load/Shown, avec de vrais
    /// jeux de la copie de test (lecture seule) ranges dans listViewGames comme le ferait LoadGamesTask, un jeu choisi.
    /// La pastille console et la jauge sont posees a des valeurs de demonstration (pas de console).
    /// Sorties : MainForm.png (1440x900), MainForm_1024.png (taille minimale 1024x640), et les memes a 144 dpi emules
    /// (MainForm_144.png : a 144 dpi la taille minimale vaut 1536x960 et l'ecran borne la fenetre, une seule vue suffit).
    /// </summary>
    internal static class MainFormSample
    {
        public const string Name = "MainForm";
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const long GB = 1024L * 1024 * 1024;

        private static bool Pump(Func<bool> done, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!done())
            {
                if (sw.ElapsedMilliseconds > timeoutMs) return false;
                Application.DoEvents();
                System.Threading.Thread.Sleep(1);
            }
            return true;
        }

        public static int Run(Options o)
        {
            var apps = CoverGridSample.LoadGames(70).GroupBy(a => a.Code).Select(g => g.First()).ToList();
            var current = apps.FirstOrDefault(a => !CoverTargets.NeedsCover(a) && !string.IsNullOrEmpty(a.Metadata == null ? null : a.Metadata.CoverSource))
                          ?? apps.FirstOrDefault(a => !CoverTargets.NeedsCover(a)) ?? apps.First();
            Console.WriteLine("fiche : " + current.Name);

            var shots = new[]
            {
                new { W = 1440, H = 900, Dpi = 96, File = Name + ".png" },
                new { W = 1024, H = 640, Dpi = 96, File = Name + "_1024.png" },
                new { W = 1440, H = 900, Dpi = 144, File = Name + "_144.png" },
            };
            foreach (var shot in shots)
            {
                float s = shot.Dpi / 96f;
                B1Dpi.Override = shot.Dpi == 96 ? 0f : s;
                Watchdog.TakeDismissed();
                MainForm f = null;
                try
                {
                    f = new MainForm();
                    Gallery.StripHandlers(f);
                    f.StartPosition = FormStartPosition.Manual;
                    f.Location = new Point(-30000, -30000);
                    f.ShowInTaskbar = false;
                    Gallery.ApplyDpi(f, shot.Dpi);
                    f.Size = new Size(shot.W, shot.H);

                    var lv = f.listViewGames;
                    lv.BeginUpdate();
                    lv.Items.AddRange(apps.Select((a, i) => new ListViewItem(a.ToString()) { Tag = a, Checked = i % 3 != 1 }).ToArray());
                    lv.EndUpdate();

                    Watchdog.Own(f.Handle);
                    Gallery.CreateHandles(f);
                    ShowWindow(f.Handle, 4);
                    f.Size = new Size(shot.W, shot.H);      // apres la mise a l'echelle automatique de la police (144 dpi)
                    f.PerformLayout();

                    f.UpdateShellCounts();
                    f.storageBar.SetUsage(6L * GB + GB / 10, 9L * GB + GB * 4 / 10);
                    f.topBar.Badge.SetState(true, "SNES Mini EUR", "1.0.4");
                    foreach (ListViewItem it in lv.Items) if (it.Tag == current) { it.Selected = true; it.Focused = true; f.grid.EnsureVisible(f.grid.VisibleItems.ToList().IndexOf(it)); }
                    f.ShowSelected();
                    Application.DoEvents();
                    using (var warm = new Bitmap(f.Width, f.Height)) f.DrawToBitmap(warm, new Rectangle(0, 0, f.Width, f.Height));
                    if (!Pump(() => f.grid.AllVisibleThumbnailsReady, 20000)) Console.WriteLine("ATTENTION : vignettes pas toutes pretes");
                    Pump(() => f.sheet.CoverReady, 10000);
                    using (var bmp = new Bitmap(f.Width, f.Height, PixelFormat.Format32bppArgb))
                    {
                        f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                        bmp.Save(Path.Combine(o.Out, shot.File), ImageFormat.Png);
                    }
                    var dismissed = Watchdog.TakeDismissed();
                    Console.WriteLine("OK    " + shot.File + " " + f.Width + "x" + f.Height + " client " + f.ClientSize.Width + "x" + f.ClientSize.Height +
                                      " grille " + f.grid.Width + "x" + f.grid.Height + " (" + f.grid.VisibleCount + " jeux) fonte Jost : " + B1Fonts.JostLoaded +
                                      (dismissed.Count > 0 ? " | dialogues fermes : " + string.Join("; ", dismissed) : ""));
                }
                finally
                {
                    try { if (f != null) f.Dispose(); } catch { }
                    B1Dpi.Override = 0f;
                }
            }
            return 0;
        }
    }
}
