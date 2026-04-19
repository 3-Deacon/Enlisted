using System;

namespace Enlisted.Features.Interface.News.Models
{
    /// <summary>
    /// Coarse “bands” keep narration stable: we prefer stable descriptors over dumping raw numbers into UI prose.
    /// All enums here are save-safe (ints) and do not store any engine objects.
    /// </summary>
    public enum ThreatBand
    {
        Unknown = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum FoodBand
    {
        Unknown = 0,
        Plenty = 1,
        Thin = 2,
        Low = 3,
        Critical = 4
    }

    public enum HealthDeltaBand
    {
        Unknown = 0,
        None = 1,
        Minor = 2,
        Moderate = 3,
        Major = 4
    }

    public enum TrainingTag
    {
        Unknown = 0,
        Routine = 1,
        Drilling = 2,
        Inspection = 3,
        Sparring = 4
    }

    /// <summary>
    /// The factual “inputs” for one in-game day.
    /// This is not player-facing prose; Phase 2 converts this into a Daily Report using templates.
    /// </summary>
    public sealed class DailyReportSnapshot
    {
        /// <summary>
        /// Integer day number (recommended source at runtime: (int)CampaignTime.Now.ToDays).
        /// </summary>
        public int DayNumber { get; set; } = -1;

        /// <summary>
        /// Optional: lord party identifier for dedupe/debugging (string only; do not store MobileParty/Hero).
        /// </summary>
        public string LordPartyId { get; set; } = string.Empty;

        // Unit deltas (day-over-day casualties and replacements)
        public int WoundedDelta { get; set; }
        public int SickDelta { get; set; }
        public int DeadDelta { get; set; }
        public int ReplacementsDelta { get; set; }
        public HealthDeltaBand HealthDeltaBand { get; set; } = HealthDeltaBand.Unknown;

        // Company bands (best-effort; populated by producers in Phase 4)
        public ThreatBand Threat { get; set; } = ThreatBand.Unknown;
        public FoodBand Food { get; set; } = FoodBand.Unknown;

        // Optional tags (kept as strings for authoring flexibility; keep them stable + low-cardinality).
        public string ObjectiveTag { get; set; } = string.Empty; // e.g. "Traveling", "Besieging"
        public string LastStopTag { get; set; } = string.Empty;  // e.g. "resupply", "recruit"
        public string BattleTag { get; set; } = string.Empty;    // e.g. "imminent", "aftermath"
        public string AttachedArmyTag { get; set; } = string.Empty; // e.g. "attached_to_army"
        public string DisciplineTag { get; set; } = string.Empty; // Legacy name: now tracks Scrutiny (0-100). Values: "clean", "minor", "troubled", "serious", "breaking"
        public int DisciplineIssues { get; set; } = -1; // Legacy name: now tracks Scrutiny (0-100). -1 when unknown.

        public string StrategicContextTag { get; set; } = string.Empty; // e.g. "coordinated_offensive", "winter_camp"

        // Optional: stabilization helpers used by template selection.
        public TrainingTag TrainingTag { get; set; } = TrainingTag.Unknown;

        public void Normalize()
        {
            // Keep strings non-null (Bannerlord IDataStore sync requires non-null refs).
            LordPartyId ??= string.Empty;
            ObjectiveTag ??= string.Empty;
            LastStopTag ??= string.Empty;
            BattleTag ??= string.Empty;
            AttachedArmyTag ??= string.Empty;
            DisciplineTag ??= string.Empty;

            // Clamp obviously-invalid values to reduce accidental save corruption.
            DayNumber = Math.Max(-1, DayNumber);
        }
    }
}


