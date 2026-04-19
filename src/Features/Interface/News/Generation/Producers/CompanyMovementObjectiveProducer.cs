using Enlisted.Features.Context;
using Enlisted.Features.Interface.News.Models;
using TaleWorlds.CampaignSystem.Settlements;

namespace Enlisted.Features.Interface.News.Generation.Producers
{
    /// <summary>
    /// Company facts: movement, objective tag, settlement stops, and army attachment context.
    /// </summary>
    public sealed class CompanyMovementObjectiveProducer : IDailyReportFactProducer
    {
        public void Contribute(DailyReportSnapshot snapshot, CampNewsContext context)
        {
            if (snapshot == null || context == null)
            {
                return;
            }

            var party = context.LordParty;
            snapshot.LordPartyId = party?.StringId ?? string.Empty;

            // Populate strategic context tag.
            if (party != null)
            {
                snapshot.StrategicContextTag = ArmyContextAnalyzer.GetLordStrategicContext(party);
            }

            // Food bands (best-effort) from the CampLife snapshot meters.
            var camp = context.CampLife;
            if (camp != null && camp.IsActiveWhileEnlisted())
            {
                // Food proxy: logistics strain correlates with time away from towns and disruption.
                var logistics = camp.LogisticsStrain;
                snapshot.Food = logistics switch
                {
                    < 25f => FoodBand.Plenty,
                    < 55f => FoodBand.Thin,
                    < 80f => FoodBand.Low,
                    _ => FoodBand.Critical
                };
            }

            // Army attachment context (acknowledge as context only; we avoid army-wide simulation).
            if (party?.Army != null)
            {
                var leader = party.Army.LeaderParty?.LeaderHero;
                var leaderName = leader?.Name?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(leaderName))
                {
                    context.Generation.AttachedArmyLeaderName = leaderName;
                    snapshot.AttachedArmyTag = "attached_to_army";
                }
            }

            // Current objective/movement.
            if (party?.Party?.MapEvent != null)
            {
                snapshot.ObjectiveTag = "battle";
                snapshot.BattleTag = "engaged";
                snapshot.Threat = ThreatBand.High;
                context.Generation.CompanySettlementName = string.Empty;
                context.Generation.TargetSettlementName = string.Empty;
                return;
            }

            if (party?.Party?.SiegeEvent != null)
            {
                snapshot.ObjectiveTag = "siege";
                snapshot.BattleTag = "siege";
                snapshot.Threat = ThreatBand.High;
                context.Generation.CompanySettlementName = string.Empty;
                context.Generation.TargetSettlementName = string.Empty;
                return;
            }

            if (party?.CurrentSettlement != null)
            {
                snapshot.ObjectiveTag = "camped";
                context.Generation.CompanySettlementName = party.CurrentSettlement.Name?.ToString() ?? string.Empty;
                context.Generation.TargetSettlementName = string.Empty;
            }
            else if (party?.TargetSettlement != null)
            {
                snapshot.ObjectiveTag = "marching";
                context.Generation.TargetSettlementName = party.TargetSettlement.Name?.ToString() ?? string.Empty;
                context.Generation.CompanySettlementName = string.Empty;
            }
            else
            {
                snapshot.ObjectiveTag = party?.Army != null ? "army_march" : "marching";
                context.Generation.CompanySettlementName = string.Empty;
                context.Generation.TargetSettlementName = string.Empty;
            }

            // Threat band (best-effort): prefer cheap checks, then token checks.
            if (snapshot.Threat == ThreatBand.Unknown)
            {
                try
                {
                    var trackerBehavior = context.TriggerTracker;
                    if (trackerBehavior != null && trackerBehavior.IsWithinDays(trackerBehavior.LastMapEventEndedTime, 2f))
                    {
                        snapshot.Threat = ThreatBand.Medium;
                    }
                    else
                    {
                        snapshot.Threat = ThreatBand.Low;
                    }

                    // Threat levels are currently determined by recent map events and the party's immediate surroundings.
                }
                catch
                {
                    snapshot.Threat = snapshot.Threat == ThreatBand.Unknown ? ThreatBand.Low : snapshot.Threat;
                }
            }

            // Recent settlement stop (best-effort grounding for rumors + logistics talk).
            // Use the shared tracker so we avoid scanning the world.
            var tracker = context.TriggerTracker;
            if (tracker == null)
            {
                return;
            }

            if (tracker.LastSettlementEnteredTime != TaleWorlds.CampaignSystem.CampaignTime.Zero &&
                tracker.IsWithinDays(tracker.LastSettlementEnteredTime, 1f))
            {
                var lastId = tracker.LastSettlementEnteredId;
                if (!string.IsNullOrWhiteSpace(lastId))
                {
                    var settlement = Settlement.Find(lastId);
                    var name = settlement?.Name?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(name) && settlement != null)
                    {
                        snapshot.LastStopTag = settlement.IsTown
                            ? "town_stop"
                            : settlement.IsVillage
                                ? "village_stop"
                                : "settlement_stop";

                        context.Generation.LastStopSettlementName = name;
                    }
                }
            }
        }
    }
}


