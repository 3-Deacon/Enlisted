namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// Catalog of favors a former lord (now a "patron") may grant when the player
    /// calls in past service. Each kind has its own cooldown, conditions, and
    /// outcome storylet — see docs/Features/Patrons/patron-favor-catalog.md.
    /// None is the default for unset fields and stays permanent so existing saves
    /// reload cleanly. Numeric values are persisted in PatronEntry.PerFavorCooldownsCsv,
    /// so reordering or renumbering existing members would corrupt cooldown reads
    /// from older saves.
    /// </summary>
    public enum FavorKind
    {
        None = 0,
        LetterOfIntroduction = 1,
        GoldLoan = 2,
        TroopLoan = 3,
        AudienceArrangement = 4,
        MarriageFacilitation = 5,
        AnotherContract = 6,
    }
}
