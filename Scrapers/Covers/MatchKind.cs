namespace TeamShinkansen.Scrapers.Covers
{
    /// <summary>Type de correspondance, par ordre de confiance décroissante.</summary>
    public enum MatchKind
    {
        Crc = 0,
        RomName = 1,
        Title = 2
    }
}
