using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Helpers;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Builds and fires the modal for an endeavor's current phase. Modeled on
    /// HomeEveningMenuProvider — looks up the storylet, populates the player /
    /// rank / lord / companion text variables, and routes through
    /// ModalEventBuilder.FireEndeavorPhase. EndeavorRunner calls FirePhase when
    /// a phase is due and FireResolution when all phases have resolved.
    /// </summary>
    public static class EndeavorPhaseProvider
    {
        public static void FirePhase(EndeavorActivity activity)
        {
            if (activity == null)
            {
                return;
            }
            var template = EndeavorCatalog.GetById(activity.EndeavorId);
            if (template == null)
            {
                ModLogger.Expected("ENDEAVOR", "phase_template_missing",
                    "EndeavorActivity references an unknown template id");
                return;
            }
            var phaseIndex = activity.GetPhaseIndex();
            if (phaseIndex < 0 || phaseIndex >= template.PhasePool.Count)
            {
                ModLogger.Expected("ENDEAVOR", "phase_index_out_of_range",
                    "Endeavor phase index outside template phase pool");
                return;
            }

            var storyletId = template.PhasePool[phaseIndex];
            FireStorylet(activity, template, storyletId, $"phase_{phaseIndex + 1}");
        }

        public static void FireResolution(EndeavorActivity activity)
        {
            if (activity == null)
            {
                return;
            }
            var template = EndeavorCatalog.GetById(activity.EndeavorId);
            if (template == null)
            {
                ModLogger.Expected("ENDEAVOR", "resolution_template_missing",
                    "EndeavorActivity references an unknown template id at resolution");
                return;
            }
            var score = activity.GetScore();
            var storyletId = PickResolutionStoryletId(template, score);
            if (string.IsNullOrEmpty(storyletId))
            {
                ModLogger.Expected("ENDEAVOR", "no_resolution_storylet",
                    "Endeavor template has no resolution storylet for the resolved bucket");
                return;
            }
            FireStorylet(activity, template, storyletId, "resolution");
        }

        public static string PickResolutionStoryletId(EndeavorTemplate template, int score)
        {
            if (template == null)
            {
                return null;
            }
            if (score >= template.ResolutionSuccessMinScore && !string.IsNullOrEmpty(template.ResolutionSuccessId))
            {
                return template.ResolutionSuccessId;
            }
            if (score <= template.ResolutionFailureMaxScore && !string.IsNullOrEmpty(template.ResolutionFailureId))
            {
                return template.ResolutionFailureId;
            }
            if (!string.IsNullOrEmpty(template.ResolutionPartialId))
            {
                return template.ResolutionPartialId;
            }
            // No partial authored — fall through by score sign
            return score >= 0
                ? template.ResolutionSuccessId
                : template.ResolutionFailureId;
        }

        private static void FireStorylet(EndeavorActivity activity, EndeavorTemplate template, string storyletId, string phaseLabel)
        {
            var primaryCompanion = activity.GetAssignedCompanions().FirstOrDefault();
            SetTokens(primaryCompanion, template);

            var ctx = new StoryletContext
            {
                ActivityTypeId = activity.TypeId,
                PhaseId = phaseLabel,
                CurrentContext = DeriveContext(),
            };
            // Resolve assigned companion slots so storylets that reference
            // {agent_<archetype>} or apply relation_change against companion slots
            // resolve cleanly. Up to two slots per template (Plan 5 §4.5).
            var companions = activity.GetAssignedCompanions();
            for (var i = 0; i < activity.AssignedCompanionIds.Count && i < template.CompanionSlots.Count; i++)
            {
                var slot = template.CompanionSlots[i];
                if (i < companions.Count && companions[i] != null && !string.IsNullOrEmpty(slot.Archetype))
                {
                    var slotName = "agent_" + slot.Archetype;
                    ctx.ResolvedSlots[slotName] = companions[i];
                }
            }

            ModalEventBuilder.FireEndeavorPhase(storyletId, ctx, activity);
        }

        private static void SetTokens(Hero primaryCompanion, EndeavorTemplate template)
        {
            var hero = Hero.MainHero;
            var enlistment = EnlistmentBehavior.Instance;

            var playerName = hero?.Name?.ToString() ?? "Soldier";
            MBTextManager.SetTextVariable("PLAYER_NAME", playerName);

            var rankTitle = enlistment != null
                ? Enlisted.Features.Ranks.RankHelper.GetCurrentRank(enlistment)
                : "Soldier";
            MBTextManager.SetTextVariable("PLAYER_RANK", rankTitle);

            var lordName = enlistment?.EnlistedLord?.Name?.ToString() ?? "the lord";
            MBTextManager.SetTextVariable("LORD_NAME", lordName);

            var tier = enlistment?.EnlistmentTier ?? 1;
            MBTextManager.SetTextVariable("PLAYER_TIER", tier.ToString());

            var companionName = primaryCompanion?.Name?.ToString() ?? string.Empty;
            MBTextManager.SetTextVariable("COMPANION_NAME", companionName);

            var companionFirstName = primaryCompanion?.FirstName?.ToString() ?? companionName;
            MBTextManager.SetTextVariable("COMPANION_FIRST_NAME", companionFirstName);

            var endeavorTitle = template?.Title ?? string.Empty;
            MBTextManager.SetTextVariable("ENDEAVOR_TITLE", endeavorTitle);
        }

        private static string DeriveContext()
        {
            var p = MobileParty.MainParty;
            if (p == null)
            {
                return "any";
            }
            if (p.BesiegerCamp != null || p.CurrentSettlement?.SiegeEvent != null)
            {
                return "siege";
            }
            if (p.CurrentSettlement != null)
            {
                return "settlement";
            }
            if (p.IsCurrentlyAtSea)
            {
                return "sea";
            }
            return "land";
        }
    }
}
