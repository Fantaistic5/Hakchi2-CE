using TeamShinkansen.Scrapers.Enums;
using Xunit;

namespace Scrapers.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void Smoke_ReferencesScrapers()
        {
            Assert.Equal(ArtType.Front, ArtType.Front);
        }
    }
}
