using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TeamShinkansen.Scrapers.Covers
{
    public sealed class LibretroThumbnailsSource : ICoverSource
    {
        private const string FbNeo = "FBNeo - Arcade Games";
        private static readonly string[] KnownRegions =
        {
            "USA", "Europe", "Japan", "World", "Germany", "France", "Spain", "Italy", "Australia", "Korea",
            "Brazil", "China", "Asia", "Taiwan", "Netherlands", "Sweden", "Canada", "Russia", "Hong Kong",
            "Denmark", "Finland", "Norway", "Portugal", "Greece", "Scandinavia", "Mexico", "Argentina"
        };

        private readonly HttpCache _http;
        private readonly Func<string, string> _datUrlForSystem;
        private readonly ConcurrentDictionary<string, LibretroDatIndex> _indexes =
            new ConcurrentDictionary<string, LibretroDatIndex>();

        public LibretroThumbnailsSource(HttpCache http, Func<string, string> datUrlForSystem)
        {
            if (http == null) throw new ArgumentNullException("http");
            if (datUrlForSystem == null) throw new ArgumentNullException("datUrlForSystem");
            _http = http;
            _datUrlForSystem = datUrlForSystem;
        }

        public string Name { get { return "libretro-thumbnails"; } }
        public bool IsAvailable { get { return true; } }

        public async Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct)
        {
            var result = new List<CoverCandidate>();
            string sys = LibretroNames.ThumbnailSystem(q.System, q.Core);
            if (sys == null) return result;

            var index = await GetIndexAsync(sys, ct).ConfigureAwait(false);
            if (index == null) return result;

            // (1) CRC32
            if (q.Crc32 != 0)
            {
                string name = index.FindByCrc(q.Crc32);
                if (name != null)
                {
                    var c = await CheckAsync(sys, name, MatchKind.Crc, ct).ConfigureAwait(false);
                    if (c != null) { result.Add(c); return result; }
                }
            }

            // (2) arcade : nom du ROM set
            if (sys == FbNeo && !string.IsNullOrEmpty(q.RomFileName))
            {
                string name = index.FindByRomSet(q.RomFileName);
                if (name != null)
                {
                    var c = await CheckAsync(sys, name, MatchKind.RomName, ct).ConfigureAwait(false);
                    if (c != null) { result.Add(c); return result; }
                }
            }

            // (3) titre
            var wanted = new HashSet<string>();
            AddWanted(wanted, q.Title);
            if (!string.IsNullOrEmpty(q.RomFileName))
                AddWanted(wanted, Path.GetFileNameWithoutExtension(q.RomFileName));
            if (wanted.Count == 0) return result;

            string pref = string.IsNullOrEmpty(q.PreferredRegion) ? "Europe" : q.PreferredRegion;
            var matches = index.Names.Distinct(StringComparer.Ordinal)
                .Where(n => wanted.Contains(Normalize(n)))
                .Select((n, i) => new { Name = n, Rank = RegionRank(n, pref), Order = i })
                .OrderBy(x => x.Rank).ThenBy(x => x.Order)
                .ToList();
            foreach (var m in matches)
            {
                var c = await CheckAsync(sys, m.Name, MatchKind.Title, ct).ConfigureAwait(false);
                if (c != null) result.Add(c);
            }
            return result;
        }

        private static void AddWanted(HashSet<string> set, string s)
        {
            string n = Normalize(s);
            if (n.Length > 0) set.Add(n);
        }

        private static string Normalize(string s)
        {
            return LibretroNames.NormalizeTitle((s ?? string.Empty).Replace('_', ' '));
        }

        private async Task<LibretroDatIndex> GetIndexAsync(string sys, CancellationToken ct)
        {
            LibretroDatIndex idx;
            if (_indexes.TryGetValue(sys, out idx)) return idx;
            string url = _datUrlForSystem(sys);
            if (string.IsNullOrEmpty(url)) return null;
            string text = await _http.GetTextAsync(url, ct).ConfigureAwait(false);
            if (text == null) return null;
            using (var reader = new StringReader(text))
                idx = LibretroDatIndex.Parse(reader);
            _indexes[sys] = idx;
            return idx;
        }

        private async Task<CoverCandidate> CheckAsync(string sys, string gameName, MatchKind kind, CancellationToken ct)
        {
            string url = LibretroNames.BoxartUrl(sys, gameName);
            if (!await _http.ExistsAsync(url, ct).ConfigureAwait(false)) return null;
            return new CoverCandidate
            {
                Url = url,
                SourceName = Name,
                Region = RegionTags(gameName).FirstOrDefault(),
                Match = kind,
                MatchedName = gameName
            };
        }

        // Rang de préférence : région demandée, Europe, USA, puis le reste.
        private static int RegionRank(string name, string preferred)
        {
            var tags = RegionTags(name).ToList();
            string[] order = { preferred, "Europe", "USA" };
            for (int i = 0; i < order.Length; i++)
                if (tags.Any(t => t.Equals(order[i], StringComparison.OrdinalIgnoreCase))) return i;
            return order.Length;
        }

        // Tags de région connus, dans l'ordre d'apparition, ex. "(USA, Europe)" -> USA, Europe.
        private static IEnumerable<string> RegionTags(string name)
        {
            int pos = 0;
            while (name != null && pos < name.Length)
            {
                int open = name.IndexOf('(', pos);
                if (open < 0) yield break;
                int close = name.IndexOf(')', open);
                if (close < 0) yield break;
                foreach (var part in name.Substring(open + 1, close - open - 1).Split(','))
                {
                    string t = part.Trim();
                    string known = KnownRegions.FirstOrDefault(r => r.Equals(t, StringComparison.OrdinalIgnoreCase));
                    if (known != null) yield return known;
                }
                pos = close + 1;
            }
        }
    }
}
