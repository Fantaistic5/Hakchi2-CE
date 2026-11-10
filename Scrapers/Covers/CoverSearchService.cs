using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TeamShinkansen.Scrapers.Covers
{
    public sealed class CoverProposal
    {
        public GameQuery Query;
        public CoverCandidate Best;
        public IReadOnlyList<CoverCandidate> Alternatives;
        /// <summary>true seulement si Best.Match est Crc ou RomName.</summary>
        public bool AutoAccept;
        /// <summary>Non null si toutes les sources ont echoue (aucun candidat et au moins une erreur).</summary>
        public string Error;
    }

    public sealed class CoverSearchService
    {
        private const int MaxParallelGames = 4;
        private readonly List<ICoverSource> _sources;

        public CoverSearchService(IEnumerable<ICoverSource> orderedSources)
        {
            if (orderedSources == null) throw new ArgumentNullException("orderedSources");
            _sources = orderedSources.ToList();
        }

        public async Task<CoverProposal> SearchAsync(GameQuery q, CancellationToken ct)
        {
            if (q == null) throw new ArgumentNullException("q");
            var all = new List<CoverCandidate>();
            CoverCandidate exact = null;
            string lastError = null;

            foreach (var source in _sources)
            {
                ct.ThrowIfCancellationRequested();
                if (!source.IsAvailable) continue;

                IReadOnlyList<CoverCandidate> found;
                try
                {
                    found = await source.FindAsync(q, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = source.Name + " : " + ex.Message;
                    continue;
                }
                if (found == null) continue;

                foreach (var c in found)
                {
                    if (c == null || string.IsNullOrEmpty(c.Url)) continue;
                    if (exact == null && (c.Match == MatchKind.Crc || c.Match == MatchKind.RomName))
                        exact = c;
                    all.Add(c);
                }
                if (exact != null) break;
            }

            var proposal = new CoverProposal { Query = q };
            CoverCandidate best = exact ?? all.FirstOrDefault();
            proposal.Best = best;
            proposal.AutoAccept = best != null && (best.Match == MatchKind.Crc || best.Match == MatchKind.RomName);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var alternatives = new List<CoverCandidate>();
            if (best != null) seen.Add(best.Url);
            foreach (var c in all)
                if (seen.Add(c.Url)) alternatives.Add(c);
            proposal.Alternatives = alternatives;

            if (best == null) proposal.Error = lastError;
            return proposal;
        }

        public async Task<IReadOnlyList<CoverProposal>> SearchManyAsync(
            IEnumerable<GameQuery> qs, IProgress<int> progress, CancellationToken ct)
        {
            if (qs == null) throw new ArgumentNullException("qs");
            var queries = qs.ToList();
            var results = new CoverProposal[queries.Count];
            int done = 0;

            using (var gate = new SemaphoreSlim(MaxParallelGames))
            {
                var tasks = new List<Task>();
                for (int i = 0; i < queries.Count; i++)
                {
                    int index = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        await gate.WaitAsync(ct).ConfigureAwait(false);
                        try
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                // Meme jeton pour tous : le DAT libretro partage est lie au 1er appelant.
                                results[index] = await SearchAsync(queries[index], ct).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                results[index] = new CoverProposal
                                {
                                    Query = queries[index],
                                    Alternatives = new List<CoverCandidate>(),
                                    Error = ex.Message
                                };
                            }
                            int n = Interlocked.Increment(ref done);
                            if (progress != null) progress.Report(n);
                        }
                        finally
                        {
                            gate.Release();
                        }
                    }));
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            return results;
        }
    }
}
