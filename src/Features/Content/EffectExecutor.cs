using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Flags;
using Enlisted.Features.Qualities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Dispatch for primitive effects. Scripted effects resolve to a list of primitives via
    /// ScriptedEffectRegistry before arriving here. Unknown primitives log once and no-op.
    /// </summary>
    public static class EffectExecutor
    {
        // Guard against malformed scripted-effect catalogs that reference each other
        // cyclically (A -> B -> A) or nest unreasonably deep. A well-authored catalog
        // never nears this depth; hitting it implies a JSON mistake worth surfacing.
        private const int MaxScriptedDepth = 8;
        private static int _scriptedDepth;

        public static void Apply(IList<EffectDecl> effects, StoryletContext ctx)
        {
            if (effects == null || effects.Count == 0)
            {
                return;
            }

            foreach (var eff in effects)
            {
                ApplyOne(eff, ctx);
            }
        }

        public static void ApplyOne(EffectDecl eff, StoryletContext ctx)
        {
            if (eff == null || string.IsNullOrEmpty(eff.Apply))
            {
                return;
            }

            // Scripted effect? Expand and recurse, with a depth cap so a cyclic
            // catalog can't stack-overflow the campaign thread.
            var scripted = ScriptedEffectRegistry.Resolve(eff.Apply);
            if (scripted != null)
            {
                if (_scriptedDepth >= MaxScriptedDepth)
                {
                    ModLogger.Expected("EFFECT", "scripted_depth_limit",
                        "Scripted-effect recursion cap hit — check for cyclic refs",
                        new Dictionary<string, object> { { "apply", eff.Apply } });
                    return;
                }
                _scriptedDepth++;
                try
                {
                    Apply(scripted, ctx);
                }
                finally
                {
                    _scriptedDepth--;
                }
                return;
            }

            switch (eff.Apply)
            {
                case "quality_add":
                    DoQualityAdd(eff, ctx, perHero: false);
                    break;
                case "quality_add_for_hero":
                    DoQualityAdd(eff, ctx, perHero: true);
                    break;
                case "quality_set":
                    DoQualitySet(eff);
                    break;
                case "set_flag":
                    DoSetFlag(eff);
                    break;
                case "clear_flag":
                    DoClearFlag(eff);
                    break;
                case "set_flag_on_hero":
                    DoSetFlagOnHero(eff, ctx);
                    break;
                case "trait_xp":
                    DoTraitXp(eff);
                    break;
                case "skill_xp":
                    DoSkillXp(eff);
                    break;
                case "give_gold":
                    DoGiveGold(eff);
                    break;
                case "give_item":
                    DoGiveItem(eff);
                    break;
                case "grant_attribute_level":
                    DoGrantAttributeLevel(eff);
                    break;
                case "grant_focus_point":
                    DoGrantFocusPoint(eff);
                    break;
                case "grant_renown":
                    DoGrantRenown(eff);
                    break;
                case "grant_skill_level":
                    DoGrantSkillLevel(eff);
                    break;
                case "grant_unspent_attribute":
                    DoGrantUnspentAttribute(eff);
                    break;
                case "grant_unspent_focus":
                    DoGrantUnspentFocus(eff);
                    break;
                case "relation_change":
                    DoRelationChange(eff, ctx);
                    break;
                case "set_trait_level":
                    DoSetTraitLevel(eff);
                    break;
                case "grant_item":
                    DoGrantItem(eff);
                    break;
                case "grant_random_item_from_pool":
                    DoGrantRandomItemFromPool(eff);
                    break;
                case "start_arc":
                    DoStartArc(eff, ctx);
                    break;
                case "clear_active_named_order":
                    DoClearActiveNamedOrder(eff);
                    break;
                default:
                    ModLogger.Expected("EFFECT", "unknown_primitive_" + eff.Apply, "Unknown effect primitive: " + eff.Apply);
                    break;
            }
        }

        private static void DoQualityAdd(EffectDecl eff, StoryletContext ctx, bool perHero)
        {
            var quality = GetStr(eff, "quality");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(quality) || amount == 0)
            {
                return;
            }

            if (perHero)
            {
                var slot = GetStr(eff, "slot");
                Hero hero = null;
                if (!string.IsNullOrEmpty(slot) && ctx?.ResolvedSlots != null)
                {
                    _ = ctx.ResolvedSlots.TryGetValue(slot, out hero);
                }

                QualityStore.Instance?.AddForHero(hero, quality, amount, "storylet");
            }
            else
            {
                QualityStore.Instance?.Add(quality, amount, "storylet");
            }
        }

        private static void DoQualitySet(EffectDecl eff)
        {
            var quality = GetStr(eff, "quality");
            var value = GetInt(eff, "value");
            if (string.IsNullOrEmpty(quality))
            {
                return;
            }

            QualityStore.Instance?.Set(quality, value, "storylet");
        }

        private static void DoSetFlag(EffectDecl eff)
        {
            var name = GetStr(eff, "name");
            var days = GetInt(eff, "days");
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var expiry = days > 0 ? CampaignTime.DaysFromNow(days) : CampaignTime.Never;
            FlagStore.Instance?.Set(name, expiry);
        }

        private static void DoClearFlag(EffectDecl eff)
        {
            var name = GetStr(eff, "name");
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            FlagStore.Instance?.Clear(name);
        }

        private static void DoSetFlagOnHero(EffectDecl eff, StoryletContext ctx)
        {
            var slot = GetStr(eff, "slot");
            var name = GetStr(eff, "name");
            var days = GetInt(eff, "days");
            if (string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(name))
            {
                return;
            }

            if (ctx?.ResolvedSlots == null)
            {
                return;
            }

            if (!ctx.ResolvedSlots.TryGetValue(slot, out var hero) || hero == null)
            {
                return;
            }

            var expiry = days > 0 ? CampaignTime.DaysFromNow(days) : CampaignTime.Never;
            FlagStore.Instance?.SetForHero(hero, name, expiry);
        }

        private static void DoTraitXp(EffectDecl eff)
        {
            var trait = GetStr(eff, "trait");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(trait) || amount == 0)
            {
                return;
            }

            // Resolve by StringId via MBObjectManager — DefaultTraits has no .All enumerable.
            var traitObj = MBObjectManager.Instance.GetObject<TraitObject>(trait);
            if (traitObj == null)
            {
                ModLogger.Expected("EFFECT", "unknown_trait_" + trait, "Unknown trait id: " + trait);
                return;
            }

            if (Hero.MainHero == null)
            {
                return;
            }

            // SetTraitLevel is the public API (SetTraitLevelInternal does not exist in v1.3.13).
            // Step by Math.Sign so a single effect nudges the trait by at most one level.
            var current = Hero.MainHero.GetTraitLevel(traitObj);
            Hero.MainHero.SetTraitLevel(traitObj, current + Math.Sign(amount));
        }

        private static void DoSkillXp(EffectDecl eff)
        {
            var skill = GetStr(eff, "skill");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(skill) || amount <= 0)
            {
                return;
            }

            // Skills.All from TaleWorlds.CampaignSystem.Extensions provides the live registry.
            var skillObj = Skills.All.FirstOrDefault(
                s => string.Equals(s.StringId, skill, StringComparison.OrdinalIgnoreCase));
            if (skillObj == null)
            {
                ModLogger.Expected("EFFECT", "unknown_skill_" + skill, "Unknown skill id: " + skill);
                return;
            }

            Hero.MainHero?.AddSkillXp(skillObj, amount);
        }

        private static void DoGiveGold(EffectDecl eff)
        {
            var amount = GetInt(eff, "amount");
            if (amount == 0 || Hero.MainHero == null)
            {
                return;
            }

            // Positive = grant; negative = charge. Route through GiveGoldAction (AGENTS.md §3).
            if (amount >= 0)
            {
                GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount);
            }
            else
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, -amount);
            }
        }

        private static void DoGiveItem(EffectDecl eff)
        {
            var itemId = GetStr(eff, "item_id");
            var count = Math.Max(1, GetInt(eff, "count"));
            if (string.IsNullOrEmpty(itemId))
            {
                return;
            }

            var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (item == null)
            {
                ModLogger.Expected("EFFECT", "unknown_item_" + itemId, "Unknown item id: " + itemId);
                return;
            }

            _ = (PartyBase.MainParty?.ItemRoster?.AddToCounts(item, count));
        }

        private static void DoGrantSkillLevel(EffectDecl eff)
        {
            var skillId = GetStr(eff, "skill");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(skillId) || amount == 0)
            {
                return;
            }

            var skill = MBObjectManager.Instance.GetObject<SkillObject>(skillId);
            if (skill == null)
            {
                ModLogger.Expected("EFFECT", "grant_skill_level_skill_not_found", $"grant_skill_level: skill='{skillId}' not in catalog");
                return;
            }

            try
            {
                Hero.MainHero?.HeroDeveloper.ChangeSkillLevel(skill, amount);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "grant_skill_level threw", ex);
            }
        }

        private static void DoGrantFocusPoint(EffectDecl eff)
        {
            var skillId = GetStr(eff, "skill");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(skillId) || amount == 0)
            {
                return;
            }

            var skill = MBObjectManager.Instance.GetObject<SkillObject>(skillId);
            if (skill == null)
            {
                ModLogger.Expected("EFFECT", "grant_focus_point_skill_not_found", $"grant_focus_point: skill='{skillId}' not in catalog");
                return;
            }

            try
            {
                // checkUnspentFocusPoints: false — this is a reward grant, not spending from the pool.
                Hero.MainHero?.HeroDeveloper.AddFocus(skill, amount, checkUnspentFocusPoints: false);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "grant_focus_point threw", ex);
            }
        }

        private static void DoGrantUnspentFocus(EffectDecl eff)
        {
            var amount = GetInt(eff, "amount");
            if (amount == 0)
            {
                return;
            }

            try
            {
                if (Hero.MainHero != null)
                {
                    Hero.MainHero.HeroDeveloper.UnspentFocusPoints += amount;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "grant_unspent_focus threw", ex);
            }
        }

        private static void DoGrantAttributeLevel(EffectDecl eff)
        {
            var attrId = GetStr(eff, "attribute");
            var amount = GetInt(eff, "amount");
            if (string.IsNullOrEmpty(attrId) || amount == 0)
            {
                return;
            }

            var attr = MBObjectManager.Instance.GetObject<CharacterAttribute>(attrId);
            if (attr == null)
            {
                ModLogger.Expected("EFFECT", "attribute_not_found", $"grant_attribute_level: attribute='{attrId}' not in catalog");
                return;
            }

            try
            {
                // checkUnspentPoints: false — this is a reward grant, not spending from the pool.
                Hero.MainHero?.HeroDeveloper.AddAttribute(attr, amount, checkUnspentPoints: false);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "grant_attribute_level threw", ex);
            }
        }

        private static void DoGrantUnspentAttribute(EffectDecl eff)
        {
            var amount = GetInt(eff, "amount");
            if (amount == 0)
            {
                return;
            }

            try
            {
                if (Hero.MainHero != null)
                {
                    Hero.MainHero.HeroDeveloper.UnspentAttributePoints += amount;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "grant_unspent_attribute threw", ex);
            }
        }

        private static void DoSetTraitLevel(EffectDecl eff)
        {
            var traitId = GetStr(eff, "trait");
            var level = GetInt(eff, "level");
            if (string.IsNullOrEmpty(traitId))
            {
                return;
            }

            var trait = MBObjectManager.Instance.GetObject<TraitObject>(traitId);
            if (trait == null)
            {
                ModLogger.Expected("EFFECT", "trait_not_found", $"set_trait_level: trait='{traitId}' not in catalog");
                return;
            }

            try
            {
                Hero.MainHero?.SetTraitLevel(trait, level);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "set_trait_level threw", ex);
            }
        }

        private static void DoGrantRenown(EffectDecl eff)
        {
            var amount = GetInt(eff, "amount");
            if (amount == 0)
            {
                return;
            }

            try
            {
                if (Hero.MainHero != null)
                {
                    GainRenownAction.Apply(Hero.MainHero, (float)amount);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "grant_renown threw", ex);
            }
        }

        private static void DoRelationChange(EffectDecl eff, StoryletContext ctx)
        {
            var slotName = GetStr(eff, "target_slot");
            var delta = GetInt(eff, "delta");
            if (string.IsNullOrEmpty(slotName) || delta == 0)
            {
                return;
            }

            if (ctx == null || ctx.ResolvedSlots == null || !ctx.ResolvedSlots.TryGetValue(slotName, out var hero) || hero == null)
            {
                ModLogger.Expected("EFFECT", "relation_target_unresolved", $"relation_change: slot='{slotName}' not resolved on storylet");
                return;
            }

            try
            {
                ChangeRelationAction.ApplyPlayerRelation(hero, delta);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "relation_change threw", ex);
            }
        }

        private static void DoGrantItem(EffectDecl eff)
        {
            var itemId = GetStr(eff, "item_id");
            var countRaw = GetInt(eff, "count");
            var count = countRaw > 0 ? countRaw : 1;
            if (string.IsNullOrEmpty(itemId))
            {
                return;
            }

            var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (item == null)
            {
                ModLogger.Expected("EFFECT", "grant_item_not_found", $"grant_item: item_id='{itemId}' not in catalog");
                return;
            }

            try
            {
                _ = (PartyBase.MainParty?.ItemRoster?.AddToCounts(item, count));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "grant_item threw", ex);
            }
        }

        private static void DoGrantRandomItemFromPool(EffectDecl eff)
        {
            var poolId = GetStr(eff, "pool_id");
            if (string.IsNullOrEmpty(poolId))
            {
                ModLogger.Expected("EFFECT", "grant_random_item_pool_id_missing", "grant_random_item_from_pool: pool_id required");
                return;
            }

            // Task 26: wire to LootPoolResolver.ResolveAndGrant(poolId, ctx) when authored content lands.
            ModLogger.Expected("EFFECT", "grant_random_item_pre_phaseB", $"grant_random_item_from_pool stub: pool='{poolId}'");
        }

        private static void DoStartArc(EffectDecl eff, StoryletContext ctx)
        {
            try
            {
                var sourceStorylet = ctx?.SourceStorylet;
                if (sourceStorylet == null)
                {
                    ModLogger.Expected("EFFECT", "start_arc_no_storylet", "start_arc with no SourceStorylet on context");
                    return;
                }
                var intent = GetStr(eff, "intent");
                Enlisted.Features.Activities.Orders.NamedOrderArcRuntime.SpliceArc(sourceStorylet, intent);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "start_arc threw", ex);
            }
        }

        private static void DoClearActiveNamedOrder(EffectDecl eff)
        {
            try
            {
                Enlisted.Features.Activities.Orders.NamedOrderArcRuntime.UnspliceArc();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EFFECT", "clear_active_named_order threw", ex);
            }
        }

        private static string GetStr(EffectDecl eff, string key)
        {
            return eff?.Params != null && eff.Params.TryGetValue(key, out var v) && v != null
                ? v.ToString()
                : string.Empty;
        }

        private static int GetInt(EffectDecl eff, string key)
        {
            if (eff?.Params == null || !eff.Params.TryGetValue(key, out var v) || v == null)
            {
                return 0;
            }

            return v switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                float f => (int)f,
                string s when int.TryParse(s, out var parsed) => parsed,
                _ => 0
            };
        }
    }
}
