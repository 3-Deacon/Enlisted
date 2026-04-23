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
            _ = msg.SetTextVariable("G", GoldPerClick);
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
            _ = msg.SetTextVariable("XP", XpPerClick);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            SessionDiagnostics.LogEvent("Debug", "GiveXP",
                $"xp={XpPerClick}, total={enlist.EnlistmentXP}, tier={enlist.EnlistmentTier}");
        }


        /// <summary>
        /// Random event forcing is removed. Events now route through StoryDirector
        /// and surface-specific emitters. Use ListEligibleEvents() for diagnostics.
        /// </summary>
        public static void ForceEventSelection()
        {
            var msg = new TextObject("Random events removed. Events now fire through StoryDirector.");
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            ModLogger.Info("Debug", "ForceEventSelection: Random event system removed");
            SessionDiagnostics.LogEvent("Debug", "ForceEventSelection", "Use StoryDirector diagnostics");
        }

        /// <summary>
        /// Lists all currently eligible events (meet requirements, not on cooldown).
        /// </summary>
        public static void ListEligibleEvents()
        {
            var eligibleCount = EventSelector.GetEligibleEventCount();
            var totalCount = EventCatalog.EventCount;

            var msg = new TextObject("{C} eligible events out of {T} total. Check log for details.");
            _ = msg.SetTextVariable("C", eligibleCount);
            _ = msg.SetTextVariable("T", totalCount);
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
            _ = msg.SetTextVariable("C", clearedCount);
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
                _ = warn.SetTextVariable("ID", eventId);
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
            _ = msg.SetTextVariable("ID", eventId);
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
                ModLogger.Caught("Debug", "TriggerMuster: MusterMenuHandler not registered as campaign behavior", null);
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
                ModLogger.Caught("Debug", "Failed to open provisions shop for testing", ex);
                var error = new TextObject("Failed to open provisions shop. Check logs for details.");
                InformationManager.DisplayMessage(new InformationMessage(error.ToString(), Colors.Red));
            }
        }

        /// <summary>
        /// Queues a known-safe test event to verify queue persistence across save/load.
        /// Usage: trigger this command, save, reload the save, then run DebugPrintQueue.
        /// </summary>
        public static void DebugQueueTestEvent()
        {
            // Use an event known to exist in the shipping catalog.
            // evt_quiet_letter_from_home is from events_quiet_stretch.json, no preconditions.
            const string testId = "evt_quiet_letter_from_home";
            var evt = EventCatalog.GetEvent(testId);
            if (evt == null)
            {
                ModLogger.Info("Debug", $"Test event id '{testId}' not in catalog; confirm events_quiet_stretch.json is loaded and retry");
                var warn = new TextObject("Test event not found in catalog. Check log for details.");
                InformationManager.DisplayMessage(new InformationMessage(warn.ToString()));
                return;
            }

            EventDeliveryManager.Instance?.QueueEvent(evt);
            var msg = new TextObject("Queued test event '{ID}'. Save now, reload, then run DebugPrintQueue.");
            _ = msg.SetTextVariable("ID", testId);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            ModLogger.Info("Debug", $"Queued test event {testId}. Save now, reload, then run DebugPrintQueue.");
            SessionDiagnostics.LogEvent("Debug", "DebugQueueTestEvent", $"eventId={testId}");
        }

        /// <summary>
        /// Dumps the current EventDeliveryManager pending queue to the session log
        /// to verify persistence worked after reload.
        /// </summary>
        public static void DebugPrintQueue()
        {
            var mgr = EventDeliveryManager.Instance;
            if (mgr == null)
            {
                ModLogger.Info("Debug", "EventDeliveryManager.Instance is null");
                var warn = new TextObject("EventDeliveryManager not found. Check log.");
                InformationManager.DisplayMessage(new InformationMessage(warn.ToString()));
                return;
            }

            var count = mgr.PendingQueueCountForDebug;
            var ids = mgr.PendingQueueIdsForDebug;
            var idList = string.Join(", ", ids);
            ModLogger.Info("Debug", $"PendingQueue count={count}, ids=[{idList}]");
            var msg = new TextObject("Queue: {C} event(s). Check log for IDs.");
            _ = msg.SetTextVariable("C", count);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
            SessionDiagnostics.LogEvent("Debug", "DebugPrintQueue", $"count={count}, ids=[{idList}]");
        }

        /// <summary>
        /// Smoke-tests QualityStore: reads scrutiny, adds +5, reads again, reads rank_xp and
        /// lord_relation (read-through), then reverts the +5. Logs the round-trip result.
        /// </summary>
        public static void SmokeTestQualities()
        {
            var store = Features.Qualities.QualityStore.Instance;
            if (store == null)
            {
                SessionDiagnostics.LogEvent("Debug", "SmokeTestQualities", "FAIL: QualityStore.Instance null");
                return;
            }

            var before = store.Get("scrutiny");
            store.Add("scrutiny", 5, "smoke-test");
            var after = store.Get("scrutiny");
            var rankXp = store.Get("rank_xp");
            var lordRel = store.Get("lord_relation");
            SessionDiagnostics.LogEvent("Debug", "SmokeTestQualities",
                $"scrutiny {before}->{after} (+5), rank_xp={rankXp} (read-through), lord_relation={lordRel}");
            store.Add("scrutiny", -5, "smoke-test-revert");
        }

        /// <summary>
        /// Smoke-tests FlagStore: sets a flag with a 1-day expiry, checks Has, clears, logs PASS/FAIL.
        /// </summary>
        public static void SmokeTestFlags()
        {
            var store = Features.Flags.FlagStore.Instance;
            if (store == null)
            {
                SessionDiagnostics.LogEvent("Debug", "SmokeTestFlags", "FAIL: FlagStore.Instance null");
                return;
            }

            store.Set("smoke_test_flag", CampaignTime.DaysFromNow(1));
            var hasIt = store.Has("smoke_test_flag");
            store.Clear("smoke_test_flag");
            SessionDiagnostics.LogEvent("Debug", "SmokeTestFlags",
                hasIt ? "PASS: set+has+clear round-trip" : "FAIL: set immediately missing");
        }

        /// <summary>
        /// Smoke-tests ScriptedEffectRegistry: counts all registered IDs and logs the total.
        /// Expected: 23 effects from the seed catalog.
        /// </summary>
        public static void SmokeTestScriptedEffects()
        {
            var ids = ScriptedEffectRegistry.AllIds;
            var count = 0;
            foreach (var _ in ids)
            {
                count++;
            }

            SessionDiagnostics.LogEvent("Debug", "SmokeTestScriptedEffects",
                $"registry loaded {count} scripted effects (expected 23 from seed catalog)");
        }

        /// <summary>
        /// Smoke-tests TriggerRegistry: evaluates context:land (true) and context:sea (false)
        /// against a StoryletContext with CurrentContext="land". Logs PASS/FAIL.
        /// </summary>
        public static void SmokeTestTriggers()
        {
            var ctx = new StoryletContext { CurrentContext = "land" };
            var trueTriggers = new System.Collections.Generic.List<string> { "context:land" };
            var falseTriggers = new System.Collections.Generic.List<string> { "context:sea" };
            var okTrue = TriggerRegistry.Evaluate(trueTriggers, ctx);
            var okFalse = !TriggerRegistry.Evaluate(falseTriggers, ctx);
            SessionDiagnostics.LogEvent("Debug", "SmokeTestTriggers",
                okTrue && okFalse
                    ? "PASS: context:land true, context:sea false while on land"
                    : $"FAIL: trueOK={okTrue} falseOK={okFalse}");
        }

        // ---------- Spec 1 smoke helpers — Home surface ----------

        /// <summary>
        /// Force-starts a HomeActivity bypassing the natural settlement-entry trigger.
        /// No-op if one is already active. Intent defaults to "brood" for a deterministic phase pool.
        /// </summary>
        public static void ForceStartHomeActivity()
        {
            var runtime = Features.Activities.ActivityRuntime.Instance;
            if (runtime == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DEBUG] ActivityRuntime not available."));
                return;
            }
            if (runtime.FindActive<Features.Activities.Home.HomeActivity>() != null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DEBUG] HomeActivity already active."));
                return;
            }
            var ctx = Features.Activities.ActivityContext.FromCurrent();
            var activity = new Features.Activities.Home.HomeActivity { Intent = "brood" };
            runtime.Start(activity, ctx);
            InformationManager.DisplayMessage(new InformationMessage(
                "[DEBUG] HomeActivity force-started."));
            SessionDiagnostics.LogEvent("Debug", "ForceStartHomeActivity", "intent=brood");
        }

        /// <summary>
        /// Fast-forwards the active HomeActivity to the "evening" phase regardless of phase
        /// delivery type. Calls <see cref="ActivityRuntime.ForceAdvancePhase"/>, which runs
        /// the full OnPhaseExit / OnPhaseEnter / parked-chain-consume lifecycle on each step,
        /// so the resulting state is indistinguishable from natural advancement — only the
        /// time-duration gate and the PlayerChoice precondition are bypassed. Loop capped
        /// at <see cref="AdvanceMaxSteps"/> for belt-and-suspenders protection against a
        /// malformed phase list; Home has four phases, so the cap is never reached in
        /// practice.
        /// </summary>
        public static void AdvanceHomeToEvening()
        {
            var runtime = Features.Activities.ActivityRuntime.Instance;
            var home = runtime?.FindActive<Features.Activities.Home.HomeActivity>();
            if (home == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DEBUG] No HomeActivity active — press Ctrl+Shift+H first."));
                return;
            }
            var steps = 0;
            while (home.CurrentPhase != null
                   && home.CurrentPhase.Id != "evening"
                   && steps++ < AdvanceMaxSteps)
            {
                runtime.ForceAdvancePhase(home);
            }
            var phaseId = home.CurrentPhase?.Id ?? "(ended)";
            InformationManager.DisplayMessage(new InformationMessage(
                $"[DEBUG] Home phase now: {phaseId}"));
            SessionDiagnostics.LogEvent("Debug", "AdvanceHomeToEvening", $"phase={phaseId},steps={steps}");
        }

        private const int AdvanceMaxSteps = 16;

        /// <summary>
        /// Emits the "departure_imminent" beat on the active HomeActivity, which skips the
        /// activity forward to the break_camp phase without waiting for a real settlement-leave event.
        /// </summary>
        public static void AdvanceHomeToBreakCamp()
        {
            var runtime = Features.Activities.ActivityRuntime.Instance;
            var home = runtime?.FindActive<Features.Activities.Home.HomeActivity>();
            if (home == null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[DEBUG] No HomeActivity active — press Ctrl+Shift+H first."));
                return;
            }
            home.OnBeat("departure_imminent",
                Features.Activities.ActivityContext.FromCurrent());
            var phaseId = home.CurrentPhase?.Id ?? "(ended)";
            InformationManager.DisplayMessage(new InformationMessage(
                $"[DEBUG] Home advanced to Break Camp (phase={phaseId})."));
            SessionDiagnostics.LogEvent("Debug", "AdvanceHomeToBreakCamp", $"phase={phaseId}");
        }

        /// <summary>
        /// Dumps a name->bool table of Home-surface-relevant triggers evaluated against a
        /// synthetic StoryletContext(ActivityTypeId="home_activity", CurrentContext="settlement")
        /// to the session log via ModLogger.Expected. Useful for diagnosing why a storylet
        /// refuses to fire.
        /// </summary>
        public static void DumpTriggerEvalTable()
        {
            var ctx = new StoryletContext
            {
                ActivityTypeId = "home_activity",
                CurrentContext = "settlement"
            };
            var log = new System.Text.StringBuilder();
            _ = log.AppendLine("[DEBUG] HomeTriggers eval dump:");
            foreach (var name in TriggerRegistry.AllRegisteredNames())
            {
                if (!name.StartsWith("home_") && !name.StartsWith("lord_") && !name.StartsWith("party_")
                    && !name.StartsWith("settlement_") && !name.StartsWith("kingdom_")
                    && !name.StartsWith("is_") && !name.StartsWith("at_")
                    && !name.StartsWith("days_enlisted") && !name.StartsWith("rank_")
                    && !name.StartsWith("enlisted_"))
                {
                    continue;
                }
                var val = TriggerRegistry.EvaluateOne(name, ctx);
                _ = log.Append("  ").Append(name).Append(" = ").AppendLine(val.ToString());
            }
            ModLogger.Expected("HOME-DEBUG", "trigger_dump", log.ToString());
            InformationManager.DisplayMessage(new InformationMessage(
                "[DEBUG] Trigger eval table written to session log."));
        }
    }
}

