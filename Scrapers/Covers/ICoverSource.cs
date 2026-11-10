using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TeamShinkansen.Scrapers.Covers
{
    public interface ICoverSource
    {
        string Name { get; }
        /// <summary>Faux si la source est inutilisable (ex. TheGamesDB sans clé).</summary>
        bool IsAvailable { get; }
        Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct);
    }
}
