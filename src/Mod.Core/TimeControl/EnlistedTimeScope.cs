using System;
using TaleWorlds.CampaignSystem;
using Enlisted.Features.Equipment.Behaviors;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.Core.TimeControl
{
    /// <summary>
    /// Scoped capture of Campaign.TimeControlMode with automatic pause + lock,
    /// restored on Dispose. Use ONLY for regions where the open call and close
    /// call run synchronously on the main campaign thread and the close is
    /// guarded by a finally block (the three Gauntlet overlays qualify).
    ///
    /// DO NOT use for cross-frame cases where a menu is activated via
    /// NextFrameDispatcher or where restore happens on an engine menu-exit
    /// callback — the scope would dispose before the work completes.
    /// See deferred item #2b in the 2026-04-18 architecture-fixes spec.
    /// </summary>
    public readonly struct EnlistedTimeScope : IDisposable
    {
        private readonly CampaignTimeControlMode? _captured;
        private readonly bool _ownedLock;

        private EnlistedTimeScope(CampaignTimeControlMode? captured, bool ownedLock)
        {
            _captured = captured;
            _ownedLock = ownedLock;
        }

        /// <summary>
        /// Capture the current TimeControlMode. If pauseAndLock is true (default),
        /// sets TimeControlMode.Stop and SetTimeControlModeLock(true) for the
        /// scope's lifetime.
        /// </summary>
        public static EnlistedTimeScope Capture(bool pauseAndLock = true)
        {
            if (Campaign.Current == null)
            {
                ModLogger.Expected("TIMECONTROL", "timescope_campaign_null",
                    "EnlistedTimeScope.Capture called with no active Campaign.Current");
                return default;
            }

            var captured = Campaign.Current.TimeControlMode;
            if (pauseAndLock)
            {
                Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
                Campaign.Current.SetTimeControlModeLock(true);
            }
            return new EnlistedTimeScope(captured, pauseAndLock);
        }

        public void Dispose()
        {
            if (Campaign.Current == null || _captured == null)
            {
                return;
            }
            if (_ownedLock)
            {
                Campaign.Current.SetTimeControlModeLock(false);
            }
            // Defensive: skip restore if the engine changed the mode during the
            // scope (e.g., battle start, settlement entry). For the three narrow
            // Gauntlet sites, decompile verification showed no engine writes fire
            // during their windows, but the check is cheap insurance.
            var current = Campaign.Current.TimeControlMode;
            if (current != CampaignTimeControlMode.Stop && current != _captured.Value)
            {
                return;
            }
            Campaign.Current.TimeControlMode = QuartermasterManager.NormalizeToStoppable(_captured.Value);
        }
    }
}
