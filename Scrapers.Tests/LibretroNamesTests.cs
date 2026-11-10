using TeamShinkansen.Scrapers.Covers;
using Xunit;

namespace Scrapers.Tests
{
    public class LibretroNamesTests
    {
        [Fact]
        public void SanitizesLibretroForbiddenChars()
        {
            Assert.Equal("Tom _ Jerry_ Frantic_Antics_", LibretroNames.Sanitize("Tom & Jerry: Frantic/Antics?"));
        }

        [Fact]
        public void BoxartUrlEscapesSpaces()
        {
            var url = LibretroNames.BoxartUrl("Nintendo - Super Nintendo Entertainment System", "Super Metroid (Japan, USA) (En,Ja)");
            Assert.StartsWith("https://thumbnails.libretro.com/Nintendo%20-%20Super%20Nintendo%20Entertainment%20System/Named_Boxarts/Super%20Metroid%20", url);
            Assert.EndsWith(".png", url);
        }

        [Fact]
        public void ArcadeSystemsMapToFbneo()
        {
            Assert.Equal("FBNeo - Arcade Games", LibretroNames.ThumbnailSystem("CP System II", "km_fbalpha2012_cps2"));
            Assert.Equal("FBNeo - Arcade Games", LibretroNames.ThumbnailSystem("", "km_fbalpha2012"));
        }

        [Fact]
        public void NormalizeTitleStripsTags()
        {
            Assert.Equal("the king of fighters 98", LibretroNames.NormalizeTitle("The King of Fighters '98 (Europe) [b]"));
        }

        [Theory]
        [InlineData("Game (USA).sfc", "USA")]
        [InlineData("Game (Europe).sfc", "Europe")]
        [InlineData("Game (Japan).sfc", "Japan")]
        [InlineData("Game (USA, Europe).sfc", "USA")]
        [InlineData("Game.sfc", null)]
        [InlineData(null, null)]
        public void RegionFromFileNameFindsFirstKnownRegion(string file, string expected)
        {
            Assert.Equal(expected, LibretroNames.RegionFromFileName(file));
        }
    }
}
