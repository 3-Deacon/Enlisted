using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// One former lord the player has served. Snapshot fields freeze relation +
    /// rank + faction at the moment of discharge so the entry remains stable even
    /// after the lord's clan, faction, or relation drift afterwards. Per-favor
    /// cooldowns are stored as CSV (FavorKind=ticks pairs) and rebuilt to a
    /// runtime dictionary by PatronRoll on access — HashSet/Dictionary&lt;enum,
    /// CampaignTime&gt; isn't directly saveable.
    /// </summary>
    [Serializable]
    public sealed class PatronEntry
    {
        public MBGUID HeroId { get; set; }
        public int DaysServed { get; set; }
        public int MaxRankReached { get; set; }
        public CampaignTime DischargedOn { get; set; } = CampaignTime.Zero;
        public CampaignTime LastFavorRequestedOn { get; set; } = CampaignTime.Zero;
        public string PerFavorCooldownsCsv { get; set; } = string.Empty;
        public int RelationSnapshotOnDischarge { get; set; }
        public bool IsDead { get; set; }
        public string FactionAtDischarge { get; set; } = string.Empty;
    }
}
