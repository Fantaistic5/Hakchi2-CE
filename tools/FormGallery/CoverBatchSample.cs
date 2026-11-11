using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using com.clusterrr.hakchi_gui;
using TeamShinkansen.Scrapers.Covers;

namespace FormGallery
{
    /// <summary>
    /// CoverBatchForm peuplee pour la galerie : jeux factices (aucun fichier), source de recherche et telechargements factices
    /// (jamais le reseau), application sans ecriture. Rend les etats « trouvee », « a verifier », « non trouvee », « erreur reseau »
    /// et « appliquee », une ligne selectionnee et ses candidats.
    /// </summary>
    internal static class CoverBatchSample
    {
        /// <summary>Jeu sans fichier : nom choisi, aucune jaquette perso (propose d'office), jaquette absente.</summary>
        private sealed class SampleApp : NesApplication
        {
            public SampleApp(string name, string code)
            {
                desktop.Name = name;
                desktop.Code = code;
                iconPath = Path.Combine(Path.GetTempPath(), "hakchi_gallery_absent_" + code + ".png");
                Metadata.CustomCoverArt = false;
                Metadata.System = "Nintendo - Super Nintendo Entertainment System";
            }
        }

        private sealed class SampleSource : ICoverSource
        {
            public string Name { get { return "libretro-thumbnails"; } }
            public bool IsAvailable { get { return true; } }

            public Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct)
            {
                IReadOnlyList<CoverCandidate> none = new CoverCandidate[0];
                switch (q.Title)
                {
                    case "Super Metroid":
                        return Task.FromResult<IReadOnlyList<CoverCandidate>>(new[] { C("sample://metroid/0", MatchKind.Crc, "libretro-thumbnails", "USA"), C("sample://metroid/1", MatchKind.Title, "TheGamesDB", "Europe"), C("sample://metroid/2", MatchKind.Title, "Google", null) });
                    case "Chrono Trigger":
                        return Task.FromResult<IReadOnlyList<CoverCandidate>>(new[] { C("sample://chrono/0", MatchKind.Title, "TheGamesDB", "Japon"), C("sample://chrono/1", MatchKind.Title, "Google", null) });
                    case "F-Zero":
                        return Task.FromResult<IReadOnlyList<CoverCandidate>>(new[] { C("sample://fzero/0", MatchKind.RomName, "libretro-thumbnails", "Europe") });
                    case "Kirby's Dream Course":
                        throw new IOException("reseau indisponible");
                    case "Super Mario Kart":
                        return Task.FromResult<IReadOnlyList<CoverCandidate>>(new[] { C("sample://kart/0", MatchKind.Crc, "libretro-thumbnails", "Europe") });
                    default:
                        return Task.FromResult(none);
                }
            }

            private static CoverCandidate C(string url, MatchKind m, string source, string region)
            {
                return new CoverCandidate { Url = url, Match = m, SourceName = source, Region = region, MatchedName = url };
            }
        }

        public static Form Build()
        {
            var games = new List<NesApplication>
            {
                new SampleApp("Super Metroid", "CLV-G-SMTRD"),
                new SampleApp("Super Mario Kart", "CLV-G-SMKRT"),
                new SampleApp("Chrono Trigger", "CLV-G-CHRNO"),
                new SampleApp("Star Ocean (traduction)", "CLV-G-STOCN"),
                new SampleApp("Kirby's Dream Course", "CLV-G-KIRBY"),
                new SampleApp("F-Zero", "CLV-G-FZERO"),
            };
            var f = new CoverBatchForm(games);
            f.ServiceFactory = () => new CoverSearchService(new ICoverSource[] { new SampleSource() });
            f.Downloader = (url, ct) => Task.FromResult(Png(url));
            f.CoverApplier = (app, img, source) => { };     // galerie : rien n'est ecrit
            return f;
        }

        /// <summary>Recherche puis application de la seule « Super Mario Kart », selection de « Super Metroid » (candidats affiches).</summary>
        public static void Populate(Control form)
        {
            var f = (CoverBatchForm)form;
            UiContext();
            f.CreateControl();          // handles crees ; Created reste faux tant que la fenetre est cachee (voir RefreshSummaryAndButtons)
            CreateHandles(f);
            var search = f.RunSearchAsync();
            Pump(() => search.IsCompleted);
            var list = (ListView)f.Controls.Find("listViewResults", true)[0];
            foreach (ListViewItem it in list.Items) it.Checked = it.Text == "Super Mario Kart";
            var apply = f.RunApplyAsync();
            Pump(() => apply.IsCompleted);
            // apres l'application : les autres propositions exactes restent a appliquer
            foreach (ListViewItem it in list.Items) if (it.Text == "Super Metroid" || it.Text == "F-Zero") it.Checked = true;
            f.RefreshSummaryAndButtons();   // ItemChecked s'arrete avant le resume tant que la fenetre n'est pas affichee
            var metroid = list.Items.Cast<ListViewItem>().First(it => it.Text == "Super Metroid");
            UiContext();
            metroid.Selected = true;
            list.Focus();
            Pump(() => f.Controls.Find("flowAlternatives", true)[0].Controls.Cast<Control>().Count(c => ((PictureBox)c).Image != null) >= 3, 3000);
            // vignettes de la liste : demandees au dessin (la fenetre n'est pas encore affichee : dessin dans une image jetable),
            // decodees en tache de fond ; on attend qu'elles soient pretes
            var deadline = Stopwatch.StartNew();
            using (var scratch = new Bitmap(Math.Max(1, list.Width), Math.Max(1, list.Height)))
                while (deadline.ElapsedMilliseconds < 1500)
                {
                    list.DrawToBitmap(scratch, new Rectangle(0, 0, scratch.Width, scratch.Height));
                    Application.DoEvents(); UiContext(); Thread.Sleep(20);
                }
        }

        private static void Pump(Func<bool> done, int timeoutMs = 10000)
        {
            var sw = Stopwatch.StartNew();
            while (!done() && sw.ElapsedMilliseconds < timeoutMs) { Application.DoEvents(); UiContext(); Thread.Sleep(5); }
        }

        /// <summary>
        /// Comme sous Application.Run : les suites des await reviennent sur ce thread. Sans boucle de messages, Application.DoEvents
        /// remet un SynchronizationContext de base (les suites partiraient sur le pool de threads) : on le reinstalle.
        /// </summary>
        private static void UiContext()
        {
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        }

        private static void CreateHandles(Control c)
        {
            var h = c.Handle;
            foreach (Control child in c.Controls) CreateHandles(child);
        }

        // Jaquettes d'exemple aux couleurs de la palette (le controle de palette de la galerie juge aussi la liste)
        private static readonly Color[] Fills =
        {
            Color.FromArgb(0x4A, 0x3C, 0x8C), Color.FromArgb(0x6B, 0x5B, 0xB5), Color.FromArgb(0x5C, 0x58, 0x70), Color.FromArgb(0x26, 0x25, 0x2E),
        };
        private static readonly Color Lavender = Color.FromArgb(0xA7, 0x9F, 0xD6), Text = Color.FromArgb(0xE4, 0xE2, 0xEA);

        private static byte[] Png(string url)
        {
            int k = Math.Abs(url.GetHashCode());
            using (var bmp = new Bitmap(300, 400))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Fills[k % Fills.Length]);
                    using (var b = new SolidBrush(Fills[(k / 7 + 1) % Fills.Length])) g.FillRectangle(b, 0, 250, 300, 150);
                    using (var p = new Pen(Lavender, 8)) g.DrawRectangle(p, 24, 24, 252, 200);
                    using (var b = new SolidBrush(Text)) g.FillRectangle(b, 40, 300, 180, 24);
                }
                using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); return ms.ToArray(); }
            }
        }
    }
}
