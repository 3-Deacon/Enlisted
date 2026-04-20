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

            // Scripted effect? Expand and recurse.
            var scripted = ScriptedEffectRegistry.Resolve(eff.Apply);
            if (scripted != null)
            {
                Apply(scripted, ctx);
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
                    ctx.ResolvedSlots.TryGetValue(slot, out hero);
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

            PartyBase.MainParty?.ItemRoster?.AddToCounts(item, count);
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
