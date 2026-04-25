using System;
using System.Collections.Generic;
using Enlisted.Features.CampaignIntelligence;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Enlisted-only wrapper over <see cref="ArmyManagementCalculationModel"/>.
    /// Biases per-party decisions (call-to-army composition, influence cost)
    /// via the Plan 1 intelligence snapshot when the identity gate matches
    /// the enlisted lord; delegates the ~15 global-constant members
    /// (<c>AIMobilePartySizeRatioToCallToArmy</c>,
    /// <c>MinimumNeededFoodInDaysToCallToArmy</c>, etc.) unchanged to
    /// <c>BaseModel</c> because biasing them would affect every AI lord in
    /// Calradia and violate the enlisted-only scope. Falls back to vanilla
    /// defaults if <c>BaseModel</c> is null during a bootstrap-order
    /// regression.
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

        public override int CalculatePartyInfluenceCost(
            MobileParty armyLeaderParty, MobileParty party)
        {
            if (BaseModel == null)
            {
                ModLogger.Surfaced("INTELAI", "base_model_missing");
                return 0;
            }
            var vanilla = BaseModel.CalculatePartyInfluenceCost(armyLeaderParty, party);

            if (!EnlistedAiGate.TryGetSnapshotForParty(armyLeaderParty, out var snapshot))
            {
                return vanilla;
            }

            try
            {
                // Moderate bias up when the enlisted lord (as leader) is under
                // supply pressure or elevated strain. This applies BEFORE the
                // empty-list suppressor in T8 at vanilla call-sites that iterate
                // (e.g., influence-budget checks inside the AI's loop at
                // DefaultArmyManagementCalculationModel.cs:171-175). T8 catches
                // the aggregate; T9 catches the per-call budgeting.
                if (snapshot.SupplyPressure >= SupplyPressure.Tightening
                    || snapshot.ArmyStrain >= ArmyStrainLevel.Elevated
                    || snapshot.RecoveryNeed >= RecoveryNeed.Medium)
                {
                    float mult = 1.5f;
                    if (snapshot.SupplyPressure == SupplyPressure.Critical
                        || snapshot.ArmyStrain == ArmyStrainLevel.Breaking)
                    {
                        mult = 2.5f;
                    }
                    int biased = (int)Math.Round(vanilla * mult);
                    EnlistedAiBiasHeartbeat.Record("influence_cost_strained", vanilla, biased);
                    return biased;
                }
                return vanilla;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTELAI", "influence_cost_bias_failed", ex);
                return vanilla;
            }
        }

        public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty) =>
            BaseModel?.DailyBeingAtArmyInfluenceAward(armyMemberParty) ?? 0f;

        public override List<MobileParty> GetMobilePartiesToCallToArmy(MobileParty leaderParty)
        {
            if (BaseModel == null)
            {
                ModLogger.Surfaced("INTELAI", "base_model_missing");
                return new List<MobileParty>();
            }
            var vanilla = BaseModel.GetMobilePartiesToCallToArmy(leaderParty);

            if (!EnlistedAiGate.TryGetSnapshotForParty(leaderParty, out var snapshot))
            {
                return vanilla;
            }

            // Only bias when the enlisted lord would BE the army leader. If
            // leaderParty is some other lord's party, the gate will already
            // have returned false — belt-and-braces leader check here.
            if (!EnlistedAiGate.IsEnlistedLordArmyLeader()
                && EnlistmentBehavior.Instance?.EnlistedLord?.PartyBelongedTo != leaderParty)
            {
                return vanilla;
            }

            try
            {
                bool strained = snapshot.SupplyPressure >= SupplyPressure.Strained
                             || snapshot.ArmyStrain >= ArmyStrainLevel.Severe
                             || snapshot.RecoveryNeed == RecoveryNeed.High;

                if (strained)
                {
                    EnlistedAiBiasHeartbeat.Record("call_to_army_suppressed", vanilla.Count, 0);
                    return new List<MobileParty>();
                }

                // Front pressure without full strain: trim the list to half so
                // the lord calls a smaller, closer army rather than a sprawling
                // one. Preserve ordering — vanilla list is strength-weighted at
                // DefaultArmyManagementCalculationModel.cs:178-204.
                if (snapshot.FrontPressure >= FrontPressure.High && vanilla.Count > 2)
                {
                    int trimTarget = Math.Max(2, vanilla.Count / 2);
                    var trimmed = new List<MobileParty>(trimTarget);
                    for (int i = 0; i < trimTarget && i < vanilla.Count; i++)
                    {
                        trimmed.Add(vanilla[i]);
                    }
                    EnlistedAiBiasHeartbeat.Record("call_to_army_trimmed", vanilla.Count, trimmed.Count);
                    return trimmed;
                }

                return vanilla;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTELAI", "call_to_army_bias_failed", ex);
                return vanilla;
            }
        }

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
