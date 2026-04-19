using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Camp;
using Enlisted.Features.Context;
using Enlisted.Features.Conversations.Data;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Features.Equipment.Managers;
using Enlisted.Features.Equipment.UI;
using Enlisted.Features.Escalation;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Features.Logistics;
using Enlisted.Features.Ranks;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Entry;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Conversations.Behaviors
{
    /// <summary>
    ///     Centralized dialog manager for all enlisted military service conversations.
    ///     This system provides a single hub for all enlisted dialogs to prevent conflicts
    ///     and maintain consistent conversation flows. Uses the diplomatic submenu for
    ///     professional integration with the game's conversation system.
    ///     The menu system is handled by EnlistedMenuBehavior.cs, which provides the main enlisted status menu
    ///     and duty/profession selection interface.
    /// </summary>
    public sealed class EnlistedDialogManager : CampaignBehaviorBase
    {
        public EnlistedDialogManager()
        {
            Instance = this;
        }

        public static EnlistedDialogManager Instance { get; private set; }

        /// <summary>
        /// Sets the baggage request context for the next QM conversation.
        /// Call this before opening a QM conversation to route to baggage dialogue.
        /// </summary>
        public void SetBaggageRequestContext(string requestType)
        {
            _baggageRequestType = requestType ?? "none";
            ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"Baggage request context set to: {_baggageRequestType}");
        }

        /// <summary>
        /// Clears the baggage request context after conversation ends.
        /// </summary>
        public void ClearBaggageRequestContext()
        {
            _baggageRequestType = "none";
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Dialog manager has no persistent state
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Load quartermaster dialogue catalog from JSON
            try
            {
                QMDialogueCatalog.Instance.LoadFromJson();
                ModLogger.Info("ENLISTEDDIALOGMANAGER", $"QM Dialogue: Loaded {QMDialogueCatalog.Instance.NodeCount} nodes from JSON");
            }
            catch (Exception ex)
            {
                ModLogger.Error("ENLISTEDDIALOGMANAGER", "Failed to load QM dialogue catalog", ex);
            }

            AddEnlistedDialogs(starter);
            AddQuartermasterDialogs(starter);
            // The menu system is handled by EnlistedMenuBehavior.cs, which provides the main enlisted status menu
        }

        /// <summary>
        ///     Add all enlisted dialog flows through centralized management.
        ///     Uses diplomatic submenu (lord_talk_speak_diplomacy_2) for professional integration.
        /// </summary>
        private void AddEnlistedDialogs(CampaignGameStarter starter)
        {
            try
            {
                // Professional entry point through diplomatic channels
                AddMainEnlistedEntry(starter);

                // Enlistment conversation flow
                AddEnlistmentDialogs(starter);

                // Retirement conversation flow
                AddRetirementDialogs(starter);

                ModLogger.Info("DIALOGMANAGER",
                    "All enlisted dialog flows registered successfully - enlistment dialogs should appear in lord conversations");
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Failed to register enlisted dialogs", ex);
            }
        }

        /// <summary>
        ///     Add main entry point for enlisted services through diplomatic submenu.
        ///     CORRECTED: Restore working player-initiated dialog structure.
        /// </summary>
        private void AddMainEnlistedEntry(CampaignGameStarter starter)
        {
            // FIXED: Player initiates military service discussion (was working!)
            starter.AddPlayerLine(
                "enlisted_diplomatic_entry",
                "lord_talk_speak_diplomacy_2",
                "enlisted_main_hub",
                GetLocalizedText(
                        "{=enlisted_diplomatic_entry}My lord, I seek to speak with you about bearing arms in your service.")
                    .ToString(),
                IsValidLordForMilitaryService,
                null,
                110);

            // Lord recognizes player serves another faction (roleplay rejection)
            starter.AddDialogLine(
                "enlisted_different_faction_rejection",
                "enlisted_main_hub",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_different_faction_rejection}Hold a moment... I recognize your bearing. You serve another lord, do you not? A soldier cannot serve two masters. Return to your commander, or settle your affairs with them first before approaching me.")
                    .ToString(),
                IsOnLeaveWithDifferentFaction,
                null,
                120); // Higher priority - check this FIRST

            // Lord responds to player's request (normal flow)
            starter.AddDialogLine(
                "enlisted_main_hub_response",
                "enlisted_main_hub",
                "enlisted_service_options",
                GetLocalizedText("{=enlisted_main_hub_response}Speak freely. What brings a warrior to my hall?")
                    .ToString(),
                null,
                null,
                110);
        }

        /// <summary>
        ///     Add enlistment conversation flow.
        /// </summary>
        private void AddEnlistmentDialogs(CampaignGameStarter starter)
        {
            var enlistmentText =
                GetLocalizedText(
                    "{=enlisted_request_service}I offer you my sword and my loyalty, my lord. Will you have me in your ranks?");
            // Option to request enlistment (standard kingdom lords)
            starter.AddPlayerLine(
                "enlisted_request_service",
                "enlisted_service_options",
                "enlisted_enlistment_response",
                enlistmentText.ToString(),
                CanRequestStandardEnlistment,
                null,
                110);

            // Option to request enlistment (minor faction lords - mercenary tone)
            starter.AddPlayerLine(
                "enlisted_minor_request_service",
                "enlisted_service_options",
                "enlisted_minor_enlistment_response",
                GetLocalizedText(
                        "{=enlisted_minor_request_service}I hear your company takes on capable fighters. I can handle myself in a scrap - interested?")
                    .ToString(),
                CanRequestMinorFactionEnlistment,
                null,
                111); // Higher priority than standard

            // Minor faction lord's response to enlistment request
            starter.AddDialogLine(
                "enlisted_minor_enlistment_accepted",
                "enlisted_minor_enlistment_response",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_minor_enlistment_accepted}You look useful. We pay fair coin for good work, and dead men don't collect. Fall in with the lads and keep your blade sharp.")
                    .ToString(),
                null,
                OnAcceptEnlistment,
                111);

            // Army leader attempting to enlist - shows when player commands their own army
            // Higher priority (109) so it appears when condition is met, blocking standard enlistment
            starter.AddPlayerLine(
                "enlisted_request_service_army_leader",
                "enlisted_service_options",
                "enlisted_army_leader_rejection",
                GetLocalizedText(
                        "{=enlisted_request_service_army_leader}My lord, I offer you my sword and my loyalty. Will you have me in your ranks?")
                    .ToString(),
                IsPlayerLeadingArmy,
                null,
                109);

            // Lord's response to army leader - cannot enlist while commanding an army
            starter.AddDialogLine(
                "enlisted_army_leader_rejection_response",
                "enlisted_army_leader_rejection",
                "enlisted_army_leader_acknowledge",
                GetLocalizedText(
                        "{=enlisted_army_leader_rejection}Hold, friend. I see you already command men of your own. A general cannot become a foot soldier while lords still march beneath his banner. Disband your army first, then we may speak of service.")
                    .ToString(),
                null,
                null,
                109);

            // Player acknowledges they must disband army first
            starter.AddPlayerLine(
                "enlisted_army_leader_acknowledge",
                "enlisted_army_leader_acknowledge",
                "lord_pretalk",
                GetLocalizedText(
                        "{=enlisted_army_leader_acknowledge}I understand, my lord. I shall see to my army's affairs first.")
                    .ToString(),
                null,
                null,
                109);

            // Lord's response when player tries to re-enlist after aborting bag check
            starter.AddDialogLine(
                "enlisted_bag_check_abort_rejection",
                "enlisted_enlistment_response",
                "enlisted_bag_check_abort_acknowledge",
                GetLocalizedText(
                        "{=enlisted_bag_check_abort_rejection}Ah. You again. I remember you at the wagon line—had your gear ready, then walked off. Changed your mind. I don't begrudge a man his doubts, but I need soldiers who are certain. Sort out your affairs. Come back in a week, if you're serious.")
                    .ToString(),
                HasBagCheckAbortCooldown,
                null,
                111); // Higher priority than normal response

            // Player acknowledges they must return later after aborting
            starter.AddPlayerLine(
                "enlisted_bag_check_abort_acknowledge",
                "enlisted_bag_check_abort_acknowledge",
                "lord_pretalk",
                GetLocalizedText(
                        "{=enlisted_bag_check_abort_acknowledge}As you say, my lord. I shall return when I am ready.")
                    .ToString(),
                null,
                null,
                109);

            // Grace-specific enlistment option (covers lord killed, captured, or army defeated)
            starter.AddPlayerLine(
                "enlisted_request_service_grace",
                "enlisted_service_options",
                "enlisted_enlistment_response_grace",
                GetLocalizedText(
                        "{=enlisted_request_service_grace}My lord, fate has taken my commander from me. I am a soldier without a banner, but my oath to this kingdom remains. Will you take me under your command?")
                    .ToString(),
                CanRequestGraceTransfer,
                null,
                110);

            // Option to return from temporary leave - kingdom lords
            starter.AddPlayerLine(
                "enlisted_return_from_leave",
                "enlisted_service_options",
                "enlisted_return_response",
                GetLocalizedText(
                        "{=enlisted_return_from_leave}I have rested enough, my lord. My blade grows restless. I am ready to return to the front.")
                    .ToString(),
                CanReturnFromLeave,
                null,
                111);

            // Option to return from temporary leave - minor faction lords
            starter.AddPlayerLine(
                "enlisted_minor_return_from_leave",
                "enlisted_service_options",
                "enlisted_minor_return_response",
                GetLocalizedText(
                        "{=enlisted_minor_return_from_leave}I've had my rest. Ready to get back to work, captain.")
                    .ToString(),
                CanReturnFromMinorFactionLeave,
                null,
                112); // Higher priority

            // Lord's response to return from leave request - kingdom lords
            starter.AddDialogLine(
                "enlisted_return_accepted",
                "enlisted_return_response",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_return_accepted}Good to see you standing tall again. The men will be glad to have you back. Fall in with your company.")
                    .ToString(),
                null,
                OnReturnFromLeave,
                111);

            // Minor faction lord's response to return from leave
            starter.AddDialogLine(
                "enlisted_minor_return_accepted",
                "enlisted_minor_return_response",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_minor_return_accepted}Good timing. We've got work lined up and could use another blade. Fall in with the lads.")
                    .ToString(),
                null,
                OnReturnFromLeave,
                112);

            // Option to transfer service to a different lord (while on leave or grace period)
            starter.AddPlayerLine(
                "enlisted_request_transfer",
                "enlisted_service_options",
                "enlisted_transfer_response",
                GetLocalizedText(
                        "{=enlisted_request_transfer}My lord, I currently serve another of this realm, but I believe my talents would be better suited under your banner. Would you accept my transfer?")
                    .ToString(),
                CanRequestServiceTransfer,
                null,
                112);

            // Lord's response to service transfer request
            starter.AddDialogLine(
                "enlisted_transfer_accepted",
                "enlisted_transfer_response",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_transfer_accepted}A soldier who knows his worth. Very well, I shall send word to your former commander. From this day, you march with me.")
                    .ToString(),
                null,
                OnAcceptServiceTransfer,
                112);

            // ===== Promotion Request Dialog (T6→T7) =====
            // Player can request T7 promotion if they meet all requirements
            starter.AddPlayerLine(
                "enlisted_request_promotion_t7",
                "enlisted_service_options",
                "enlisted_promotion_t7_response",
                GetLocalizedText(
                        "{=enlisted_request_promotion_t7}My lord, I have proven myself in your service. I believe I am ready to accept the responsibilities of command.")
                    .ToString(),
                CanRequestCommanderPromotion,
                null,
                108); // High priority - shows when eligible

            // Lord evaluates player and offers promotion
            starter.AddDialogLine(
                "enlisted_promotion_t7_offer",
                "enlisted_promotion_t7_response",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_promotion_t7_offer}You have proven yourself worthy. The rank of commander is yours, along with twenty soldiers to train and lead. Make them into warriors worthy of our banner.")
                    .ToString(),
                null,
                OnAcceptCommanderPromotion,
                110);

            // Lord's response to enlistment request
            starter.AddDialogLine(
                "enlisted_lord_accepts",
                "enlisted_enlistment_response",
                "enlisted_service_terms",
                GetLocalizedText(
                        "{=enlisted_lord_accepts}I see the steel in your eyes. You'll do. But know this - I expect loyalty, discipline, and courage. Here are my terms...")
                    .ToString(),
                null,
                null,
                110);

            // Lord's response to grace re-assignment
            starter.AddDialogLine(
                "enlisted_lord_accepts_grace",
                "enlisted_enlistment_response_grace",
                "enlisted_service_terms",
                GetLocalizedText(
                        "{=enlisted_lord_accepts_grace}A soldier who seeks a new banner rather than deserting? That speaks well of your character. I welcome you to my company. These are my terms...")
                    .ToString(),
                null,
                null,
                110);

            // Service terms and confirmation (3-year initial term as per user request)
            starter.AddDialogLine(
                "enlisted_service_terms_details",
                "enlisted_service_terms",
                "enlisted_confirm_service",
                GetLocalizedText(
                        "{=enlisted_service_terms_details}You will march when I say march, fight when I say fight, and hold the line when all seems lost. Your term is three years. In return, you shall have daily wages and a place by the fire. Do we have an accord?")
                    .ToString(),
                null,
                null,
                110);

            // Player accepts service
            starter.AddPlayerLine(
                "enlisted_accept_service",
                "enlisted_confirm_service",
                "close_window",
                GetLocalizedText("{=enlisted_accept_service}You have my oath, my lord. Point me at the enemy.")
                    .ToString(),
                null,
                OnAcceptEnlistment,
                110);

            // Player declines service
            starter.AddPlayerLine(
                "enlisted_decline_service",
                "enlisted_confirm_service",
                "lord_pretalk",
                GetLocalizedText(
                        "{=enlisted_decline_service}Your offer is generous, my lord, but I must think on it. Perhaps another time.")
                    .ToString(),
                null,
                null,
                110);
        }

        /// <summary>
        ///     Add retirement conversation flow with full benefits explanation.
        /// </summary>
        private void AddRetirementDialogs(CampaignGameStarter starter)
        {
            // Retirement/discharge is requested via Camp -> Leave Service -> Request Discharge (Pending Discharge -> Final Muster).
            // Conversations only provide guidance so we don't maintain a second conflicting retirement system here.
            starter.AddPlayerLine(
                "enlisted_discuss_discharge_guidance",
                "enlisted_service_options",
                "enlisted_discharge_guidance",
                GetLocalizedText(
                        "{=enlisted_discuss_discharge_guidance}My lord, if I wished to leave your service, how would I do so properly?")
                    .ToString(),
                CanDiscussDischargeGuidance,
                null,
                109);

            starter.AddDialogLine(
                "enlisted_discharge_guidance_text",
                "enlisted_discharge_guidance",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_discharge_guidance_text}If you wish to leave my service, do it properly. Make camp and request discharge. Your final pay and discharge papers will be handled at the next muster.")
                    .ToString(),
                null,
                null,
                110);

#if false
            // First-term retirement discussion (available after 3 years) - kingdom lords
            starter.AddPlayerLine(
                "enlisted_discuss_retirement",
                "enlisted_service_options",
                "enlisted_retirement_benefits",
                GetLocalizedText(
                        "{=enlisted_discuss_retirement}My lord, I have served faithfully these many months. The time has come to discuss my future.")
                    .ToString(),
                CanDiscussFirstTermRetirement,
                null,
                109); // Higher priority than regular retirement

            // First-term retirement discussion - minor faction lords (mercenary tone)
            starter.AddPlayerLine(
                "enlisted_minor_discuss_retirement",
                "enlisted_service_options",
                "enlisted_minor_retirement_benefits",
                GetLocalizedText(
                        "{=enlisted_minor_discuss_retirement}Captain, I've been riding with the company long enough. Time we settled accounts.")
                    .ToString(),
                CanDiscussMinorFactionRetirement,
                null,
                110); // Higher priority than kingdom version

            // Minor faction lord explains benefits (mercenary tone)
            starter.AddDialogLine(
                "enlisted_minor_retirement_benefits_explanation",
                "enlisted_minor_retirement_benefits",
                "enlisted_minor_retirement_choice",
                GetLocalizedText(
                        "{=enlisted_minor_retirement_benefits}You've earned your share and then some. Take your coin now and walk free, or stick around another year - I'll sweeten the pot. Your call.")
                    .ToString(),
                null,
                null,
                110);

            // Player accepts retirement from minor faction (with retinue - goes to troop choice)
            starter.AddPlayerLine(
                "enlisted_minor_accept_retirement_with_retinue",
                "enlisted_minor_retirement_choice",
                "enlisted_minor_retinue_ask",
                GetLocalizedText(
                        "{=enlisted_minor_accept_retirement}I'll take my cut now. It's been good riding with you.")
                    .ToString(),
                HasRetinueForRetirement,
                null,
                111); // Higher priority when has retinue

            // Player accepts retirement from minor faction (no retinue - direct farewell)
            starter.AddPlayerLine(
                "enlisted_minor_accept_retirement_no_retinue",
                "enlisted_minor_retirement_choice",
                "enlisted_minor_retirement_farewell",
                GetLocalizedText(
                        "{=enlisted_minor_accept_retirement}I'll take my cut now. It's been good riding with you.")
                    .ToString(),
                () => !HasRetinueForRetirement(),
                OnAcceptFirstTermRetirement,
                110);

            // Minor faction captain asks about the troops
            starter.AddDialogLine(
                "enlisted_minor_retinue_ask_text",
                "enlisted_minor_retinue_ask",
                "enlisted_minor_retinue_choice",
                GetLocalizedText(
                        "{=enlisted_minor_retinue_ask}Your lads have been asking around. Word is they'd rather follow you than draw army pay. Your call, fighter.")
                    .ToString(),
                null,
                null,
                110);

            // Player keeps troops (minor faction)
            starter.AddPlayerLine(
                "enlisted_minor_keep_troops",
                "enlisted_minor_retinue_choice",
                "enlisted_minor_farewell_with_troops",
                GetLocalizedText(
                        "{=enlisted_minor_keep_troops}Tell them to pack their gear. We ride together.")
                    .ToString(),
                null,
                OnKeepTroopsRetirement,
                110);

            // Player releases troops (minor faction)
            starter.AddPlayerLine(
                "enlisted_minor_release_troops",
                "enlisted_minor_retinue_choice",
                "enlisted_minor_retirement_farewell",
                GetLocalizedText(
                        "{=enlisted_minor_release_troops}They're good fighters. The company needs them more than I do.")
                    .ToString(),
                null,
                OnAcceptFirstTermRetirement,
                110);

            // Minor faction farewell when keeping troops
            starter.AddDialogLine(
                "enlisted_minor_farewell_with_troops_text",
                "enlisted_minor_farewell_with_troops",
                "close_window",
                GetRetinueFarewellText(isMinorFaction: true),
                null,
                OnFinalizeRetirementWithTroops,
                110);

            // Player accepts re-enlistment with minor faction (1-year term)
            starter.AddPlayerLine(
                "enlisted_minor_accept_reenlist",
                "enlisted_minor_retirement_choice",
                "enlisted_minor_reenlist_confirmed",
                GetLocalizedText(
                        "{=enlisted_minor_accept_reenlist}Coin's good, company's better. I'll stick around for that bonus.")
                    .ToString(),
                null,
                OnAcceptFirstTermReenlistBonus,
                110);

            // Player needs time to decide (minor faction)
            starter.AddPlayerLine(
                "enlisted_minor_retirement_later",
                "enlisted_minor_retirement_choice",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_minor_retirement_later}Let me think on it. I'll find you when I've decided.")
                    .ToString(),
                null,
                null,
                110);

            // Minor faction lord's farewell on retirement
            starter.AddDialogLine(
                "enlisted_minor_retirement_farewell_text",
                "enlisted_minor_retirement_farewell",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_minor_retirement_farewell}Fair winds, fighter. You've done right by the company. If you ever need work again, you know where to find us.")
                    .ToString(),
                null,
                null,
                110);

            // Minor faction lord confirms re-enlistment
            starter.AddDialogLine(
                "enlisted_minor_reenlist_confirmed",
                "enlisted_minor_reenlist_confirmed",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_minor_reenlist_confirmed}Smart choice. The lads will be glad to have you. Your bonus is guaranteed when the year's up. Now let's find some trouble.")
                    .ToString(),
                null,
                null,
                110);

            // Lord explains benefits (mentions 1-year re-enlistment term) - kingdom lords
            starter.AddDialogLine(
                "enlisted_retirement_benefits_explanation",
                "enlisted_retirement_benefits",
                "enlisted_retirement_choice",
                GetLocalizedText(
                        "{=enlisted_retirement_benefits_explanation}You have served with honor, and I do not forget loyalty. Retire now and you leave with gold in your pocket, my personal letter of recommendation, and the respect of this kingdom. Or... stay another year, and I shall double your severance. The choice is yours.")
                    .ToString(),
                null,
                null,
                110);

            // Player accepts retirement with benefits (with retinue - goes to troop choice)
            starter.AddPlayerLine(
                "enlisted_accept_retirement_with_retinue",
                "enlisted_retirement_choice",
                "enlisted_retinue_ask",
                GetLocalizedText(
                        "{=enlisted_accept_retirement}I thank you for everything, my lord. It has been an honor to serve under your banner.")
                    .ToString(),
                HasRetinueForRetirement,
                null,
                111); // Higher priority when has retinue

            // Player accepts retirement with benefits (no retinue - direct farewell)
            starter.AddPlayerLine(
                "enlisted_accept_retirement_no_retinue",
                "enlisted_retirement_choice",
                "enlisted_retirement_farewell",
                GetLocalizedText(
                        "{=enlisted_accept_retirement}I thank you for everything, my lord. It has been an honor to serve under your banner.")
                    .ToString(),
                () => !HasRetinueForRetirement(),
                OnAcceptFirstTermRetirement,
                110);

            // Kingdom lord asks about the troops
            starter.AddDialogLine(
                "enlisted_retinue_ask_text",
                "enlisted_retinue_ask",
                "enlisted_retinue_choice",
                GetRetinueAskText(),
                null,
                null,
                110);

            // Player keeps troops (kingdom)
            starter.AddPlayerLine(
                "enlisted_keep_troops",
                "enlisted_retinue_choice",
                "enlisted_farewell_with_troops",
                GetLocalizedText(
                        "{=enlisted_keep_troops}My men have chosen to march with me, my lord. We've bled together - that bond does not break so easily.")
                    .ToString(),
                null,
                OnKeepTroopsRetirement,
                110);

            // Player releases troops (kingdom)
            starter.AddPlayerLine(
                "enlisted_release_troops",
                "enlisted_retinue_choice",
                "enlisted_retirement_farewell",
                GetLocalizedText(
                        "{=enlisted_release_troops}Let them return to the army. They signed on to serve the kingdom, not one man.")
                    .ToString(),
                null,
                OnAcceptFirstTermRetirement,
                110);

            // Kingdom farewell when keeping troops
            starter.AddDialogLine(
                "enlisted_farewell_with_troops_text",
                "enlisted_farewell_with_troops",
                "close_window",
                GetRetinueFarewellText(isMinorFaction: false),
                null,
                OnFinalizeRetirementWithTroops,
                110);

            // Player accepts re-enlistment bonus (1-year term)
            starter.AddPlayerLine(
                "enlisted_accept_reenlist_bonus",
                "enlisted_retirement_choice",
                "enlisted_reenlist_confirmed",
                GetLocalizedText(
                        "{=enlisted_accept_reenlist_bonus}The battlefield is the only home I know, my lord. I shall stay and earn that gold.")
                    .ToString(),
                null,
                OnAcceptFirstTermReenlistBonus,
                110);

            // Player needs time to decide
            starter.AddPlayerLine(
                "enlisted_retirement_later",
                "enlisted_retirement_choice",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_retirement_later}This is not a decision to make lightly. Give me time to consider, my lord.")
                    .ToString(),
                null,
                null,
                110);

            // Lord's farewell on retirement
            starter.AddDialogLine(
                "enlisted_retirement_farewell_text",
                "enlisted_retirement_farewell",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_retirement_farewell}Go well, soldier. You have earned your peace. Should you ever wish to return, my door remains open.")
                    .ToString(),
                null,
                null,
                110);

            // Lord confirms re-enlistment (1-year term)
            starter.AddDialogLine(
                "enlisted_reenlist_bonus_confirmed",
                "enlisted_reenlist_confirmed",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_reenlist_bonus_confirmed}Ha! I knew you had more fight in you. Your gold will be waiting when the year is done. Now get back to your post.")
                    .ToString(),
                null,
                null,
                110);

            // Renewal term complete dialog (after 1-year renewal) - kingdom lords
            starter.AddPlayerLine(
                "enlisted_renewal_complete",
                "enlisted_service_options",
                "enlisted_renewal_options",
                GetLocalizedText(
                    "{=enlisted_renewal_complete}My lord, another year has passed. What becomes of me now?").ToString(),
                CanDiscussRenewalTermEnd,
                null,
                108);

            // Renewal term complete dialog - minor faction lords
            starter.AddPlayerLine(
                "enlisted_minor_renewal_complete",
                "enlisted_service_options",
                "enlisted_minor_renewal_options",
                GetLocalizedText(
                    "{=enlisted_minor_renewal_complete}Captain, the year's up. What's the score?").ToString(),
                CanDiscussMinorFactionRenewalEnd,
                null,
                109); // Higher priority

            // Minor faction lord explains renewal options
            starter.AddDialogLine(
                "enlisted_minor_renewal_options_explanation",
                "enlisted_minor_renewal_options",
                "enlisted_minor_renewal_choice",
                GetLocalizedText(
                        "{=enlisted_minor_renewal_options}Another year done, and you're still breathing. Take your gold and go free, or sign on for another round - same deal as before. What'll it be?")
                    .ToString(),
                null,
                null,
                110);

            // Player accepts renewal discharge from minor faction (with retinue - goes to troop choice)
            starter.AddPlayerLine(
                "enlisted_minor_accept_renewal_discharge_with_retinue",
                "enlisted_minor_renewal_choice",
                "enlisted_minor_renewal_retinue_ask",
                GetLocalizedText(
                        "{=enlisted_minor_renewal_discharge}I'll take my share. Time to see what else is out there.")
                    .ToString(),
                HasRetinueForRetirement,
                null,
                111); // Higher priority when has retinue

            // Player accepts renewal discharge from minor faction (no retinue - direct farewell)
            starter.AddPlayerLine(
                "enlisted_minor_accept_renewal_discharge_no_retinue",
                "enlisted_minor_renewal_choice",
                "enlisted_minor_renewal_farewell",
                GetLocalizedText(
                        "{=enlisted_minor_renewal_discharge}I'll take my share. Time to see what else is out there.")
                    .ToString(),
                () => !HasRetinueForRetirement(),
                OnAcceptRenewalDischarge,
                110);

            // Minor faction captain asks about troops on renewal discharge
            starter.AddDialogLine(
                "enlisted_minor_renewal_retinue_ask_text",
                "enlisted_minor_renewal_retinue_ask",
                "enlisted_minor_renewal_retinue_choice",
                GetLocalizedText(
                        "{=enlisted_minor_retinue_ask}Your lads have been asking around. Word is they'd rather follow you than draw army pay. Your call, fighter.")
                    .ToString(),
                null,
                null,
                110);

            // Player keeps troops on renewal discharge (minor faction)
            starter.AddPlayerLine(
                "enlisted_minor_renewal_keep_troops",
                "enlisted_minor_renewal_retinue_choice",
                "enlisted_minor_renewal_farewell_with_troops",
                GetLocalizedText(
                        "{=enlisted_minor_keep_troops}Tell them to pack their gear. We ride together.")
                    .ToString(),
                null,
                OnKeepTroopsRetirement,
                110);

            // Player releases troops on renewal discharge (minor faction)
            starter.AddPlayerLine(
                "enlisted_minor_renewal_release_troops",
                "enlisted_minor_renewal_retinue_choice",
                "enlisted_minor_renewal_farewell",
                GetLocalizedText(
                        "{=enlisted_minor_release_troops}They're good fighters. The company needs them more than I do.")
                    .ToString(),
                null,
                OnAcceptRenewalDischarge,
                110);

            // Minor faction farewell when keeping troops on renewal
            starter.AddDialogLine(
                "enlisted_minor_renewal_farewell_with_troops_text",
                "enlisted_minor_renewal_farewell_with_troops",
                "close_window",
                GetRetinueFarewellText(isMinorFaction: true),
                null,
                OnFinalizeRenewalWithTroops,
                110);

            // Player continues with minor faction (another 1-year term)
            starter.AddPlayerLine(
                "enlisted_minor_continue_service",
                "enlisted_minor_renewal_choice",
                "enlisted_minor_continue_confirmed",
                GetLocalizedText(
                        "{=enlisted_minor_continue_service}The road's been good to me here. I'm in for another year.")
                    .ToString(),
                null,
                OnContinueService,
                110);

            // Minor faction lord's farewell on renewal discharge
            starter.AddDialogLine(
                "enlisted_minor_renewal_farewell_text",
                "enlisted_minor_renewal_farewell",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_minor_renewal_farewell}Your coin's counted and your horse is yours. Good luck out there - and if you hear of any good contracts, send word.")
                    .ToString(),
                null,
                null,
                110);

            // Minor faction lord confirms continued service
            starter.AddDialogLine(
                "enlisted_minor_continue_confirmed",
                "enlisted_minor_continue_confirmed",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_minor_continue_confirmed}Good man. The company's better for having you. Same terms, same coin - let's get back to work.")
                    .ToString(),
                null,
                null,
                110);

            // Lord explains renewal options (1-year terms) - kingdom lords
            starter.AddDialogLine(
                "enlisted_renewal_options_explanation",
                "enlisted_renewal_options",
                "enlisted_renewal_choice",
                GetLocalizedText(
                        "{=enlisted_renewal_options}You have proven yourself time and again. Take your discharge with five thousand gold and my blessing, or stay another year with a bonus to match. What say you?")
                    .ToString(),
                null,
                null,
                110);

            // Player accepts renewal discharge (with retinue - goes to troop choice)
            starter.AddPlayerLine(
                "enlisted_accept_renewal_discharge_with_retinue",
                "enlisted_renewal_choice",
                "enlisted_renewal_retinue_ask",
                GetLocalizedText(
                        "{=enlisted_renewal_discharge}It is time I sought my own path, my lord. I thank you for the opportunity.")
                    .ToString(),
                HasRetinueForRetirement,
                null,
                111); // Higher priority when has retinue

            // Player accepts renewal discharge (no retinue - direct farewell)
            starter.AddPlayerLine(
                "enlisted_accept_renewal_discharge_no_retinue",
                "enlisted_renewal_choice",
                "enlisted_renewal_farewell",
                GetLocalizedText(
                        "{=enlisted_renewal_discharge}It is time I sought my own path, my lord. I thank you for the opportunity.")
                    .ToString(),
                () => !HasRetinueForRetirement(),
                OnAcceptRenewalDischarge,
                110);

            // Kingdom lord asks about troops on renewal discharge
            starter.AddDialogLine(
                "enlisted_renewal_retinue_ask_text",
                "enlisted_renewal_retinue_ask",
                "enlisted_renewal_retinue_choice",
                GetRetinueAskText(),
                null,
                null,
                110);

            // Player keeps troops on renewal discharge (kingdom)
            starter.AddPlayerLine(
                "enlisted_renewal_keep_troops",
                "enlisted_renewal_retinue_choice",
                "enlisted_renewal_farewell_with_troops",
                GetLocalizedText(
                        "{=enlisted_keep_troops}My men have chosen to march with me, my lord. We've bled together - that bond does not break so easily.")
                    .ToString(),
                null,
                OnKeepTroopsRetirement,
                110);

            // Player releases troops on renewal discharge (kingdom)
            starter.AddPlayerLine(
                "enlisted_renewal_release_troops",
                "enlisted_renewal_retinue_choice",
                "enlisted_renewal_farewell",
                GetLocalizedText(
                        "{=enlisted_release_troops}Let them return to the army. They signed on to serve the kingdom, not one man.")
                    .ToString(),
                null,
                OnAcceptRenewalDischarge,
                110);

            // Kingdom farewell when keeping troops on renewal
            starter.AddDialogLine(
                "enlisted_renewal_farewell_with_troops_text",
                "enlisted_renewal_farewell_with_troops",
                "close_window",
                GetRetinueFarewellText(isMinorFaction: false),
                null,
                OnFinalizeRenewalWithTroops,
                110);

            // Player continues service (another 1-year term)
            starter.AddPlayerLine(
                "enlisted_continue_service",
                "enlisted_renewal_choice",
                "enlisted_continue_confirmed",
                GetLocalizedText(
                        "{=enlisted_continue_service}Why would I leave now? We have enemies yet to crush. Count me in for another year.")
                    .ToString(),
                null,
                OnContinueService,
                110);

            // Lord's farewell on renewal discharge
            starter.AddDialogLine(
                "enlisted_renewal_farewell_text",
                "enlisted_renewal_farewell",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_renewal_farewell}Then this is farewell, for now. You have been a fine soldier. May the winds carry you to fortune.")
                    .ToString(),
                null,
                null,
                110);

            // Lord confirms continued service
            starter.AddDialogLine(
                "enlisted_continue_service_confirmed",
                "enlisted_continue_confirmed",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_continue_confirmed}Ha! That's the spirit! I knew I could count on you. Another year, then. Try not to get yourself killed.")
                    .ToString(),
                null,
                null,
                110);

            // Post-cooldown re-enlistment option (veteran returning)
            starter.AddPlayerLine(
                "enlisted_reenlist_after_cooldown",
                "enlisted_service_options",
                "enlisted_veteran_welcome",
                GetLocalizedText(
                        "{=enlisted_reenlist_cooldown}My lord, you may remember me - I served this realm before. The quiet life does not suit me. Will you have me back?")
                    .ToString(),
                CanReEnlistAfterCooldown,
                null,
                107);

            // Lord welcomes back veteran (1-year term for veterans)
            starter.AddDialogLine(
                "enlisted_veteran_welcome_back",
                "enlisted_veteran_welcome",
                "enlisted_veteran_confirm",
                GetLocalizedText(
                        "{=enlisted_veteran_welcome}I remember you! A good soldier returns to the fold. Your old rank awaits, though you will need to choose your specialty anew. Serve one year and you leave with gold in hand.")
                    .ToString(),
                null,
                null,
                110);

            // Veteran confirms re-enlistment
            starter.AddPlayerLine(
                "enlisted_veteran_accept",
                "enlisted_veteran_confirm",
                "enlisted_veteran_accepted",
                GetLocalizedText(
                        "{=enlisted_veteran_accept}My sword arm is strong as ever, my lord. Where do you need me?")
                    .ToString(),
                null,
                OnVeteranReEnlist,
                110);

            // Veteran decides not to re-enlist
            starter.AddPlayerLine(
                "enlisted_veteran_decline",
                "enlisted_veteran_confirm",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_veteran_decline}Forgive me, my lord. The call to arms is strong, but I am not yet ready. Perhaps another day.")
                    .ToString(),
                null,
                null,
                110);

            // Lord confirms veteran re-enlistment
            starter.AddDialogLine(
                "enlisted_veteran_accepted_text",
                "enlisted_veteran_accepted",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_veteran_accepted}Good to have you back in the ranks. Report to the Master at Arms to choose your role, then find your company.")
                    .ToString(),
                null,
                null,
                110);

            // Simple early discharge (for those not eligible for full retirement) - kingdom lords
            starter.AddPlayerLine(
                "enlisted_request_early_discharge",
                "enlisted_service_options",
                "enlisted_early_discharge_response",
                GetLocalizedText(
                        "{=enlisted_early_discharge}My lord, I must ask to be released from my oath. I cannot continue.")
                    .ToString(),
                CanRequestEarlyDischarge,
                null,
                115); // Lower priority - shows when not eligible for full retirement

            // Simple early discharge - minor faction lords
            starter.AddPlayerLine(
                "enlisted_minor_request_early_discharge",
                "enlisted_service_options",
                "enlisted_minor_early_discharge_response",
                GetLocalizedText(
                        "{=enlisted_minor_early_discharge}Captain, I need to break contract early. Something's come up.")
                    .ToString(),
                CanRequestMinorFactionEarlyDischarge,
                null,
                116); // Higher priority

            // Lord grants early discharge (no benefits) - kingdom lords
            starter.AddDialogLine(
                "enlisted_early_discharge_granted",
                "enlisted_early_discharge_response",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_early_discharge_granted}I release you from your oath. You leave without the honors of a full term, but you are free. Go, and think carefully before you take up arms again.")
                    .ToString(),
                null,
                OnGrantEarlyDischarge,
                110);

            // Minor faction lord grants early discharge
            starter.AddDialogLine(
                "enlisted_minor_early_discharge_granted",
                "enlisted_minor_early_discharge_response",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_minor_early_discharge_granted}Contract's done then. No hard feelings - everyone's got their reasons. Don't expect a warm welcome if you come crawling back soon, though.")
                    .ToString(),
                null,
                OnGrantEarlyDischarge,
                110);
#endif

            // Exit option - always available as fallback from service options
            // Uses default priority (100) - shows last among service options (others use 110-115)
            starter.AddPlayerLine(
                "enlisted_service_nevermind",
                "enlisted_service_options",
                "close_window",
                GetLocalizedText(
                        "{=enlisted_service_nevermind}Forgive me, my lord. I spoke out of turn. I have nothing to discuss at this time.")
                    .ToString(),
                null,
                null);
        }

        // ========================================================================
        // QUARTERMASTER DIALOG SYSTEM
        // Conversation-driven interface for equipment, sell, provisions, upgrades.
        // Dialogue selections open Gauntlet UI for browsing/purchasing equipment.
        // ========================================================================

        // Track selected equipment category for browse response
        private string _selectedEquipmentCategory = "";

        // Track selected armor slot for armor drill-down
        private EquipmentIndex _selectedArmorSlot = EquipmentIndex.None;

        // Track baggage request context for emergency access dialogue
        private string _baggageRequestType = "none";

        /// <summary>
        ///     Add quartermaster NPC dialog tree.
        ///     Player opens conversation via "Speak with the Quartermaster" menu option.
        ///     Category selection happens through dialogue, then Gauntlet UI opens for browsing.
        /// </summary>
        private void AddQuartermasterDialogs(CampaignGameStarter starter)
        {
            try
            {
                // Test JSON dialogue catalog loading
                var jsonLoaded = QMDialogueCatalog.Instance != null && QMDialogueCatalog.Instance.NodeCount > 0;
                
                if (jsonLoaded)
                {
                    var testContext = new QMDialogueContext { IsIntroduced = true };
                    var hubNode = QMDialogueCatalog.Instance.GetNode("qm_hub", testContext);
                    if (hubNode != null)
                    {
                        ModLogger.Info("ENLISTEDDIALOGMANAGER", $"QM Dialogue: Successfully loaded test node 'qm_hub' with {hubNode.Options.Count} options");

                        // Register JSON-based dialogue dynamically
                        // Register QM greeting and hub (for returning visits when already introduced)
                        RegisterJsonQuartermasterLines(starter, "qm_hub", "start", 200);
                        RegisterJsonQuartermasterLines(starter, "qm_hub", "qm_hub", 199); // Also trigger when already in hub state (for refreshing after submenus)

                        // Response nodes - only appear after player selects corresponding option from hub
                        RegisterJsonQuartermasterLines(starter, "qm_browse_response", "qm_browse_response", 140);
                        RegisterJsonQuartermasterLines(starter, "qm_sell_response", "qm_sell_response", 148);
                        RegisterJsonQuartermasterLines(starter, "qm_baggage_status_response", "qm_baggage_status_response", 147);
                        RegisterJsonQuartermasterLines(starter, "qm_armor_slots", "qm_armor_slots", 149);

                        // Register introduction flow (first meeting - highest priority from start)
                        RegisterJsonQuartermasterLines(starter, "qm_intro_greeting", "start", 202);

                        // Acknowledgment nodes appear AFTER player selects intro tone from greeting
                        RegisterJsonQuartermasterLines(starter, "qm_intro_ack_direct", "qm_intro_ack_direct", 151);
                        RegisterJsonQuartermasterLines(starter, "qm_intro_ack_military", "qm_intro_ack_military", 151);
                        RegisterJsonQuartermasterLines(starter, "qm_intro_ack_friendly", "qm_intro_ack_friendly", 151);
                        RegisterJsonQuartermasterLines(starter, "qm_intro_ack_flattering", "qm_intro_ack_flattering", 151);

                        // Register gate nodes (RP responses for blocked actions)
                        RegisterJsonQuartermasterLines(starter, "qm_sell_blocked", "qm_sell_blocked", 148);
                        RegisterJsonQuartermasterLines(starter, "qm_gate_horse", "qm_gate_horse", 148);
                        RegisterJsonQuartermasterLines(starter, "qm_gate_officers_armory_rank", "qm_gate_officers_armory_rank", 148);
                        RegisterJsonQuartermasterLines(starter, "qm_gate_officers_armory_trust", "qm_gate_officers_armory_trust", 148);
                        RegisterJsonQuartermasterLines(starter, "qm_gate_upgrade_masterwork", "qm_gate_upgrade_masterwork", 148);
                        RegisterJsonQuartermasterLines(starter, "qm_gate_upgrade_legendary", "qm_gate_upgrade_legendary", 148);

                        // Register baggage train emergency access dialogue
                        RegisterJsonQuartermasterLines(starter, "qm_baggage_request_emergency", "start", 195);
                        RegisterJsonQuartermasterLines(starter, "qm_baggage_request_locked", "start", 195);
                        RegisterJsonQuartermasterLines(starter, "qm_baggage_explain", "qm_baggage_request_emergency", 145);
                        RegisterJsonQuartermasterLines(starter, "qm_baggage_explain_lockdown", "qm_baggage_request_locked", 145);
                        RegisterJsonQuartermasterLines(starter, "qm_baggage_cooldown_active", "qm_baggage_request_emergency", 145);
                        RegisterJsonQuartermasterLines(starter, "qm_baggage_column_halt_response", "qm_baggage_column_halt_response", 145);

                        // Then register player options for each node
                        RegisterJsonDialogueNode(starter, "qm_intro_greeting", 152);
                        RegisterJsonDialogueNode(starter, "qm_intro_ack_direct", 151);
                        RegisterJsonDialogueNode(starter, "qm_intro_ack_military", 151);
                        RegisterJsonDialogueNode(starter, "qm_intro_ack_friendly", 151);
                        RegisterJsonDialogueNode(starter, "qm_intro_ack_flattering", 151);
                        RegisterJsonDialogueNode(starter, "qm_hub", 150);
                        RegisterJsonDialogueNode(starter, "qm_browse_response", 149);
                        RegisterJsonDialogueNode(starter, "qm_sell_response", 148);
                        RegisterJsonDialogueNode(starter, "qm_sell_blocked", 148);
                        RegisterJsonDialogueNode(starter, "qm_baggage_status_response", 147);
                        RegisterJsonDialogueNode(starter, "qm_armor_slots", 148);
                        RegisterJsonDialogueNode(starter, "qm_gate_horse", 147);
                        RegisterJsonDialogueNode(starter, "qm_gate_officers_armory_rank", 147);
                        RegisterJsonDialogueNode(starter, "qm_gate_officers_armory_trust", 147);
                        RegisterJsonDialogueNode(starter, "qm_gate_upgrade_masterwork", 147);
                        RegisterJsonDialogueNode(starter, "qm_gate_upgrade_legendary", 147);

                        // Register player options for baggage dialogue
                        RegisterJsonDialogueNode(starter, "qm_baggage_request_emergency", 146);
                        RegisterJsonDialogueNode(starter, "qm_baggage_request_locked", 146);
                        RegisterJsonDialogueNode(starter, "qm_baggage_explain", 144);
                        RegisterJsonDialogueNode(starter, "qm_baggage_explain_lockdown", 144);
                        RegisterJsonDialogueNode(starter, "qm_baggage_cooldown_active", 144);
                        RegisterJsonDialogueNode(starter, "qm_baggage_column_halt_response", 144);
                    }
                    else
                    {
                        ModLogger.Warn("ENLISTEDDIALOGMANAGER", "QM Dialogue: Test node 'qm_hub' not found in catalog");
                        jsonLoaded = false;
                    }
                }
                
                // ========================================
                // CRITICAL FALLBACK: If JSON failed to load, register basic dialogue
                // This ensures quartermaster is ALWAYS functional even if JSON files are missing
                // ========================================
                if (!jsonLoaded)
                {
                    ModLogger.Error("ENLISTEDDIALOGMANAGER", 
                        "========================================\n" +
                        "QM JSON DIALOGUE FAILED TO LOAD\n" +
                        "Using fallback dialogue system.\n" +
                        "Quartermaster will work but with limited dialogue.\n" +
                        "This indicates a corrupted/incomplete installation.\n" +
                        "Check Debugging/Conflicts-A_*.log for missing files.\n" +
                        "========================================");
                    RegisterFallbackQuartermasterDialogue(starter);
                }

                // Note: Quartermaster heroes use Occupation.Soldier (not Wanderer) to prevent
                // vanilla companion recruitment dialogue from appearing.

                // Register supply response node with dynamic SUPPLY_STATUS text variable
                // This must be registered manually to call SetSupplyStatusText() in the condition
                starter.AddDialogLine(
                    "qm_supply_response_dynamic",
                    "qm_supply_response",
                    "qm_supply_response",
                    "{=qm_supply_report}{SUPPLY_STATUS}",
                    () => IsQuartermasterConversation() && SetSupplyStatusText(),
                    null,
                    200);

                // Register the player option to return to hub after seeing supply report
                starter.AddPlayerLine(
                    "qm_supply_continue",
                    "qm_supply_response",
                    "qm_hub",
                    "{=qm_continue}[Continue]",
                    IsQuartermasterConversation,
                    null,
                    100);

                ModLogger.Info("CONVERSATIONS", "Quartermaster dialog tree registered");
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "Failed to register quartermaster dialogs", ex);
            }
        }

        /// <summary>
        /// Fallback dialogue system when JSON fails to load.
        /// Provides basic quartermaster functionality using XML strings directly.
        /// </summary>
        private void RegisterFallbackQuartermasterDialogue(CampaignGameStarter starter)
        {
            // QM greeting (first meeting or return)
            starter.AddDialogLine(
                "qm_fallback_greeting",
                "start",
                "qm_fallback_hub",
                "{=qm_hub_greeting}What do you need?",
                IsQuartermasterConversation,
                null,
                200);

            // Player: Browse equipment
            starter.AddPlayerLine(
                "qm_fallback_browse",
                "qm_fallback_hub",
                "close_window",
                "{=qm_player_browse}I'm looking for some new gear. What've you got?",
                null,
                () => {
                    _selectedEquipmentCategory = "weapons";
                    OnQuartermasterBrowseCategory();
                });

            // Player: Upgrade equipment
            starter.AddPlayerLine(
                "qm_fallback_upgrade",
                "qm_fallback_hub",
                "close_window",
                "{=qm_player_upgrade}I want to improve what I'm carrying.",
                null,
                OnQuartermasterUpgradeRequest);

            // Player: Sell equipment
            starter.AddPlayerLine(
                "qm_fallback_sell",
                "qm_fallback_hub",
                "close_window",
                "{=qm_player_sell}I've got some equipment to offload... quietly.",
                null,
                OnQuartermasterSellRequest);

            // Player: Provisions (officers only)
            starter.AddPlayerLine(
                "qm_fallback_provisions",
                "qm_fallback_hub",
                "close_window",
                "{=qm_player_provisions}I could use some provisions.",
                () => EnlistmentBehavior.Instance?.EnlistmentTier >= 7,
                OnQuartermasterProvisionsRequest);

            // Player: Supply inquiry
            starter.AddPlayerLine(
                "qm_fallback_supply",
                "qm_fallback_hub",
                "qm_fallback_supply_response",
                "{=qm_player_supply}How are we looking? Supply-wise, I mean.",
                null,
                null);

            // QM: Supply response (with dynamic text)
            starter.AddDialogLine(
                "qm_fallback_supply_response",
                "qm_fallback_supply_response",
                "qm_fallback_hub",
                "{=qm_supply_report}{SUPPLY_STATUS}",
                () => IsQuartermasterConversation() && SetSupplyStatusText(),
                null,
                200);

            // Player: Goodbye
            starter.AddPlayerLine(
                "qm_fallback_goodbye",
                "qm_fallback_hub",
                "close_window",
                "{=qm_player_farewell}That's all for now.",
                null,
                null);

            ModLogger.Info("ENLISTEDDIALOGMANAGER", "Fallback quartermaster dialogue registered");
        }

        #region Quartermaster Dialog Conditions

        /// <summary>
        ///     Sets the QM_GREETING text variable for dynamic greeting display.
        ///     Returns true always (used in condition chain).
        /// </summary>
        private bool SetQuartermasterGreetingText()
        {
            MBTextManager.SetTextVariable("QM_GREETING", GetQuartermasterGreeting());

            // Set common dialogue variables for placeholders in gate text and other nodes
            SetCommonDialogueVariables();

            return true;
        }

        /// <summary>
        ///     Sets common dialogue variables used across QM conversation nodes.
        ///     These placeholders can be used in JSON/XML dialogue text.
        /// </summary>
        private void SetCommonDialogueVariables()
        {
            var hero = Hero.MainHero;
            var enlistment = EnlistmentBehavior.Instance;

            // Player name
            var playerName = hero?.Name?.ToString() ?? "Soldier";
            MBTextManager.SetTextVariable("PLAYER_NAME", playerName);

            // Culture-specific rank title
            var rankTitle = enlistment != null
                ? RankHelper.GetCurrentRank(enlistment)
                : "Soldier";
            MBTextManager.SetTextVariable("PLAYER_RANK", rankTitle);

            // QM name
            var qmName = enlistment?.QuartermasterHero?.Name?.ToString() ?? "QUARTERMASTER";
            MBTextManager.SetTextVariable("QM_NAME", qmName);

            // Lord name
            var lordName = enlistment?.EnlistedLord?.Name?.ToString() ?? "the Lord";
            MBTextManager.SetTextVariable("LORD_NAME", lordName);

            // Current tier
            var tier = enlistment?.EnlistmentTier ?? 1;
            MBTextManager.SetTextVariable("PLAYER_TIER", tier.ToString());
        }

        /// <summary>
        ///     Checks if the current conversation is with the quartermaster hero.
        /// </summary>
        private bool IsQuartermasterConversation()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return false;
            }

            var qm = enlistment.QuartermasterHero;
            return qm != null && qm.IsAlive && Hero.OneToOneConversationHero == qm;
        }

        /// <summary>
        ///     Checks if the player has met the current quartermaster before.
        /// </summary>
        private bool HasMetQuartermaster()
        {
            return EnlistmentBehavior.Instance?.HasMetQuartermaster == true;
        }

        /// <summary>
        ///     Dynamically registers dialogue from JSON catalog for a given node ID.
        ///     Creates player lines for all options in the node and handles action routing.
        /// </summary>
        private void RegisterJsonDialogueNode(CampaignGameStarter starter, string nodeId, int basePriority)
        {
            try
            {
                // Get ALL node variants for this ID (not just the one matching current context at registration time)
                var allNodes = QMDialogueCatalog.Instance.GetAllNodesById(nodeId);

                if (allNodes == null || allNodes.Count == 0)
                {
                    ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"No JSON node found for '{nodeId}'");
                    return;
                }

                ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"Found {allNodes.Count} node variants for '{nodeId}'");

                // Filter to nodes that have player options (QM nodes with options = player's turn to respond)
                var nodesWithOptions = allNodes.Where(n => n.Options != null && n.Options.Count > 0).ToList();

                ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"After filtering, {nodesWithOptions.Count} nodes have options for '{nodeId}'");
                foreach (var node in allNodes)
                {
                    var optCount = node.Options?.Count ?? 0;
                    ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"  Node speaker='{node.Speaker}' context_specificity={node.Context?.GetSpecificity() ?? 0} options={optCount}");
                }

                if (nodesWithOptions.Count == 0)
                {
                    ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"No nodes with player options found for '{nodeId}'");
                    return;
                }

                // Register player options for each contextual variant
                // Sort by specificity (most specific first) to assign highest priority to most specific
                var sortedNodes = nodesWithOptions.OrderByDescending(n => n.Context?.GetSpecificity() ?? 0).ToList();

                // Track registered option IDs to avoid duplicates when multiple node variants have identical options
                var registeredOptionIds = new HashSet<string>();

                var totalOptionsRegistered = 0;
                foreach (var node in sortedNodes)
                {
                    var nodeContext = node.Context; // Capture for closure
                    var specificity = nodeContext?.GetSpecificity() ?? 0;

                    ModLogger.Debug("ENLISTEDDIALOGMANAGER",
                        $"Registering player options for '{nodeId}' variant (specificity={specificity}) with {node.Options.Count} options");

                    // Register each player option as a dialogue line
                    var priority = basePriority - (sortedNodes.Count - 1 - sortedNodes.IndexOf(node)) * 100;
                    foreach (var option in node.Options)
                    {
                        // Skip if this option ID has already been registered (prevents duplicates across node variants)
                        if (registeredOptionIds.Contains(option.Id))
                        {
                            ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"Skipping duplicate option '{option.Id}' for node '{nodeId}'");
                            continue;
                        }

                        registeredOptionIds.Add(option.Id);

                        // Build TextObject pattern string so Bannerlord resolves it at display time (when variables are set)
                        // Pattern: {=textId}fallback allows XML localization with runtime variable substitution
                        var optionText = !string.IsNullOrEmpty(option.TextId)
                            ? $"{{={option.TextId}}}{option.Text ?? string.Empty}"
                            : option.Text ?? string.Empty;

                        // Check if this option has a gate condition
                        if (option.Gate != null && !string.IsNullOrEmpty(option.Gate.Condition) && !string.IsNullOrEmpty(option.Gate.GateNode))
                        {
                            // Register TWO versions of this option:
                            // 1. Version that passes the gate (normal behavior)
                            // 2. Version that fails the gate (redirects to gate node)

                            // Version 1: Gate condition PASSES (higher priority)
                            var outputState = string.IsNullOrEmpty(option.NextNode) ? "close_window" : option.NextNode;

                            ConversationSentence.OnConditionDelegate passCondition = () =>
                            {
                                // Set text variables at runtime so {PLAYER_NAME} etc. are substituted
                                SetCommonDialogueVariables();

                                // Only check parent node context if option has requirements
                                // This allows options without requirements to appear regardless of parent node variant
                                if (option.Requirements != null)
                                {
                                    var currentContext = GetCurrentDialogueContext();
                                    if (nodeContext != null && !nodeContext.Matches(currentContext))
                                    {
                                        return false;
                                    }
                                    if (!CheckOptionRequirements(option.Requirements))
                                    {
                                        return false;
                                    }
                                }

                                return CheckGateCondition(option.Gate.Condition);
                            };

                            ConversationSentence.OnConsequenceDelegate passConsequence = null;
                            if (!string.IsNullOrEmpty(option.Action) && option.Action != "none")
                            {
                                passConsequence = () => ExecuteDialogueAction(option.Action, option.ActionData);
                            }

                            starter.AddPlayerLine(
                                $"json_{nodeId}_spec{specificity}_{option.Id}_pass",
                                nodeId,
                                outputState,
                                optionText,
                                passCondition,
                                passConsequence,
                                priority);

                            // Version 2: Gate condition FAILS (redirects to gate node)
                            ConversationSentence.OnConditionDelegate failCondition = () =>
                            {
                                // Set text variables at runtime so {PLAYER_NAME} etc. are substituted
                                SetCommonDialogueVariables();

                                // Only check parent node context if option has requirements
                                // This allows options without requirements to appear regardless of parent node variant
                                if (option.Requirements != null)
                                {
                                    var currentContext = GetCurrentDialogueContext();
                                    if (nodeContext != null && !nodeContext.Matches(currentContext))
                                    {
                                        return false;
                                    }
                                    if (!CheckOptionRequirements(option.Requirements))
                                    {
                                        return false;
                                    }
                                }

                                // Show this version only if gate fails
                                return !CheckGateCondition(option.Gate.Condition);
                            };

                            starter.AddPlayerLine(
                                $"json_{nodeId}_spec{specificity}_{option.Id}_fail",
                                nodeId,
                                option.Gate.GateNode, // Redirect to gate node
                                optionText,
                                failCondition,
                                null, // No action when gated
                                priority - 1); // Lower priority than pass version

                            priority -= 2; // Account for both versions
                            totalOptionsRegistered += 2;
                        }
                        else
                        {
                            // Normal option without gate
                            var outputState = string.IsNullOrEmpty(option.NextNode) ? "close_window" : option.NextNode;

                            ConversationSentence.OnConditionDelegate conditionDelegate = () =>
                            {
                                // Set text variables at runtime so {PLAYER_NAME} etc. are substituted
                                SetCommonDialogueVariables();

                                // Only check parent node context if option has requirements
                                // This allows options without requirements to appear regardless of parent node variant
                                if (option.Requirements != null)
                                {
                                    var currentContext = GetCurrentDialogueContext();
                                    if (nodeContext != null && !nodeContext.Matches(currentContext))
                                    {
                                        return false;
                                    }
                                    if (!CheckOptionRequirements(option.Requirements))
                                    {
                                        return false;
                                    }
                                }

                                return true;
                            };

                            ConversationSentence.OnConsequenceDelegate consequenceDelegate = null;
                            if (!string.IsNullOrEmpty(option.Action) && option.Action != "none")
                            {
                                consequenceDelegate = () => ExecuteDialogueAction(option.Action, option.ActionData);
                            }

                            starter.AddPlayerLine(
                                $"json_{nodeId}_spec{specificity}_{option.Id}",
                                nodeId,
                                outputState,
                                optionText,
                                conditionDelegate,
                                consequenceDelegate,
                                priority--);

                            totalOptionsRegistered++;
                        }
                    }
                }

                ModLogger.Info("ENLISTEDDIALOGMANAGER",
                    $"Registered {totalOptionsRegistered} player option variants for '{nodeId}' across {sortedNodes.Count} node variants");
            }
            catch (Exception ex)
            {
                ModLogger.Error("ENLISTEDDIALOGMANAGER", $"Failed to register JSON node '{nodeId}'", ex);
            }
        }

        /// <summary>
        ///     Registers QM dialogue lines for all contextual variants of a node.
        ///     Each variant gets its own AddDialogLine with a runtime condition that checks context matching.
        ///     This enables dynamic context-aware dialogue (e.g., different hub greetings based on supply level).
        /// </summary>
        private void RegisterJsonQuartermasterLines(CampaignGameStarter starter, string nodeId, string inputToken, int basePriority)
        {
            try
            {
                // Get all node variants for this ID from the catalog
                var allNodes = QMDialogueCatalog.Instance.GetAllNodesById(nodeId);

                if (allNodes == null || allNodes.Count == 0)
                {
                    ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"No QM nodes found for '{nodeId}'");
                    return;
                }

                // Register a dialogue line for each contextual variant
                // Sort by specificity (most specific first) to assign highest priority to most specific
                var sortedNodes = allNodes.OrderByDescending(n => n.Context?.GetSpecificity() ?? 0).ToList();

                var priority = basePriority;
                foreach (var node in sortedNodes)
                {
                    if (node.Speaker != "quartermaster")
                    {
                        continue; // Only register QM lines, not player lines
                    }

                    var specificity = node.Context?.GetSpecificity() ?? 0;
                    var nodeContext = node.Context; // Capture for closure

                    // Build TextObject pattern string so Bannerlord resolves it at display time (when variables are set)
                    // Pattern: {=textId}fallback allows XML localization with runtime variable substitution
                    var qmText = !string.IsNullOrEmpty(node.TextId)
                        ? $"{{={node.TextId}}}{node.Text ?? string.Empty}"
                        : node.Text ?? string.Empty;

                    // Create condition that checks if current game context matches this node's requirements
                    ConversationSentence.OnConditionDelegate condition = () =>
                    {
                        // Must be talking to the quartermaster for any QM dialogue
                        if (!IsQuartermasterConversation())
                        {
                            return false;
                        }

                        // Set text variables (PLAYER_NAME, PLAYER_RANK, etc.) before dialogue displays
                        SetCommonDialogueVariables();

                        var currentContext = GetCurrentDialogueContext();
                        // If node has no context requirements, it's a universal fallback
                        if (nodeContext == null || nodeContext.GetSpecificity() == 0)
                        {
                            return true;
                        }
                        return nodeContext.Matches(currentContext);
                    };

                    // Register the QM line with dynamic context checking
                    starter.AddDialogLine(
                        $"json_qm_{nodeId}_spec{specificity}_{priority}",
                        inputToken,
                        nodeId, // Output token is the node ID (where player options attach)
                        qmText,
                        condition,
                        null, // No consequence needed
                        priority--);

                    ModLogger.Debug("ENLISTEDDIALOGMANAGER",
                        $"Registered QM line: {nodeId} (specificity={specificity}, priority={priority + 1})");
                }

                ModLogger.Info("ENLISTEDDIALOGMANAGER",
                    $"Registered {sortedNodes.Count(n => n.Speaker == "quartermaster")} QM line variants for '{nodeId}'");
            }
            catch (Exception ex)
            {
                ModLogger.Error("ENLISTEDDIALOGMANAGER", $"Failed to register QM lines for '{nodeId}'", ex);
            }
        }

        /// <summary>
        ///     Gets the current dialogue context for node matching.
        /// </summary>
        private QMDialogueContext GetCurrentDialogueContext()
        {
            var playerTier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
            var qmReputation = EnlistmentBehavior.Instance?.GetQMReputation() ?? 0;

            var context = new QMDialogueContext
            {
                IsIntroduced = HasMetQuartermaster(),
                PlayerTier = playerTier,
                TierCategory = GetTierCategory(playerTier),
                IsCavalry = IsCavalryFormation(),
                IsOfficer = playerTier >= 7,
                SupplyLevel = GetSupplyLevelCategory(),
                Archetype = EnlistmentBehavior.Instance?.QuartermasterArchetype ?? "veteran",
                ReputationTier = GetReputationTierCategory(qmReputation),
                BaggageRequestType = _baggageRequestType,
                BaggageAccess = GetBaggageAccessCategory(),
                BaggageDelayed = IsBaggageDelayed()
            };
            return context;
        }

        /// <summary>
        ///     Gets the supply level category for context-aware dialogue.
        /// </summary>
        private string GetSupplyLevelCategory()
        {
            var supplyPercent = CompanySupplyManager.Instance?.TotalSupply ?? 50;

            if (supplyPercent >= 80)
            {
                return "excellent";
            }
            if (supplyPercent >= 60)
            {
                return "good";
            }
            if (supplyPercent >= 40)
            {
                return "fair";
            }
            if (supplyPercent >= 20)
            {
                return "low";
            }
            return "critical";
        }

        /// <summary>
        ///     Gets the tier category (enlisted/nco/officer) for a given player tier.
        /// </summary>
        private string GetTierCategory(int tier)
        {
            if (tier >= 7)
            {
                return "officer";
            }
            if (tier >= 5)
            {
                return "nco";
            }
            return "enlisted";
        }

        /// <summary>
        ///     Gets the reputation tier category for context-aware dialogue.
        /// </summary>
        private string GetReputationTierCategory(int reputation)
        {
            if (reputation >= 65)
            {
                return "trusted";
            }
            if (reputation >= 35)
            {
                return "friendly";
            }
            if (reputation >= 10)
            {
                return "neutral";
            }
            if (reputation >= -10)
            {
                return "wary";
            }
            return "hostile";
        }

        /// <summary>
        ///     Checks if player is in cavalry formation (can request horses).
        /// </summary>
        private bool IsCavalryFormation()
        {
            var equipment = Hero.MainHero?.BattleEquipment;
            if (equipment == null)
            {
                return false;
            }

            // Has horse equipped = cavalry
            return !equipment[EquipmentIndex.Horse].IsEmpty;
        }

        /// <summary>
        ///     Gets the current baggage access state as a string category for dialogue matching.
        /// </summary>
        private string GetBaggageAccessCategory()
        {
            var baggageManager = Logistics.BaggageTrainManager.Instance;
            if (baggageManager == null)
            {
                return "no_access";
            }

            var accessState = baggageManager.GetCurrentAccess();
            return accessState switch
            {
                Logistics.BaggageAccessState.FullAccess => "full_access",
                Logistics.BaggageAccessState.TemporaryAccess => "temporary_access",
                Logistics.BaggageAccessState.Locked => "locked",
                Logistics.BaggageAccessState.NoAccess => "no_access",
                _ => "no_access"
            };
        }

        /// <summary>
        ///     Checks if the baggage train is currently delayed (stuck, separated, etc).
        /// </summary>
        private bool IsBaggageDelayed()
        {
            var baggageManager = Logistics.BaggageTrainManager.Instance;
            if (baggageManager == null)
            {
                return false;
            }

            return baggageManager.GetBaggageDelayDaysRemaining() > 0;
        }

        /// <summary>
        ///     Checks if dialogue option requirements are met.
        /// </summary>
        private bool CheckOptionRequirements(QMDialogueContext requirements)
        {
            if (requirements == null)
            {
                return true;
            }

            var actual = GetCurrentDialogueContext();
            return requirements.Matches(actual);
        }

        /// <summary>
        ///     Checks if a gate condition is met.
        ///     Returns true if the condition passes (player meets requirements).
        ///     Returns false if the condition fails (should redirect to gate node).
        /// </summary>
        private bool CheckGateCondition(string gateCondition)
        {
            if (string.IsNullOrEmpty(gateCondition))
            {
                return true;
            }

            var context = GetCurrentDialogueContext();

            // Parse gate conditions (supports simple string checks)
            switch (gateCondition.ToLowerInvariant())
            {
                case "cavalry_unlocked":
                    // Check if cavalry is available at player's tier for their culture
                    return QuartermasterManager.Instance?.IsCavalryUnlockedForPlayer() ?? false;

                case "is_cavalry":
                    // Legacy: Check if player currently has a horse equipped
                    return context.IsCavalry.GetValueOrDefault(false);

                case "is_officer":
                    return context.IsOfficer.GetValueOrDefault(false);

                case "tier_min_7":
                    return context.PlayerTier.GetValueOrDefault(1) >= 7;

                case "reputation_min_neg10":
                    var repNeg10 = EnlistmentBehavior.Instance?.GetQMReputation() ?? 0;
                    return repNeg10 >= -10;

                case "reputation_min_30":
                    var rep30 = EnlistmentBehavior.Instance?.GetQMReputation() ?? 0;
                    return rep30 >= 30;

                case "reputation_min_60":
                    var rep60 = EnlistmentBehavior.Instance?.GetQMReputation() ?? 0;
                    return rep60 >= 60;

                case "reputation_min_61":
                    var rep61 = EnlistmentBehavior.Instance?.GetQMReputation() ?? 0;
                    return rep61 >= 61;

                case "cooldown_not_active":
                    // Check if emergency baggage access cooldown is not active
                    var baggageManager = BaggageTrainManager.Instance;
                    if (baggageManager == null)
                    {
                        return false;
                    }
                    // Cooldown check: returns true if NOT on cooldown
                    return !baggageManager.IsEmergencyAccessOnCooldown();

                default:
                    ModLogger.Expected("ENLISTEDDIALOGMANAGER", "dialog_unknown_gate", $"Unknown gate condition '{gateCondition}' — option will be treated as gated (hidden/redirected). Add a handler to CheckGateCondition or fix the JSON.");
                    return false;
            }
        }

        /// <summary>
        ///     Executes a dialogue action from JSON.
        /// </summary>
        private void ExecuteDialogueAction(string action, Dictionary<string, object> actionData)
        {
            try
            {
                ModLogger.Debug("ENLISTEDDIALOGMANAGER", $"Executing dialogue action: {action}");

                switch (action)
                {
                    case "open_weapons":
                        _selectedEquipmentCategory = "weapons";
                        OnQuartermasterBrowseCategory();
                        break;

                    case "open_armor":
                        _selectedEquipmentCategory = "armor";
                        OnQuartermasterBrowseCategory();
                        break;

                    case "open_armor_body":
                        _selectedArmorSlot = EquipmentIndex.Body;
                        OnQuartermasterArmorSlotSelected();
                        break;

                    case "open_armor_head":
                        _selectedArmorSlot = EquipmentIndex.Head;
                        OnQuartermasterArmorSlotSelected();
                        break;

                    case "open_armor_hands":
                        _selectedArmorSlot = EquipmentIndex.Gloves;
                        OnQuartermasterArmorSlotSelected();
                        break;

                    case "open_armor_legs":
                        _selectedArmorSlot = EquipmentIndex.Leg;
                        OnQuartermasterArmorSlotSelected();
                        break;

                    case "open_accessories":
                        _selectedEquipmentCategory = "accessories";
                        OnQuartermasterBrowseCategory();
                        break;

                    case "open_mounts":
                        _selectedEquipmentCategory = "mounts";
                        OnQuartermasterBrowseCategory();
                        break;

                    case "open_upgrade":
                        OnQuartermasterUpgradeRequest();
                        break;

                    case "open_officers_armory":
                        OnQuartermasterOfficersArmoryRequest();
                        break;

                    case "open_sell":
                        OnQuartermasterSellRequest();
                        break;

                    case "open_provisions":
                        OnQuartermasterProvisionsRequest();
                        break;

                    case "close":
                        // Conversation closes naturally
                        break;

                    case "set_introduced":
                        OnQuartermasterFirstMeeting();
                        break;

                    case "set_player_style":
                        ExecuteSetPlayerStyleAction(actionData);
                        break;

                    case "grant_emergency_access":
                        OnGrantEmergencyBaggageAccess();
                        break;

                    case "halt_column_for_baggage":
                        OnHaltColumnForBaggage();
                        break;

                    default:
                        ModLogger.Expected("ENLISTEDDIALOGMANAGER", "dialog_unknown_action", $"Unknown dialogue action: '{action}' — this option will not do anything. Dialogue JSON references an action with no handler.");
                        InformationManager.DisplayMessage(new InformationMessage(
                            new TextObject("{=dialog_unknown_action}That option is misconfigured and does nothing. Please report this bug.").ToString()));
                        break;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("ENLISTEDDIALOGMANAGER", $"Failed to execute dialogue action '{action}'", ex);
            }
        }

        /// <summary>
        ///     Checks if the player can sell equipment (has sellable items).
        /// </summary>
        private bool CanSellEquipment()
        {
            // Always available - the menu will show what can be sold
            return true;
        }

        /// <summary>
        ///     Checks if the player can chat with the quartermaster.
        ///     Available on first meeting or at high relationship (40+).
        /// </summary>
        private bool CanChatWithQuartermaster()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return false;
            }

            // Chat available: first time (not met), or high relationship
            return !enlistment.HasMetQuartermaster || enlistment.QuartermasterRelationship >= 40;
        }

        // ========================================
        // PAYTENSION DIALOG CONDITIONS
        // ========================================

        /// <summary>
        ///     Checks if Scoundrel black market option is available.
        ///     Requires: Scoundrel archetype + PayTension 40+ + relationship 20+
        /// </summary>
        private bool IsScoundrelBlackMarketAvailable()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return false;
            }

            return enlistment.QuartermasterArchetype == "scoundrel"
                && enlistment.PayTension >= 40
                && enlistment.QuartermasterRelationship >= 20;
        }

        /// <summary>
        ///     Checks if Believer moral guidance option is available.
        ///     Requires: Believer archetype + PayTension 60+
        /// </summary>
        private bool IsBelieverGuidanceAvailable()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return false;
            }

            return enlistment.QuartermasterArchetype == "believer"
                && enlistment.PayTension >= 60;
        }

        /// <summary>
        ///     Checks if Veteran survival advice option is available.
        ///     Requires: Veteran archetype + PayTension 80+
        /// </summary>
        private bool IsVeteranAdviceAvailable()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return false;
            }

            return enlistment.QuartermasterArchetype == "veteran"
                && enlistment.PayTension >= 80;
        }

        /// <summary>
        ///     Checks if "Help the Lord" suggestion is available.
        ///     Requires: PayTension 40+ (any archetype)
        /// </summary>
        private bool IsHelpLordAvailable()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return false;
            }

            return enlistment.PayTension >= 40;
        }

        // ========================================
        // EQUIPMENT CATEGORY CONDITIONS
        // ========================================

        /// <summary>
        ///     Checks if there are weapon variants available for the player's troop tree.
        /// </summary>
        private bool HasWeaponVariantsAvailable()
        {
            try
            {
                var qm = QuartermasterManager.Instance;
                if (qm == null)
                {
                    return false;
                }

                // Use reflection to call the private BuildWeaponOptionsFromCurrentTroop method
                var method = typeof(QuartermasterManager).GetMethod("BuildWeaponOptionsFromCurrentTroop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return true; // Default to available if can't check
                }

                var options = method.Invoke(qm, null) as Dictionary<EquipmentIndex, List<EquipmentVariantOption>>;
                return options != null && options.Any(kvp => kvp.Value?.Any(opt => !opt.IsAtLimit) == true);
            }
            catch
            {
                return true; // Default to available on error
            }
        }

        /// <summary>
        ///     Checks if there are armor variants available for the player's troop tree.
        /// </summary>
        private bool HasArmorVariantsAvailable()
        {
            try
            {
                var qm = QuartermasterManager.Instance;
                if (qm == null)
                {
                    return false;
                }

                var method = typeof(QuartermasterManager).GetMethod("BuildArmorOptionsFromCurrentTroop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return true;
                }

                var options = method.Invoke(qm, null) as Dictionary<EquipmentIndex, List<EquipmentVariantOption>>;
                if (options == null)
                {
                    return false;
                }

                // Check armor slots (body, head, leg, gloves, cape)
                var armorSlots = new[] { EquipmentIndex.Body, EquipmentIndex.Head,
                    EquipmentIndex.Leg, EquipmentIndex.Gloves, EquipmentIndex.Cape };
                return armorSlots.Any(slot => options.ContainsKey(slot) && options[slot].Any(opt => !opt.IsAtLimit));
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        ///     Checks if there are accessory variants available (capes, shields, harness).
        /// </summary>
        private bool HasAccessoryVariantsAvailable()
        {
            try
            {
                var qm = QuartermasterManager.Instance;
                if (qm == null)
                {
                    return false;
                }

                // Accessories include capes from armor, shields from weapons, and harness
                // For now, always show if QM is available since accessories are common
                return EnlistmentBehavior.Instance?.IsEnlisted == true;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        ///     Checks if there are mount variants available.
        /// </summary>
        private bool HasMountVariantsAvailable()
        {
            try
            {
                var qm = QuartermasterManager.Instance;
                if (qm == null)
                {
                    return false;
                }

                // Mounts are available if player's formation supports cavalry or player wants a horse
                return EnlistmentBehavior.Instance?.IsEnlisted == true;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        ///     Checks if any equipment is available for purchase (weapons, armor, or accessories).
        ///     Used for the main "I'm looking for gear" option.
        /// </summary>
        private bool HasAnyEquipmentAvailable()
        {
            try
            {
                return EnlistmentBehavior.Instance?.IsEnlisted == true &&
                       (HasWeaponVariantsAvailable() || HasArmorVariantsAvailable() || HasAccessoryVariantsAvailable());
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        ///     Checks if player has unlocked cavalry (can purchase horses).
        ///     Horses are available if the player's troop tree includes mounts at their current tier.
        /// </summary>
        private bool IsCavalryUnlocked()
        {
            // Cavalry unlocked if mounts are available in the player's troop tree
            return HasMountVariantsAvailable();
        }

        /// <summary>
        ///     Checks if a specific armor slot has variants available.
        /// </summary>
        private bool HasArmorSlotVariants(EquipmentIndex slot)
        {
            try
            {
                var qm = QuartermasterManager.Instance;
                if (qm == null)
                {
                    return false;
                }

                var method = typeof(QuartermasterManager).GetMethod("BuildArmorOptionsFromCurrentTroop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return true;
                }

                var options = method.Invoke(qm, null) as Dictionary<EquipmentIndex, List<EquipmentVariantOption>>;
                return options != null && options.ContainsKey(slot) && options[slot].Any(opt => !opt.IsAtLimit);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        ///     Sets the SUPPLY_STATUS text variable for supply inquiry response.
        ///     Enhanced with archetype flavor, reputation tone, and strategic context.
        ///     Returns true always (used in condition chain).
        /// </summary>
        private bool SetSupplyStatusText()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    MBTextManager.SetTextVariable("SUPPLY_STATUS", "Can't say much about supplies right now.");
                    return true;
                }

                var companyNeeds = enlistment.CompanyNeeds;
                if (companyNeeds == null)
                {
                    MBTextManager.SetTextVariable("SUPPLY_STATUS", "Hard to say. Things are... complicated.");
                    return true;
                }

                var supplies = companyNeeds.Supplies;
            var morale = companyNeeds.Readiness; // Morale removed, using readiness
                var archetype = enlistment.QuartermasterArchetype;
                var reputation = enlistment.QuartermasterRelationship;

                // Get strategic context if available
                var strategicContext = "";
                var lordSettlement = (Settlement)null;
                try
                {
                    var lord = enlistment.EnlistedLord;
                    if (lord?.PartyBelongedTo != null)
                    {
                        strategicContext = ArmyContextAnalyzer.GetLordStrategicContext(lord.PartyBelongedTo);
                        lordSettlement = lord.PartyBelongedTo.CurrentSettlement;
                    }
                }
                catch
                {
                    // Strategic context is optional
                }

                // Build contextual supply report with archetype flavor
                string statusText = GetSupplyReportWithArchetypeFlavor(supplies, morale,
                    archetype, reputation, strategicContext, lordSettlement);

                MBTextManager.SetTextVariable("SUPPLY_STATUS", statusText);
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "Failed to set supply status text", ex);
                MBTextManager.SetTextVariable("SUPPLY_STATUS", "Hard to say. Things are... complicated.");
                return true;
            }
        }

        /// <summary>
        ///     Generates a supply report with archetype personality and reputation tone.
        ///     Includes strategic context awareness (winter, battle prep, etc.) and resupply status when in settlements.
        ///     Fully bulletproof with null checks, validation, and fallbacks.
        /// </summary>
        private string GetSupplyReportWithArchetypeFlavor(int supplies, int morale,
            string archetype, int reputation, string strategicContext, Settlement lordSettlement = null)
        {
            // Validate and normalize inputs
            archetype = ValidateArchetype(archetype);
            strategicContext = strategicContext ?? string.Empty;

            // Clamp values to safe ranges
            supplies = Clamp(supplies, 0, 100);
            morale = Clamp(morale, 0, 100);

            // Determine supply level category
            string supplyLevel;
            if (supplies >= 80)
            {
                supplyLevel = "excellent";
            }
            else if (supplies >= 60)
            {
                supplyLevel = "good";
            }
            else if (supplies >= 40)
            {
                supplyLevel = "fair";
            }
            else if (supplies >= 20)
            {
                supplyLevel = "low";
            }
            else
            {
                supplyLevel = "critical";
            }

            // Determine reputation tone (hostile, neutral, friendly, trusted)
            string repTone;
            if (reputation < 0)
            {
                repTone = "hostile";
            }
            else if (reputation < 31)
            {
                repTone = "neutral";
            }
            else if (reputation < 61)
            {
                repTone = "friendly";
            }
            else
            {
                repTone = "trusted";
            }

            // Build string ID and load from XML with fallback
            string stringId = BuildSupplyStringId(archetype, supplyLevel, repTone);
            string baseReport = GetLocalizedTextSafe(stringId,
                $"Supplies are at {supplies}%.");

            // Add morale context if critically low or exceptionally high
            if (morale < 30)
            {
                string moraleStringId = $"qm_morale_low_{archetype}";
                string moraleNote = GetLocalizedTextSafe(moraleStringId, " Morale is very low.");
                baseReport += moraleNote;
            }
            else if (morale >= 80)
            {
                string moraleStringId = $"qm_morale_high_{archetype}";
                string moraleNote = GetLocalizedTextSafe(moraleStringId, " Morale is high.");
                baseReport += moraleNote;
            }

            // Add strategic context if relevant
            if (!string.IsNullOrEmpty(strategicContext))
            {
                string contextStringId = strategicContext switch
                {
                    "winter_camp" => "qm_context_winter",
                    "preparing_siege" => "qm_context_siege",
                    "preparing_battle" => "qm_context_battle",
                    "long_march" => "qm_context_march",
                    "raiding" when archetype == "veteran" => "qm_context_raid_veteran",
                    "raiding" when archetype == "merchant" => "qm_context_raid_merchant",
                    "raiding" when archetype == "scoundrel" => "qm_context_raid_scoundrel",
                    "raiding" when archetype == "believer" => "qm_context_raid_believer",
                    "raiding" when archetype == "bookkeeper" => "qm_context_raid_bookkeeper",
                    "raiding" when archetype == "eccentric" => "qm_context_raid_eccentric",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(contextStringId))
                {
                    string contextNote = GetLocalizedTextSafe(contextStringId, "");
                    if (!string.IsNullOrEmpty(contextNote))
                    {
                        baseReport += contextNote;
                    }
                }
            }

            // Add resupply context if in a settlement (hourly resupply is active)
            // Simplified flavor without percentages - just acknowledges we're resupplying
            if (lordSettlement != null && (lordSettlement.IsTown || lordSettlement.IsCastle) && supplies < 95)
            {
                string resupplyStringId = $"qm_resupply_{archetype}_standard";
                string resupplyNote = GetLocalizedTextSafe(resupplyStringId, 
                    " We're taking on supplies while we're here.");
                baseReport += resupplyNote;
            }

            return baseReport;
        }

        /// <summary>
        ///     Clamps a value between min and max (Math.Clamp not available in older .NET versions).
        /// </summary>
        private int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        /// <summary>
        ///     Safely retrieves a localized string with fallback handling.
        ///     Ensures the conversation never breaks due to missing strings or errors.
        /// </summary>
        private string GetLocalizedTextSafe(string stringId, string fallback)
        {
            try
            {
                var text = GetLocalizedText(stringId)?.ToString();
                // If string not found, Bannerlord returns the ID itself or empty string
                if (string.IsNullOrEmpty(text) || text == stringId)
                {
                    return fallback;
                }
                return text;
            }
            catch (Exception ex)
            {
                // Log the error for debugging but don't break the conversation
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Enlisted] Failed to load string '{stringId}': {ex.Message}",
                    Colors.Yellow));
                return fallback;
            }
        }

        /// <summary>
        ///     Validates and normalizes archetype names.
        ///     Returns "default" for invalid/unknown archetypes.
        /// </summary>
        private string ValidateArchetype(string archetype)
        {
            if (string.IsNullOrEmpty(archetype))
            {
                return "default";
            }

            var validArchetypes = new[] { "veteran", "merchant", "bookkeeper", "scoundrel", "believer", "eccentric" };
            return validArchetypes.Contains(archetype.ToLower()) ? archetype.ToLower() : "default";
        }

        /// <summary>
        ///     Builds the XML string ID for supply reports based on archetype, level, and reputation.
        /// </summary>
        private string BuildSupplyStringId(string archetype, string supplyLevel, string repTone)
        {
            // Normalize archetype
            archetype = ValidateArchetype(archetype);

            // Use default fallback if archetype is invalid
            if (archetype == "default")
            {
                return $"qm_supply_default_{supplyLevel}";
            }

            // Try specific archetype + level + reputation combo first
            if ((archetype == "merchant" && supplyLevel == "good" && repTone == "friendly") ||
                (archetype == "merchant" && supplyLevel == "fair" && repTone == "friendly") ||
                (archetype == "merchant" && supplyLevel == "low" && repTone == "trusted") ||
                (archetype == "bookkeeper" && supplyLevel == "fair" && repTone == "friendly") ||
                (archetype == "bookkeeper" && supplyLevel == "low" && repTone == "trusted") ||
                (archetype == "veteran" && supplyLevel == "low" && repTone == "trusted") ||
                (archetype == "veteran" && supplyLevel == "critical" && repTone == "trusted") ||
                (archetype == "scoundrel" && supplyLevel == "excellent" && repTone == "friendly") ||
                (archetype == "scoundrel" && supplyLevel == "good" && repTone == "friendly") ||
                (archetype == "scoundrel" && supplyLevel == "fair" && repTone == "friendly") ||
                (archetype == "scoundrel" && supplyLevel == "low" && repTone == "trusted") ||
                (archetype == "scoundrel" && supplyLevel == "critical" && repTone == "trusted") ||
                (archetype == "believer" && supplyLevel == "fair" && repTone == "friendly") ||
                (archetype == "believer" && supplyLevel == "low" && repTone == "trusted") ||
                (archetype == "believer" && supplyLevel == "critical" && repTone == "trusted") ||
                (archetype == "eccentric" && supplyLevel == "fair" && repTone == "friendly") ||
                (archetype == "eccentric" && supplyLevel == "low" && repTone == "trusted"))
            {
                return $"qm_supply_{archetype}_{supplyLevel}_{repTone}";
            }

            // Try archetype + level (most common case)
            return $"qm_supply_{archetype}_{supplyLevel}";
        }

        /// <summary>
        ///     Sets the BROWSE_RESPONSE text variable for contextual equipment browsing.
        ///     Varies by supply level, reputation, and archetype.
        ///     Returns true always (used in condition chain).
        /// </summary>
        private bool SetBrowseResponseText()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    MBTextManager.SetTextVariable("BROWSE_RESPONSE", "Let me see what's in stock.");
                    return true;
                }

                var companyNeeds = enlistment.CompanyNeeds;
                var supplies = companyNeeds?.Supplies ?? 60;
                var archetype = enlistment.QuartermasterArchetype;
                var reputation = enlistment.QuartermasterRelationship;
                var tier = enlistment.EnlistmentTier;
                var rankTitle = RankHelper.GetCurrentRank(enlistment);

                // Calculate discount percentage based on reputation
                var discountPct = GetReputationDiscount(reputation);

                // Generate contextual browse response with rank, supply, rep, and price hints
                string response = GetBrowseResponse(supplies, reputation, tier, rankTitle, discountPct);

                MBTextManager.SetTextVariable("BROWSE_RESPONSE", response);
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "Failed to set browse response text", ex);
                MBTextManager.SetTextVariable("BROWSE_RESPONSE", "Let me see what's in stock.");
                return true;
            }
        }

        /// <summary>
        ///     Gets the discount percentage based on QM reputation.
        /// </summary>
        private int GetReputationDiscount(int reputation)
        {
            if (reputation >= 80)
            {
                return 30;      // Trusted - best prices
            }
            if (reputation >= 50)
            {
                return 20;      // Friendly - good prices
            }
            if (reputation >= 20)
            {
                return 10;      // Neutral - fair prices
            }
            if (reputation >= 0)
            {
                return 5;        // Wary - slight discount
            }
            return 0;                              // Hostile - no discount
        }

        /// <summary>
        ///     Sets the SELL_RESPONSE text variable for contextual selling dialogue.
        ///     Varies by mood, reputation, and archetype.
        ///     Returns true always (used in condition chain).
        /// </summary>
        private bool SetSellResponseText()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    MBTextManager.SetTextVariable("SELL_RESPONSE", "Show me what you've got.");
                    return true;
                }

                var archetype = enlistment.QuartermasterArchetype;
                var reputation = enlistment.QuartermasterRelationship;
                var mood = GetQuartermasterMood();
                var companyNeeds = enlistment.CompanyNeeds;
                var supplies = companyNeeds?.Supplies ?? 60;

                // Generate contextual sell response
                string response = GetSellResponse(archetype, reputation, mood, supplies);

                MBTextManager.SetTextVariable("SELL_RESPONSE", response);
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "Failed to set sell response text", ex);
                MBTextManager.SetTextVariable("SELL_RESPONSE", "Show me what you've got.");
                return true;
            }
        }

        /// <summary>
        ///     Sets the UPGRADE_RESPONSE text variable for contextual upgrade dialogue.
        ///     Varies by archetype and reputation.
        ///     Returns true always (used in condition chain).
        /// </summary>
        private bool SetUpgradeResponseText()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    MBTextManager.SetTextVariable("UPGRADE_RESPONSE", "Aye, bring me what you've got. Good work costs good coin.");
                    return true;
                }

                var archetype = enlistment.QuartermasterArchetype;
                var reputation = enlistment.QuartermasterRelationship;

                // Generate contextual upgrade response
                string response = GetUpgradeResponse(archetype, reputation);

                MBTextManager.SetTextVariable("UPGRADE_RESPONSE", response);
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "Failed to set upgrade response text", ex);
                MBTextManager.SetTextVariable("UPGRADE_RESPONSE", "Aye, bring me what you've got. Good work costs good coin.");
                return true;
            }
        }

        /// <summary>
        ///     Generates a contextual browse response based on supply levels, archetype, and reputation.
        ///     Uses XML localization strings with full validation and fallback handling.
        /// </summary>
        private string GetBrowseResponse(int suppliesParam, int reputation, int tier = 1, string rankTitle = "Soldier", int discountPct = 0)
        {
            // Validate and normalize inputs
            int supplies = Clamp(suppliesParam, 0, 100);

            // Build a contextual response with multiple parts:
            // 1. Stock/supply context
            // 2. Rank acknowledgment (for higher tiers)
            // 3. Price/discount hint based on reputation

            var parts = new List<string>();

            // Part 1: Stock situation
            if (supplies >= 80)
            {
                parts.Add("Plenty in stock. Recent resupply came through.");
            }
            else if (supplies >= 60)
            {
                parts.Add("We've a fair bit in stock.");
            }
            else if (supplies >= 40)
            {
                parts.Add("We've got what you'd expect.");
            }
            else if (supplies >= 20)
            {
                parts.Add("Stock's thin right now. We're stretched.");
            }
            else
            {
                parts.Add("Pickings are slim. We're rationing everything.");
            }

            // Part 2: Rank acknowledgment (tier 5+ gets acknowledged)
            if (tier >= 7)
            {
                parts.Add($"For an officer of your standing, {rankTitle}, I can show you the better pieces.");
            }
            else if (tier >= 5)
            {
                parts.Add($"You've earned some pull around here, {rankTitle}.");
            }

            // Part 3: Price/discount hint based on reputation
            if (reputation >= 80)
            {
                parts.Add($"Prices are good for you—{discountPct}% off. You've earned it.");
            }
            else if (reputation >= 50)
            {
                parts.Add($"I'll cut you a fair deal—{discountPct}% off standard.");
            }
            else if (reputation >= 20)
            {
                parts.Add("Prices are standard. Nothing fancy.");
            }
            else if (reputation >= 0)
            {
                parts.Add("Prices are what they are. Don't expect favors.");
            }
            else
            {
                parts.Add("Full price. No discounts for troublemakers.");
            }

            // Part 4: Category prompt
            parts.Add("What are you after—weapons, armor, accessories?");

            return string.Join(" ", parts);
        }

        /// <summary>
        ///     Generates a contextual sell response based on archetype, reputation, mood, and supply levels.
        ///     Uses XML localization strings with full validation and fallback handling.
        /// </summary>
        private string GetSellResponse(string archetype, int reputation, string mood, int supplies)
        {
            // Validate and normalize inputs
            archetype = ValidateArchetype(archetype);
            mood = (mood ?? "content").ToLower();
            supplies = Clamp(supplies, 0, 100);

            // Determine reputation tone
            bool trusted = reputation >= 61;

            // Determine if supplies are low (might affect buyback willingness)
            bool lowSupplies = supplies < 40;

            // Build string ID based on context priority: mood > reputation > supplies
            string stringId;

            if (mood == "content" && trusted)
            {
                stringId = $"qm_sell_content_trusted_{archetype}";
            }
            else if (mood == "content")
            {
                stringId = $"qm_sell_content_{archetype}";
            }
            else if (mood == "stressed")
            {
                stringId = $"qm_sell_stressed_{archetype}";
            }
            else if (mood == "grim" && lowSupplies)
            {
                stringId = $"qm_sell_grim_lowsup_{archetype}";
            }
            else if (mood == "grim")
            {
                stringId = $"qm_sell_grim_{archetype}";
            }
            else
            {
                // Unknown mood - use content as default
                stringId = $"qm_sell_content_{archetype}";
            }

            return GetLocalizedTextSafe(stringId, "Show me what you've got. I'll give you a fair price.");
        }

        /// <summary>
        ///     Generates a contextual upgrade response based on archetype and reputation.
        ///     Uses XML localization strings with full validation and fallback handling.
        /// </summary>
        private string GetUpgradeResponse(string archetype, int reputation)
        {
            // Validate and normalize inputs
            archetype = ValidateArchetype(archetype);

            // Determine reputation tone
            bool hostile = reputation < 0;
            bool trusted = reputation >= 61;

            // Build string ID based on reputation
            string stringId;

            if (trusted)
            {
                stringId = $"qm_upgrade_trusted_{archetype}";
            }
            else if (hostile)
            {
                stringId = $"qm_upgrade_hostile_{archetype}";
            }
            else
            {
                stringId = $"qm_upgrade_default_{archetype}";
            }

            return GetLocalizedTextSafe(stringId, "Aye, bring me what you've got. Good work costs good coin.");
        }

        #endregion

        #region Quartermaster Dialog Text Generation

        /// <summary>
        ///     Gets the quartermaster's greeting text based on archetype, mood, and first meeting status.
        ///     Called dynamically when the greeting dialog is displayed.
        /// </summary>
        public string GetQuartermasterGreeting()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return "What do you need, soldier?";
            }

            var archetype = enlistment.QuartermasterArchetype;
            var isFirstMeeting = !enlistment.HasMetQuartermaster;
            var mood = GetQuartermasterMood();
            var qmName = enlistment.QuartermasterHero?.Name?.ToString() ?? "QUARTERMASTER";

            if (isFirstMeeting)
            {
                return GetFirstMeetingGreeting(archetype, qmName);
            }

            return GetReturningGreeting(archetype, mood);
        }

        /// <summary>
        ///     Gets the first-meeting introduction based on archetype and tier.
        ///     Uses XML localized strings for proper translation support.
        ///     Rank-aware: Officers get deferential intro, NCOs get respected intro, Enlisted get casual intro.
        /// </summary>
        private string GetFirstMeetingGreeting(string archetype, string qmName)
        {
            var tier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;

            // Get rank category suffix for localization lookup
            var rankSuffix = tier >= 7 ? "_officer" : tier >= 5 ? "_nco" : "_enlisted";

            // Use the appropriate localized intro greeting based on archetype and rank
            var stringId = $"qm_intro_greeting_{archetype ?? "default"}{rankSuffix}";
            var fallback = GetFirstMeetingFallback(archetype, tier);

            // Use TextObject for XML localization lookup
            var text = new TextObject($"{{={stringId}}}{fallback}");
            text.SetTextVariable("QM_NAME", qmName);
            return text.ToString();
        }

        /// <summary>
        ///     Gets the fallback text for first-meeting greetings based on archetype and tier.
        /// </summary>
        private string GetFirstMeetingFallback(string archetype, int tier)
        {
            // T7+ Officers get deferential treatment
            if (tier >= 7)
            {
                return archetype switch
                {
                    "veteran" => "My lord. I wasn't told we had a new officer. I'm at your service.",
                    "merchant" => "My lord! An officer I haven't had the pleasure of serving. How may I assist you?",
                    "bookkeeper" => "My lord. I don't have your requisitions on file. A new assignment? I'll update the ledger.",
                    "scoundrel" => "My lord! Always a pleasure to serve the officer corps. What can I do for you?",
                    "believer" => "The Lord has blessed our command with new leadership. How may I serve, my lord?",
                    "eccentric" => "A new star in the heavens! The omens foretold of your arrival, my lord. Welcome.",
                    _ => "My lord. I wasn't told to expect you. How may I serve?"
                };
            }

            // T5-T6 NCOs get respected treatment, addressed by rank
            if (tier >= 5)
            {
                var cultureId = EnlistmentBehavior.Instance?.EnlistedLord?.Culture?.StringId ?? "mercenary";
                var rankTitle = RankHelper.GetRankTitle(tier, cultureId);
                return archetype switch
                {
                    "veteran" => $"New face with stripes. You must be the new {rankTitle}. What do you need?",
                    "merchant" => $"A {rankTitle} I haven't done business with. Good to meet you. What can I sell?",
                    "bookkeeper" => $"{rankTitle}. I don't have you in my records yet. New transfer? I'll update the files.",
                    "scoundrel" => $"A {rankTitle}, eh? Good to have some experienced hands. What can I do for you?",
                    "believer" => $"The Lord sends us another soul with authority. Welcome, {rankTitle}.",
                    "eccentric" => $"The cards spoke of a new {rankTitle}. The stars align in your favor. Welcome.",
                    _ => $"Haven't seen you before, {rankTitle}. What do you need?"
                };
            }

            // T1-T4 Enlisted get casual treatment
            return archetype switch
            {
                "veteran" => "New face. You one of the fresh recruits? What do they call you?",
                "merchant" => "Ah, a new customer. Haven't done business with you before. Name?",
                "bookkeeper" => "I don't have you in my ledger. New arrival? State your name for the record.",
                "scoundrel" => "Well, well. Fresh meat. What's your name, soldier?",
                "believer" => "The Lord sends us new brothers in arms. What name were you given, friend?",
                "eccentric" => "A new star in my constellation! What sign were you born under? ...No, wait. What's your name?",
                _ => "Haven't seen you before. You new? What's your name?"
            };
        }

        /// <summary>
        ///     Gets the returning visit greeting based on archetype, mood, tier, and PayTension level.
        ///     Now includes tension-aware greetings at 40+, 60+, and 80+ thresholds.
        ///     Rank-aware: Officers get "my lord", NCOs get rank title, Enlisted get "soldier".
        /// </summary>
        private string GetReturningGreeting(string archetype, string mood)
        {
            // Check PayTension for tension-aware greetings
            var tension = EnlistmentBehavior.Instance?.PayTension ?? 0;

            // Critical tension (80+) - all archetypes show concern
            if (tension >= 80)
            {
                return GetCriticalTensionGreeting(archetype);
            }

            // High tension (60+) - archetype-specific warnings
            if (tension >= 60)
            {
                return GetHighTensionGreeting(archetype);
            }

            // Moderate tension (40+) - subtle hints
            if (tension >= 40)
            {
                return GetModerateTensionGreeting(archetype);
            }

            // Get player tier for rank-aware greetings
            var tier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;

            // T7+ Officers get deferential "my lord" treatment
            if (tier >= 7)
            {
                return GetOfficerGreeting(archetype, mood);
            }

            // T5-T6 NCOs get addressed by rank
            if (tier >= 5)
            {
                return GetNCOGreeting(archetype, mood);
            }

            // T1-T4 Enlisted get informal/soldier treatment
            return GetEnlistedGreeting(archetype, mood);
        }

        /// <summary>
        ///     Gets greeting for T7+ officers. Deferential "my lord" treatment.
        /// </summary>
        private string GetOfficerGreeting(string archetype, string mood)
        {
            return (archetype, mood) switch
            {
                ("veteran", "content") => "My lord. Good to see you. What do you require?",
                ("veteran", "stressed") => "My lord. Busy day, but I'm at your service.",
                ("veteran", "grim") => "Supply's tight, my lord. What do you need?",
                ("merchant", "content") => "My lord! A pleasure. What can I provide for you today?",
                ("merchant", "stressed") => "My lord, prices are up, but I'll find you something. What do you need?",
                ("merchant", "grim") => "Difficult times, my lord. But I'll do what I can.",
                ("bookkeeper", "content") => "My lord. Your requisition forms are in order. What do you require?",
                ("bookkeeper", "stressed") => "My lord. The inventory is a mess, but I'll find what you need.",
                ("bookkeeper", "grim") => "Everything is behind schedule, my lord. What is it?",
                ("scoundrel", "content") => "My lord! Always a pleasure serving the brass. What can I do for you?",
                ("scoundrel", "stressed") => "My lord. Things are getting tight, but not for someone of your standing.",
                ("scoundrel", "grim") => "Bad times, my lord. Very bad times. But I'll see you right.",
                ("believer", "content") => "The Lord blesses your command, my lord. How may I serve?",
                ("believer", "stressed") => "Trying times test us all, my lord. What do you require?",
                ("believer", "grim") => "Dark days, my lord, but the faithful endure. What do you need?",
                ("eccentric", "content") => "My lord! The stars foretold your coming. What do you seek?",
                ("eccentric", "stressed") => "The celestial alignments are troubling, my lord. What brings you?",
                ("eccentric", "grim") => "Dark omens, my lord. Dark omens everywhere. What?",
                _ => "My lord. What do you require?"
            };
        }

        /// <summary>
        ///     Gets greeting for T5-T6 NCOs. Addressed by rank with respect.
        /// </summary>
        private string GetNCOGreeting(string archetype, string mood)
        {
            var tier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 5;
            var cultureId = EnlistmentBehavior.Instance?.EnlistedLord?.Culture?.StringId ?? "mercenary";
            var rankTitle = RankHelper.GetRankTitle(tier, cultureId);

            return (archetype, mood) switch
            {
                ("veteran", "content") => $"{rankTitle}. Good to see you. What do you need?",
                ("veteran", "stressed") => $"Busy day, {rankTitle}. Make it quick.",
                ("veteran", "grim") => $"Supply's tight, {rankTitle}. What?",
                ("merchant", "content") => $"Ah, {rankTitle}! A valued customer. What can I sell you?",
                ("merchant", "stressed") => $"Prices are up, {rankTitle}. Supply issues. What do you need?",
                ("merchant", "grim") => $"Difficult times, {rankTitle}. But I'll do what I can.",
                ("bookkeeper", "content") => $"{rankTitle}. Equipment requisition? Let me find the form.",
                ("bookkeeper", "stressed") => $"The inventory is a mess, {rankTitle}. What is it?",
                ("bookkeeper", "grim") => $"Everything is behind schedule, {rankTitle}. What?",
                ("scoundrel", "content") => $"{rankTitle}! One of my favorite regulars. What are you looking for?",
                ("scoundrel", "stressed") => $"Things are getting tight, {rankTitle}. Need something?",
                ("scoundrel", "grim") => $"Bad times, {rankTitle}. Very bad times. What do you need?",
                ("believer", "content") => $"The Lord's blessings upon you, {rankTitle}. How may I serve?",
                ("believer", "stressed") => $"Trying times test our faith, {rankTitle}. What do you need?",
                ("believer", "grim") => $"Pray for us all, {rankTitle}. What can I do for you?",
                ("eccentric", "content") => $"Ah, {rankTitle}! The stars spoke of your coming. What do you seek?",
                ("eccentric", "stressed") => $"The alignments are troubled, {rankTitle}. What brings you?",
                ("eccentric", "grim") => $"Dark omens everywhere, {rankTitle}. What?",
                _ => $"What do you need, {rankTitle}?"
            };
        }

        /// <summary>
        ///     Gets greeting for T1-T4 enlisted. Informal "soldier" treatment.
        /// </summary>
        private string GetEnlistedGreeting(string archetype, string mood)
        {
            return (archetype, mood) switch
            {
                ("veteran", "content") => "Back again? What do you need, soldier?",
                ("veteran", "stressed") => "Busy day. Make it quick.",
                ("veteran", "grim") => "Supply's tight. What?",
                ("merchant", "content") => "Good to see you. What can I sell you today?",
                ("merchant", "stressed") => "Prices are up. Supply issues. What do you need?",
                ("merchant", "grim") => "Not a good time for shopping. Make it fast.",
                ("bookkeeper", "content") => "Soldier. Equipment requisition? Let me find the form.",
                ("bookkeeper", "stressed") => "The inventory is a mess. What is it?",
                ("bookkeeper", "grim") => "Everything is behind schedule. What?",
                ("scoundrel", "content") => "Ah, one of the lads. What are you looking for today?",
                ("scoundrel", "stressed") => "Things are getting tight around here. Need something?",
                ("scoundrel", "grim") => "Bad times, friend. Very bad times. What do you need?",
                ("believer", "content") => "The Lord's blessings upon you, brother. How may I serve?",
                ("believer", "stressed") => "Trying times test our faith. What do you need?",
                ("believer", "grim") => "Pray for us all, soldier. What can I do for you?",
                ("eccentric", "content") => "The ravens spoke of your coming. What do you seek?",
                ("eccentric", "stressed") => "The stars are misaligned today. Be careful what you ask for.",
                ("eccentric", "grim") => "Dark omens everywhere. What?",
                _ => "What do you need, soldier?"
            };
        }

        /// <summary>
        ///     Gets greeting when PayTension is 80+ (critical).
        ///     All archetypes express serious concern about the situation.
        /// </summary>
        private string GetCriticalTensionGreeting(string archetype)
        {
            return archetype switch
            {
                "veteran" => "You're still here? Half the company's talking desertion. The lord better pay up soon or there won't be anyone left to pay.",
                "merchant" => "Ah, friend. These are dark days. Even my best suppliers won't extend credit anymore. The whole operation is falling apart.",
                "bookkeeper" => "The accounts... they're a disaster. Unpaid wages everywhere. I don't know how much longer we can hold things together.",
                "scoundrel" => "Looking to disappear? I know some people... for a price. This whole army is one bad battle away from scattering like leaves.",
                "believer" => "The men's faith is being tested beyond measure. Many are losing hope. Pray with me, soldier, for we are in dire straits.",
                "eccentric" => "The stars scream of betrayal and abandonment. The wheel of fortune has turned against us. Do you hear the whispers of the damned?",
                _ => "Things are very bad. What do you need?"
            };
        }

        /// <summary>
        ///     Gets greeting when PayTension is 60-79 (high).
        ///     Archetypes provide specific warnings and hints.
        /// </summary>
        private string GetHighTensionGreeting(string archetype)
        {
            return archetype switch
            {
                "veteran" => "The men are angry. Real angry. I've seen mutinies start from less than this. Watch your back.",
                "merchant" => "Payment's been... irregular. My credit with suppliers is stretched thin. We may need to discuss... alternatives.",
                "bookkeeper" => "The ledgers don't balance. Too many 'delayed' entries. This is unsustainable.",
                "scoundrel" => "Desperate times, friend. People are getting creative about finding coin. If you need anything... unofficial...",
                "believer" => "The men's spirits are breaking. Some are losing their way. Perhaps if you could help the lord find coin, their faith would be restored.",
                "eccentric" => "I dreamed of empty coffers and marching boots heading home. An ill omen. Very ill.",
                _ => "Times are getting hard. What do you need?"
            };
        }

        /// <summary>
        ///     Gets greeting when PayTension is 40-59 (moderate).
        ///     Subtle hints that something is wrong.
        /// </summary>
        private string GetModerateTensionGreeting(string archetype)
        {
            return archetype switch
            {
                "veteran" => "The lads are grumbling about pay. Nothing serious yet, but keep your ears open.",
                "merchant" => "Cash flow is getting tight. Might be some opportunities for a resourceful soldier, if you take my meaning.",
                "bookkeeper" => "There are some... irregularities in the pay records. I'm sure it will be sorted soon.",
                "scoundrel" => "Heard some interesting rumors about the lord's finances. Nothing that concerns you, of course... unless you want it to.",
                "believer" => "Some of the men are wavering. Material concerns are clouding their devotion. Perhaps you could set an example.",
                "eccentric" => "The coins in my pouch keep spinning counterclockwise. That usually means someone isn't getting paid.",
                _ => "What brings you here today?"
            };
        }

        /// <summary>
        ///     Gets the quartermaster's response to equipment request based on archetype.
        ///     Note: Reserved for future dynamic dialog enhancement.
        /// </summary>
        [Obsolete("Reserved for future dynamic dialog. Currently using static localized strings.")]
        public string GetEquipmentResponseLine()
        {
            var archetype = EnlistmentBehavior.Instance?.QuartermasterArchetype ?? "veteran";

            return archetype switch
            {
                "veteran" => "Equipment requisition. Let me see what I've got.",
                "merchant" => "Looking to upgrade? Smart. Follow me to the armory.",
                "bookkeeper" => "Equipment requisition approved. Please review the inventory.",
                "scoundrel" => "Official channels or... off the books?",
                "believer" => "The armory is open. May you choose wisely.",
                "eccentric" => "The stars favor shields today. Or swords. Maybe both.",
                _ => "This way."
            };
        }

        /// <summary>
        ///     Gets the quartermaster's response to sell request based on mood.
        /// </summary>
        public string GetSellResponseLine()
        {
            var mood = GetQuartermasterMood();

            return mood switch
            {
                "content" => "Let's see what you've got. I'll give you fair value.",
                "stressed" => "Selling? Fine. Prices are low right now.",
                "grim" => "Not buying much today. Supply issues.",
                _ => "What are you selling?"
            };
        }

        /// <summary>
        ///     Gets the quartermaster's response to provisions request based on archetype.
        ///     Note: Reserved for future dynamic dialog enhancement.
        /// </summary>
        [Obsolete("Reserved for future dynamic dialog. Currently using static localized strings.")]
        public string GetProvisionsResponseLine()
        {
            var archetype = EnlistmentBehavior.Instance?.QuartermasterArchetype ?? "veteran";

            return archetype switch
            {
                "veteran" => "Hungry? Smart soldier keeps his belly full. Let me show you what I've got.",
                "merchant" => "Looking to upgrade from hardtack? I've got options. Quality costs, mind you.",
                "bookkeeper" => "Provisions requisition. I'll need to check the allocation limits for your rank.",
                "scoundrel" => "Want something better than the slop they serve the regulars? I might know a source.",
                "believer" => "The body is a temple. You're wise to nourish it properly. What do you need?",
                "eccentric" => "The ravens say the meat is fresh today. The bread too. Maybe. Let me show you.",
                _ => "Looking for better rations? Follow me."
            };
        }

        /// <summary>
        ///     Gets the quartermaster's backstory based on archetype.
        ///     Uses {LORD_NAME} placeholder for dynamic lord reference.
        ///     Note: Reserved for future dynamic dialog enhancement.
        /// </summary>
        [Obsolete("Reserved for future dynamic dialog. Currently using static localized strings.")]
        public string GetQuartermasterBackstory()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var archetype = enlistment?.QuartermasterArchetype ?? "veteran";

            var text = archetype switch
            {
                "veteran" => new TextObject("Lost my sword arm at Pendraic. {LORD_NAME} gave me this posting. Beats starving. Now I make sure other soldiers don't go hungry."),
                "merchant" => new TextObject("I ran caravans for twenty years. Profitable, but dangerous. This is safer and steadier. Plus, I know every trader from here to Zeonica."),
                "bookkeeper" => new TextObject("I clerked for the imperial bureaucracy for fifteen years. When the war came, they needed someone who could count. Here I am."),
                "scoundrel" => new TextObject("Let's just say I had some... disagreements... with the city guard. {LORD_NAME} offered me a choice: noose or supply wagon. Easy choice."),
                "believer" => new TextObject("I served as a priest before the war. But faith without works is dead. Now I serve by ensuring the faithful are fed and armed."),
                "eccentric" => new TextObject("The stars told me I'd manage supplies for a great army. Twelve years ago. On a Tuesday. In the rain. And here I am, just as foretold."),
                _ => new TextObject("I've been doing this for years. It's honest work.")
            };

            // Direct unit name variables are being updated.
            if (enlistment != null)
            {
                // Dynamic name replacement will be updated to support the new unit structure.
            }

            return text.ToString();
        }

        // ========================================
        // PAYTENSION DIALOG TEXT GENERATION
        // Reserved for future dynamic dialog enhancement.
        // Currently using static localized strings in dialog registration.
        // ========================================

        /// <summary>
        ///     Gets the Scoundrel's black market response.
        ///     Note: Reserved for future dynamic dialog enhancement.
        /// </summary>
        [Obsolete("Reserved for future dynamic dialog. Currently using static localized strings.")]
        public string GetBlackMarketResponseLine()
        {
            var tension = EnlistmentBehavior.Instance?.PayTension ?? 0;

            if (tension >= 80)
            {
                return "Alternative supplies? Ha! Half the camp is already selling what they can carry. " +
                       "I know some merchants who don't ask questions... for a cut, of course. " +
                       "Smuggled goods, looted gear, you name it. Times like these, everyone's a little flexible with the rules.";
            }
            else if (tension >= 60)
            {
                return "Looking to make some coin on the side? I might know some people. " +
                       "There's always a market for... surplus equipment. Weapons that fell off wagons, rations that weren't counted right. " +
                       "Just don't get caught. The lord's too broke to pay bribes right now.";
            }
            else
            {
                return "Interested in opportunities, eh? Smart. " +
                       "I can point you toward some traders who pay premium for certain... acquisitions. " +
                       "Nothing too serious yet, but if things get worse, we might need to be more creative.";
            }
        }

        /// <summary>
        ///     Gets the Believer's moral guidance response.
        ///     Note: Reserved for future dynamic dialog enhancement.
        /// </summary>
        [Obsolete("Reserved for future dynamic dialog. Currently using static localized strings.")]
        public string GetMoralGuidanceResponseLine()
        {
            var tension = EnlistmentBehavior.Instance?.PayTension ?? 0;

            if (tension >= 80)
            {
                return "These are the darkest of times, my child. The men's faith is being tested beyond measure. " +
                       "Some have already abandoned their posts - and their honor. But you are still here. That means something. " +
                       "The heavens see our suffering. They see the lord's broken promises. Justice will come - one way or another. " +
                       "Until then, hold to your principles. That is all any of us can do.";
            }
            else
            {
                return "I see the doubt in your eyes. You are not alone. Many question why we suffer when we have served faithfully. " +
                       "Remember: material wealth fades, but honor endures. If the lord cannot pay, perhaps there are other ways to serve - " +
                       "helping him recover his fortune. The faithful are rewarded, in this life or the next.";
            }
        }

        /// <summary>
        ///     Gets the Veteran's survival advice response.
        ///     Note: Reserved for future dynamic dialog enhancement.
        /// </summary>
        [Obsolete("Reserved for future dynamic dialog. Currently using static localized strings.")]
        public string GetSurvivalAdviceResponseLine()
        {
            return "Things are bad, I won't lie to you. But we've been in tough spots before. " +
                   "Keep your head down, don't volunteer for anything stupid, and watch your purse. " +
                   "Some men are getting desperate - and desperate men do foolish things. " +
                   "If it gets worse... well, we'll talk then.";
        }

        /// <summary>
        ///     Gets the Help the Lord suggestion response based on archetype.
        ///     Uses {LORD_NAME} placeholder for dynamic lord reference.
        ///     Note: Reserved for future dynamic dialog enhancement.
        /// </summary>
        [Obsolete("Reserved for future dynamic dialog. Currently using static localized strings.")]
        public string GetHelpLordResponseLine()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var archetype = enlistment?.QuartermasterArchetype ?? "veteran";

            var text = archetype switch
            {
                "veteran" => new TextObject("You want to help? Good soldier. {LORD_NAME} needs gold, and fast. " +
                             "There's always bounty work, escort jobs, that sort of thing. Traders sometimes pay for protection. " +
                             "If we're at war, enemy supply wagons are fair game. Every denar helps."),

                "merchant" => new TextObject("Ah, thinking like a businessman! There are merchants who owe {LORD_NAME} money. " +
                              "Perhaps you could... encourage timely payment. Also, I hear there's profit in captured goods - " +
                              "just bring them to me and I'll handle the sale. Discretely."),

                "bookkeeper" => new TextObject("According to my records, there are several outstanding debts owed to {LORD_NAME}. " +
                                "If someone were to... collect these debts... the accounts would balance better. " +
                                "I can provide you with a list of debtors, if you're interested."),

                "scoundrel" => new TextObject("Help {LORD_NAME}? Ha! Plenty of ways, if you're not too squeamish. " +
                               "Enemy caravans, shakedowns, a little light robbery... all perfectly acceptable in wartime. " +
                               "Bring me the goods and I'll fence them. We split the profit - and the lord gets his cut."),

                "believer" => new TextObject("Your loyalty is commendable. Perhaps you could collect donations from the faithful? " +
                              "Or help {LORD_NAME} recover what is rightfully owed. " +
                              "There's also the matter of enemy supplies - liberating them serves a higher purpose."),

                "eccentric" => new TextObject("The stars aligned last night to show me a vision. A merchant caravan, heavy with gold, " +
                               "passing through dangerous territory. Perhaps they need protection? Perhaps they don't survive the journey? " +
                               "Either way, there's opportunity for the bold."),

                _ => new TextObject("The lord needs gold. Find work that pays and bring back what you can.")
            };

            // Direct unit name variables are being updated.
            if (enlistment != null)
            {
                // Dynamic name replacement will be updated to support the new unit structure.
            }

            return text.ToString();
        }

        /// <summary>
        ///     Gets the current quartermaster mood based on camp conditions.
        /// </summary>
        private string GetQuartermasterMood()
        {
            // Try to get mood from CampLifeBehavior if available
            var campLife = CampLifeBehavior.Instance;
            if (campLife != null && campLife.IsActiveWhileEnlisted())
            {
                // Map QuartermasterMoodTier enum to mood strings
                return campLife.QuartermasterMoodTier switch
                {
                    QuartermasterMoodTier.Fine => "content",
                    QuartermasterMoodTier.Tense => "stressed",
                    QuartermasterMoodTier.Sour => "grim",
                    QuartermasterMoodTier.Predatory => "grim",
                    _ => "content"
                };
            }

            // Fallback: Use pay tension as mood indicator
            var payTension = EnlistmentBehavior.Instance?.PayTension ?? 0;
            if (payTension < 20)
            {
                return "content";
            }
            if (payTension < 50)
            {
                return "stressed";
            }
            return "grim";
        }

        #endregion

        #region Quartermaster Dialog Consequences

        /// <summary>
        ///     Called on first meeting with the quartermaster.
        ///     Marks the quartermaster as met.
        /// </summary>
        private void OnQuartermasterFirstMeeting()
        {
            EnlistmentBehavior.Instance?.MarkQuartermasterMet();
            ModLogger.Info("CONVERSATIONS", "QM: First meeting with quartermaster completed");
        }

        /// <summary>
        ///     Executes the set_player_style action from JSON dialogue.
        ///     Sets the player's communication style with the QM and applies rep change.
        /// </summary>
        /// <param name="actionData">Action data containing "style" and "rep_change" fields</param>
        private void ExecuteSetPlayerStyleAction(Dictionary<string, object> actionData)
        {
            if (actionData == null)
            {
                ModLogger.Warn("ENLISTEDDIALOGMANAGER", "set_player_style action called without action_data");
                return;
            }

            var style = "military"; // Default
            var repChange = 0;

            if (actionData.TryGetValue("style", out var styleObj) && styleObj != null)
            {
                style = styleObj.ToString();
            }

            if (actionData.TryGetValue("rep_change", out var repObj) && repObj != null)
            {
                if (repObj is long repLong)
                {
                    repChange = (int)repLong;
                }
                else if (repObj is int repInt)
                {
                    repChange = repInt;
                }
                else if (int.TryParse(repObj.ToString(), out var parsed))
                {
                    repChange = parsed;
                }
            }

            EnlistmentBehavior.Instance?.SetQMPlayerStyle(style, repChange);
            ModLogger.Info("ENLISTEDDIALOGMANAGER", $"Player chose tone '{style}' with rep change {repChange}");
        }

        /// <summary>
        ///     Called when player selects an equipment category (weapons, accessories, mounts).
        ///     Opens Gauntlet UI for the selected category using NextFrameDispatcher.
        /// </summary>
        private void OnQuartermasterBrowseCategory()
        {
            try
            {
                EnlistmentBehavior.Instance?.ModifyQuartermasterRelationship(1);
                var category = _selectedEquipmentCategory;

                // Guard against empty category (shouldn't happen, but defensive)
                if (string.IsNullOrEmpty(category))
                {
                    ModLogger.Warn("CONVERSATIONS", "QM: Browse category called with empty category, defaulting to weapons");
                    category = "weapons";
                }

                ModLogger.Info("CONVERSATIONS", $"QM: Player selected {category} category");

                // Use NextFrameDispatcher so the conversation fully closes before Gauntlet opens
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    try
                    {
                        OpenGauntletForCategory(category);
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error("CONVERSATIONS", $"QM: Failed to open Gauntlet for {category}", ex);
                        // Try to return to conversation on error
                        RestartQuartermasterConversation();
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "QM: Failed to process browse category", ex);
            }
        }

        /// <summary>
        ///     Called when player selects an armor slot.
        ///     Opens Gauntlet UI filtered to that armor slot.
        /// </summary>
        private void OnQuartermasterArmorSlotSelected()
        {
            try
            {
                EnlistmentBehavior.Instance?.ModifyQuartermasterRelationship(1);
                var slot = _selectedArmorSlot;

                // Guard against unset slot (shouldn't happen, but defensive)
                if (slot == EquipmentIndex.None)
                {
                    ModLogger.Warn("CONVERSATIONS", "QM: Armor slot selection called with None, defaulting to Body");
                    slot = EquipmentIndex.Body;
                }

                ModLogger.Info("CONVERSATIONS", $"QM: Player selected armor slot {slot}");

                NextFrameDispatcher.RunNextFrame(() =>
                {
                    try
                    {
                        OpenGauntletForArmorSlot(slot);
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error("CONVERSATIONS", $"QM: Failed to open Gauntlet for armor slot {slot}", ex);
                        RestartQuartermasterConversation();
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "QM: Failed to process armor slot selection", ex);
            }
        }

        /// <summary>
        ///     Opens Gauntlet equipment selector for the specified category.
        /// </summary>
        private void OpenGauntletForCategory(string category)
        {
            var qm = QuartermasterManager.Instance;
            if (qm == null)
            {
                ModLogger.Warn("CONVERSATIONS", "QM: QuartermasterManager instance not available");
                return;
            }

            List<EquipmentVariantOption> variants;
            EquipmentIndex targetSlot;

            switch (category)
            {
                case "weapons":
                    variants = GetWeaponVariants(qm);
                    targetSlot = EquipmentIndex.Weapon0;
                    break;

                case "armor":
                    variants = GetAllArmorVariants(qm);
                    targetSlot = EquipmentIndex.Body;
                    break;

                case "accessories":
                    variants = GetAccessoryVariants(qm);
                    targetSlot = EquipmentIndex.Cape;
                    break;

                case "mounts":
                    variants = GetMountVariants(qm);
                    targetSlot = EquipmentIndex.Horse;
                    break;

                default:
                    ModLogger.Warn("CONVERSATIONS", $"QM: Unknown equipment category: {category}");
                    return;
            }

            if (variants == null || variants.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"No {category} available in stock.", Colors.Red));
                // Return to conversation since there's nothing to show
                RestartQuartermasterConversation();
                return;
            }

            ModLogger.Debug("CONVERSATIONS", $"QM: Opening Gauntlet for {category} with {variants.Count} variants");
            QuartermasterEquipmentSelectorBehavior.ShowEquipmentSelector(variants, targetSlot, category);
        }

        /// <summary>
        ///     Opens Gauntlet equipment selector for a specific armor slot.
        /// </summary>
        private void OpenGauntletForArmorSlot(EquipmentIndex slot)
        {
            var qm = QuartermasterManager.Instance;
            if (qm == null)
            {
                ModLogger.Warn("CONVERSATIONS", "QM: QuartermasterManager instance not available");
                return;
            }

            var variants = GetArmorSlotVariants(qm, slot);
            if (variants == null || variants.Count == 0)
            {
                var slotName = GetArmorSlotDisplayName(slot);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"No {slotName} available in stock.", Colors.Red));
                RestartQuartermasterConversation();
                return;
            }

            var slotDisplayName = GetArmorSlotDisplayName(slot);
            ModLogger.Debug("CONVERSATIONS", $"QM: Opening Gauntlet for armor slot {slotDisplayName} with {variants.Count} variants");
            QuartermasterEquipmentSelectorBehavior.ShowEquipmentSelector(variants, slot, slotDisplayName);
        }

        /// <summary>
        ///     Gets a display name for an armor slot.
        /// </summary>
        private static string GetArmorSlotDisplayName(EquipmentIndex slot)
        {
            return slot switch
            {
                EquipmentIndex.Head => "helmets",
                EquipmentIndex.Body => "body armor",
                EquipmentIndex.Gloves => "gloves",
                EquipmentIndex.Leg => "boots",
                EquipmentIndex.Cape => "capes",
                _ => "armor"
            };
        }

        /// <summary>
        ///     Gets weapon variants from QuartermasterManager.
        /// </summary>
        private List<EquipmentVariantOption> GetWeaponVariants(QuartermasterManager qm)
        {
            try
            {
                var method = typeof(QuartermasterManager).GetMethod("BuildWeaponOptionsFromCurrentTroop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                var options = method.Invoke(qm, null) as Dictionary<EquipmentIndex, List<EquipmentVariantOption>>;
                if (options == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                // Flatten all weapon variants into one list, excluding shields (shields go to accessories)
                return options
                    .SelectMany(kvp => kvp.Value)
                    .Where(opt => opt.Item?.WeaponComponent?.PrimaryWeapon?.IsShield != true)
                    .ToList();
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "QM: Failed to get weapon variants", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        ///     Gets accessory variants (capes, shields, harness) from QuartermasterManager.
        /// </summary>
        private List<EquipmentVariantOption> GetAccessoryVariants(QuartermasterManager qm)
        {
            try
            {
                var combined = new List<EquipmentVariantOption>();

                // Get capes from armor options
                var armorMethod = typeof(QuartermasterManager).GetMethod("BuildArmorOptionsFromCurrentTroop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (armorMethod != null)
                {
                    var armorOptions = armorMethod.Invoke(qm, null) as Dictionary<EquipmentIndex, List<EquipmentVariantOption>>;
                    if (armorOptions?.TryGetValue(EquipmentIndex.Cape, out var capeOptions) == true)
                    {
                        combined.AddRange(capeOptions);
                    }
                }

                // Get shields from weapon slots
                var shieldMethod = typeof(QuartermasterManager).GetMethod("BuildShieldOptionsFromWeapons",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (shieldMethod != null)
                {
                    var shieldOptions = shieldMethod.Invoke(qm, null) as List<EquipmentVariantOption>;
                    if (shieldOptions != null)
                    {
                        combined.AddRange(shieldOptions);
                    }
                }

                return combined;
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "QM: Failed to get accessory variants", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        ///     Gets mount variants from QuartermasterManager.
        ///     Uses direct public API instead of reflection for better maintainability.
        /// </summary>
        private List<EquipmentVariantOption> GetMountVariants(QuartermasterManager qm)
        {
            try
            {
                // Direct call to the public method - no reflection needed
                return qm.GetMountVariantsForBrowsing();
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "QM: Failed to get mount variants", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        ///     Gets armor variants for a specific slot.
        /// </summary>
        private List<EquipmentVariantOption> GetArmorSlotVariants(QuartermasterManager qm, EquipmentIndex slot)
        {
            try
            {
                var method = typeof(QuartermasterManager).GetMethod("BuildArmorOptionsFromCurrentTroop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                var options = method.Invoke(qm, null) as Dictionary<EquipmentIndex, List<EquipmentVariantOption>>;
                if (options?.TryGetValue(slot, out var slotVariants) == true)
                {
                    return slotVariants;
                }

                return new List<EquipmentVariantOption>();
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", $"QM: Failed to get armor variants for slot {slot}", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        ///     Gets all armor variants across all armor slots (head, body, gloves, legs, cape).
        ///     Used when player selects "I need armor" to show everything in one grid.
        /// </summary>
        private List<EquipmentVariantOption> GetAllArmorVariants(QuartermasterManager qm)
        {
            try
            {
                var method = typeof(QuartermasterManager).GetMethod("BuildArmorOptionsFromCurrentTroop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                var options = method.Invoke(qm, null) as Dictionary<EquipmentIndex, List<EquipmentVariantOption>>;
                if (options == null)
                {
                    return new List<EquipmentVariantOption>();
                }

                // Combine all armor slots into a single list (cape goes to accessories)
                var combined = new List<EquipmentVariantOption>();
                var armorSlots = new[]
                {
                    EquipmentIndex.Head,
                    EquipmentIndex.Body,
                    EquipmentIndex.Gloves,
                    EquipmentIndex.Leg
                };

                foreach (var slot in armorSlots)
                {
                    if (options.TryGetValue(slot, out var slotVariants))
                    {
                        combined.AddRange(slotVariants);
                    }
                }

                return combined;
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "QM: Failed to get all armor variants", ex);
                return new List<EquipmentVariantOption>();
            }
        }

        /// <summary>
        ///     Restarts the quartermaster conversation, returning player to qm_hub.
        ///     Called after Gauntlet closes or when there's nothing to show.
        /// </summary>
        public static void RestartQuartermasterConversation()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                var qmHero = enlistment?.QuartermasterHero;

                if (qmHero == null || !qmHero.IsAlive)
                {
                    ModLogger.Warn("CONVERSATIONS", "QM: Cannot restart conversation - QM hero not available");
                    return;
                }

                NextFrameDispatcher.RunNextFrame(() =>
                {
                    try
                    {
                        var party = QuartermasterPartyResolver.GetConversationParty(qmHero);
                        if (party == null)
                        {
                            ModLogger.Expected("CONVERSATIONS", "qm_conv_no_party",
                                "Both QM and enlisted lord have no party — cannot restart QM conversation with correct scene");
                            InformationManager.DisplayMessage(new InformationMessage(
                                new TextObject("{=qm_party_unavailable}The quartermaster cannot be reached right now.").ToString()));
                            return;
                        }

                        var playerData = new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty);
                        var qmData = new ConversationCharacterData(qmHero.CharacterObject, party);

                        // Use sea conversation scene if at sea, otherwise use map conversation
                        // Mirrors lord conversation behavior for proper scene selection
                        if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
                        {
                            const string seaConversationScene = "conversation_scene_sea_multi_agent";
                            ModLogger.Debug("CONVERSATIONS", $"QM: Opening sea conversation using scene: {seaConversationScene}");
                            CampaignMission.OpenConversationMission(playerData, qmData, seaConversationScene);
                        }
                        else
                        {
                            CampaignMapConversation.OpenConversation(playerData, qmData);
                            ModLogger.Debug("CONVERSATIONS", "QM: Opened land conversation");
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error("CONVERSATIONS", "QM: Failed to restart conversation", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.Error("CONVERSATIONS", "QM: Error in RestartQuartermasterConversation", ex);
            }
        }

        /// <summary>
        ///     Called when player wants to sell equipment.
        ///     Opens a popup inquiry for selling items back to the quartermaster.
        /// </summary>
        private void OnQuartermasterSellRequest()
        {
            try
            {
                // Increase relationship for interaction
                EnlistmentBehavior.Instance?.ModifyQuartermasterRelationship(1);

                // Open sell popup via QuartermasterManager
                var qm = QuartermasterManager.Instance;
                if (qm != null)
                {
                    qm.ShowSellPopup();
                    ModLogger.Info("QUARTERMASTER", "Opened sell popup from dialog");
                }
                else
                {
                    ModLogger.Error("QUARTERMASTER", "Cannot open sell popup: QuartermasterManager.Instance is null");
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=qm_return_error}Return processing unavailable.").ToString()));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTER", "Failed to open sell popup from dialog", ex);
            }
        }

        /// <summary>
        ///     Called when player chats with the quartermaster.
        ///     Increases relationship.
        /// </summary>
        private void OnQuartermasterChat()
        {
            // Increase relationship for chatting
            EnlistmentBehavior.Instance?.ModifyQuartermasterRelationship(5);
            ModLogger.Info("QUARTERMASTER", "Chat completed, relationship increased");
        }

        /// <summary>
        ///     Called when player requests provisions from the quartermaster.
        ///     T7+ only: Opens the Gauntlet provisions shop with food items.
        ///     T1-T6 receive issued rations at muster and shouldn't access this.
        /// </summary>
        private void OnQuartermasterProvisionsRequest()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                var playerTier = enlistment?.EnlistmentTier ?? 1;
                
                // All ranks can view provisions, but only T7+ can purchase (enforced in UI)
                ModLogger.Info("QUARTERMASTER", $"Opening provisions UI for T{playerTier} player");
                
                // Defer to next frame so the conversation fully closes before the UI activates.
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    try
                    {
                        QuartermasterProvisionsBehavior.ShowProvisionsScreen();
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Caught("QUARTERMASTER", "Failed to open provisions UI", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTER", "Failed to open provisions", ex);
            }
        }

        /// <summary>
        ///     Called when player requests equipment upgrades.
        ///     Opens the upgrade interface using NextFrameDispatcher.
        /// </summary>
        private void OnQuartermasterUpgradeRequest()
        {
            try
            {
                // Increase relationship for interaction
                EnlistmentBehavior.Instance?.ModifyQuartermasterRelationship(1);

                // Open upgrade screen after dialogue closes
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    QuartermasterEquipmentSelectorBehavior.ShowUpgradeScreen();
                });

                ModLogger.Info("QUARTERMASTER", "Player requested equipment upgrades");
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTER", "Error opening upgrade screen", ex);
            }
        }

        /// <summary>
        ///     Opens the Officers' Armory screen for T7+ players with high reputation.
        ///     This provides access to premium officer-grade equipment.
        /// </summary>
        private void OnQuartermasterOfficersArmoryRequest()
        {
            try
            {
                // Increase relationship for interaction (extra bonus for officer access)
                EnlistmentBehavior.Instance?.ModifyQuartermasterRelationship(2);

                // Set filter to officer equipment - this will be applied when the next category is browsed
                QuartermasterManager.Instance?.SetFilterToOfficerEquipment();

                ModLogger.Info("QUARTERMASTER", "Player accessed Officers' Armory - officer filter enabled");

                // Note: The dialogue should transition to qm_browse_response where player picks a category
                // The officer filter will be applied to whatever category they select
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTER", "Error enabling officers armory", ex);
            }
        }

        /// <summary>
        /// Handles emergency baggage access request during march.
        /// Attempts to grant temporary access via BaggageTrainManager, applying reputation costs.
        /// </summary>
        private void OnGrantEmergencyBaggageAccess()
        {
            try
            {
                var baggageManager = BaggageTrainManager.Instance;
                if (baggageManager == null)
                {
                    ModLogger.Error("BAGGAGE", "BaggageTrainManager not available for emergency access");
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=baggage_error}Unable to access baggage system.").ToString(),
                        Colors.Red));
                    return;
                }

                // Attempt to grant emergency access
                if (baggageManager.TryRequestEmergencyAccess(out var failReason))
                {
                    // Success - access granted
                    ModLogger.Info("BAGGAGE", "Emergency baggage access granted");
                    InformationManager.DisplayMessage(new InformationMessage(
                        new TextObject("{=baggage_granted}The quartermaster sends a runner. The baggage train will be brought forward shortly.").ToString(),
                        Colors.Green));

                    // Small relationship bonus for using proper channels
                    EnlistmentBehavior.Instance?.ModifyQuartermasterRelationship(1);
                    
                    // Open the baggage stash now that access is granted
                    EnlistmentBehavior.Instance?.TryOpenBaggageTrain();
                }
                else
                {
                    // Request denied - show appropriate message
                    ModLogger.Info("BAGGAGE", $"Emergency baggage access denied: {failReason}");

                    TextObject message;
                    switch (failReason)
                    {
                        case "Rank too low":
                            message = new TextObject("{=baggage_denied_tier}Your rank is too low to request emergency baggage access. Wait until camp.");
                            break;
                        case "Cooldown active":
                            message = new TextObject("{=baggage_denied_cooldown}You recently requested emergency access. Wait at least 12 hours before asking again.");
                            break;
                        default:
                            message = new TextObject("{=baggage_denied_generic}Emergency baggage access is not available right now.");
                            break;
                    }

                    InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Red));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BAGGAGE", "Error processing emergency baggage access", ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=baggage_error}Unable to process baggage request.").ToString(),
                    Colors.Red));
            }
        }

        /// <summary>
        /// Handles column halt request for T7+ commanders to bring baggage train forward.
        /// Stops the party and grants full access until movement resumes. Costs officer reputation.
        /// </summary>
        private void OnHaltColumnForBaggage()
        {
            try
            {
                var party = MobileParty.MainParty;
                if (party == null)
                {
                    ModLogger.Error("BAGGAGE", "MainParty not available for column halt");
                    return;
                }

                // Stop party movement (halt the column)
                if (party.IsMoving)
                {
                    party.Ai.SetDoNotMakeNewDecisions(true);
                    party.SetMoveModeHold();
                    ModLogger.Info("BAGGAGE", "Column halted for baggage access");
                }

                // Apply lord reputation cost (inconvenience to the column)
                var repCost = 3;
                var escalation = EscalationManager.Instance;
                if (escalation != null)
                {
                    escalation.ModifyLordReputation(-repCost); // Officer reputation removed, using lord relation
                }

                ModLogger.Info("BAGGAGE", $"Column halt granted: Full baggage access until movement resumes (cost: -{repCost} Officer Rep)");

                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=baggage_column_halt}The column halts. Wagons are brought forward and the baggage train is made accessible.").ToString(),
                    Colors.Green));
                
                // Open the baggage stash now that the column is halted
                EnlistmentBehavior.Instance?.TryOpenBaggageTrain();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("BAGGAGE", "Error processing column halt request", ex);
            }
        }

        /// <summary>
        ///     Checks if the player has any equipped items that can be upgraded.
        /// </summary>
        private bool HasUpgradeableEquipment()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null)
                {
                    return false;
                }

                var qm = QuartermasterManager.Instance;
                if (qm == null)
                {
                    return false;
                }

                var availableTiers = qm.GetAvailableUpgradeTiers();
                if (availableTiers == null || availableTiers.Count == 0)
                {
                    return false;
                }

                // Check all equipment slots for upgradeable items
                for (int i = 0; i < (int)EquipmentIndex.NumEquipmentSetSlots; i++)
                {
                    var slot = (EquipmentIndex)i;
                    var element = hero.BattleEquipment[slot];

                    // Skip empty slots
                    if (element.IsEmpty || element.Item == null)
                    {
                        continue;
                    }

                    // Skip items without modifier groups
                    var modGroup = element.Item.ItemComponent?.ItemModifierGroup;
                    if (modGroup == null)
                    {
                        continue;
                    }

                    // Skip items already at Legendary quality
                    var currentQuality = QuartermasterManager.GetModifierQuality(element.Item, element.ItemModifier);
                    if (currentQuality == ItemQuality.Legendary)
                    {
                        continue;
                    }

                    // Check if any upgrade tier is available for this item
                    foreach (var tier in availableTiers)
                    {
                        if (tier > currentQuality)
                        {
                            var modifiers = modGroup.GetModifiersBasedOnQuality(tier);
                            if (modifiers != null && modifiers.Count > 0)
                            {
                                // Found at least one upgradeable item
                                return true;
                            }
                        }
                    }
                }

                // No upgradeable items found
                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Error("QUARTERMASTER", "Error checking for upgradeable equipment", ex);
                return false;
            }
        }

        // ========================================
        // PAYTENSION DIALOG CONSEQUENCES
        // ========================================

        /// <summary>
        ///     Called when player asks Scoundrel for black market options.
        ///     Provides hints about illicit ways to acquire coin/supplies.
        /// </summary>
        private void OnQuartermasterBlackMarket()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            // Increase relationship for showing trust
            enlistment.ModifyQuartermasterRelationship(3);

            // Display the black market response text
            var tension = enlistment.PayTension;
            InformationManager.DisplayMessage(new InformationMessage(
                "The quartermaster shares some... alternative supply channels.",
                Colors.Magenta));

            ModLogger.Info("QUARTERMASTER", $"Black market dialog triggered (tension={tension})");
        }

        /// <summary>
        ///     Called when player asks Believer for moral guidance.
        ///     Provides morale boost and spiritual encouragement.
        /// </summary>
        private void OnQuartermasterMoralGuidance()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            // Increase relationship for seeking guidance
            enlistment.ModifyQuartermasterRelationship(5);

            InformationManager.DisplayMessage(new InformationMessage(
                "The quartermaster's words bring some comfort.",
                Colors.Green));

            ModLogger.Info("QUARTERMASTER", "Moral guidance dialog triggered");
        }

        /// <summary>
        ///     Called when player asks Veteran for survival advice.
        ///     Provides practical advice about desertion and survival.
        /// </summary>
        private void OnQuartermasterSurvivalAdvice()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            // Increase relationship for confiding
            enlistment.ModifyQuartermasterRelationship(2);

            InformationManager.DisplayMessage(new InformationMessage(
                "The veteran shares tips for surviving hard times. 'Keep your head down. Watch for opportunities.'",
                Colors.Yellow));

            ModLogger.Info("QUARTERMASTER", "Survival advice dialog triggered");
        }

        /// <summary>
        ///     Called when player asks how to help the lord.
        ///     Provides archetype-specific suggestions for reducing PayTension.
        /// </summary>
        private void OnQuartermasterHelpLordSuggestion()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return;
            }

            var archetype = enlistment.QuartermasterArchetype;
            var suggestion = archetype switch
            {
                "veteran" => "The lord needs gold, and fast. Look for bounty work, escort missions, anything that pays quick.",
                "merchant" => "I know some merchants who owe the lord money. Perhaps you could... encourage them to pay up.",
                "bookkeeper" => "There are discrepancies in the accounts. If you find evidence of embezzlement, the lord could recover funds.",
                "scoundrel" => "Plenty of ways to make coin if you're not too squeamish. Raid enemy supply lines, shake down travelers...",
                "believer" => "The faithful should help their lord in times of need. Perhaps donations could be collected, or enemy coffers... liberated.",
                "eccentric" => "The stars suggest a merchant caravan will pass by soon with more gold than guards. Just saying.",
                _ => "The lord needs coin. Find work that pays."
            };

            // Increase relationship for showing loyalty
            enlistment.ModifyQuartermasterRelationship(3);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Quartermaster advice: \"{suggestion}\"",
                Colors.Cyan));

            ModLogger.Info("QUARTERMASTER", "Help lord suggestion dialog triggered");
        }

        #endregion

        #region Utility Methods

        /// <summary>
        ///     Get localized text with fallback support.
        /// </summary>
        private TextObject GetLocalizedText(string key)
        {
            return new TextObject(key);
        }

#if false
        /// <summary>
        ///     Cleans up the enlisted menu after discharge/retirement.
        ///     Called deferred (next frame) after retirement dialog closes to ensure
        ///     the player isn't stuck on an empty menu with no options.
        /// </summary>
        private static void CleanupEnlistedMenuAfterDischarge()
        {
            try
            {
                // Only exit if we're still on the enlisted menu and not enlisted
                if (EnlistmentBehavior.Instance?.IsEnlisted != true)
                {
                    ModLogger.Info("DIALOGMANAGER", "Cleaning up enlisted menu after discharge");
                    GameMenu.ExitToLast();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DIALOGMANAGER", "Error cleaning up menu after discharge", ex);
            }
        }
#endif

        #endregion

        #region Shared Dialog Conditions

        /// <summary>
        ///     Checks if the player is on leave and talking to a lord from a DIFFERENT faction.
        ///     Used to trigger the roleplay rejection dialog ("You cannot serve two masters").
        /// </summary>
        private bool IsOnLeaveWithDifferentFaction()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            if (enlistment?.IsOnLeave != true || lord == null)
            {
                return false;
            }

            // If talking to current lord, this is "return from leave" - not a rejection
            if (enlistment.CurrentLord == lord)
            {
                return false;
            }

            // Check if different faction
            var currentLordKingdom = enlistment.CurrentLord?.MapFaction as Kingdom;
            var targetLordKingdom = lord.MapFaction as Kingdom;

            // Different faction (or one has no kingdom) = rejection
            if (currentLordKingdom == null || targetLordKingdom != currentLordKingdom)
            {
                ModLogger.Debug("DIALOGMANAGER",
                    $"Triggering faction rejection - on leave from {enlistment.CurrentLord?.Name}, talking to {lord.Name} (different faction)");
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Checks if the conversation partner is a valid lord for military service discussions.
        ///     This shared condition prevents dialog conflicts and ensures consistency.
        ///     CRITICAL: Prevents enlistment dialog from appearing if player is already enlisted, on leave, or in grace period.
        ///     Player must be fully discharged before they can enlist with another lord/kingdom.
        /// </summary>
        private bool IsValidLordForMilitaryService()
        {
            var lord = Hero.OneToOneConversationHero;
            if (lord == null || !lord.IsLord || !lord.IsAlive)
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;

            // Allow dialog when talking to current lord (for retirement, duties discussion, etc.)
            // Block dialog with OTHER lords when actively enlisted - player can't enlist elsewhere
            if (enlistment?.IsEnlisted == true && enlistment?.IsOnLeave != true)
            {
                if (enlistment?.CurrentLord != lord)
                {
                    ModLogger.Debug("DIALOGMANAGER",
                        $"Dialog hidden - player is actively enlisted with {enlistment?.CurrentLord?.Name}");
                    return false;
                }

                // Allow - player talking to their own lord (retirement, early discharge, etc.)
                ModLogger.Debug("DIALOGMANAGER", $"Dialog shown - player talking to their enlisted lord {lord.Name}");
                return true;
            }

            // When on leave, ALLOW dialog with all lords - we handle same vs different faction in the dialog itself
            // Same faction lords can accept transfers, different faction lords give roleplay rejection
            if (enlistment?.IsOnLeave == true)
            {
                if (enlistment.CurrentLord == lord)
                {
                    // Allow - player can return from leave with this lord
                    ModLogger.Debug("DIALOGMANAGER",
                        $"Dialog shown - player on leave, talking to their lord {lord.Name}");
                    return true;
                }

                // Allow dialog with ALL lords when on leave - rejection handled in dialog flow
                // This prevents "Missing dialog state" and gives roleplay rejection for different factions
                ModLogger.Debug("DIALOGMANAGER",
                    $"Dialog shown - player on leave, talking to {lord.Name} (will check faction in dialog)");
                return true;
            }

            if (enlistment?.IsInDesertionGracePeriod == true)
            {
                // During grace period, player can only rejoin the same kingdom they served
                // Don't show enlistment dialog for different kingdoms
                var lordKingdom = lord.MapFaction as Kingdom;
                if (lordKingdom != enlistment.PendingDesertionKingdom)
                {
                    ModLogger.Debug("DIALOGMANAGER",
                        $"Dialog hidden - player in grace period, can only rejoin {enlistment.PendingDesertionKingdom?.Name}");
                    return false;
                }
                // Allow dialog if same kingdom - they can rejoin during grace period
            }

            return true;
        }

        /// <summary>
        ///     Checks if the conversation partner is a minor faction lord.
        ///     Minor faction lords (mercenary companies, bandits, etc.) use different dialog text
        ///     even when they're serving as mercenaries for a kingdom. Uses Clan.IsMinorFaction
        ///     which remains true regardless of kingdom affiliation.
        /// </summary>
        private bool IsMinorFactionLord()
        {
            var lord = Hero.OneToOneConversationHero;
            return lord?.Clan?.IsMinorFaction == true;
        }

        /// <summary>
        ///     Checks if the player has an active bag check abort cooldown with the current lord.
        ///     Used to show lord's rejection dialogue when player tries to enlist too soon after aborting.
        /// </summary>
        private bool HasBagCheckAbortCooldown()
        {
            var lord = Hero.OneToOneConversationHero;
            if (lord == null)
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return false;
            }

            return enlistment.IsBlockedFromLordDueToAbort(lord, out _);
        }

        /// <summary>
        ///     Checks if the player can request enlistment with a minor faction lord.
        ///     Same logic as standard enlistment but only for minor faction lords.
        /// </summary>
        private bool CanRequestMinorFactionEnlistment()
        {
            if (!IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;

            if (enlistment?.IsEnlisted == true || enlistment?.IsOnLeave == true)
            {
                return false;
            }

            if (enlistment?.IsInDesertionGracePeriod == true)
            {
                return false;
            }

            var lord = Hero.OneToOneConversationHero;
            if (lord == null || !lord.IsLord)
            {
                return false;
            }

            return enlistment != null && enlistment.CanEnlistWithParty(lord, out _);
        }

        /// <summary>
        ///     Checks if the player can request standard kingdom enlistment (not minor faction).
        ///     Minor faction lords use CanRequestMinorFactionEnlistment instead.
        /// </summary>
        private bool CanRequestStandardEnlistment()
        {
            // Minor faction lords use separate dialog
            if (IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;

            if (enlistment?.IsEnlisted == true || enlistment?.IsOnLeave == true)
            {
                return false;
            }

            if (enlistment?.IsInDesertionGracePeriod == true)
            {
                return false; // hide standard line when in grace period
            }

            var lord = Hero.OneToOneConversationHero;
            if (lord == null || !lord.IsLord)
            {
                return false;
            }

            // Allow showing the enlistment option even if there's an abort cooldown,
            // so the conversation can flow to the lord's rejection dialogue
            if (enlistment != null && enlistment.IsBlockedFromLordDueToAbort(lord, out _))
            {
                return true; // Show option so lord can reject with roleplay text
            }

            return enlistment?.CanEnlistWithParty(lord, out _) == true;
        }

        private bool CanRequestGraceTransfer()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsInDesertionGracePeriod != true || enlistment.IsEnlisted || enlistment.IsOnLeave)
            {
                return false;
            }

            var lord = Hero.OneToOneConversationHero;
            if (lord == null || !lord.IsLord)
            {
                return false;
            }

            var lordKingdom = lord.MapFaction as Kingdom;
            if (lordKingdom == null || lordKingdom != enlistment.PendingDesertionKingdom)
            {
                return false;
            }

            return enlistment.CanEnlistWithParty(lord, out _);
        }

        /// <summary>
        ///     Checks if the player is currently leading their own army.
        ///     Used to show army leader rejection dialog instead of standard enlistment.
        /// </summary>
        private bool IsPlayerLeadingArmy()
        {
            var enlistment = EnlistmentBehavior.Instance;

            // Don't show if already enlisted or on leave
            if (enlistment?.IsEnlisted == true || enlistment?.IsOnLeave == true)
            {
                return false;
            }

            // Don't show during grace period (different dialog flow)
            if (enlistment?.IsInDesertionGracePeriod == true)
            {
                return false;
            }

            var lord = Hero.OneToOneConversationHero;
            if (lord == null || !lord.IsLord)
            {
                return false;
            }

            // Check if player is leading their own army
            var main = MobileParty.MainParty;
            return main?.Army != null && main.Army.LeaderParty == main;
        }

#if false
        /// <summary>
        ///     Checks if the player can discuss first-term retirement (after 252 days).
        ///     Must be enlisted with current lord, have completed minimum service,
        ///     and not be in a grace period or already in a renewal term.
        ///     Only for kingdom lords - minor factions use CanDiscussMinorFactionRetirement.
        /// </summary>
        private bool CanDiscussFirstTermRetirement()
        {
            // Minor faction lords use separate dialog
            if (IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            // Must be enlisted with this specific lord and eligible for retirement
            // Also explicitly exclude grace period for clarity (IsEnlisted is false during grace anyway)
            return enlistment?.IsEnlisted == true &&
                   enlistment.CurrentLord == lord &&
                   enlistment.IsEligibleForRetirement &&
                   !enlistment.IsInDesertionGracePeriod;
        }
#endif

        private bool CanDiscussDischargeGuidance()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            return enlistment?.IsEnlisted == true &&
                   enlistment.CurrentLord == lord &&
                   !enlistment.IsInDesertionGracePeriod;
        }

#if false
        /// <summary>
        ///     Checks if the player can discuss retirement with a minor faction lord.
        ///     Same eligibility as kingdom lords but uses mercenary-themed dialog.
        /// </summary>
        private bool CanDiscussMinorFactionRetirement()
        {
            if (!IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            return enlistment?.IsEnlisted == true &&
                   enlistment.CurrentLord == lord &&
                   enlistment.IsEligibleForRetirement &&
                   !enlistment.IsInDesertionGracePeriod;
        }
#endif

#if false
        /// <summary>
        ///     Checks if the player can discuss renewal term completion.
        ///     Only for kingdom lords - minor factions use CanDiscussMinorFactionRenewalEnd.
        /// </summary>
        private bool CanDiscussRenewalTermEnd()
        {
            // Minor faction lords use separate dialog
            if (IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            return enlistment?.IsEnlisted == true &&
                   enlistment.CurrentLord == lord &&
                   enlistment.IsInRenewalTerm &&
                   enlistment.IsRenewalTermComplete;
        }
#endif

#if false
        /// <summary>
        ///     Checks if the player can discuss renewal term completion with a minor faction lord.
        /// </summary>
        private bool CanDiscussMinorFactionRenewalEnd()
        {
            if (!IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            return enlistment?.IsEnlisted == true &&
                   enlistment.CurrentLord == lord &&
                   enlistment.IsInRenewalTerm &&
                   enlistment.IsRenewalTermComplete;
        }
#endif

#if false
        /// <summary>
        ///     Checks if the player can re-enlist after cooldown (veteran return).
        /// </summary>
        private bool CanReEnlistAfterCooldown()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            if (enlistment?.IsEnlisted == true || enlistment?.IsOnLeave == true)
            {
                return false;
            }

            var kingdom = lord?.MapFaction as Kingdom;
            return kingdom != null && enlistment?.CanReEnlistAfterCooldown(kingdom) == true;
        }
#endif

#if false
        /// <summary>
        ///     Checks if the player can request early discharge (before full term).
        ///     Shows only when not eligible for full retirement benefits.
        ///     Only for kingdom lords - minor factions use CanRequestMinorFactionEarlyDischarge.
        /// </summary>
        private bool CanRequestEarlyDischarge()
        {
            // Minor faction lords use separate dialog
            if (IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            // Must be enlisted with this lord but NOT eligible for full retirement
            return enlistment?.IsEnlisted == true &&
                   enlistment.CurrentLord == lord &&
                   !enlistment.IsEligibleForRetirement &&
                   !enlistment.IsRenewalTermComplete;
        }
#endif

#if false
        /// <summary>
        ///     Checks if the player can request early discharge from a minor faction.
        /// </summary>
        private bool CanRequestMinorFactionEarlyDischarge()
        {
            if (!IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            return enlistment?.IsEnlisted == true &&
                   enlistment.CurrentLord == lord &&
                   !enlistment.IsEligibleForRetirement &&
                   !enlistment.IsRenewalTermComplete;
        }
#endif

        /// <summary>
        ///     Checks if the player can return from temporary leave.
        ///     Only for kingdom lords - minor factions use CanReturnFromMinorFactionLeave.
        /// </summary>
        private bool CanReturnFromLeave()
        {
            // Minor faction lords use separate dialog
            if (IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            // Must be on leave and talking to the same lord you served
            return enlistment?.IsOnLeave == true &&
                   enlistment.CurrentLord == lord;
        }

        /// <summary>
        ///     Checks if the player can return from leave with a minor faction lord.
        /// </summary>
        private bool CanReturnFromMinorFactionLeave()
        {
            if (!IsMinorFactionLord())
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            return enlistment?.IsOnLeave == true &&
                   enlistment.CurrentLord == lord;
        }

        /// <summary>
        ///     Checks if the player can request to transfer service to a different lord.
        ///     Available ONLY when on leave, talking to a different lord in the same faction.
        ///     Note: Grace period transfers are handled separately by CanRequestGraceTransfer().
        /// </summary>
        private bool CanRequestServiceTransfer()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            if (lord == null || !lord.IsLord || !lord.IsAlive)
            {
                return false;
            }

            // Only show this dialog when on leave (NOT in grace period)
            // Grace period has its own dedicated dialog via CanRequestGraceTransfer()
            if (enlistment?.IsOnLeave != true)
            {
                return false;
            }

            // Must be talking to a DIFFERENT lord (not current lord - that's "return from leave")
            if (enlistment.CurrentLord == lord)
            {
                return false;
            }

            // Must be same faction/kingdom
            var currentLordKingdom = enlistment.CurrentLord?.MapFaction as Kingdom;
            var targetLordKingdom = lord.MapFaction as Kingdom;

            if (currentLordKingdom == null || targetLordKingdom != currentLordKingdom)
            {
                return false;
            }

            // Check if the lord can accept service
            return enlistment.CanEnlistWithParty(lord, out _);
        }

        /// <summary>
        ///     Check if player can request T7 (Commander) promotion.
        ///     Player must be T6, enlisted with the lord they're talking to, and meet all T7 requirements.
        /// </summary>
        private bool CanRequestCommanderPromotion()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lord = Hero.OneToOneConversationHero;

            if (lord == null || !lord.IsLord || !lord.IsAlive)
            {
                return false;
            }

            // Must be enlisted with this specific lord
            if (enlistment?.IsEnlisted != true || enlistment.CurrentLord != lord)
            {
                return false;
            }

            // Must be exactly Tier 6 (not already T7+)
            if (enlistment.EnlistmentTier != 6)
            {
                return false;
            }

            // Must meet all T7 promotion requirements
            var promotionBehavior = Ranks.Behaviors.PromotionBehavior.Instance;
            if (promotionBehavior == null)
            {
                return false;
            }

            var (canPromote, _) = promotionBehavior.CanPromote();
            return canPromote;
        }

        #endregion

        #region Shared Dialog Consequences

        /// <summary>
        ///     Handles the consequence of accepting enlistment.
        ///     Centralized to ensure consistent behavior across all enlistment paths.
        ///     CRITICAL: The entire enlistment process is deferred to the next frame because
        ///     StartEnlist() calls PlayerEncounter.LeaveSettlement/Finish which crashes if
        ///     the conversation mission is still active (MissionLocationLogic.OnRemoveBehavior fails).
        /// </summary>
        private void OnAcceptEnlistment()
        {
            try
            {
                var lord = Hero.OneToOneConversationHero;
                if (lord == null)
                {
                    ModLogger.LogOnce("dialog_accept_enlistment_no_conversation_hero", "DIALOGMANAGER",
                        "[E-DIALOG-001] No conversation hero found during enlistment acceptance", LogLevel.Error);
                    return;
                }

                if (EnlistmentBehavior.Instance == null)
                {
                    ModLogger.LogOnce("dialog_accept_enlistment_enlistment_instance_null", "DIALOGMANAGER",
                        "[E-DIALOG-002] EnlistmentBehavior.Instance is null during enlistment", LogLevel.Error);
                    return;
                }

                ModLogger.Info("DIALOGMANAGER", $"Player accepting enlistment with lord: {lord.Name}");

                // Capture lord reference before conversation ends (Hero.OneToOneConversationHero will be null next frame)
                var capturedLord = lord;

                // CRITICAL: Defer the entire enlistment to next frame to let the conversation mission
                // finalize first. Calling StartEnlist() during the dialog consequence crashes because
                // it tries to end the PlayerEncounter while MissionLocationLogic is still active.
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    try
                    {
                        if (EnlistmentBehavior.Instance == null)
                        {
                            ModLogger.LogOnce("dialog_deferred_enlistment_instance_null", "DIALOGMANAGER",
                                "[E-DIALOG-004] EnlistmentBehavior.Instance became null before deferred enlistment", LogLevel.Error);
                            return;
                        }

                        EnlistmentBehavior.Instance.StartEnlist(capturedLord);

                        // Activate the enlisted status menu after enlistment completes
                        NextFrameDispatcher.RunNextFrame(EnlistedMenuBehavior.SafeActivateEnlistedMenu);
                        ModLogger.Debug("DIALOGMANAGER",
                            "Scheduled enlisted_status menu activation - preventing encounter gap");

                        // Professional notification
                        var message =
                            GetLocalizedText("{=enlisted_success_notification}You have enlisted in {LORD_NAME}'s service.");
                        message.SetTextVariable("LORD_NAME", capturedLord.Name);
                        InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error("DIALOGMANAGER", "Error in deferred enlistment", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during enlistment acceptance", ex);
            }
        }

#if false
        /// <summary>
        ///     Handles first-term retirement with full benefits.
        ///     Schedules menu cleanup after dialog closes to prevent empty menu state.
        /// </summary>
        private void OnAcceptFirstTermRetirement()
        {
            try
            {
                EnlistmentBehavior.Instance?.ProcessFirstTermRetirement();
                ModLogger.Info("DIALOGMANAGER", "First-term retirement processed");

                // Defer menu cleanup until after dialog closes - prevents stuck menu with no options
                NextFrameDispatcher.RunNextFrame(CleanupEnlistedMenuAfterDischarge);
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during first-term retirement", ex);
            }
        }

        /// <summary>
        ///     Called when player chooses to keep their troops during retirement.
        ///     Sets the retention flag - actual processing happens in OnFinalizeRetirementWithTroops.
        /// </summary>
        private void OnKeepTroopsRetirement()
        {
            EnlistmentBehavior.RetainTroopsOnRetirement = true;
            ModLogger.Info("DIALOGMANAGER", "Player chose to keep retinue troops on retirement");
        }

        /// <summary>
        ///     Finalizes retirement when player kept troops. Processes retirement,
        ///     adds starter supplies (grain), and cleans up.
        /// </summary>
        private void OnFinalizeRetirementWithTroops()
        {
            try
            {
                // Process the retirement with troop retention flag set
                EnlistmentBehavior.Instance?.ProcessFirstTermRetirement();

                // Add starter supplies so they don't starve immediately
                AddRetirementSupplies();

                ModLogger.Info("DIALOGMANAGER", "First-term retirement with troops processed");

                // Defer menu cleanup until after dialog closes
                NextFrameDispatcher.RunNextFrame(CleanupEnlistedMenuAfterDischarge);
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during retirement with troops", ex);
            }
        }
#endif

#if false
        /// <summary>
        ///     Adds 10 grain to player inventory as retirement supplies.
        ///     Prevents immediate starvation when leaving the army's supply train.
        /// </summary>
        private static void AddRetirementSupplies()
        {
            try
            {
                var party = MobileParty.MainParty;
                if (party == null)
                {
                    return;
                }

                var grain = MBObjectManager.Instance.GetObject<ItemObject>("grain");
                if (grain != null)
                {
                    party.ItemRoster.AddToCounts(grain, 10);
                    ModLogger.Info("DIALOGMANAGER", "Added 10 grain as retirement supplies");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DIALOGMANAGER", "Failed to add retirement supplies", ex);
            }
        }
#endif

#if false
        /// <summary>
        ///     Checks if player has a retinue that can be kept on retirement.
        ///     Used to determine which dialog branch to show.
        /// </summary>
        private static bool HasRetinueForRetirement()
        {
            var manager = ServiceRecordManager.Instance?.RetinueManager;
            return manager?.State?.HasRetinue == true;
        }

        /// <summary>
        ///     Gets the lord's question text about the troops, with unit name variable.
        /// </summary>
        private static string GetRetinueAskText()
        {
            var unitName = GetRetinueUnitName();
            var text = new TextObject(
                "{=enlisted_retinue_ask}Your {UNIT_NAME} has served you well these many months. What of your men? They may return to the ranks, or... if they wish to follow you into the unknown, I shall not stop them.");
            text.SetTextVariable("UNIT_NAME", unitName);
            return text.ToString();
        }

        /// <summary>
        ///     Gets the farewell text when player keeps their troops.
        /// </summary>
        private static string GetRetinueFarewellText(bool isMinorFaction)
        {
            var manager = ServiceRecordManager.Instance?.RetinueManager;
            var count = manager?.State?.TotalSoldiers ?? 0;

            TextObject text = new TextObject(
                isMinorFaction
                    ? "{=enlisted_minor_farewell_troops}Ha! Loyal to the end, that lot. Your {COUNT} have decided to stay with you instead of signing back on. Here's some grain for the road - you'll need it. Don't be a stranger."
                    : "{=enlisted_farewell_troops}So be it. Your {COUNT} soldiers have decided to stay with you rather than re-enlist. I've had the quartermaster set aside provisions for your journey. Fare well, soldier.");
            text.SetTextVariable("COUNT", count);
            return text.ToString();
        }
#endif

#if false
        /// <summary>
        ///     Gets the unit name based on player's tier (Lance, Squad, or Retinue).
        /// </summary>
        private static string GetRetinueUnitName()
        {
            var tier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 4;
            return tier switch
            {
                >= 6 => "retinue",
                5 => "squad",
                _ => "lance"
            };
        }
#endif

#if false
        /// <summary>
        ///     Handles first-term re-enlistment with 20,000 gold bonus.
        /// </summary>
        private void OnAcceptFirstTermReenlistBonus()
        {
            try
            {
                var config = AssignmentsConfig.LoadRetirementConfig();
                EnlistmentBehavior.Instance?.StartRenewalTerm(config.FirstTermReenlistBonus);
                ModLogger.Info("DIALOGMANAGER",
                    $"First-term re-enlistment with {config.FirstTermReenlistBonus}g bonus");
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during first-term re-enlistment", ex);
            }
        }

        /// <summary>
        ///     Handles renewal term discharge with 5,000 gold.
        ///     Schedules menu cleanup after dialog closes to prevent empty menu state.
        /// </summary>
        private void OnAcceptRenewalDischarge()
        {
            try
            {
                EnlistmentBehavior.Instance?.ProcessRenewalRetirement();
                ModLogger.Info("DIALOGMANAGER", "Renewal discharge processed");

                // Defer menu cleanup until after dialog closes - prevents stuck menu with no options
                NextFrameDispatcher.RunNextFrame(CleanupEnlistedMenuAfterDischarge);
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during renewal discharge", ex);
            }
        }

        /// <summary>
        ///     Finalizes renewal discharge when player kept troops. Processes discharge,
        ///     adds starter supplies (grain), and cleans up.
        /// </summary>
        private void OnFinalizeRenewalWithTroops()
        {
            try
            {
                // Process the renewal discharge with troop retention flag set
                EnlistmentBehavior.Instance?.ProcessRenewalRetirement();

                // Add starter supplies so they don't starve immediately
                AddRetirementSupplies();

                ModLogger.Info("DIALOGMANAGER", "Renewal discharge with troops processed");

                // Defer menu cleanup until after dialog closes
                NextFrameDispatcher.RunNextFrame(CleanupEnlistedMenuAfterDischarge);
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during renewal discharge with troops", ex);
            }
        }
#endif

#if false
        /// <summary>
        ///     Handles continuing service with 5,000 gold bonus.
        /// </summary>
        private void OnContinueService()
        {
            try
            {
                var config = AssignmentsConfig.LoadRetirementConfig();
                EnlistmentBehavior.Instance?.StartRenewalTerm(config.RenewalContinueBonus);
                ModLogger.Info("DIALOGMANAGER", $"Service continued with {config.RenewalContinueBonus}g bonus");
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error continuing service", ex);
            }
        }

        /// <summary>
        ///     Handles veteran re-enlistment after cooldown.
        /// </summary>
        private void OnVeteranReEnlist()
        {
            try
            {
                var lord = Hero.OneToOneConversationHero;
                if (lord != null)
                {
                    EnlistmentBehavior.Instance?.ReEnlistAfterCooldown(lord);

                    // Redirect to troop selection
                    NextFrameDispatcher.RunNextFrame(EnlistedMenuBehavior.SafeActivateEnlistedMenu);
                }

                ModLogger.Info("DIALOGMANAGER", "Veteran re-enlistment processed");
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during veteran re-enlistment", ex);
            }
        }

        /// <summary>
        ///     Handles early discharge (before full term, no benefits).
        ///     Schedules menu cleanup after dialog closes to prevent empty menu state.
        /// </summary>
        private void OnGrantEarlyDischarge()
        {
            try
            {
                if (EnlistmentBehavior.Instance?.IsEnlisted == true)
                {
                    var lordName = EnlistmentBehavior.Instance.CurrentLord?.Name?.ToString() ?? "Unknown Lord";
                    ModLogger.Info("DIALOGMANAGER", $"Player early discharge from service with: {lordName}");

                    // Early discharge is not honorable (default false)
                    EnlistmentBehavior.Instance.StopEnlist("Early discharge through dialog");

                    var message =
                        GetLocalizedText(
                            "{=enlisted_early_discharge_notification}You have been discharged from service without full benefits.");
                    InformationManager.DisplayMessage(new InformationMessage(message.ToString()));

                    // Defer menu cleanup until after dialog closes - prevents stuck menu with no options
                    NextFrameDispatcher.RunNextFrame(CleanupEnlistedMenuAfterDischarge);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during early discharge", ex);
            }
        }
#endif

        /// <summary>
        ///     Handles the consequence of returning from temporary leave.
        /// </summary>
        private void OnReturnFromLeave()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsOnLeave == true)
                {
                    var lordName = enlistment.CurrentLord?.Name?.ToString() ?? "Unknown Lord";
                    ModLogger.Info("DIALOGMANAGER", $"Player returning from leave to service with: {lordName}");

                    enlistment.ReturnFromLeave();

                    // Professional notification
                    var returnMessage =
                        GetLocalizedText(
                            "{=enlisted_return_notification}You have returned to active military service.");
                    InformationManager.DisplayMessage(new InformationMessage(returnMessage.ToString()));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during return from leave", ex);
            }
        }

        /// <summary>
        ///     Handles the consequence of transferring service to a new lord.
        ///     Called when player accepts transfer while on leave or in grace period.
        /// </summary>
        private void OnAcceptServiceTransfer()
        {
            try
            {
                var newLord = Hero.OneToOneConversationHero;
                var enlistment = EnlistmentBehavior.Instance;

                if (newLord == null || enlistment == null)
                {
                    ModLogger.LogOnce("dialog_transfer_service_missing_inputs", "DIALOGMANAGER",
                        "[E-DIALOG-003] Cannot transfer service - missing lord or enlistment instance", LogLevel.Error);
                    return;
                }

                var previousLordName = enlistment.CurrentLord?.Name?.ToString() ?? "your previous commander";
                var newLordName = newLord.Name?.ToString() ?? "Unknown Lord";

                ModLogger.Info("DIALOGMANAGER",
                    $"Player transferring service from {previousLordName} to {newLordName}");

                // Perform the transfer
                enlistment.TransferServiceToLord(newLord);

                // Professional notification
                var message =
                    GetLocalizedText(
                        "{=enlisted_transfer_notification}You have transferred your service to {LORD_NAME}. Your rank and experience have been preserved.");
                message.SetTextVariable("LORD_NAME", newLord.Name);
                InformationManager.DisplayMessage(new InformationMessage(message.ToString()));
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error during service transfer", ex);
            }
        }

        /// <summary>
        ///     Handles the consequence of accepting T7 Commander promotion via dialog.
        ///     Triggers the T6 to T7 promotion event which grants retinue and promotes player.
        /// </summary>
        private void OnAcceptCommanderPromotion()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;

                if (enlistment == null)
                {
                    ModLogger.Error("DIALOGMANAGER", "EnlistmentBehavior.Instance is null during commander promotion acceptance");
                    return;
                }

                if (enlistment.EnlistmentTier != 6)
                {
                    ModLogger.Warn("DIALOGMANAGER", $"Player not T6 when accepting commander promotion (tier={enlistment.EnlistmentTier})");
                    return;
                }

                ModLogger.Info("DIALOGMANAGER", "Player accepting T7 Commander promotion via dialog");

                // Clear declined promotion flag (player previously said "not ready" but now requesting promotion)
                EscalationManager.Instance?.ClearDeclinedPromotion(7);

                // Defer to next frame to let conversation close first
                NextFrameDispatcher.RunNextFrame(() =>
                {
                    try
                    {
                        // High-ranking commissions are processed directly through the promotion system.
                        ModLogger.Info("DIALOGMANAGER", "Processing commander promotion");

                        var enlistmentCheck = EnlistmentBehavior.Instance;
                        if (enlistmentCheck != null && enlistmentCheck.EnlistmentTier == 6)
                        {
                            enlistmentCheck.SetTier(7);
                            QuartermasterManager.Instance?.UpdateNewlyUnlockedItems();

                            var message = new TextObject("{=promotion_t7_notification}You have been promoted to Commander. Twenty recruits await your command.");
                            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Green));
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error("DIALOGMANAGER", "Error during deferred commander promotion", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                ModLogger.Error("DIALOGMANAGER", "Error accepting commander promotion", ex);
            }
        }

        #endregion
    }
}
