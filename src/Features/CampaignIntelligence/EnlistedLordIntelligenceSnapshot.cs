using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Compact assessed state for the enlisted lord. Recomputed on hourly tick and on
    /// specific campaign-event invalidation. Never stores live TaleWorlds references —
    /// all fields are enums or primitives so the snapshot round-trips cleanly through
    /// save/load without dangling pointers. Owning behavior clears volatile fields on
    /// enlistment end; recompute-on-load is honored by the next hourly tick
    /// overwriting fields from live world state.
    /// </summary>
    [Serializable]
    public sealed class EnlistedLordIntelligenceSnapshot
    {
        public StrategicPosture Posture { get; set; }
        public ObjectiveType Objective { get; set; }
        public ObjectiveConfidence ObjectiveConfidence { get; set; }
        public FrontPressure FrontPressure { get; set; }
        public ArmyStrainLevel ArmyStrain { get; set; }
        public SupplyPressure SupplyPressure { get; set; }
        public InformationConfidence InformationConfidence { get; set; }
        public EnemyContactRisk EnemyContactRisk { get; set; }
        public PursuitViability PursuitViability { get; set; }
        public RecoveryNeed RecoveryNeed { get; set; }
        public PrisonerStakes PrisonerStakes { get; set; }
        public PlayerTrustWindow PlayerTrustWindow { get; set; }
        public RecentChangeFlags RecentChanges { get; set; }

        /// <summary>In-game time at which this snapshot was last refreshed.</summary>
        public CampaignTime LastRefreshed { get; set; }

        /// <summary>
        /// Reset fields to default. Called by the behavior on enlistment end so the
        /// accessor returns a clean no-state object should something read it during
        /// the OnEnlistmentEnded tear-down window.
        /// </summary>
        public void Clear()
        {
            Posture = StrategicPosture.March;
            Objective = ObjectiveType.None;
            ObjectiveConfidence = ObjectiveConfidence.Low;
            FrontPressure = FrontPressure.None;
            ArmyStrain = ArmyStrainLevel.None;
            SupplyPressure = SupplyPressure.None;
            InformationConfidence = InformationConfidence.Low;
            EnemyContactRisk = EnemyContactRisk.Low;
            PursuitViability = PursuitViability.NotViable;
            RecoveryNeed = RecoveryNeed.None;
            PrisonerStakes = PrisonerStakes.None;
            PlayerTrustWindow = PlayerTrustWindow.None;
            RecentChanges = RecentChangeFlags.None;
            LastRefreshed = CampaignTime.Never;
        }
    }
}
