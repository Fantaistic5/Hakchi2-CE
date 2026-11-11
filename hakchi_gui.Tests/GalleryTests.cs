using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace hakchi_gui.Tests
{
    /// <summary>Retrouve les chemins du depot depuis le dossier de sortie des tests.</summary>
    internal static class Repo
    {
        public static string Root
        {
            get
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "hakchi_gui.sln")))
                    dir = dir.Parent;
                if (dir == null) throw new InvalidOperationException("Racine du depot introuvable");
                return dir.FullName;
            }
        }

        public static string GalleryExe => Path.Combine(Root, "tools", "FormGallery", "bin", "Debug", "net48", "FormGallery.exe");
        public static string DataDir => Path.Combine(Root, "hakchi_gui.Tests", "Data");

        /// <summary>
        /// Copie de test de hakchi (lecture seule) qui fournit les vrais jeux d'exemple : variable d'environnement
        /// HAKCHI_SAMPLES, par defaut P:\HakchiSNESUSA\app (poste de l'auteur). Les jeux sont dans son dossier games.
        /// </summary>
        public static string SamplesRoot
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("HAKCHI_SAMPLES");
                return string.IsNullOrWhiteSpace(env) ? @"P:\HakchiSNESUSA\app" : env.Trim();
            }
        }
    }

    public class GalleryTests
    {
        [Fact]
        public void GalleryRendersAtLeast30Forms()
        {
            var exe = Repo.GalleryExe;
            Assert.True(File.Exists(exe), "FormGallery.exe absent : " + exe);

            var outDir = Path.Combine(Path.GetTempPath(), "hakchi_gallery_" + Guid.NewGuid().ToString("N"));
            try
            {
                var psi = new ProcessStartInfo(exe, "--out \"" + outDir + "\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                };
                using (var p = Process.Start(psi))
                {
                    var stdout = p.StandardOutput.ReadToEndAsync();
                    var stderr = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(240000))
                    {
                        try { p.Kill(); } catch { }
                        Assert.True(false, "La galerie ne s'est pas terminee en 240 s\n" + stdout.Result);
                    }
                    Assert.True(p.ExitCode == 0, "Code de sortie " + p.ExitCode + "\n" + stdout.Result + "\n" + stderr.Result);
                }

                var pngs = Directory.GetFiles(outDir, "*.png");
                var nonEmpty = pngs.Count(f => new FileInfo(f).Length > 0);
                Assert.True(nonEmpty >= 30, "PNG non vides : " + nonEmpty + " sur " + pngs.Length);
                Assert.True(File.Exists(Path.Combine(outDir, "index.html")), "index.html manquant");
            }
            finally
            {
                try { Directory.Delete(outDir, true); } catch { }
            }
        }

        [Fact]
        public void BaselinesAreCaptured()
        {
            var menus = JArray.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "menu_items_before.json")));
            Assert.True(menus.Count >= 140, "items de menu : " + menus.Count);
            foreach (var m in menus)
            {
                Assert.False(string.IsNullOrEmpty((string)m["field"]));
                Assert.NotNull(m["textFr"]);
                Assert.NotNull(m["textInvariant"]);
                Assert.NotNull(m["owner"]);
                Assert.NotNull(m["clickHandlers"]);
            }

            var controls = JObject.Parse(File.ReadAllText(Path.Combine(Repo.DataDir, "controls_before.json")));
            Assert.True(controls.Properties().Count() >= 30, "fenetres : " + controls.Properties().Count());
            Assert.NotNull(controls["MainForm"]);
            Assert.True(((JArray)controls["MainForm"]["controls"]).Count > 20);
        }
    }
}
