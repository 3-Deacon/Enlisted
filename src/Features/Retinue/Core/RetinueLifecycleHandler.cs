using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using EnlistedConfig = Enlisted.Mod.Core.Config.ConfigurationManager;

namespace Enlisted.Features.Retinue.Core
{
    /// <summary>
    /// Handles retinue lifecycle events: player capture, enlistment end, lord death, army defeat.
    /// When these events occur, the retinue is cleared with an appropriate message.
    /// This class hooks into campaign events and EnlistmentBehavior events to manage retinue state.
    /// </summary>
    public sealed class RetinueLifecycleHandler : CampaignBehaviorBase
    {
        private const string LogCategory = "RETINUELIFECYCLE";

        // Track if we're in a grace period to know the discharge reason
        private bool _dischargeDuringGrace;
        private string _lastDischargeReason = string.Empty;

        public static RetinueLifecycleHandler Instance { get; private set; }

        public RetinueLifecycleHandler()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            // Hook into campaign events for player capture
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);

            // Hook into EnlistmentBehavior events for discharge handling
            EnlistmentBehavior.OnDischarged += OnEnlistmentEnded;

            // Hook into lord death and army defeat events
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, OnArmyDispersed);

            // Hook into leave expiration (desertion) through daily tick
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

            ModLogger.Debug(LogCategory, "RetinueLifecycleHandler registered for lifecycle events");
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persistent state needed - lifecycle handler is purely reactive
            // The retinue state itself is managed by ServiceRecordManager/RetinueState
        }

        #region Event Handlers

        /// <summary>
        /// Handles player being taken prisoner. Clears retinue with capture message.
        /// </summary>
        private void OnHeroPrisonerTaken(PartyBase capturingParty, Hero prisoner)
        {
            // Only handle player capture
            if (prisoner != Hero.MainHero)
            {
                return;
            }

            var manager = RetinueManager.Instance;
            if (manager == null || !manager.State.HasRetinue)
            {
                return;
            }

            ModLogger.Info(LogCategory, $"Player captured by {capturingParty?.Name} - clearing retinue");

            // Clear retinue with capture message
            ClearRetinueWithMessage("capture");
        }

        /// <summary>
        /// Handles enlistment ending for any reason. Determines appropriate retinue fate.
        /// Also clears companion assignments on full discharge (not grace period).
        /// If player chose to keep troops on retirement, skips retinue clearing.
        /// </summary>
        private void OnEnlistmentEnded(string reason)
        {
            var enlistment = EnlistmentBehavior.Instance;
            var isFullDischarge = enlistment?.IsInDesertionGracePeriod != true;

            // Clear companion assignments on full discharge only
            if (isFullDischarge)
            {
                CompanionAssignmentManager.Instance?.ClearAllSettings();
                ModLogger.Debug(LogCategory, "Cleared companion assignments on full discharge");
            }

            // If player chose to keep their troops, skip clearing (handled by ServiceRecordManager)
            if (EnlistmentBehavior.RetainTroopsOnRetirement)
            {
                ModLogger.Info(LogCategory, "Skipping retinue clear - player retained troops on retirement");
                return;
            }

            var manager = RetinueManager.Instance;
            if (manager == null || !manager.State.HasRetinue)
            {
                return;
            }

            var lifecycleReason = DetermineLifecycleReason(reason);

            ModLogger.Info(LogCategory,
                $"Enlistment ended (reason: {reason}) - clearing retinue (lifecycle: {lifecycleReason})");

            ClearRetinueWithMessage(lifecycleReason);
        }

        /// <summary>
        /// Handles lord death. If player's lord dies, retinue scatters.
        /// Note: EnlistmentBehavior handles grace period, we just clear the retinue.
        /// </summary>
        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail,
            bool showNotification)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return;
            }

            // Check if this is the player's lord
            if (victim != enlistment.CurrentLord)
            {
                return;
            }

            var manager = RetinueManager.Instance;
            if (manager == null || !manager.State.HasRetinue)
            {
                return;
            }

            ModLogger.Info(LogCategory, $"Enlisted lord {victim?.Name} died - clearing retinue");

            // Mark that this discharge is from lord death (for OnEnlistmentEnded)
            _dischargeDuringGrace = true;
            _lastDischargeReason = "lord_died";

            // Don't clear here - let OnEnlistmentEnded handle it when StopEnlist is called
            // This prevents double-clearing
        }

        /// <summary>
        /// Handles army dispersal. If player's army is defeated, retinue scatters.
        /// </summary>
        private void OnArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isLeaderPartyRemoved)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return;
            }

            // Check if this is the player's lord's army
            if (army?.LeaderParty?.LeaderHero != enlistment.CurrentLord)
            {
                return;
            }

            var manager = RetinueManager.Instance;
            if (manager == null || !manager.State.HasRetinue)
            {
                return;
            }

            // Only trigger on defeat-related dispersions that indicate the lord is lost
            // Valid defeat reasons: LeaderPartyRemoved, ArmyLeaderIsDead, PlayerTakenPrisoner
            if (reason != Army.ArmyDispersionReason.LeaderPartyRemoved &&
                reason != Army.ArmyDispersionReason.ArmyLeaderIsDead &&
                reason != Army.ArmyDispersionReason.NotEnoughParty)
            {
                // Check if lord is actually lost (for other dispersion reasons)
                var lord = enlistment.CurrentLord;
                if (lord is { IsAlive: true, PartyBelongedTo: not null })
                {
                    return;
                }
            }

            ModLogger.Info(LogCategory, $"Army dispersed (reason: {reason}) - marking for retinue clear");

            // Mark that this discharge is from army defeat
            _dischargeDuringGrace = true;
            _lastDischargeReason = "army_defeat";

            // Don't clear here - let OnEnlistmentEnded handle it
        }

        /// <summary>
        /// Daily tick handler - checks for leave expiration (desertion).
        /// </summary>
        private void OnDailyTick()
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    return;
                }

                // Check if player is on leave and leave has expired (desertion scenario)
                // The EnlistmentBehavior handles the desertion itself, but we check for
                // retinue clearing needs. We calculate progress using LeaveStartDate.
                if (enlistment.IsOnLeave && enlistment.LeaveStartDate != CampaignTime.Zero)
                {
                    var daysOnLeave = (CampaignTime.Now - enlistment.LeaveStartDate).ToDays;
                    var maxLeaveDays = EnlistedConfig.LoadGameplayConfig().LeaveMaxDays;

                    // If leave has expired (deserted)
                    if (daysOnLeave >= maxLeaveDays)
                    {
                        var manager = RetinueManager.Instance;
                        if (manager != null && manager.State.HasRetinue)
                        {
                            ModLogger.Info(LogCategory, "Leave expired (desertion) - clearing retinue");
                            ClearRetinueWithMessage("desertion");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error in daily lifecycle tick", ex);
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Maps discharge reasons from EnlistmentBehavior to lifecycle event types.
        /// </summary>
        private string DetermineLifecycleReason(string dischargeReason)
        {
            // Check if we have a pending grace-period reason from earlier events
            if (_dischargeDuringGrace && !string.IsNullOrEmpty(_lastDischargeReason))
            {
                var graceReason = _lastDischargeReason;
                _dischargeDuringGrace = false;
                _lastDischargeReason = string.Empty;
                return graceReason;
            }

            // Map discharge reasons to lifecycle types
            var lowerReason = dischargeReason?.ToLowerInvariant() ?? string.Empty;

            if (lowerReason.Contains("desert"))
            {
                return "desertion";
            }

            if (lowerReason.Contains("capture") || lowerReason.Contains("prisoner"))
            {
                return "capture";
            }

            if (lowerReason.Contains("lord") && (lowerReason.Contains("killed") || lowerReason.Contains("died") ||
                                                 lowerReason.Contains("fallen")))
            {
                return "lord_died";
            }

            if (lowerReason.Contains("army") && (lowerReason.Contains("defeat") || lowerReason.Contains("lost")))
            {
                return "army_defeat";
            }

            if (lowerReason.Contains("retire") || lowerReason.Contains("discharge") ||
                lowerReason.Contains("term") || lowerReason.Contains("end"))
            {
                return "enlistment_end";
            }

            // Default to generic enlistment end
            return "enlistment_end";
        }

        /// <summary>
        /// Clears the retinue and displays the appropriate lifecycle message.
        /// </summary>
        /// <param name="reason">The lifecycle reason (capture, desertion, lord_died, army_defeat, enlistment_end)</param>
        public void ClearRetinueWithMessage(string reason)
        {
            var manager = RetinueManager.Instance;
            if (manager == null)
            {
                return;
            }

            var soldierCount = manager.State.TotalSoldiers;
            if (soldierCount <= 0)
            {
                return;
            }

            // Clear the retinue troops from the roster
            manager.ClearRetinueTroops(reason);

            // Display the appropriate message
            var message = GetLifecycleMessage(reason);
            if (!string.IsNullOrEmpty(message))
            {
                InformationManager.DisplayMessage(new InformationMessage(message, Colors.Red));

                // Also show a quick notification for visibility
                var quickMsg = new TextObject(message);
                MBInformationManager.AddQuickInformation(quickMsg, soundEventPath: "event:/ui/notification/quest_fail");
            }

            ModLogger.Info(LogCategory, $"Retinue cleared: {soldierCount} soldiers (reason: {reason})");
        }

        /// <summary>
        /// Gets the localized message for a lifecycle event.
        /// </summary>
        private static string GetLifecycleMessage(string reason)
        {
            return reason switch
            {
                "capture" =>
                    new TextObject("{=ct_capture_retinue_lost}Your retinue has scattered. Your soldiers fled when you were captured.").ToString(),

                "desertion" =>
                    new TextObject("{=ct_desert_retinue_lost}Your retinue has abandoned you. Deserters cannot command men.").ToString(),

                "lord_died" =>
                    new TextObject("{=ct_lord_died_retinue}With your lord fallen, your retinue has scattered to the winds.").ToString(),

                "army_defeat" =>
                    new TextObject("{=ct_defeat_retinue_lost}In the chaos of defeat, your retinue has scattered.").ToString(),

                "enlistment_end" =>
                    new TextObject("{=ct_enlist_end_retinue}Your soldiers have returned to the army ranks. Serve again to command new men.").ToString(),

                "type_change" =>
                    new TextObject("{=ct_type_change_dismiss}Your current soldiers have been dismissed to make way for new recruits.").ToString(),

                _ =>
                    new TextObject("{=ct_defeat_retinue_lost}In the chaos of defeat, your retinue has scattered.").ToString()
            };
        }

        #endregion
    }
}

