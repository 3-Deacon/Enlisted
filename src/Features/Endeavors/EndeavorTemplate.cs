using System.Collections.Generic;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Player-issued multi-phase endeavor template, parsed from
    /// ModuleData/Enlisted/Endeavors/endeavor_catalog.json. Schema reference at
    /// docs/Features/Endeavors/endeavor-catalog-schema.md. POCO loaded fresh per
    /// session — not persisted in saves; runtime instances live on
    /// EndeavorActivity (offset 57).
    /// </summary>
    public sealed class EndeavorTemplate
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string TitleId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string DescriptionId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public List<int> PhaseOffsetsHours { get; set; } = new List<int>();
        public List<string> PhasePool { get; set; } = new List<string>();
        public string ResolutionSuccessId { get; set; } = string.Empty;
        public string ResolutionPartialId { get; set; } = string.Empty;
        public string ResolutionFailureId { get; set; } = string.Empty;
        public int ResolutionSuccessMinScore { get; set; } = 2;
        public int ResolutionFailureMaxScore { get; set; } = -2;
        public List<string> SkillAxis { get; set; } = new List<string>();
        public int SelfGateSkillThreshold { get; set; }
        public List<EndeavorCompanionSlot> CompanionSlots { get; set; } = new List<EndeavorCompanionSlot>();
        public float ScrutinyRiskPerPhase { get; set; }
        public int TierMin { get; set; } = 1;
        public List<string> FlagGates { get; set; } = new List<string>();
        public List<string> FlagBlockers { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// Companion slot definition inside an endeavor template. Up to two slots per
    /// template (Plan 5 §4.5). When the player picks the endeavor, the assign
    /// inquiry presents currently-spawned companions matching the archetype.
    /// </summary>
    public sealed class EndeavorCompanionSlot
    {
        public string Archetype { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Required { get; set; }
        public string SkillBonusAxis { get; set; } = string.Empty;
        public int SkillBonusAmount { get; set; }
    }
}
