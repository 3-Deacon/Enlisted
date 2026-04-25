using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using System;
using System.Diagnostics;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Intercepts InformationManager.DisplayMessage to route messages to the custom
    /// enlisted combat log when the player is enlisted. Allows native display to resume
    /// when not enlisted (prisoner, left army, etc.).
    /// </summary>
    [HarmonyPatch(typeof(InformationManager), "DisplayMessage")]
    internal class InformationManagerDisplayMessagePatch
    {
        private static readonly string[] PlayerUpdateMarkers =
        {
            " XP",
            "trait XP",
            "enlistment XP",
            "Past few days:",
        };

        private static readonly string[] PlayerUpdateSourceMarkers =
        {
            "Enlisted.Features.Enlistment.Behaviors.EnlistmentBehavior",
            "Enlisted.Features.Content.EventDeliveryManager",
            "Enlisted.Features.Activities.Orders.DailyDriftApplicator",
            "Enlisted.Features.Activities.TrainingXpScaler",
        };

        /// <summary>
        /// Prefix that decides whether to show messages in native log or custom log.
        /// Returns false to suppress native display when enlisted.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(InformationMessage message)
        {
            try
            {
                // Allow native log during missions (battles)
                if (Mission.Current != null)
                {
                    return true; // Execute original DisplayMessage
                }

                // Not enlisted: use native combat log
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return true; // Execute original DisplayMessage
                }

                if (ShouldSuppressPlayerUpdate(message))
                {
                    return false; // Drop personal progression/reward toasts; news/feed owns them
                }

                // Enlisted: route to custom combat log
                var combatLog = EnlistedCombatLogBehavior.Instance;
                if (combatLog != null)
                {
                    combatLog.AddMessage(message);
                    return false; // Skip original DisplayMessage
                }
                else
                {
                    // Fallback to native if combat log not initialized yet
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.Caught("Interface", $"Error in combat log patch: {ex.Message}", ex);
                return true; // Fallback to native on error
            }
        }

        private static bool ShouldSuppressPlayerUpdate(InformationMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Information))
            {
                return false;
            }

            bool matchesText = false;
            foreach (var marker in PlayerUpdateMarkers)
            {
                if (message.Information.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchesText = true;
                    break;
                }
            }

            if (!matchesText)
            {
                return false;
            }

            var trace = new StackTrace(2, false);
            var frames = trace.GetFrames();
            if (frames == null)
            {
                return false;
            }

            foreach (var frame in frames)
            {
                var declaringType = frame.GetMethod()?.DeclaringType;
                var fullName = declaringType?.FullName;
                if (string.IsNullOrEmpty(fullName))
                {
                    continue;
                }

                foreach (var marker in PlayerUpdateSourceMarkers)
                {
                    if (fullName.IndexOf(marker, StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
