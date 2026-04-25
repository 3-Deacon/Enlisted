namespace Enlisted.Features.Content.Models
{
    /// <summary>
    /// Military day cycle - maps directly to Order System's 4 phases.
    /// Content is filtered by DayPhase for appropriate timing.
    /// </summary>
    public enum DayPhase
    {
        Dawn,     // 6am-11am: Briefings, training, work details (Order Phase 1)
        Midday,   // 12pm-5pm: Active duty, patrols, main work (Order Phase 2)
        Dusk,     // 6pm-9pm: Evening meal, social time, relaxation (Order Phase 3)
        Night     // 10pm-5am: Sleep, night watch, quiet (Order Phase 4)
    }

    /// <summary>
    /// What the enlisted lord is currently doing.
    /// Mapped from party/settlement/army state.
    /// </summary>
    public enum LordSituation
    {
        PeacetimeGarrison,      // In settlement, no wars
        PeacetimeRecruiting,    // Moving between villages in peace
        WarMarching,            // Moving during wartime
        WarActiveCampaign,      // In army during wartime
        SiegeAttacking,         // Besieging enemy settlement
        SiegeDefending,         // Defending own settlement
        Defeated,               // Post-battle defeat recovery
        Captured                // Lord is prisoner
    }

    /// <summary>
    /// Overall military life phase.
    /// Determines baseline event frequency.
    /// </summary>
    public enum LifePhase
    {
        Peacetime,   // No active wars
        Campaign,    // Active warfare
        Siege,       // Siege operations
        Recovery,    // Post-defeat/injury
        Crisis       // Multiple severe pressures
    }

    /// <summary>
    /// Expected activity level.
    /// Maps to concrete event frequency targets.
    /// </summary>
    public enum ActivityLevel
    {
        Quiet,      // ~1 event/week (0.14/day)
        Routine,    // ~3 events/week (0.43/day)
        Active,     // ~5 events/week (0.71/day)
        Intense     // ~7 events/week (1.0/day)
    }

    /// <summary>
    /// Kingdom war posture.
    /// Affects transition between life phases.
    /// </summary>
    public enum WarStance
    {
        Peace,          // No active wars
        Defensive,      // Wars declared against us
        Offensive,      // Wars we declared
        MultiWar,       // Fighting multiple factions
        Desperate       // Losing badly
    }
}
