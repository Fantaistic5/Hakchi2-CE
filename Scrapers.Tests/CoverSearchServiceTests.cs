using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TeamShinkansen.Scrapers.Covers;
using TeamShinkansen.Scrapers.Enums;
using TeamShinkansen.Scrapers.Interfaces;
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

        private sealed class FakeImg : IScraperImage
        {
            public ArtType Type { get; set; }
            public string Url { get; set; }
            public string Description { get { return null; } }
        }

        private sealed class FakeData : IScraperData
        {
            public string ID { get { return "1"; } }
            public string Name { get; set; }
            public string Description { get { return null; } }
            public string[] Publishers { get { return null; } }
            public string[] Developers { get { return null; } }
            public string Platform { get; set; }
            public DateTime ReleaseDate { get { return DateTime.MinValue; } }
            public string Copyright { get { return null; } }
            public IScraperGenre[] Genres { get { return null; } }
            public int PlayerCount { get { return 1; } }
            public IScraperImage[] Images { get; set; }
        }

        private static IScraperData D(string platform, string url)
        {
            return new FakeData { Name = "G", Platform = platform, Images = new IScraperImage[] {
                new FakeImg { Type = ArtType.Front, Url = url }, new FakeImg { Type = ArtType.Back, Url = url + "b" } } };
        }

        [Fact]
        public async Task PlatformMapRejectsWrongPlatform()
        {
            var data = new[] { D("Sony Playstation 2", "ps2"), D("Sony Playstation", "ps1") };
            var src = new TheGamesDbCoverSource("k", t => Task.FromResult<IEnumerable<IScraperData>>(data));
            var r = await src.FindAsync(new GameQuery { Title = "G", System = "Sony - PlayStation" }, CancellationToken.None);
            Assert.Equal(new[] { "ps1" }, r.Select(c => c.Url).ToArray());
            Assert.All(r, c => Assert.Equal(MatchKind.Title, c.Match));

            var only = new[] { D("Sega 32X", "x") };
            var src2 = new TheGamesDbCoverSource("k", t => Task.FromResult<IEnumerable<IScraperData>>(only));
            Assert.Empty(await src2.FindAsync(new GameQuery { Title = "G", System = "Nintendo - Super Nintendo Entertainment System" }, CancellationToken.None));
            Assert.Empty(await src2.FindAsync(new GameQuery { Title = "G", System = "Unknown - Thing" }, CancellationToken.None));

            var arc = new[] { D("Arcade", "arc") };
            var src3 = new TheGamesDbCoverSource("k", t => Task.FromResult<IEnumerable<IScraperData>>(arc));
            Assert.Single(await src3.FindAsync(new GameQuery { Title = "G", System = "", Core = "km_fbneo" }, CancellationToken.None));
            Assert.Single(await src3.FindAsync(new GameQuery { Title = "G", System = "CP System II" }, CancellationToken.None));
        }

        [Fact]
        public async Task TheGamesDbCallsAreSerialized()
        {
            int active = 0, max = 0;
            var src = new TheGamesDbCoverSource("k", async t =>
            {
                int a = Interlocked.Increment(ref active);
                int m;
                while ((m = max) < a && Interlocked.CompareExchange(ref max, a, m) != m) { }
                await Task.Delay(50);
                Interlocked.Decrement(ref active);
                return new[] { D("Super Nintendo (SNES)", "u" + t) };
            });
            var q = new GameQuery { Title = "A", System = "Nintendo - Super Nintendo Entertainment System" };
            var q2 = new GameQuery { Title = "B", System = "Nintendo - Super Nintendo Entertainment System" };
            var res = await Task.WhenAll(src.FindAsync(q, CancellationToken.None), src.FindAsync(q2, CancellationToken.None));
            Assert.Equal(1, max);
            Assert.Equal("uA", res[0][0].Url);
            Assert.Equal("uB", res[1][0].Url);
        }
    }
}
