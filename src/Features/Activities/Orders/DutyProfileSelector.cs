using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Resolves a duty profile from a MobileParty's current state. First match wins
    /// in priority order: Besieging, Raiding, Escorting, Garrisoned, Marching,
    /// Wandering. Never returns Imprisoned — that's set only by explicit force from
    /// DutyProfileBehavior.
    /// </summary>
    public static class DutyProfileSelector
    {
        public static string Resolve(MobileParty lordParty)
        {
            try
            {
                if (lordParty == null)
                {
                    return DutyProfileIds.Wandering;
                }

                // Priority 1: Besieging
                if (lordParty.BesiegedSettlement != null)
                {
                    return DutyProfileIds.Besieging;
                }

                // Priority 2: Raiding (lord's party AI is actively raiding a settlement)
                if (lordParty.DefaultBehavior == AiBehavior.RaidSettlement)
                {
                    return DutyProfileIds.Raiding;
                }

                // Priority 3: Escorting (subordinate inside an army, not the leader)
                if (lordParty.Army != null && lordParty.Army.LeaderParty != lordParty)
                {
                    return DutyProfileIds.Escorting;
                }

                // Priority 4: Garrisoned (stationary at a friendly settlement)
                if (lordParty.CurrentSettlement != null && !lordParty.IsMoving)
                {
                    var settlement = lordParty.CurrentSettlement;
                    if (lordParty.MapFaction != null && settlement.MapFaction != null
                        && !FactionManager.IsAtWarAgainstFaction(lordParty.MapFaction, settlement.MapFaction))
                    {
                        return DutyProfileIds.Garrisoned;
                    }
                }

                // Priority 5: Marching
                if (lordParty.IsMoving && lordParty.DefaultBehavior != AiBehavior.Hold)
                {
                    return DutyProfileIds.Marching;
                }

                // Priority 6: fallback
                return DutyProfileIds.Wandering;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTYPROFILE", "Resolve threw; defaulting to wandering", ex);
                return DutyProfileIds.Wandering;
            }
        }
    }
}
