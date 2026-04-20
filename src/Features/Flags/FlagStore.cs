using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Flags
{
    /// <summary>
    /// Replaces EscalationState.ActiveFlags, informal identity tags, and arc progress markers.
    /// Global + hero-scoped named booleans with optional expiry (CampaignTime.Never = permanent).
    /// Hero-scoped flags purge when the hero is no longer in the game on load.
    /// </summary>
    [Serializable]
    public sealed class FlagStore
    {
        public static FlagStore Instance { get; private set; }

        public Dictionary<string, CampaignTime> GlobalFlags { get; set; } =
            new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<MBGUID, Dictionary<string, CampaignTime>> HeroFlags { get; set; } =
            new Dictionary<MBGUID, Dictionary<string, CampaignTime>>();

        internal static void SetInstance(FlagStore instance) => Instance = instance;

        /// <summary>
        /// Re-seats null dictionary fields with empty instances. Save data produced before
        /// either field existed deserializes the container with those properties left as null —
        /// field initializers don't run on deserialization paths that skip the ctor. Call this
        /// right after SyncData and again on load before any read or purge.
        /// </summary>
        public void EnsureInitialized()
        {
            if (GlobalFlags == null)
            {
                GlobalFlags = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
            }
            if (HeroFlags == null)
            {
                HeroFlags = new Dictionary<MBGUID, Dictionary<string, CampaignTime>>();
            }
        }

        public bool Has(string flag)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return false;
            }
            if (!GlobalFlags.TryGetValue(flag, out var expiry))
            {
                return false;
            }
            return expiry == CampaignTime.Never || expiry.IsFuture;
        }

        public bool HasForHero(Hero hero, string flag)
        {
            if (hero == null || string.IsNullOrEmpty(flag))
            {
                return false;
            }
            if (!HeroFlags.TryGetValue(hero.Id, out var map))
            {
                return false;
            }
            if (!map.TryGetValue(flag, out var expiry))
            {
                return false;
            }
            return expiry == CampaignTime.Never || expiry.IsFuture;
        }

        public void Set(string flag, CampaignTime expiry)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return;
            }
            GlobalFlags[flag] = expiry;
        }

        public void SetForHero(Hero hero, string flag, CampaignTime expiry)
        {
            if (hero == null || string.IsNullOrEmpty(flag))
            {
                return;
            }
            if (!HeroFlags.TryGetValue(hero.Id, out var map))
            {
                map = new Dictionary<string, CampaignTime>(StringComparer.OrdinalIgnoreCase);
                HeroFlags[hero.Id] = map;
            }
            map[flag] = expiry;
        }

        public void Clear(string flag)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return;
            }
            _ = GlobalFlags.Remove(flag);
        }

        public void ClearForHero(Hero hero, string flag)
        {
            if (hero == null || string.IsNullOrEmpty(flag))
            {
                return;
            }
            if (HeroFlags.TryGetValue(hero.Id, out var map))
            {
                _ = map.Remove(flag);
            }
        }

        public int DaysSinceSet(string flag)
        {
            if (string.IsNullOrEmpty(flag))
            {
                return int.MaxValue;
            }
            if (!GlobalFlags.TryGetValue(flag, out _))
            {
                return int.MaxValue;
            }
            // Expiry isn't the same as set-time. Callers that need days-since must set a
            // companion flag with a known expiry when the event happens; DaysSinceSet is a
            // convenience only for flags whose expiry == CampaignTime.Never (permanent; returns 0).
            return 0;
        }

        /// <summary>Sweep expired entries. Called by FlagBehavior on hourly tick.</summary>
        public void PurgeExpired(CampaignTime now)
        {
            _ = now;
            PurgeExpiredIn(GlobalFlags);
            foreach (var kv in HeroFlags)
            {
                PurgeExpiredIn(kv.Value);
            }
        }

        private static void PurgeExpiredIn(Dictionary<string, CampaignTime> map)
        {
            List<string> toRemove = null;
            foreach (var kv in map)
            {
                if (kv.Value != CampaignTime.Never && !kv.Value.IsFuture)
                {
                    (toRemove ??= new List<string>()).Add(kv.Key);
                }
            }
            if (toRemove == null)
            {
                return;
            }
            foreach (var k in toRemove)
            {
                _ = map.Remove(k);
            }
        }

        /// <summary>On load, drop hero-scoped flags whose hero is no longer known to the game.</summary>
        public void PurgeMissingHeroes()
        {
            List<MBGUID> toRemove = null;
            foreach (var id in HeroFlags.Keys)
            {
                if (MBObjectManager.Instance.GetObject(id) == null)
                {
                    (toRemove ??= new List<MBGUID>()).Add(id);
                }
            }
            if (toRemove == null)
            {
                return;
            }
            foreach (var id in toRemove)
            {
                _ = HeroFlags.Remove(id);
            }
        }
    }
}
