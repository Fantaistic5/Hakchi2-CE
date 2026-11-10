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
        private readonly HttpCache _http;
        private readonly Func<string, string> _datUrlForSystem;
        private readonly ConcurrentDictionary<string, Lazy<Task<LibretroDatIndex>>> _indexes =
            new ConcurrentDictionary<string, Lazy<Task<LibretroDatIndex>>>();

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

        // Un seul chargement par système (single-flight) ; une entrée échouée est retirée pour permettre un nouvel essai ; un DAT absent (404) est mémorisé pour la durée de vie de la source.
        private async Task<LibretroDatIndex> GetIndexAsync(string sys, CancellationToken ct)
        {
            var lazy = _indexes.GetOrAdd(sys, k => new Lazy<Task<LibretroDatIndex>>(() => LoadIndexAsync(k, ct)));
            LibretroDatIndex idx;
            try
            {
                idx = await lazy.Value.ConfigureAwait(false);
            }
            catch
            {
                Evict(sys, lazy);
                throw;
            }
            return idx;
        }

        private void Evict(string sys, Lazy<Task<LibretroDatIndex>> lazy)
        {
            ((ICollection<KeyValuePair<string, Lazy<Task<LibretroDatIndex>>>>)_indexes)
                .Remove(new KeyValuePair<string, Lazy<Task<LibretroDatIndex>>>(sys, lazy));
        }

        private async Task<LibretroDatIndex> LoadIndexAsync(string sys, CancellationToken ct)
        {
            string url = _datUrlForSystem(sys);
            if (string.IsNullOrEmpty(url)) return null;
            string text = await _http.GetTextAsync(url, ct).ConfigureAwait(false);
            if (text == null) return null;
            using (var reader = new StringReader(text))
                return LibretroDatIndex.Parse(reader);
        }

        private async Task<CoverCandidate> CheckAsync(string sys, string gameName, MatchKind kind, CancellationToken ct)
        {
            string url = LibretroNames.BoxartUrl(sys, gameName);
            if (!await _http.ExistsAsync(url, ct).ConfigureAwait(false)) return null;
            return new CoverCandidate
            {
                Url = url,
                SourceName = Name,
                Region = LibretroNames.RegionTags(gameName).FirstOrDefault(),
                Match = kind,
                MatchedName = gameName
            };
        }

        // Rang de préférence : région demandée, Europe, USA, puis le reste.
        private static int RegionRank(string name, string preferred)
        {
            var tags = LibretroNames.RegionTags(name).ToList();
            string[] order = { preferred, "Europe", "USA" };
            for (int i = 0; i < order.Length; i++)
                if (tags.Any(t => t.Equals(order[i], StringComparison.OrdinalIgnoreCase))) return i;
            return order.Length;
        }
    }
}
