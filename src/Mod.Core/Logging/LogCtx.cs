using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Mod.Core.Logging
{
    /// <summary>
    /// Helpers for building the <c>ctx</c> dictionary passed to
    /// <see cref="ModLogger.Surfaced"/> / <see cref="ModLogger.Caught"/>.
    /// Keep each bundle to 3-6 values that materially help diagnosis.
    /// </summary>
    public static class LogCtx
    {
        /// <summary>Build an arbitrary ctx from alternating key/value args.</summary>
        public static IDictionary<string, object> Of(params object[] kv)
        {
            var d = new Dictionary<string, object>(kv.Length / 2);
            for (int i = 0; i + 1 < kv.Length; i += 2)
            {
                var key = kv[i]?.ToString() ?? "?";
                d[key] = kv[i + 1];
            }
            return d;
        }

        /// <summary>Canonical player-state bundle for enlistment-context errors.</summary>
        public static IDictionary<string, object> PlayerState()
        {
            var hero = Hero.MainHero;
            var d = new Dictionary<string, object>(6);
            if (hero == null) { d["MainHero"] = "null"; return d; }
            d["Gold"] = hero.Gold;
            d["Party"] = hero.PartyBelongedTo?.Name?.ToString() ?? "null";
            d["InSettlement"] = hero.CurrentSettlement?.Name?.ToString() ?? "null";
            d["IsAlive"] = hero.IsAlive;
            return d;
        }
    }
}
