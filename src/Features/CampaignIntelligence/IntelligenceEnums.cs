using System;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Top-level strategic stance the enlisted lord's party is currently operating in.
    /// Computed from live state; not author-overridable.
    /// </summary>
    public enum StrategicPosture : byte
    {
        March,
        ShadowingEnemy,
        FrontierDefense,
        Relief,
        OffensiveSiege,
        OpportunisticRaid,
        Recovery,
        Regroup,
        ArmyGather,
        GarrisonHold
    }

    /// <summary>
    /// Concrete operational objective the enlisted lord is pursuing right now, if any.
    /// </summary>
    public enum ObjectiveType : byte
    {
        None,
        Move,
        Pursue,
        DefendSettlement,
        RelieveSettlement,
        BesiegeSettlement,
        RaidVillage,
        GatherArmy,
        Recover
    }

    public enum ObjectiveConfidence : byte { Low, Medium, High }

    public enum FrontPressure : byte { None, Low, Medium, High, Critical }

    public enum ArmyStrainLevel : byte { None, Mild, Elevated, Severe, Breaking }

    public enum SupplyPressure : byte { None, Tightening, Strained, Critical }

    public enum InformationConfidence : byte { Low, Medium, High }

    public enum EnemyContactRisk : byte { Low, Medium, High }

    public enum PursuitViability : byte { NotViable, Marginal, Viable, Strong }

    public enum RecoveryNeed : byte { None, Low, Medium, High }

    public enum PrisonerStakes : byte { None, Low, Medium, High }

    public enum PlayerTrustWindow : byte { None, Narrow, Moderate, Broad }

    /// <summary>
    /// Bit flags describing what has changed recently enough that downstream consumers
    /// may still need to notice. Set by event-driven invalidation + delta detection
    /// during recompute; decayed on DailyTickEvent.
    /// </summary>
    [Flags]
    public enum RecentChangeFlags : ushort
    {
        None              = 0,
        ObjectiveShift    = 1 << 0,
        NewThreat         = 1 << 1,
        SiegeTransition   = 1 << 2,
        DispersalRisk     = 1 << 3,
        ConfirmedScoutInfo = 1 << 4,
        SupplyBreak       = 1 << 5,
        PrisonerUpdate    = 1 << 6,
        WarDeclared       = 1 << 7,
        MakePeace         = 1 << 8,
        ArmyFormed        = 1 << 9,
        ArmyDispersed     = 1 << 10,
        MapEventJoined    = 1 << 11
    }
}
