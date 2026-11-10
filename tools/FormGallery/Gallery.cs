using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace FormGallery
{
    internal sealed class Entry
    {
        public string Name;
        public string Type;
        public string Kind;
        public string Status; // rendered | skipped
        public string Reason;
        public string Constructor;
        public string Png;
        public int Width;
        public int Height;
        public string Dpi;
        public List<string> DismissedDialogs = new List<string>();
    }

    internal static class Gallery
    {
        public static int Run(Options o)
        {
            Directory.CreateDirectory(o.Out);
            var culture = string.IsNullOrEmpty(o.Culture) ? CultureInfo.InvariantCulture : new CultureInfo(o.Culture);
            Thread.CurrentThread.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            if (string.Equals(o.Only, ThemeSample.Name, StringComparison.OrdinalIgnoreCase)) return RunThemeSample(o);
            if (string.Equals(o.Only, ControlsBoard.Name, StringComparison.OrdinalIgnoreCase)) return RunControlsBoard(o);
            if (string.Equals(o.Only, CoverGridSample.Name, StringComparison.OrdinalIgnoreCase)) return CoverGridSample.Run(o);
            if (string.Equals(o.Only, ShellSample.Name, StringComparison.OrdinalIgnoreCase)) return ShellSample.Run(o);

            var entries = new List<Entry>();
            var types = Host.UiTypes().Where(t => o.Only == null || string.Equals(t.Name, o.Only, StringComparison.OrdinalIgnoreCase)).ToList();
            if (types.Count == 0) { Console.Error.WriteLine("Aucune fenetre ne correspond a --only " + o.Only); return 3; }

            foreach (var t in types)
            {
                var e = new Entry { Name = t.Name, Type = t.FullName, Kind = Host.KindOf(t), Dpi = o.Dpi.ToString(CultureInfo.InvariantCulture) };
                Watchdog.TakeDismissed();
                Control c = null;
                try
                {
                    string ctor, reason;
                    c = Host.Construct(t, out ctor, out reason);
                    if (c == null)
                    {
                        e.Status = "skipped";
                        e.Reason = "construction impossible sans console / etat reel : " + reason;
                    }
                    else
                    {
                        e.Constructor = ctor;
                        Render(c, o, Path.Combine(o.Out, t.Name + ".png"), e);
                        e.Status = "rendered";
                        e.Png = t.Name + ".png";
                    }
                }
                catch (Exception ex)
                {
                    var inner = ex is System.Reflection.TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                    e.Status = "skipped";
                    e.Reason = "rendu impossible : " + inner.GetType().Name + " : " + inner.Message.Split('\n')[0].Trim();
                }
                finally
                {
                    try { if (c != null) c.Dispose(); } catch { }
                }
                e.DismissedDialogs = Watchdog.TakeDismissed();
                if (e.DismissedDialogs.Count > 0) e.Reason = (e.Reason == null ? "" : e.Reason + " | ") + "dialogue(s) ferme(s) par le garde-fou : " + string.Join("; ", e.DismissedDialogs);
                Console.WriteLine((e.Status == "rendered" ? "OK    " : "SKIP  ") + e.Kind.PadRight(11) + e.Name + (e.Reason != null ? "  -- " + e.Reason : ""));
                entries.Add(e);
            }

            File.WriteAllText(Path.Combine(o.Out, "gallery.json"), JsonConvert.SerializeObject(entries, Formatting.Indented));
            WriteIndex(o, entries);
            Console.WriteLine("Rendues : " + entries.Count(x => x.Status == "rendered") + ", non rendues : " + entries.Count(x => x.Status != "rendered") + ", dossier : " + o.Out);
            return 0;
        }

        /// <summary>--only ThemeSample : fenetre d'essai du theme B1 (hors hakchi.exe) + un menu ouvert.</summary>
        private static int RunThemeSample(Options o)
        {
            var f = ThemeSample.Build();
            try
            {
                ThemeSample.Skin(f);
                var e = new Entry { Name = ThemeSample.Name, Type = "FormGallery.ThemeSample", Kind = "Form", Dpi = o.Dpi.ToString(CultureInfo.InvariantCulture) };
                Render(f, o, Path.Combine(o.Out, ThemeSample.Name + ".png"), e);
                Console.WriteLine("OK    " + ThemeSample.Name + " " + e.Width + "x" + e.Height + " fonte Jost : " + com.clusterrr.hakchi_gui.Theme.B1Fonts.JostLoaded);
                using (var bmp = ThemeSample.RenderMenu()) bmp.Save(Path.Combine(o.Out, ThemeSample.Name + "_menu.png"), ImageFormat.Png);
            }
            finally { try { f.Dispose(); } catch { } }
            return 0;
        }

        /// <summary>--only Controls : planche des controles B1 a 96 et 144 dpi (controls_96.png, controls_144.png, controls.png = les deux cote a cote).</summary>
        private static int RunControlsBoard(Options o)
        {
            var oo = new Options { Out = o.Out, Dpi = 96, Culture = o.Culture };      // l'echelle est celle de B1Dpi, pas de la police
            var bitmaps = new List<Bitmap>();
            try
            {
                foreach (var dpi in new[] { 96, 144 })
                {
                    float s = dpi / 96f;
                    com.clusterrr.hakchi_gui.Theme.Controls.B1Dpi.Override = s;
                    var f = ControlsBoard.Build(s);
                    try
                    {
                        ControlsBoard.Skin(f);
                        var png = Path.Combine(o.Out, ControlsBoard.Name + "_" + dpi + ".png");
                        var e = new Entry { Name = ControlsBoard.Name, Dpi = dpi.ToString(CultureInfo.InvariantCulture) };
                        // jamais affichee : une fenetre visible est plafonnee a la hauteur de l ecran, pas une capture
                        f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-30000, -30000);
                        var want = (Size)f.Tag; int bw = want.Width, bh = want.Height;      // avant la creation du handle : la fenetre serait plafonnee
                        CreateHandles(f);
                        f.PerformLayout();
                        using (var bmp = new Bitmap(bw, bh, PixelFormat.Format32bppArgb))
                        {
                            using (var g = Graphics.FromImage(bmp))
                            {
                                g.Clear(f.BackColor);
                                for (int i = f.Controls.Count - 1; i >= 0; i--)      // les controles un par un : la fenetre est plafonnee a la hauteur de l ecran
                                {
                                    var c = f.Controls[i];
                                    if (c.Width <= 0 || c.Height <= 0) continue;
                                    using (var tmp = new Bitmap(c.Width, c.Height, PixelFormat.Format32bppArgb))
                                    {
                                        c.DrawToBitmap(tmp, new Rectangle(0, 0, c.Width, c.Height));
                                        g.DrawImageUnscaled(tmp, c.Location);
                                    }
                                }
                            }
                            bmp.Save(png, ImageFormat.Png);
                            e.Width = bw; e.Height = bh;
                        }
                        Console.WriteLine("OK    " + ControlsBoard.Name + " " + dpi + " dpi " + e.Width + "x" + e.Height + " fonte Jost : " + com.clusterrr.hakchi_gui.Theme.B1Fonts.JostLoaded);
                        using (var fs = File.OpenRead(png)) using (var img = Image.FromStream(fs)) bitmaps.Add(new Bitmap(img));
                    }
                    finally { try { f.Dispose(); } catch { } com.clusterrr.hakchi_gui.Theme.Controls.B1Dpi.Override = 0f; }
                }
                using (var both = ControlsBoard.Compose(bitmaps[0], bitmaps[1])) both.Save(Path.Combine(o.Out, "controls.png"), ImageFormat.Png);
            }
            finally { foreach (var b in bitmaps) b.Dispose(); }
            return 0;
        }

        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOWNOACTIVATE = 4;

        // Evenements de Form dont les gestionnaires parlent a la console / au reseau / a la config (Load, Shown, fermeture...).
        private static readonly string[] StrippedFormEvents =
        {
            "EVENT_LOAD", "EVENT_SHOWN", "EVENT_ACTIVATED", "EVENT_DEACTIVATE",
            "EVENT_CLOSING", "EVENT_CLOSED", "EVENT_FORMCLOSING", "EVENT_FORMCLOSED",
        };

        private static void StripHandlers(Form f)
        {
            var events = (EventHandlerList)typeof(Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(f, null);
            foreach (var name in StrippedFormEvents)
            {
                var fi = typeof(Form).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
                if (fi != null) events[fi.GetValue(null)] = null;
            }
        }

        /// <summary>
        /// La fenetre est affichee hors ecran, sans activation et SANS ses gestionnaires Load/Shown/Closing :
        /// elle peint toute sa mise en page (menus, listes...) mais n'execute aucune logique applicative
        /// (connexion console, mise a jour, chargement des jeux). Elle est donc rendue dans son etat "vide" de conception.
        /// </summary>
        internal static void Render(Control c, Options o, string png, Entry e)
        {
            Form host = c as Form;
            Form wrapper = null;
            if (host == null)
            {
                var size = (c.Width < 10 || c.Height < 10) ? new Size(420, 320) : c.Size;
                wrapper = new Form { FormBorderStyle = FormBorderStyle.None, ClientSize = size };
                c.Dock = DockStyle.Fill;
                wrapper.Controls.Add(c);
                host = wrapper;
            }
            try
            {
                StripHandlers(host);
                host.StartPosition = FormStartPosition.Manual;
                host.Location = new Point(-30000, -30000);
                host.ShowInTaskbar = false;

                ApplyDpi(host, o.Dpi);
                Watchdog.Own(host.Handle);
                CreateHandles(host);
                ShowWindow(host.Handle, SW_SHOWNOACTIVATE);
                host.PerformLayout();

                int w = Math.Min(Math.Max(host.Width, 1), 4000);
                int hgt = Math.Min(Math.Max(host.Height, 1), 4000);
                using (var bmp = new Bitmap(w, hgt, PixelFormat.Format32bppArgb))
                {
                    host.DrawToBitmap(bmp, new Rectangle(0, 0, w, hgt));
                    bmp.Save(png, ImageFormat.Png);
                }
                e.Width = w;
                e.Height = hgt;
            }
            finally
            {
                if (wrapper != null) { try { wrapper.Dispose(); } catch { } }
            }
        }

        /// <summary>Un Form jamais affiche n'a pas de handle pour ses enfants : sans eux DrawToBitmap ne dessine que le cadre.</summary>
        private static void CreateHandles(Control c)
        {
            var h = c.Handle;
            foreach (Control child in c.Controls) CreateHandles(child);
        }

        /// <summary>
        /// Emulation du DPI : le processus n'est pas "per monitor", donc on met a l'echelle les polices
        /// (AutoScaleMode.Font remet alors toute la mise en page a l'echelle) ou, a defaut, les tailles.
        /// </summary>
        private static void ApplyDpi(Control c, int dpi)
        {
            if (dpi == 96) return;
            float k = dpi / 96f;
            var scaled = new Font(c.Font.FontFamily, c.Font.Size * k, c.Font.Style, c.Font.Unit);
            var cc = c as ContainerControl;
            if (cc != null && cc.AutoScaleMode == AutoScaleMode.Font)
            {
                c.Font = scaled;
            }
            else
            {
                c.Scale(new SizeF(k, k));
                c.Font = scaled;
            }
        }

        private static void WriteIndex(Options o, List<Entry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Galerie hakchi</title>");
            sb.AppendLine("<style>body{font:14px Segoe UI,sans-serif;background:#f4f4f4;margin:16px}h1{font-size:18px}" +
                          ".g{display:flex;flex-wrap:wrap;gap:16px}.c{background:#fff;border:1px solid #ccc;padding:8px}" +
                          ".c img{display:block;max-width:560px;max-height:420px;border:1px solid #ddd}.c b{display:block;margin-bottom:6px}" +
                          "table{border-collapse:collapse}td,th{border:1px solid #ccc;padding:3px 8px;text-align:left;vertical-align:top}</style></head><body>");
            sb.AppendLine("<h1>Galerie hakchi - " + WebUtility.HtmlEncode(o.Culture) + " - " + o.Dpi + " dpi - " + entries.Count(x => x.Status == "rendered") + " rendues, " + entries.Count(x => x.Status != "rendered") + " non rendues</h1>");
            sb.AppendLine("<div class=\"g\">");
            foreach (var e in entries.Where(x => x.Status == "rendered"))
                sb.AppendLine("<div class=\"c\"><b>" + WebUtility.HtmlEncode(e.Name) + " <small>(" + e.Kind + ", " + e.Width + "x" + e.Height + ")</small></b><a href=\"" + e.Png + "\"><img src=\"" + e.Png + "\"></a></div>");
            sb.AppendLine("</div>");
            var skipped = entries.Where(x => x.Status != "rendered").ToList();
            if (skipped.Count > 0)
            {
                sb.AppendLine("<h1>Non rendues</h1><table><tr><th>Fenetre</th><th>Type</th><th>Raison</th></tr>");
                foreach (var e in skipped)
                    sb.AppendLine("<tr><td>" + WebUtility.HtmlEncode(e.Name) + "</td><td>" + e.Kind + "</td><td>" + WebUtility.HtmlEncode(e.Reason ?? "") + "</td></tr>");
                sb.AppendLine("</table>");
            }
            sb.AppendLine("</body></html>");
            File.WriteAllText(Path.Combine(o.Out, "index.html"), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
