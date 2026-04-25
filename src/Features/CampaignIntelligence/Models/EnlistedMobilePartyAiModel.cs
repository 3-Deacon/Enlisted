using Enlisted.Features.CampaignIntelligence;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Enlisted-only wrapper over <see cref="MobilePartyAIModel"/>. Biases
    /// three per-party decisions via the Plan 1 intelligence snapshot when
    /// the identity gate matches the enlisted lord: <c>ShouldConsiderAttacking</c>
    /// (abort unviable pursuits), <c>ShouldConsiderAvoiding</c> (more eager
    /// to avoid under recovery need), and <c>GetBestInitiativeBehavior</c>
    /// (the central bait-magnetization fix — zero the initiative score when
    /// FrontPressure is high and the candidate is an <c>EngageParty</c>). The
    /// other ~12 abstract members are either global tuning constants
    /// (<c>AiCheckInterval</c>, flee radii, food thresholds) that affect every
    /// AI lord in Calradia, or decision predicates already scoped by the three
    /// biased methods (e.g. <c>ShouldPartyCheckInitiativeBehavior</c> gates
    /// whether <c>GetBestInitiativeBehavior</c> runs at all). Passing them
    /// through preserves enlisted-only scope. Falls back to vanilla defaults
    /// if <c>BaseModel</c> is null during a bootstrap-order regression.
    /// </summary>
    public sealed class EnlistedMobilePartyAiModel : MobilePartyAIModel
    {
        public override float AiCheckInterval =>
            BaseModel?.AiCheckInterval ?? 0.25f;

        public override float FleeToNearbyPartyRadius =>
            BaseModel?.FleeToNearbyPartyRadius ?? 15f;

        public override float FleeToNearbySettlementRadius =>
            BaseModel?.FleeToNearbySettlementRadius ?? 25f;

        public override float HideoutPatrolDistanceAsDays =>
            BaseModel?.HideoutPatrolDistanceAsDays ?? 0.5f;

        public override float FortificationPatrolDistanceAsDays =>
            BaseModel?.FortificationPatrolDistanceAsDays ?? 0.3f;

        public override float VillagePatrolDistanceAsDays =>
            BaseModel?.VillagePatrolDistanceAsDays ?? 0.25f;

        public override float SettlementDefendingNearbyPartyCheckRadius =>
            BaseModel?.SettlementDefendingNearbyPartyCheckRadius ?? 25f;

        public override float SettlementDefendingWaitingPositionRadius =>
            BaseModel?.SettlementDefendingWaitingPositionRadius ?? 3f;

        public override float NeededFoodsInDaysThresholdForSiege =>
            BaseModel?.NeededFoodsInDaysThresholdForSiege ?? 12f;

        public override float NeededFoodsInDaysThresholdForRaid =>
            BaseModel?.NeededFoodsInDaysThresholdForRaid ?? 8f;

        public override bool ShouldConsiderAvoiding(
            MobileParty party, MobileParty targetParty)
        {
            if (BaseModel == null)
            {
                ModLogger.Surfaced("INTELAI", "base_model_missing");
                return false;
            }
            var vanilla = BaseModel.ShouldConsiderAvoiding(party, targetParty);

            if (!EnlistedAiGate.TryGetSnapshotForParty(party, out var snapshot))
            {
                return vanilla;
            }

            // Vanilla already says avoid — respect it.
            if (vanilla)
            {
                return true;
            }

            // Bias toward avoidance when recovery is urgent.
            if (snapshot.RecoveryNeed == RecoveryNeed.High)
            {
                EnlistedAiBiasHeartbeat.Record("avoid_recovery", 0, 1);
                return true;
            }

            // Bias toward avoidance when pursuit is not viable AND the target
            // would be the one vanilla is checking against.
            if (snapshot.PursuitViability == PursuitViability.NotViable
                && snapshot.EnemyContactRisk >= EnemyContactRisk.Medium)
            {
                EnlistedAiBiasHeartbeat.Record("avoid_unviable_pursuit", 0, 1);
                return true;
            }

            return false;
        }

        public override bool ShouldConsiderAttacking(
            MobileParty party, MobileParty targetParty)
        {
            if (BaseModel == null)
            {
                ModLogger.Surfaced("INTELAI", "base_model_missing");
                return false;
            }
            var vanilla = BaseModel.ShouldConsiderAttacking(party, targetParty);

            if (!EnlistedAiGate.TryGetSnapshotForParty(party, out var snapshot))
            {
                return vanilla;
            }

            if (!vanilla)
            {
                // Vanilla already says no — don't second-guess.
                return false;
            }

            // Abort unviable chases.
            if (snapshot.PursuitViability == PursuitViability.NotViable)
            {
                EnlistedAiBiasHeartbeat.Record("attack_abort_unviable", 1, 0);
                return false;
            }

            // Abort when contact risk is high AND the lord is not army-leader
            // (a lone party should not rush a high-risk contact).
            if (snapshot.EnemyContactRisk == EnemyContactRisk.High
                && !EnlistedAiGate.IsEnlistedLordArmyLeader()
                && snapshot.ArmyStrain >= ArmyStrainLevel.Elevated)
            {
                EnlistedAiBiasHeartbeat.Record("attack_abort_high_risk", 1, 0);
                return false;
            }

            return true;
        }

        public override float GetPatrolRadius(MobileParty mobileParty, CampaignVec2 patrolPoint) =>
            BaseModel?.GetPatrolRadius(mobileParty, patrolPoint) ?? 1f;

        public override bool ShouldPartyCheckInitiativeBehavior(MobileParty mobileParty) =>
            BaseModel?.ShouldPartyCheckInitiativeBehavior(mobileParty) ?? false;

        public override void GetBestInitiativeBehavior(
            MobileParty mobileParty,
            out AiBehavior bestInitiativeBehavior,
            out MobileParty bestInitiativeTargetParty,
            out float bestInitiativeBehaviorScore,
            out Vec2 averageEnemyVec)
        {
            if (BaseModel == null)
            {
                ModLogger.Surfaced("INTELAI", "base_model_missing");
                bestInitiativeBehavior = AiBehavior.None;
                bestInitiativeTargetParty = null;
                bestInitiativeBehaviorScore = 0f;
                averageEnemyVec = Vec2.Zero;
                return;
            }

            BaseModel.GetBestInitiativeBehavior(
                mobileParty,
                out bestInitiativeBehavior,
                out bestInitiativeTargetParty,
                out bestInitiativeBehaviorScore,
                out averageEnemyVec);

            if (!EnlistedAiGate.TryGetSnapshotForParty(mobileParty, out var snapshot))
            {
                return;
            }

            // Vanilla score already below commit threshold — no bias needed.
            if (bestInitiativeBehaviorScore <= 1f)
            {
                return;
            }

            // Bait-break: under High-or-greater front pressure AND the
            // candidate initiative is an EngageParty, zero the score so the
            // vanilla consumer at MobilePartyAi.cs:488 rejects the commit.
            // FrontPressure alone is a reliable proxy for "there is real
            // pressure here" because Plan 1's ClassifyFrontPressure (T14)
            // already folds the threatened-friendly count into the enum
            // classification — we do not need a raw count on the snapshot.
            if (snapshot.FrontPressure >= FrontPressure.High
                && bestInitiativeBehavior == AiBehavior.EngageParty)
            {
                EnlistedAiBiasHeartbeat.Record("initiative_bait_break",
                    (int)(bestInitiativeBehaviorScore * 100), 0);
                bestInitiativeBehaviorScore = 0f;
                return;
            }

            // Unviable pursuit: soft-break by multiplying down. Don't zero —
            // PursuitViability.Marginal may still be worth attempting.
            if (snapshot.PursuitViability == PursuitViability.NotViable
                && bestInitiativeBehavior == AiBehavior.EngageParty)
            {
                float biased = bestInitiativeBehaviorScore * 0.3f;
                EnlistedAiBiasHeartbeat.Record("initiative_pursuit_weak",
                    (int)(bestInitiativeBehaviorScore * 100), (int)(biased * 100));
                bestInitiativeBehaviorScore = biased;
                return;
            }

            // Severe-strain blanket: don't initiate ANY initiative when strain
            // is breaking. The lord should accept what his DefaultBehavior
            // already set (likely recovery) and stop probing for fights.
            if (snapshot.ArmyStrain == ArmyStrainLevel.Breaking
                || snapshot.RecoveryNeed == RecoveryNeed.High)
            {
                EnlistedAiBiasHeartbeat.Record("initiative_strain_block",
                    (int)(bestInitiativeBehaviorScore * 100), 0);
                bestInitiativeBehaviorScore = 0f;
                return;
            }
        }
    }
}
