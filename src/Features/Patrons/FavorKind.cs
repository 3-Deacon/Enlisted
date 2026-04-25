namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// Catalog of favors a former lord (now a "patron") may grant. Plan 6 populates
    /// the full member list (LetterOfIntroduction, GoldLoan, TroopLoan,
    /// AudienceArrangement, MarriageFacilitation, AnotherContract). The None member
    /// is the default value for unset/uninitialized fields and remains permanent.
    /// </summary>
    public enum FavorKind
    {
        None = 0,
    }
}
