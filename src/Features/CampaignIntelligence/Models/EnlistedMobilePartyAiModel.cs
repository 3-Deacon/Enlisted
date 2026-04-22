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
    /// per-party movement decisions (<c>ShouldConsiderAttacking</c>,
    /// <c>ShouldConsiderAvoiding</c>, and the initiative-behavior score in
    /// <c>GetBestInitiativeBehavior</c>) via the Plan 1 intelligence
    /// snapshot when the identity gate matches the enlisted lord; delegates
    /// the remaining members (<c>AiCheckInterval</c>, flee radii, etc.)
    /// unchanged to <c>BaseModel</c>. Falls back to vanilla defaults if
    /// <c>BaseModel</c> is null during a bootstrap-order regression.
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

        public override bool ShouldConsiderAvoiding(MobileParty party, MobileParty targetParty) =>
            BaseModel?.ShouldConsiderAvoiding(party, targetParty) ?? false;

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
            if (BaseModel != null)
            {
                BaseModel.GetBestInitiativeBehavior(
                    mobileParty,
                    out bestInitiativeBehavior,
                    out bestInitiativeTargetParty,
                    out bestInitiativeBehaviorScore,
                    out averageEnemyVec);
                return;
            }
            bestInitiativeBehavior = AiBehavior.None;
            bestInitiativeTargetParty = null;
            bestInitiativeBehaviorScore = 0f;
            averageEnemyVec = Vec2.Zero;
        }
    }
}
