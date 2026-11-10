using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace hakchi_gui.Tests.Shell
{
    public class ShellResourcesTests
    {
        private static readonly string[] Keys =
        {
            "NavSystemsHeader", "NavCollectionsHeader", "NavAll", "NavNoCover", "NavFavorites", "ConsoleBadgeOffline",
            "SheetCoverSource", "SheetCoverSourceUnknown", "SheetChangeCover", "SheetPlayers", "SheetPublisher", "SheetGenre", "SheetCore",
            "StorageBarCaption", "StorageUnitMB", "StorageUnitGB",
        };

        private static System.Collections.Generic.Dictionary<string, string> Load(string file)
        {
            var path = Path.Combine(Repo.Root, "hakchi_gui", "Properties", file);
            return XDocument.Load(path).Root.Elements("data")
                .ToDictionary(d => (string)d.Attribute("name"), d => (string)d.Element("value"));
        }

        [Fact]
        public void NewStringsExistInEnglishAndFrench()
        {
            var en = Load("Resources.resx");
            var fr = Load("Resources.fr-FR.resx");
            foreach (var k in Keys)
            {
                Assert.True(en.ContainsKey(k) && en[k].Length > 0, "EN manquant : " + k);
                Assert.True(fr.ContainsKey(k) && fr[k].Length > 0, "FR manquant : " + k);
            }
            Assert.Equal("Sans jaquette", fr["NavNoCover"]);
            Assert.Equal("Source de la pochette :", fr["SheetCoverSource"]);
            Assert.Equal("inconnue", fr["SheetCoverSourceUnknown"]);
            Assert.Equal("Go", fr["StorageUnitGB"]);
        }
    }
}
