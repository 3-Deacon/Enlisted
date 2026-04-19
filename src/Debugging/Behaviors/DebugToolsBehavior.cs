using System;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.UI;
using Enlisted.Features.Escalation;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Debugging.Behaviors
{
    /// <summary>
    ///     Lightweight in-game debug helpers for QA: grant gold and enlistment XP.
    ///     Keeps state changes minimal and logged for traceability.
    /// </summary>
    public static class DebugToolsBehavior
    {
        private const int GoldPerClick = 1000;
        private const int XpPerClick = 2000;

        public static void GiveGold()
        {
            var hero = Hero.MainHero;
            if (hero == null)
            {
                return;
            }

            hero.Gold += GoldPerClick;
            var msg = new TextObject("{=dbg_gold_added}+{G} gold granted (debug).");
            msg.SetTextVariable("G", GoldPerClick);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            SessionDiagnostics.LogEvent("Debug", "GiveGold", $"gold={GoldPerClick}, total={hero.Gold}");
        }

        public static void GiveEnlistmentXp()
        {
            var enlist = EnlistmentBehavior.Instance;
            if (enlist?.IsEnlisted != true)
            {
                var warn = new TextObject("{=dbg_xp_not_enlisted}Cannot grant XP while not enlisted.");
                InformationManager.DisplayMessage(new InformationMessage(warn.ToString()));
                return;
            }

            enlist.AddEnlistmentXP(XpPerClick, "Debug");
            var msg = new TextObject("{=dbg_xp_added}+{XP} enlistment XP granted (debug).");
            msg.SetTextVariable("XP", XpPerClick);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            SessionDiagnostics.LogEvent("Debug", "GiveXP",
                $"xp={XpPerClick}, total={enlist.EnlistmentXP}, tier={enlist.EnlistmentTier}");
        }


        /// <summary>
        /// [DEPRECATED] Random event forcing removed per Order Prompt Model.
        /// Events now fire via player-driven Order Prompt system only.
        /// Use ListEligibleEvents() to check what events could fire from prompts.
        /// </summary>
        public static void ForceEventSelection()
        {
            var msg = new TextObject("Random events removed. Events now fire via Order Prompt system.");
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            ModLogger.Info("Debug", "ForceEventSelection: Random event system removed per Order Prompt Model");
            SessionDiagnostics.LogEvent("Debug", "ForceEventSelection", "Deprecated - use Order Prompts");
        }

        /// <summary>
        /// Lists all currently eligible events (meet requirements, not on cooldown).
        /// </summary>
        public static void ListEligibleEvents()
        {
            var eligibleCount = EventSelector.GetEligibleEventCount();
            var totalCount = EventCatalog.EventCount;

            var msg = new TextObject("{C} eligible events out of {T} total. Check log for details.");
            msg.SetTextVariable("C", eligibleCount);
            msg.SetTextVariable("T", totalCount);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));

            ModLogger.Info("Debug", $"Eligible events: {eligibleCount}/{totalCount}");
            SessionDiagnostics.LogEvent("Debug", "ListEligibleEvents", $"eligible={eligibleCount}, total={totalCount}");
        }

        /// <summary>
        /// Clears all event cooldowns, allowing any event to fire again.
        /// Useful for testing event variety without waiting for cooldowns.
        /// </summary>
        public static void ClearEventCooldowns()
        {
            var escalationState = EscalationManager.Instance?.State;
            if (escalationState == null)
            {
                var warn = new TextObject("Cannot clear cooldowns - EscalationManager not found.");
                InformationManager.DisplayMessage(new InformationMessage(warn.ToString()));
                return;
            }

            var clearedCount = escalationState.EventLastFired?.Count ?? 0;
            escalationState.EventLastFired?.Clear();

            var msg = new TextObject("Cleared {C} event cooldowns. All events can fire again.");
            msg.SetTextVariable("C", clearedCount);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            SessionDiagnostics.LogEvent("Debug", "ClearEventCooldowns", $"cleared={clearedCount}");
        }

        /// <summary>
        /// Fires a specific event by ID, bypassing selection and requirements.
        /// Useful for testing specific event content.
        /// </summary>
        public static void FireSpecificEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                var warn = new TextObject("No event ID provided. Usage: enlisted.fire_event <event_id>");
                InformationManager.DisplayMessage(new InformationMessage(warn.ToString()));
                return;
            }

            var evt = EventCatalog.GetEvent(eventId);
            if (evt == null)
            {
                var warn = new TextObject("Event '{ID}' not found in catalog.");
                warn.SetTextVariable("ID", eventId);
                InformationManager.DisplayMessage(new InformationMessage(warn.ToString()));
                return;
            }

            var deliveryManager = EventDeliveryManager.Instance;
            if (deliveryManager == null)
            {
                var warn = new TextObject("Cannot fire event - EventDeliveryManager not found.");
                InformationManager.DisplayMessage(new InformationMessage(warn.ToString()));
                return;
            }

            // Intentional bypass of StoryDirector — this debug path must fire arbitrary events
            // immediately for testing, regardless of pacing guards.
            deliveryManager.QueueEvent(evt);
            var msg = new TextObject("Queued event '{ID}' for delivery.");
            msg.SetTextVariable("ID", eventId);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            SessionDiagnostics.LogEvent("Debug", "FireSpecificEvent", $"eventId={eventId}");
        }

        /// <summary>
        /// Triggers an immediate pay muster bypassing the 12-day cycle.
        /// Useful for testing the muster system flow, pay options, inspections, and promotions
        /// </summary>
        public static void TriggerMuster()
        {
            var enlist = EnlistmentBehavior.Instance;
            if (enlist?.IsEnlisted != true)
            {
                var warn = new TextObject("{=dbg_muster_not_enlisted}Cannot trigger muster while not enlisted.");
                InformationManager.DisplayMessage(new InformationMessage(warn.ToString()));
                ModLogger.Warn("Debug", "TriggerMuster: Player not enlisted");
                return;
            }

            // Find the MusterMenuHandler (registered as a campaign behavior)
            var musterHandler = Campaign.Current?.CampaignBehaviorManager?.GetBehavior<MusterMenuHandler>();
            if (musterHandler == null)
            {
                var error = new TextObject("{=dbg_muster_handler_missing}Cannot trigger muster - MusterMenuHandler not found.");
                InformationManager.DisplayMessage(new InformationMessage(error.ToString()));
                ModLogger.Error("Debug", "TriggerMuster: MusterMenuHandler not registered as campaign behavior");
                return;
            }

            // Call the muster handler directly to open the 6-stage muster sequence
            musterHandler.BeginMusterSequence();

            var msg = new TextObject("{=dbg_muster_started}Muster sequence started (debug). Opening intro stage...");
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            SessionDiagnostics.LogEvent("Debug", "TriggerMuster",
                $"tier={enlist.EnlistmentTier}, xp={enlist.EnlistmentXP}, pay_owed={enlist.PendingMusterPay}");
        }

        /// <summary>
        /// Opens the provisions shop UI for testing, bypassing tier requirements.
        /// Allows testing of the Gauntlet provisions UI at any tier.
        /// </summary>
        public static void TestProvisionsShop()
        {
            try
            {
                ModLogger.Info("Debug", "Opening provisions shop for testing (bypassing tier requirements)");
                QuartermasterProvisionsBehavior.ShowProvisionsScreen();

                var msg = new TextObject("Provisions shop opened (debug). Testing T7+ officer UI.");
                InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
                SessionDiagnostics.LogEvent("Debug", "TestProvisionsShop", "Provisions UI opened for testing");
            }
            catch (Exception ex)
            {
                ModLogger.Error("Debug", "Failed to open provisions shop for testing", ex);
                var error = new TextObject("Failed to open provisions shop. Check logs for details.");
                InformationManager.DisplayMessage(new InformationMessage(error.ToString(), Colors.Red));
            }
        }
    }
}

