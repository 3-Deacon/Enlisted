using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Enlisted-only wrapper over <see cref="ArmyManagementCalculationModel"/>.
    /// Currently a pass-through to <c>BaseModel</c> for every member; intel-
    /// driven bias on call-to-army cost and party eligibility lands in later
    /// Plan 2 tasks. When <c>BaseModel</c> is null (bootstrap ordering
    /// regression), each override returns a type-sensible default.
    /// </summary>
    public sealed class EnlistedArmyManagementModel : ArmyManagementCalculationModel
    {
        public override float AIMobilePartySizeRatioToCallToArmy =>
            BaseModel?.AIMobilePartySizeRatioToCallToArmy ?? 0.6f;

        public override float PlayerMobilePartySizeRatioToCallToArmy =>
            BaseModel?.PlayerMobilePartySizeRatioToCallToArmy ?? 0.4f;

        public override float MinimumNeededFoodInDaysToCallToArmy =>
            BaseModel?.MinimumNeededFoodInDaysToCallToArmy ?? 15f;

        public override float MaximumDistanceToCallToArmy =>
            BaseModel?.MaximumDistanceToCallToArmy ?? 150f;

        public override int InfluenceValuePerGold =>
            BaseModel?.InfluenceValuePerGold ?? 40;

        public override int AverageCallToArmyCost =>
            BaseModel?.AverageCallToArmyCost ?? 20;

        public override int CohesionThresholdForDispersion =>
            BaseModel?.CohesionThresholdForDispersion ?? 10;

        public override float MaximumWaitTime =>
            BaseModel?.MaximumWaitTime ?? 72f;

        public override bool CanPlayerCreateArmy(out TextObject disabledReason)
        {
            if (BaseModel != null)
            {
                return BaseModel.CanPlayerCreateArmy(out disabledReason);
            }
            disabledReason = TextObject.GetEmpty();
            return false;
        }

        public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party) =>
            BaseModel?.CalculatePartyInfluenceCost(armyLeaderParty, party) ?? 0;

        public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty) =>
            BaseModel?.DailyBeingAtArmyInfluenceAward(armyMemberParty) ?? 0f;

        public override List<MobileParty> GetMobilePartiesToCallToArmy(MobileParty leaderParty) =>
            BaseModel?.GetMobilePartiesToCallToArmy(leaderParty) ?? new List<MobileParty>();

        public override int CalculateTotalInfluenceCost(Army army, float percentage) =>
            BaseModel?.CalculateTotalInfluenceCost(army, percentage) ?? 0;

        public override float GetPartySizeScore(MobileParty party) =>
            BaseModel?.GetPartySizeScore(party) ?? 0f;

        public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
        {
            if (BaseModel != null)
            {
                return BaseModel.CheckPartyEligibility(party, out explanation);
            }
            explanation = TextObject.GetEmpty();
            return false;
        }

        public override int GetPartyRelation(Hero hero) =>
            BaseModel?.GetPartyRelation(hero) ?? 0;

        public override ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false) =>
            BaseModel?.CalculateDailyCohesionChange(army, includeDescriptions) ?? new ExplainedNumber(0f);

        public override int CalculateNewCohesion(Army army, PartyBase newParty, int calculatedCohesion, int sign) =>
            BaseModel?.CalculateNewCohesion(army, newParty, calculatedCohesion, sign) ?? calculatedCohesion;

        public override int GetCohesionBoostInfluenceCost(Army army, int percentageToBoost = 100) =>
            BaseModel?.GetCohesionBoostInfluenceCost(army, percentageToBoost) ?? 0;
    }
}
