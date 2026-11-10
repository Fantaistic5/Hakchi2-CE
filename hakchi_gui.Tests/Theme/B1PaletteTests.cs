using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using com.clusterrr.hakchi_gui.Theme;
using Xunit;

namespace hakchi_gui.Tests.Theme
{
    [Collection("B1Static")]
    public class B1PaletteTests
    {
        // Les 16 couleurs de global-constraints.md, ni plus ni moins.
        private static readonly Dictionary<string, string> Spec = new Dictionary<string, string>
        {
            { "Slate", "1C1B22" }, { "RowActive", "26252E" }, { "Rule", "2C2A34" },
            { "Primary", "4A3C8C" }, { "Accent", "6B5BB5" }, { "Lavender", "A79FD6" }, { "Dashed", "5C5870" },
            { "Text", "E4E2EA" }, { "TextOnPrimary", "ECE9F8" }, { "TextSecondary", "B6B2C4" }, { "TextDim", "8E8A9C" }, { "TextFaint", "6F6B7E" },
            { "HatchA", "24232B" }, { "HatchB", "2A2932" },
            { "Online", "7BC47F" }, { "Error", "D9776A" },
        };

        private static string Hex(Color c) => c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");

        [Fact]
        public void PaletteHasExactlySpecColors()
        {
            Assert.Equal(16, Spec.Count);
            var fields = typeof(B1Palette).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(Color)).ToList();
            Assert.Equal(16, fields.Count);                       // aucune couleur en plus
            foreach (var kv in Spec)
            {
                var f = typeof(B1Palette).GetField(kv.Key, BindingFlags.Public | BindingFlags.Static);
                Assert.True(f != null, "champ manquant : " + kv.Key);
                var c = (Color)f.GetValue(null);
                Assert.Equal(255, c.A);
                Assert.Equal(kv.Value, Hex(c));
            }
            Assert.Equal(16, B1Palette.All.Count);
            Assert.Equal(Spec.Values.OrderBy(x => x), B1Palette.All.Select(Hex).OrderBy(x => x));
        }
    }
}
