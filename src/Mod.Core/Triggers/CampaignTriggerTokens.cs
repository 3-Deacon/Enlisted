using System;
using System.Collections.Generic;

namespace Enlisted.Mod.Core.Triggers
{
    /// <summary>
    /// Canonical trigger token names (Phase 1 "trigger vocabulary").
    ///
    /// - Content packs should only use tokens from this vocabulary.
    /// - Some tokens are recognized but intentionally not implemented yet (they will always evaluate false).
    ///   Those are safe to ship in data, but stories depending on them will not trigger until the provider exists.
    /// </summary>
    public static class CampaignTriggerTokens
    {
        // Time-of-day windows
        public const string Dawn = "dawn";
        public const string Morning = "morning";
        public const string Afternoon = "afternoon";
        public const string Evening = "evening";
        public const string Day = "day";
        public const string Dusk = "dusk";
        public const string Night = "night";
        public const string LateNight = "late_night";

        // Convenience token used by some content tables.
        public const string AnyTime = "any";

        // Settlement entry/exit (player party)
        public const string EnteredSettlement = "entered_settlement";
        public const string EnteredTown = "entered_town";
        public const string EnteredCastle = "entered_castle";
        public const string EnteredVillage = "entered_village";
        public const string LeftSettlement = "left_settlement";

        // Location context (planned; used by event metadata vocabulary)
        public const string InSettlement = "in_settlement";
        public const string NotInSettlement = "not_in_settlement";

        // Battle / map event (player party / campaign)
        public const string LeavingBattle = "leaving_battle";

        // Battle timing (planned; event vocabulary)
        public const string BeforeBattle = "before_battle";
        public const string AfterBattle = "after_battle";

        // Tick hints (planned; used by data as coarse cadence labels)
        public const string DailyTick = "daily_tick";
        public const string WeeklyTick = "weekly_tick";
        public const string RandomChance = "random_chance";
        public const string Chance = "chance";

        // Movement/camp context (planned; used by event metadata vocabulary)
        public const string ArmyMoving = "army_moving";

        // Simple world/camp context (planned)
        public const string CampEstablished = "camp_established";
        public const string EnemyNearby = "enemy_nearby";
        public const string WoundedInCamp = "wounded_in_camp";
        public const string AtSea = "at_sea";

        // Player state gates (planned)
        public const string IsEnlisted = "is_enlisted";
        public const string AiSafe = "ai_safe";

        // Elapsed day counters (support simple comparisons in triggers)
        public const string DaysSinceEnlistment = "days_since_enlistment";
        public const string DaysSincePromotion = "days_since_promotion";
        public const string DaysEnlisted = "days_enlisted";

        // Numeric comparison prefixes used by metadata-driven content (Phase 5)
        public const string DaysFromTown = "days_from_town";
        public const string Gold = "gold";
        public const string LogisticsStrain = "logistics_strain";
        public const string MoraleShock = "morale_shock";
        public const string PayTension = "pay_tension";
        public const string Scrutiny = "scrutiny";
        public const string CampReputation = "camp_reputation";
        public const string MedicalRisk = "medical_risk";


        // Story flags (Decision Events): allow authored content to require/negate a named story flag.
        // Flags are free-form (set/cleared by events), so we use explicit prefixes rather than enumerating them.
        public const string FlagPrefix = "flag:";
        public const string NotFlagPrefix = "not:flag:";

        // Decision Events (Track D2): Activity-aware event tokens
        // Used by decision events to match the player's current schedule activity
        public const string CurrentActivityPrefix = "current_activity:";

        // Decision Events (Track D2): Duty-aware event tokens
        // Used by decision events to match the player's assigned duty role
        public const string OnDutyPrefix = "on_duty:";

        // Camp conditions (planned; provided by Camp Life snapshot in later phases)
        public const string LogisticsHigh = "logistics_high";
        public const string MoraleLow = "morale_low";
        public const string PayTensionHigh = "pay_tension_high";
        public const string ScrutinyHigh = "scrutiny_high";

        // Escalation thresholds (0-100 scale for scrutiny)
        public const string Scrutiny20 = "scrutiny_20";
        public const string Scrutiny40 = "scrutiny_40";
        public const string Scrutiny60 = "scrutiny_60";
        public const string Scrutiny80 = "scrutiny_80";
        public const string Scrutiny100 = "scrutiny_100";

        public const string CampRep20 = "camp_rep_20";
        public const string CampRep40 = "camp_rep_40";
        public const string CampRepNeg20 = "camp_rep_-20";
        public const string CampRepNeg40 = "camp_rep_-40";

        public const string Medical3 = "medical_3";
        public const string Medical4 = "medical_4";
        public const string Medical5 = "medical_5";

        // Player condition gates (Phase 5)
        public const string HasInjury = "has_injury";
        public const string HasIllness = "has_illness";
        public const string HasCondition = "has_condition";

        // Phase 7: Faction capability tokens (for formation choice events)
        public const string FactionHasHorseArchers = "faction_has_horse_archers";

        private static readonly HashSet<string> RecognizedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Dawn, Morning, Afternoon, Evening, Day, Dusk, Night, LateNight, AnyTime,
            EnteredSettlement, EnteredTown, EnteredCastle, EnteredVillage, LeftSettlement,
            InSettlement, NotInSettlement,
            LeavingBattle, BeforeBattle, AfterBattle,
            DailyTick, WeeklyTick, RandomChance, Chance,
            ArmyMoving,
            CampEstablished, EnemyNearby, WoundedInCamp, AtSea,
            IsEnlisted, AiSafe,
            LogisticsHigh, MoraleLow, PayTensionHigh, ScrutinyHigh,
            Scrutiny20, Scrutiny40, Scrutiny60, Scrutiny80, Scrutiny100,
            CampRep20, CampRep40, CampRepNeg20, CampRepNeg40,
            Medical3, Medical4, Medical5,
            HasInjury, HasIllness, HasCondition,
            FactionHasHorseArchers
        };

        private static readonly HashSet<string> ImplementedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Implemented today means "we have a concrete provider in shipping code".
            // Recognized-but-unimplemented tokens are intentionally allowed in data; they just evaluate false until implemented.
            Dawn, Morning, Afternoon, Evening, Day, Dusk, Night, LateNight,
            EnteredSettlement, EnteredTown, EnteredCastle, EnteredVillage, LeftSettlement,
            LeavingBattle,
            InSettlement, NotInSettlement,
            BeforeBattle, AfterBattle,
            DailyTick, WeeklyTick, RandomChance, Chance,
            ArmyMoving,
            CampEstablished, EnemyNearby, WoundedInCamp, AtSea,
            IsEnlisted, AiSafe,
            LogisticsHigh, MoraleLow, PayTensionHigh, ScrutinyHigh,
            Scrutiny20, Scrutiny40, Scrutiny60, Scrutiny80, Scrutiny100,
            CampRep20, CampRep40, CampRepNeg20, CampRepNeg40,
            Medical3, Medical4, Medical5,
            HasInjury, HasIllness, HasCondition,
            FactionHasHorseArchers
        };

        public static bool IsRecognized(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var trimmed = token.Trim();

            // Support prefix tokens without forcing every possible value into the static set.
            if (trimmed.StartsWith(FlagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Length > FlagPrefix.Length;
            }

            if (trimmed.StartsWith(NotFlagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Length > NotFlagPrefix.Length;
            }

            // Activity-aware tokens (Track D2: Decision Events)
            if (trimmed.StartsWith(CurrentActivityPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Length > CurrentActivityPrefix.Length;
            }

            // Duty-aware tokens (Track D2: Decision Events)
            if (trimmed.StartsWith(OnDutyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Length > OnDutyPrefix.Length;
            }

            // Support comparison-style tokens used in onboarding packs, e.g. "days_since_enlistment < 1".
            if (trimmed.StartsWith(DaysSinceEnlistment, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(DaysSincePromotion, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(DaysEnlisted, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(DaysFromTown, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(Gold, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(LogisticsStrain, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(MoraleShock, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(PayTension, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(Scrutiny, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(CampReputation, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(MedicalRisk, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return RecognizedTokens.Contains(trimmed);
        }

        public static bool IsImplemented(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var trimmed = token.Trim();

            // Prefix tokens: implemented when we have an evaluator/provider.
            if (trimmed.StartsWith(FlagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (trimmed.StartsWith(NotFlagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Activity-aware tokens (Track D2: Decision Events)
            if (trimmed.StartsWith(CurrentActivityPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Duty-aware tokens (Track D2: Decision Events)
            if (trimmed.StartsWith(OnDutyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (trimmed.StartsWith(DaysSinceEnlistment, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(DaysSincePromotion, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(DaysEnlisted, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(DaysFromTown, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(Gold, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(LogisticsStrain, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(MoraleShock, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(PayTension, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(Scrutiny, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(CampReputation, StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith(MedicalRisk, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ImplementedTokens.Contains(trimmed);
        }

        public static IReadOnlyCollection<string> GetRecognizedTokens()
        {
            return RecognizedTokens;
        }
    }
}


