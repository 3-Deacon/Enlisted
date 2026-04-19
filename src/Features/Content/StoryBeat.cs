namespace Enlisted.Features.Content
{
    /// <summary>
    /// World-event or simulation beat that a story candidate can respond to.
    /// Used by SeverityClassifier to weight candidates and by the Director to gate tier promotion.
    /// </summary>
    public enum StoryBeat
    {
        LordCaptured,
        LordKilled,
        LordMajorBattleStart,
        LordMajorBattleEnd,
        PlayerBattleEnd,
        SiegeBegin,
        SiegeEnd,
        WarDeclared,
        PeaceSigned,
        ArmyFormed,
        ArmyDispersed,
        SettlementEntered,
        SettlementLeft,
        PaydayBeat,
        OrderPhaseTransition,
        OrderComplete,
        CompanyCrisisThreshold,
        CompanyPressureThreshold,
        EscalationThreshold,
        QuietStretchTimeout
    }
}
