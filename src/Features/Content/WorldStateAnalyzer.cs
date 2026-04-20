using System.Linq;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Analyzes current world state to determine appropriate content context.
    /// Provides situation analysis for the Content Orchestrator.
    /// </summary>
    public static class WorldStateAnalyzer
    {
        private const string LogCategory = "WorldState";

        /// <summary>
        /// Analyzes the current world situation for content selection.
        /// </summary>
        public static WorldSituation AnalyzeSituation()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return CreateDefaultSituation();
            }

            var lord = enlistment.EnlistedLord;
            var party = lord?.PartyBelongedTo;

            if (lord == null || party == null)
            {
                ModLogger.Debug(LogCategory, "Lord or party is null, returning default situation");
                return CreateDefaultSituation();
            }

            // Analyze what lord is doing
            var lordSituation = DetermineLordSituation(lord, party);

            // Analyze kingdom war status
            var warStance = DetermineWarStance(lord.MapFaction);

            // Determine life phase
            var lifePhase = DetermineLifePhase(lordSituation, warStance);

            // Calculate expected activity level
            var activityLevel = DetermineActivityLevel(lifePhase, lordSituation);

            // Map to realistic frequency
            var frequency = MapToRealisticFrequency(lifePhase, activityLevel);

            // Get current day phase
            var currentHour = CampaignTime.Now.GetHourOfDay;
            var dayPhase = GetDayPhaseFromHour(currentHour);

            // Detect sea/land travel context
            var travelContext = DetectTravelContext(party);

            // Analyze medical pressure for orchestrator integration
            var (medicalAnalysis, medicalLevel) = SimulationPressureCalculator.GetMedicalPressure();

            return new WorldSituation
            {
                LordIs = lordSituation,
                KingdomStance = warStance,
                CurrentPhase = lifePhase,
                ExpectedActivity = activityLevel,
                RealisticEventFrequency = frequency,
                CurrentDayPhase = dayPhase,
                CurrentHour = currentHour,
                CurrentSettlement = party.CurrentSettlement,
                TargetSettlement = party.TargetSettlement,
                DaysInCurrentPhase = 0, // TODO: Track phase duration
                InEnemyTerritory = IsInEnemyTerritory(lord, party),
                TravelContext = travelContext,
                MedicalPressure = medicalLevel,
                RequiresMedicalCare = medicalAnalysis.HasCondition && medicalAnalysis.IsUntreated,
                HasCriticalCondition = medicalAnalysis.HasSevereCondition
            };
        }

        /// <summary>
        /// Maps internal world state to event system context string for filtering.
        /// Used by EventRequirementChecker to filter eligible narrative events.
        /// Returns simplified context values: "Camp", "War", "Siege", "Any".
        /// </summary>
        public static string GetEventContext(WorldSituation situation)
        {
            return situation.LordIs switch
            {
                LordSituation.PeacetimeGarrison => "Camp",
                LordSituation.SiegeAttacking => "Siege",
                LordSituation.SiegeDefending => "Siege",
                LordSituation.WarMarching => "War",
                LordSituation.WarActiveCampaign => "War",
                LordSituation.Defeated => "Camp",  // Recovery counts as garrison
                LordSituation.Captured => "Camp",
                _ => "Any"
            };
        }

        /// <summary>
        /// Returns granular world state key for order event weighting.
        /// Order events use detailed world_state requirements for context-specific event selection.
        /// </summary>
        public static string GetOrderEventWorldState(WorldSituation situation)
        {
            return (situation.LordIs, situation.KingdomStance) switch
            {
                (LordSituation.PeacetimeGarrison, WarStance.Peace) => "peacetime_garrison",
                (LordSituation.PeacetimeRecruiting, WarStance.Peace) => "peacetime_recruiting",
                (LordSituation.WarMarching, _) => "war_marching",
                (LordSituation.WarActiveCampaign, _) => "war_active_campaign",
                (LordSituation.SiegeAttacking, _) => "siege_attacking",
                (LordSituation.SiegeDefending, _) => "siege_defending",
                (LordSituation.Defeated, _) => "lord_defeated",
                (LordSituation.Captured, _) => "lord_captured",
                _ => "peacetime_garrison"  // Default to safest/quietest
            };
        }

        /// <summary>
        /// Gets the current day phase based on the current campaign time.
        /// Convenience method that wraps GetDayPhaseFromHour.
        /// </summary>
        public static DayPhase GetCurrentDayPhase()
        {
            return GetDayPhaseFromHour(CampaignTime.Now.GetHourOfDay);
        }

        /// <summary>
        /// Gets the current day phase from hour of day.
        /// </summary>
        public static DayPhase GetDayPhaseFromHour(int hour)
        {
            return hour switch
            {
                >= 6 and <= 11 => DayPhase.Dawn,
                >= 12 and <= 17 => DayPhase.Midday,
                >= 18 and <= 21 => DayPhase.Dusk,
                _ => DayPhase.Night  // 22-5
            };
        }

        private static WorldSituation CreateDefaultSituation()
        {
            return new WorldSituation
            {
                LordIs = LordSituation.PeacetimeGarrison,
                KingdomStance = WarStance.Peace,
                CurrentPhase = LifePhase.Peacetime,
                ExpectedActivity = ActivityLevel.Quiet,
                RealisticEventFrequency = 1.0f,
                CurrentDayPhase = GetDayPhaseFromHour(CampaignTime.Now.GetHourOfDay),
                CurrentHour = CampaignTime.Now.GetHourOfDay,
                TravelContext = TravelContext.Land,
                MedicalPressure = MedicalPressureLevel.None,
                RequiresMedicalCare = false,
                HasCriticalCondition = false
            };
        }

        private static LordSituation DetermineLordSituation(Hero lord, MobileParty party)
        {
            // Check if lord is captured
            if (lord.IsPrisoner)
            {
                return LordSituation.Captured;
            }

            // Check if in siege
            if (party.SiegeEvent != null)
            {
                var besiegedSettlement = party.SiegeEvent.BesiegedSettlement;
                if (besiegedSettlement?.OwnerClan?.MapFaction == lord.MapFaction)
                {
                    return LordSituation.SiegeDefending;
                }
                return LordSituation.SiegeAttacking;
            }

            // Check if in settlement
            if (party.CurrentSettlement != null)
            {
                var isAtWar = IsKingdomAtWar(lord.MapFaction);
                return isAtWar ? LordSituation.WarMarching : LordSituation.PeacetimeGarrison;
            }

            // Check if in army
            if (party.Army != null)
            {
                return LordSituation.WarActiveCampaign;
            }

            // Moving on map
            var atWar = IsKingdomAtWar(lord.MapFaction);
            return atWar ? LordSituation.WarMarching : LordSituation.PeacetimeRecruiting;
        }

        private static WarStance DetermineWarStance(IFaction faction)
        {
            if (faction == null)
            {
                return WarStance.Peace;
            }

            var enemies = faction.FactionsAtWarWith
                .Where(f => f != null && f.IsKingdomFaction)
                .ToList();

            if (enemies.Count == 0)
            {
                return WarStance.Peace;
            }

            if (enemies.Count >= 3)
            {
                return WarStance.Desperate;
            }

            if (enemies.Count >= 2)
            {
                return WarStance.MultiWar;
            }

            // Single war - check if we're winning or losing
            // For now, simplified to offensive/defensive based on who declared
            // TODO: Add strength comparison for more accurate stance
            return WarStance.Offensive;
        }

        private static LifePhase DetermineLifePhase(LordSituation lordSituation, WarStance warStance)
        {
            // Check for crisis conditions
            if (warStance == WarStance.Desperate)
            {
                return LifePhase.Crisis;
            }

            // Check for recovery
            if (lordSituation == LordSituation.Defeated || lordSituation == LordSituation.Captured)
            {
                return LifePhase.Recovery;
            }

            // Check for siege
            if (lordSituation == LordSituation.SiegeAttacking || lordSituation == LordSituation.SiegeDefending)
            {
                return LifePhase.Siege;
            }

            // Check for campaign
            if (lordSituation == LordSituation.WarMarching || lordSituation == LordSituation.WarActiveCampaign)
            {
                return LifePhase.Campaign;
            }

            return LifePhase.Peacetime;
        }

        private static ActivityLevel DetermineActivityLevel(LifePhase lifePhase, LordSituation lordSituation)
        {
            return lifePhase switch
            {
                LifePhase.Crisis => ActivityLevel.Intense,
                LifePhase.Siege => ActivityLevel.Active,
                LifePhase.Campaign => lordSituation == LordSituation.WarActiveCampaign
                    ? ActivityLevel.Active
                    : ActivityLevel.Routine,
                LifePhase.Recovery => ActivityLevel.Quiet,
                LifePhase.Peacetime => lordSituation == LordSituation.PeacetimeGarrison
                    ? ActivityLevel.Quiet
                    : ActivityLevel.Routine,
                _ => ActivityLevel.Routine
            };
        }

        private static float MapToRealisticFrequency(LifePhase lifePhase, ActivityLevel activityLevel)
        {
            // Base frequency from activity level (events per week)
            var baseFrequency = activityLevel switch
            {
                ActivityLevel.Quiet => 1.0f,
                ActivityLevel.Routine => 3.0f,
                ActivityLevel.Active => 5.0f,
                ActivityLevel.Intense => 7.0f,
                _ => 3.0f
            };

            // Modify based on life phase
            var phaseModifier = lifePhase switch
            {
                LifePhase.Crisis => 1.2f,
                LifePhase.Siege => 1.1f,
                LifePhase.Campaign => 1.0f,
                LifePhase.Recovery => 0.8f,
                LifePhase.Peacetime => 0.9f,
                _ => 1.0f
            };

            return baseFrequency * phaseModifier;
        }

        private static bool IsKingdomAtWar(IFaction faction)
        {
            if (faction == null)
            {
                return false;
            }

            return faction.FactionsAtWarWith.Any(f => f != null && f.IsKingdomFaction);
        }

        private static bool IsInEnemyTerritory(Hero lord, MobileParty party)
        {
            var settlement = party.CurrentSettlement ?? party.TargetSettlement;
            if (settlement == null)
            {
                return false;
            }

            var settlementFaction = settlement.OwnerClan?.MapFaction;
            if (settlementFaction == null || lord.MapFaction == null)
            {
                return false;
            }

            return FactionManager.IsAtWarAgainstFaction(lord.MapFaction, settlementFaction);
        }

        /// <summary>
        /// Detects whether party is at sea or on land.
        /// Uses the native IsCurrentlyAtSea property available in modern Bannerlord versions.
        /// </summary>
        private static TravelContext DetectTravelContext(MobileParty party)
        {
            if (party == null)
            {
                return TravelContext.Land;
            }

            // BUGFIX: If party is in a settlement or besieging, they cannot be at sea
            // This prevents sea events/decisions from appearing when on land in settlements
            if (party.CurrentSettlement != null || party.BesiegedSettlement != null)
            {
                ModLogger.Debug(LogCategory,
                    $"Party in settlement or siege - land context (IsCurrentlyAtSea={party.IsCurrentlyAtSea})");
                return TravelContext.Land;
            }

            // Directly use IsCurrentlyAtSea property (available in base game)
            if (party.IsCurrentlyAtSea)
            {
                ModLogger.Debug(LogCategory, "Detected sea travel");
                return TravelContext.Sea;
            }

            return TravelContext.Land;
        }

        /// <summary>
        /// Returns travel context string for order text variant selection.
        /// Used by OrderCatalog to pick sea or land flavor text.
        /// </summary>
        public static string GetTravelContextKey(WorldSituation situation)
        {
            return situation.TravelContext == TravelContext.Sea ? "sea" : "land";
        }
    }
}
