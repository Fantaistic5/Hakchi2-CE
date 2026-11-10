using System.Collections.Generic;
using System.Linq;
using TeamShinkansen.Scrapers.Covers;

namespace com.clusterrr.hakchi_gui.Covers
{
    public static class CoverTargets
    {
        public static GameQuery ToQuery(NesApplication app)
        {
            var m = app.Metadata;
            return new GameQuery
            {
                System = m != null ? m.System : null,
                Core = m != null ? m.Core : null,
                Crc32 = m != null ? m.OriginalCrc32 : 0,
                RomFileName = m != null ? m.OriginalFilename : null,
                Title = app.Name,
                PreferredRegion = "Europe"
            };
        }

        /// <summary>
        /// Vrai si le jeu n'a pas de jaquette personnalisee. On se base sur CustomCoverArt seul
        /// (pas de comparaison d'image avec la jaquette par defaut du systeme : couteux et fragile).
        /// Les jeux d'origine de la console ont deja leur jaquette officielle : jamais proposes d'office.
        /// </summary>
        public static bool NeedsCover(NesApplication app)
        {
            return app != null && !app.IsOriginalGame && (app.Metadata == null || !app.Metadata.CustomCoverArt);
        }

        public static IEnumerable<NesApplication> DefaultSelection(IEnumerable<NesApplication> apps)
        {
            return apps.Where(NeedsCover);
        }
    }
}
