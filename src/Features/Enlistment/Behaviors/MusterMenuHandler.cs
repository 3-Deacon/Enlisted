using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Enlisted.Features.Context;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Equipment.Managers;
using Enlisted.Features.Equipment.UI;
using Enlisted.Features.Escalation;
using Enlisted.Features.Ranks.Behaviors;
using Enlisted.Features.Identity;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Features.Logistics;
using Enlisted.Features.Retinue.Core;
using Enlisted.Features.Retinue.Data;
using Enlisted.Mod.Core.Config;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Entry;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Enlistment.Behaviors
{
    /// <summary>
    /// Handles the multi-stage muster menu sequence that guides players through pay day,
    /// inspections, period summaries, and transitions to post-muster activities.
    /// Replaces the simple inquiry popup with a comprehensive GameMenu flow.
    /// </summary>
    public sealed class MusterMenuHandler : CampaignBehaviorBase
    {
        private const string LogCategory = "Muster";

        // Menu IDs for the 6 muster stages
        private const string MusterIntroMenuId = "enlisted_muster_intro";
        private const string MusterPayMenuId = "enlisted_muster_pay";
        private const string MusterRecruitMenuId = "enlisted_muster_recruit";
        private const string MusterPromotionRecapMenuId = "enlisted_muster_promotion_recap";
        private const string MusterRetinueMenuId = "enlisted_muster_retinue";
        private const string MusterCompleteMenuId = "enlisted_muster_complete";

        /// <summary>
        /// Singleton instance for accessing the muster menu handler.
        /// </summary>
        public static MusterMenuHandler Instance { get; private set; }

        /// <summary>
        /// Current muster session state. Non-null only while a muster sequence is active.
        /// </summary>
        private MusterSessionState _currentMuster;

        /// <summary>
        /// Flag set when muster is pending but player is in combat.
        /// Muster will trigger when combat ends.
        /// </summary>
        private bool _musterPendingAfterCombat;

        /// <summary>
        /// Flag set when muster was attempted but player was in another menu.
        /// Muster will trigger when player exits to map.
        /// </summary>
        private bool _musterPendingAfterMenu;

        /// <summary>
        /// Tracks the last muster trigger attempt time to prevent rapid retries.
        /// </summary>
        private CampaignTime _lastMusterTriggerAttempt = CampaignTime.Zero;

        /// <summary>
        /// Flag set when QM conversation is opened from muster menu.
        /// Used to return to enlisted_status menu after conversation ends.
        /// </summary>
        private bool _qmOpenedFromMuster;

        /// <summary>
        /// Tracks muster session data including outcomes, progression, and post-muster flags.
        /// Created fresh at the start of each muster and cleared when muster completes.
        /// </summary>
        public class MusterSessionState
        {
            // Flow tracking (essential for save/load)
            /// <summary>Current menu stage ID for resume.</summary>
            public string CurrentStage { get; set; }

            /// <summary>Day muster started.</summary>
            public int MusterDay { get; set; }

            /// <summary>Previous muster for period calculation.</summary>
            public int LastMusterDay { get; set; }

            // Strategic context (for intro flavor text)
            /// <summary>Strategic context from ArmyContextAnalyzer.</summary>
            public string StrategicContext { get; set; }

            // Pay stage outcomes
            /// <summary>Amount of pay received.</summary>
            public int PayReceived { get; set; }

            /// <summary>Pay outcome type: "full", "partial", "iou", "corruption", "qm_deal".</summary>
            public string PayOutcome { get; set; }

            /// <summary>Ration outcome: "issued", "none", "officer_exempt".</summary>
            public string RationOutcome { get; set; }

            // Promotion tracking (promotion occurs via PromotionBehavior, recap shows at muster)
            /// <summary>True if tier changed since last muster.</summary>
            public bool PromotionOccurredThisPeriod { get; set; }

            /// <summary>Tier at last muster.</summary>
            public int PreviousTier { get; set; }

            /// <summary>Tier at this muster.</summary>
            public int CurrentTier { get; set; }

            /// <summary>Campaign day when promotion occurred.</summary>
            public int PromotionDay { get; set; }

            // Retinue (T7+ only)
            /// <summary>Current retinue soldier count.</summary>
            public int RetinueStrength { get; set; }

            /// <summary>Maximum retinue capacity.</summary>
            public int RetinueCapacity { get; set; }

            /// <summary>Retinue casualties this period.</summary>
            public int RetinueCasualties { get; set; }

            /// <summary>Names of fallen soldiers this period.</summary>
            public List<string> FallenRetinueNames { get; set; } = new List<string>();

            // Post-muster flags
            /// <summary>If true, open QM conversation after muster.</summary>
            public bool VisitQMAfter { get; set; }

            /// <summary>If true, open retinue recruitment after muster.</summary>
            public bool RecruitRetinueAfter { get; set; }

            /// <summary>If true, request temporary leave after muster.</summary>
            public bool RequestLeaveAfter { get; set; }

            // Escalation tracking (for edge case handling)
            /// <summary>Whether high scrutiny warning should display.</summary>
            public bool HighScrutinyWarning { get; set; }

            /// <summary>Whether discharge threshold was reached during muster.</summary>
            public bool DischargeThresholdReached { get; set; }

            /// <summary>Events queued to fire after muster completes.</summary>
            public List<string> PendingEscalationEvents { get; set; } = new List<string>();

            // Error tracking
            /// <summary>Errors encountered during muster that couldn't halt the flow.</summary>
            public List<string> EncounteredErrors { get; set; } = new List<string>();

            /// <summary>Whether any effects failed to apply.</summary>
            public bool EffectsPartiallyFailed { get; set; }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            Instance = this;
            ModLogger.Info(LogCategory, "MusterMenuHandler registered");
        }

        /// <summary>
        /// Hourly tick handler to check for deferred muster triggers.
        /// When muster was deferred due to player being in combat, conversation, or another menu,
        /// this retries the trigger once conditions allow.
        /// </summary>
        private void OnHourlyTick()
        {
            // Only check if we're enlisted and have a pending deferred muster
            if (EnlistmentBehavior.Instance?.IsEnlisted != true)
            {
                return;
            }

            CheckPendingMusterTrigger();
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            if (_qmOpenedFromMuster)
            {
                _qmOpenedFromMuster = false;
                ModLogger.Debug(LogCategory, "QM conversation ended, returning to enlisted_status menu");
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    if (EnlistmentBehavior.Instance?.IsEnlisted == true)
                    {
                        // Use SafeActivateEnlistedMenu to respect siege detection
                        EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                    }
                });
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Persist muster-related flags for edge case handling
            dataStore.SyncData("_musterPendingAfterCombat", ref _musterPendingAfterCombat);
            dataStore.SyncData("_musterPendingAfterMenu", ref _musterPendingAfterMenu);

            // Muster session state persistence for mid-muster save/load
            var hasMusterInProgress = _currentMuster != null;
            dataStore.SyncData("_hasMusterInProgress", ref hasMusterInProgress);

            if (dataStore.IsSaving)
            {
                // Save current muster state if in progress
                if (_currentMuster != null)
                {
                    var currentStage = _currentMuster.CurrentStage ?? "";
                    var musterDay = _currentMuster.MusterDay;
                    var lastMusterDay = _currentMuster.LastMusterDay;
                    var strategicContext = _currentMuster.StrategicContext ?? "";
                    var payReceived = _currentMuster.PayReceived;
                    var payOutcome = _currentMuster.PayOutcome ?? "";
                    var promotionOccurred = _currentMuster.PromotionOccurredThisPeriod;
                    var previousTier = _currentMuster.PreviousTier;
                    var currentTier = _currentMuster.CurrentTier;
                    var highScrutiny = _currentMuster.HighScrutinyWarning;
                    var visitQM = _currentMuster.VisitQMAfter;

                    dataStore.SyncData("_muster_currentStage", ref currentStage);
                    dataStore.SyncData("_muster_musterDay", ref musterDay);
                    dataStore.SyncData("_muster_lastMusterDay", ref lastMusterDay);
                    dataStore.SyncData("_muster_strategicContext", ref strategicContext);
                    dataStore.SyncData("_muster_payReceived", ref payReceived);
                    dataStore.SyncData("_muster_payOutcome", ref payOutcome);
                    dataStore.SyncData("_muster_promotionOccurred", ref promotionOccurred);
                    dataStore.SyncData("_muster_previousTier", ref previousTier);
                    dataStore.SyncData("_muster_currentTier", ref currentTier);
                    dataStore.SyncData("_muster_highScrutiny", ref highScrutiny);
                    dataStore.SyncData("_muster_visitQM", ref visitQM);

                    ModLogger.Info(LogCategory, $"Saved muster state at stage {currentStage}");
                }
            }
            else
            {
                // Loading - restore muster state if one was in progress
                if (hasMusterInProgress)
                {
                    try
                    {
                        var currentStage = "";
                        var musterDay = 0;
                        var lastMusterDay = 0;
                        var strategicContext = "";
                        var payReceived = 0;
                        var payOutcome = "";
                        var promotionOccurred = false;
                        var previousTier = 0;
                        var currentTier = 0;
                        var highScrutiny = false;
                        var visitQM = false;

                        dataStore.SyncData("_muster_currentStage", ref currentStage);
                        dataStore.SyncData("_muster_musterDay", ref musterDay);
                        dataStore.SyncData("_muster_lastMusterDay", ref lastMusterDay);
                        dataStore.SyncData("_muster_strategicContext", ref strategicContext);
                        dataStore.SyncData("_muster_payReceived", ref payReceived);
                        dataStore.SyncData("_muster_payOutcome", ref payOutcome);
                        dataStore.SyncData("_muster_promotionOccurred", ref promotionOccurred);
                        dataStore.SyncData("_muster_previousTier", ref previousTier);
                        dataStore.SyncData("_muster_currentTier", ref currentTier);
                        dataStore.SyncData("_muster_highScrutiny", ref highScrutiny);
                        dataStore.SyncData("_muster_visitQM", ref visitQM);

                        // Validate restored state
                        if (string.IsNullOrEmpty(currentStage) || !IsValidMusterStage(currentStage))
                        {
                            ModLogger.Expected(LogCategory, "muster_corrupt_state",
                                $"Corrupted muster state on load (stage={currentStage}), aborting muster");
                            _currentMuster = null;
                            EnlistmentBehavior.Instance?.DeferPayMuster();
                        }
                        else
                        {
                            // Restore muster session
                            _currentMuster = new MusterSessionState
                            {
                                CurrentStage = currentStage,
                                MusterDay = musterDay,
                                LastMusterDay = lastMusterDay,
                                StrategicContext = strategicContext,
                                PayReceived = payReceived,
                                PayOutcome = payOutcome,
                                PromotionOccurredThisPeriod = promotionOccurred,
                                PreviousTier = previousTier,
                                CurrentTier = currentTier,
                                HighScrutinyWarning = highScrutiny,
                                VisitQMAfter = visitQM
                            };

                            ModLogger.Info(LogCategory, $"Restored muster state, will resume at stage {currentStage}");

                            // Queue resume for after load completes
                            _musterPendingAfterMenu = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught(LogCategory, "Failed to restore muster state", ex);
                        _currentMuster = null;
                        EnlistmentBehavior.Instance?.DeferPayMuster();
                    }
                }
            }
        }

        /// <summary>
        /// Validates that a stage ID is a known muster menu stage.
        /// </summary>
        private bool IsValidMusterStage(string stageId)
        {
            return stageId == MusterIntroMenuId ||
                   stageId == MusterPayMenuId ||
                   stageId == MusterPromotionRecapMenuId ||
                   stageId == MusterRetinueMenuId ||
                   stageId == MusterCompleteMenuId;
        }

        /// <summary>
        /// Tracks whether muster menu registration succeeded.
        /// If false, BeginMusterSequence will fall back to legacy inquiry.
        /// </summary>
        private bool _menusRegisteredSuccessfully;

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (starter == null)
            {
                ModLogger.Error(LogCategory, "OnSessionLaunched called with null CampaignGameStarter");
                _menusRegisteredSuccessfully = false;
                return;
            }

            try
            {
                RegisterMusterMenus(starter);
                _menusRegisteredSuccessfully = true;
                ModLogger.Info(LogCategory, "Muster menus registered successfully");
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Failed to register muster menus, will use legacy fallback", ex);
                _menusRegisteredSuccessfully = false;
            }
        }

        /// <summary>
        /// Registers all 6 muster menu stages with the game starter.
        /// Each menu is a wait menu that hides progress bars and shows formatted text.
        /// </summary>
        private void RegisterMusterMenus(CampaignGameStarter starter)
        {
            // 1. Muster Intro Menu
            starter.AddWaitGameMenu(MusterIntroMenuId,
                "{=muster_intro_title}PAY MUSTER - DAY {MUSTER_DAY}\n{MUSTER_INTRO_TEXT}",
                OnMusterIntroInit,
                OnMusterMenuCondition,
                null,
                OnMusterMenuTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Intro options
            starter.AddGameMenuOption(MusterIntroMenuId, "muster_intro_continue",
                "{=muster_continue}Proceed to Pay Line",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    args.Tooltip = new TextObject("{=muster_continue_tt}Step forward to receive wages.");
                    return true;
                },
                _ => GameMenu.SwitchToMenu(MusterPayMenuId),
                false, 1);

            // 2. Pay Line Menu
            starter.AddWaitGameMenu(MusterPayMenuId,
                "{=muster_pay_title}PAYMASTER'S LINE\n{MUSTER_PAY_TEXT}",
                OnMusterPayInit,
                OnMusterMenuCondition,
                null,
                OnMusterMenuTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            // Pay option 1: Accept Your Pay (standard payment)
            starter.AddGameMenuOption(MusterPayMenuId, "muster_pay_accept",
                "{=muster_pay_accept}Accept Your Pay ({PAY_AMOUNT} denars)",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    args.Tooltip = new TextObject("{=muster_pay_accept_tt}Receive wages owed. Lord's finances determine full/partial payment.");
                    return true;
                },
                _ => ResolvePayMusterStandard(),
                false, 1);

            // Pay option 2: Demand a Recount (corruption)
            starter.AddGameMenuOption(MusterPayMenuId, "muster_pay_recount",
                "{=muster_pay_recount}Demand a Recount",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Bribe;
                    args.Tooltip = new TextObject("{=muster_pay_recount_tt}Roguery/Charm check. 120% pay on success.");
                    return true;
                },
                _ => ResolveCorruptionMuster(),
                false, 2);

            // Pay option 3: Quartermaster's Deal (equipment lottery)
            starter.AddGameMenuOption(MusterPayMenuId, "muster_pay_side_deal",
                "{=muster_pay_side_deal}Quartermaster's Deal",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    args.Tooltip = new TextObject("{=muster_pay_side_deal_tt}Take 40% pay for surplus equipment. 70% chance (your tier).");
                    return true;
                },
                _ => ResolveSideDealMuster(),
                false, 3);

            // Pay option 4: Accept Payment in Arrears
            starter.AddGameMenuOption(MusterPayMenuId, "muster_pay_iou",
                "{=muster_pay_iou}Accept Payment in Arrears",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    args.Tooltip = new TextObject("{=muster_pay_iou_tt}Accept delayed payment. Wages remain owed. Paid at next muster.");
                    var enlistment = EnlistmentBehavior.Instance;
                    if (enlistment == null) return false;

                    // Only available when lord cannot afford to pay
                    var totalOwed = enlistment.PendingMusterPay + enlistment.OwedBackpay;
                    var lordCanPay = enlistment.EnlistedLord?.Gold >= totalOwed + 500; // 500 denar buffer
                    return !lordCanPay && totalOwed > 0;
                },
                _ => ResolvePromissoryMuster(),
                false, 4);

            // Pay option 5: Take Your Final Pay (honorable discharge)
            starter.AddGameMenuOption(MusterPayMenuId, "muster_pay_discharge",
                "{=muster_pay_discharge}Take Your Final Pay",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    args.Tooltip = new TextObject("{=muster_pay_discharge_tt}Process discharge. Receive final pay + pension. Service ends.");
                    var enlistment = EnlistmentBehavior.Instance;
                    return enlistment != null && enlistment.IsPendingDischarge;
                },
                _ => ShowFinalPayDischargeConfirmation(),
                false, 5);

            // Pay option 6: Slip Away in the Night (deserter)
            starter.AddGameMenuOption(MusterPayMenuId, "muster_pay_smuggle",
                "{=muster_pay_smuggle}Slip Away in the Night",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    args.Tooltip = new TextObject("{=muster_pay_smuggle_tt}Desert with your gear. Forfeit pension. Hunted as a deserter.");
                    var enlistment = EnlistmentBehavior.Instance;
                    return enlistment != null && enlistment.IsPendingDischarge;
                },
                _ => ResolveSmuggleDischarge(),
                false, 6);

            // 3. Promotion Recap Menu
            starter.AddWaitGameMenu(MusterPromotionRecapMenuId,
                "{=muster_promotion_title}PROMOTION ACKNOWLEDGED\n{MUSTER_PROMOTION_TEXT}",
                OnPromotionInit,
                OnMusterMenuCondition,
                null,
                OnMusterMenuTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            starter.AddGameMenuOption(MusterPromotionRecapMenuId, "muster_promotion_continue",
                "{=muster_promotion_continue}Acknowledge Promotion",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    args.Tooltip = new TextObject("{=muster_promotion_continue_tt}Proceed with muster.");
                    return true;
                },
                _ => ProceedToNextStageFromPromotion(),
                false, 1);

            starter.AddGameMenuOption(MusterPromotionRecapMenuId, "muster_promotion_visit_qm",
                "{=muster_promotion_visit_qm}Visit Quartermaster Now",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    args.Tooltip = new TextObject("{=muster_promotion_visit_qm_tt}Review newly unlocked equipment. Will return to muster after.");
                    return true;
                },
                _ =>
                {
                    if (_currentMuster != null)
                    {
                        _currentMuster.VisitQMAfter = true;
                        ModLogger.Info(LogCategory, "Player flagged QM visit from promotion stage");
                    }
                    ProceedToNextStageFromPromotion();
                },
                false, 2);

            // 7. Retinue Muster Menu (T7+ only)
            starter.AddWaitGameMenu(MusterRetinueMenuId,
                "{=muster_retinue_title}RETINUE MUSTER\n{MUSTER_RETINUE_TEXT}",
                OnRetinueInit,
                OnMusterMenuCondition,
                null,
                OnMusterMenuTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            starter.AddGameMenuOption(MusterRetinueMenuId, "muster_retinue_continue",
                "{=muster_retinue_continue}Dismiss Retinue",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    args.Tooltip = new TextObject("{=muster_retinue_continue_tt}Proceed to muster summary.");
                    return true;
                },
                _ => GameMenu.SwitchToMenu(MusterCompleteMenuId),
                false, 1);

            starter.AddGameMenuOption(MusterRetinueMenuId, "muster_retinue_recruit",
                "{=muster_retinue_recruit}Seek New Soldiers",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.TroopSelection;
                    args.Tooltip = new TextObject("{=muster_retinue_recruit_tt}Open recruitment after muster. Fill retinue openings.");
                    // Only available if retinue has openings
                    return _currentMuster != null && _currentMuster.RetinueStrength < _currentMuster.RetinueCapacity;
                },
                _ =>
                {
                    if (_currentMuster != null)
                    {
                        _currentMuster.RecruitRetinueAfter = true;
                    }
                    GameMenu.SwitchToMenu(MusterCompleteMenuId);
                },
                false, 2);

            // 8. Muster Complete Menu
            starter.AddWaitGameMenu(MusterCompleteMenuId,
                "{=muster_complete_title}MUSTER COMPLETE - DAY {MUSTER_DAY}\n{MUSTER_COMPLETE_TEXT}",
                OnMusterCompleteInit,
                OnMusterMenuCondition,
                null,
                OnMusterMenuTick,
                GameMenu.MenuAndOptionType.WaitMenuHideProgressAndHoursOption);

            starter.AddGameMenuOption(MusterCompleteMenuId, "muster_complete_dismiss",
                "{=muster_complete_dismiss}Dismiss",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    args.Tooltip = new TextObject("{=muster_complete_dismiss_tt}Return to map. Muster complete.");
                    return true;
                },
                _ => CompleteMusterSequence(),
                false, 1);

            starter.AddGameMenuOption(MusterCompleteMenuId, "muster_complete_qm",
                "{=muster_complete_qm}Visit Quartermaster",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    var supply = CompanySupplyManager.Instance?.TotalSupply ?? 100;
                    if (supply < 15)
                    {
                        args.Tooltip = new TextObject("{=muster_complete_qm_blocked_tt}Quartermaster unavailable. Supplies critically low. Equipment requisitions suspended.");
                        args.IsEnabled = false;
                    }
                    else
                    {
                        args.Tooltip = new TextObject("{=muster_complete_qm_tt}Browse newly refreshed equipment stock and access your baggage.");
                    }
                    return true;
                },
                _ =>
                {
                    if (_currentMuster != null)
                    {
                        _currentMuster.VisitQMAfter = true;
                    }
                    CompleteMusterSequence();
                },
                false, 2);

            starter.AddGameMenuOption(MusterCompleteMenuId, "muster_complete_leave",
                "{=muster_complete_leave}Request Leave of Absence",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                    args.Tooltip = new TextObject("{=muster_complete_leave_tt}Request leave of absence. 14-day limit.");
                    // Disabled during combat/siege
                    var enlistment = EnlistmentBehavior.Instance;
                    if (enlistment?.EnlistedLord?.PartyBelongedTo?.BesiegerCamp != null ||
                        enlistment?.EnlistedLord?.PartyBelongedTo?.MapEvent != null)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=muster_complete_leave_blocked_tt}Cannot request leave during active siege or combat.");
                    }
                    return true;
                },
                _ =>
                {
                    if (_currentMuster != null)
                    {
                        _currentMuster.RequestLeaveAfter = true;
                    }
                    CompleteMusterSequence();
                },
                false, 4);

            // Request Retirement option (greyed out until 252+ days)
            starter.AddGameMenuOption(MusterCompleteMenuId, "muster_complete_retire",
                "{=muster_complete_retire}Request Retirement",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    
                    var enlistment = EnlistmentBehavior.Instance;
                    if (enlistment == null || !enlistment.IsEnlisted)
                    {
                        return false;
                    }

                    // Only enable if eligible for retirement (252+ days)
                    if (enlistment.IsEligibleForRetirement)
                    {
                        args.Tooltip = new TextObject(
                            "{=muster_complete_retire_tt}Complete your term with honor. Receive severance pay and pension. Positive relations.");
                        return true;
                    }
                    else
                    {
                        // Show why they can't retire yet
                        var daysServed = (int)enlistment.DaysServed;
                        var daysRemaining = 252 - daysServed;
                        
                        var tooltip = new TextObject("{=muster_complete_retire_blocked_tt}Retirement requires 252 days of service. You have served {DAYS_SERVED} days ({DAYS_REMAINING} days remaining).");
                        tooltip.SetTextVariable("DAYS_SERVED", daysServed);
                        tooltip.SetTextVariable("DAYS_REMAINING", daysRemaining);
                        args.Tooltip = tooltip;
                        args.IsEnabled = false;
                        return true;
                    }
                },
                _ =>
                {
                    var enlistment = EnlistmentBehavior.Instance;
                    if (enlistment != null && enlistment.IsEnlisted)
                    {
                        enlistment.RequestDischarge();
                        
                        if (_currentMuster != null)
                        {
                            _currentMuster.PayOutcome = "final_discharge";
                        }
                        
                        FinalizePendingDischarge();
                    }
                    else
                    {
                        CompleteMusterSequence();
                    }
                },
                false, 5);

            // Request Discharge option (always enabled, shows expected outcome)
            starter.AddGameMenuOption(MusterCompleteMenuId, "muster_complete_discharge",
                "{=muster_complete_discharge}Request Discharge",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Manage;
                    
                    var enlistment = EnlistmentBehavior.Instance;
                    if (enlistment == null || !enlistment.IsEnlisted)
                    {
                        return false;
                    }

                    // Calculate expected discharge band and show in tooltip
                    var daysServed = (int)enlistment.DaysServed;
                    var negativeRelation = enlistment.CurrentLord != null && 
                                          Hero.MainHero.GetRelation(enlistment.CurrentLord) < 0;
                    
                    // Determine expected discharge band
                    string band;
                    if (negativeRelation)
                    {
                        band = "washout";
                    }
                    else if (daysServed >= 200)
                    {
                        band = enlistment.EnlistmentTier >= 4 ? "heroic" : "veteran";
                    }
                    else if (daysServed >= 100)
                    {
                        band = "honorable";
                    }
                    else
                    {
                        band = "washout";
                    }
                    
                    // Set tooltip based on expected discharge band
                    var tooltipText = band switch
                    {
                        "heroic" or "veteran" => 
                            "{=muster_complete_discharge_veteran_tt}Honorable discharge. Receive severance (3000g) and pension. +30 relation with lord.",
                        "honorable" => 
                            "{=muster_complete_discharge_honorable_tt}Honorable discharge. Receive severance (3000g) and pension. +10 relation with lord.",
                        _ => 
                            "{=muster_complete_discharge_washout_tt}Early discharge (washout). No severance or pension. -10 relation. Gear confiscated."
                    };
                    
                    args.Tooltip = new TextObject(tooltipText);
                    return true;
                },
                _ =>
                {
                    var enlistment = EnlistmentBehavior.Instance;
                    if (enlistment != null && enlistment.IsEnlisted)
                    {
                        ShowDischargeConfirmation();
                    }
                    else
                    {
                        CompleteMusterSequence();
                    }
                },
                false, 6);

            ModLogger.Debug(LogCategory, "All 6 muster menu stages registered");
        }

        /// <summary>
        /// Checks if muster can be triggered in the current game state.
        /// Returns a tuple: (canTrigger, deferReason).
        /// </summary>
        private (bool canTrigger, string deferReason) CanTriggerMuster()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return (false, "Not enlisted");
            }

            // Player in combat
            if (MobileParty.MainParty?.MapEvent != null)
            {
                return (false, "Player in combat");
            }

            // Player captured/prisoner
            if (Hero.MainHero?.IsPrisoner == true)
            {
                return (false, "Player captured");
            }

            // Player in active encounter (siege, battle preparation)
            if (PlayerEncounter.Current != null && !PlayerEncounter.InsideSettlement)
            {
                return (false, "Player in encounter");
            }

            // Player in conversation
            if (Campaign.Current?.ConversationManager?.IsConversationInProgress == true)
            {
                return (false, "Player in conversation");
            }

            // Player has a Gauntlet UI screen open (provisions, equipment selector, upgrade screen)
            // These are custom UI layers that would be disrupted by menu activation
            if (QuartermasterProvisionsBehavior.IsOpen || QuartermasterEquipmentSelectorBehavior.IsOpen)
            {
                return (false, "Player in Gauntlet UI");
            }

            // Already in a muster sequence
            if (_currentMuster != null)
            {
                return (false, "Muster already in progress");
            }

            // Check if currently in a game menu that would conflict
            var menuManager = Campaign.Current?.GameMenuManager;

            // Bannerlord's GameMenuManager.NextMenu can throw ArgumentNullException when the menu system
            // is in an invalid state (null menu ID in internal dictionary). Catch and allow muster to proceed.
            try
            {
                var nextMenu = menuManager?.NextMenu;
                if (nextMenu != null && !string.IsNullOrEmpty(nextMenu.StringId))
                {
                    // Allow all enlisted menus (status, camp hub, muster, etc.) - these are our menus
                    if (nextMenu.StringId.StartsWith("enlisted_"))
                    {
                        return (true, null);
                    }

                    // Allow from town/settlement menus (muster closes them)
                    if (nextMenu.StringId.Contains("town") || nextMenu.StringId.Contains("village") ||
                        nextMenu.StringId.Contains("castle") || nextMenu.StringId.Contains("settlement"))
                    {
                        return (true, null);
                    }

                    // Defer from other menus
                    return (false, "Player in menu: " + nextMenu.StringId);
                }
            }
            catch (ArgumentNullException ex)
            {
                // Bannerlord's menu system has null key in internal dictionary - safe to proceed
                ModLogger.Debug(LogCategory, $"Menu system in invalid state (null key), allowing muster: {ex.Message}");
            }

            return (true, null);
        }

        /// <summary>
        /// Checks for pending muster triggers after combat/menu exits.
        /// Called periodically from hourly tick.
        /// </summary>
        public void CheckPendingMusterTrigger()
        {
            if (!_musterPendingAfterCombat && !_musterPendingAfterMenu)
            {
                return;
            }

            // Safety check: ensure menus are registered before attempting to trigger
            if (!_menusRegisteredSuccessfully)
            {
                ModLogger.Debug(LogCategory, "Deferred muster skipped: menus not registered");
                _musterPendingAfterCombat = false;
                _musterPendingAfterMenu = false;
                EnlistmentBehavior.Instance?.DeferPayMuster();
                return;
            }

            var (canTrigger, reason) = CanTriggerMuster();
            if (canTrigger)
            {
                _musterPendingAfterCombat = false;
                _musterPendingAfterMenu = false;
                ModLogger.Info(LogCategory, "Executing deferred muster trigger");
                BeginMusterSequenceInternal();
            }
            else
            {
                ModLogger.Debug(LogCategory, $"Deferred muster still blocked: {reason}");
            }
        }

        /// <summary>
        /// Entry point called from EnlistmentBehavior when muster day arrives.
        /// Creates a new session state, captures context, and switches to the intro menu.
        /// Handles edge cases like combat, capture, and menu states.
        /// </summary>
        public void BeginMusterSequence()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                ModLogger.Warn(LogCategory, "BeginMusterSequence called but player is not enlisted");
                return;
            }

            // If menu registration failed, defer muster to next cycle
            if (!_menusRegisteredSuccessfully)
            {
                ModLogger.Expected(LogCategory, "muster_menus_not_registered", "Muster menus not registered, deferring to next cycle");
                enlistment.DeferPayMuster();
                InformationManager.DisplayMessage(new InformationMessage("Muster system unavailable. Will retry next cycle.", Colors.Yellow));
                return;
            }

            // Prevent rapid retry attempts - but only skip if we're already pending
            // The throttle prevents spam, but we must ensure muster eventually triggers.
            // If throttled, set deferred flag so the hourly check will retry.
            if (CampaignTime.Now - _lastMusterTriggerAttempt < CampaignTime.Hours(1))
            {
                // Only throttle if there's already a deferred muster pending.
                // If neither flag is set, this is a fresh attempt that got throttled
                // and we need to ensure it retries.
                if (!_musterPendingAfterCombat && !_musterPendingAfterMenu)
                {
                    _musterPendingAfterMenu = true;
                    ModLogger.Debug(LogCategory, "Muster trigger attempt throttled - setting deferred flag to retry later");
                }
                else
                {
                    ModLogger.Debug(LogCategory, "Muster trigger attempt throttled (already deferred)");
                }
                return;
            }
            _lastMusterTriggerAttempt = CampaignTime.Now;

            // Check if we can trigger muster in current state
            var (canTrigger, deferReason) = CanTriggerMuster();

            if (!canTrigger)
            {
                // Handle specific defer scenarios
                if (deferReason == "Player in combat")
                {
                    _musterPendingAfterCombat = true;
                    ModLogger.Info(LogCategory, "Muster deferred: player in combat. Will trigger when combat ends.");
                    return;
                }

                if (deferReason == "Player captured")
                {
                    // Skip muster entirely while captured, wages accumulate as backpay
                    ModLogger.Info(LogCategory, "Muster skipped: player captured. Wages will accumulate as backpay.");
                    enlistment.DeferPayMuster();
                    return;
                }

                if (deferReason == "Player in conversation" || deferReason?.StartsWith("Player in menu:") == true)
                {
                    _musterPendingAfterMenu = true;
                    ModLogger.Info(LogCategory, $"Muster deferred: {deferReason}. Will trigger after.");
                    return;
                }

                if (deferReason == "Muster already in progress")
                {
                    ModLogger.Debug(LogCategory, "Muster trigger ignored: already in progress");
                    return;
                }

                // Unknown defer reason, log and defer
                ModLogger.Warn(LogCategory, $"Muster deferred: {deferReason}");
                _musterPendingAfterMenu = true;
                return;
            }

            BeginMusterSequenceInternal();
        }

        /// <summary>
        /// Internal implementation of muster sequence start.
        /// Called after edge case checks pass.
        /// </summary>
        private void BeginMusterSequenceInternal()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                ModLogger.Expected(LogCategory, "muster_begin_not_enlisted", "BeginMusterSequenceInternal called but player not enlisted");
                return;
            }

            if (Campaign.Current == null)
            {
                ModLogger.Expected(LogCategory, "muster_campaign_null", "Cannot start muster: Campaign.Current is null");
                InformationManager.DisplayMessage(new InformationMessage("Cannot trigger muster: campaign not active.", Colors.Red));
                return;
            }

            try
            {
                var currentDay = (int)CampaignTime.Now.ToDays;
                ModLogger.Info(LogCategory, $"Beginning muster sequence on day {currentDay}");

                // Create fresh session state
                _currentMuster = new MusterSessionState
                {
                    CurrentStage = MusterIntroMenuId,
                    MusterDay = currentDay,
                    LastMusterDay = enlistment.LastMusterDay
                };

                // Capture strategic context for flavor text
                var lordParty = enlistment.EnlistedLord?.PartyBelongedTo;
                _currentMuster.StrategicContext = lordParty != null
                    ? ArmyContextAnalyzer.GetLordStrategicContext(lordParty)
                    : "patrol_peacetime";

                // Check for high scrutiny warning
                var scrutiny = EscalationManager.Instance?.State?.Scrutiny ?? 0;
                if (scrutiny >= Escalation.EscalationThresholds.ScrutinyShakedown)
                {
                    _currentMuster.HighScrutinyWarning = true;
                    ModLogger.Debug(LogCategory, $"High scrutiny warning enabled (scrutiny={scrutiny})");
                }

                // Check for promotion this period
                if (enlistment.EnlistmentTier > enlistment.TierAtLastMuster && enlistment.TierAtLastMuster > 0)
                {
                    _currentMuster.PromotionOccurredThisPeriod = true;
                    _currentMuster.PreviousTier = enlistment.TierAtLastMuster;
                    _currentMuster.CurrentTier = enlistment.EnlistmentTier;
                    _currentMuster.PromotionDay = enlistment.DayOfLastPromotion;
                    ModLogger.Info(LogCategory, $"Promotion detected this period: T{_currentMuster.PreviousTier} -> T{_currentMuster.CurrentTier}");
                }

                // Close any current menu before opening muster (handles town/settlement menus)
                var menuManager = Campaign.Current?.GameMenuManager;

                // Bannerlord's GameMenuManager.NextMenu can throw ArgumentNullException when the menu system
                // is in an invalid state (null menu ID in internal dictionary). Catch and continue.
                GameMenu nextMenu = null;
                try
                {
                    nextMenu = menuManager?.NextMenu;
                }
                catch (ArgumentNullException ex)
                {
                    ModLogger.Debug(LogCategory, $"Menu system in invalid state (null key), continuing: {ex.Message}");
                }

                if (nextMenu != null && !nextMenu.StringId.StartsWith("enlisted_muster"))
                {
                    ModLogger.Debug(LogCategory, $"Closing current menu before muster: {nextMenu.StringId}");
                }

                // Validate menu system is ready
                if (menuManager == null)
                {
                    ModLogger.Expected(LogCategory, "muster_menu_manager_null", "Cannot start muster: GameMenuManager is null");
                    AbortMusterWithFallback("Menu system not available");
                    return;
                }

                // CRITICAL: Close any active menus before muster to prevent graphics state corruption
                // The native rendering system cannot handle menu transitions while another menu is rendering
                try
                {
                    var currentMenu = Campaign.Current?.CurrentMenuContext?.GameMenu;
                    if (currentMenu != null)
                    {
                        ModLogger.Debug(LogCategory, $"Closing active menu before muster: {currentMenu.StringId}");
                        GameMenu.ExitToLast();
                    }
                }
                catch (Exception ex)
                {
                    ModLogger.Error(LogCategory, "Failed to close active menu before muster", ex);
                    // Continue anyway - the menu might not be closeable
                }

                // Build full intro text BEFORE menu activation to prevent rendering crashes
                // The menu text uses {MUSTER_DAY} and {MUSTER_INTRO_TEXT} which must be fully populated before rendering
                try
                {
                    MBTextManager.SetTextVariable("MUSTER_DAY", Math.Round(enlistment.DaysServed).ToString());

                    // Build the complete intro text now, not just a placeholder
                    var introText = BuildIntroTextSafe();
                    MBTextManager.SetTextVariable("MUSTER_INTRO_TEXT", introText);

                    ModLogger.Debug(LogCategory, $"Pre-initialized menu text variables (intro text length: {introText?.Length ?? 0})");
                }
                catch (Exception ex)
                {
                    ModLogger.Surfaced("MUSTER", "Failed to pre-initialize text variables", ex);
                    AbortMusterWithFallback("Failed to initialize menu text");
                    return;
                }

                // Defer menu activation with multiple-frame delay to ensure clean state
                // This prevents native rendering crashes by allowing all state transitions to complete
                ModLogger.Debug(LogCategory, $"Scheduling deferred muster intro menu activation (context: {_currentMuster.StrategicContext})");

                // Capture time state NOW before any frame delays, so we restore properly after muster.
                // This handles the case where muster triggers while player is fast-forwarding.
                QuartermasterManager.CaptureTimeStateBeforeMenuActivation();

                // First frame: ensure we're on the map with no active menus
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    try
                    {
                        // Validate we're in a safe state for menu activation
                        if (PlayerEncounter.Current != null)
                        {
                            ModLogger.Warn(LogCategory, "Muster activation deferred: encounter still active");
                            AbortMusterWithFallback("Cannot activate muster during encounter");
                            return;
                        }

                        // Second frame: activate the menu after state is clean
                        NextFrameDispatcher.RunNextFrame(() =>
                        {
                            try
                            {
                                ModLogger.Debug(LogCategory, "Executing deferred muster menu activation");
                                GameMenu.ActivateGameMenu(MusterIntroMenuId);
                                ModLogger.Debug(LogCategory, "Muster intro menu activated successfully");
                            }
                            catch (Exception ex)
                            {
                                ModLogger.Surfaced("MUSTER", "Deferred menu activation failed", ex);
                                AbortMusterWithFallback("Failed to activate muster menu");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Surfaced("MUSTER", "Pre-activation validation failed", ex);
                        AbortMusterWithFallback("Failed to validate game state");
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Unhandled exception in BeginMusterSequence", ex);
                // Ensure player isn't stuck - try fallback
                AbortMusterWithFallback("Failed to start muster menu sequence");
            }
        }

        /// <summary>
        /// Aborts the current muster sequence and schedules hourly retry.
        /// Ensures player is never stuck, especially during multi-day sieges.
        /// </summary>
        private void AbortMusterWithFallback(string reason)
        {
            ModLogger.Warn(LogCategory, $"Muster aborted: {reason} - will retry hourly");

            // Clear muster session state but keep retry flags
            _currentMuster = null;
            
            // Set deferred flag for hourly retry instead of daily retry.
            // This is critical for multi-day sieges where daily retry keeps failing.
            _musterPendingAfterMenu = true;
            _musterPendingAfterCombat = false;

            // Restore time control if locked
            try
            {
                if (Campaign.Current != null)
                {
                    Campaign.Current.SetTimeControlModeLock(false);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to unlock time control during abort", ex);
            }

            // Try to exit any active menu
            try
            {
                GameMenu.ExitToLast();
            }
            catch
            {
                // Ignore - might not be in a menu
            }

            // Don't call DeferPayMuster() - let hourly tick handle retry via _musterPendingAfterMenu.
            // This prevents the daily retry loop that fails during sieges.
            // Note: _payMusterPending remains true in EnlistmentBehavior, which is correct -
            // it prevents daily tick from re-triggering while we're doing hourly retries.
            try
            {
                // Only show message once per abort, not repeatedly
                ModLogger.Debug(LogCategory, "Muster will retry automatically when conditions allow");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to defer muster during abort", ex);
            }
        }

        #region Menu Init Handlers

        /// <summary>
        /// Safely switches to another muster stage with error handling.
        /// If transition fails, jumps to muster complete or aborts.
        /// </summary>
        private void SafeSwitchToMenu(string menuId)
        {
            try
            {
                ModLogger.Debug(LogCategory, $"Switching to menu: {menuId}");
                GameMenu.SwitchToMenu(menuId);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Stage transition failed", ex,
                    ctx: LogCtx.Of("TargetMenu", menuId));
                _currentMuster?.EncounteredErrors.Add($"Stage transition failed: {menuId}");

                // Try to jump to complete stage if not already there
                if (menuId != MusterCompleteMenuId)
                {
                    try
                    {
                        GameMenu.SwitchToMenu(MusterCompleteMenuId);
                    }
                    catch
                    {
                        AbortMusterWithFallback($"Failed to switch to {menuId} and complete stage");
                    }
                }
                else
                {
                    AbortMusterWithFallback($"Failed to switch to complete stage");
                }
            }
        }

        /// <summary>
        /// Wraps effect application in try-catch, logging errors but continuing muster.
        /// </summary>
        private bool SafeApplyEffect(string effectName, Action effectAction)
        {
            try
            {
                effectAction();
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Effect application failed", ex,
                    ctx: LogCtx.Of("Effect", effectName));
                if (_currentMuster != null)
                {
                    _currentMuster.EffectsPartiallyFailed = true;
                    _currentMuster.EncounteredErrors.Add($"Effect failed: {effectName}");
                }
                return false;
            }
        }

        private void OnMusterIntroInit(MenuCallbackArgs args)
        {
            try
            {
                if (_currentMuster == null)
                {
                    ModLogger.Warn(LogCategory, "OnMusterIntroInit called with null session state");
                    return;
                }

                _currentMuster.CurrentStage = MusterIntroMenuId;

                var enlistment = EnlistmentBehavior.Instance;

                // Build intro text with null safety
                var introText = BuildIntroTextSafe();
                MBTextManager.SetTextVariable("MUSTER_DAY", enlistment != null ? Math.Round(enlistment.DaysServed).ToString() : "?");
                MBTextManager.SetTextVariable("MUSTER_INTRO_TEXT", introText);

                // Initialize time control on first stage
                InitializeMusterTimeControl(args);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "OnMusterIntroInit failed", ex);
                AbortMusterWithFallback("Intro stage initialization failed");
            }
        }

        private void OnMusterPayInit(MenuCallbackArgs args)
        {
            try
            {
                if (_currentMuster == null)
                {
                    return;
                }
                _currentMuster.CurrentStage = MusterPayMenuId;

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    ModLogger.Warn(LogCategory, "OnMusterPayInit: EnlistmentBehavior null");
                    var payUnavailable = new TextObject("{=muster_pay_unavailable}Pay records unavailable").ToString();
                    MBTextManager.SetTextVariable("MUSTER_PAY_TEXT", $"{payUnavailable}.");
                    MBTextManager.SetTextVariable("PAY_AMOUNT", "0");
                    return;
                }

                // Set pay amount variable for option text
                MBTextManager.SetTextVariable("PAY_AMOUNT", enlistment.PendingMusterPay.ToString());
                MBTextManager.SetTextVariable("MUSTER_PAY_TEXT", BuildPayTextSafe());
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "OnMusterPayInit failed", ex);
                var payContactPaymaster = new TextObject("{=muster_pay_contact_paymaster}Pay records unavailable. Contact paymaster.").ToString();
                MBTextManager.SetTextVariable("MUSTER_PAY_TEXT", payContactPaymaster);
                MBTextManager.SetTextVariable("PAY_AMOUNT", "0");
            }
        }

        /// <summary>
        /// Builds intro text with null safety for all dependent systems.
        /// </summary>
        private string BuildIntroTextSafe()
        {
            try
            {
                return BuildIntroText();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "BuildIntroText failed, using fallback", ex);
                _currentMuster?.EncounteredErrors.Add("Intro text generation failed");

                var sb = new StringBuilder();
                sb.AppendLine("The sergeants call the muster. Your company assembles in formation.");
                sb.AppendLine();
                sb.AppendLine("_____ YOUR SERVICE RECORD _____");
                sb.AppendLine();

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment != null)
                {
                    sb.AppendLine($"Rank: {GetRankName(enlistment.EnlistmentTier)} (Tier {enlistment.EnlistmentTier})");
                    sb.AppendLine($"Days Served: {enlistment.DaysServed} days");
                }
                else
                {
                    sb.AppendLine("Service record unavailable.");
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Builds pay text with null safety for all dependent systems.
        /// </summary>
        private string BuildPayTextSafe()
        {
            try
            {
                return BuildPayText();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "BuildPayText failed, using fallback", ex);
                _currentMuster?.EncounteredErrors.Add("Pay text generation failed");

                var sb = new StringBuilder();
                var payIntro = new TextObject("{=muster_pay_intro}You step forward to the paymaster's table. He opens his ledger.").ToString();
                var payStatusHeader = new TextObject("{=muster_pay_status_header}PAY STATUS").ToString();
                var wagesOwedLabel = new TextObject("{=muster_wages_owed}WAGES OWED").ToString();
                var denarsText = new TextObject("{=muster_denars}denars").ToString();
                var payUnavailable = new TextObject("{=muster_pay_unavailable}Pay records unavailable").ToString();
                
                sb.AppendLine(payIntro);
                sb.AppendLine();
                sb.AppendLine($"_____ {payStatusHeader} _____");
                sb.AppendLine();

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment != null)
                {
                    sb.AppendLine($"{wagesOwedLabel}: {enlistment.PendingMusterPay} {denarsText}");
                }
                else
                {
                    sb.AppendLine($"{payUnavailable}.");
                }

                return sb.ToString();
            }
        }

        private void OnPromotionInit(MenuCallbackArgs args)
        {
            if (_currentMuster == null)
            {
                return;
            }
            _currentMuster.CurrentStage = MusterPromotionRecapMenuId;

            MBTextManager.SetTextVariable("MUSTER_PROMOTION_TEXT", BuildPromotionText());
        }

        private void OnRetinueInit(MenuCallbackArgs args)
        {
            if (_currentMuster == null)
            {
                return;
            }
            _currentMuster.CurrentStage = MusterRetinueMenuId;

            MBTextManager.SetTextVariable("MUSTER_RETINUE_TEXT", BuildRetinueText());
        }

        private void OnMusterCompleteInit(MenuCallbackArgs args)
        {
            try
            {
                if (_currentMuster == null)
                {
                    return;
                }
                _currentMuster.CurrentStage = MusterCompleteMenuId;

                var enlistment = EnlistmentBehavior.Instance;
                MBTextManager.SetTextVariable("MUSTER_DAY", enlistment != null ? Math.Round(enlistment.DaysServed).ToString() : "?");
                MBTextManager.SetTextVariable("MUSTER_COMPLETE_TEXT", BuildMusterCompleteTextSafe());
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "OnMusterCompleteInit failed", ex);
                MBTextManager.SetTextVariable("MUSTER_DAY", "?");
                MBTextManager.SetTextVariable("MUSTER_COMPLETE_TEXT", "Muster summary unavailable. You may dismiss.");
            }
        }

        /// <summary>
        /// Builds muster complete text with null safety and edge case handling.
        /// </summary>
        private string BuildMusterCompleteTextSafe()
        {
            try
            {
                var text = BuildMusterCompleteText();

                // Append warnings for edge cases
                var sb = new StringBuilder(text);

                // High scrutiny warning
                if (_currentMuster?.HighScrutinyWarning == true)
                {
                    sb.AppendLine();
                    sb.AppendLine("[WARNING] You are under heightened scrutiny. Your actions are being watched closely.");
                }

                // Effects partially failed warning
                if (_currentMuster?.EffectsPartiallyFailed == true)
                {
                    sb.AppendLine();
                    sb.AppendLine("[WARNING] Some effects may not have applied correctly.");
                }

                // Discharge threshold warning
                if (_currentMuster?.DischargeThresholdReached == true)
                {
                    sb.AppendLine();
                    sb.AppendLine("[DISCIPLINARY NOTICE] Your conduct will be reviewed by command.");
                }

                // Supply crisis warning
                var supply = CompanySupplyManager.Instance?.TotalSupply ?? 100;
                if (supply < 20)
                {
                    sb.AppendLine();
                    sb.AppendLine("[CRITICAL SUPPLY] Quartermaster has locked all baggage access.");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "BuildMusterCompleteText failed, using fallback", ex);
                _currentMuster?.EncounteredErrors.Add("Complete text generation failed");

                var sb = new StringBuilder();
                sb.AppendLine("Muster is dismissed. The sergeants release the company.");
                sb.AppendLine();
                sb.AppendLine("_____ MUSTER OUTCOMES _____");
                sb.AppendLine();

                if (_currentMuster != null)
                {
                    var payLabel = new TextObject("{=muster_status_pay}Pay").ToString();
                    sb.AppendLine($"{payLabel}: {_currentMuster.PayOutcome ?? "unknown"}");
                }
                else
                {
                    var detailsUnavailable = new TextObject("{=muster_details_unavailable}Details unavailable").ToString();
                    sb.AppendLine($"{detailsUnavailable}.");
                }

                return sb.ToString();
            }
        }

        #endregion

        #region Menu Flow Control

        private static bool OnMusterMenuCondition(MenuCallbackArgs args)
        {
            // Muster menus are always available when active
            return true;
        }

        private static void OnMusterMenuTick(MenuCallbackArgs args, CampaignTime dt)
        {
            // No-op tick handler for wait menus
        }

        /// <summary>
        /// Resolves standard pay muster (accept your pay).
        /// Calls EnlistmentBehavior.ResolvePayMusterStandard() which handles full/partial/delay logic internally.
        /// Note: ResolvePayMusterStandard calls OnMusterCycleComplete which processes rations and QM stock.
        /// </summary>
        private void ResolvePayMusterStandard()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                ModLogger.Expected(LogCategory, "muster_standard_pay_no_enlistment", "Cannot resolve standard pay: EnlistmentBehavior null");
                if (_currentMuster != null)
                {
                    _currentMuster.EffectsPartiallyFailed = true;
                    _currentMuster.PayOutcome = "error";
                }
                ProceedToNextStageFromPay();
                return;
            }

            var success = SafeApplyEffect("StandardPay", () =>
            {
                ModLogger.Info(LogCategory, "Resolving standard pay from muster menu");

                // Capture pay details before resolution for muster state tracking
                if (_currentMuster != null)
                {
                    var wages = enlistment.PendingMusterPay;
                    var backpay = enlistment.OwedBackpay;
                    _currentMuster.PayReceived = wages + backpay;
                    _currentMuster.PayOutcome = "full";
                }

                // Call EnlistmentBehavior's standard pay resolution
                enlistment.ResolvePayMusterStandard();

                // Update muster state with actual outcome
                if (_currentMuster != null)
                {
                    _currentMuster.PayOutcome = enlistment.LastPayOutcome;
                }

                ModLogger.Debug(LogCategory, $"Standard pay resolved: {enlistment.LastPayOutcome}");
            });

            if (!success && _currentMuster != null)
            {
                _currentMuster.PayOutcome = "error";
            }

            ProceedToNextStageFromPay();
        }

        /// <summary>
        /// Resolves corruption muster (demand a recount).
        /// Attempts skill check to get 95% payout through illicit channels.
        /// </summary>
        private void ResolveCorruptionMuster()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                ModLogger.Warn(LogCategory, "Cannot resolve corruption pay: EnlistmentBehavior unavailable");
                ProceedToNextStageFromPay();
                return;
            }

            try
            {
                ModLogger.Info(LogCategory, "Resolving corruption pay from muster menu");

                if (_currentMuster != null)
                {
                    _currentMuster.PayOutcome = "corruption";
                }

                enlistment.ResolveCorruptionMuster();

                if (_currentMuster != null)
                {
                    _currentMuster.PayOutcome = enlistment.LastPayOutcome;
                }

                ModLogger.Debug(LogCategory, $"Corruption pay resolved: {enlistment.LastPayOutcome}");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Failed to resolve corruption pay", ex);
            }

            ProceedToNextStageFromPay();
        }

        /// <summary>
        /// Resolves side deal muster (trade pay for gear).
        /// Takes reduced pay (60%) but opens equipment selection after muster.
        /// </summary>
        private void ResolveSideDealMuster()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                ModLogger.Warn(LogCategory, "Cannot resolve side deal pay: EnlistmentBehavior unavailable");
                ProceedToNextStageFromPay();
                return;
            }

            try
            {
                ModLogger.Info(LogCategory, "Resolving Quartermaster's Deal from muster menu");

                if (_currentMuster != null)
                {
                    _currentMuster.PayOutcome = "qm_deal";
                }

                var outcome = enlistment.ResolveSideDealMuster();

                if (_currentMuster != null)
                {
                    _currentMuster.PayOutcome = outcome;

                }

                ModLogger.Debug(LogCategory, $"Quartermaster's Deal resolved: {outcome}");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Failed to resolve Quartermaster's Deal", ex);
            }

            ProceedToNextStageFromPay();
        }

        /// <summary>
        /// Resolves promissory note muster (IOU).
        /// Defers payment 3 days to ease lord's finances.
        /// </summary>
        private void ResolvePromissoryMuster()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                ModLogger.Warn(LogCategory, "Cannot resolve promissory pay: EnlistmentBehavior unavailable");
                ProceedToNextStageFromPay();
                return;
            }

            try
            {
                ModLogger.Info(LogCategory, "Resolving promissory note from muster menu");

                if (_currentMuster != null)
                {
                    _currentMuster.PayOutcome = "promissory";
                    _currentMuster.PayReceived = 0; // No pay now, deferred
                }

                enlistment.ResolvePromissoryMuster();

                ModLogger.Debug(LogCategory, "Promissory note accepted, payment deferred 3 days");
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Failed to resolve promissory note", ex);
            }

            ProceedToNextStageFromPay();
        }

        /// <summary>
        /// Processes final discharge (honorable discharge).
        /// Ends service, pays final wages + severance, activates pension.
        /// ResolvePayMusterStandard() detects IsPendingDischarge internally and calls
        /// the private FinalizePendingDischarge() method which handles the full flow.
        /// </summary>
        private void FinalizePendingDischarge()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                ModLogger.Warn(LogCategory, "Cannot finalize discharge: EnlistmentBehavior unavailable");
                GameMenu.ExitToLast();
                return;
            }

            try
            {
                ModLogger.Info(LogCategory, "Processing final discharge from muster menu");

                if (_currentMuster != null)
                {
                    _currentMuster.PayOutcome = "final_discharge";
                }

                // Capture discharge details before processing
                var isRetirement = enlistment.IsEligibleForRetirement;
                var daysServed = (int)(CampaignTime.Now - enlistment.EnlistmentDate).ToDays;
                var pendingPay = enlistment.PendingMusterPay;

                // ResolvePayMusterStandard detects IsPendingDischarge and handles final muster internally
                // This pays final wages + severance, applies relations, and calls StopEnlist
                enlistment.ResolvePayMusterStandard();

                // Show discharge outcome to player
                var dischargeBand = enlistment.LastDischargeBand;
                var pensionPerDay = enlistment.PensionAmountPerDay;
                ShowDischargeOutcome(dischargeBand, isRetirement, daysServed, pensionPerDay);

                ModLogger.Info(LogCategory, "Final discharge complete, service ended");

                // Service has ended, clear muster state and exit
                RestoreTimeControlOnExit();
                _currentMuster = null;
                GameMenu.ExitToLast();
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Failed to finalize discharge", ex);
                RestoreTimeControlOnExit();
                _currentMuster = null;
                GameMenu.ExitToLast();
            }
        }

        /// <summary>
        /// Resolves smuggle discharge (desertion).
        /// Player keeps all gear but loses pension and faces desertion penalties.
        /// Uses DesertArmy() which handles keeping equipped gear and applying penalties.
        /// </summary>
        private void ResolveSmuggleDischarge()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                ModLogger.Warn(LogCategory, "Cannot resolve smuggle discharge: EnlistmentBehavior unavailable");
                GameMenu.ExitToLast();
                return;
            }

            try
            {
                ModLogger.Info(LogCategory, "Processing smuggle discharge (deserter) from muster menu");

                if (_currentMuster != null)
                {
                    _currentMuster.PayOutcome = "smuggle_desertion";
                }

                // StopEnlist with desertion status - player forfeits pension but keeps equipped gear
                enlistment.StopEnlist("Desertion via smuggle", isHonorableDischarge: false);

                ModLogger.Info(LogCategory, "Smuggle discharge complete, service ended as deserter");

                // Service has ended, clear muster state and exit
                RestoreTimeControlOnExit();
                _currentMuster = null;
                GameMenu.ExitToLast();
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Failed to resolve smuggle discharge", ex);
                RestoreTimeControlOnExit();
                _currentMuster = null;
                GameMenu.ExitToLast();
            }
        }

        /// <summary>
        /// Shows a confirmation dialog before processing discharge from the Request Discharge option.
        /// </summary>
        private void ShowDischargeConfirmation()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return;
                }

                // Calculate discharge details
                var daysServed = (int)enlistment.DaysServed;
                var lord = enlistment.CurrentLord;
                var negativeRelation = lord != null && Hero.MainHero.GetRelation(lord) < 0;

                // Determine discharge band
                string band;
                if (negativeRelation)
                {
                    band = "washout";
                }
                else if (daysServed >= 200)
                {
                    band = enlistment.EnlistmentTier >= 4 ? "heroic" : "veteran";
                }
                else if (daysServed >= 100)
                {
                    band = "honorable";
                }
                else
                {
                    band = "washout";
                }

                // Build description based on discharge band
                var title = new TextObject("{=muster_discharge_confirm_title}Confirm Discharge").ToString();
                string description;

                switch (band)
                {
                    case "heroic":
                    case "veteran":
                        description = new TextObject(
                            "{=muster_discharge_confirm_veteran}You will receive:\n\n" +
                            "• Severance Pay: 3000 denars\n" +
                            "• Pension: Daily payment for service rendered\n" +
                            "• Relation: +30 with your lord\n" +
                            "• Gear: Keep armor, weapons returned to inventory\n\n" +
                            "Your service has been exemplary. Proceed with discharge?").ToString();
                        break;

                    case "honorable":
                        description = new TextObject(
                            "{=muster_discharge_confirm_honorable}You will receive:\n\n" +
                            "• Severance Pay: 3000 denars\n" +
                            "• Pension: Daily payment for service rendered\n" +
                            "• Relation: +10 with your lord\n" +
                            "• Gear: Keep armor, weapons returned to inventory\n\n" +
                            "Your service has been satisfactory. Proceed with discharge?").ToString();
                        break;

                    default: // washout
                        description = new TextObject(
                            "{=muster_discharge_confirm_washout}Early discharge (washout):\n\n" +
                            "• Severance Pay: None\n" +
                            "• Pension: None\n" +
                            "• Relation: -10 with your lord\n" +
                            "• Gear: All equipment confiscated\n\n" +
                            "This is not an honorable discharge. Proceed?").ToString();
                        break;
                }

                var confirmData = new InquiryData(
                    title,
                    description,
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: true,
                    affirmativeText: new TextObject("{=muster_discharge_confirm_yes}Confirm Discharge").ToString(),
                    negativeText: new TextObject("{=muster_discharge_confirm_no}Cancel").ToString(),
                    affirmativeAction: () =>
                    {
                        try
                        {
                            enlistment.RequestDischarge();

                            if (_currentMuster != null)
                            {
                                _currentMuster.PayOutcome = "final_discharge";
                            }

                            FinalizePendingDischarge();
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("MUSTER", "Error processing discharge confirmation", ex);
                        }
                    },
                    negativeAction: () =>
                    {
                        // Cancel - just close the popup, remain on the menu
                    });

                InformationManager.ShowInquiry(confirmData);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Error showing discharge confirmation", ex);
            }
        }

        /// <summary>
        /// Shows a confirmation dialog before processing final pay discharge.
        /// </summary>
        private void ShowFinalPayDischargeConfirmation()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsPendingDischarge)
                {
                    return;
                }

                // Get discharge band from enlistment (should already be calculated when RequestDischarge was called)
                var band = enlistment.LastDischargeBand;
                var daysServed = (int)enlistment.DaysServed;

                // Build description based on discharge band
                var title = new TextObject("{=muster_final_pay_confirm_title}Take Your Final Pay").ToString();
                string description;

                switch (band)
                {
                    case "heroic":
                    case "veteran":
                        description = new TextObject(
                            "{=muster_final_pay_confirm_veteran}Collecting your final pay and pension.\n\n" +
                            "• Severance Pay: 3000 denars\n" +
                            "• Pension: Daily payment for {DAYS} days of service\n" +
                            "• Relation: +30 with your lord\n" +
                            "• Gear: Keep armor, weapons returned to inventory\n\n" +
                            "Your exemplary service is complete. Proceed?")
                            .SetTextVariable("DAYS", daysServed)
                            .ToString();
                        break;

                    case "honorable":
                        description = new TextObject(
                            "{=muster_final_pay_confirm_honorable}Collecting your final pay and pension.\n\n" +
                            "• Severance Pay: 3000 denars\n" +
                            "• Pension: Daily payment for {DAYS} days of service\n" +
                            "• Relation: +10 with your lord\n" +
                            "• Gear: Keep armor, weapons returned to inventory\n\n" +
                            "Your service is complete. Proceed?")
                            .SetTextVariable("DAYS", daysServed)
                            .ToString();
                        break;

                    default: // washout or other
                        description = new TextObject(
                            "{=muster_final_pay_confirm_washout}Processing discharge.\n\n" +
                            "• Severance Pay: None\n" +
                            "• Pension: None\n" +
                            "• Relation: -10 with your lord\n" +
                            "• Gear: All equipment confiscated\n\n" +
                            "This discharge is not honorable. Proceed?").ToString();
                        break;
                }

                var confirmData = new InquiryData(
                    title,
                    description,
                    isAffirmativeOptionShown: true,
                    isNegativeOptionShown: true,
                    affirmativeText: new TextObject("{=muster_final_pay_yes}Take Final Pay").ToString(),
                    negativeText: new TextObject("{=muster_final_pay_no}Cancel").ToString(),
                    affirmativeAction: () =>
                    {
                        try
                        {
                            FinalizePendingDischarge();
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Surfaced("MUSTER", "Error processing final pay discharge", ex);
                        }
                    },
                    negativeAction: () =>
                    {
                        // Cancel - just close the popup, remain on the pay menu
                    });

                InformationManager.ShowInquiry(confirmData);
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Error showing final pay confirmation", ex);
            }
        }

        /// <summary>
        /// Proceeds to next stage after pay resolution.
        /// After pay, automatically processes ration exchange (handled by OnMusterCycleComplete),
        /// then moves to the next stage (inspection or promotion recap).
        /// </summary>
        private void ProceedToNextStageFromPay()
        {
            // Ration exchange happens automatically in OnMusterCycleComplete (called by payment resolution)
            // No menu stage for ration exchange - it's automatic

            if (_currentMuster == null)
            {
                GameMenu.SwitchToMenu(MusterCompleteMenuId);
                return;
            }

            // If promotion occurred this period, show recap
            if (_currentMuster.PromotionOccurredThisPeriod)
            {
                GameMenu.SwitchToMenu(MusterPromotionRecapMenuId);
            }
            else
            {
                ProceedToNextStageFromPromotion();
            }
        }

        private void ProceedToNextStageFromPromotion()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var retinueMgr = RetinueManager.Instance;

            // If T7+ with retinue (or just granted retinue), show retinue muster
            if (enlistment?.EnlistmentTier >= 7 && retinueMgr != null)
            {
                // Show retinue stage even if retinue not formed yet (T7 promotion case)
                // or if retinue exists
                var hasRetinue = retinueMgr.State?.HasRetinue ?? false;
                var justPromotedToT7 = _currentMuster?.CurrentTier == 7 && _currentMuster?.PromotionOccurredThisPeriod == true;

                if (hasRetinue || justPromotedToT7)
                {
                    GameMenu.SwitchToMenu(MusterRetinueMenuId);
                }
                else
                {
                    GameMenu.SwitchToMenu(MusterCompleteMenuId);
                }
            }
            else
            {
                GameMenu.SwitchToMenu(MusterCompleteMenuId);
            }
        }

        private void CompleteMusterSequence()
        {
            ModLogger.Info(LogCategory, "Muster sequence completing");

            // Capture post-muster flags before clearing state
            var visitQM = _currentMuster?.VisitQMAfter ?? false;
            var recruitRetinue = _currentMuster?.RecruitRetinueAfter ?? false;
            var requestLeave = _currentMuster?.RequestLeaveAfter ?? false;
            var effectsPartiallyFailed = _currentMuster?.EffectsPartiallyFailed ?? false;
            var pendingEscalationEvents = _currentMuster?.PendingEscalationEvents?.ToList() ?? new List<string>();
            var dischargeThresholdReached = _currentMuster?.DischargeThresholdReached ?? false;
            var encounteredErrors = _currentMuster?.EncounteredErrors?.ToList() ?? new List<string>();

            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment != null)
                {
                    // Create MusterOutcomeRecord for news feed BEFORE OnMusterComplete resets tracking
                    if (_currentMuster != null)
                    {
                        SafeApplyEffect("CreateMusterRecord", () => CreateMusterOutcomeRecord());
                    }

                    // Mark muster complete (updates tier/XP tracking, resets XP sources)
                    SafeApplyEffect("MusterComplete", () => enlistment.OnMusterComplete());

                    // Invalidate opportunity cache so IsMusterDay recalculates
                    SafeApplyEffect("InvalidateOpportunityCache", () =>
                    {
                        Camp.CampOpportunityGenerator.Instance?.InvalidateCache();
                        ModLogger.Debug(LogCategory, "Opportunity cache invalidated after muster");
                    });

                    // Refresh quartermaster stock (with null safety)
                    SafeApplyEffect("QMStockRefresh", () =>
                    {
                        QuartermasterManager.Instance?.RollStockAvailability();
                        ModLogger.Debug(LogCategory, "QM stock availability refreshed");
                    });

                    // Mark newly unlocked items in case promotion occurred this period
                    SafeApplyEffect("QMUnlockedItems", () =>
                    {
                        QuartermasterManager.Instance?.UpdateNewlyUnlockedItems();
                    });
                }

                RestoreTimeControlOnExit();

                // Clear muster state before exiting
                _currentMuster = null;

                GameMenu.ExitToLast();

                // Return to enlisted menu if no post-muster actions
                if (!visitQM && !recruitRetinue && !requestLeave)
                {
                    NextFrameDispatcher.RunNextFrame(() =>
                    {
                        if (EnlistmentBehavior.Instance?.IsEnlisted == true)
                        {
                            // Use SafeActivateEnlistedMenu to respect siege detection
                            EnlistedMenuBehavior.SafeActivateEnlistedMenu();
                        }
                    });
                }

                // Show warning if some effects failed to apply
                if (effectsPartiallyFailed)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Some muster effects may not have applied correctly.", Colors.Yellow));
                }

                // Log any encountered errors
                if (encounteredErrors.Count > 0)
                {
                    ModLogger.Warn(LogCategory, $"Muster completed with {encounteredErrors.Count} errors: {string.Join(", ", encounteredErrors)}");
                }

                // Handle post-muster transitions after menu exits
                if (visitQM)
                {
                    ModLogger.Debug(LogCategory, "Post-muster: opening QM conversation");
                    SafeOpenQuartermaster();
                }
                else if (recruitRetinue)
                {
                    ModLogger.Debug(LogCategory, "Post-muster: opening retinue recruitment");
                    // TODO: Open retinue recruitment screen
                }
                else if (requestLeave)
                {
                    ModLogger.Debug(LogCategory, "Post-muster: requesting temporary leave");
                    EnlistmentBehavior.Instance?.StartTemporaryLeave();
                }

                // Queue escalation events that were triggered during muster
                if (pendingEscalationEvents.Count > 0)
                {
                    ModLogger.Info(LogCategory, $"Queuing {pendingEscalationEvents.Count} escalation events for post-muster");
                    // Events will fire on next hourly tick via EscalationManager
                }

                // Handle discharge threshold reached during muster
                if (dischargeThresholdReached)
                {
                    ModLogger.Info(LogCategory, "Discharge threshold reached during muster, queuing discharge event");
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Your conduct has been noted. Expect a summons from command.", Colors.Red));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Surfaced("MUSTER", "Failed to complete muster sequence", ex);
                _currentMuster = null;
                RestoreTimeControlOnExit();
                try { GameMenu.ExitToLast(); } catch { /* Ensure we don't get stuck */ }
            }
        }

        /// <summary>
        /// Safely opens QM conversation with error handling.
        /// </summary>
        private void SafeOpenQuartermaster()
        {
            try
            {
                // Check supply level before opening
                var supply = CompanySupplyManager.Instance?.TotalSupply ?? 100;
                if (supply < 15)
                {
                    ModLogger.Warn(LogCategory, "QM blocked due to critical supply shortage");
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Quartermaster unavailable due to critical supply shortage.", Colors.Yellow));
                    return;
                }

                OpenQuartermasterConversation();
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to open QM conversation", ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    "Quartermaster unavailable.", Colors.Yellow));
            }
        }

        /// <summary>
        /// Queues an escalation event to fire after muster completes.
        /// </summary>
        public void QueueEscalationEventForAfterMuster(string eventId)
        {
            if (_currentMuster == null)
            {
                ModLogger.Warn(LogCategory, $"Cannot queue escalation event {eventId}: no muster in progress");
                return;
            }

            _currentMuster.PendingEscalationEvents.Add(eventId);
            ModLogger.Debug(LogCategory, $"Queued escalation event for after muster: {eventId}");
        }

        /// <summary>
        /// Marks that the discharge threshold was reached during muster.
        /// The actual discharge event will fire after muster completes.
        /// </summary>
        public void MarkDischargeThresholdReached()
        {
            if (_currentMuster == null)
            {
                return;
            }
            _currentMuster.DischargeThresholdReached = true;
            ModLogger.Info(LogCategory, "Discharge threshold marked during muster");
        }

        private void CreateMusterOutcomeRecord()
        {
            try
            {
                var newsBehavior = EnlistedNewsBehavior.Instance;
                if (newsBehavior == null)
                {
                    ModLogger.Warn(LogCategory, "Cannot create muster outcome record: EnlistedNewsBehavior unavailable");
                    return;
                }

                // Calculate order counts from news feed
                var ordersCompleted = 0;
                var ordersFailed = 0;
                var lastMusterDay = _currentMuster.LastMusterDay;
                if (lastMusterDay > 0)
                {
                    var orders = newsBehavior.GetOrderOutcomesSince(lastMusterDay);
                    if (orders != null)
                    {
                        ordersCompleted = orders.Count(o => o.Success);
                        ordersFailed = orders.Count(o => !o.Success);
                    }
                }

                var record = new MusterOutcomeRecord
                {
                    DayNumber = _currentMuster.MusterDay,
                    StrategicContext = _currentMuster.StrategicContext ?? string.Empty,
                    PayOutcome = _currentMuster.PayOutcome ?? "unknown",
                    PayAmount = _currentMuster.PayReceived,
                    RationOutcome = _currentMuster.RationOutcome ?? "unknown",
                    RationItemId = string.Empty, // Not tracked in current muster state
                    QmReputation = 0, // Not tracked in current muster state
                    SupplyLevel = EnlistmentBehavior.Instance?.CompanyNeeds?.Supplies ?? 60,
                    LostSinceLast = 0, // Not tracked in current muster state
                    SickSinceLast = 0, // Not tracked in current muster state
                    OrdersCompleted = ordersCompleted,
                    OrdersFailed = ordersFailed,
                    BaggageOutcome = "not_conducted", // Baggage check removed
                    PromotionTier = _currentMuster.PromotionOccurredThisPeriod ? _currentMuster.CurrentTier : 0,
                    RetinueStrength = _currentMuster.RetinueStrength,
                    RetinueCasualties = _currentMuster.RetinueCasualties,
                    FallenRetinueNames = _currentMuster.FallenRetinueNames ?? new List<string>(),
                    RationFlavorText = string.Empty // Not currently used
                };

                newsBehavior.AddMusterOutcome(record);
                ModLogger.Info(LogCategory, $"Muster outcome recorded: pay={record.PayOutcome}:{record.PayAmount}, supply={record.SupplyLevel}%");
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to create muster outcome record", ex);
            }
        }

        #endregion

        #region Time Control

        /// <summary>
        /// Initialize time control behavior for muster menus based on user settings.
        /// Called once at the start of the muster sequence (intro stage).
        /// </summary>
        private void InitializeMusterTimeControl(MenuCallbackArgs args)
        {
            var settings = ModConfig.Settings;

            if (settings?.PauseGameDuringMuster == true)
            {
                // Pause mode: force stop and lock controls
                // Player cannot advance time during muster
                if (Campaign.Current != null)
                {
                    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                    Campaign.Current.SetTimeControlModeLock(true);
                }
                ModLogger.Debug(LogCategory, "Time control: Paused (locked)");
            }
            else
            {
                // Time-preserving mode: respect player's current speed
                // Player can advance time during muster using speed controls
                QuartermasterManager.CaptureTimeStateBeforeMenuActivation();
                args.MenuContext?.GameMenu?.StartWait();

                if (Campaign.Current != null)
                {
                    Campaign.Current.SetTimeControlModeLock(false);

                    var captured = QuartermasterManager.CapturedTimeMode ?? Campaign.Current.TimeControlMode;
                    var normalized = QuartermasterManager.NormalizeToStoppable(captured);
                    Campaign.Current.TimeControlMode = normalized;
                }
                ModLogger.Debug(LogCategory, $"Time control: Preserved ({QuartermasterManager.CapturedTimeMode})");
            }
        }

        /// <summary>
        /// Restore previous time control state when exiting muster.
        /// Called in CompleteMusterSequence() before exiting.
        /// </summary>
        private void RestoreTimeControlOnExit()
        {
            var settings = ModConfig.Settings;

            if (settings?.PauseGameDuringMuster != true && Campaign.Current != null)
            {
                // Time-preserving mode: restore captured time state
                if (QuartermasterManager.CapturedTimeMode.HasValue)
                {
                    var normalized = QuartermasterManager.NormalizeToStoppable(QuartermasterManager.CapturedTimeMode.Value);
                    Campaign.Current.TimeControlMode = normalized;
                    ModLogger.Debug(LogCategory, $"Time control restored: {normalized}");
                }

                // Clear captured state from QuartermasterManager
                QuartermasterManager.CapturedTimeMode = null;
            }
            else if (settings?.PauseGameDuringMuster == true && Campaign.Current != null)
            {
                // Pause mode: unlock time controls
                Campaign.Current.SetTimeControlModeLock(false);
                ModLogger.Debug(LogCategory, "Time control unlocked on muster exit");
            }
        }

        #endregion

        #region Text Builders

        private string BuildIntroText()
        {
            if (_currentMuster == null)
            {
                return "Muster session unavailable.";
            }

            var sb = new StringBuilder();
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return new TextObject("{=muster_enlistment_unavailable}Enlistment data unavailable").ToString() + ".";
            }

            // Strategic context flavor text
            var flavorText = BuildStrategicContextFlavor();
            sb.AppendLine(flavorText);
            sb.AppendLine();

            // Service record section
            sb.AppendLine("<span style=\"Header\">_____ YOUR SERVICE RECORD _____</span>");
            sb.AppendLine();
            sb.Append(BuildServiceRecord());
            sb.AppendLine();

            // Events since last muster section (includes order outcomes)
            var eventsText = BuildPeriodSummary();
            if (!string.IsNullOrEmpty(eventsText))
            {
                sb.AppendLine("<span style=\"Header\">_____ EVENTS SINCE LAST MUSTER _____</span>");
                sb.AppendLine();
                sb.Append(eventsText);
                sb.AppendLine();
            }

            // Status line
            sb.Append(BuildStatusLine());

            return sb.ToString();
        }

        private string BuildStrategicContextFlavor()
        {
            var context = _currentMuster?.StrategicContext ?? "patrol_peacetime";
            var flavorText = GetStrategicContextFlavor(context);

            // Add army name if available
            var enlistment = EnlistmentBehavior.Instance;
            var lordParty = enlistment?.EnlistedLord?.PartyBelongedTo;
            if (lordParty?.Army != null)
            {
                var armyName = lordParty.Army.Name?.ToString();
                if (!string.IsNullOrEmpty(armyName))
                {
                    return $"{armyName}\n\n{flavorText}";
                }
            }

            return flavorText;
        }

        private string BuildServiceRecord()
        {
            var sb = new StringBuilder();
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return "Service data unavailable.";
            }

            // Rank and tier
            var tier = enlistment.EnlistmentTier;
            var rankName = GetRankName(tier);
            sb.AppendLine($"<span style=\"Label\">RANK:</span> {rankName} (Tier {tier})");

            // Days served
            var daysServed = enlistment.DaysServed;
            sb.AppendLine($"<span style=\"Label\">DAYS SERVED:</span> {Math.Round(daysServed)} days");

            // XP progress
            var currentXP = enlistment.EnlistmentXP;
            var xpNeeded = GetXPRequiredForNextTier(tier);
            if (xpNeeded > 0)
            {
                var percentage = (int)((currentXP / (float)xpNeeded) * 100);
                var nextRankName = GetRankName(tier + 1);
                var xpColor = percentage >= 75 ? "Success" : percentage >= 40 ? "Warning" : "Default";
                sb.AppendLine($"<span style=\"Label\">EXPERIENCE:</span> <span style=\"{xpColor}\">{currentXP:N0} / {xpNeeded:N0} XP ({percentage}% to {nextRankName})</span>");
            }
            else
            {
                // Max tier reached
                sb.AppendLine($"<span style=\"Label\">EXPERIENCE:</span> <span style=\"Success\">{currentXP:N0} XP (Maximum rank achieved)</span>");
            }

            // Health status
            var healthText = BuildHealthStatus();
            if (!string.IsNullOrEmpty(healthText))
            {
                sb.AppendLine(healthText);
            }

            // Baggage access
            var baggageAccess = BaggageTrainManager.Instance?.GetCurrentAccess() ?? BaggageAccessState.FullAccess;
            var baggageText = GetBaggageAccessText(baggageAccess);
            var baggageColor = baggageAccess == BaggageAccessState.FullAccess ? "Success" : "Warning";
            sb.AppendLine($"<span style=\"Label\">BAGGAGE ACCESS:</span> <span style=\"{baggageColor}\">{baggageText}</span> (muster window)");

            // Period XP gain
            var periodXP = currentXP - enlistment.XPAtLastMuster;
            if (periodXP > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"<span style=\"Label\">THIS PERIOD:</span> <span style=\"Success\">+{periodXP} XP</span> (battles, training, orders completed)");
            }


            return sb.ToString();
        }

        private string BuildHealthStatus()
        {
            var hero = Hero.MainHero;
            if (hero == null)
            {
                return string.Empty;
            }

            var healthPercent = (int)((hero.HitPoints / (float)hero.MaxHitPoints) * 100);

            if (healthPercent >= 100)
            {
                return "<span style=\"Label\">HEALTH STATUS:</span> <span style=\"Success\">Fit for duty</span>";
            }
            else if (healthPercent >= 50)
            {
                return $"<span style=\"Label\">HEALTH STATUS:</span> <span style=\"Warning\">Wounded ({healthPercent}%)</span> - Light duties";
            }
            else if (healthPercent >= 30)
            {
                return $"<span style=\"Label\">HEALTH STATUS:</span> <span style=\"Alert\">Badly Wounded ({healthPercent}%)</span> - Limited duties";
            }
            else
            {
                return $"<span style=\"Label\">HEALTH STATUS:</span> <span style=\"Alert\">Critically Wounded ({healthPercent}%)</span> - Excused from inspection";
            }
        }

        private string BuildPeriodSummary()
        {
            var newsBehavior = EnlistedNewsBehavior.Instance;
            if (newsBehavior == null)
            {
                return "Event records unavailable.";
            }

            var lastMusterDay = _currentMuster?.LastMusterDay ?? 0;

            var feedItems = newsBehavior.GetPersonalFeedSince(lastMusterDay);
            if (feedItems == null || feedItems.Count == 0)
            {
                return new TextObject("{=muster_quiet_period}A quiet period. Nothing of note occurred.").ToString();
            }

            var sb = new StringBuilder();
            var count = 0;
            var maxEvents = 8;

            foreach (var item in feedItems)
            {
                if (count >= maxEvents)
                {
                    break;
                }

                var headline = FormatFeedItemForMuster(item);
                if (!string.IsNullOrEmpty(headline))
                {
                    // Truncate very long headlines
                    if (headline.Length > 80)
                    {
                        headline = headline.Substring(0, 77) + "...";
                    }

                    sb.AppendLine($"• {headline}");
                    count++;
                }
            }

            if (count == 0)
            {
                return new TextObject("{=muster_quiet_period}A quiet period. Nothing of note occurred.").ToString();
            }

            return sb.ToString();
        }

        private string BuildStatusLine()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return string.Empty;
            }

            var pay = enlistment.PendingMusterPay;
            var supply = CompanySupplyManager.Instance?.TotalSupply ?? 100;

            // Unit status (simplified for now - could be enhanced with actual casualty tracking)
            var battles = CountBattlesThisPeriod();

            var payLabel = new TextObject("{=muster_status_pay}Pay").ToString();
            var supplyLabel = new TextObject("{=muster_status_supply}Supply").ToString();
            var battlesLabel = new TextObject("{=muster_status_battles}Battles").ToString();
            var denarsText = new TextObject("{=muster_denars}denars").ToString();
            
            return $"[{payLabel}: {pay:N0} {denarsText} | {supplyLabel}: {supply}% | {battlesLabel}: {battles}]";
        }

        private string BuildPayText()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return new TextObject("{=muster_enlistment_unavailable}Enlistment data unavailable").ToString() + ".";
            }

            var sb = new StringBuilder();
            var payIntro = new TextObject("{=muster_pay_intro}You step forward to the paymaster's table. He opens his ledger.").ToString();
            var payStatusHeader = new TextObject("{=muster_pay_status_header}PAY STATUS").ToString();
            var wagesOwedLabel = new TextObject("{=muster_wages_owed}WAGES OWED").ToString();
            var backpayLabel = new TextObject("{=muster_backpay_outstanding}BACKPAY OUTSTANDING").ToString();
            var lordTreasuryLabel = new TextObject("{=muster_lord_treasury}LORD'S TREASURY").ToString();
            var denarsText = new TextObject("{=muster_denars}denars").ToString();
            
            sb.AppendLine(payIntro);
            sb.AppendLine();
            sb.AppendLine($"<span style=\"Header\">_____ {payStatusHeader} _____</span>");
            sb.AppendLine();

            // Wages owed
            var wages = enlistment.PendingMusterPay;
            var wagesColor = wages > 0 ? "Success" : "Default";
            sb.AppendLine($"<span style=\"Label\">{wagesOwedLabel}:</span>           <span style=\"{wagesColor}\">{wages:N0} {denarsText}</span>");

            // Backpay outstanding
            var backpay = enlistment.OwedBackpay;
            if (backpay > 0)
            {
                sb.AppendLine($"<span style=\"Label\">{backpayLabel}:</span>  <span style=\"Warning\">{backpay:N0} {denarsText}</span>");
            }

            // Lord's treasury status
            var lordGold = enlistment.EnlistedLord?.Gold ?? 0;
            var wealthStatus = GetLordWealthStatus(lordGold);
            var wealthColor = lordGold > 20000 ? "Success" : lordGold > 5000 ? "Warning" : "Alert";
            sb.AppendLine($"<span style=\"Label\">{lordTreasuryLabel}:</span>      <span style=\"{wealthColor}\">{wealthStatus}</span>");

            // Special status indicators
            if (enlistment.IsOnProbation)
            {
                var probationText = new TextObject("{=muster_probation_rate}[Probation Rate] - Wages reduced 50%").ToString();
                sb.AppendLine();
                sb.AppendLine($"<span style=\"Warning\">{probationText}</span>");
            }

            if (enlistment.EnlistedLord?.PartyBelongedTo?.Army != null)
            {
                sb.AppendLine();
                sb.AppendLine("<span style=\"Success\">[Army Bonus] - Wages +20%</span>");
            }

            if (enlistment.PayTension >= 100)
            {
                sb.AppendLine();
                sb.AppendLine("<span style=\"Alert\">[MUTINY RISK] - Pay tension at maximum!</span>");
            }
            else if (enlistment.PayTension >= 60)
            {
                sb.AppendLine();
                sb.AppendLine($"<span style=\"Warning\">[Pay Tension: {enlistment.PayTension}/100] (High)</span>");
            }

            // Pending discharge note
            if (enlistment.IsPendingDischarge)
            {
                sb.AppendLine();
                sb.AppendLine("<span style=\"Warning\">[Final Muster] - Discharge processing</span>");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets human-readable wealth status text for the lord's treasury.
        /// Thresholds match pay-system.md spec: Wealthy >50k, Comfortable >20k,
        /// Struggling >5k, Poor >1k, Broke ≤1k.
        /// </summary>
        private string GetLordWealthStatus(int lordGold)
        {
            if (lordGold > 50000)
            {
                return "Wealthy";
            }
            if (lordGold > 20000)
            {
                return "Comfortable";
            }
            if (lordGold > 5000)
            {
                return "Struggling";
            }
            if (lordGold > 1000)
            {
                return "Poor";
            }
            return "Broke";
        }

        private string BuildPromotionText()
        {
            if (_currentMuster == null)
            {
                return "[No promotion data]";
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                var unavailable = new TextObject("{=muster_enlistment_unavailable}Enlistment data unavailable").ToString();
                return $"[{unavailable}]";
            }

            var sb = new StringBuilder();

            // Formal announcement
            var prevRank = GetRankName(_currentMuster.PreviousTier);
            var currentRank = GetRankName(_currentMuster.CurrentTier);
            var daysSincePromotion = _currentMuster.MusterDay - _currentMuster.PromotionDay;

            sb.AppendLine("The captain addresses the formation. Your promotion is formally");
            sb.AppendLine("recognized before the assembled company.");
            sb.AppendLine();

            // Promotion announcement text
            if (daysSincePromotion == 0)
            {
                sb.AppendLine($"\"Earlier today, this soldier was promoted to {currentRank}");
                sb.AppendLine("for distinguished service. The rank has been earned and noted.\"");
            }
            else if (daysSincePromotion == 1)
            {
                sb.AppendLine($"\"Yesterday, this soldier was promoted to {currentRank}");
                sb.AppendLine("for distinguished service. The rank has been earned and noted.\"");
            }
            else
            {
                sb.AppendLine($"\"On Day {_currentMuster.PromotionDay} of this campaign, this soldier was promoted to {currentRank}");
                sb.AppendLine("for distinguished service. The rank has been earned and noted.\"");
            }

            sb.AppendLine();
            sb.AppendLine("The men salute. Your promotion is now part of the official record.");
            sb.AppendLine();
            sb.AppendLine("_____ PROMOTION DETAILS _____");
            sb.AppendLine();

            // Promotion details
            sb.AppendLine($"Promoted From:       {prevRank} (Tier {_currentMuster.PreviousTier})");
            sb.AppendLine($"Current Rank:        {currentRank} (Tier {_currentMuster.CurrentTier})");

            if (daysSincePromotion == 0)
            {
                sb.AppendLine($"Date of Promotion:   Day {_currentMuster.PromotionDay} (earlier today)");
            }
            else
            {
                sb.AppendLine($"Date of Promotion:   Day {_currentMuster.PromotionDay} ({daysSincePromotion} days ago)");
            }

            sb.AppendLine();

            // Benefits unlocked at promotion
            sb.AppendLine("Benefits Unlocked at Promotion:");

            // Wage increase
            var prevWage = CalculateTierWage(_currentMuster.PreviousTier);
            var currentWage = CalculateTierWage(_currentMuster.CurrentTier);
            sb.AppendLine($"• Wages increased: {prevWage} → {currentWage} denars/day");

            // Equipment tier access
            sb.AppendLine($"• Tier {_currentMuster.CurrentTier} equipment available from Quartermaster");

            // Authority changes
            var authority = GetTierAuthority(_currentMuster.CurrentTier);
            if (!string.IsNullOrEmpty(authority))
            {
                sb.AppendLine($"• New authority: {authority}");
            }

            // Enhanced reputation gains (T4+)
            if (_currentMuster.CurrentTier >= 4 && _currentMuster.PreviousTier < 4)
            {
                sb.AppendLine("• Enhanced reputation gains from successful orders");
            }

            // Special benefits for specific tiers
            if (_currentMuster.CurrentTier >= 7)
            {
                sb.AppendLine("• Officer rations (exempt from standard ration exchange)");
                sb.AppendLine("• Daily baggage access during campaign");
            }

            if (_currentMuster.CurrentTier == 7)
            {
                sb.AppendLine($"• Commander's retinue granted: {RetinueManager.CommanderCapacity1} soldiers");
            }
            else if (_currentMuster.CurrentTier == 8)
            {
                sb.AppendLine($"• Retinue capacity expanded to {RetinueManager.CommanderCapacity2} soldiers");
            }
            else if (_currentMuster.CurrentTier == 9)
            {
                sb.AppendLine($"• Retinue capacity expanded to {RetinueManager.CommanderCapacity3} soldiers");
            }

            sb.AppendLine();

            // New abilities unlocked (camp decisions, etc.)
            // For multi-tier promotions (e.g., T5→T7), show abilities from all gained tiers
            var abilities = new List<string>();
            for (int tier = _currentMuster.PreviousTier + 1; tier <= _currentMuster.CurrentTier; tier++)
            {
                abilities.AddRange(GetTierAbilities(tier));
            }

            if (abilities.Count > 0)
            {
                sb.AppendLine("New Abilities Unlocked:");
                foreach (var ability in abilities)
                {
                    sb.AppendLine($"• {ability}");
                }
                sb.AppendLine();
            }

            // Current XP progress to next rank
            // XP thresholds are cumulative totals, so we need to calculate progress within current tier
            var currentXP = enlistment.EnlistmentXP;
            var currentTierThreshold = GetXPThresholdForTier(_currentMuster.CurrentTier);
            var nextTierThreshold = GetXPThresholdForTier(_currentMuster.CurrentTier + 1);

            if (nextTierThreshold > 0)
            {
                // XP progress = total XP minus current tier threshold
                var xpProgress = currentXP - currentTierThreshold;
                // XP needed for next tier = difference between thresholds
                var xpToNext = nextTierThreshold - currentTierThreshold;
                var percentage = xpToNext > 0 ? (int)((xpProgress / (float)xpToNext) * 100) : 0;
                var nextRank = GetRankName(_currentMuster.CurrentTier + 1);
                sb.AppendLine($"Current XP Progress: {xpProgress:N0} / {xpToNext:N0} XP to {nextRank} ({percentage}%)");
            }
            else
            {
                // Max tier reached
                sb.AppendLine("Current XP Progress: Maximum rank achieved");
                sb.AppendLine();
                sb.AppendLine("You have reached the pinnacle of military service. No further");
                sb.AppendLine("promotion is possible—your place in history is secured.");
            }

            sb.AppendLine();
            sb.AppendLine("The promotion proving event occurred when you earned it. This is a");
            sb.AppendLine("formal acknowledgment for your service record.");

            // Reminder about visiting QM if new equipment available
            if (_currentMuster.CurrentTier < 9) // Not at max tier
            {
                sb.AppendLine();
                sb.AppendLine("Visit the Quartermaster to review newly unlocked equipment.");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Calculates approximate daily wage for a given tier (simplified for display).
        /// </summary>
        private int CalculateTierWage(int tier)
        {
            // Simplified wage calculation: base 10 + 5 per tier
            // This matches the formula in wage_system config
            return 10 + (tier * 5);
        }

        /// <summary>
        /// Gets the authority description for a given tier.
        /// </summary>
        private string GetTierAuthority(int tier)
        {
            return tier switch
            {
                2 => "Select formation assignment",
                3 => "Request extra training sessions",
                4 => "Lead small patrols",
                5 => "Organize squad drills, recommend promotions",
                6 => "Act as NCO, train recruits",
                7 => "Command retinue, halt column for baggage access",
                8 => "Lead independent operations",
                9 => "Strategic planning, major tactical decisions",
                _ => null
            };
        }

        /// <summary>
        /// Gets the list of abilities unlocked at a given tier.
        /// </summary>
        private List<string> GetTierAbilities(int tier)
        {
            var abilities = new List<string>();

            switch (tier)
            {
                case 2:
                    abilities.Add("Formation selection (permanent choice)");
                    break;
                case 3:
                    abilities.Add("Request Extra Training (camp decision)");
                    abilities.Add("Eligible for Green Recruit mentoring events");
                    break;
                case 4:
                    abilities.Add("Organize Squad Drill (camp decision)");
                    abilities.Add("Lead patrol orders");
                    break;
                case 5:
                    abilities.Add("Recommend Comrades for Promotion (social decision)");
                    abilities.Add("Daily baggage access window");
                    break;
                case 6:
                    abilities.Add("Train New Recruits (camp decision)");
                    abilities.Add("Enhanced order authority");
                    break;
                case 7:
                    abilities.Add("Command personal retinue of 20 soldiers");
                    abilities.Add("Halt column for baggage access");
                    abilities.Add("Request emergency supplies");
                    break;
                case 8:
                    abilities.Add("Expanded retinue (30 soldiers)");
                    abilities.Add("Request lord reinforcements");
                    break;
                case 9:
                    abilities.Add("Elite retinue (40 soldiers)");
                    abilities.Add("Influence strategic decisions");
                    break;
            }

            return abilities;
        }

        private string BuildRetinueText()
        {
            var sb = new StringBuilder();

            var retinueMgr = RetinueManager.Instance;
            var enlistment = EnlistmentBehavior.Instance;

            if (retinueMgr == null || enlistment == null)
            {
                sb.AppendLine("[Retinue system unavailable]");
                ModLogger.Warn(LogCategory, "BuildRetinueText: RetinueManager or EnlistmentBehavior unavailable");
                return sb.ToString();
            }

            var state = retinueMgr.State;
            if (state == null || !state.HasRetinue)
            {
                // Edge case: Just promoted to T7 but haven't formed retinue yet
                if (enlistment.EnlistmentTier == 7)
                {
                    sb.AppendLine("You have been granted the authority to command a personal retinue.");
                    sb.AppendLine();
                    sb.AppendLine($"Retinue Capacity: 0 / {RetinueManager.CommanderCapacity1} soldiers");
                    sb.AppendLine();
                    sb.AppendLine("Visit your lord to select your retinue type and begin recruitment.");
                }
                else
                {
                    sb.AppendLine("Your retinue has been lost. All soldiers have fallen.");
                    sb.AppendLine();
                    sb.AppendLine("Visit your lord to rebuild your retinue when ready.");
                }

                return sb.ToString();
            }

            // Standard retinue muster display
            sb.AppendLine("Your soldiers form up for inspection. You walk the line, checking");
            sb.AppendLine("each man's gear and bearing. They salute as you pass.");
            sb.AppendLine();
            sb.AppendLine("_____ RETINUE STRENGTH _____");
            sb.AppendLine();

            // Current strength vs capacity
            var capacity = RetinueManager.GetTierCapacity(enlistment.EnlistmentTier);
            var current = state.TotalSoldiers;

            if (_currentMuster != null)
            {
                _currentMuster.RetinueStrength = current;
                _currentMuster.RetinueCapacity = capacity;
            }

            sb.AppendLine($"Current Strength:    {current} / {capacity} soldiers");

            // Morale (based on RetinueLoyalty)
            var loyalty = state.RetinueLoyalty;
            var moraleDesc = GetMoraleDescription(loyalty);
            var moraleColor = loyalty < 50 ? "⚠️" : "";
            sb.AppendLine($"Morale:              {moraleColor}{moraleDesc} ({loyalty})");

            // Equipment status (simplified for now)
            var equipmentStatus = GetEquipmentStatus(state);
            sb.AppendLine($"Equipment:           {equipmentStatus}");

            // Experience (based on battles participated)
            var expDesc = GetExperienceDescription(state.BattlesParticipated);
            sb.AppendLine($"Experience:          {expDesc} ({state.BattlesParticipated} battles)");

            sb.AppendLine();
            sb.AppendLine("_____ THIS PERIOD _____");
            sb.AppendLine();

            // Query casualties from news feed
            var newsBehavior = EnlistedNewsBehavior.Instance;
            int killed = 0;
            int wounded = 0;
            List<string> fallenNames = new List<string>();

            if (newsBehavior != null && _currentMuster != null)
            {
                // Check personal feed for retinue casualty reports since last muster
                var periodFeed = newsBehavior.GetPersonalFeedSince(_currentMuster.LastMusterDay);

                foreach (var item in periodFeed)
                {
                    // Get the formatted text for pattern matching
                    var headline = FormatFeedItemForMuster(item);
                    if (string.IsNullOrEmpty(headline))
                    {
                        continue;
                    }

                    // Look for retinue casualty entries
                    // Format: "Your retinue suffered X killed and Y wounded" or "Your retinue lost X soldier(s)"
                    if (headline.IndexOf("retinue", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Extract killed count
                        var killedMatch = Regex.Match(headline, @"(\d+)\s+killed");
                        if (killedMatch.Success && int.TryParse(killedMatch.Groups[1].Value, out int kCount))
                        {
                            killed += kCount;
                        }
                        else
                        {
                            // Try alternate format: "lost X soldier(s)"
                            var lostMatch = Regex.Match(headline, @"lost\s+(\d+)\s+soldier");
                            if (lostMatch.Success && int.TryParse(lostMatch.Groups[1].Value, out int lCount))
                            {
                                killed += lCount;
                            }
                        }

                        // Extract wounded count
                        var woundedMatch = Regex.Match(headline, @"(\d+)\s+wounded");
                        if (woundedMatch.Success && int.TryParse(woundedMatch.Groups[1].Value, out int wCount))
                        {
                            wounded += wCount;
                        }
                    }

                    // Look for named veteran deaths
                    // Format: "X the Y has fallen in battle"
                    if (headline.IndexOf("has fallen in battle", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // Extract name from headline
                        var nameMatch = Regex.Match(headline, @"^([^\s]+(?:\s+the\s+[^\s]+)?)\s+has\s+fallen");
                        if (nameMatch.Success)
                        {
                            fallenNames.Add(nameMatch.Groups[1].Value.Trim());
                        }
                    }
                }
            }

            // Store in muster state for retinue recruitment option
            if (_currentMuster != null)
            {
                _currentMuster.RetinueCasualties = killed;
                _currentMuster.FallenRetinueNames = fallenNames;
            }

            // Display casualties in spec format: "X killed, Y wounded"
            if (killed > 0 || wounded > 0)
            {
                var casualtyParts = new List<string>();
                if (killed > 0)
                {
                    casualtyParts.Add($"{killed} killed");
                }
                if (wounded > 0)
                {
                    casualtyParts.Add($"{wounded} wounded");
                }
                sb.AppendLine($"Casualties:          {string.Join(", ", casualtyParts)}");
                sb.AppendLine($"Recruits Added:      0");
                sb.AppendLine($"Desertions:          0");
            }
            else
            {
                sb.AppendLine("Casualties:          None");
                sb.AppendLine($"Recruits Added:      0");
                sb.AppendLine($"Desertions:          0");
                sb.AppendLine();
                sb.AppendLine("Your retinue stands ready. No losses this period.");
            }

            // Show fallen soldiers with names
            if (fallenNames.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("_____ FALLEN THIS PERIOD _____");
                sb.AppendLine();

                foreach (var name in fallenNames)
                {
                    sb.AppendLine($"• {name} - Killed in battle");
                }
            }

            // Show recruitment availability
            var openings = capacity - current;
            if (openings > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[{openings} recruitment slot{(openings == 1 ? "" : "s")} available]");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets morale description from loyalty value.
        /// </summary>
        private string GetMoraleDescription(int loyalty)
        {
            return loyalty switch
            {
                >= 80 => "Excellent",
                >= 65 => "High",
                >= 50 => "Steady",
                >= 35 => "Shaky",
                >= 20 => "Low",
                _ => "Critical"
            };
        }

        /// <summary>
        /// Gets equipment status description (simplified).
        /// </summary>
        private string GetEquipmentStatus(RetinueState state)
        {
            // Simplified: based on troop tier quality
            // In the future, this could check actual equipment values
            if (state == null || state.TotalSoldiers == 0)
            {
                return "N/A";
            }

            // Basic heuristic: if we have troops, they're equipped
            return "Serviceable";
        }

        /// <summary>
        /// Gets experience description from battles participated.
        /// </summary>
        private string GetExperienceDescription(int battles)
        {
            return battles switch
            {
                >= 20 => "Battle-hardened veterans",
                >= 10 => "Experienced troops",
                >= 5 => "Seasoned soldiers",
                >= 2 => "Tested in combat",
                _ => "Green but willing"
            };
        }

        private string BuildMusterCompleteText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Muster is dismissed. The sergeants release the company.");
            sb.AppendLine();

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || _currentMuster == null)
            {
                sb.AppendLine("[Muster data unavailable]");
                return sb.ToString();
            }

            // Section 1: Muster Outcomes
            sb.AppendLine("<span style=\"Header\">_____ MUSTER OUTCOMES _____</span>");
            sb.AppendLine();

            BuildMusterOutcomesSection(sb, enlistment);

            sb.AppendLine();

            // Section 2: Rank & Progression
            sb.AppendLine("<span style=\"Header\">_____ RANK & PROGRESSION _____</span>");
            sb.AppendLine();

            BuildRankProgressionSection(sb, enlistment);

            sb.AppendLine();

            // Section 3: Period Summary
            var periodDays = _currentMuster.MusterDay - _currentMuster.LastMusterDay;
            sb.AppendLine($"<span style=\"Header\">_____ PERIOD SUMMARY ({periodDays} Days) _____</span>");
            sb.AppendLine();

            BuildPeriodSummarySection(sb, enlistment);

            return sb.ToString();
        }

        private void BuildMusterOutcomesSection(StringBuilder sb, EnlistmentBehavior enlistment)
        {
            // Pay Received
            var payAmount = _currentMuster.PayReceived;
            var payType = _currentMuster.PayOutcome switch
            {
                "full" => "Full Payment",
                "partial" => "Partial Payment",
                "iou" => "IOU - Pay Delayed",
                "corruption" => "Corruption Deal",
                var outcome when outcome != null && outcome.StartsWith("qm_deal_success") => "Quartermaster's Deal",
                var outcome when outcome != null && outcome.StartsWith("qm_deal_failure") => "Quartermaster's Deal (No Equipment)",
                _ => "Unknown"
            };
            var payColor = _currentMuster.PayOutcome == "full" ? "Success" : _currentMuster.PayOutcome == "iou" ? "Alert" : "Warning";
            sb.AppendLine($"<span style=\"Label\">PAY RECEIVED:</span>         <span style=\"{payColor}\">{payAmount} denars ({payType})</span>");

            // QM Deal Equipment (if applicable)
            if (_currentMuster.PayOutcome != null && _currentMuster.PayOutcome.StartsWith("qm_deal_success:"))
            {
                var parts = _currentMuster.PayOutcome.Split(':');
                if (parts.Length >= 3)
                {
                    var itemId = parts[2];
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                    var itemName = item?.Name?.ToString() ?? itemId;
                    sb.AppendLine($"<span style=\"Label\">QM DEAL ITEM:</span>         <span style=\"Success\">{itemName}</span>");
                }
            }

            // Ration Issued
            var rationText = _currentMuster.RationOutcome switch
            {
                "issued" => GetRationItemName(enlistment),
                "none" => "None - low supply",
                "officer_exempt" => "Officer Exempt",
                _ => "Not processed"
            };
            var rationColor = _currentMuster.RationOutcome == "issued" ? "Success" : _currentMuster.RationOutcome == "none" ? "Alert" : "Default";
            sb.AppendLine($"<span style=\"Label\">RATION ISSUED:</span>        <span style=\"{rationColor}\">{rationText}</span>");

            // Supply Status - use CompanyNeeds for proper fallback handling
            var supplyPct = EnlistmentBehavior.Instance?.CompanyNeeds?.Supplies ?? 60;
            var supplyStatus = supplyPct >= 50 ? "Adequate" : supplyPct >= 20 ? "Low" : "Critical";
            var supplyColor = supplyPct >= 50 ? "Success" : supplyPct >= 20 ? "Warning" : "Alert";
            sb.AppendLine($"<span style=\"Label\">SUPPLY STATUS:</span>        <span style=\"{supplyColor}\">{supplyPct}% - {supplyStatus} condition</span>");
        }

        private void BuildRankProgressionSection(StringBuilder sb, EnlistmentBehavior enlistment)
        {
            var currentTier = enlistment.EnlistmentTier;
            var rankName = enlistment.GetRankName(currentTier) ?? $"Tier {currentTier}";

            sb.AppendLine($"<span style=\"Label\">CURRENT RANK:</span>         {rankName} (Tier {currentTier})");

            // XP this period - calculate from current XP minus XP at last muster
            var currentXP = enlistment.EnlistmentXP;
            var xpAtLastMuster = enlistment.XPAtLastMuster;
            var xpThisPeriod = Math.Max(0, currentXP - xpAtLastMuster);

            // Get XP requirements from config
            var tierXpRequirements = Mod.Core.Config.ConfigurationManager.GetTierXpRequirements();
            var xpForNextRank = currentTier < tierXpRequirements.Length ? tierXpRequirements[currentTier] : 0;

            if (currentTier >= 9)
            {
                sb.AppendLine($"<span style=\"Label\">XP THIS PERIOD:</span>       <span style=\"Success\">+{xpThisPeriod} XP</span>");
                sb.AppendLine($"<span style=\"Label\">TOTAL EXPERIENCE:</span>     <span style=\"Success\">{currentXP} XP (Maximum Rank)</span>");
                sb.AppendLine($"<span style=\"Label\">STATUS:</span>               <span style=\"Success\">Pinnacle of military service reached</span>");
            }
            else
            {
                var xpPercent = xpForNextRank > 0 ? (int)((float)xpThisPeriod / (xpForNextRank - xpAtLastMuster) * 100) : 0;
                var xpPeriodColor = xpThisPeriod > 0 ? "Success" : "Default";
                sb.AppendLine($"<span style=\"Label\">XP THIS PERIOD:</span>       <span style=\"{xpPeriodColor}\">+{xpThisPeriod} XP ({xpPercent}%)</span>");

                var nextRankName = enlistment.GetRankName(currentTier + 1) ?? $"Tier {currentTier + 1}";
                var progressPercent = xpForNextRank > 0 ? (int)((float)currentXP / xpForNextRank * 100) : 0;
                var xpNeeded = Math.Max(0, xpForNextRank - currentXP);

                var progressColor = progressPercent >= 75 ? "Success" : progressPercent >= 40 ? "Warning" : "Default";
                sb.AppendLine($"<span style=\"Label\">TOTAL EXPERIENCE:</span>     <span style=\"{progressColor}\">{currentXP} / {xpForNextRank} XP to {nextRankName} ({progressPercent}%)</span>");

                // Estimate days to promotion based on XP rate
                var periodDays = Math.Max(1, _currentMuster.MusterDay - _currentMuster.LastMusterDay);
                var dailyRate = xpThisPeriod / (float)periodDays;
                var estimatedDays = dailyRate > 0 ? (int)(xpNeeded / dailyRate) : 999;

                // Add multi-factor requirements check
                BuildPromotionRequirementsCheck(sb, enlistment, currentTier, nextRankName, estimatedDays);
            }

            // XP Sources breakdown
            sb.AppendLine();
            sb.AppendLine("<span style=\"Label\">XP SOURCES THIS PERIOD:</span>");

            var xpSources = enlistment.GetXPSourcesThisPeriod();
            if (xpSources != null && xpSources.Count > 0)
            {
                var topSources = xpSources.OrderByDescending(kvp => kvp.Value).Take(5);
                foreach (var source in topSources)
                {
                    var sourceName = FormatXPSourceName(source.Key);
                    sb.AppendLine($"• {sourceName}: <span style=\"Success\">+{source.Value} XP</span>");
                }
            }
            else
            {
                // Fallback: all XP from daily service
                sb.AppendLine($"• Daily service: <span style=\"Success\">+{xpThisPeriod} XP</span>");
            }
        }

        /// <summary>
        /// Builds the multi-factor promotion requirements checklist.
        /// Shows players exactly what requirements they've met and what's blocking promotion.
        /// </summary>
        private void BuildPromotionRequirementsCheck(StringBuilder sb, EnlistmentBehavior enlistment, int currentTier, string nextRankName, int estimatedDays)
        {
            var targetTier = currentTier + 1;
            var req = PromotionRequirements.GetForTier(targetTier);
            var escalation = EscalationManager.Instance;

            // Get current values for each requirement
            var tierXp = Mod.Core.Config.ConfigurationManager.GetTierXpRequirements();
            var requiredXp = currentTier < tierXp.Length ? tierXp[currentTier] : tierXp[tierXp.Length - 1];
            var currentXp = enlistment.EnlistmentXP;
            var daysInRank = enlistment.DaysInRank;
            var battles = enlistment.BattlesSurvived;
            var scrutiny = escalation?.State?.Scrutiny ?? 0;  // Discipline/Soldier rep removed, using scrutiny
            var leaderRelation = enlistment.EnlistedLord?.GetRelationWithPlayer() ?? 0;

            // Check each requirement
            var xpReady = currentXp >= requiredXp;
            var daysReady = daysInRank >= req.DaysInRank;
            var battlesReady = battles >= req.BattlesRequired;
            var leaderRelReady = leaderRelation >= req.MinLeaderRelation;
            var scrutinyReady = scrutiny < req.MaxScrutiny;  // Discipline merged into Scrutiny (0-100 scale)

            var allReady = xpReady && daysReady && battlesReady && leaderRelReady && scrutinyReady;

            // Build the section
            sb.AppendLine();
            sb.AppendLine($"<span style=\"Header\">_____ NEXT RANK: {nextRankName.ToUpperInvariant()} (T{targetTier}) _____</span>");
            sb.AppendLine();

            // Estimate display
            if (allReady)
            {
                sb.AppendLine($"<span style=\"Label\">STATUS:</span>               <span style=\"Success\">All requirements met - promotion imminent!</span>");
            }
            else
            {
                sb.AppendLine($"<span style=\"Label\">ESTIMATED:</span>            ~{estimatedDays} days at current pace");
            }

            sb.AppendLine();
            sb.AppendLine("<span style=\"Label\">REQUIREMENTS:</span>");

            // Experience
            var xpIcon = xpReady ? "✓" : "✗";
            var xpColor = xpReady ? "Success" : "Alert";
            var xpStatus = xpReady ? "(Ready)" : $"(Need +{requiredXp - currentXp})";
            sb.AppendLine($"  <span style=\"{xpColor}\">{xpIcon}</span> Experience:      {currentXp:N0}/{requiredXp:N0} XP {xpStatus}");

            // Days in Rank
            var daysIcon = daysReady ? "✓" : "✗";
            var daysColor = daysReady ? "Success" : "Alert";
            var daysStatus = daysReady ? "(Ready)" : $"(Need +{req.DaysInRank - daysInRank} days)";
            sb.AppendLine($"  <span style=\"{daysColor}\">{daysIcon}</span> Time in Rank:    {daysInRank}/{req.DaysInRank} days {daysStatus}");

            // Battles
            var battlesIcon = battlesReady ? "✓" : "✗";
            var battlesColor = battlesReady ? "Success" : "Alert";
            var battlesStatus = battlesReady ? "(Ready)" : $"(Need +{req.BattlesRequired - battles})";
            sb.AppendLine($"  <span style=\"{battlesColor}\">{battlesIcon}</span> Battles Fought:  {battles}/{req.BattlesRequired} battles {battlesStatus}");

            // Leader Relation
            var leaderIcon = leaderRelReady ? "✓" : "✗";
            var leaderColor = leaderRelReady ? "Success" : "Alert";
            var leaderStatus = leaderRelReady ? "(Ready)" : $"(Need +{req.MinLeaderRelation - leaderRelation})";
            sb.AppendLine($"  <span style=\"{leaderColor}\">{leaderIcon}</span> Leader Relation: {leaderRelation}/{req.MinLeaderRelation} {leaderStatus}");

            // Scrutiny (lower is better, 0-100 scale)
            var scrutIcon = scrutinyReady ? "✓" : "✗";
            var scrutColor = scrutinyReady ? "Success" : "Alert";
            var scrutStatus = scrutinyReady ? "(Safe)" : "(Too high!)";
            sb.AppendLine($"  <span style=\"{scrutColor}\">{scrutIcon}</span> Scrutiny:       {scrutiny}/{req.MaxScrutiny} max {scrutStatus}");

            // Show blocker advice if not ready
            if (!allReady)
            {
                sb.AppendLine();
                var blockers = new List<string>();

                if (!xpReady)
                {
                    blockers.Add("Gain more XP from orders, combat, and training");
                }
                if (!daysReady)
                {
                    blockers.Add("Continue serving to accumulate time in rank");
                }
                if (!battlesReady)
                {
                    blockers.Add("Participate in more battles (not reserve duty)");
                }
                if (!leaderRelReady)
                {
                    blockers.Add("Build relation with your lord via successful orders");
                }
                if (!scrutinyReady)
                {
                    blockers.Add("Reduce scrutiny through good behavior and loyalty");
                }

                sb.AppendLine($"<span style=\"Warning\">BLOCKERS:</span> {string.Join("; ", blockers)}.");
            }
        }

        private void BuildPeriodSummarySection(StringBuilder sb, EnlistmentBehavior enlistment)
        {
            // Net Gold
            var goldGained = _currentMuster.PayReceived;
            var goldColor = goldGained > 0 ? "Success" : "Default";
            sb.AppendLine($"<span style=\"Label\">NET GOLD:</span>            <span style=\"{goldColor}\">+{goldGained:N0} denars</span> ({goldGained} pay)");

            // Reputation Changes
            // Note: We don't track reputation changes across the period currently,
            // so this is a placeholder for future enhancement
            sb.AppendLine($"<span style=\"Label\">REPUTATION CHANGES:</span>  [Tracked in future update]");

            // Skills Improved
            sb.AppendLine($"<span style=\"Label\">SKILLS IMPROVED:</span>     [Tracked in future update]");

            // Items Acquired
            sb.AppendLine($"<span style=\"Label\">ITEMS ACQUIRED:</span>      [Tracked in future update]");

            // Unit Status
            var currentTier = enlistment.EnlistmentTier;
            var retinueCount = RetinueManager.Instance?.State?.TroopCounts?.Values.Sum() ?? 0;
            var casualties = _currentMuster.RetinueCasualties;
            var moraleText = GetMoraleText();

            if (currentTier >= 7 && retinueCount > 0)
            {
                var casualtyColor = casualties == 0 ? "Success" : casualties < 5 ? "Warning" : "Alert";
                sb.AppendLine($"<span style=\"Label\">UNIT STATUS:</span>         <span style=\"{casualtyColor}\">{casualties} casualties</span>, morale {moraleText}");
            }
            else
            {
                sb.AppendLine($"<span style=\"Label\">UNIT STATUS:</span>         [No retinue under command]");
            }

            // Next Muster
            var paydayInterval = Mod.Core.Config.ConfigurationManager.LoadFinanceConfig()?.PaydayIntervalDays ?? 12;
            var nextMusterDay = _currentMuster.MusterDay + paydayInterval;
            sb.AppendLine($"<span style=\"Label\">NEXT MUSTER:</span>         Day {nextMusterDay} ({paydayInterval} days)");
        }

        private string GetRationItemName(EnlistmentBehavior enlistment)
        {
            // Try to get the ration item name from the last issued ration
            // This is a best-effort approach; if we can't determine it, show generic text
            var tier = enlistment.EnlistmentTier;
            return tier switch
            {
                >= 5 => "Officer Ration",
                >= 3 => "Field Ration",
                _ => "Basic Ration"
            };
        }

        private string FormatXPSourceName(string sourceKey)
        {
            return sourceKey switch
            {
                "daily_service" => "Daily service",
                "battles_won" => "Battles won",
                "orders_completed" => "Orders completed",
                "training" => "Training",
                "reputation" => "Officer reputation gains",
                "events" => "Event outcomes",
                _ => sourceKey
            };
        }

        private string GetMoraleText()
        {
            var morale = RetinueManager.Instance?.State?.RetinueLoyalty ?? 50;
            return morale switch
            {
                >= 75 => "high",
                >= 50 => "steady",
                >= 25 => "wavering",
                _ => "poor"
            };
        }

        private static string GetStrategicContextFlavor(string context)
        {
            return context switch
            {
                "coordinated_offensive" => "The host prepares for the Grand Campaign. Spirits are high as the sergeants call the muster.",
                "desperate_defense" => "The realm bleeds. Muster is brief—every sword is needed on the line.",
                "siege_operation" => "The walls loom above. Muster proceeds in the muddy siege lines.",
                "raid_operation" => "Smoke rises in the distance. The company assembles between raids.",
                "patrol_peacetime" => "A quiet day on the march. The sergeants call muster at the usual hour.",
                "garrison_duty" => "Within the castle walls, the garrison assembles for pay day.",
                "winter_camp" => "Snow blankets the camp. Men huddle close as the sergeants call muster.",
                "recruitment_drive" => "Fresh faces join the ranks. The company swells as muster is called.",
                _ => "The sergeants call the muster. Your company assembles in formation."
            };
        }

        #endregion

        #region Baggage Check Handlers

        #endregion

        #region Inspection Handlers

        /// <summary>
        /// Resolves perfect attention inspection (OneHanded 30+).
        /// Awards +10 OneHanded XP, +6 Officer Rep, +3 Soldier Rep.
        /// </summary>
        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the localized rank name for a given tier.
        /// Falls back to generic tier names if culture-specific progression config unavailable.
        /// </summary>
        private string GetRankName(int tier)
        {
            // Generic fallback rank names
            return tier switch
            {
                1 => "Follower",
                2 => "Recruit",
                3 => "Free Sword",
                4 => "Sergeant",
                5 => "Veteran",
                6 => "Blade",
                7 => "Captain",
                8 => "Lieutenant",
                9 => "Commander",
                _ => $"Tier {tier}"
            };

            // TODO: Query progression_config.json for culture-specific rank names
        }

        /// <summary>
        /// Gets the XP required to reach the next tier.
        /// Returns 0 if already at max tier.
        /// </summary>
        /// <summary>
        /// Gets the cumulative XP threshold to ENTER a specific tier.
        /// E.g., GetXPThresholdForTier(3) returns 3000 (total XP needed to become T3).
        /// </summary>
        private int GetXPThresholdForTier(int tier)
        {
            // XP thresholds are cumulative totals from progression_config.json
            return tier switch
            {
                <= 1 => 0,      // T1 starts at 0
                2 => 800,       // T2 requires 800 total
                3 => 3000,      // T3 requires 3000 total
                4 => 6000,      // T4 requires 6000 total
                5 => 11000,     // T5 requires 11000 total
                6 => 19000,     // T6 requires 19000 total
                7 => 30000,     // T7 requires 30000 total
                8 => 45000,     // T8 requires 45000 total
                9 => 65000,     // T9 requires 65000 total
                _ => 0
            };
        }

        /// <summary>
        /// Gets the XP needed to advance from current tier to next tier.
        /// Returns 0 if at max tier.
        /// </summary>
        private int GetXPRequiredForNextTier(int currentTier)
        {
            var currentThreshold = GetXPThresholdForTier(currentTier);
            var nextThreshold = GetXPThresholdForTier(currentTier + 1);
            return nextThreshold > currentThreshold ? nextThreshold : 0;
        }

        /// <summary>
        /// Converts baggage access state to human-readable text.
        /// </summary>
        private string GetBaggageAccessText(BaggageAccessState state)
        {
            return state switch
            {
                BaggageAccessState.FullAccess => "Available",
                BaggageAccessState.TemporaryAccess => "Temporary",
                BaggageAccessState.NoAccess => "Unavailable",
                BaggageAccessState.Locked => "Locked",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Formats a dispatch item for display in the muster period summary.
        /// Simplifies headlines and removes redundant prefixes.
        /// </summary>
        private string FormatFeedItemForMuster(DispatchItem item)
        {
            // DispatchItem is a struct, so check for default values instead
            if (item.DayCreated == 0 && string.IsNullOrEmpty(item.HeadlineKey))
            {
                return string.Empty;
            }

            // Use HeadlineKey directly as it contains the formatted text
            var headline = item.HeadlineKey ?? string.Empty;

            // Remove common prefixes to make it more concise
            headline = headline.Replace("The company ", "");
            headline = headline.Replace("Your lord's army ", "Army ");

            return headline.Trim();
        }

        /// <summary>
        /// Displays discharge outcome messages to the player showing band, severance, pension, and relation changes.
        /// </summary>
        private void ShowDischargeOutcome(string dischargeBand, bool isRetirement, int daysServed, int pensionPerDay)
        {
            var config = Enlisted.Mod.Core.Config.ConfigurationManager.LoadRetirementConfig();
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            // Determine discharge type text
            var dischargeType = isRetirement ? "Retirement" : "Discharge";
            
            // Determine band description and rewards
            string bandText;
            string rewardsText;
            int severance;
            int lordRelation;

            switch (dischargeBand)
            {
                case "heroic":
                case "veteran":
                    bandText = "VETERAN";
                    severance = config?.SeveranceVeteran ?? 3000;
                    lordRelation = 30;
                    rewardsText = $"Severance: {severance} denars | Pension: {pensionPerDay} denars/day | Relation: +{lordRelation}";
                    InformationManager.DisplayMessage(new InformationMessage($"{dischargeType} Complete: {bandText}", new Color(0.2f, 0.8f, 0.2f)));
                    InformationManager.DisplayMessage(new InformationMessage(rewardsText, new Color(0.8f, 0.8f, 0.2f)));
                    break;
                
                case "honorable":
                    bandText = "HONORABLE";
                    severance = config?.SeveranceHonorable ?? 3000;
                    lordRelation = 10;
                    rewardsText = $"Severance: {severance} denars | Pension: {pensionPerDay} denars/day | Relation: +{lordRelation}";
                    InformationManager.DisplayMessage(new InformationMessage($"{dischargeType} Complete: {bandText}", new Color(0.2f, 0.8f, 0.2f)));
                    InformationManager.DisplayMessage(new InformationMessage(rewardsText, new Color(0.8f, 0.8f, 0.2f)));
                    break;
                
                default: // washout
                    bandText = "WASHOUT";
                    InformationManager.DisplayMessage(new InformationMessage($"Discharge Complete: {bandText}", new Color(0.8f, 0.3f, 0.2f)));
                    InformationManager.DisplayMessage(new InformationMessage("No severance or pension awarded. Relation: -10", new Color(0.8f, 0.3f, 0.2f)));
                    break;
            }

            // Summary line
            InformationManager.DisplayMessage(new InformationMessage($"Service ended after {daysServed} days.", Color.White));
        }

        /// <summary>
        /// Counts battles that occurred since the last muster.
        /// </summary>
        private int CountBattlesThisPeriod()
        {
            var newsBehavior = EnlistedNewsBehavior.Instance;
            if (newsBehavior == null)
            {
                return 0;
            }

            var lastMusterDay = _currentMuster?.LastMusterDay ?? 0;
            var feedItems = newsBehavior.GetPersonalFeedSince(lastMusterDay);

            if (feedItems == null)
            {
                return 0;
            }

            // Count battle-related feed items
            return feedItems.Count(item =>
                item.Category?.Contains("battle") == true ||
                item.HeadlineKey?.ToLowerInvariant().Contains("battle") == true ||
                item.HeadlineKey?.ToLowerInvariant().Contains("victory") == true ||
                item.HeadlineKey?.ToLowerInvariant().Contains("defeat") == true);
        }

        /// <summary>
        /// Opens the quartermaster conversation after muster.
        /// </summary>
        private void OpenQuartermasterConversation()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                ModLogger.Warn(LogCategory, "Cannot open QM: player not enlisted");
                return;
            }

            var qm = enlistment.GetOrCreateQuartermaster();
            if (qm != null && qm.IsAlive)
            {
                var party = QuartermasterPartyResolver.GetConversationParty(qm);
                if (party == null)
                {
                    ModLogger.Expected(LogCategory, "qm_party_unavailable",
                        "Both QM and enlisted lord have no party — cannot open muster-complete QM conversation with correct scene");
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_party_unavailable}The quartermaster cannot be reached right now.").ToString()));
                    return;
                }

                ModLogger.Info(LogCategory, "Opening quartermaster conversation from muster complete menu");
                _qmOpenedFromMuster = true;
                CampaignMapConversation.OpenConversation(
                    new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty),
                    new ConversationCharacterData(qm.CharacterObject, party));
            }
            else
            {
                ModLogger.Error(LogCategory, "Quartermaster hero unavailable");
                InformationManager.DisplayMessage(new InformationMessage("Quartermaster unavailable."));
            }
        }

        #endregion
    }
}

