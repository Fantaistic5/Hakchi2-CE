using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TeamShinkansen.Scrapers.Covers;
using TeamShinkansen.Scrapers.Interfaces;
using TeamShinkansen.Scrapers.TheGamesDB;
using Xunit;

namespace Scrapers.Tests
{
    /// <summary>Saute le test sauf si HAKCHI_LIVE=1 (acces reseau reel).</summary>
    public sealed class LiveFactAttribute : FactAttribute
    {
        public LiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("HAKCHI_LIVE") != "1")
                Skip = "Test reseau reel : definir HAKCHI_LIVE=1 pour l'executer.";
        }
    }

    /// <summary>
    /// Essai reel des sources libretro + TheGamesDB sur les metadata.json de la copie de travail.
    /// Aucune ecriture dans les dossiers de jeux. Ecrit docs/superpowers/notes/covers-run-1.md.
    /// La cle TheGamesDB est lue au runtime dans hakchi_gui/ApiKeys/TheGamesDB.txt et n'est jamais ecrite.
    /// </summary>
    public class LiveCoverRunTests
    {
        private sealed class Game
        {
            public string Code, Title, System, Core, RomFile;
            public uint Crc;
            public bool Custom;
        }

        private static string FindRepoRoot()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "hakchi_gui.sln"))) d = d.Parent;
            if (d == null) throw new InvalidOperationException("racine du depot introuvable");
            return d.FullName;
        }

        private static List<Game> LoadGames(string gamesDir)
        {
            var list = new List<Game>();
            foreach (var dir in Directory.GetDirectories(gamesDir).OrderBy(x => x, StringComparer.Ordinal))
            {
                string meta = Path.Combine(dir, "metadata.json");
                string code = Path.GetFileName(dir);
                string desktop = Path.Combine(dir, code + ".desktop");
                if (!File.Exists(meta) || !File.Exists(desktop)) continue;
                var g = new Game { Code = code };
                using (var doc = JsonDocument.Parse(File.ReadAllText(meta)))
                {
                    var r = doc.RootElement;
                    g.System = r.TryGetProperty("System", out var s) ? s.GetString() : null;
                    g.Core = r.TryGetProperty("Core", out var c) ? c.GetString() : null;
                    g.RomFile = r.TryGetProperty("OriginalFilename", out var f) ? f.GetString() : null;
                    g.Crc = r.TryGetProperty("OriginalCrc32", out var k) && k.ValueKind == JsonValueKind.Number ? k.GetUInt32() : 0;
                    g.Custom = r.TryGetProperty("CustomCoverArt", out var cc) && cc.ValueKind == JsonValueKind.True;
                }
                foreach (var line in File.ReadLines(desktop))
                    if (line.StartsWith("Name=", StringComparison.Ordinal)) { g.Title = line.Substring(5).Trim(); break; }
                if (string.IsNullOrEmpty(g.Title)) continue;
                list.Add(g);
            }
            return list;
        }

        private static GameQuery Q(Game g)
        {
            return new GameQuery { System = g.System, Core = g.Core, Crc32 = g.Crc, RomFileName = g.RomFile, Title = g.Title, PreferredRegion = "Europe" };
        }

        private static string Kind(string system, string core)
        {
            string s = string.IsNullOrWhiteSpace(system) ? "(" + core + ")" : system;
            return s;
        }

        [LiveFact]
        [Trait("Category", "Live")]
        public async Task RunOnWorkingCopy()
        {
            string root = FindRepoRoot();
            string gamesDir = Environment.GetEnvironmentVariable("HAKCHI_GAMES_DIR") ?? @"P:\HakchiSNESUSA\app\games";
            Assert.True(Directory.Exists(gamesDir), "dossier de jeux introuvable : " + gamesDir);

            string keyFile = Path.Combine(root, "hakchi_gui", "ApiKeys", "TheGamesDB.txt");
            string key = File.Exists(keyFile) ? File.ReadAllText(keyFile).Trim() : "";

            var all = LoadGames(gamesDir);
            var target = all.Where(g => !g.Custom).ToList();
            var others = all.Where(g => g.Custom).Where((g, i) => i % 9 == 0).ToList();
            var games = target.Concat(others).ToList();

            string cacheDir = Path.Combine(Path.GetTempPath(), "hakchi-covers-live-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var http = new HttpCache(cacheDir);
            var libretro = new LibretroThumbnailsSource(http, LibretroNames.DatUrl);
            var tgdb = new TheGamesDbCoverSource(key);
            var service = new CoverSearchService(new ICoverSource[] { libretro, tgdb });

            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(12)))
            {
                var ct = cts.Token;

                // 1. orchestrateur (libretro puis TheGamesDB) : meilleur resultat
                var proposals = await service.SearchManyAsync(games.Select(Q).ToList(), null, ct);

                // 2. chaque source seule, pour les comptes par source
                var libRes = new Dictionary<Game, IReadOnlyList<CoverCandidate>>();
                var tgRes = new Dictionary<Game, IReadOnlyList<CoverCandidate>>();
                foreach (var g in games)
                {
                    try { libRes[g] = await libretro.FindAsync(Q(g), ct); } catch (Exception ex) { libRes[g] = new List<CoverCandidate>(); Console.WriteLine("libretro erreur " + g.Code + " : " + ex.GetType().Name); }
                    if (tgdb.IsAvailable)
                    {
                        try { tgRes[g] = await tgdb.FindAsync(Q(g), ct); } catch (Exception ex) { tgRes[g] = new List<CoverCandidate>(); Console.WriteLine("tgdb erreur " + g.Code + " : " + ex.GetType().Name); }
                    }
                }

                // 3. verification des valeurs Platform de TheGamesDB
                var platformRows = new List<string>();
                var mismatch = new List<string>();
                if (tgdb.IsAvailable)
                {
                    var scraper = new Scraper { ApiKey = key };
                    var seenSystems = new HashSet<string>();
                    foreach (var g in games)
                    {
                        string sk = Kind(g.System, g.Core);
                        if (!seenSystems.Add(sk)) continue;
                        var accepted = TheGamesDbCoverSource.AcceptedPlatforms(g.System, g.Core);
                        if (accepted == null) { platformRows.Add($"| {sk} | (aucune plateforme TGDB mappee) | | |"); continue; }
                        IScraperResult res;
                        try { res = await scraper.GetInfoByName(g.Title); } catch (Exception ex) { platformRows.Add($"| {sk} | erreur {ex.GetType().Name} | | |"); continue; }
                        var items = res == null || res.Items == null ? new List<IScraperData>() : res.Items.ToList();
                        var platforms = items.Select(i => i.Platform).Where(p => p != null).Distinct().ToList();
                        int kept = TheGamesDbCoverSource.FilterByPlatform(g.System, g.Core, items).Count();
                        platformRows.Add($"| {sk} | {string.Join(" ; ", accepted)} | {g.Title} -> {items.Count} resultats, plateformes vues : {string.Join(" ; ", platforms)} | {kept} gardes |");
                        if (items.Count > 0 && kept == 0 && platforms.Count > 0) mismatch.Add(sk + " / " + g.Title + " : " + string.Join(" ; ", platforms));
                    }
                }

                // ---- rapport ----
                var sb = new StringBuilder();
                sb.AppendLine("# Essai reel des sources de jaquettes (covers-run-1)");
                sb.AppendLine();
                sb.AppendLine("- Date : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                sb.AppendLine("- Genere par `Scrapers.Tests/LiveCoverRunTests.cs` (`HAKCHI_LIVE=1`, categorie `Live`), sans aucune ecriture dans les dossiers de jeux.");
                sb.AppendLine("- Jeux : " + games.Count + " (" + target.Count + " sans CustomCoverArt + " + others.Count + " echantillon parmi ceux qui en ont une).");
                sb.AppendLine("- Sources essayees : libretro-thumbnails, TheGamesDB (" + (tgdb.IsAvailable ? "cle presente" : "cle absente, source inactive") + "). **Google non essayee** : `GoogleCoverSource` vit dans hakchi_gui (net48, depend de ImageGoogler), hors de ce test.");
                sb.AppendLine();

                sb.AppendLine("## Resultats de l'orchestrateur (libretro puis TheGamesDB)");
                sb.AppendLine();
                Func<IEnumerable<Game>, string> summarize = set =>
                {
                    var props = set.Select(g => proposals[games.IndexOf(g)]).ToList();
                    int found = props.Count(p => p.Best != null);
                    var bySource = props.Where(p => p.Best != null).GroupBy(p => p.Best.SourceName).Select(x => x.Key + " " + x.Count());
                    var byKind = props.Where(p => p.Best != null).GroupBy(p => p.Best.Match).Select(x => x.Key + " " + x.Count());
                    int auto = props.Count(p => p.AutoAccept);
                    return $"{props.Count} jeux : {found} trouves ({string.Join(", ", bySource)}) ; type de correspondance : {string.Join(", ", byKind)} ; cochees d'office : {auto} ; non trouves : {props.Count - found}";
                };
                sb.AppendLine("- Les " + target.Count + " jeux sans jaquette : " + summarize(target));
                sb.AppendLine("- Echantillon de jeux deja illustres : " + summarize(others));
                sb.AppendLine();

                sb.AppendLine("## Par source (chaque source interrogee seule, sur les " + games.Count + " jeux)");
                sb.AppendLine();
                Action<string, Dictionary<Game, IReadOnlyList<CoverCandidate>>> perSource = (name, dict) =>
                {
                    int hits = dict.Count(kv => kv.Value.Count > 0);
                    var kinds = dict.Where(kv => kv.Value.Count > 0).GroupBy(kv => kv.Value[0].Match).Select(x => x.Key + " " + x.Count());
                    sb.AppendLine($"- {name} : {hits}/{games.Count} jeux avec au moins un candidat ; meilleur candidat par type : {string.Join(", ", kinds)}");
                };
                perSource("libretro-thumbnails", libRes);
                if (tgdb.IsAvailable) perSource("TheGamesDB", tgRes);
                sb.AppendLine();
                sb.AppendLine("Par systeme (libretro / TGDB, jeux avec candidat sur total) :");
                sb.AppendLine();
                sb.AppendLine("| Systeme | Jeux | libretro | TGDB |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var grp in games.GroupBy(g => Kind(g.System, g.Core)).OrderBy(x => x.Key))
                {
                    int l = grp.Count(g => libRes[g].Count > 0);
                    string t = tgdb.IsAvailable ? grp.Count(g => tgRes[g].Count > 0).ToString() : "-";
                    sb.AppendLine($"| {grp.Key} | {grp.Count()} | {l} | {t} |");
                }
                sb.AppendLine();

                sb.AppendLine("## Non trouves par l'orchestrateur");
                sb.AppendLine();
                var nf = games.Where(g => proposals[games.IndexOf(g)].Best == null).ToList();
                if (nf.Count == 0) sb.AppendLine("Aucun.");
                foreach (var g in nf)
                {
                    var p = proposals[games.IndexOf(g)];
                    sb.AppendLine($"- {g.Code} « {g.Title} » ({Kind(g.System, g.Core)}, rom `{g.RomFile}`){(p.Error != null ? " — erreur : " + p.Error : "")}{(g.Custom ? " [avait deja une jaquette]" : "")}");
                }
                sb.AppendLine();

                sb.AppendLine("## Exemples a verifier a l'oeil (URL des jaquettes proposees)");
                sb.AppendLine();
                var picks = new List<Game>();
                Func<Func<Game, bool>, Game> firstFound = pred => games.FirstOrDefault(g => pred(g) && proposals[games.IndexOf(g)].Best != null && !picks.Contains(g));
                Action<Game> add = g => { if (g != null) picks.Add(g); };
                add(firstFound(g => (g.System ?? "").Contains("Arcade") || (g.System ?? "").StartsWith("CP System") || (g.Core ?? "").StartsWith("km_fb")));
                add(firstFound(g => (g.System ?? "").Contains("PlayStation") && !(g.System ?? "").Contains("Portable")));
                add(firstFound(g => (g.System ?? "").Contains("Super Nintendo")));
                add(firstFound(g => (g.System ?? "").Contains("Mega Drive")));
                add(firstFound(g => (g.System ?? "").Contains("Game Boy Advance") || (g.System ?? "").Contains("Nintendo 64") || (g.System ?? "").Contains("Portable")));
                foreach (var g in games)
                {
                    if (picks.Count >= 7) break;
                    if (!picks.Contains(g) && proposals[games.IndexOf(g)].Best != null) picks.Add(g);
                }
                foreach (var g in picks)
                {
                    var p = proposals[games.IndexOf(g)];
                    sb.AppendLine($"### {g.Title} ({Kind(g.System, g.Core)})");
                    sb.AppendLine();
                    sb.AppendLine($"- Meilleur : {p.Best.SourceName} · {p.Best.Region ?? "sans region"} · {p.Best.Match} · nom trouve « {p.Best.MatchedName} »");
                    sb.AppendLine($"- URL : {p.Best.Url}");
                    sb.AppendLine($"- Cochee d'office : {(p.AutoAccept ? "oui" : "non")} ; alternatives : {p.Alternatives.Count}");
                    sb.AppendLine();
                }

                sb.AppendLine("## Verification des valeurs `Platform` de TheGamesDB");
                sb.AppendLine();
                if (!tgdb.IsAvailable) sb.AppendLine("Non faite : cle absente.");
                else
                {
                    sb.AppendLine("| Systeme hakchi | Plateformes acceptees (carte) | Requete | Filtre |");
                    sb.AppendLine("|---|---|---|---|");
                    foreach (var r in platformRows) sb.AppendLine(r);
                    sb.AppendLine();
                    sb.AppendLine(mismatch.Count == 0
                        ? "Aucune divergence : chaque systeme mappe qui a renvoye des resultats en a garde au moins un apres filtrage."
                        : "Divergences (resultats presents mais tous filtres) :\n\n" + string.Join("\n", mismatch.Select(m => "- " + m)));
                }

                string note = Path.Combine(root, "docs", "superpowers", "notes", "covers-run-1.md");
                Directory.CreateDirectory(Path.GetDirectoryName(note));
                string text = sb.ToString();
                Assert.False(key.Length > 0 && text.Contains(key), "cle presente dans le rapport"); // la cle ne doit jamais atterrir dans le rapport
                File.WriteAllText(note, text, new UTF8Encoding(false));
            }
            try { Directory.Delete(cacheDir, true); } catch { }
        }
    }
}
