namespace TeamShinkansen.Scrapers.Covers
{
    public sealed class GameQuery
    {
        /// <summary>Valeur brute de AppMetadata.System (ex. "Nintendo - Super Nintendo Entertainment System", "FBA - Arcade Games", "CP System II", "").</summary>
        public string System;
        /// <summary>AppMetadata.Core (ex. "km_fbneo"), sert quand System est vide.</summary>
        public string Core;
        /// <summary>AppMetadata.OriginalCrc32 (0 = inconnu).</summary>
        public uint Crc32;
        /// <summary>AppMetadata.OriginalFilename.</summary>
        public string RomFileName;
        /// <summary>NesApplication.Name.</summary>
        public string Title;
        /// <summary>Région préférée, "Europe" par défaut.</summary>
        public string PreferredRegion = "Europe";
    }
}
