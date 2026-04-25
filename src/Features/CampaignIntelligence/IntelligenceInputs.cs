using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Transient plain-data inputs produced by SnapshotCollector and consumed by
    /// SnapshotClassifier. One instance per recompute. Fields are grouped into
    /// lord-and-party state, strategic neighborhood, information evidence, and
    /// player relevance.
    /// </summary>
    internal struct IntelligenceInputs
    {
        // Lord and party state
        public bool LordAlive;
        public bool LordIsPrisoner;
        public bool LordIsWounded;
        public MobileParty LordParty;
        public Settlement CurrentSettlement;
        public Settlement BesiegedSettlement;
        public bool InMapEvent;
        public bool IsArmyLeader;
        public bool InArmy;
        public Army.ArmyTypes ArmyType;
        public bool ArmyIsGathering;
        public float ArmyCohesion;
        public float FoodDaysRemaining;
        public float PartySizeRatio;
        public float WoundedRatio;

        // Strategic neighborhood
        public int NearbyHostileCount;
        public float NearestHostileDistance;
        public float NearestHostileStrengthRatio;
        public int NearbyAlliedCount;
        public bool NearbyAlliedReliefPlausible;
        public int ThreatenedFriendlySettlementCount;
        public Settlement NearestThreatenedFriendly;
        public bool FrontierHeatingUp;

        // Information evidence
        public bool HasFreshTracks;
        public bool HasEnemyBuildupEvidence;
        public bool HasGarrisonObservation;
        public int ImportantPrisonerCount;
        public bool RecentWarTransition;
        public bool RecentPeaceTransition;
        public bool RecentArmyFormation;
        public bool RecentArmyDispersal;

        // Player relevance
        public int PlayerTier;
        public int PlayerRelationWithLord;
        public bool PlayerProximate;

        // Transient carry-over: pending change flags from the behavior's dirty-bit
        // accumulator. OR'd into the published snapshot after classification.
        public RecentChangeFlags PendingFlagsAtTickStart;
    }
}
