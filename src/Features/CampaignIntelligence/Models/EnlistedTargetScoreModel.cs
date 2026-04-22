using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Enlisted-only wrapper over <see cref="TargetScoreCalculatingModel"/>.
    /// Currently a pass-through to <c>BaseModel</c> for every member; intel-
    /// driven bias for assault/raid/defence targeting lands in later Plan 2
    /// tasks. When <c>BaseModel</c> is null (bootstrap ordering regression),
    /// each override returns a type-sensible default.
    /// </summary>
    public sealed class EnlistedTargetScoreModel : TargetScoreCalculatingModel
    {
        public override float TravelingToAssignmentFactor =>
            BaseModel?.TravelingToAssignmentFactor ?? 1.33f;

        public override float BesiegingFactor =>
            BaseModel?.BesiegingFactor ?? 1.67f;

        public override float AssaultingTownFactor =>
            BaseModel?.AssaultingTownFactor ?? 2f;

        public override float RaidingFactor =>
            BaseModel?.RaidingFactor ?? 1.67f;

        public override float DefendingFactor =>
            BaseModel?.DefendingFactor ?? 2f;

        public override float GetPatrollingFactor(bool isNavalPatrolling) =>
            BaseModel?.GetPatrollingFactor(isNavalPatrolling) ?? (isNavalPatrolling ? 0.4356f : 0.66f);

        public override float GetTargetScoreForFaction(
            Settlement targetSettlement,
            Army.ArmyTypes missionType,
            MobileParty mobileParty,
            float ourStrength) =>
            BaseModel?.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, ourStrength) ?? 0f;

        public override float CalculatePatrollingScoreForSettlement(
            Settlement settlement,
            bool isFromPort,
            MobileParty mobileParty) =>
            BaseModel?.CalculatePatrollingScoreForSettlement(settlement, isFromPort, mobileParty) ?? 0f;

        public override float CurrentObjectiveValue(MobileParty mobileParty) =>
            BaseModel?.CurrentObjectiveValue(mobileParty) ?? 0f;

        /// <summary>
        /// Unified identity-gate + vanilla-fallback entry for every biased
        /// float-returning method. Always returns the vanilla value when the
        /// gate fails; the caller applies any enlisted-only bias on top.
        /// </summary>
        private float VanillaOnlyOrBias(
            MobileParty party,
            float vanilla,
            Func<EnlistedLordIntelligenceSnapshot, float, float> biasFn)
        {
            if (!EnlistedAiGate.TryGetSnapshotForParty(party, out var snapshot))
            {
                return vanilla;
            }
            try
            {
                return biasFn(snapshot, vanilla);
            }
            catch (Exception ex)
            {
                Enlisted.Mod.Core.Logging.ModLogger.Caught("INTELAI", "target_score_bias_failed", ex);
                return vanilla;
            }
        }
    }
}
