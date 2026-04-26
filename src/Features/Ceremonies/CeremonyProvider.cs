using System;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Features.Ranks;
using Enlisted.Mod.Core.Helpers;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace Enlisted.Features.Ceremonies
{
    /// <summary>
    /// Centralizes ceremony modal firing for the five retained tier transitions
    /// (newTier 2/3/5/7/8). Tiers 4/6/9 are intentionally skipped — they collide
    /// with PathCrossroadsBehavior modals (architecture brief §6, Plan 3 Lock 1).
    /// Resolves cultural variant via CeremonyCultureSelector, populates the
    /// canonical text-variable bag (PLAYER_NAME / PLAYER_RANK / LORD_NAME /
    /// PLAYER_TIER) so storylet text interpolates correctly even on the very
    /// first ceremony (before QM or companion conversation has fired), seeds
    /// witness slots into StoryletContext.ResolvedSlots so scripted
    /// relation_change effects can address each witness via target_slot, then
    /// delegates to ModalEventBuilder.FireCeremony.
    /// </summary>
    public static class CeremonyProvider
    {
        private static readonly int[] CeremonyTiers = { 2, 3, 5, 7, 8 };

        public static void FireCeremonyForTier(int newTier)
        {
            if (Array.IndexOf(CeremonyTiers, newTier) < 0)
            {
                ModLogger.Expected("CEREMONY", "tier_not_authored",
                    "No ceremony at this tier (PathCrossroads collision or out of range)",
                    LogCtx.Of("newTier", newTier));
                return;
            }

            var dedupKey = "ceremony_fired_t" + newTier;
            if (FlagStore.Instance != null && FlagStore.Instance.Has(dedupKey))
            {
                ModLogger.Expected("CEREMONY", "already_fired",
                    "Ceremony for this tier already fired",
                    LogCtx.Of("newTier", newTier));
                return;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                ModLogger.Expected("CEREMONY", "not_enlisted",
                    "Ceremony fire blocked — not enlisted",
                    LogCtx.Of("newTier", newTier));
                return;
            }

            var storyletId = CeremonyCultureSelector.ResolveStoryletId(newTier);
            if (string.IsNullOrEmpty(storyletId))
            {
                ModLogger.Expected("CEREMONY", "no_storylet_id",
                    "Cultural variant resolver returned empty",
                    LogCtx.Of("newTier", newTier));
                return;
            }

            try
            {
                SetCommonTextVariables(enlistment);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CEREMONY", "set_text_variables_failed", ex);
            }

            var ctx = BuildCeremonyContext(enlistment, newTier);
            ModalEventBuilder.FireCeremony(storyletId, ctx);
        }

        /// <summary>
        /// Mirrors EnlistedDialogManager.SetCommonDialogueVariables so a
        /// ceremony fired before the QM or companion flow has populated the
        /// bag still has correct interpolation. Idempotent — re-setting the
        /// same variables is harmless.
        /// </summary>
        private static void SetCommonTextVariables(EnlistmentBehavior enlistment)
        {
            var hero = Hero.MainHero;
            var playerName = hero?.Name?.ToString() ?? "Soldier";
            MBTextManager.SetTextVariable("PLAYER_NAME", playerName);

            var rankTitle = enlistment != null
                ? RankHelper.GetCurrentRank(enlistment)
                : "Soldier";
            MBTextManager.SetTextVariable("PLAYER_RANK", rankTitle);

            var lordName = enlistment?.EnlistedLord?.Name?.ToString() ?? "the Lord";
            MBTextManager.SetTextVariable("LORD_NAME", lordName);

            MBTextManager.SetTextVariable("PLAYER_TIER",
                (enlistment?.EnlistmentTier ?? 1).ToString());
        }

        /// <summary>
        /// Pre-populates ResolvedSlots with witness_&lt;archetype&gt; entries so
        /// option effects of the form { apply: relation_change, target_slot:
        /// witness_sergeant, delta: ... } resolve correctly when EffectExecutor
        /// drains pending effects after the player picks. StoryletEventAdapter.BuildModal
        /// does not call Storylet.IsEligible (which would overwrite ResolvedSlots
        /// via SlotFiller.Resolve), so pre-populated slots survive the modal
        /// pipeline. If a future edit introduces an IsEligible call between
        /// BuildModal and DrainPendingEffects, this assumption regresses.
        /// </summary>
        private static StoryletContext BuildCeremonyContext(EnlistmentBehavior enlistment, int newTier)
        {
            var ctx = new StoryletContext
            {
                ActivityTypeId = "ceremony",
                PhaseId = "ceremony_t" + newTier,
                CurrentContext = "any",
                EvaluatedAt = CampaignTime.Now,
                SubjectHero = enlistment?.EnlistedLord
            };

            var witnesses = CeremonyWitnessSelector.GetWitnessesForCeremony(newTier);
            foreach (var kv in witnesses)
            {
                ctx.ResolvedSlots[kv.Key] = kv.Value;
            }

            return ctx;
        }
    }
}
