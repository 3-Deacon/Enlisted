using System;
using System.Collections.Generic;
using System.IO;
using Enlisted.Features.Ceremonies;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Officer
{
    /// <summary>
    /// Assigns the appropriate cape (a vanilla shoulder-armor <see cref="ItemObject"/>) to the
    /// player's cape slot at tier-band transitions. v1 ships using existing vanilla items by
    /// StringId, mapped per culture variant via <c>cape_progression.json</c>. Plan 7 polish
    /// may swap in per-culture authored items if desired.
    /// </summary>
    public static class CapeProgression
    {
        private static Dictionary<string, Dictionary<string, string>> _variants;

        public static void EnsureLoaded()
        {
            if (_variants != null)
            {
                return;
            }

            try
            {
                var dir = ModulePaths.GetContentPath("Officer");
                var path = Path.Combine(dir, "cape_progression.json");
                if (!File.Exists(path))
                {
                    ModLogger.Expected(
                        "OFFICER",
                        "cape_config_missing",
                        "cape_progression.json not found at expected path");
                    _variants = new Dictionary<string, Dictionary<string, string>>();
                    return;
                }

                var json = File.ReadAllText(path);
                var config = JsonConvert.DeserializeObject<CapeConfigDto>(json);
                _variants = config?.Variants ?? new Dictionary<string, Dictionary<string, string>>();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "cape_config_load_failed", ex);
                _variants = new Dictionary<string, Dictionary<string, string>>();
            }
        }

        public static void ApplyAtTierChange(int newTier)
        {
            if (newTier < 1 || newTier > 9)
            {
                return;
            }

            EnsureLoaded();
            if (_variants == null || _variants.Count == 0)
            {
                return;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            var variantSuffix = CeremonyCultureSelector.SelectVariantSuffix();
            var band = TierBand(newTier);

            var capeId = ResolveCapeId(variantSuffix, band);
            if (string.IsNullOrEmpty(capeId))
            {
                return;
            }

            var cape = MBObjectManager.Instance?.GetObject<ItemObject>(capeId);
            if (cape == null)
            {
                ModLogger.Expected(
                    "OFFICER",
                    "cape_lookup_failed",
                    "vanilla cape ItemObject not found",
                    LogCtx.Of("StringId", capeId, "Variant", variantSuffix, "Band", band));
                return;
            }

            try
            {
                hero.BattleEquipment[EquipmentIndex.Cape] = new EquipmentElement(cape, null);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "cape_apply_failed", ex);
            }
        }

        private static string ResolveCapeId(string variantSuffix, string band)
        {
            if (_variants.TryGetValue(variantSuffix, out var variantMap)
                && variantMap.TryGetValue(band, out var v))
            {
                return v;
            }

            if (_variants.TryGetValue("base", out var baseMap)
                && baseMap.TryGetValue(band, out var b))
            {
                return b;
            }

            return null;
        }

        private static string TierBand(int tier)
        {
            if (tier <= 3)
            {
                return "recruit";
            }

            if (tier <= 6)
            {
                return "nco";
            }

            return "officer";
        }

        private sealed class CapeConfigDto
        {
            [JsonProperty("variants")]
            public Dictionary<string, Dictionary<string, string>> Variants { get; set; }
        }
    }
}
