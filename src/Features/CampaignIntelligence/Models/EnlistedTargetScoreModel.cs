using System;
using Enlisted.Features.CampaignIntelligence;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>Enlisted-only wrapper over <see cref="TargetScoreCalculatingModel"/> that biases target scoring via the Plan 1 intelligence snapshot when the identity gate matches the enlisted lord's army and delegates to <c>BaseModel</c> otherwise; overrides fall back to vanilla defaults if <c>BaseModel</c> is null during a bootstrap-order regression.</summary>
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
            float ourStrength)
        {
            if (BaseModel == null)
            {
                ModLogger.Surfaced("INTELAI", "base_model_missing");
                return 0f;
            }
            var vanilla = BaseModel.GetTargetScoreForFaction(
                targetSettlement, missionType, mobileParty, ourStrength);

            return VanillaOnlyOrBias(mobileParty, vanilla, (snapshot, v) =>
            {
                // Defend / relieve friendly-under-threat bias. Key only on
                // FrontPressure — Plan 1 T14's classifier already folds
                // ThreatenedFriendlySettlementCount into FrontPressure score
                // (>0 adds +2, >2 adds +4), so `FrontPressure >= Medium`
                // is a reliable proxy for "there's a threatened friendly"
                // without needing a raw count on the snapshot. See Plan 1
                // IntelligenceEnums + SnapshotClassifier.ClassifyFrontPressure.
                if (missionType == Army.ArmyTypes.Defender
                    && snapshot.FrontPressure >= FrontPressure.Medium)
                {
                    float mult = snapshot.FrontPressure == FrontPressure.Critical ? 1.6f
                               : snapshot.FrontPressure == FrontPressure.High ? 1.35f
                               : 1.15f;
                    EnlistedAiBiasHeartbeat.Record("target_score_defend", v, v * mult);
                    return v * mult;
                }

                // Besieger bias down under recovery / supply strain. The lord
                // should not commit to an offensive siege while under strain.
                if (missionType == Army.ArmyTypes.Besieger)
                {
                    if (snapshot.RecoveryNeed >= RecoveryNeed.Medium
                        || snapshot.SupplyPressure >= SupplyPressure.Strained
                        || snapshot.ArmyStrain >= ArmyStrainLevel.Severe)
                    {
                        float mult = snapshot.RecoveryNeed == RecoveryNeed.High ? 0.5f
                                   : snapshot.SupplyPressure == SupplyPressure.Critical ? 0.55f
                                   : 0.7f;
                        EnlistedAiBiasHeartbeat.Record("target_score_siege_strained", v, v * mult);
                        return v * mult;
                    }
                }

                // Raider bias down under High-or-greater front pressure.
                // Design-spec §8.2: "defend or relieve nearby friendly holdings
                // before speculative raiding." FrontPressure is the proxy for
                // "there's something more important nearby" — see Plan 1
                // classifier rationale above.
                if (missionType == Army.ArmyTypes.Raider
                    && snapshot.FrontPressure >= FrontPressure.High)
                {
                    EnlistedAiBiasHeartbeat.Record("target_score_raid_suppressed", v, v * 0.6f);
                    return v * 0.6f;
                }

                return v;
            });
        }

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
                ModLogger.Caught("INTELAI", "target_score_bias_failed", ex);
                return vanilla;
            }
        }
    }
}
