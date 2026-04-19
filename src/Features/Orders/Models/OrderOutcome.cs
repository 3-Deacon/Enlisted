using System.Collections.Generic;

namespace Enlisted.Features.Orders.Models
{
    /// <summary>
    /// Represents the consequences of an order outcome (success, failure, or decline).
    /// Affects skills, traits, reputation, company needs, resources, and provides narrative feedback.
    /// </summary>
    public class OrderOutcome
    {
        /// <summary>
        /// Skill XP awards for this outcome.
        /// Key is skill name (e.g., "Scouting", "Leadership"), value is XP amount.
        /// Can be negative for penalties.
        /// </summary>
        public Dictionary<string, int> SkillXp { get; set; } = [];

        /// <summary>
        /// Trait XP awards for this outcome.
        /// Key is trait name (e.g., "Scout", "Commander"), value is XP amount.
        /// Can be negative for penalties.
        /// </summary>
        public Dictionary<string, int> TraitXp { get; set; } = [];

        /// <summary>
        /// Reputation changes with chain of command.
        /// Keys: "lord", "officer", "soldier".
        /// Values: reputation delta (positive or negative).
        /// </summary>
        public Dictionary<string, int> Reputation { get; set; } = [];

        /// <summary>
        /// Company need modifications.
        /// Keys: "Readiness", "Supplies".
        /// Values: delta to apply (positive improves, negative degrades).
        /// </summary>
        public Dictionary<string, int> CompanyNeeds { get; set; } = [];

        /// <summary>
        /// Escalation track modifications.
        /// Keys: "scrutiny", "discipline".
        /// Values: delta to apply.
        /// </summary>
        public Dictionary<string, int> Escalation { get; set; } = [];

        /// <summary>
        /// Medical risk delta (escalation track for illness/injury risk).
        /// Positive values increase medical risk from spoiled food, disease exposure, etc.
        /// </summary>
        public int? MedicalRisk { get; set; }

        /// <summary>
        /// Denar reward or penalty for this outcome. Null if no gold change.
        /// </summary>
        public int? Denars { get; set; }

        /// <summary>
        /// Renown reward or penalty for this outcome. Null if no renown change.
        /// </summary>
        public int? Renown { get; set; }

        /// <summary>
        /// HP loss for player character (injuries from dangerous orders). Null if no HP loss.
        /// DEPRECATED: Use InjuryType instead for narrative-driven injuries with varied severity.
        /// </summary>
        public int? HpLoss { get; set; }
        
        /// <summary>
        /// Injury type inflicted on player (e.g., "sprained_ankle", "broken_rib", "head_wound").
        /// When set, applies percentage-based HP loss and narrative from injury definitions.
        /// Overrides HpLoss if both are specified.
        /// </summary>
        public string InjuryType { get; set; }

        /// <summary>
        /// Minimum troop casualties for critical failures. Used with TroopLossMax to determine random troop loss.
        /// </summary>
        public int? TroopLossMin { get; set; }

        /// <summary>
        /// Maximum troop casualties for critical failures. Used with TroopLossMin to determine random troop loss.
        /// </summary>
        public int? TroopLossMax { get; set; }

        /// <summary>
        /// Narrative text describing this outcome.
        /// Shown to player in UI after order completion.
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }
}

