using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private static readonly HashSet<string> NoiseWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "nintendo", "entertainment", "system", "the", "and", "sega", "sony", "console", "games", "game"
        };

        private readonly string _apiKey;
        private Scraper _scraper;

        public TheGamesDbCoverSource(string apiKey)
        {
            _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        }

        public string Name { get { return "thegamesdb"; } }
        public bool IsAvailable { get { return _apiKey != null; } }

        public async Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct)
        {
            var list = new List<CoverCandidate>();
            if (!IsAvailable || q == null || string.IsNullOrWhiteSpace(q.Title)) return list;
            ct.ThrowIfCancellationRequested();

            if (_scraper == null) _scraper = new Scraper { ApiKey = _apiKey };
            IScraperResult result = await _scraper.GetInfoByName(q.Title).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (result == null || result.Items == null) return list;

            var items = result.Items.Where(i => i != null).ToList();
            if (items.Count == 0) return list;

            // Le scraper ne filtre pas par plateforme : comparaison approximative, sinon on garde le 1er resultat.
            var matching = items.Where(i => PlatformMatches(q.System, i.Platform)).ToList();
            if (matching.Count == 0) matching.Add(items[0]);

            foreach (var item in matching)
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

        internal static bool PlatformMatches(string system, string platform)
        {
            if (string.IsNullOrWhiteSpace(system) || string.IsNullOrWhiteSpace(platform)) return false;
            return Tokens(system).Overlaps(Tokens(platform));
        }

        private static HashSet<string> Tokens(string s)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            foreach (char c in s + " ")
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else
                {
                    if (sb.Length >= 2 && !NoiseWords.Contains(sb.ToString())) set.Add(sb.ToString());
                    sb.Clear();
                }
            }
            return set;
        }
    }
}
