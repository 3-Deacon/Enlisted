using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Pure static collector. Reads live TaleWorlds state for the enlisted lord
    /// and packages it into an IntelligenceInputs struct for classification.
    /// Returns an empty inputs struct with LordAlive=false when the lord is not
    /// available; classifier treats that as "no assessment possible this tick."
    /// </summary>
    internal static class SnapshotCollector
    {
        /// <summary>
        /// Collect all inputs for the given lord. Caller must already have gated on
        /// IsEnlisted. Exceptions in any sub-collector are caught and logged; the
        /// returned struct contains whatever was populated before the throw plus the
        /// pending flags carried over from the behavior's dirty-bit accumulator.
        /// </summary>
        public static IntelligenceInputs Collect(Hero lord, RecentChangeFlags pendingFlags)
        {
            var inputs = new IntelligenceInputs
            {
                PendingFlagsAtTickStart = pendingFlags
            };

            try
            {
                CollectLordAndPartyState(ref inputs, lord);
                CollectStrategicNeighborhood(ref inputs, lord);
                CollectInformationEvidence(ref inputs, lord);
                CollectPlayerRelevance(ref inputs, lord);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTEL", "collector_failed", ex);
            }

            return inputs;
        }

        /// <summary>
        /// Populate lord/party-state inputs: alive/prisoner/wounded status, the lord's
        /// party (if any), current/besieged settlement, map-event presence, army branch
        /// (cohesion, type, gathering proxy), food days remaining, and the party-size
        /// and wounded ratios. Skips party-derived fields when the lord has no party
        /// (typical when a prisoner); LordAlive / LordIsPrisoner are always set first
        /// so the classifier always has basic identity even on early return.
        /// </summary>
        private static void CollectLordAndPartyState(ref IntelligenceInputs inputs, Hero lord)
        {
            if (lord == null)
            {
                return;
            }

            // Pre-seed lord identity before any party lookup so classifier always has
            // LordAlive and LordIsPrisoner even if subsequent reads bail out.
            inputs.LordAlive = lord.IsAlive;
            inputs.LordIsPrisoner = lord.IsPrisoner;
            inputs.LordIsWounded = lord.IsWounded;

            var party = lord.PartyBelongedTo;
            if (party == null)
            {
                return;
            }

            inputs.LordParty = party;
            inputs.CurrentSettlement = party.CurrentSettlement;
            inputs.BesiegedSettlement = party.BesiegedSettlement;
            inputs.InMapEvent = party.MapEvent != null;

            var army = party.Army;
            if (army != null)
            {
                inputs.InArmy = true;
                inputs.IsArmyLeader = army.LeaderParty == party;
                inputs.ArmyType = army.ArmyType;
                inputs.ArmyCohesion = army.Cohesion;

                // Gathering proxy: we're in the army but not the leader and we haven't
                // formed up on the leader yet (AttachedTo points elsewhere or is null).
                inputs.ArmyIsGathering = !inputs.IsArmyLeader && party.AttachedTo != army.LeaderParty;
            }

            // Food days remaining. FoodChange is the daily delta (negative = net
            // consumption). When non-negative (e.g. hero in a settlement being fed,
            // or a party with net gain), treat remaining days as infinite so downstream
            // supply-pressure classification skips this party.
            float foodChange = party.FoodChange;
            inputs.FoodDaysRemaining = foodChange < -0.001f
                ? party.Food / -foodChange
                : float.PositiveInfinity;

            // PartySizeRatio is a native MobileParty float (NumberOfAllMembers / PartySizeLimit).
            inputs.PartySizeRatio = party.PartySizeRatio;

            var roster = party.MemberRoster;
            if (roster != null)
            {
                int total = roster.TotalManCount;
                inputs.WoundedRatio = total > 0 ? roster.TotalWounded / (float)total : 0f;
            }
        }

        private static void CollectStrategicNeighborhood(ref IntelligenceInputs inputs, Hero lord) { }

        private static void CollectInformationEvidence(ref IntelligenceInputs inputs, Hero lord) { }

        private static void CollectPlayerRelevance(ref IntelligenceInputs inputs, Hero lord) { }
    }
}
