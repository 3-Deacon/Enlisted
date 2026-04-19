using Enlisted.Features.Content.Models;

namespace Enlisted.Features.Camp.Models
{
    /// <summary>
    /// Outcome quality levels for routine activities. Better outcomes yield more rewards,
    /// while mishaps can cause injuries or losses.
    /// </summary>
    public enum OutcomeType
    {
        /// <summary>Exceptional performance with bonus rewards.</summary>
        Excellent,
        /// <summary>Above average results.</summary>
        Good,
        /// <summary>Standard expected outcome.</summary>
        Normal,
        /// <summary>Below average, reduced gains.</summary>
        Poor,
        /// <summary>Something went wrong, potential injury or loss.</summary>
        Mishap
    }

    /// <summary>
    /// Represents the result of processing a routine activity during a day phase.
    /// Contains all outcome data including XP, resource changes, conditions, and display text.
    /// </summary>
    public class RoutineOutcome
    {
        /// <summary>The day phase when this outcome occurred.</summary>
        public DayPhase Phase { get; set; }

        /// <summary>The activity category that was processed (training, foraging, patrol, etc.).</summary>
        public string ActivityCategory { get; set; }

        /// <summary>Human-readable name of the activity.</summary>
        public string ActivityName { get; set; }

        /// <summary>Quality of the outcome, affects rewards and consequences.</summary>
        public OutcomeType Outcome { get; set; }

        /// <summary>XP gained from this activity. Can be 0 for mishaps.</summary>
        public int XpGained { get; set; }

        /// <summary>Skill that receives the XP (e.g., "OneHanded", "Athletics", "Scouting").</summary>
        public string SkillAffected { get; set; }

        /// <summary>Gold or supplies found/earned. Can be negative for losses.</summary>
        public int GoldChange { get; set; }

    /// <summary>Supply change from foraging or losses.</summary>
    public int SupplyChange { get; set; }

    /// <summary>Condition ID to apply (e.g., "minor_injury", "illness"). Null if none.</summary>
        public string ConditionApplied { get; set; }

        /// <summary>Short flavor text for combat log display.</summary>
        public string FlavorText { get; set; }

        /// <summary>Longer summary text for news feed display.</summary>
        public string NewsText { get; set; }

        /// <summary>True if this was an orchestrator override rather than baseline schedule.</summary>
        public bool WasOverride { get; set; }

        /// <summary>Reason for override if applicable (e.g., "Supplies critical").</summary>
        public string OverrideReason { get; set; }

        /// <summary>True if outcome is positive (Excellent, Good, or Normal without mishap).</summary>
        public bool IsPositive => Outcome == OutcomeType.Excellent || 
                                   Outcome == OutcomeType.Good || 
                                   Outcome == OutcomeType.Normal;

        /// <summary>True if outcome resulted in injury or negative condition.</summary>
        public bool HasCondition => !string.IsNullOrEmpty(ConditionApplied);

        /// <summary>
        /// Creates a formatted string for the combat log with XP and other gains.
        /// </summary>
        public string GetCombatLogText()
        {
            var result = FlavorText ?? "Activity completed.";

            // Append gains/losses
            var gains = new System.Collections.Generic.List<string>();
            
            if (XpGained > 0 && !string.IsNullOrEmpty(SkillAffected))
            {
                gains.Add($"+{XpGained} {SkillAffected} XP");
            }
            if (GoldChange > 0)
            {
                gains.Add($"+{GoldChange} denars");
            }
            else if (GoldChange < 0)
            {
                gains.Add($"{GoldChange} denars");
            }
        if (SupplyChange > 0)
        {
            gains.Add($"+{SupplyChange} supplies");
        }

        if (gains.Count > 0)
            {
                result += " (" + string.Join(", ", gains) + ")";
            }

            return result;
        }

        /// <summary>
        /// Creates a short summary for news feed entries.
        /// Uses flavor text when available for immersive narrative.
        /// </summary>
        public string GetNewsSummary()
        {
            if (!string.IsNullOrEmpty(NewsText))
            {
                return NewsText;
            }

            // Use flavor text directly if available - it's already descriptive and immersive
            // No need to prefix with activity name as that creates redundancy
            if (!string.IsNullOrEmpty(FlavorText))
            {
                return FlavorText;
            }

            // Fallback to generic outcome text with activity name
            return Outcome switch
            {
                OutcomeType.Excellent => $"{ActivityName}: Exceptional performance",
                OutcomeType.Good => $"{ActivityName}: Good progress",
                OutcomeType.Normal => $"{ActivityName}: Completed",
                OutcomeType.Poor => $"{ActivityName}: Struggled today",
                OutcomeType.Mishap => $"{ActivityName}: Incident occurred",
                _ => $"{ActivityName}: Done"
            };
        }
    }
}
