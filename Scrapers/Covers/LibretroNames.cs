using System;
using System.Text;

namespace TeamShinkansen.Scrapers.Covers
{
    public static class LibretroNames
    {
        private const string FbNeo = "FBNeo - Arcade Games";
        private const string BaseUrl = "https://thumbnails.libretro.com/";

        /// <summary>Remplace les caractères interdits par libretro (&amp; * / : ` &lt; &gt; ? \ | ") par '_'.</summary>
        public static string Sanitize(string gameName)
        {
            if (gameName == null) return string.Empty;
            const string forbidden = "&*/:`<>?\\|\"";
            var sb = new StringBuilder(gameName.Length);
            foreach (char c in gameName)
                sb.Append(forbidden.IndexOf(c) >= 0 ? '_' : c);
            return sb.ToString();
        }

        /// <summary>Nom de dossier libretro-thumbnails pour un système/core hakchi ; null si non pris en charge.</summary>
        public static string ThumbnailSystem(string system, string core)
        {
            system = (system ?? string.Empty).Trim();
            core = (core ?? string.Empty).Trim();

            if (system.Length == 0)
            {
                if (core.StartsWith("km_fbalpha", StringComparison.OrdinalIgnoreCase) ||
                    core.StartsWith("km_fbneo", StringComparison.OrdinalIgnoreCase))
                    return FbNeo;
                return null;
            }

            if (system.Equals("FBA - Arcade Games", StringComparison.OrdinalIgnoreCase) ||
                system.Equals("FB Alpha - Arcade Games", StringComparison.OrdinalIgnoreCase) ||
                system.Equals("CP System I", StringComparison.OrdinalIgnoreCase) ||
                system.Equals("CP System II", StringComparison.OrdinalIgnoreCase) ||
                system.Equals("CP System III", StringComparison.OrdinalIgnoreCase))
                return FbNeo;

            // Noms déjà au format libretro (Nintendo - ..., Sega - ..., Sony - ..., NEC - ..., The 3DO Company - ...)
            return system;
        }

        public static string BoxartUrl(string thumbnailSystem, string gameName)
        {
            return BaseUrl + Uri.EscapeDataString(thumbnailSystem ?? string.Empty)
                + "/Named_Boxarts/" + Uri.EscapeDataString(Sanitize(gameName)) + ".png";
        }

        /// <summary>Minuscules, sans balises (...) [...], sans ponctuation, espaces réduits.</summary>
        public static string NormalizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return string.Empty;
            var sb = new StringBuilder(title.Length);
            int depthRound = 0, depthSquare = 0;
            foreach (char c in title)
            {
                if (c == '(') { depthRound++; continue; }
                if (c == ')') { if (depthRound > 0) depthRound--; continue; }
                if (c == '[') { depthSquare++; continue; }
                if (c == ']') { if (depthSquare > 0) depthSquare--; continue; }
                if (depthRound > 0 || depthSquare > 0) continue;
                if (c == '\'' || c == '\u2019') continue;
                sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            }
            var parts = sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }
    }
}
