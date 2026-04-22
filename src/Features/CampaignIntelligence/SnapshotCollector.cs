using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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

        /// <summary>
        /// Scan lord-owned parties and friendly settlements within a 50-unit radius of
        /// the enlisted lord's party to populate counts, nearest-hostile distance and
        /// strength ratio, allied-relief plausibility, threatened-settlement count plus
        /// reference, and the frontier-heating-up heuristic.
        /// </summary>
        private static void CollectStrategicNeighborhood(ref IntelligenceInputs inputs, Hero lord)
        {
            var party = inputs.LordParty;
            if (party == null)
            {
                return;
            }

            const float NearbyRadius = 50f;

            int hostileCount = 0;
            int alliedCount = 0;
            float nearestHostileDistance = float.PositiveInfinity;
            MobileParty nearestHostile = null;

            var partyPos = party.GetPosition2D;

            foreach (var candidate in MobileParty.AllLordParties)
            {
                if (candidate == null || candidate == party)
                {
                    continue;
                }
                if (!candidate.IsActive || candidate.IsDisbanding)
                {
                    continue;
                }

                var distance = candidate.GetPosition2D.Distance(partyPos);
                if (distance > NearbyRadius)
                {
                    continue;
                }

                bool isHostile = FactionManager.IsAtWarAgainstFaction(candidate.MapFaction, party.MapFaction);
                if (isHostile)
                {
                    hostileCount++;
                    if (distance < nearestHostileDistance)
                    {
                        nearestHostileDistance = distance;
                        nearestHostile = candidate;
                    }
                }
                else if (candidate.MapFaction == party.MapFaction)
                {
                    alliedCount++;
                }
            }

            inputs.NearbyHostileCount = hostileCount;
            inputs.NearbyAlliedCount = alliedCount;
            inputs.NearestHostileDistance = hostileCount > 0 ? nearestHostileDistance : float.PositiveInfinity;
            if (nearestHostile != null && party.Party != null && party.Party.EstimatedStrength > 0f)
            {
                inputs.NearestHostileStrengthRatio = nearestHostile.Party.EstimatedStrength / party.Party.EstimatedStrength;
            }
            inputs.NearbyAlliedReliefPlausible = alliedCount >= 2;

            // Friendly settlements under siege within the same radius feed the
            // threatened-settlement signal and the frontier-heating-up heuristic.
            int threatenedCount = 0;
            Settlement nearestThreatened = null;
            float nearestThreatenedDistance = float.PositiveInfinity;

            foreach (var settlement in Settlement.All)
            {
                if (settlement == null || settlement.MapFaction != party.MapFaction)
                {
                    continue;
                }
                var sd = settlement.GetPosition2D.Distance(partyPos);
                if (sd > NearbyRadius)
                {
                    continue;
                }

                if (settlement.IsUnderSiege)
                {
                    threatenedCount++;
                    if (sd < nearestThreatenedDistance)
                    {
                        nearestThreatenedDistance = sd;
                        nearestThreatened = settlement;
                    }
                }
            }

            inputs.ThreatenedFriendlySettlementCount = threatenedCount;
            inputs.NearestThreatenedFriendly = nearestThreatened;
            inputs.FrontierHeatingUp = threatenedCount > 0 || hostileCount >= 3;
        }

        /// <summary>
        /// Populate coarse information-evidence inputs: recent war/peace/army transition
        /// flags come directly from the behavior's pending-flags accumulator, and the
        /// notable-prisoner count scans the lord's PrisonRoster for clan leaders or
        /// members of noble clans. Richer evidence signals (scout tracks, enemy buildup,
        /// garrison observations) are left as false placeholders for future plans that
        /// own the player-observation surface.
        /// </summary>
        private static void CollectInformationEvidence(ref IntelligenceInputs inputs, Hero lord)
        {
            inputs.RecentWarTransition = (inputs.PendingFlagsAtTickStart & RecentChangeFlags.WarDeclared) != 0;
            inputs.RecentPeaceTransition = (inputs.PendingFlagsAtTickStart & RecentChangeFlags.MakePeace) != 0;
            inputs.RecentArmyFormation = (inputs.PendingFlagsAtTickStart & RecentChangeFlags.ArmyFormed) != 0;
            inputs.RecentArmyDispersal = (inputs.PendingFlagsAtTickStart & RecentChangeFlags.ArmyDispersed) != 0;

            inputs.ImportantPrisonerCount = 0;
            var party = inputs.LordParty;
            if (party != null && party.PrisonRoster != null)
            {
                for (int i = 0; i < party.PrisonRoster.Count; i++)
                {
                    var element = party.PrisonRoster.GetElementCopyAtIndex(i);
                    if (element.Character != null && element.Character.IsHero && element.Character.HeroObject != null)
                    {
                        var prisoner = element.Character.HeroObject;
                        if (prisoner.Clan?.Leader == prisoner || prisoner.Clan?.IsNoble == true)
                        {
                            inputs.ImportantPrisonerCount++;
                        }
                    }
                }
            }

            inputs.HasFreshTracks = false;
            inputs.HasEnemyBuildupEvidence = false;
            inputs.HasGarrisonObservation = false;
        }

        /// <summary>
        /// Populate player-relevance inputs: current enlistment tier, player/lord
        /// relation, and a proximity check against the player's own MobileParty.
        /// Falls back to PlayerProximate=true when either party reference is missing,
        /// matching the enlisted escort-AI default that keeps the player tethered.
        /// </summary>
        private static void CollectPlayerRelevance(ref IntelligenceInputs inputs, Hero lord)
        {
            var player = Hero.MainHero;
            if (player == null || lord == null)
            {
                return;
            }

            inputs.PlayerTier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 0;
            inputs.PlayerRelationWithLord = lord.GetRelation(player);

            var playerParty = PartyBase.MainParty?.MobileParty;
            if (playerParty != null && inputs.LordParty != null)
            {
                var distance = playerParty.GetPosition2D.Distance(inputs.LordParty.GetPosition2D);
                inputs.PlayerProximate = distance < 3f;
            }
            else
            {
                inputs.PlayerProximate = true;
            }
        }
    }
}
