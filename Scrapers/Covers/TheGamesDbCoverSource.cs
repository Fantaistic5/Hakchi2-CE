using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamShinkansen.Scrapers.Enums;
using TeamShinkansen.Scrapers.Interfaces;
using TeamShinkansen.Scrapers.TheGamesDB;

namespace TeamShinkansen.Scrapers.Covers
{
    /// <summary>Enveloppe du scraper TheGamesDB existant : recherche par nom, images ArtType.Front, candidats Title.</summary>
    public sealed class TheGamesDbCoverSource : ICoverSource
    {
        private static readonly Dictionary<string, string[]> PlatformMap =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Nintendo - Super Nintendo Entertainment System", new[] { "Super Nintendo (SNES)", "Super Famicom" } },
            { "Nintendo - Nintendo Entertainment System", new[] { "Nintendo Entertainment System (NES)", "Famicom" } },
            { "Nintendo - Nintendo 64", new[] { "Nintendo 64" } },
            { "Nintendo - Game Boy Advance", new[] { "Nintendo Game Boy Advance" } },
            { "Nintendo - Game Boy", new[] { "Nintendo Game Boy" } },
            { "Nintendo - Game Boy Color", new[] { "Nintendo Game Boy Color" } },
            { "Sega - Mega Drive - Genesis", new[] { "Sega Genesis", "Sega Mega Drive" } },
            { "Sega - Master System - Mark III", new[] { "Sega Master System" } },
            { "Sony - PlayStation", new[] { "Sony Playstation" } },
            { "Sony - PlayStation Portable", new[] { "Sony Playstation Portable", "Sony PSP" } },
            { "The 3DO Company - 3DO", new[] { "3DO" } },
            { "NEC - PC Engine CD - TurboGrafx-CD", new[] { "TurboGrafx CD", "PC Engine CD" } },
        };

        private static readonly string[] ArcadeNames = { "Arcade", "Neo Geo" };

        private readonly string _apiKey;
        private readonly Func<string, Task<IEnumerable<IScraperData>>> _fetch;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public TheGamesDbCoverSource(string apiKey)
        {
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            if (_apiKey != null)
            {
                var scraper = new Scraper { ApiKey = _apiKey };
                _fetch = async title =>
                {
                    IScraperResult r = await scraper.GetInfoByName(title).ConfigureAwait(false);
                    return r == null ? null : r.Items;
                };
            }
        }

        /// <summary>Test seam : remplace l'appel reseau.</summary>
        internal TheGamesDbCoverSource(string apiKey, Func<string, Task<IEnumerable<IScraperData>>> fetch)
        {
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
            _fetch = fetch;
        }

        public string Name { get { return "thegamesdb"; } }
        public bool IsAvailable { get { return _apiKey != null && _fetch != null; } }

        public async Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct)
        {
            var list = new List<CoverCandidate>();
            if (!IsAvailable || q == null || string.IsNullOrWhiteSpace(q.Title)) return list;
            if (AcceptedPlatforms(q.System, q.Core) == null) return list;
            ct.ThrowIfCancellationRequested();

            IEnumerable<IScraperData> items;
            // Le scraper partage des caches non thread-safe et TGDB limite le debit : appels serialises.
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                items = await _fetch(q.Title).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
            ct.ThrowIfCancellationRequested();
            if (items == null) return list;

            foreach (var item in FilterByPlatform(q.System, q.Core, items))
            {
                if (item.Images == null) continue;
                foreach (var img in item.Images)
                {
                    if (img == null || img.Type != ArtType.Front || string.IsNullOrEmpty(img.Url)) continue;
                    list.Add(new CoverCandidate
                    {
                        Url = img.Url,
                        SourceName = Name,
                        Region = null,
                        Match = MatchKind.Title,
                        MatchedName = item.Name
                    });
                }
            }
            return list;
        }

        internal static string[] AcceptedPlatforms(string system, string core)
        {
            string s = (system ?? "").Trim();
            if (s.Length == 0)
            {
                string c = (core ?? "").Trim();
                if (c.StartsWith("km_fbalpha", StringComparison.OrdinalIgnoreCase) ||
                    c.StartsWith("km_fbneo", StringComparison.OrdinalIgnoreCase))
                    return ArcadeNames;
                return null;
            }
            string[] names;
            if (PlatformMap.TryGetValue(s, out names)) return names;
            if (s.Equals("FBA - Arcade Games", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("FB Alpha - Arcade Games", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("CP System", StringComparison.OrdinalIgnoreCase))
                return ArcadeNames;
            return null;
        }

        internal static IEnumerable<IScraperData> FilterByPlatform(string system, string core, IEnumerable<IScraperData> items)
        {
            var accepted = AcceptedPlatforms(system, core);
            if (accepted == null || items == null) return Enumerable.Empty<IScraperData>();
            return items.Where(i => i != null && i.Platform != null &&
                accepted.Any(a => string.Equals(a, i.Platform.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
        }
    }
}
