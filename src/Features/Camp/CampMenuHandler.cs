using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Retinue.Core;
using Enlisted.Features.Retinue.Systems;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using EnlistedConfig = Enlisted.Mod.Core.Config.ConfigurationManager;

namespace Enlisted.Features.Camp
{
    /// <summary>
    /// Handles the Camp menu system for service records display.
    /// Provides menus for viewing current posting, faction history, and lifetime statistics.
    /// Integrates with the existing enlisted status menu by adding a "Camp" option.
    /// </summary>
    public sealed class CampMenuHandler : CampaignBehaviorBase
    {
        private const string LogCategory = "CAMP";

        // Menu IDs
        // NOTE: "Command Tent" is no longer a concept in the UI.
        // The player-facing hub is "Camp" and the hub menu id is `enlisted_camp_hub`.
        // This class only registers submenus that the Camp hub can route to.
        private const string CampHubMenuId = "enlisted_camp_hub";
        private const string ServiceRecordsMenuId = "enlisted_service_records";
        private const string CurrentPostingMenuId = "enlisted_current_posting";
        private const string FactionRecordsMenuId = "enlisted_faction_records";
        private const string FactionDetailMenuId = "enlisted_faction_detail";
        private const string LifetimeSummaryMenuId = "enlisted_lifetime_summary";

        // Retinue Menu IDs (kept for now; still routed from Camp)
        private const string RetinueMenuId = "enlisted_retinue";
        private const string RetinuePurchaseMenuId = "enlisted_retinue_purchase";
        private const string RetinueDismissMenuId = "enlisted_retinue_dismiss";
        private const string RetinueRequisitionMenuId = "enlisted_retinue_requisition";

        #region Wait Menu Handlers (enables spacebar time control like Quartermaster menus)

        /// <summary>
        /// Wait condition - always returns true since we control exit via menu options.
        /// </summary>
        private static bool CampWaitCondition(MenuCallbackArgs args) => true;

        /// <summary>
        /// Wait consequence - empty since we handle exit via menu options.
        /// </summary>
        private static void CampWaitConsequence(MenuCallbackArgs args)
        {
            // No consequence needed - we never let progress reach 100%
        }

        /// <summary>
        /// Wait tick handler for Camp menus.
        /// NOTE: Time mode restoration is handled ONCE during menu init, not here.
        /// Previously this tick handler would restore CapturedTimeMode whenever it saw
        /// UnstoppableFastForward, but this fought with user input - when the user clicked
        /// fast forward, the next tick would immediately restore it. This caused x3 speed to pause.
        /// </summary>
        private static void CampWaitTick(MenuCallbackArgs args, CampaignTime dt)
        {
            // Intentionally empty - time mode is handled in menu init, not per-tick
            // The old code here fought with user speed input and caused pausing issues
        }

        /// <summary>
        /// Switch to a menu while preserving the current time control mode.
        /// Uses the shared CapturedTimeMode from QuartermasterManager.
        /// </summary>
        private static void SwitchToMenuPreserveTime(string menuId)
        {
            // Preserve the player's time mode from the moment they first entered Camp.
            // Do not overwrite it on subsequent hops between submenus so time never gets paused/resumed unexpectedly.
            var capturedMode = QuartermasterManager.CapturedTimeMode
                               ?? Campaign.Current?.TimeControlMode
                               ?? CampaignTimeControlMode.Stop;

            QuartermasterManager.CapturedTimeMode ??= capturedMode;

            GameMenu.SwitchToMenu(menuId);

            if (Campaign.Current != null)
            {
                Campaign.Current.TimeControlMode = capturedMode;
            }
        }

        #endregion

        // Companion Assignment Menu IDs
        private const string CompanionAssignmentsMenuId = "enlisted_companions";

        // PayTension Action Menu IDs
        private const string DesperateMeasuresMenuId = "enlisted_desperate";
        private const string HelpTheLordMenuId = "enlisted_help_lord";

        // Track selected faction for detail view
        private string _selectedFactionKey;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent state needed for menu handler
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                // Set up inline icons for use in menu text (Bannerlord's rich text system)
                SetupInlineIcons();

                AddCampMenus(starter);
                ModLogger.Info(LogCategory, "Camp menus registered successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Failed to register camp menus", ex);
            }
        }

        /// <summary>
        /// Sets up inline icon variables for use in menu text.
        /// Uses Bannerlord's native img tag system for inline sprites.
        /// </summary>
        private static void SetupInlineIcons()
        {
            // Gold/denar icon - displays coin sprite inline with text
            MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");

            // Influence icon - displays influence sprite inline
            MBTextManager.SetTextVariable("INFLUENCE_ICON", "{=!}<img src=\"General\\Icons\\Influence@2x\" extend=\"7\">");
        }

        /// <summary>
        /// Registers Camp submenus with the game.
        /// The Camp hub (`enlisted_camp_hub`) is defined in `EnlistedMenuBehavior`.
        /// </summary>
        private void AddCampMenus(CampaignGameStarter starter)
        {
            // Service Records submenu
            AddServiceRecordsMenu(starter);

            // Current Posting display
            AddCurrentPostingMenu(starter);

            // Faction Records list
            AddFactionRecordsMenu(starter);

            // Faction Detail view
            AddFactionDetailMenu(starter);

            // Lifetime Summary display
            AddLifetimeSummaryMenu(starter);

            // Retinue menus
            AddRetinueMenu(starter);
            AddRetinuePurchaseMenu(starter);
            AddRetinueDismissMenu(starter);

            // Requisition menu
            AddRetinueRequisitionMenu(starter);

            // Companion Assignments menu
            AddCompanionAssignmentsMenu(starter);

            // PayTension Action Menus
            AddDesperateMeasuresMenu(starter);
            AddHelpTheLordMenu(starter);
        }
        #region Menu Background and Audio

        /// <summary>
        /// Menu background and audio initialization for Camp menus.
        /// Sets military-themed background and ambient audio for immersion.
        /// Resumes time so it continues passing while browsing menus.
        /// </summary>
        [GameMenuInitializationHandler(CampHubMenuId)]
        [GameMenuInitializationHandler(ServiceRecordsMenuId)]
        [GameMenuInitializationHandler(CurrentPostingMenuId)]
        [GameMenuInitializationHandler(FactionRecordsMenuId)]
        [GameMenuInitializationHandler(FactionDetailMenuId)]
        [GameMenuInitializationHandler(LifetimeSummaryMenuId)]
        [GameMenuInitializationHandler(RetinueMenuId)]
        [GameMenuInitializationHandler(RetinuePurchaseMenuId)]
        [GameMenuInitializationHandler(RetinueDismissMenuId)]
        [GameMenuInitializationHandler(RetinueRequisitionMenuId)]
        [GameMenuInitializationHandler(CompanionAssignmentsMenuId)]
        public static void CampMenuBackgroundInit(MenuCallbackArgs args)
        {
            // Use a military meeting/camp background
            args.MenuContext.SetBackgroundMeshName("encounter_meeting");

            // Add ambient audio for the camp atmosphere
            args.MenuContext.SetAmbientSound("event:/map/ambient/node/settlements/2d/keep");

            // NOTE: We intentionally do NOT call StartWait() here. Camp menus rely on
            // the wait-menu type for layout/options, but we keep time control unchanged and
            // preserve the player's captured time mode when hopping between submenus.
        }

        #endregion

        #region Service Records Menu

        /// <summary>
        /// Creates the Service Records submenu with options for different record views.
        /// </summary>
        private void AddServiceRecordsMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                ServiceRecordsMenuId,
                "{=ct_records_intro}Your service history and military records.",
                OnServiceRecordsInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Current Posting
            starter.AddGameMenuOption(
                ServiceRecordsMenuId,
                "records_current_posting",
                "{=ct_option_current}Current Posting",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(CurrentPostingMenuId),
                false,
                1);

            // Faction Records
            starter.AddGameMenuOption(
                ServiceRecordsMenuId,
                "records_faction_records",
                "{=ct_option_faction}Faction Records",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(FactionRecordsMenuId),
                false,
                2);

            // Lifetime Summary
            starter.AddGameMenuOption(
                ServiceRecordsMenuId,
                "records_lifetime_summary",
                "{=ct_option_lifetime}Lifetime Summary",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(LifetimeSummaryMenuId),
                false,
                3);

            // Back to Camp hub
            starter.AddGameMenuOption(
                ServiceRecordsMenuId,
                "records_back",
                "{=ct_back_tent}Back to Camp",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(CampHubMenuId),
                true,
                100);
        }

        private void OnServiceRecordsInit(MenuCallbackArgs args)
        {
            // Start wait to enable time controls for the wait menu
            args.MenuContext.GameMenu.StartWait();

            // Unlock time control so player can change speed, then restore their prior state
            Campaign.Current.SetTimeControlModeLock(false);

            // Restore captured time using stoppable equivalents, preserving Stop when paused
            var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
            var normalized = QuartermasterManager.NormalizeToStoppable(captured);
            Campaign.Current.TimeControlMode = normalized;

            ModLogger.Debug(LogCategory, "Service Records menu initialized");
        }

        #endregion

        #region Current Posting Menu

        /// <summary>
        /// Creates the Current Posting display showing current enlistment details.
        /// </summary>
        private void AddCurrentPostingMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                CurrentPostingMenuId,
                "{CURRENT_POSTING_TEXT}",
                OnCurrentPostingInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Back option
            starter.AddGameMenuOption(
                CurrentPostingMenuId,
                "ct_posting_back",
                "{=ct_back_records}Back to Service Records",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(ServiceRecordsMenuId),
                true,
                100);
        }

        /// <summary>
        /// Initializes the Current Posting display with live data.
        /// </summary>
        private void OnCurrentPostingInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var text = BuildCurrentPostingText();
                MBTextManager.SetTextVariable("CURRENT_POSTING_TEXT", text);
                ModLogger.Debug(LogCategory, "Current Posting display initialized");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing Current Posting", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("CURRENT_POSTING_TEXT",
                    new TextObject("{=enl_camp_error_current_posting}Error loading current posting data.").ToString());
            }
        }

        /// <summary>
        /// Builds the formatted text for current posting display.
        /// Uses clean, modern formatting without ASCII art.
        /// </summary>
        private string BuildCurrentPostingText()
        {
            var sb = new StringBuilder();
            var enlistment = EnlistmentBehavior.Instance;
            var recordManager = ServiceRecordManager.Instance;

            if (enlistment?.IsEnlisted != true)
            {
                return new TextObject("{=enl_posting_not_enlisted}You are not currently enlisted.").ToString();
            }

            var lord = enlistment.CurrentLord;
            var faction = lord?.Clan?.MapFaction;
            var tier = enlistment.EnlistmentTier;
            var daysServed = (int)enlistment.DaysServed;
            var daysRemaining = GetDaysRemaining(enlistment);
            var factionName = faction?.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString();
            var lordName = lord?.Name?.ToString() ?? new TextObject("{=enl_ui_unknown}Unknown").ToString();
            var rankName = GetRankName(tier);
            var termBattles = recordManager?.CurrentTermBattles ?? 0;
            var termKills = recordManager?.CurrentTermKills ?? 0;
            var pendingPay = enlistment.PendingMusterPay;
            var nextPay = enlistment.NextPaydaySafe;
            var daysToPay = Math.Max(0, (float)(nextPay - CampaignTime.Now).ToDays);
            var lastPay = enlistment.LastPayOutcome;
            var musterQueued = enlistment.IsPayMusterPending;
            var pensionDaily = GetPensionDaily(enlistment);
            var pensionStatus = GetPensionStatus(enlistment);

            _ = sb.AppendLine();
            _ = sb.AppendLine(new TextObject("{=enl_posting_header_record}â€” Current Service Record â€”").ToString());
            _ = sb.AppendLine();

            var postingLine = new TextObject("{=enl_posting_line_posting}Posting: Army of {FACTION}");
            _ = postingLine.SetTextVariable("FACTION", factionName);
            _ = sb.AppendLine(postingLine.ToString());

            var commanderLine = new TextObject("{=enl_posting_line_commander}Commander: {LORD}");
            _ = commanderLine.SetTextVariable("LORD", lordName);
            _ = sb.AppendLine(commanderLine.ToString());

            var rankLine = new TextObject("{=enl_posting_line_rank}Rank: {RANK} (Tier {TIER})");
            _ = rankLine.SetTextVariable("RANK", rankName ?? string.Empty);
            _ = rankLine.SetTextVariable("TIER", tier);
            _ = sb.AppendLine(rankLine.ToString());
            _ = sb.AppendLine();

            var daysLine = new TextObject("{=enl_posting_line_days_served}Days Served: {DAYS}");
            _ = daysLine.SetTextVariable("DAYS", daysServed);
            _ = sb.AppendLine(daysLine.ToString());

            var contractLine = new TextObject("{=enl_posting_line_contract}Contract: {CONTRACT}");
            _ = contractLine.SetTextVariable("CONTRACT", daysRemaining ?? string.Empty);
            _ = sb.AppendLine(contractLine.ToString());
            _ = sb.AppendLine();
            _ = sb.AppendLine(new TextObject("{=enl_posting_header_pay}â€” Pay Muster â€”").ToString());
            _ = sb.AppendLine();

            var pendingLine = new TextObject("{=enl_posting_line_pending_pay}Pending Muster Pay: {PAY} denars");
            _ = pendingLine.SetTextVariable("PAY", pendingPay);
            _ = sb.AppendLine(pendingLine.ToString());

            var nextPayLine = new TextObject("{=enl_posting_line_next_payday}Next Payday: in {DAYS} days");
            _ = nextPayLine.SetTextVariable("DAYS", $"{daysToPay:F1}");
            _ = sb.AppendLine(nextPayLine.ToString());

            var lastOutcomeLine = new TextObject("{=enl_posting_line_last_outcome}Last Outcome: {OUTCOME}");
            _ = lastOutcomeLine.SetTextVariable("OUTCOME", lastPay ?? string.Empty);
            _ = sb.AppendLine(lastOutcomeLine.ToString());
            if (musterQueued)
            {
                _ = sb.AppendLine(new TextObject("{=enl_posting_status_muster_queued}Status: Muster queued").ToString());
            }
            if (enlistment.IsPendingDischarge)
            {
                _ = sb.AppendLine(new TextObject("{=enl_posting_status_discharge_pending}Status: Discharge pending (will resolve at next pay muster)").ToString());
            }
            _ = sb.AppendLine();
            _ = sb.AppendLine(new TextObject("{=enl_posting_header_pension}â€” Pension â€”").ToString());
            _ = sb.AppendLine();

            var pensionLine = new TextObject("{=enl_posting_line_pension_daily}Daily Pension: {PAY} denars");
            _ = pensionLine.SetTextVariable("PAY", pensionDaily);
            _ = sb.AppendLine(pensionLine.ToString());

            var pensionStatusLine = new TextObject("{=enl_posting_line_status}Status: {STATUS}");
            _ = pensionStatusLine.SetTextVariable("STATUS", pensionStatus ?? string.Empty);
            _ = sb.AppendLine(pensionStatusLine.ToString());
            _ = sb.AppendLine();
            _ = sb.AppendLine(new TextObject("{=enl_posting_header_term}â€” This Term â€”").ToString());
            _ = sb.AppendLine();

            var battlesLine = new TextObject("{=enl_posting_line_battles}Battles Fought: {COUNT}");
            _ = battlesLine.SetTextVariable("COUNT", termBattles);
            _ = sb.AppendLine(battlesLine.ToString());

            var killsLine = new TextObject("{=enl_posting_line_kills}Enemies Slain: {COUNT}");
            _ = killsLine.SetTextVariable("COUNT", termKills);
            _ = sb.AppendLine(killsLine.ToString());

            return sb.ToString();
        }

        private static int GetPensionDaily(EnlistmentBehavior enlistment)
        {
            return enlistment?.PensionAmountPerDay ?? 0;
        }

        private static string GetPensionStatus(EnlistmentBehavior enlistment)
        {
            if (enlistment == null)
            {
                return new TextObject("{=enl_posting_pension_unknown}Unknown").ToString();
            }

            if (enlistment.IsPensionPaused)
            {
                return new TextObject("{=enl_posting_pension_paused}Paused").ToString();
            }

            return new TextObject("{=enl_posting_pension_active}Active").ToString();
        }

        /// <summary>
        /// Calculates days remaining until first-term completion (used as a simple progress indicator).
        /// </summary>
        private static string GetDaysRemaining(EnlistmentBehavior enlistment)
        {
            try
            {
                var retirementConfig = EnlistedConfig.LoadRetirementConfig();
                var termEnd = enlistment.EnlistmentDate + CampaignTime.Days(retirementConfig.FirstTermDays);
                var remainingDays = (int)(termEnd - CampaignTime.Now).ToDays;

                if (remainingDays <= 0)
                {
                    return new TextObject("{=enl_term_complete}Term complete").ToString();
                }

                var t = new TextObject("{=enl_days_count}{DAYS} days");
                _ = t.SetTextVariable("DAYS", remainingDays);
                return t.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error calculating days remaining", ex);
                return new TextObject("{=enl_unknown}Unknown").ToString();
            }
        }

        #endregion

        #region Faction Records Menu

        /// <summary>
        /// Creates the Faction Records list showing all factions served.
        /// </summary>
        private void AddFactionRecordsMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                FactionRecordsMenuId,
                "{FACTION_RECORDS_TEXT}",
                OnFactionRecordsInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Dynamic faction options will be added during init
            // For now, add a static back option
            starter.AddGameMenuOption(
                FactionRecordsMenuId,
                "ct_factions_back",
                "{=ct_back_records}Back to Service Records",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(ServiceRecordsMenuId),
                true,
                100);
        }

        /// <summary>
        /// Initializes the Faction Records list.
        /// </summary>
        private void OnFactionRecordsInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var text = BuildFactionRecordsListText();
                MBTextManager.SetTextVariable("FACTION_RECORDS_TEXT", text);
                ModLogger.Debug(LogCategory, "Faction Records list initialized");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing Faction Records", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("FACTION_RECORDS_TEXT",
                    new TextObject("{=enl_camp_error_faction_records}Error loading faction records.").ToString());
            }
        }

        /// <summary>
        /// Builds the faction records list text showing all factions served with summary stats.
        /// Uses clean, modern formatting.
        /// </summary>
        private string BuildFactionRecordsListText()
        {
            var sb = new StringBuilder();
            var recordManager = ServiceRecordManager.Instance;
            var records = recordManager?.GetAllRecords();

            _ = sb.AppendLine();
            var factionServiceRecordsText = new TextObject("{=records_faction_service_title}Faction Service Records").ToString();
            _ = sb.AppendLine($"— {factionServiceRecordsText} —");
            _ = sb.AppendLine();

            if (records == null || records.Count == 0)
            {
                _ = sb.AppendLine("No faction service records found.");
                _ = sb.AppendLine();
                _ = sb.AppendLine("Enlist with a lord to begin building your military service history.");
            }
            else
            {
                foreach (var record in records.Values.OrderByDescending(r => r.TotalDaysServed))
                {
                    var factionType = FormatFactionType(record.FactionType);
                    _ = sb.AppendLine($"• {record.FactionDisplayName}");
                    _ = sb.AppendLine($"  {factionType} — {record.TermsCompleted} terms, {record.TotalDaysServed} days");
                    _ = sb.AppendLine($"  Kills: {record.TotalKills}");
                    _ = sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Formats the faction type for display.
        /// </summary>
        private static string FormatFactionType(string factionType)
        {
            return factionType switch
            {
                "kingdom" => new TextObject("{=enl_faction_type_kingdom}Kingdom").ToString(),
                "minor" => new TextObject("{=enl_faction_type_minor}Minor Faction").ToString(),
                "merc" => new TextObject("{=enl_faction_type_merc}Mercenary Company").ToString(),
                "clan" => new TextObject("{=enl_faction_type_clan}Noble Clan").ToString(),
                "bandit" => new TextObject("{=enl_faction_bandit_clan}Bandit Clan").ToString(),
                _ => new TextObject("{=enl_unknown}Unknown").ToString()
            };
        }

        #endregion

        #region Faction Detail Menu

        /// <summary>
        /// Creates the Faction Detail view for a specific faction.
        /// </summary>
        private void AddFactionDetailMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                FactionDetailMenuId,
                "{FACTION_DETAIL_TEXT}",
                OnFactionDetailInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Back option
            starter.AddGameMenuOption(
                FactionDetailMenuId,
                "ct_detail_back",
                "{=ct_back_factions}Back to Faction Records",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(FactionRecordsMenuId),
                true,
                100);
        }

        /// <summary>
        /// Initializes the Faction Detail display.
        /// </summary>
        private void OnFactionDetailInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var text = BuildFactionDetailText(_selectedFactionKey);
                MBTextManager.SetTextVariable("FACTION_DETAIL_TEXT", text);
                ModLogger.Debug(LogCategory, $"Faction Detail initialized for: {_selectedFactionKey}");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing Faction Detail", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("FACTION_DETAIL_TEXT",
                    new TextObject("{=enl_camp_error_faction_detail}Error loading faction detail.").ToString());
            }
        }

        /// <summary>
        /// Builds detailed faction record text.
        /// Uses clean, modern formatting.
        /// </summary>
        private string BuildFactionDetailText(string factionKey)
        {
            var sb = new StringBuilder();
            var recordManager = ServiceRecordManager.Instance;
            var record = recordManager?.GetRecord(factionKey);

            if (record == null)
            {
                return "No record found for this faction.";
            }

            var highestRank = GetRankName(record.HighestTier);

            _ = sb.AppendLine();
            _ = sb.AppendLine($"â€” {record.FactionDisplayName} â€”");
            _ = sb.AppendLine();
            _ = sb.AppendLine($"Enlistments: {record.Enlistments}");
            _ = sb.AppendLine($"Terms Completed: {record.TermsCompleted}");
            _ = sb.AppendLine($"Days Served: {record.TotalDaysServed}");
            _ = sb.AppendLine($"Highest Rank: {highestRank} (Tier {record.HighestTier})");
            _ = sb.AppendLine();
            _ = sb.AppendLine("â€” Combat Record â€”");
            _ = sb.AppendLine();
            _ = sb.AppendLine($"Battles Fought: {record.BattlesFought}");
            _ = sb.AppendLine($"Enemies Slain: {record.TotalKills}");
            _ = sb.AppendLine($"Lords Served: {record.LordsServed}");

            return sb.ToString();
        }

        /// <summary>
        /// Sets the selected faction for detail view and navigates to detail menu.
        /// </summary>
        public void ViewFactionDetail(string factionKey)
        {
            _selectedFactionKey = factionKey;
            SwitchToMenuPreserveTime(FactionDetailMenuId);
        }

        #endregion

        #region Lifetime Summary Menu

        /// <summary>
        /// Creates the Lifetime Summary display showing cross-faction totals.
        /// </summary>
        private void AddLifetimeSummaryMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                LifetimeSummaryMenuId,
                "{LIFETIME_SUMMARY_TEXT}",
                OnLifetimeSummaryInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Back option
            starter.AddGameMenuOption(
                LifetimeSummaryMenuId,
                "ct_lifetime_back",
                "{=ct_back_records}Back to Camp",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(CampHubMenuId),
                true,
                100);
        }

        /// <summary>
        /// Initializes the Lifetime Summary display.
        /// </summary>
        private void OnLifetimeSummaryInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var text = BuildLifetimeSummaryText();
                MBTextManager.SetTextVariable("LIFETIME_SUMMARY_TEXT", text);
                ModLogger.Debug(LogCategory, "Lifetime Summary initialized");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing Lifetime Summary", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("LIFETIME_SUMMARY_TEXT",
                    new TextObject("{=enl_camp_error_lifetime_summary}Error loading lifetime summary.").ToString());
            }
        }

        /// <summary>
        /// Builds the lifetime summary text showing cross-faction statistics.
        /// Uses clean, modern formatting.
        /// </summary>
        private string BuildLifetimeSummaryText()
        {
            var sb = new StringBuilder();
            var recordManager = ServiceRecordManager.Instance;
            var lifetime = recordManager?.LifetimeRecord;

            _ = sb.AppendLine();
            var lifetimeHeaderText = new TextObject("{=records_lifetime_summary}Lifetime Service Summary").ToString();
            _ = sb.AppendLine($"— {lifetimeHeaderText} —");
            _ = sb.AppendLine();

            if (lifetime == null)
            {
                var noRecordsText = new TextObject("{=records_no_lifetime}No lifetime service records found").ToString();
                var careerNotBegunText = new TextObject("{=records_career_not_begun}Your military career has not yet begun").ToString();
                _ = sb.AppendLine($"{noRecordsText}.");
                _ = sb.AppendLine();
                _ = sb.AppendLine($"{careerNotBegunText}.");
            }
            else
            {
                // Calculate years and months from total days
                var (years, months) = lifetime.GetServiceDuration();
                var yearText = years > 1
                    ? new TextObject("{=records_years}years").ToString()
                    : new TextObject("{=records_year}year").ToString();
                var monthText = months != 1
                    ? new TextObject("{=records_months}months").ToString()
                    : new TextObject("{=records_month}month").ToString();
                var daysText = new TextObject("{=records_days}days").ToString();

                var timeString = years > 0
                    ? $"{years} {yearText}, {months} {monthText}"
                    : $"{months} {monthText}";

                if (years == 0 && months == 0)
                {
                    timeString = $"{lifetime.TotalDaysServed} {daysText}";
                }

                var timeInServiceLabel = new TextObject("{=records_time_in_service}Time in Service").ToString();
                var totalEnlistmentsLabel = new TextObject("{=records_total_enlistments}Total Enlistments").ToString();
                var termsCompletedLabel = new TextObject("{=records_terms_completed}Terms Completed").ToString();

                _ = sb.AppendLine($"{timeInServiceLabel}: {timeString}");
                _ = sb.AppendLine($"{totalEnlistmentsLabel}: {lifetime.TotalEnlistments}");
                _ = sb.AppendLine($"{termsCompletedLabel}: {lifetime.TermsCompleted}");
                _ = sb.AppendLine();

                // Current service snapshot (replaces the removed Activity Log + XP Breakdown menus)
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted == true)
                {
                    var currentEnlistmentText = new TextObject("{=records_current_enlistment}Current Enlistment").ToString();
                    _ = sb.AppendLine($"— {currentEnlistmentText} —");
                    _ = sb.AppendLine();

                    var tierLabel = new TextObject("{=records_tier}Tier").ToString();
                    var serviceXpLabel = new TextObject("{=records_service_xp}Service XP").ToString();
                    var daysServedLabel = new TextObject("{=records_days_served}Days Served").ToString();
                    var nextTierLabel = new TextObject("{=records_next_tier_req}Next Tier Requirement").ToString();

                    _ = sb.AppendLine($"{tierLabel}: {enlistment.EnlistmentTier}");
                    _ = sb.AppendLine($"{serviceXpLabel}: {enlistment.EnlistmentXP}");
                    _ = sb.AppendLine($"{daysServedLabel}: {(int)enlistment.DaysServed}");

                    if (enlistment.EnlistmentTier < 6)
                    {
                        var tierXp = EnlistedConfig.GetTierXpRequirements();
                        var nextTierXp = enlistment.EnlistmentTier < tierXp.Length ? tierXp[enlistment.EnlistmentTier] : tierXp[tierXp.Length - 1];
                        _ = sb.AppendLine($"{nextTierLabel}: {nextTierXp} XP");
                    }
                    else
                    {
                        var maxTierText = new TextObject("{=records_max_tier}(Max tier)").ToString();
                        _ = sb.AppendLine($"{nextTierLabel}: {maxTierText}");
                    }

                    if (enlistment.IsPendingDischarge)
                    {
                        var dischargeLabel = new TextObject("{=records_discharge}Discharge").ToString();
                        var pendingText = new TextObject("{=records_pending_discharge}Pending (resolves at next pay muster)").ToString();
                        _ = sb.AppendLine($"{dischargeLabel}: {pendingText}");
                    }

                    if (enlistment.PendingMusterPay > 0)
                    {
                        var pendingPayLabel = new TextObject("{=records_pending_pay}Pending Pay (next muster)").ToString();
                        _ = sb.AppendLine($"{pendingPayLabel}: {enlistment.PendingMusterPay}");
                    }

                    if (enlistment.OwedBackpay > 0)
                    {
                        var backpayLabel = new TextObject("{=records_owed_backpay}Owed Backpay").ToString();
                        _ = sb.AppendLine($"{backpayLabel}: {enlistment.OwedBackpay}");
                    }

                    _ = sb.AppendLine();
                    var xpSourcesText = new TextObject("{=records_xp_sources}XP Sources").ToString();
                    _ = sb.AppendLine($"— {xpSourcesText} —");
                    _ = sb.AppendLine();

                    var xpBattlesText = new TextObject("{=records_xp_battles}Battles: XP for participating in combat").ToString();
                    var xpDutiesText = new TextObject("{=records_xp_duties}Duties: Daily XP for assigned tasks").ToString();
                    var xpActivitiesText = new TextObject("{=records_xp_activities}Activities: Skill XP from camp and lance activities").ToString();
                    var xpServiceText = new TextObject("{=records_xp_service}Service: Passive XP for time served").ToString();

                    _ = sb.AppendLine($"• {xpBattlesText}");
                    _ = sb.AppendLine($"• {xpDutiesText}");
                    _ = sb.AppendLine($"• {xpActivitiesText}");
                    _ = sb.AppendLine($"• {xpServiceText}");
                    _ = sb.AppendLine();

                    var thisTermText = new TextObject("{=records_this_term}This Term").ToString();
                    _ = sb.AppendLine($"— {thisTermText} —");
                    _ = sb.AppendLine();

                    var battlesLabel = new TextObject("{=records_battles}Battles").ToString();
                    var killsLabel = new TextObject("{=records_kills}Kills").ToString();

                    _ = sb.AppendLine($"{battlesLabel}: {recordManager.CurrentTermBattles}");
                    _ = sb.AppendLine($"{killsLabel}: {recordManager.CurrentTermKills}");
                    _ = sb.AppendLine();
                }

                // List factions served
                if (lifetime.FactionsServed is { Count: > 0 })
                {
                    var factionsServedText = new TextObject("{=records_factions_served}Factions Served").ToString();
                    _ = sb.AppendLine($"— {factionsServedText} —");
                    _ = sb.AppendLine();

                    var termText = new TextObject("{=records_term}term").ToString();
                    var termsText = new TextObject("{=records_terms}terms").ToString();
                    var inProgressText = new TextObject("{=records_in_progress}in progress").ToString();

                    var allRecords = recordManager.GetAllRecords();
                    foreach (var factionId in lifetime.FactionsServed)
                    {
                        if (allRecords.TryGetValue(factionId, out var record))
                        {
                            var terms = record.TermsCompleted > 0
                                ? $"{record.TermsCompleted} {(record.TermsCompleted > 1 ? termsText : termText)}"
                                : inProgressText;
                            _ = sb.AppendLine($"• {record.FactionDisplayName} ({terms})");
                        }
                    }
                    _ = sb.AppendLine();
                }

                var combatStatsText = new TextObject("{=records_combat_statistics}Combat Statistics").ToString();
                _ = sb.AppendLine($"— {combatStatsText} —");
                _ = sb.AppendLine();

                var totalBattlesLabel = new TextObject("{=records_total_battles}Total Battles").ToString();
                var enemiesSlainLabel = new TextObject("{=records_enemies_slain}Enemies Slain").ToString();

                _ = sb.AppendLine($"{totalBattlesLabel}: {lifetime.TotalBattlesFought}");
                _ = sb.AppendLine($"{enemiesSlainLabel}: {lifetime.LifetimeKills}");
            }

            return sb.ToString();
        }

        #endregion

        #region Retinue Menu

        /// <summary>
        /// Creates the Personal Retinue submenu showing current muster status and options.
        /// </summary>
        private void AddRetinueMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                RetinueMenuId,
                "{RETINUE_STATUS_TEXT}",
                OnRetinueMenuInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Current Muster option - ManageGarrison icon (soldiers/shield)
            starter.AddGameMenuOption(
                RetinueMenuId,
                "ct_retinue_muster",
                "{=ct_retinue_current_muster}View Current Muster",
                args =>
                {
                    var manager = RetinueManager.Instance;
                    var hasRetinue = manager?.State?.HasRetinue == true;
                    args.IsEnabled = hasRetinue;
                    // Hint refreshes when enabled; clear guidance when empty.
                    args.Tooltip = hasRetinue
                        ? new TextObject("{=ct_retinue_muster_hint}Select to refresh the menu and see updated retinue data.")
                        : new TextObject("{=ct_retinue_no_soldiers}You have no soldiers mustered.");
                    args.optionLeaveType = GameMenuOption.LeaveType.ManageGarrison;
                    return true;
                },
                _ =>
                {
                    // Refresh the menu to show current muster breakdown
                    SwitchToMenuPreserveTime(RetinueMenuId);
                },
                false,
                1);

            // Purchase Soldiers option - Recruit icon
            starter.AddGameMenuOption(
                RetinueMenuId,
                "ct_retinue_purchase",
                "{=ct_retinue_purchase}Purchase Soldiers",
                args =>
                {
                    var manager = RetinueManager.Instance;
                    var enlistment = EnlistmentBehavior.Instance;

                    if (manager is not null && enlistment is not null)
                    {
                        var tierCapacity = RetinueManager.GetTierCapacity(enlistment.EnlistmentTier);
                        var currentSoldiers = manager.State?.TotalSoldiers ?? 0;
                        var partySpace = GetAvailablePartySpace();

                        if (currentSoldiers >= tierCapacity)
                        {
                            args.IsEnabled = false;
                            args.Tooltip = new TextObject("{=ct_warn_full_retinue}Your retinue is at full strength.");
                        }
                        else if (partySpace <= 0)
                        {
                            args.IsEnabled = false;
                            args.Tooltip = new TextObject("{=ct_warn_party_full}Your party is full. Dismiss troops or increase party size.");
                        }
                    }

                    args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(RetinuePurchaseMenuId),
                false,
                2);

            // Request Reinforcements option - Trade icon (coins)
            // Uses relation-based pricing and cooldown
            starter.AddGameMenuOption(
                RetinueMenuId,
                "ct_retinue_request_reinforcements",
                "{REQUEST_REINFORCEMENTS_TEXT}",
                IsRequestReinforcementsAvailable,
                _ => SwitchToMenuPreserveTime(RetinueRequisitionMenuId),
                false,
                3);

            // Dismiss Soldiers option - DonateTroops icon
            starter.AddGameMenuOption(
                RetinueMenuId,
                "ct_retinue_dismiss",
                "{=ct_retinue_dismiss}Dismiss Soldiers",
                args =>
                {
                    var manager = RetinueManager.Instance;
                    var hasRetinue = manager?.State?.HasRetinue == true;
                    args.IsEnabled = hasRetinue;
                    if (!hasRetinue)
                    {
                        args.Tooltip = new TextObject("{=ct_retinue_no_soldiers}You have no soldiers to dismiss.");
                    }
                    args.optionLeaveType = GameMenuOption.LeaveType.DonateTroops;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(RetinueDismissMenuId),
                false,
                4);

            // Back option
            starter.AddGameMenuOption(
                RetinueMenuId,
                "ct_retinue_back",
                "{=ct_back_tent}Back to Camp",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(CampHubMenuId),
                true,
                100);
        }

        /// <summary>
        /// Initializes the Retinue menu with current status.
        /// </summary>
        private void OnRetinueMenuInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var text = BuildRetinueStatusText();
                MBTextManager.SetTextVariable("RETINUE_STATUS_TEXT", text);

                // Set Request Reinforcements option text with lord name and cost
                SetRequestReinforcementsOptionText();

                ModLogger.Debug(LogCategory, "Retinue menu initialized");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing Retinue menu", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("RETINUE_STATUS_TEXT",
                    new TextObject("{=enl_retinue_error_loading_status}Error loading retinue status.").ToString());
            }
        }

        /// <summary>
        /// Sets the Request Reinforcements menu option text with lord name and relation info.
        /// Shows cost with relation-based discount if applicable.
        /// </summary>
        private static void SetRequestReinforcementsOptionText()
        {
            var manager = RetinueManager.Instance;
            var enlistment = EnlistmentBehavior.Instance;
            var lord = enlistment?.EnlistedLord;

            if (manager == null || lord == null)
            {
                MBTextManager.SetTextVariable("REQUEST_REINFORCEMENTS_TEXT",
                    new TextObject("{=enl_retinue_option_request}Request Reinforcements").ToString());
                return;
            }

            var relation = RetinueManager.GetLordRelation();
            var cost = manager.CalculateReinforcementRequestCost();
            var cooldownDays = manager.GetReinforcementRequestCooldownDays();
            var missing = manager.GetMissingSoldierCount();

            string optionText;
            if (relation < RetinueManager.MinRelationForRequest)
            {
                // Low relation - show blocked
                var t = new TextObject("{=enl_retinue_option_request_blocked}Request Reinforcements (relation too low)");
                optionText = t.ToString();
            }
            else if (cooldownDays > 0)
            {
                // On cooldown
                var t = new TextObject("{=enl_retinue_option_request_cooldown}Request Reinforcements ({DAYS}d cooldown)");
                _ = t.SetTextVariable("DAYS", cooldownDays);
                optionText = t.ToString();
            }
            else if (missing <= 0)
            {
                // At capacity
                optionText = new TextObject("{=enl_retinue_option_request_capacity}Request Reinforcements (at capacity)").ToString();
            }
            else
            {
                // Available - show cost with lord name
                var t = new TextObject("{=enl_retinue_option_request_cost}Request from {LORD} ({COST}{GOLD_ICON})");
                _ = t.SetTextVariable("LORD", lord.Name);
                _ = t.SetTextVariable("COST", cost);
                _ = t.SetTextVariable("GOLD_ICON", "{GOLD_ICON}");
                optionText = t.ToString();
            }

            MBTextManager.SetTextVariable("REQUEST_REINFORCEMENTS_TEXT", optionText);
        }

        /// <summary>
        /// Builds the retinue status text showing current muster and capacity.
        /// Uses clean formatting with inline gold icons for currency.
        /// </summary>
        private string BuildRetinueStatusText()
        {
            var sb = new StringBuilder();
            var enlistment = EnlistmentBehavior.Instance;
            var manager = RetinueManager.Instance;

            if (enlistment?.IsEnlisted != true)
            {
                return new TextObject("{=enl_retinue_not_enlisted}You are not currently enlisted.").ToString();
            }

            var tier = enlistment.EnlistmentTier;
            var rankName = GetRankName(tier);
            var unitName = GetUnitNameForTier(tier);
            var tierCapacity = RetinueManager.GetTierCapacity(tier);
            var currentSoldiers = manager?.State?.TotalSoldiers ?? 0;
            var selectedType = manager?.State?.SelectedTypeId;
            var partySpace = GetAvailablePartySpace();
            var dailyUpkeep = currentSoldiers * 2;
            var partyLimit = PartyBase.MainParty?.PartySizeLimit ?? 0;
            var currentMembers = PartyBase.MainParty?.NumberOfAllMembers ?? 0;

            _ = sb.AppendLine();
            _ = sb.AppendLine(new TextObject("{=enl_retinue_header_personal}â€” Personal Retinue â€”").ToString());
            _ = sb.AppendLine();

            var rankLine = new TextObject("{=enl_retinue_line_rank}Rank: {RANK_NAME} (Tier {TIER})");
            _ = rankLine.SetTextVariable("RANK_NAME", rankName ?? string.Empty);
            _ = rankLine.SetTextVariable("TIER", tier);
            _ = sb.AppendLine(rankLine.ToString());

            var limitLine = new TextObject("{=enl_retinue_line_command_limit}Command Limit: {UNIT_NAME}");
            _ = limitLine.SetTextVariable("UNIT_NAME", unitName ?? string.Empty);
            _ = sb.AppendLine(limitLine.ToString());
            _ = sb.AppendLine();
            _ = sb.AppendLine(new TextObject("{=enl_retinue_header_current_muster}â€” Current Muster â€”").ToString());
            _ = sb.AppendLine();

            if (string.IsNullOrEmpty(selectedType))
            {
                _ = sb.AppendLine(new TextObject("{=enl_retinue_none_mustered}No soldiers mustered.").ToString());
                _ = sb.AppendLine(new TextObject("{=enl_retinue_select_type_prompt}Select a soldier type to begin.").ToString());
            }
            else
            {
                var typeName = GetSoldierTypeName(selectedType, enlistment.CurrentLord?.Culture);

                var typeLine = new TextObject("{=enl_retinue_line_type}Type: {TYPE_NAME}");
                _ = typeLine.SetTextVariable("TYPE_NAME", typeName ?? string.Empty);
                _ = sb.AppendLine(typeLine.ToString());

                var soldiersLine = new TextObject("{=enl_retinue_line_soldiers}Soldiers: {CUR} / {MAX}");
                _ = soldiersLine.SetTextVariable("CUR", currentSoldiers);
                _ = soldiersLine.SetTextVariable("MAX", tierCapacity);
                _ = sb.AppendLine(soldiersLine.ToString());

                var upkeepLine = new TextObject("{=enl_retinue_line_upkeep}Daily Upkeep: {UPKEEP}{GOLD_ICON}");
                _ = upkeepLine.SetTextVariable("UPKEEP", dailyUpkeep);
                _ = upkeepLine.SetTextVariable("GOLD_ICON", "{GOLD_ICON}");
                _ = sb.AppendLine(upkeepLine.ToString());
            }

            _ = sb.AppendLine();
            _ = sb.AppendLine(new TextObject("{=enl_retinue_header_party_capacity}â€” Party Capacity â€”").ToString());
            _ = sb.AppendLine();

            var partyLimitLine = new TextObject("{=enl_retinue_line_party_limit}Party Limit: {LIMIT}");
            _ = partyLimitLine.SetTextVariable("LIMIT", partyLimit);
            _ = sb.AppendLine(partyLimitLine.ToString());

            var partyMembersLine = new TextObject("{=enl_retinue_line_party_members}Current Members: {COUNT}");
            _ = partyMembersLine.SetTextVariable("COUNT", currentMembers);
            _ = sb.AppendLine(partyMembersLine.ToString());

            var partySpaceLine = new TextObject("{=enl_retinue_line_party_space}Available Space: {SPACE}");
            _ = partySpaceLine.SetTextVariable("SPACE", partySpace);
            _ = sb.AppendLine(partySpaceLine.ToString());

            if (partySpace < tierCapacity - currentSoldiers)
            {
                _ = sb.AppendLine();
                _ = sb.AppendLine(new TextObject("{=enl_retinue_party_limits_retinue}Party size limits your retinue.").ToString());
            }

            // Add reinforcement status section
            _ = sb.AppendLine();
            _ = sb.AppendLine(new TextObject("{=enl_retinue_header_reinforcement}-- Reinforcement Status --").ToString());
            _ = sb.AppendLine();

            var state = manager?.State;
            if (state != null)
            {
                // Show last battle outcome
                var daysSinceBattle = state.GetDaysSinceLastBattle();
                if (daysSinceBattle >= 0)
                {
                    var outcomeText = state.LastBattleWon ? "Victory" : "Defeat";
                    var battleLine = new TextObject("{=enl_retinue_last_battle}Last battle: {OUTCOME} ({DAYS} days ago)");
                    _ = battleLine.SetTextVariable("OUTCOME", outcomeText);
                    _ = battleLine.SetTextVariable("DAYS", (int)daysSinceBattle);
                    _ = sb.AppendLine(battleLine.ToString());
                }
                else
                {
                    _ = sb.AppendLine(new TextObject("{=enl_retinue_no_battle}No recent battles").ToString());
                }

                // Show trickle status using the new helper method
                var (contextDesc, daysUntilNext, isBlocked) = RetinueTrickleSystem.GetTrickleStatusInfo();

                if (isBlocked && contextDesc == "Morale recovering")
                {
                    _ = sb.AppendLine(new TextObject("{=enl_retinue_delayed}Replacements delayed (morale recovering)").ToString());
                    if (daysUntilNext > 0)
                    {
                        var resumeLine = new TextObject("{=enl_retinue_resume}Resume in: {DAYS} days");
                        _ = resumeLine.SetTextVariable("DAYS", daysUntilNext);
                        _ = sb.AppendLine(resumeLine.ToString());
                    }
                }
                else if (currentSoldiers < tierCapacity)
                {
                    var statusLine = new TextObject("{=enl_retinue_status}Status: {CONTEXT}");
                    _ = statusLine.SetTextVariable("CONTEXT", contextDesc);
                    _ = sb.AppendLine(statusLine.ToString());

                    if (daysUntilNext > 0)
                    {
                        var nextLine = new TextObject("{=enl_retinue_next_recruit}Next recruit expected: {DAYS} days");
                        _ = nextLine.SetTextVariable("DAYS", daysUntilNext);
                        _ = sb.AppendLine(nextLine.ToString());
                    }
                    else
                    {
                        _ = sb.AppendLine(new TextObject("{=enl_retinue_arriving}Recruit arriving soon").ToString());
                    }
                }
                else
                {
                    _ = sb.AppendLine(new TextObject("{=enl_retinue_at_capacity}Retinue at full capacity").ToString());
                }

                // Show territory status
                var territory = RetinueTrickleSystem.IsInFriendlyTerritory() ? "Friendly" : "Hostile/Neutral";
                var territoryLine = new TextObject("{=enl_retinue_territory}Territory: {TERRITORY}");
                _ = territoryLine.SetTextVariable("TERRITORY", territory);
                _ = sb.AppendLine(territoryLine.ToString());
            }

            return sb.ToString();
        }

        #endregion

        #region Retinue Purchase Menu

        /// <summary>
        /// Creates the soldier type selection and purchase menu.
        /// </summary>
        private void AddRetinuePurchaseMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                RetinuePurchaseMenuId,
                "{RETINUE_PURCHASE_TEXT}",
                OnRetinuePurchaseInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Infantry option - Men-at-Arms
            starter.AddGameMenuOption(
                RetinuePurchaseMenuId,
                "ct_purchase_infantry",
                "{INFANTRY_OPTION_TEXT}",
                args => IsSoldierTypeAvailable(args, "infantry"),
                _ => OnSoldierTypePurchase("infantry"),
                false,
                1);

            // Archers option - Bowmen
            starter.AddGameMenuOption(
                RetinuePurchaseMenuId,
                "ct_purchase_archers",
                "{ARCHERS_OPTION_TEXT}",
                args => IsSoldierTypeAvailable(args, "archers"),
                _ => OnSoldierTypePurchase("archers"),
                false,
                2);

            // Cavalry option - Mounted Lancers
            starter.AddGameMenuOption(
                RetinuePurchaseMenuId,
                "ct_purchase_cavalry",
                "{CAVALRY_OPTION_TEXT}",
                args => IsSoldierTypeAvailable(args, "cavalry"),
                _ => OnSoldierTypePurchase("cavalry"),
                false,
                3);

            // Horse Archers option - Mounted Bowmen (faction restricted)
            starter.AddGameMenuOption(
                RetinuePurchaseMenuId,
                "ct_purchase_horse_archers",
                "{HORSE_ARCHERS_OPTION_TEXT}",
                args => IsSoldierTypeAvailable(args, "horse_archers"),
                _ => OnSoldierTypePurchase("horse_archers"),
                false,
                4);

            // Back option
            starter.AddGameMenuOption(
                RetinuePurchaseMenuId,
                "ct_purchase_back",
                "{=ct_back_retinue}Back to Retinue",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(RetinueMenuId),
                true,
                100);
        }

        /// <summary>
        /// Initializes the purchase menu. Shows gold, soldier options, and naval dismount note for mounted types.
        /// Uses clean formatting with inline gold icons.
        /// </summary>
        private void OnRetinuePurchaseInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var enlistment = EnlistmentBehavior.Instance;
                var culture = enlistment?.CurrentLord?.Culture;
                var tier = enlistment?.EnlistmentTier ?? 4;
                var playerGold = Hero.MainHero?.Gold ?? 0;

                var sb = new StringBuilder();
                _ = sb.AppendLine();
                _ = sb.AppendLine("â€” Purchase Soldiers â€”");
                _ = sb.AppendLine();
                _ = sb.AppendLine("Select the type of soldiers to muster.");
                _ = sb.AppendLine();
                _ = sb.AppendLine($"Your Gold: {playerGold}{{GOLD_ICON}}");
                _ = sb.AppendLine();
                _ = sb.AppendLine("* Mounted troops fight on foot in naval battles.");

                MBTextManager.SetTextVariable("RETINUE_PURCHASE_TEXT", sb.ToString());

                // Set option text variables with costs and gold icons
                SetSoldierTypeOptionText("infantry", culture, tier);
                SetSoldierTypeOptionText("archers", culture, tier);
                SetSoldierTypeOptionText("cavalry", culture, tier);
                SetSoldierTypeOptionText("horse_archers", culture, tier);

                ModLogger.Debug(LogCategory, "Retinue purchase menu initialized");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing purchase menu", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("RETINUE_PURCHASE_TEXT",
                    new TextObject("{=enl_camp_error_retinue_purchase}Error loading purchase options.").ToString());
            }
        }

        /// <summary>
        /// Sets the menu option text for a soldier type with inline gold icon.
        /// Mounted types marked with * for naval warning.
        /// </summary>
        private void SetSoldierTypeOptionText(string typeId, CultureObject culture, int playerTier)
        {
            var typeName = GetSoldierTypeName(typeId, culture);
            var cost = CalculateRecruitmentCost(typeId, culture, playerTier);
            var variableName = typeId.ToUpperInvariant() + "_OPTION_TEXT";

            // Use inline gold icon for cost display
            var optionText = $"{typeName} ({cost}{{GOLD_ICON}})";

            if (IsMountedType(typeId))
            {
                optionText += " *";
            }

            MBTextManager.SetTextVariable(variableName, optionText);
        }

        /// <summary>
        /// Returns true for cavalry/horse_archers which fight dismounted in naval battles.
        /// </summary>
        private static bool IsMountedType(string typeId)
        {
            return typeId is "cavalry" or "horse_archers";
        }

        /// <summary>
        /// Gets a human-readable display name for a formation/soldier type.
        /// Used for tooltip messages explaining formation restrictions.
        /// </summary>
        private static string GetFormationDisplayName(string typeId)
        {
            return typeId?.ToLowerInvariant() switch
            {
                "infantry" => "infantry",
                "archers" or "archer" or "ranged" => "archers",
                "cavalry" => "cavalry",
                "horse_archers" or "horsearcher" or "horse_archer" => "horse archers",
                _ => typeId ?? "soldiers"
            };
        }

        /// <summary>
        /// Validates soldier type availability. Checks player formation match, faction restrictions, affordability,
        /// and shows naval tooltip for mounted types.
        /// Players can only recruit soldiers matching their own troop type (infantry can only lead infantry, etc.)
        /// </summary>
        private bool IsSoldierTypeAvailable(MenuCallbackArgs args, string typeId)
        {
            var enlistment = EnlistmentBehavior.Instance;
            var culture = enlistment?.CurrentLord?.Culture;

            if (culture == null)
            {
                args.IsEnabled = false;
                return true;
            }

            // Detect player's actual formation from equipment
            var playerFormation = QuartermasterManager.Instance?.GetPlayerFormationString() ?? "infantry";

            // Map player formation to retinue type for comparison
            var playerRetinueType = playerFormation switch
            {
                "archer" or "ranged" => "archers",
                "cavalry" => "cavalry",
                "horsearcher" or "horse_archer" => "horse_archers",
                _ => "infantry"
            };

            if (!typeId.Equals(playerRetinueType, StringComparison.OrdinalIgnoreCase))
            {
                args.IsEnabled = false;
                var formationDisplayName = GetFormationDisplayName(playerRetinueType);
                var requestedDisplayName = GetFormationDisplayName(typeId);
                var tooltip = new TextObject("{=ct_warn_formation_mismatch}As a {PLAYER_TYPE}, you can only command {PLAYER_TYPE} soldiers. You cannot lead {REQUESTED_TYPE}.");
                _ = tooltip.SetTextVariable("PLAYER_TYPE", formationDisplayName);
                _ = tooltip.SetTextVariable("REQUESTED_TYPE", requestedDisplayName);
                args.Tooltip = tooltip;
                return true;
            }

            // Faction restriction check (horse archers not available for some factions)
            if (!RetinueManager.IsSoldierTypeAvailable(typeId, culture))
            {
                args.IsEnabled = false;
                var factionName = culture.Name?.ToString() ?? "This faction";
                var tooltip = new TextObject("{=ct_warn_faction_unavailable}{FACTION_NAME} does not field mounted archers.");
                _ = tooltip.SetTextVariable("FACTION_NAME", factionName);
                args.Tooltip = tooltip;
                return true;
            }

            // Affordability check
            var cost = CalculateRecruitmentCost(typeId, culture, enlistment.EnlistmentTier);
            var playerGold = Hero.MainHero?.Gold ?? 0;

            if (playerGold < cost)
            {
                args.IsEnabled = false;
                var tooltip = new TextObject("{=ct_warn_cannot_afford}You cannot afford this ({COST} denars required).");
                _ = tooltip.SetTextVariable("COST", cost);
                args.Tooltip = tooltip;
                return true;
            }

            // Type change requires dismissing current retinue
            var manager = RetinueManager.Instance;
            if (manager?.State?.HasRetinue == true && manager.State.SelectedTypeId != typeId)
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            }
            else
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
            }

            // Naval dismount info tooltip for mounted types
            if (IsMountedType(typeId))
            {
                var tooltipId = typeId == "cavalry"
                    ? "{=ct_naval_cavalry_tooltip}Cavalry will dismount and fight as infantry during naval engagements. Their horses cannot be brought aboard ships."
                    : "{=ct_naval_horse_archer_tooltip}Horse archers will dismount and fight as foot archers during naval engagements. Their horses cannot be brought aboard ships.";
                args.Tooltip = new TextObject(tooltipId);
            }

            return true;
        }

        /// <summary>
        /// Handles soldier type purchase selection.
        /// </summary>
        private void OnSoldierTypePurchase(string typeId)
        {
            var manager = RetinueManager.Instance;
            var enlistment = EnlistmentBehavior.Instance;

            if (manager == null || enlistment == null)
            {
                ModLogger.Warn(LogCategory, "Cannot purchase: manager or enlistment null");
                return;
            }

            var culture = enlistment.CurrentLord?.Culture;
            if (culture == null)
            {
                ModLogger.Warn(LogCategory, "Cannot purchase: no culture");
                return;
            }

            // Check if changing type with existing retinue
            if (manager.State?.HasRetinue == true && manager.State.SelectedTypeId != typeId)
            {
                // Show confirmation to dismiss existing retinue
                ShowTypeChangeConfirmation(typeId);
                return;
            }

            // Calculate how many soldiers to purchase
            var tierCapacity = RetinueManager.GetTierCapacity(enlistment.EnlistmentTier);
            var currentSoldiers = manager.State?.TotalSoldiers ?? 0;
            var partySpace = GetAvailablePartySpace();
            var canPurchase = Math.Min(tierCapacity - currentSoldiers, partySpace);

            if (canPurchase <= 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Cannot purchase soldiers: at capacity or party full.", Colors.Red));
                return;
            }

            // Calculate total cost
            var costPerSoldier = CalculateRecruitmentCost(typeId, culture, enlistment.EnlistmentTier);
            var playerGold = Hero.MainHero?.Gold ?? 0;
            var maxAffordable = playerGold / Math.Max(1, costPerSoldier);
            var purchaseCount = Math.Min(canPurchase, maxAffordable);

            if (purchaseCount <= 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "You cannot afford any soldiers.", Colors.Red));
                return;
            }

            // Show purchase confirmation
            ShowPurchaseConfirmation(typeId, purchaseCount, costPerSoldier * purchaseCount);
        }

        /// <summary>
        /// Shows purchase confirmation dialog.
        /// </summary>
        private void ShowPurchaseConfirmation(string typeId, int count, int totalCost)
        {
            var enlistment = EnlistmentBehavior.Instance;
            var culture = enlistment?.CurrentLord?.Culture;
            var typeName = GetSoldierTypeName(typeId, culture);

            var title = new TextObject("{=ct_purchase_confirm_title}Confirm Purchase");
            var message = new TextObject("{=ct_purchase_confirm_msg}Purchase {COUNT} {TYPE_NAME} for {COST} denars?\n\nDaily upkeep: {UPKEEP} denars");
            _ = message.SetTextVariable("COUNT", count);
            _ = message.SetTextVariable("TYPE_NAME", typeName);
            _ = message.SetTextVariable("COST", totalCost);
            _ = message.SetTextVariable("UPKEEP", count * 2);

            // pauseGameActiveState = false so dialogs don't freeze game time
            var pauseGameActiveState = ShouldPauseDuringCampInquiry();
            InformationManager.ShowInquiry(
                new InquiryData(
                    title.ToString(),
                    message.ToString(),
                    true,
                    true,
                    new TextObject("{=ct_confirm_yes}Purchase").ToString(),
                    new TextObject("{=ct_confirm_no}Cancel").ToString(),
                    () => ExecutePurchase(typeId, count, totalCost),
                    () => SwitchToMenuPreserveTime(RetinuePurchaseMenuId)),
                pauseGameActiveState);
        }

        /// <summary>
        /// Executes the actual purchase of soldiers.
        /// </summary>
        private void ExecutePurchase(string typeId, int count, int totalCost)
        {
            var manager = RetinueManager.Instance;

            if (manager == null)
            {
                ModLogger.Surfaced("CAMP", "ExecutePurchase: manager null", null, LogCtx.PlayerState());
                return;
            }

            // Deduct gold using GiveGoldAction (properly affects party treasury and updates UI)
            if (totalCost > 0 && Hero.MainHero != null)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, totalCost);
            }

            // Add soldiers
            if (manager.TryAddSoldiers(count, typeId, out var actuallyAdded, out var message))
            {
                ModLogger.Info(LogCategory, $"Purchased {actuallyAdded} {typeId} soldiers for {totalCost} gold");

                var successMsg = new TextObject("{=ct_purchase_success}{COUNT} soldiers have been mustered to your retinue.");
                _ = successMsg.SetTextVariable("COUNT", actuallyAdded);
                InformationManager.DisplayMessage(new InformationMessage(successMsg.ToString(), Colors.Green));
            }
            else
            {
                // Refund gold if failed
                if (totalCost > 0 && Hero.MainHero != null)
                {
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, totalCost);
                }
                ModLogger.Warn(LogCategory, $"Purchase failed: {message}");
                InformationManager.DisplayMessage(new InformationMessage(message, Colors.Red));
            }

            // Return to retinue menu
            SwitchToMenuPreserveTime(RetinueMenuId);
        }

        /// <summary>
        /// Shows confirmation dialog when changing soldier type with existing retinue.
        /// </summary>
        private void ShowTypeChangeConfirmation(string newTypeId)
        {
            var manager = RetinueManager.Instance;
            var currentCount = manager?.State?.TotalSoldiers ?? 0;

            var title = new TextObject("{=ct_type_change_title}Change Soldier Type");
            var message = new TextObject("{=ct_type_change_msg}You currently have {COUNT} soldiers. Changing type will dismiss them. Continue?");
            _ = message.SetTextVariable("COUNT", currentCount);

            // pauseGameActiveState = false so dialogs don't freeze game time
            var pauseGameActiveState = ShouldPauseDuringCampInquiry();
            InformationManager.ShowInquiry(
                new InquiryData(
                    title.ToString(),
                    message.ToString(),
                    true,
                    true,
                    new TextObject("{=ct_confirm_yes}Dismiss & Continue").ToString(),
                    new TextObject("{=ct_confirm_no}Cancel").ToString(),
                    () =>
                    {
                        // Dismiss current retinue
                        manager?.ClearRetinueTroops("type_change");
                        ModLogger.Info(LogCategory, "Dismissed retinue for type change");

                        var dismissMsg = new TextObject("{=ct_type_change_dismiss}Your current soldiers have been dismissed to make way for new recruits.");
                        InformationManager.DisplayMessage(new InformationMessage(dismissMsg.ToString()));

                        // Now proceed with new type purchase
                        OnSoldierTypePurchase(newTypeId);
                    },
                    () => SwitchToMenuPreserveTime(RetinuePurchaseMenuId)),
                pauseGameActiveState);
        }

        #endregion

        #region Retinue Dismiss Menu

        /// <summary>
        /// Creates the dismiss soldiers confirmation menu.
        /// </summary>
        private void AddRetinueDismissMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                RetinueDismissMenuId,
                "{RETINUE_DISMISS_TEXT}",
                OnRetinueDismissInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Confirm dismiss option
            starter.AddGameMenuOption(
                RetinueDismissMenuId,
                "ct_dismiss_confirm",
                "{=ct_dismiss_confirm_btn}Dismiss all soldiers",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Surrender;
                    return true;
                },
                _ => ExecuteDismiss(),
                false,
                1);

            // Cancel option
            starter.AddGameMenuOption(
                RetinueDismissMenuId,
                "ct_dismiss_cancel",
                "{=ct_dismiss_cancel}Keep my soldiers",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(RetinueMenuId),
                true,
                100);
        }

        /// <summary>
        /// Initializes the dismiss menu with clean formatting.
        /// </summary>
        private void OnRetinueDismissInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var manager = RetinueManager.Instance;
                var currentCount = manager?.State?.TotalSoldiers ?? 0;

                var sb = new StringBuilder();
                _ = sb.AppendLine();
                _ = sb.AppendLine("â€” Dismiss Soldiers â€”");
                _ = sb.AppendLine();
                _ = sb.AppendLine("Are you certain?");
                _ = sb.AppendLine();
                _ = sb.AppendLine($"Your {currentCount} soldiers will return to the army ranks.");
                _ = sb.AppendLine();
                _ = sb.AppendLine("This action cannot be undone.");

                MBTextManager.SetTextVariable("RETINUE_DISMISS_TEXT", sb.ToString());
                ModLogger.Debug(LogCategory, "Dismiss menu initialized");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing dismiss menu", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("RETINUE_DISMISS_TEXT",
                    new TextObject("{=enl_camp_error_retinue_dismiss}Error loading dismiss options.").ToString());
            }
        }

        /// <summary>
        /// Executes the dismissal of all retinue soldiers.
        /// </summary>
        private void ExecuteDismiss()
        {
            var manager = RetinueManager.Instance;
            var dismissedCount = manager?.State?.TotalSoldiers ?? 0;

            manager?.ClearRetinueTroops("player_dismiss");

            ModLogger.Info(LogCategory, $"Player dismissed {dismissedCount} soldiers");

            var msg = new TextObject("{=ct_dismiss_success}Your soldiers have been dismissed.");
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));

            SwitchToMenuPreserveTime(RetinueMenuId);
        }

        #endregion

        #region Requisition Menu

        /// <summary>
        /// Checks if the Request Reinforcements option should be available.
        /// Uses relation-based checks and shows appropriate tooltips.
        /// </summary>
        private static bool IsRequestReinforcementsAvailable(MenuCallbackArgs args)
        {
            var manager = RetinueManager.Instance;

            // Must have a retinue type selected
            if (manager?.State?.HasTypeSelected != true)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=ct_request_no_type}You must select a soldier type first.");
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                return true;
            }

            // Check relation with lord
            var relation = RetinueManager.GetLordRelation();
            if (relation < RetinueManager.MinRelationForRequest)
            {
                args.IsEnabled = false;
                var tooltip = new TextObject("{=ct_request_relation_low}Your lord will not spare soldiers for you. Improve your standing first (requires {REQ}+ relation).");
                _ = tooltip.SetTextVariable("REQ", RetinueManager.MinRelationForRequest);
                args.Tooltip = tooltip;
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                return true;
            }

            // Check cooldown
            if (!manager.IsReinforcementRequestAvailable())
            {
                args.IsEnabled = false;
                var days = manager.GetReinforcementRequestCooldownDays();
                var tooltip = new TextObject("{=ct_request_cooldown}Request on cooldown: {DAYS} days remaining.");
                _ = tooltip.SetTextVariable("DAYS", days);
                args.Tooltip = tooltip;
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                return true;
            }

            // Check if already at capacity
            var missing = manager.GetMissingSoldierCount();
            if (missing <= 0)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=ct_warn_full_retinue}Your retinue is at full strength.");
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                return true;
            }

            // Check affordability
            var cost = manager.CalculateReinforcementRequestCost();
            var playerGold = Hero.MainHero?.Gold ?? 0;
            if (playerGold < cost)
            {
                args.IsEnabled = false;
                var tooltip = new TextObject("{=ct_warn_cannot_afford}You cannot afford this ({COST} denars required).");
                _ = tooltip.SetTextVariable("COST", cost);
                args.Tooltip = tooltip;
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                return true;
            }

            // Show relation bonus info in tooltip
            if (relation >= RetinueManager.HighRelationThreshold)
            {
                args.Tooltip = new TextObject("{=ct_request_relation_high}Your lord is pleased with your service. Reduced cost and shorter cooldown.");
            }

            args.optionLeaveType = GameMenuOption.LeaveType.Trade;
            return true;
        }

        /// <summary>
        /// Creates the Requisition confirmation menu with cost breakdown.
        /// </summary>
        private void AddRetinueRequisitionMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                RetinueRequisitionMenuId,
                "{REQUISITION_MENU_TEXT}",
                OnRetinueRequisitionInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Confirm requisition option
            starter.AddGameMenuOption(
                RetinueRequisitionMenuId,
                "ct_requisition_confirm",
                "{REQUISITION_CONFIRM_TEXT}",
                args =>
                {
                    var manager = RetinueManager.Instance;
                    if (manager == null || !manager.CanRequisition(out _))
                    {
                        args.IsEnabled = false;
                        return true;
                    }

                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return true;
                },
                _ => ExecuteRequisition(),
                false,
                1);

            // Cancel option
            starter.AddGameMenuOption(
                RetinueRequisitionMenuId,
                "ct_requisition_cancel",
                "{=ct_requisition_cancel}Return without requisition",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(RetinueMenuId),
                true,
                100);
        }

        /// <summary>
        /// Initializes the reinforcement request menu with relation-based pricing.
        /// Shows lord dialogue that varies based on relationship level.
        /// </summary>
        private static void OnRetinueRequisitionInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var manager = RetinueManager.Instance;
                var enlistment = EnlistmentBehavior.Instance;
                var lord = enlistment?.EnlistedLord;
                var missing = manager?.GetMissingSoldierCount() ?? 0;
                var baseCost = manager?.CalculateRequisitionCost() ?? 0;
                var adjustedCost = manager?.CalculateReinforcementRequestCost() ?? 0;
                var playerGold = Hero.MainHero?.Gold ?? 0;
                var relation = RetinueManager.GetLordRelation();
                var cooldownDays = RetinueManager.GetRelationCooldownDays();
                var cooldownRemaining = manager?.GetReinforcementRequestCooldownDays() ?? 0;

                var sb = new StringBuilder();
                _ = sb.AppendLine();
                var lordName = lord?.Name?.ToString() ?? "your lord";
                _ = sb.AppendLine($"-- Request Reinforcements from {lordName} --");
                _ = sb.AppendLine();

                // Relation-based dialogue
                if (relation >= RetinueManager.HighRelationThreshold)
                {
                    _ = sb.AppendLine($"\"{Hero.MainHero?.Name}, your men fought well. I can spare some from the reserves.\"");
                    _ = sb.AppendLine();
                    _ = sb.AppendLine("(Lord is pleased - reduced cost, 7-day cooldown)");
                }
                else if (relation >= RetinueManager.MinRelationForRequest)
                {
                    _ = sb.AppendLine("\"Reinforcements? I'll see what I can do. It won't be free.\"");
                    _ = sb.AppendLine();
                    _ = sb.AppendLine("(Standard terms - full cost, 14-day cooldown)");
                }
                else
                {
                    _ = sb.AppendLine("\"You want soldiers? Prove your worth first.\"");
                    _ = sb.AppendLine();
                    _ = sb.AppendLine("(Relation too low - request unavailable)");
                }

                _ = sb.AppendLine();
                _ = sb.AppendLine($"Missing Soldiers: {missing}");
                _ = sb.AppendLine($"Cost: {adjustedCost}{{GOLD_ICON}}");

                if (adjustedCost < baseCost)
                {
                    _ = sb.AppendLine($"  (Reduced from {baseCost}{{GOLD_ICON}} due to lord's favor)");
                }

                _ = sb.AppendLine();
                _ = sb.AppendLine($"Your Gold: {playerGold}{{GOLD_ICON}}");
                _ = sb.AppendLine();

                _ = sb.AppendLine(cooldownRemaining > 0 ? $"Cooldown: {cooldownRemaining} days remaining" : "Request available now");

                _ = sb.AppendLine($"After request: {cooldownDays} day cooldown");

                MBTextManager.SetTextVariable("REQUISITION_MENU_TEXT", sb.ToString());

                // Set confirm button text with gold icon
                var confirmText = new TextObject("{=enl_camp_retinue_request_confirm}Request {COUNT} soldiers ({COST}{GOLD_ICON})");
                _ = confirmText.SetTextVariable("COUNT", missing);
                _ = confirmText.SetTextVariable("COST", adjustedCost);
                _ = confirmText.SetTextVariable("GOLD_ICON", "{GOLD_ICON}");
                MBTextManager.SetTextVariable("REQUISITION_CONFIRM_TEXT", confirmText.ToString());

                ModLogger.Debug(LogCategory, "Reinforcement request menu initialized");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing reinforcement request menu", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("REQUISITION_MENU_TEXT",
                    new TextObject("{=enl_camp_error_retinue_request}Error loading request details.").ToString());
                MBTextManager.SetTextVariable("REQUISITION_CONFIRM_TEXT",
                    new TextObject("{=enl_camp_retinue_request_confirm_short}Request Reinforcements").ToString());
            }
        }

        /// <summary>
        /// Executes the reinforcement request from the lord.
        /// </summary>
        private void ExecuteRequisition()
        {
            var manager = RetinueManager.Instance;
            if (manager == null)
            {
                ModLogger.Surfaced("CAMP", "ExecuteRequisition: manager null", null, LogCtx.PlayerState());
                SwitchToMenuPreserveTime(RetinueMenuId);
                return;
            }

            if (manager.TryRequestReinforcements(out var message))
            {
                var successMsg = new TextObject("{=ct_request_success}{MESSAGE}");
                _ = successMsg.SetTextVariable("MESSAGE", message);
                InformationManager.DisplayMessage(new InformationMessage(successMsg.ToString(), Colors.Green));

                ModLogger.Info(LogCategory, $"Reinforcement request complete: {message}");
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(message, Colors.Red));
                ModLogger.Warn(LogCategory, $"Reinforcement request failed: {message}");
            }

            SwitchToMenuPreserveTime(RetinueMenuId);
        }

        #endregion

        #region Companion Assignments Menu

        /// <summary>
        /// Creates the Companion Assignments submenu showing each companion with toggle options.
        /// </summary>
        private void AddCompanionAssignmentsMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                CompanionAssignmentsMenuId,
                "{COMPANION_ASSIGNMENTS_TEXT}",
                OnCompanionAssignmentsInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Dynamic companion options are added via menu init text since GameMenu doesn't support
            // truly dynamic options. Instead we show the list in the description and use toggle buttons.
            // Add 8 companion slots (should cover most parties)
            for (var i = 0; i < 8; i++)
            {
                var index = i;
                starter.AddGameMenuOption(
                    CompanionAssignmentsMenuId,
                    $"ct_companion_{i}",
                    $"{{COMPANION_{i}_TEXT}}",
                    args => IsCompanionSlotVisible(args, index),
                    _ => OnCompanionToggle(index),
                    false,
                    i + 1);
            }

            // Back option
            starter.AddGameMenuOption(
                CompanionAssignmentsMenuId,
                "ct_companions_back",
                "{=ct_back_tent}Back to Camp",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(CampHubMenuId),
                true,
                100);
        }

        // Cache companions for the current menu session to maintain consistent indices
        private List<Hero> _cachedCompanions;

        /// <summary>
        /// Initializes the companion assignments menu with current companion list.
        /// Uses clean formatting with status indicators.
        /// </summary>
        private void OnCompanionAssignmentsInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var manager = CompanionAssignmentManager.Instance;
                _cachedCompanions = manager?.GetAssignableCompanions() ?? new List<Hero>();

                var sb = new StringBuilder();
                _ = sb.AppendLine();
                var companionsTitle = new TextObject("{=ct_companions_title}Companion Assignments").ToString();
                _ = sb.AppendLine($"— {companionsTitle} —");
                _ = sb.AppendLine();

                var stayBackInfo = new TextObject("{=ct_companions_stay_back_info}Companions set to 'Stay Back' will not spawn in battle").ToString();
                var stayBackSafe = new TextObject("{=ct_companions_stay_back_safe}They remain safe, immune to death, wounds, or capture").ToString();
                _ = sb.AppendLine($"{stayBackInfo}.");
                _ = sb.AppendLine($"{stayBackSafe}.");
                _ = sb.AppendLine();

                if (_cachedCompanions.Count == 0 || manager == null)
                {
                    var noCompanionsText = new TextObject("{=ct_companions_none}No companions in your command").ToString();
                    _ = sb.AppendLine($"{noCompanionsText}.");
                }
                else
                {
                    var fightCount = manager.GetFightingCompanionCount();
                    var stayBackCount = manager.GetStayBackCompanionCount();
                    _ = sb.AppendLine($"Fighting: {fightCount}  |  Staying Back: {stayBackCount}");
                }

                MBTextManager.SetTextVariable("COMPANION_ASSIGNMENTS_TEXT", sb.ToString());

                // Set text variables for each companion slot with clear status indicators
                for (var i = 0; i < 8; i++)
                {
                    if (i < _cachedCompanions.Count && manager != null)
                    {
                        var companion = _cachedCompanions[i];
                        var shouldFight = manager.ShouldCompanionFight(companion);
                        var statusText = shouldFight
                            ? new TextObject("{=enl_camp_status_fight}[Fight]").ToString()
                            : new TextObject("{=enl_camp_status_stay_back}[Stay Back]").ToString();
                        MBTextManager.SetTextVariable($"COMPANION_{i}_TEXT", $"{companion.Name} {statusText}");
                    }
                    else
                    {
                        MBTextManager.SetTextVariable($"COMPANION_{i}_TEXT", string.Empty);
                    }
                }

                ModLogger.Debug(LogCategory, $"Companion assignments menu initialized with {_cachedCompanions.Count} companions");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("CAMP", "Error initializing companion assignments", ex, LogCtx.PlayerState());
                MBTextManager.SetTextVariable("COMPANION_ASSIGNMENTS_TEXT",
                    new TextObject("{=enl_camp_error_companion_assignments}Error loading companion data.").ToString());
            }
        }

        /// <summary>
        /// Checks if a companion slot should be visible based on cached companion list.
        /// </summary>
        private bool IsCompanionSlotVisible(MenuCallbackArgs args, int index)
        {
            if (_cachedCompanions == null || index >= _cachedCompanions.Count)
            {
                return false;
            }

            args.optionLeaveType = GameMenuOption.LeaveType.Continue;
            return true;
        }

        /// <summary>
        /// Toggles a companion's battle participation when their menu option is selected.
        /// </summary>
        private void OnCompanionToggle(int index)
        {
            if (_cachedCompanions == null || index >= _cachedCompanions.Count)
            {
                return;
            }

            var companion = _cachedCompanions[index];
            var manager = CompanionAssignmentManager.Instance;
            if (manager == null || companion == null)
            {
                return;
            }

            manager.ToggleCompanionParticipation(companion);
            var newStatus = manager.ShouldCompanionFight(companion) ? "Fight" : "Stay Back";

            var message = new TextObject("{=ct_companion_toggled}{COMPANION_NAME} set to: {STATUS}");
            _ = message.SetTextVariable("COMPANION_NAME", companion.Name);
            _ = message.SetTextVariable("STATUS", newStatus);
            InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

            ModLogger.Info(LogCategory, $"Toggled {companion.Name} to {newStatus}");

            // Refresh the menu
            SwitchToMenuPreserveTime(CompanionAssignmentsMenuId);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets the rank name for a given tier.
        /// </summary>
        private static string GetRankName(int tier)
        {
            return tier switch
            {
                1 => "Levy",
                2 => "Footman",
                3 => "Serjeant",
                4 => "Man-at-Arms",
                5 => "Banner Serjeant",
                6 => "Household Guard",
                _ => $"Tier {tier}"
            };
        }

        /// <summary>
        /// Gets the unit name for a tier (lance/squad/retinue).
        /// </summary>
        private static string GetUnitNameForTier(int tier)
        {
            return tier switch
            {
                >= 6 => "Retinue of Twenty",
                5 => "Squad of Ten",
                4 => "Lance of Five",
                _ => "None"
            };
        }

        /// <summary>
        /// Gets the display name for a soldier type, with faction-specific overrides.
        /// </summary>
        private static string GetSoldierTypeName(string typeId, CultureObject culture)
        {
            var cultureId = culture?.StringId?.ToLowerInvariant() ?? "";

            return typeId switch
            {
                "infantry" => "Men-at-Arms",
                "archers" => cultureId switch
                {
                    "vlandia" => "Crossbowmen",
                    "battania" => "Fian Archers",
                    _ => "Bowmen"
                },
                "cavalry" => "Mounted Lancers",
                "horse_archers" => cultureId switch
                {
                    "khuzait" => "Steppe Riders",
                    "aserai" => "Mameluke Cavalry",
                    _ => "Mounted Bowmen"
                },
                _ => typeId
            };
        }

        /// <summary>
        /// Gets available party space for new soldiers.
        /// </summary>
        private static int GetAvailablePartySpace()
        {
            var party = PartyBase.MainParty;
            if (party == null)
            {
                return 0;
            }

            return Math.Max(0, party.PartySizeLimit - party.NumberOfAllMembers);
        }

        /// <summary>
        /// Calculates the recruitment cost for one soldier of the given type.
        /// Uses the native PartyWageModel.GetTroopRecruitmentCost formula.
        /// </summary>
        private static int CalculateRecruitmentCost(string typeId, CultureObject culture, int playerTier)
        {
            // Get a representative troop to calculate cost
            var troops = RetinueManager.GetAvailableTroops(typeId, culture, playerTier);
            if (troops == null || troops.Count == 0)
            {
                // Fallback cost based on type
                return typeId switch
                {
                    "cavalry" or "horse_archers" => 200, // Mounted troops cost more
                    _ => 100
                };
            }

            // Calculate average cost across available troops
            var totalCost = 0;
            var count = 0;

            foreach (var troop in troops)
            {
                var cost = Campaign.Current?.Models?.PartyWageModel?
                    .GetTroopRecruitmentCost(troop, Hero.MainHero)
                    .RoundedResultNumber ?? 100;
                totalCost += cost;
                count++;
            }

            return count > 0 ? totalCost / count : 100;
        }

        #endregion

        #region PayTension Action Menus

        /// <summary>
        /// Creates the Desperate Measures menu - corruption path options.
        /// Options unlock at different PayTension thresholds.
        /// </summary>
        private void AddDesperateMeasuresMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                DesperateMeasuresMenuId,
                "{=dm_menu_intro}Desperate Measures\n\nWhen legitimate channels fail, some turn to darker paths. " +
                "Choose carefully - your reputation and honor are at stake.\n\n{DESPERATE_STATUS}",
                OnDesperateMeasuresInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Option 1: Bribe Paymaster's Clerk (40+ tension)
            starter.AddGameMenuOption(
                DesperateMeasuresMenuId,
                "dm_bribe_clerk",
                "{=dm_bribe_clerk}Bribe the Paymaster's Clerk (50{GOLD_ICON})",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tension = enlistment?.PayTension ?? 0;

                    if (tension < 40)
                    {
                        return false;
                    }

                    if (Hero.MainHero.Gold < 50)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=dm_no_gold}You don't have enough gold.");
                    }
                    else
                    {
                        args.Tooltip = new TextObject("{=dm_bribe_tooltip}Slip the clerk some coin to 'adjust' the pay records. Gain 20 gold but risk getting caught.");
                    }
                    return true;
                },
                _ => OnBribeClerk(),
                false, 1);

            // Option 2: Skim Supplies (40+ tension, Quartermaster or Armorer duty)
            starter.AddGameMenuOption(
                DesperateMeasuresMenuId,
                "dm_skim_supplies",
                "{=dm_skim_supplies}Skim Supplies from the Baggage Train",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tension = enlistment?.PayTension ?? 0;
                    var role = enlistment?.CurrentSpecialization ?? "Soldier";

                    if (tension < 40)
                    {
                        return false;
                    }

                    var isSupplyRole = role == "Quartermaster" || role == "Armorer";
                    if (!isSupplyRole)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=dm_wrong_duty}Only available for Quartermaster or Armorer role.");
                        return true;
                    }

                    args.Tooltip = new TextObject("{=dm_skim_tooltip}Divert some supplies for personal gain. Gain 30 gold worth of goods.");
                    return true;
                },
                _ => OnSkimSupplies(),
                false, 2);

            // Option 3: Find Black Market (50+ tension)
            starter.AddGameMenuOption(
                DesperateMeasuresMenuId,
                "dm_black_market",
                "{=dm_black_market}Find the Black Market",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tension = enlistment?.PayTension ?? 0;

                    if (tension < 50)
                    {
                        return false;
                    }

                    args.Tooltip = new TextObject("{=dm_black_market_tooltip}Seek out illicit traders for buying and selling goods.");
                    return true;
                },
                _ => OnFindBlackMarket(),
                false, 3);

            // Option 4: Sell Your Gear (60+ tension)
            starter.AddGameMenuOption(
                DesperateMeasuresMenuId,
                "dm_sell_gear",
                "{=dm_sell_gear}Sell Your Issued Equipment",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tension = enlistment?.PayTension ?? 0;

                    if (tension < 60)
                    {
                        return false;
                    }

                    args.Tooltip = new TextObject("{=dm_sell_gear_tooltip}Sell equipment you were issued. Risky - you'll need to replace it or face punishment.");
                    return true;
                },
                _ => OnSellIssuedGear(),
                false, 4);

            // Back option
            starter.AddGameMenuOption(
                DesperateMeasuresMenuId,
                "dm_back",
                "{=dm_back}Return to Camp",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(CampHubMenuId),
                true, 99);
        }

        /// <summary>
        /// Initializes the Desperate Measures menu.
        /// </summary>
        private void OnDesperateMeasuresInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var enlistment = EnlistmentBehavior.Instance;
                var tension = enlistment?.PayTension ?? 0;

                var status = $"Current PayTension: {tension}/100\n";
                status += tension >= 70 ? "The situation is critical. Desperate times call for desperate measures."
                        : tension >= 50 ? "The men are angry. Options are limited."
                        : "Things are getting difficult. Some are already bending the rules.";

                MBTextManager.SetTextVariable("DESPERATE_STATUS", status);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CAMP", "Error initializing Desperate Measures menu", ex);
            }
        }

        /// <summary>
        /// Creates the Help the Lord menu - loyalty path missions.
        /// Missions help reduce PayTension through legitimate means.
        /// </summary>
        private void AddHelpTheLordMenu(CampaignGameStarter starter)
        {
            starter.AddWaitGameMenu(
                HelpTheLordMenuId,
                "{=hlm_menu_intro}Help the Lord\n\nThe lord's coffers are running low, but loyal soldiers can help. " +
                "Volunteer for missions that bring coin to the treasury.\n\n{HELP_LORD_STATUS}",
                OnHelpTheLordInit,
                CampWaitCondition,
                CampWaitConsequence,
                CampWaitTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Option 1: Collect Debts (40+ tension)
            starter.AddGameMenuOption(
                HelpTheLordMenuId,
                "hlm_collect_debts",
                "{=hlm_collect_debts}Volunteer to Collect Debts",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Mission;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tension = enlistment?.PayTension ?? 0;

                    if (tension < 40)
                    {
                        return false;
                    }

                    args.Tooltip = new TextObject("{=hlm_debts_tooltip}Visit merchants who owe the lord money. Persuade them to pay. (-10 PayTension on success)");
                    return true;
                },
                _ => OnCollectDebts(),
                false, 1);

            // Option 2: Escort Merchant (50+ tension)
            starter.AddGameMenuOption(
                HelpTheLordMenuId,
                "hlm_escort_merchant",
                "{=hlm_escort_merchant}Escort a Merchant Caravan",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Mission;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tension = enlistment?.PayTension ?? 0;

                    if (tension < 50)
                    {
                        return false;
                    }

                    args.Tooltip = new TextObject("{=hlm_escort_tooltip}Guard a friendly merchant through dangerous territory. The lord takes a cut. (-15 PayTension)");
                    return true;
                },
                _ => OnEscortMerchant(),
                false, 2);

            // Option 3: Negotiate Loan (60+ tension, requires Trade/Charm skill)
            starter.AddGameMenuOption(
                HelpTheLordMenuId,
                "hlm_negotiate_loan",
                "{=hlm_negotiate_loan}Negotiate a Loan for the Lord",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tension = enlistment?.PayTension ?? 0;

                    if (tension < 60)
                    {
                        return false;
                    }

                    var tradeSkill = Hero.MainHero?.GetSkillValue(DefaultSkills.Trade) ?? 0;
                    if (tradeSkill < 50)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=hlm_loan_no_skill}Requires Trade skill 50+. Your skill: {SKILL}").SetTextVariable("SKILL", tradeSkill);
                        return true;
                    }

                    args.Tooltip = new TextObject("{=hlm_loan_tooltip}Use your trade connections to secure a loan. (-20 PayTension, requires Trade 50+)");
                    return true;
                },
                _ => OnNegotiateLoan(),
                false, 3);

            // Option 4: Volunteer for Raid (70+ tension, at war only)
            starter.AddGameMenuOption(
                HelpTheLordMenuId,
                "hlm_volunteer_raid",
                "{=hlm_volunteer_raid}Volunteer for a Raid",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.HostileAction;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tension = enlistment?.PayTension ?? 0;

                    if (tension < 70)
                    {
                        return false;
                    }

                    // Check if lord is at war with anyone - simplified check
                    var lordKingdom = enlistment?.CurrentLord?.MapFaction;
                    var atWar = lordKingdom != null && Campaign.Current?.Factions?
                        .Any(f => f != lordKingdom && lordKingdom.IsAtWarWith(f)) == true;

                    if (!atWar)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=hlm_no_war}Only available when the lord is at war.");
                        return true;
                    }

                    args.Tooltip = new TextObject("{=hlm_raid_tooltip}Lead a raid on enemy territory. Dangerous but lucrative. (-25 PayTension, risk of injury)");
                    return true;
                },
                _ => OnVolunteerRaid(),
                false, 4);

            // Back option
            starter.AddGameMenuOption(
                HelpTheLordMenuId,
                "hlm_back",
                "{=hlm_back}Return to Camp",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                _ => SwitchToMenuPreserveTime(CampHubMenuId),
                true, 99);
        }

        /// <summary>
        /// Initializes the Help the Lord menu.
        /// </summary>
        private void OnHelpTheLordInit(MenuCallbackArgs args)
        {
            try
            {
                // Start wait to enable time controls for the wait menu
                args.MenuContext.GameMenu.StartWait();

                // Unlock time control so player can change speed, then restore their prior state
                Campaign.Current.SetTimeControlModeLock(false);

                // Restore captured time using stoppable equivalents, preserving Stop when paused
                var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                Campaign.Current.TimeControlMode = normalized;

                var enlistment = EnlistmentBehavior.Instance;
                var tension = enlistment?.PayTension ?? 0;
                var lordName = enlistment?.CurrentLord?.Name?.ToString() ?? "the lord";

                var status = $"Current PayTension: {tension}/100\n";
                status += $"Lord {lordName} needs your help to restore the treasury.";

                MBTextManager.SetTextVariable("HELP_LORD_STATUS", status);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CAMP", "Error initializing Help The Lord menu", ex);
            }
        }

        // ========================================
        // DESPERATE MEASURES CONSEQUENCES
        // ========================================

        private void OnBribeClerk()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            // Pay the bribe
            hero.ChangeHeroGold(-50);

            // Roll for success/caught
            var successChance = 70; // 70% chance of success
            var roll = MBRandom.RandomInt(100);

            if (roll < successChance)
            {
                // Success - gain more than you paid
                hero.ChangeHeroGold(70);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=dm_bribe_success}The clerk adjusts the records in your favor. You gain 20 gold net.").ToString(),
                    Colors.Green));
                ModLogger.Info(LogCategory, "Bribe clerk succeeded");
            }
            else
            {
                // Caught - lose the bribe and relationship
                enlistment.ModifyQuartermasterRelationship(-10);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=dm_bribe_caught}The clerk takes your money... then reports you. Your reputation suffers.").ToString(),
                    Colors.Red));
                ModLogger.Info(LogCategory, "Bribe clerk failed - caught");
            }

            SwitchToMenuPreserveTime(DesperateMeasuresMenuId);
        }

        private void OnSkimSupplies()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            // Gain supplies (as gold equivalent)
            hero.ChangeHeroGold(30);

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=dm_skim_success}You quietly divert some supplies for yourself. +30 gold worth of goods.").ToString(),
                Colors.Yellow));

            // Small reputation cost if quartermaster relationship is low
            if (enlistment.QuartermasterRelationship < 40)
            {
                enlistment.ModifyQuartermasterRelationship(-5);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=dm_skim_suspicious}The quartermaster seems suspicious...").ToString(),
                    Colors.Gray));
            }

            ModLogger.Info(LogCategory, "Skimmed supplies");
            SwitchToMenuPreserveTime(DesperateMeasuresMenuId);
        }

        private void OnFindBlackMarket()
        {
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=dm_black_market_contact}You make contact with some... entrepreneurial traders. They'll be around the camp from time to time.").ToString(),
                Colors.Magenta));

            ModLogger.Info(LogCategory, "Found black market");
            SwitchToMenuPreserveTime(DesperateMeasuresMenuId);
        }

        private void OnSellIssuedGear()
        {
            // Gain gold based on current tier
            var enlistment = EnlistmentBehavior.Instance;
            var tier = enlistment?.EnlistmentTier ?? 1;
            var goldGain = tier * 25; // 25-225 gold based on tier

            Hero.MainHero.ChangeHeroGold(goldGain);

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=dm_sell_gear_success}You sell some of your issued equipment for {GOLD} gold. You'll need to replace it...")
                    .SetTextVariable("GOLD", goldGain).ToString(),
                Colors.Yellow));

            ModLogger.Info(LogCategory, $"Sold issued gear for {goldGain}g");
            SwitchToMenuPreserveTime(DesperateMeasuresMenuId);
        }

        // ========================================
        // HELP THE LORD CONSEQUENCES
        // ========================================

        private void OnCollectDebts()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            // Simple skill check based on Charm
            var charm = Hero.MainHero?.GetSkillValue(DefaultSkills.Charm) ?? 0;
            var successChance = 50 + (charm / 5); // 50-90% based on charm
            var roll = MBRandom.RandomInt(100);

            if (roll < successChance)
            {
                // Reduce PayTension
                ReducePayTension(10);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=hlm_debts_success}You successfully collect the debts. The lord is pleased. (-10 PayTension)").ToString(),
                    Colors.Green));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=hlm_debts_fail}The merchants refuse to pay. Perhaps they need more... persuasion.").ToString(),
                    Colors.Yellow));
            }

            ModLogger.Info(LogCategory, "Collect debts mission");
            SwitchToMenuPreserveTime(HelpTheLordMenuId);
        }

        private void OnEscortMerchant()
        {
            // Always succeeds - reduce PayTension
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            // Reduce PayTension
            ReducePayTension(15);

            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=hlm_escort_success}You escort the merchant safely. The lord's coffers grow. (-15 PayTension)").ToString(),
                Colors.Green));

            ModLogger.Info(LogCategory, "Escort merchant mission");
            SwitchToMenuPreserveTime(HelpTheLordMenuId);
        }

        private void OnNegotiateLoan()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            // Skill check
            var trade = Hero.MainHero?.GetSkillValue(DefaultSkills.Trade) ?? 0;
            var successChance = trade; // Direct skill percentage
            var roll = MBRandom.RandomInt(100);

            if (roll < successChance)
            {
                ReducePayTension(20);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=hlm_loan_success}You secure a favorable loan for the lord. The treasury is replenished. (-20 PayTension)").ToString(),
                    Colors.Green));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=hlm_loan_fail}The bankers aren't interested. Perhaps another approach...").ToString(),
                    Colors.Yellow));
            }

            ModLogger.Info(LogCategory, "Negotiate loan mission");
            SwitchToMenuPreserveTime(HelpTheLordMenuId);
        }

        private void OnVolunteerRaid()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            // Risky mission - chance of injury
            var combatSkill = hero.GetSkillValue(DefaultSkills.OneHanded) + hero.GetSkillValue(DefaultSkills.TwoHanded);
            var injuryChance = Math.Max(10, 50 - (combatSkill / 10)); // 10-50% based on skill
            var roll = MBRandom.RandomInt(100);

            if (roll < injuryChance)
            {
                // Injured
                var damage = MBRandom.RandomInt(20, 50);
                hero.HitPoints = Math.Max(1, hero.HitPoints - damage);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=hlm_raid_wounded}The raid succeeds but you're wounded! (-{DAMAGE} HP, -25 PayTension)")
                        .SetTextVariable("DAMAGE", damage).ToString(),
                    Colors.Yellow));
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=hlm_raid_success}The raid is a complete success! Valuable loot is captured. (-25 PayTension)").ToString(),
                    Colors.Green));
            }

            // Always reduces tension significantly
            ReducePayTension(25);

            // Gain some gold personally
            hero.ChangeHeroGold(50);

            ModLogger.Info(LogCategory, "Volunteer raid mission");
            SwitchToMenuPreserveTime(HelpTheLordMenuId);
        }

        /// <summary>
        /// Reduces the current PayTension by the specified amount.
        /// </summary>
        private static void ReducePayTension(int amount)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            enlistment.ReducePayTension(amount);
        }

        #endregion

        /// <summary>
        /// Determine if the game should pause during Camp inquiries.
        /// </summary>
        private bool ShouldPauseDuringCampInquiry()
        {
            // Inquiries should not pause the game time to keep simulation running.
            return false;
        }
    }
}

