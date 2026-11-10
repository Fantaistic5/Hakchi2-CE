using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TeamShinkansen.Scrapers.Covers
{
    /// <summary>
    /// Index d'un DAT clrmamepro de libretro-database : CRC32 -> nom du jeu (No-Intro) et
    /// nom de ROM set (arcade FBNeo, "kof98.zip") -> nom du jeu. Le nom du jeu (champ "name" du bloc game)
    /// est le nom de fichier de pochette libretro-thumbnails (avant Sanitize).
    /// </summary>
    public sealed class LibretroDatIndex
    {
        private readonly Dictionary<uint, string> _byCrc = new Dictionary<uint, string>();
        private readonly Dictionary<string, string> _byRomSet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _names = new List<string>();

        private LibretroDatIndex() { }

        public IReadOnlyCollection<string> Names { get { return _names; } }

        public static LibretroDatIndex Parse(TextReader clrmameproDat)
        {
            if (clrmameproDat == null) throw new ArgumentNullException("clrmameproDat");
            var idx = new LibretroDatIndex();

            bool inGame = false;
            string gameName = null;
            var crcs = new List<uint>();
            var romSets = new List<string>();

            string line;
            while ((line = clrmameproDat.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0) continue;

                if (!inGame)
                {
                    if (line.StartsWith("game", StringComparison.Ordinal) && line.EndsWith("(", StringComparison.Ordinal))
                    {
                        inGame = true;
                        gameName = null;
                        crcs.Clear();
                        romSets.Clear();
                    }
                    continue;
                }

                if (line == ")")
                {
                    idx.Commit(gameName, crcs, romSets);
                    inGame = false;
                    continue;
                }

                var tokens = Tokenize(line);
                if (tokens.Count < 2) continue;
                if (tokens[0] == "name")
                {
                    gameName = tokens[1];
                }
                else if (tokens[0] == "rom")
                {
                    string romName = null;
                    for (int i = 1; i + 1 < tokens.Count; i++)
                    {
                        if (tokens[i] == "name") romName = tokens[i + 1];
                        else if (tokens[i] == "crc")
                        {
                            uint crc;
                            if (uint.TryParse(tokens[i + 1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out crc))
                                crcs.Add(crc);
                        }
                    }
                    if (romName != null && romName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        romSets.Add(romName.Substring(0, romName.Length - 4));
                }
            }
            return idx;
        }

        private void Commit(string gameName, List<uint> crcs, List<string> romSets)
        {
            if (string.IsNullOrEmpty(gameName)) return;
            _names.Add(gameName);
            foreach (var c in crcs) if (!_byCrc.ContainsKey(c)) _byCrc[c] = gameName;
            foreach (var r in romSets) if (!_byRomSet.ContainsKey(r)) _byRomSet[r] = gameName;
        }

        /// <summary>Nom du jeu pour ce CRC32 ; null si absent.</summary>
        public string FindByCrc(uint crc32)
        {
            string name;
            return _byCrc.TryGetValue(crc32, out name) ? name : null;
        }

        /// <summary>"kof98.zip" ou "kof98" (insensible à la casse) -> nom du jeu ; null si absent.</summary>
        public string FindByRomSet(string romFileName)
        {
            if (string.IsNullOrWhiteSpace(romFileName)) return null;
            string key = Path.GetFileName(romFileName.Trim());
            if (key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                key = key.Substring(0, key.Length - 4);
            string name;
            return _byRomSet.TryGetValue(key, out name) ? name : null;
        }

        // Jetons séparés par des espaces ; les chaînes entre guillemets (avec \" et \ échappés) forment un seul jeton.
        private static List<string> Tokenize(string s)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                if (char.IsWhiteSpace(s[i])) { i++; continue; }
                var sb = new StringBuilder();
                if (s[i] == '"')
                {
                    i++;
                    while (i < s.Length && s[i] != '"')
                    {
                        if (s[i] == (char)92 && i + 1 < s.Length && (s[i + 1] == '"' || s[i + 1] == (char)92)) i++;
                        sb.Append(s[i++]);
                    }
                    i++; // guillemet fermant
                }
                else
                {
                    while (i < s.Length && !char.IsWhiteSpace(s[i])) sb.Append(s[i++]);
                }
                tokens.Add(sb.ToString());
            }
            return tokens;
        }
    }
}
