using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Samples the lord's party every in-game hour and commits a duty-profile change only after two consecutive matching samples, with a hard-bypass for siege/army/rethink events.</summary>
    public sealed class DutyProfileBehavior : CampaignBehaviorBase
    {
        private const int HYSTERESIS_THRESHOLD = 2;
        private const int MIN_HOURS_BETWEEN_TRANSITION_BEATS = 4;
        private const int HEARTBEAT_INTERVAL_HOURS = 6;

        private int _lastTransitionBeatHourTick = int.MinValue;
        private int _lastHeartbeatHourTick = int.MinValue;

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // State lives on OrderActivity; no per-behavior persistence required.
        }

        private void OnHourlyTick()
        {
            try
            {
                LogHeartbeatIfDue();

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return;
                }

                var lordParty = enlistment.EnlistedLord?.PartyBelongedTo;
                if (lordParty == null)
                {
                    return;
                }

                var orderActivity = OrderActivity.Instance;
                if (orderActivity == null)
                {
                    return;
                }

                var observed = DutyProfileSelector.Resolve(lordParty);
                var committed = orderActivity.CurrentDutyProfile;

                if (observed == committed)
                {
                    orderActivity.PendingProfileMatches.Clear();
                    return;
                }

                bool hardBypass = ShouldBypassHysteresis(orderActivity, lordParty);

                if (!hardBypass)
                {
                    if (!orderActivity.PendingProfileMatches.TryGetValue(observed, out var count))
                    {
                        count = 0;
                    }
                    orderActivity.PendingProfileMatches[observed] = count + 1;
                    if (orderActivity.PendingProfileMatches[observed] < HYSTERESIS_THRESHOLD)
                    {
                        return;
                    }
                }

                CommitTransition(orderActivity, committed, observed);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTYPROFILE", "OnHourlyTick threw", ex);
            }
        }

        private void LogHeartbeatIfDue()
        {
            try
            {
                var nowHour = (int)CampaignTime.Now.ToHours;
                if (nowHour - _lastHeartbeatHourTick < HEARTBEAT_INTERVAL_HOURS)
                {
                    return;
                }
                _lastHeartbeatHourTick = nowHour;

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    ModLogger.Info("DUTYPROFILE", $"heartbeat: not_enlisted (hour {nowHour})");
                    return;
                }

                var orderActivity = OrderActivity.Instance;
                var lordParty = enlistment.EnlistedLord?.PartyBelongedTo;
                if (orderActivity == null || lordParty == null)
                {
                    ModLogger.Info("DUTYPROFILE", $"heartbeat: enlisted but no_activity_or_lord_party (activity={orderActivity != null}, lordParty={lordParty != null})");
                    return;
                }

                var observed = DutyProfileSelector.Resolve(lordParty);
                var committed = orderActivity.CurrentDutyProfile;
                var pending = orderActivity.PendingProfileMatches?.Count ?? 0;
                ModLogger.Info("DUTYPROFILE", $"heartbeat: observed={observed} committed={committed} pending_count={pending}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTYPROFILE", "heartbeat threw", ex);
            }
        }

        private static bool ShouldBypassHysteresis(OrderActivity activity, MobileParty lordParty)
        {
            if (activity.CurrentDutyProfile == DutyProfileIds.Besieging && lordParty.BesiegedSettlement == null)
            {
                return true;
            }
            if (activity.CurrentDutyProfile == DutyProfileIds.Escorting && lordParty.Army == null)
            {
                return true;
            }
            if (lordParty.Ai != null && lordParty.Ai.RethinkAtNextHourlyTick)
            {
                return true;
            }
            return false;
        }

        private void CommitTransition(OrderActivity activity, string oldProfile, string newProfile)
        {
            // Profile mutation commits unconditionally; only the beat dispatch is throttled.
            activity.CurrentDutyProfile = newProfile;
            activity.PendingProfileMatches.Clear();

            var nowHour = (int)CampaignTime.Now.ToHours;
            if (nowHour - _lastTransitionBeatHourTick < MIN_HOURS_BETWEEN_TRANSITION_BEATS)
            {
                ModLogger.Expected("DUTYPROFILE", "transition_beat_throttled",
                    $"profile_changed beat coalesced: {oldProfile} -> {newProfile}");
                return;
            }
            _lastTransitionBeatHourTick = nowHour;

            ModLogger.Info("DUTYPROFILE", $"profile_changed: {oldProfile} -> {newProfile}");

            activity.OnBeat("profile_changed", new ActivityContext
            {
                Args = new Dictionary<string, string>
                {
                    { "old_profile", oldProfile },
                    { "new_profile", newProfile }
                }
            });
        }

        /// <summary>Sets the profile unconditionally, clears any pending hysteresis samples, and does not fire a profile_changed beat.</summary>
        public void ForceProfile(string profileId)
        {
            var activity = OrderActivity.Instance;
            if (activity == null)
            {
                return;
            }
            var old = activity.CurrentDutyProfile;
            activity.CurrentDutyProfile = profileId;
            activity.PendingProfileMatches.Clear();
            ModLogger.Info("DUTYPROFILE", $"profile_forced: {old} -> {profileId}");
        }
    }
}
