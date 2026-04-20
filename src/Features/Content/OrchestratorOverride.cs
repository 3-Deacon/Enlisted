using Enlisted.Features.Content.Models;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Type of schedule override injected by the orchestrator.
    /// </summary>
    public enum OverrideType
    {
        /// <summary>No override active, use normal schedule.</summary>
        None,
        /// <summary>Override triggered by critical company need (low supplies, exhaustion, etc.).</summary>
        NeedBased,
        /// <summary>Periodic variety injection to break up routine monotony.</summary>
        VarietyInjection
    }

    /// <summary>
    /// Represents an orchestrator-injected activity that overrides the normal camp schedule.
    /// The orchestrator uses these to respond to company needs (foraging when supplies low)
    /// or inject variety (patrol duty, scouting) to prevent monotony.
    /// </summary>
    public class OrchestratorOverride
    {
        /// <summary>Type of override (need-based or variety injection).</summary>
        public OverrideType Type { get; set; }

        /// <summary>Activity category being injected (foraging, patrol, extended_rest, etc.).</summary>
        public string ActivityCategory { get; set; }

        /// <summary>Human-readable name of the override activity.</summary>
        public string ActivityName { get; set; }

        /// <summary>Description shown in schedule and combat log.</summary>
        public string Description { get; set; }

        /// <summary>Reason for the override (e.g., "Supplies critical", "Variety assignment").</summary>
        public string Reason { get; set; }

        /// <summary>Priority for conflict resolution. Higher values take precedence.</summary>
        public int Priority { get; set; }

        /// <summary>Day phases this override applies to. Empty means all phases.</summary>
        public DayPhase[] AffectedPhases { get; set; }

        /// <summary>Skill that gains XP from this activity.</summary>
        public string SkillAffected { get; set; }

        /// <summary>Base XP range minimum for this activity.</summary>
        public int BaseXpMin { get; set; }

        /// <summary>Base XP range maximum for this activity.</summary>
        public int BaseXpMax { get; set; }

        /// <summary>True if this override replaces both activity slots.</summary>
        public bool ReplaceBothSlots { get; set; }

        /// <summary>Flavor text for the combat log when this override activates.</summary>
        public string ActivationText { get; set; }

        /// <summary>ID of the need this override addresses (for tracking recovery).</summary>
        public string AddressesNeed { get; set; }

        /// <summary>
        /// Creates a need-based override for a critical company condition.
        /// </summary>
        public static OrchestratorOverride CreateNeedBased(
            string category,
            string name,
            string reason,
            string addressesNeed,
            DayPhase[] phases = null)
        {
            return new OrchestratorOverride
            {
                Type = OverrideType.NeedBased,
                ActivityCategory = category,
                ActivityName = name,
                Reason = reason,
                AddressesNeed = addressesNeed,
                AffectedPhases = phases ?? new DayPhase[0],
                Priority = 100, // Need-based overrides have high priority
                ReplaceBothSlots = true
            };
        }

        /// <summary>
        /// Creates a variety injection to break up routine.
        /// </summary>
        public static OrchestratorOverride CreateVariety(
            string category,
            string name,
            string description,
            DayPhase preferredPhase)
        {
            return new OrchestratorOverride
            {
                Type = OverrideType.VarietyInjection,
                ActivityCategory = category,
                ActivityName = name,
                Description = description,
                Reason = "Variety assignment",
                AffectedPhases = new[] { preferredPhase },
                Priority = 50, // Variety has lower priority than needs
                ReplaceBothSlots = false
            };
        }

        /// <summary>
        /// Checks if this override applies to a given phase.
        /// </summary>
        public bool AppliesToPhase(DayPhase phase)
        {
            if (AffectedPhases == null || AffectedPhases.Length == 0)
            {
                return true; // Applies to all phases
            }

            foreach (var p in AffectedPhases)
            {
                if (p == phase)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
