using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Resolves named loot-pool ids from ModuleData/Enlisted/Loot/loot_pools.json
    /// and grants one random ItemObject to the main party's roster. faction_troop_tree
    /// pools walk CultureTroopTreeHelper's BFS output filtered by the enlisted lord's
    /// culture and player tier; global_catalog pools query MBObjectManager directly.
    /// "categories" entries in the JSON are ItemObject.ItemTypeEnum member names
    /// (HeadArmor, Polearm, Goods, ...), not ItemCategory StringIds.
    /// </summary>
    public static class LootPoolResolver
    {
        private static readonly Dictionary<string, PoolDef> _pools =
            new Dictionary<string, PoolDef>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded) { return; }
            try
            {
                var dir = ModulePaths.GetContentPath("Loot");
                if (string.IsNullOrEmpty(dir))
                {
                    ModLogger.Expected("LOOT", "no_loot_dir", "loot content directory path empty");
                    return;
                }
                var file = Path.Combine(dir, "loot_pools.json");
                if (!File.Exists(file))
                {
                    ModLogger.Expected("LOOT", "no_loot_pools_file", "loot_pools.json missing", LogCtx.Of("dir", dir));
                    return;
                }
                var root = JObject.Parse(File.ReadAllText(file));
                var pools = root["pools"] as JArray;
                if (pools == null) { return; }
                foreach (var poolObj in pools.OfType<JObject>())
                {
                    try
                    {
                        var def = ParsePool(poolObj);
                        if (def != null && !string.IsNullOrEmpty(def.Id))
                        {
                            _pools[def.Id] = def;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("LOOT", "bad_loot_pool_parse", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LOOT", "EnsureLoaded threw", ex);
            }
            finally
            {
                _loaded = true;
            }
        }

        public static void ResolveAndGrant(string poolId)
        {
            try
            {
                EnsureLoaded();
                if (string.IsNullOrEmpty(poolId)) { return; }
                if (!_pools.TryGetValue(poolId, out var def))
                {
                    ModLogger.Expected("LOOT", "pool_not_found", "pool id not in loot_pools.json", LogCtx.Of("pool_id", poolId));
                    return;
                }
                var item = PickItem(def);
                if (item == null)
                {
                    ModLogger.Expected("LOOT", "pool_no_match", "pool resolved to zero items", LogCtx.Of("pool_id", poolId));
                    return;
                }
                _ = (PartyBase.MainParty?.ItemRoster?.AddToCounts(item, 1));
                ModLogger.Info("LOOT", $"granted {item.StringId} from pool {poolId}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("LOOT", "ResolveAndGrant threw", ex);
            }
        }

        private static ItemObject PickItem(PoolDef def)
        {
            var candidates = new List<ItemObject>();

            if (string.Equals(def.Source, "faction_troop_tree", StringComparison.OrdinalIgnoreCase))
            {
                var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (lord?.Culture == null) { return null; }
                var maxTier = ResolveMaxTier(def);
                var tree = CultureTroopTreeHelper.BuildCultureTroopTree(lord.Culture);
                foreach (var troop in tree)
                {
                    if (troop == null || troop.Tier > maxTier) { continue; }
                    if (troop.BattleEquipments == null) { continue; }
                    foreach (var equip in troop.BattleEquipments)
                    {
                        if (equip == null) { continue; }
                        for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                        {
                            var element = equip[(EquipmentIndex)i];
                            var item = element.Item;
                            if (item == null) { continue; }
                            if (!def.TypeSet.Contains(item.ItemType)) { continue; }
                            if (!candidates.Contains(item)) { candidates.Add(item); }
                        }
                    }
                }
            }
            else
            {
                foreach (var item in MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
                {
                    if (item == null) { continue; }
                    var tier = (int)item.Tier;
                    if (tier < def.TierMin || tier > def.TierMax) { continue; }
                    if (!def.TypeSet.Contains(item.ItemType)) { continue; }
                    candidates.Add(item);
                }
            }

            return candidates.Count == 0 ? null : candidates[MBRandom.RandomInt(candidates.Count)];
        }

        private static int ResolveMaxTier(PoolDef def)
        {
            if (string.Equals(def.MaxTroopTier, "player_tier", StringComparison.OrdinalIgnoreCase))
            {
                return EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
            }
            return int.TryParse(def.MaxTroopTier, out var t) ? t : 9;
        }

        private static PoolDef ParsePool(JObject obj)
        {
            if (obj == null) { return null; }
            var def = new PoolDef
            {
                Id = (string)obj["id"] ?? string.Empty,
                Source = (string)obj["source"] ?? string.Empty,
            };
            var filter = obj["filter"] as JObject;
            if (filter != null)
            {
                var cats = filter["categories"] as JArray;
                if (cats != null)
                {
                    foreach (var cat in cats)
                    {
                        var name = (string)cat;
                        if (!string.IsNullOrEmpty(name) &&
                            Enum.TryParse<ItemObject.ItemTypeEnum>(name, ignoreCase: true, out var parsed))
                        {
                            def.TypeSet.Add(parsed);
                        }
                    }
                }
                def.MaxTroopTier = (string)filter["max_troop_tier"] ?? string.Empty;
                var range = filter["tier_range"] as JArray;
                if (range != null && range.Count == 2)
                {
                    def.TierMin = (int?)range[0] ?? 0;
                    def.TierMax = (int?)range[1] ?? 9;
                }
                else
                {
                    def.TierMin = 0;
                    def.TierMax = 9;
                }
            }
            return def;
        }

        private sealed class PoolDef
        {
            public string Id { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public HashSet<ItemObject.ItemTypeEnum> TypeSet { get; } = new HashSet<ItemObject.ItemTypeEnum>();
            public string MaxTroopTier { get; set; } = string.Empty;
            public int TierMin { get; set; }
            public int TierMax { get; set; } = 9;
        }
    }
}
