using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Activities.Orders;
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

            // Named orders are proposed before episodic duty beats by the emitter.
            // The acute snapshot gates raise priority when the campaign state clearly
            // asks for a specific order, while the profile fallbacks ensure an enlisted
            // soldier can still receive ordinary NCO orders during quiet service.
            if (IsProfile(currentDutyProfile, DutyProfileIds.Marching)
                && snapshot.InformationConfidence == InformationConfidence.Low)
            {
                AddArc(opps, "order_scout_accept", "order_scout_", 3.2f, "InformationConfidence=Low");
            }

            if (IsProfile(currentDutyProfile, DutyProfileIds.Garrisoned)
                && snapshot.RecoveryNeed >= RecoveryNeed.High)
            {
                AddArc(opps, "order_treat_wounded_accept", "order_treat_wounded_", 3.2f, "RecoveryNeed>=High");
            }

            if (IsProfile(currentDutyProfile, DutyProfileIds.Besieging)
                && snapshot.Posture == StrategicPosture.OffensiveSiege)
            {
                AddArc(opps, "order_siege_works_accept", "order_siege_works_", 3.5f, "Posture=OffensiveSiege");
            }

            AddProfileNamedOrders(opps, currentDutyProfile);

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
                    Priority = 2.8f,
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

        private static void AddProfileNamedOrders(List<DutyOpportunity> opps, string currentDutyProfile)
        {
            if (IsProfile(currentDutyProfile, DutyProfileIds.Besieging))
            {
                AddArc(opps, "order_siege_works_accept", "order_siege_works_", 3.0f, "profile=besieging");
                AddArc(opps, "order_guard_post_accept", "order_guard_post_", 2.2f, "profile=besieging");
                AddArc(opps, "order_fatigue_detail_accept", "order_fatigue_detail_", 2.0f, "profile=besieging");
                return;
            }

            if (IsProfile(currentDutyProfile, DutyProfileIds.Garrisoned))
            {
                AddArc(opps, "order_guard_post_accept", "order_guard_post_", 2.4f, "profile=garrisoned");
                AddArc(opps, "order_inspection_accept", "order_inspection_", 2.2f, "profile=garrisoned");
                AddArc(opps, "order_drill_accept", "order_drill_", 2.0f, "profile=garrisoned");
                AddArc(opps, "order_treat_wounded_accept", "order_treat_wounded_", 1.9f, "profile=garrisoned");
                return;
            }

            if (IsProfile(currentDutyProfile, DutyProfileIds.Escorting))
            {
                AddArc(opps, "order_escort_accept", "order_escort_", 2.4f, "profile=escorting");
                AddArc(opps, "order_patrol_accept", "order_patrol_", 2.0f, "profile=escorting");
                AddArc(opps, "order_courier_accept", "order_courier_", 1.9f, "profile=escorting");
                return;
            }

            if (IsProfile(currentDutyProfile, DutyProfileIds.Raiding))
            {
                AddArc(opps, "order_scout_accept", "order_scout_", 2.2f, "profile=raiding");
                AddArc(opps, "order_patrol_accept", "order_patrol_", 2.0f, "profile=raiding");
                AddArc(opps, "order_fatigue_detail_accept", "order_fatigue_detail_", 1.8f, "profile=raiding");
                return;
            }

            if (IsProfile(currentDutyProfile, DutyProfileIds.Marching))
            {
                AddArc(opps, "order_scout_accept", "order_scout_", 2.4f, "profile=marching");
                AddArc(opps, "order_courier_accept", "order_courier_", 2.2f, "profile=marching");
                AddArc(opps, "order_patrol_accept", "order_patrol_", 2.0f, "profile=marching");
                AddArc(opps, "order_escort_accept", "order_escort_", 1.8f, "profile=marching");
                return;
            }

            if (IsProfile(currentDutyProfile, DutyProfileIds.Imprisoned))
            {
                return;
            }

            AddArc(opps, "order_patrol_accept", "order_patrol_", 2.0f, "profile=wandering");
            AddArc(opps, "order_courier_accept", "order_courier_", 1.9f, "profile=wandering");
            AddArc(opps, "order_fatigue_detail_accept", "order_fatigue_detail_", 1.8f, "profile=wandering");
            AddArc(opps, "order_drill_accept", "order_drill_", 1.6f, "profile=wandering");
        }

        private static void AddArc(
            List<DutyOpportunity> opps,
            string archetypeStoryletId,
            string poolPrefix,
            float priority,
            string triggerReason)
        {
            if (opps == null || string.IsNullOrEmpty(archetypeStoryletId))
            {
                return;
            }

            if (opps.Any(o => string.Equals(o.ArchetypeStoryletId, archetypeStoryletId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            opps.Add(new DutyOpportunity
            {
                Shape = DutyOpportunityShape.ArcScale,
                ArchetypeStoryletId = archetypeStoryletId,
                PoolPrefix = poolPrefix,
                Priority = priority,
                AsOf = CampaignTime.Now,
                TriggerReason = triggerReason
            });
        }

        private static bool IsProfile(string currentDutyProfile, string expected)
        {
            return string.Equals(currentDutyProfile, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
