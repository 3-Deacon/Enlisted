using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
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
    }
}
