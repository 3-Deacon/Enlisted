using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enlisted.Features.Companions;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Retinue.Core;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Registers the enlisted_endeavors sub-menu opened from Camp menu slot 5
    /// (EnlistedMenuBehavior). Owns the Browse-available + Cancel-active +
    /// Back options, the assign-companions inquiry shown when the player picks
    /// an endeavor with companion slots, and the start/cancel routing into
    /// EndeavorRunner. Status text is rebuilt in OnEndeavorsMenuInit and
    /// surfaced via the {ENDEAVORS_STATUS_TEXT} menu variable.
    /// </summary>
    public sealed class EndeavorsMenuHandler : CampaignBehaviorBase
    {
        private const string MenuId = "enlisted_endeavors";
        private const string CampHubMenuId = "enlisted_camp_hub";

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                AddEndeavorsMenu(starter);
                ModLogger.Info("ENDEAVOR", "Endeavors sub-menu registered");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENDEAVOR", "Failed to register endeavors menu", ex);
            }
        }

        private void AddEndeavorsMenu(CampaignGameStarter starter)
        {
            starter.AddGameMenu(MenuId,
                "{ENDEAVORS_STATUS_TEXT}",
                OnEndeavorsMenuInit,
                GameMenu.MenuOverlayType.None,
                GameMenu.MenuFlags.None);

            starter.AddGameMenuOption(MenuId, "endeavors_browse",
                "{=enlisted_endeavors_browse}Browse available endeavors",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    var available = GetAvailableEndeavorsForPlayer();
                    if (EndeavorActivity.Instance != null)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=enlisted_endeavors_browse_blocked_active}You cannot start a new endeavor while one is active.");
                        return true;
                    }
                    if (available.Count == 0)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=enlisted_endeavors_browse_none}No endeavors are currently available. Try a different category, recruit a matching companion, or wait for cooldowns to elapse.");
                        return true;
                    }
                    args.Tooltip = new TextObject("{=enlisted_endeavors_browse_tooltip}Select an endeavor to begin.");
                    return true;
                },
                _ => OnBrowseSelected(),
                false, 1);

            starter.AddGameMenuOption(MenuId, "endeavors_cancel",
                "{=enlisted_endeavors_cancel}Abandon current endeavor",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Mission;
                    if (EndeavorActivity.Instance == null)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=enlisted_endeavors_cancel_none}No endeavor is currently active.");
                        return false;
                    }
                    args.Tooltip = new TextObject("{=enlisted_endeavors_cancel_tooltip}Forfeit progress and end the current endeavor.");
                    return true;
                },
                _ => OnCancelSelected(),
                false, 2);

            starter.AddGameMenuOption(MenuId, "endeavors_back",
                "{=enlisted_endeavors_back}Back",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => GameMenu.SwitchToMenu(CampHubMenuId),
                false, 100);
        }

        private void OnEndeavorsMenuInit(MenuCallbackArgs args)
        {
            try
            {
                var statusText = BuildStatusText();
                MBTextManager.SetTextVariable("ENDEAVORS_STATUS_TEXT", statusText);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ENDEAVOR", "Failed to build endeavors status text", ex);
                MBTextManager.SetTextVariable("ENDEAVORS_STATUS_TEXT",
                    new TextObject("{=enlisted_endeavors_status_error}Endeavor status unavailable.").ToString());
            }
        }

        private string BuildStatusText()
        {
            var sb = new StringBuilder();
            var active = EndeavorActivity.Instance;
            if (active != null)
            {
                var template = EndeavorCatalog.GetById(active.EndeavorId);
                var title = template?.Title ?? active.EndeavorId;
                var phaseIndex = active.GetPhaseIndex();
                var totalPhases = active.GetTotalPhases();
                _ = sb.Append(new TextObject("{=enlisted_endeavors_status_active_prefix}Active endeavor: ").ToString());
                _ = sb.Append(title);
                _ = sb.Append("  (");
                _ = sb.Append(new TextObject("{=enlisted_endeavors_status_phase}phase ").ToString());
                _ = sb.Append(Math.Min(phaseIndex + 1, Math.Max(1, totalPhases)));
                _ = sb.Append(" / ");
                _ = sb.Append(Math.Max(1, totalPhases));
                _ = sb.Append(")\n\n");
            }
            else
            {
                _ = sb.Append(new TextObject("{=enlisted_endeavors_status_idle}No endeavor active.").ToString());
                _ = sb.Append("\n\n");
            }

            var runner = EndeavorRunner.Instance;
            var spawned = runner?.GetSpawnedCompanions() ?? new List<Hero>();
            var player = Hero.MainHero;
            var totalAvailable = 0;
            var lockedReasons = new Dictionary<EndeavorGatingResolver.LockReason, int>();
            foreach (var template in EndeavorCatalog.All)
            {
                var resolution = EndeavorGatingResolver.Resolve(template, player, spawned);
                if (resolution.IsAvailable)
                {
                    totalAvailable++;
                }
                else
                {
                    if (!lockedReasons.TryGetValue(resolution.Reason, out var n))
                    {
                        n = 0;
                    }
                    lockedReasons[resolution.Reason] = n + 1;
                }
            }
            _ = sb.Append(new TextObject("{=enlisted_endeavors_status_available}Endeavors available: ").ToString());
            _ = sb.Append(totalAvailable);
            _ = sb.Append(" / ");
            _ = sb.Append(EndeavorCatalog.Count);

            return sb.ToString();
        }

        private void OnBrowseSelected()
        {
            try
            {
                var available = GetAvailableEndeavorsForPlayer();
                if (available.Count == 0)
                {
                    return;
                }

                var elements = new List<InquiryElement>();
                foreach (var template in available)
                {
                    var label = $"[{Capitalize(template.Category)}] {template.Title}";
                    elements.Add(new InquiryElement(template, label, null, true, template.Description ?? string.Empty));
                }

                var data = new MultiSelectionInquiryData(
                    titleText: new TextObject("{=enlisted_endeavors_browse_title}Choose an endeavor").ToString(),
                    descriptionText: new TextObject("{=enlisted_endeavors_browse_desc}Select one endeavor to begin. You will then assign any companion agents.").ToString(),
                    inquiryElements: elements,
                    isExitShown: true,
                    minSelectableOptionCount: 1,
                    maxSelectableOptionCount: 1,
                    affirmativeText: new TextObject("{=enlisted_endeavors_browse_select}Select").ToString(),
                    negativeText: new TextObject("{=enlisted_endeavors_browse_back}Back").ToString(),
                    affirmativeAction: selected =>
                    {
                        if (selected?.FirstOrDefault()?.Identifier is EndeavorTemplate t)
                        {
                            BeginAssignmentForTemplate(t);
                        }
                    },
                    negativeAction: null);
                MBInformationManager.ShowMultiSelectionInquiry(data, true);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENDEAVOR", "Endeavor browse threw", ex);
            }
        }

        private void BeginAssignmentForTemplate(EndeavorTemplate template)
        {
            try
            {
                var slots = template.CompanionSlots ?? new List<EndeavorCompanionSlot>();
                if (slots.Count == 0)
                {
                    StartEndeavor(template, new List<Hero>());
                    return;
                }

                var spawned = EndeavorRunner.Instance?.GetSpawnedCompanions() ?? new List<Hero>();
                var assignment = CompanionAssignmentManager.Instance;
                var enlistment = EnlistmentBehavior.Instance;
                var elements = new List<InquiryElement>();
                foreach (var hero in spawned)
                {
                    if (hero == null || !hero.IsAlive)
                    {
                        continue;
                    }
                    if (assignment != null && assignment.IsAssignedToEndeavor(hero))
                    {
                        continue;
                    }
                    var typeId = enlistment?.GetCompanionTypeId(hero) ?? string.Empty;
                    if (!slots.Any(s => string.Equals(s.Archetype, typeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    var portrait = new CharacterImageIdentifier(CharacterCode.CreateFrom(hero.CharacterObject));
                    var label = $"{hero.Name} — {typeId.Replace('_', ' ')}";
                    elements.Add(new InquiryElement(hero, label, portrait, true, string.Empty));
                }

                var hasRequired = slots.Any(s => s.Required);
                if (elements.Count == 0)
                {
                    if (hasRequired)
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=enlisted_endeavors_no_companion_required}This endeavor requires a matching companion who isn't currently with you.").ToString()));
                        return;
                    }
                    // No matching companions but companions are optional — start solo.
                    StartEndeavor(template, new List<Hero>());
                    return;
                }

                var maxSlots = Math.Min(slots.Count, elements.Count);
                var data = new MultiSelectionInquiryData(
                    titleText: new TextObject("{=enlisted_endeavors_assign_title}Assign companions").ToString(),
                    descriptionText: new TextObject("{=enlisted_endeavors_assign_desc}Select companions to join this endeavor. They cannot join other endeavors until this one resolves.").ToString(),
                    inquiryElements: elements,
                    isExitShown: true,
                    minSelectableOptionCount: hasRequired ? 1 : 0,
                    maxSelectableOptionCount: maxSlots,
                    affirmativeText: new TextObject("{=enlisted_endeavors_assign_confirm}Begin").ToString(),
                    negativeText: new TextObject("{=enlisted_endeavors_assign_back}Back").ToString(),
                    affirmativeAction: selected =>
                    {
                        var picked = (selected ?? new List<InquiryElement>())
                            .Select(e => e.Identifier as Hero)
                            .Where(h => h != null)
                            .ToList();
                        StartEndeavor(template, picked);
                    },
                    negativeAction: null);
                MBInformationManager.ShowMultiSelectionInquiry(data, true);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENDEAVOR", "Endeavor companion assignment threw", ex);
            }
        }

        private static void StartEndeavor(EndeavorTemplate template, IList<Hero> assigned)
        {
            var runner = EndeavorRunner.Instance;
            if (runner == null)
            {
                ModLogger.Surfaced("ENDEAVOR", "EndeavorRunner null at start", null);
                return;
            }
            if (runner.StartEndeavor(template, assigned))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=enlisted_endeavors_started}Endeavor begun: ").ToString() + (template.Title ?? template.Id)));
                GameMenu.SwitchToMenu(CampHubMenuId);
            }
        }

        private void OnCancelSelected()
        {
            try
            {
                var active = EndeavorActivity.Instance;
                if (active == null)
                {
                    return;
                }
                var template = EndeavorCatalog.GetById(active.EndeavorId);
                var title = template?.Title ?? active.EndeavorId;
                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=enlisted_endeavors_cancel_confirm_title}Abandon endeavor?").ToString(),
                    new TextObject("{=enlisted_endeavors_cancel_confirm_desc}You will forfeit all progress on '" + title + "'. Companions assigned to this endeavor will be released.").ToString(),
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: true,
                    affirmativeText: new TextObject("{=enlisted_endeavors_cancel_yes}Abandon").ToString(),
                    negativeText: new TextObject("{=enlisted_endeavors_cancel_no}Keep").ToString(),
                    affirmativeAction: () =>
                    {
                        EndeavorRunner.Instance?.CancelEndeavor("player_cancelled_from_menu");
                        GameMenu.SwitchToMenu(MenuId);
                    },
                    negativeAction: null));
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("ENDEAVOR", "Endeavor cancel threw", ex);
            }
        }

        private static List<EndeavorTemplate> GetAvailableEndeavorsForPlayer()
        {
            var result = new List<EndeavorTemplate>();
            var runner = EndeavorRunner.Instance;
            var spawned = runner?.GetSpawnedCompanions() ?? new List<Hero>();
            var player = Hero.MainHero;
            foreach (var template in EndeavorCatalog.All)
            {
                if (runner != null && !runner.CanStartCategory(template.Category))
                {
                    continue;
                }
                if (!EndeavorGatingResolver.IsAvailable(template, player, spawned))
                {
                    continue;
                }
                result.Add(template);
            }
            // Stable sort by category then title for deterministic UI.
            result.Sort((a, b) =>
            {
                var c = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase);
                if (c != 0) { return c; }
                return string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
