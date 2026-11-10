using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TeamShinkansen.Scrapers.Covers;
using Xunit;

namespace Scrapers.Tests
{
    public class LibretroThumbnailsSourceTests : IDisposable
    {
        private const string Snes = "Nintendo - Super Nintendo Entertainment System";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "covers-test-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            public readonly Dictionary<string, string> Texts = new Dictionary<string, string>();
            public readonly HashSet<string> Existing = new HashSet<string>();
            public int Requests;
            public int DatGets;
            public int DatDelayMs;
            public int FailDatTimes;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Interlocked.Increment(ref Requests);
                string url = request.RequestUri.AbsoluteUri;
                if (request.Method == HttpMethod.Head)
                    return new HttpResponseMessage(Existing.Contains(url) ? HttpStatusCode.OK : HttpStatusCode.NotFound);
                int n = Interlocked.Increment(ref DatGets);
                if (DatDelayMs > 0) await Task.Delay(DatDelayMs, ct);
                if (n <= FailDatTimes) throw new HttpRequestException("boom");
                string body;
                if (Texts.TryGetValue(url, out body))
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        private static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
        }

        private static string DatUrl(string sys) { return "https://dat.test/" + Uri.EscapeDataString(sys) + ".dat"; }

        private LibretroThumbnailsSource Make(FakeHandler h)
        {
            return new LibretroThumbnailsSource(new HttpCache(_dir, h), DatUrl);
        }

        private static string Box(string sys, string name) { return LibretroNames.BoxartUrl(sys, name); }

        [Fact]
        public async Task CrcMatchReturnsExactBoxart()
        {
            var h = new FakeHandler();
            string name = "Super Metroid (USA, Europe) (En,Ja) (Virtual Console, Classic Mini, Switch Online)";
            h.Texts[DatUrl(Snes)] = Fixture("snes-sample.dat");
            h.Existing.Add(Box(Snes, name));
            var res = await Make(h).FindAsync(new GameQuery { System = Snes, Crc32 = 0x45913E52, Title = "Whatever" }, CancellationToken.None);
            Assert.Single(res);
            Assert.Equal(MatchKind.Crc, res[0].Match);
            Assert.Equal(Box(Snes, name), res[0].Url);
            Assert.Equal("libretro-thumbnails", res[0].SourceName);
            Assert.Equal("USA", res[0].Region);
            Assert.Equal(name, res[0].MatchedName);
        }

        [Fact]
        public async Task ArcadeUsesRomSetName()
        {
            var h = new FakeHandler();
            string arc = "FBNeo - Arcade Games";
            string name = "The King of Fighters '98 - The Slugfest / King of Fighters '98 - Dream Match Never Ends (NGM-2420)";
            h.Texts[DatUrl(arc)] = Fixture("fbneo-sample.dat");
            h.Existing.Add(Box(arc, name));
            var res = await Make(h).FindAsync(new GameQuery { System = "CP System II", RomFileName = "kof98.zip", Title = "KOF 98" }, CancellationToken.None);
            Assert.Single(res);
            Assert.Equal(MatchKind.RomName, res[0].Match);
            Assert.Equal(Box(arc, name), res[0].Url);
        }

        [Fact]
        public async Task TitleFallbackPrefersEurope()
        {
            var h = new FakeHandler();
            string dat = "game (\n\tname \"Foo Quest (USA)\"\n\trom ( name \"a.sfc\" size 1 crc 00000001 )\n)\n"
                       + "game (\n\tname \"Foo Quest (USA)\"\n\trom ( name \"a2.sfc\" size 1 crc 00000003 )\n)\n"
                       + "game (\n\tname \"Foo Quest (Europe)\"\n\trom ( name \"b.sfc\" size 1 crc 00000002 )\n)\n";
            h.Texts[DatUrl(Snes)] = dat;
            h.Existing.Add(Box(Snes, "Foo Quest (USA)"));
            h.Existing.Add(Box(Snes, "Foo Quest (Europe)"));
            var res = await Make(h).FindAsync(new GameQuery { System = Snes, Title = "Foo_Quest" }, CancellationToken.None);
            Assert.Equal(2, res.Count);
            Assert.Equal("Europe", res[0].Region);
            Assert.Equal("USA", res[1].Region);
            Assert.All(res, c => Assert.Equal(MatchKind.Title, c.Match));
        }

        [Fact]
        public async Task MissingBoxartYieldsNoCandidate()
        {
            var h = new FakeHandler();
            h.Texts[DatUrl(Snes)] = Fixture("snes-sample.dat");
            var res = await Make(h).FindAsync(new GameQuery { System = Snes, Crc32 = 0x45913E52, Title = "Super Metroid" }, CancellationToken.None);
            Assert.Empty(res);
        }

        [Fact]
        public async Task DatIsCachedOnDisk()
        {
            var h = new FakeHandler();
            h.Texts[DatUrl(Snes)] = Fixture("snes-sample.dat");
            h.Existing.Add(Box(Snes, "Super Mario World (USA)"));
            var q = new GameQuery { System = Snes, Crc32 = 0xB19ED489 };
            var first = await Make(h).FindAsync(q, CancellationToken.None);
            Assert.Single(first);
            int after = h.Requests;
            Assert.Equal(2, after); // DAT + HEAD
            var second = await Make(h).FindAsync(q, CancellationToken.None); // nouvelle instance: cache disque
            Assert.Single(second);
            Assert.Equal(after, h.Requests);
        }

        [Fact]
        public async Task ConcurrentQueriesLoadDatOnce()
        {
            var h = new FakeHandler { DatDelayMs = 200 };
            h.Texts[DatUrl(Snes)] = Fixture("snes-sample.dat");
            var src = Make(h);
            var q = new GameQuery { System = Snes, Crc32 = 0xB19ED489 };
            var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => src.FindAsync(q, CancellationToken.None))).ToArray();
            await Task.WhenAll(tasks);
            Assert.Equal(1, h.DatGets);
        }

        [Fact]
        public async Task FailedDatLoadIsRetried()
        {
            var h = new FakeHandler { FailDatTimes = 1 };
            h.Texts[DatUrl(Snes)] = Fixture("snes-sample.dat");
            h.Existing.Add(Box(Snes, "Super Mario World (USA)"));
            var src = Make(h);
            var q = new GameQuery { System = Snes, Crc32 = 0xB19ED489 };
            await Assert.ThrowsAsync<HttpRequestException>(() => src.FindAsync(q, CancellationToken.None));
            var res = await src.FindAsync(q, CancellationToken.None);
            Assert.Single(res);
            Assert.Equal(2, h.DatGets);
        }

        [Fact]
        public async Task GetTextReturnsNullOn404()
        {
            var h = new FakeHandler();
            Assert.Null(await new HttpCache(_dir, h).GetTextAsync("https://dat.test/none.dat", CancellationToken.None));
        }

        [Fact]
        public void DatUrlRoutesBySystemFamily()
        {
            const string b = "https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/";
            Assert.Equal(b + "fbneo-split/FBNeo%20-%20Arcade%20Games.dat", LibretroNames.DatUrl("FBNeo - Arcade Games"));
            Assert.Equal(b + "redump/Sony%20-%20PlayStation.dat", LibretroNames.DatUrl("Sony - PlayStation"));
            Assert.Equal(b + "no-intro/Nintendo%20-%20Game%20Boy%20Advance.dat", LibretroNames.DatUrl("Nintendo - Game Boy Advance"));
        }
    }
}
