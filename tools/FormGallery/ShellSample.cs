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
    /// --only Shell : la coque de la tache 4 (TopBar + SystemNavList + grille + GameSheetPanel + StorageBar) avec de vrais jeux de
    /// la copie de test (lecture seule). Shell.png = fenetre a 66 % de stockage, avec en dessous le pied de page a 105 % (Error).
    /// </summary>
    internal static class ShellSample
    {
        public const string Name = "Shell";
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const long GB = 1024L * 1024 * 1024;

        private static Form Build(IList<NesApplication> apps, NesApplication current, long used, long total, out CoverGridView grid, out ListView src)
        {
            var f = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-30000, -30000),
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.None,
                BackColor = B1Palette.Slate,
                ClientSize = new Size(1180, 740),
            };
            src = new ListView { View = View.Details, CheckBoxes = true, HideSelection = false, Visible = false };
            src.Columns.Add("Nom", 200);
            src.Items.AddRange(apps.Select((a, i) => new ListViewItem(a.ToString()) { Tag = a, Checked = i % 3 != 1 }).ToArray());
            f.Controls.Add(src);

            // ordre d'ajout = inverse de l'empilement des Dock (le dernier ajoute est range en premier)
            grid = new CoverGridView { Dock = DockStyle.Fill };
            f.Controls.Add(grid);

            var sheet = new GameSheetPanel { Dock = DockStyle.Right, Width = (int)(1180 * 0.27) };
            f.Controls.Add(sheet);

            var nav = new SystemNavList { Dock = DockStyle.Left, Width = (int)(1180 * 0.22) };
            var bySystem = apps.GroupBy(GameFilter.SystemOf).Where(g => g.Key.Length > 0).OrderByDescending(g => g.Count());
            nav.SetSystems(bySystem.Select(g => (g.Key, g.Count())));
            nav.SetCollectionCount(GameCollection.NoCover, apps.Count(CoverTargets.NeedsCover));
            f.Controls.Add(nav);

            var foot = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = B1Palette.Slate };
            var rule = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = B1Palette.Rule };
            var storage = new StorageBar { Dock = DockStyle.Fill };
            storage.SetUsage(used, total);
            var sync = new B1Button { Text = "Synchroniser", Kind = B1ButtonKind.Primary, Dock = DockStyle.Right, Width = 130 };
            var find = new B1Button { Text = "Chercher " + apps.Count(CoverTargets.NeedsCover) + " jaquettes", Kind = B1ButtonKind.Secondary, Dock = DockStyle.Right, Width = 190 };
            var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 12, 18, 12), BackColor = B1Palette.Slate };
            var row = new Panel { Dock = DockStyle.Fill, BackColor = B1Palette.Slate };
            row.Controls.Add(storage);
            row.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 18, BackColor = B1Palette.Slate });
            row.Controls.Add(find);
            row.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10, BackColor = B1Palette.Slate });
            row.Controls.Add(sync);
            pad.Controls.Add(row);
            foot.Controls.Add(pad);
            foot.Controls.Add(rule);
            f.Controls.Add(foot);

            var bar = new TopBar { Dock = DockStyle.Top };
            foreach (var t in new[] { "Jeux", "Mods", "Pochettes", "Outils" })
            {
                bar.AddMenu(t).DropDownItems.Add(t + " : entree");
            }
            bar.Badge.SetState(true, "SNES Mini EUR", "1.0.4");
            f.Controls.Add(bar);

            if (current != null) sheet.Show(current);
            return f;
        }

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

        private static Bitmap Draw(Control c)
        {
            var bmp = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppArgb);
            c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
            return bmp;
        }

        public static int Run(Options o)
        {
            var apps = CoverGridSample.LoadGames(70).GroupBy(a => a.Code).Select(g => g.First()).ToList();
            var current = apps.FirstOrDefault(a => !CoverTargets.NeedsCover(a) && !string.IsNullOrEmpty(a.Metadata == null ? null : a.Metadata.CoverSource))
                          ?? apps.FirstOrDefault(a => !CoverTargets.NeedsCover(a)) ?? apps.First();
            Console.WriteLine("fiche : " + current.Name + " (source " + (current.Metadata.CoverSource ?? "null") + ")");

            var shots = new List<Bitmap>();
            try
            {
                foreach (var pct in new[] { 66, 105 })
                {
                    CoverGridView grid; ListView src;
                    var f = Build(apps, current, 6L * GB + GB / 10, pct == 66 ? 9L * GB + GB * 4 / 10 : 5L * GB + GB * 8 / 10, out grid, out src);
                    try
                    {
                        var h = f.Handle;
                        ShowWindow(f.Handle, 4);      // hors ecran (-30000) : les enfants ne se dessinent que dans une fenetre affichee
                        f.PerformLayout();
                        grid.Bind(src);
                        src.Items[Math.Max(0, apps.IndexOf(current))].Selected = true;
                        Application.DoEvents();
                        using (Draw(f)) { }
                        Pump(() => grid.AllVisibleThumbnailsReady, 20000);
                        Pump(() => f.Controls.OfType<GameSheetPanel>().First().CoverReady, 10000);
                        shots.Add(Draw(f));
                    }
                    finally { f.Dispose(); }
                }
                // fenetre a 66 % ; en dessous, le pied de page a 105 %
                int footH = 52;
                using (var all = new Bitmap(shots[0].Width, shots[0].Height + footH + 8))
                using (var g = Graphics.FromImage(all))
                {
                    g.Clear(Color.FromArgb(0x10, 0x10, 0x14));
                    g.DrawImageUnscaled(shots[0], 0, 0);
                    g.DrawImage(shots[1], new Rectangle(0, shots[0].Height + 8, shots[1].Width, footH),
                        new Rectangle(0, shots[1].Height - footH, shots[1].Width, footH), GraphicsUnit.Pixel);
                    var png = Path.Combine(o.Out, Name + ".png");
                    all.Save(png, ImageFormat.Png);
                    Console.WriteLine("OK    " + Name + " " + all.Width + "x" + all.Height + " fonte Jost : " + B1Fonts.JostLoaded);
                }
            }
            finally { foreach (var b in shots) b.Dispose(); }
            return 0;
        }
    }
}
