using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Qualities
{
    /// <summary>Authoring record — loaded from quality_defs.json at startup, never saved.</summary>
    public sealed class QualityDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;   // localization key
        public int Min { get; set; }
        public int Max { get; set; }
        public int Default { get; set; }
        public QualityScope Scope { get; set; } = QualityScope.Global;
        public QualityKind Kind { get; set; } = QualityKind.Stored;
        public DecayRule Decay { get; set; }                       // null = no decay
        public bool Writable { get; set; } = true;                 // read-only read-throughs (rank, days_in_rank) set false

        // Populated by QualityStore.RegisterAll at boot for ReadThrough entries; not serialized.
        public Func<Hero, int> Reader { get; set; }
        public Action<Hero, int, string> Writer { get; set; }      // (subject, delta, reason) — delta, not absolute
    }

    /// <summary>Decay specification. RequireIdle = only decay while LastChanged is older than IntervalDays.</summary>
    public sealed class DecayRule
    {
        public int Amount { get; set; } = -1;       // typically negative
        public int IntervalDays { get; set; } = 5;
        public bool RequireIdle { get; set; } = true;
        public int Floor { get; set; }              // clamps against this after decay; typically Min
    }
}
