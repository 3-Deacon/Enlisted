using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// One former lord the player has served. Snapshot fields freeze relation +
    /// rank + faction at the moment of discharge so the entry remains stable even
    /// after the lord's clan, faction, or relation drift afterwards. Per-favor
    /// cooldowns are stored as a CSV string ("kindId:hours;kindId:hours") on
    /// PerFavorCooldownsCsv — Dictionary&lt;FavorKind, CampaignTime&gt; isn't a
    /// saveable container shape (CLAUDE.md known issue #14). Cooldown values are
    /// hours-since-epoch (CampaignTime.ToHours), giving daily-granularity
    /// resolution that's more than sufficient for 30-180 day favor cooldowns and
    /// avoids depending on the private NumTicks accessor.
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

        /// <summary>
        /// Returns the cooldown expiry for a favor kind, or null if no cooldown
        /// has been recorded. Empty / malformed CSV segments are skipped silently.
        /// </summary>
        public CampaignTime? GetCooldown(FavorKind kind)
        {
            if (string.IsNullOrEmpty(PerFavorCooldownsCsv))
            {
                return null;
            }
            var parts = PerFavorCooldownsCsv.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var segment = parts[i];
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }
                var kv = segment.Split(':');
                if (kv.Length != 2)
                {
                    continue;
                }
                if (int.TryParse(kv[0], out var k) && (FavorKind)k == kind &&
                    long.TryParse(kv[1], out var hours))
                {
                    return CampaignTime.Hours(hours);
                }
            }
            return null;
        }

        /// <summary>
        /// Stamps a cooldown expiry for a favor kind. Replaces any existing entry
        /// for that kind; existing entries for other kinds are preserved.
        /// </summary>
        public void SetCooldown(FavorKind kind, CampaignTime expiry)
        {
            var preserved = new List<string>();
            var raw = PerFavorCooldownsCsv ?? string.Empty;
            var parts = raw.Split(';');
            var prefix = $"{(int)kind}:";
            for (int i = 0; i < parts.Length; i++)
            {
                var segment = parts[i];
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }
                if (segment.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }
                preserved.Add(segment);
            }
            preserved.Add($"{prefix}{(long)expiry.ToHours}");
            PerFavorCooldownsCsv = string.Join(";", preserved);
        }

        /// <summary>
        /// True when the recorded cooldown for this favor has already passed
        /// (or no cooldown has been recorded yet).
        /// </summary>
        public bool IsFavorAvailable(FavorKind kind, CampaignTime now)
        {
            var cooldown = GetCooldown(kind);
            return !cooldown.HasValue || cooldown.Value <= now;
        }
    }
}
