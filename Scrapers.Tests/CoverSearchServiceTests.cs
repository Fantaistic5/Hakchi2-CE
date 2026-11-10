using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TeamShinkansen.Scrapers.Covers;
using Xunit;

namespace Scrapers.Tests
{
    public class CoverSearchServiceTests
    {
        private sealed class FakeSource : ICoverSource
        {
            private readonly Func<GameQuery, IReadOnlyList<CoverCandidate>> _f;
            public int Calls;
            public FakeSource(string name, Func<GameQuery, IReadOnlyList<CoverCandidate>> f, bool available = true)
            {
                Name = name; _f = f; IsAvailable = available;
            }
            public string Name { get; private set; }
            public bool IsAvailable { get; private set; }
            public Task<IReadOnlyList<CoverCandidate>> FindAsync(GameQuery q, CancellationToken ct)
            {
                Interlocked.Increment(ref Calls);
                return Task.FromResult(_f(q));
            }
        }

        private static CoverCandidate C(string url, MatchKind m, string src)
        {
            return new CoverCandidate { Url = url, Match = m, SourceName = src, Region = "Europe", MatchedName = "x" };
        }

        private static IReadOnlyList<CoverCandidate> L(params CoverCandidate[] c) { return c; }

        private static GameQuery Q(string title) { return new GameQuery { Title = title, System = "SNES" }; }

        [Fact]
        public async Task StopsAtFirstExactMatch()
        {
            var s1 = new FakeSource("a", q => L(C("u1", MatchKind.Crc, "a")));
            var s2 = new FakeSource("b", q => L(C("u2", MatchKind.Title, "b")));
            var svc = new CoverSearchService(new ICoverSource[] { s1, s2 });
            var p = await svc.SearchAsync(Q("G"), CancellationToken.None);
            Assert.Equal("u1", p.Best.Url);
            Assert.True(p.AutoAccept);
            Assert.Null(p.Error);
            Assert.Equal(0, s2.Calls);
        }

        [Fact]
        public async Task TitleOnlyMatchIsNotAutoAccepted()
        {
            var s1 = new FakeSource("a", q => L(C("u1", MatchKind.Title, "a"), C("u2", MatchKind.Title, "a")));
            var s2 = new FakeSource("b", q => L(C("u2", MatchKind.Title, "b"), C("u3", MatchKind.Title, "b")));
            var svc = new CoverSearchService(new ICoverSource[] { s1, s2 });
            var p = await svc.SearchAsync(Q("G"), CancellationToken.None);
            Assert.Equal("u1", p.Best.Url);
            Assert.False(p.AutoAccept);
            Assert.Equal(new[] { "u2", "u3" }, p.Alternatives.Select(c => c.Url).ToArray());
        }

        [Fact]
        public async Task UnavailableSourceIsSkipped()
        {
            var s1 = new FakeSource("a", q => L(C("u1", MatchKind.Crc, "a")), available: false);
            var s2 = new FakeSource("b", q => L(C("u2", MatchKind.RomName, "b")));
            var svc = new CoverSearchService(new ICoverSource[] { s1, s2 });
            var p = await svc.SearchAsync(Q("G"), CancellationToken.None);
            Assert.Equal(0, s1.Calls);
            Assert.Equal("u2", p.Best.Url);
            Assert.True(p.AutoAccept);
        }

        [Fact]
        public async Task FailingSourceDoesNotStopBatch()
        {
            var s1 = new FakeSource("a", q =>
            {
                if (q.Title == "A") throw new HttpRequestException("boom");
                return L(C("uB", MatchKind.Crc, "a"));
            });
            var svc = new CoverSearchService(new ICoverSource[] { s1 });
            var progress = new List<int>();
            var res = await svc.SearchManyAsync(new[] { Q("A"), Q("B") }, new Progress<int>(i => { lock (progress) progress.Add(i); }), CancellationToken.None);
            Assert.Equal(2, res.Count);
            Assert.NotNull(res[0].Error);
            Assert.Null(res[0].Best);
            Assert.Null(res[1].Error);
            Assert.Equal("uB", res[1].Best.Url);
        }

        [Fact]
        public async Task SearchManyHonorsCancellation()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var s1 = new FakeSource("a", q => L(C("u", MatchKind.Crc, "a")));
            var svc = new CoverSearchService(new ICoverSource[] { s1 });
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                svc.SearchManyAsync(new[] { Q("A") }, null, cts.Token));
        }

        [Fact]
        public void TheGamesDbWithoutKeyIsUnavailable()
        {
            Assert.False(new TheGamesDbCoverSource(null).IsAvailable);
            Assert.False(new TheGamesDbCoverSource("  ").IsAvailable);
            Assert.True(new TheGamesDbCoverSource("k").IsAvailable);
        }

        [Fact]
        public async Task TheGamesDbWithoutKeyReturnsEmptyWithoutNetwork()
        {
            var r = await new TheGamesDbCoverSource("").FindAsync(Q("G"), CancellationToken.None);
            Assert.Empty(r);
        }
    }
}
