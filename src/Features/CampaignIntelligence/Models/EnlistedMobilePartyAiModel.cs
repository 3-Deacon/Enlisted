using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Enlisted-only wrapper over <see cref="MobilePartyAIModel"/>. Currently
    /// a pass-through to <c>BaseModel</c> for every member; intel-driven bias
    /// on avoidance, attacking, and initiative behaviors lands in later Plan
    /// 2 tasks. When <c>BaseModel</c> is null (bootstrap ordering regression),
    /// each override returns a type-sensible default.
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

        public override bool ShouldConsiderAttacking(MobileParty party, MobileParty targetParty) =>
            BaseModel?.ShouldConsiderAttacking(party, targetParty) ?? false;

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
