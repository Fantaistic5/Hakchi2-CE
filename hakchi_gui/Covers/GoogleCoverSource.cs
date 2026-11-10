using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamShinkansen.Scrapers.Covers;

namespace com.clusterrr.hakchi_gui.Covers
{
    /// <summary>
    /// Derniere source : recherche Google Images (meme requete que ImageGooglerForm).
    /// Toujours disponible ; resultats de confiance minimale (MatchKind.Title, sans region).
    /// </summary>
    public sealed class GoogleCoverSource : ICoverSource
    {
        private const int MaxResults = 12;

        public string Name { get { return "google"; } }
        public bool IsAvailable { get { return true; } }

        public async Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct)
        {
            var result = new List<CoverCandidate>();
            if (q == null || string.IsNullOrWhiteSpace(q.Title)) return result;
            ct.ThrowIfCancellationRequested();

            string query = ImageGooglerForm.BuildQuery(q.Title, GoogleSuffixFor(q));
            string[] urls;
            try
            {
                // l'appel Google est bloquant : hors du thread UI
                urls = await Task.Run(() => Controls.ImageGoogler.GetImageUrls(query), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("GoogleCoverSource: " + ex.Message);
                return result;
            }
            ct.ThrowIfCancellationRequested();

            if (urls == null) return result;
            foreach (var url in urls)
            {
                if (string.IsNullOrEmpty(url)) continue;
                result.Add(new CoverCandidate
                {
                    Url = url,
                    SourceName = Name,
                    Region = null,
                    Match = MatchKind.Title,
                    MatchedName = q.Title
                });
                if (result.Count >= MaxResults) break;
            }
            return result;
        }

        private static string GoogleSuffixFor(GameQuery q)
        {
            return AppTypeCollection.GetAppBySystem(q.System ?? string.Empty).GoogleSuffix;
        }
    }
}
