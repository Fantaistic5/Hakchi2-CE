using System.IO;
using TeamShinkansen.Scrapers.Covers;
using Xunit;

namespace Scrapers.Tests
{
    public class LibretroDatIndexTests
    {
        private static LibretroDatIndex Load(string fixture)
        {
            using (var r = new StreamReader(Path.Combine("Fixtures", fixture)))
                return LibretroDatIndex.Parse(r);
        }

        [Fact]
        public void FindsGameByCrc()
        {
            var idx = Load("snes-sample.dat");
            Assert.Equal("Super Metroid (USA, Europe) (En,Ja) (Virtual Console, Classic Mini, Switch Online)",
                idx.FindByCrc(0x45913E52));
            Assert.Equal("Super Mario World (USA)", idx.FindByCrc(0xB19ED489));
            Assert.Equal(2, idx.Names.Count);
        }

        [Fact]
        public void ArcadeRomNameResolvesViaFbneoDat()
        {
            var idx = Load("fbneo-sample.dat");
            const string expected = "The King of Fighters '98 - The Slugfest / King of Fighters '98 - Dream Match Never Ends (NGM-2420)";
            Assert.Equal(expected, idx.FindByRomSet("kof98.zip"));
            Assert.Equal(expected, idx.FindByRomSet("KOF98"));
            Assert.Equal("'88 Games", idx.FindByRomSet("88games"));
            Assert.Null(idx.FindByRomSet("nope.zip"));
            Assert.Null(idx.FindByRomSet(null));
        }

        [Fact]
        public void UnknownCrcReturnsNull()
        {
            Assert.Null(Load("snes-sample.dat").FindByCrc(0xDEADBEEF));
        }

        [Fact]
        public void ParsesQuotedNamesWithParentheses()
        {
            var dat = "game (\n\tname \"Foo \\\"Bar\\\" (World) (Rev 1)\"\n" +
                      "\trom ( name \"Foo (World).sfc\" size 1 crc 0000ABCD )\n)\n";
            var idx = LibretroDatIndex.Parse(new StringReader(dat));
            Assert.Equal("Foo \"Bar\" (World) (Rev 1)", idx.FindByCrc(0xABCD));
        }
    }
}
