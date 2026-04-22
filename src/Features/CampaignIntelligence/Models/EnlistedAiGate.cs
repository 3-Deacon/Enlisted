using Enlisted.Features.Enlistment.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.CampaignIntelligence.Models
{
    /// <summary>
    /// Shared per-call identity gate for every enlisted-only AI model wrapper.
    /// Returns true only when the player is actively enlisted, the given party
    /// belongs to the enlisted lord, and Plan 1's intelligence snapshot is
    /// available. Every model wrapper must call this before applying bias;
    /// when it returns false, the caller delegates unmodified to BaseModel.
    /// </summary>
    internal static class EnlistedAiGate
    {
        /// <summary>
        /// Tests whether <paramref name="party"/> is the enlisted lord's current
        /// party and, if so, exposes the latest intelligence snapshot via
        /// <paramref name="snapshot"/>. Cheap reference compares + one property
        /// read; safe to call on every AI tick.
        /// </summary>
        public static bool TryGetSnapshotForParty(
            MobileParty party,
            out EnlistedLordIntelligenceSnapshot snapshot)
        {
            snapshot = null;
            if (party == null)
            {
                return false;
            }
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return false;
            }
            var lord = enlistment.EnlistedLord;
            if (lord == null || !lord.IsAlive)
            {
                return false;
            }
            var lordParty = lord.PartyBelongedTo;
            if (lordParty == null || lordParty != party)
            {
                return false;
            }
            var intel = EnlistedCampaignIntelligenceBehavior.Instance;
            if (intel == null)
            {
                return false;
            }
            snapshot = intel.Current;
            return snapshot != null;
        }

        /// <summary>
        /// Returns true when the enlisted lord's party is currently the army
        /// leader's party. When false, Plan 2 bias is inert because the
        /// enlisted lord has ceded strategy to the army leader. Used by
        /// wrappers that only apply under leader authority.
        /// </summary>
        public static bool IsEnlistedLordArmyLeader()
        {
            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            var party = lord?.PartyBelongedTo;
            var army = party?.Army;
            return army != null && army.LeaderParty == party;
        }
    }
}
