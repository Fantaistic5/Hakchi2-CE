using System.IO;
using Newtonsoft.Json;
using Xunit;

namespace Scrapers.Tests
{
    /// <summary>
    /// hakchi 3.8.0 lit metadata.json avec JsonConvert.DeserializeObject (reglages par defaut) dans un
    /// AppMetadata sans CoverSource. Le champ additionnel doit etre ignore sans erreur.
    /// </summary>
    public class MetadataCompatTests
    {
        // Miroir des champs serialises d'AppMetadata en 3.8.0 (sans CoverSource).
        private class AppMetadata380
        {
            public string System = string.Empty;
            public string Core = string.Empty;
            public string OriginalFilename = string.Empty;
            public uint OriginalCrc32 = 0;
            public bool CustomCoverArt = false;
        }

        [Fact]
        public void Metadata_with_CoverSource_is_readable_by_380()
        {
            string json = File.ReadAllText(Path.Combine("Fixtures", "metadata-380.json"));
            var o = Newtonsoft.Json.Linq.JObject.Parse(json);
            o["CoverSource"] = "libretro";
            string withSource = o.ToString(Formatting.Indented);

            var m = JsonConvert.DeserializeObject<AppMetadata380>(withSource);

            var orig = JsonConvert.DeserializeObject<AppMetadata380>(json);
            Assert.Equal(orig.System, m.System);
            Assert.Equal(orig.Core, m.Core);
            Assert.Equal(orig.OriginalFilename, m.OriginalFilename);
            Assert.Equal(orig.OriginalCrc32, m.OriginalCrc32);
            Assert.Equal(orig.CustomCoverArt, m.CustomCoverArt);
            Assert.True(orig.OriginalCrc32 != 0);
        }
    }
}
