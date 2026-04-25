using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.CampaignIntelligence;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Pure projector — reads an intelligence snapshot + the current duty profile
    /// and returns candidate duty opportunities. No side effects, no campaign
    /// reads beyond the snapshot parameter, unit-testable.
    /// </summary>
    public static class EnlistedDutyOpportunityBuilder
    {
        public static List<DutyOpportunity> Build(
            EnlistedLordIntelligenceSnapshot snapshot,
            string currentDutyProfile)
        {
            var opps = new List<DutyOpportunity>();
            if (snapshot == null || string.IsNullOrEmpty(currentDutyProfile))
            {
                return opps;
            }

            // Arc-scale opportunities — propose a named order when the snapshot
            // strongly supports it. Priority scoring biases higher for more
            // acute strain or more confident signals.
            if (snapshot.InformationConfidence == InformationConfidence.Low
                && currentDutyProfile == "marching")
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.ArcScale,
                    ArchetypeStoryletId = "order_scout_accept",
                    PoolPrefix = "order_scout_",
                    Priority = 2.0f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "InformationConfidence=Low"
                });
            }

            if (snapshot.RecoveryNeed >= RecoveryNeed.High
                && currentDutyProfile == "garrisoned")
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.ArcScale,
                    ArchetypeStoryletId = "order_treat_wounded_accept",
                    PoolPrefix = "order_treat_wounded_",
                    Priority = 2.5f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "RecoveryNeed>=High"
                });
            }

            if (snapshot.Posture == StrategicPosture.OffensiveSiege
                && currentDutyProfile == "besieging")
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.ArcScale,
                    ArchetypeStoryletId = "order_siege_works_accept",
                    PoolPrefix = "order_siege_works_",
                    Priority = 2.8f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "Posture=OffensiveSiege"
                });
            }

            // Episodic opportunities — snapshot-gated ambient storylets from the
            // current profile's pool. The actual storylet lookup happens in the
            // emitter via TriggerRegistry; the Builder only proposes pool prefix
            // + priority.
            var profilePrefix = "duty_" + currentDutyProfile + "_";

            if (snapshot.SupplyPressure >= SupplyPressure.Strained)
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.Episodic,
                    PoolPrefix = profilePrefix,
                    Priority = 3.0f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "SupplyPressure>=Strained"
                });
            }

            if (snapshot.ArmyStrain >= ArmyStrainLevel.Elevated)
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.Episodic,
                    PoolPrefix = profilePrefix,
                    Priority = 2.5f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "ArmyStrain>=Elevated"
                });
            }

            if (snapshot.FrontPressure >= FrontPressure.High)
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.Episodic,
                    PoolPrefix = profilePrefix,
                    Priority = 2.8f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "FrontPressure>=High"
                });
            }

            if (snapshot.PrisonerStakes >= PrisonerStakes.Medium)
            {
                opps.Add(new DutyOpportunity
                {
                    Shape = DutyOpportunityShape.Episodic,
                    PoolPrefix = profilePrefix,
                    Priority = 2.2f,
                    AsOf = CampaignTime.Now,
                    TriggerReason = "PrisonerStakes>=Medium"
                });
            }

            // Quiet-stretch fallback — always add a routine opportunity at low
            // priority so every tick has SOMETHING to emit unless throttle blocks.
            opps.Add(new DutyOpportunity
            {
                Shape = DutyOpportunityShape.Episodic,
                PoolPrefix = profilePrefix,
                Priority = 1.0f,
                AsOf = CampaignTime.Now,
                TriggerReason = "routine"
            });

            // Stable descending sort — insertion order breaks priority ties, so
            // ArcScale opportunities surface first at equal priority.
            return opps.OrderByDescending(o => o.Priority).ToList();
        }
    }
}
