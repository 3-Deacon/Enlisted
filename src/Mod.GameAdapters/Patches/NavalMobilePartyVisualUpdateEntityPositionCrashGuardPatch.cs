using System;
using System.Threading;
using HarmonyLib;
using Enlisted.Mod.Core.Logging;
namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Crash guard for a native Naval DLC map visual crash observed when pushing Gauntlet screens.
    ///
    /// Observed stack trace:
    /// NavalDLC_View!NavalDLC.View.Map.Visuals.NavalMobilePartyVisual.UpdateEntityPosition(...)
    ///
    /// NavalDLC_View updates map visuals on background threads. During screen transitions (PushScreen),
    /// the native visual update path can crash. We cannot catch that crash in managed code, so we prevent
    /// the risky code path from running during the transition window.
    ///
    /// This patch intentionally does minimal work and only reads a volatile flag exposed by
    /// LanceLifeEventScreen, so it is safe to execute on worker threads.
    /// </summary>
    public static class NavalMobilePartyVisualUpdateEntityPositionCrashGuardPatch
    {
        private const string LogCategory = "Naval";
        private static bool _patchApplied;
        // Skip logs for event screens are currently disabled.
        private static int _loggedInvalidDt;

        /// <summary>
        /// Apply the patch via reflection, only if the Naval DLC View assembly is present.
        /// Must be called after the campaign is ready (Naval view types are not always available at module load).
        /// </summary>
        public static void TryApplyPatch(Harmony harmony)
        {
            if (_patchApplied)
            {
                return;
            }

            try
            {
                var visualType = AccessTools.TypeByName("NavalDLC.View.Map.Visuals.NavalMobilePartyVisual");
                if (visualType == null)
                {
                    ModLogger.Debug(LogCategory, "Naval DLC View not loaded - UpdateEntityPosition crash guard not applied");
                    return;
                }

                // Decompiled signature (Bannerlord 1.3.x):
                // internal void UpdateEntityPosition(float dt, float realDt, bool isVisible = false)
                var targetMethod = AccessTools.Method(visualType, "UpdateEntityPosition",
                    new[] { typeof(float), typeof(float), typeof(bool) });
                if (targetMethod == null)
                {
                    ModLogger.Warn(LogCategory, "Could not find NavalMobilePartyVisual.UpdateEntityPosition - crash guard not applied");
                    return;
                }

                var prefixMethod = AccessTools.Method(
                    typeof(NavalMobilePartyVisualUpdateEntityPositionCrashGuardPatch),
                    nameof(UpdateEntityPositionPrefix));

                harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));
                _patchApplied = true;

                ModLogger.Info(LogCategory, "Naval UpdateEntityPosition crash guard patch registered");
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Failed to apply UpdateEntityPosition crash guard patch", ex);
            }
        }

        /// <summary>
        /// Prefix that skips the original UpdateEntityPosition call while the Lance Life event screen is
        /// opening/open, and when dt arguments are invalid (NaN/Infinity).
        /// </summary>
        /// <remarks>
        /// IMPORTANT: This method can run on background worker threads. Do not touch ScreenManager,
        /// Campaign objects, or any non-thread-safe game state here.
        /// </remarks>
        public static bool UpdateEntityPositionPrefix(float dt, float realDt, bool isVisible)
        {
            try
            {
                // Visual crash guards are currently being reassessed for the updated event system.
                /*
                if (LanceLifeEventScreen.IsNavalVisualCrashGuardActive)
                {
                    // Ultra-low overhead: log at most once per session, then silently skip.
                    if (Volatile.Read(ref _loggedSkipWhileEventScreenActive) == 0 &&
                        Interlocked.CompareExchange(ref _loggedSkipWhileEventScreenActive, 1, 0) == 0)
                    {
                        ModLogger.Warn(LogCategory,
                            "Skipping NavalMobilePartyVisual.UpdateEntityPosition while LanceLifeEventScreen is opening/open (crash guard)");
                    }
                    return false;
                }
                */

                // Defensive: avoid ever feeding NaN/Infinity into the native side.
                if (float.IsNaN(dt) || float.IsInfinity(dt) || float.IsNaN(realDt) || float.IsInfinity(realDt))
                {
                    if (Volatile.Read(ref _loggedInvalidDt) == 0 &&
                        Interlocked.CompareExchange(ref _loggedInvalidDt, 1, 0) == 0)
                    {
                        ModLogger.Warn(LogCategory,
                            "Skipping NavalMobilePartyVisual.UpdateEntityPosition due to invalid dt/realDt (NaN/Infinity)");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Never allow a crash guard to crash the game. If something goes wrong, just allow original.
                ModLogger.Caught(LogCategory, "Error in UpdateEntityPosition crash guard prefix", ex);
            }

            return true;
        }
    }
}


