using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Prevents the game from treating the enlisted player as "waiting" when following their lord.
    /// 
    /// Problem: When the player's party is set to EscortParty behavior (following the lord),
    /// and the lord enters battle, MobileParty.ComputeIsWaiting() returns true because the
    /// escort target is "busy". This sets Campaign.IsMainPartyWaiting = true, which:
    /// - Prevents time from flowing even when the player tries to unpause
    /// - Causes a delay before the encounter menu appears
    /// - Results in the player joining battles late
    /// 
    /// Solution: Override ComputeIsWaiting() to return false when the player is enlisted,
    /// allowing time to flow normally and encounters to process immediately.
    /// </summary>
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.ComputeIsWaiting))]
    public static class EnlistedWaitingPatch
    {
        /// <summary>
        /// Postfix patch that overrides the waiting state for enlisted players.
        /// When enlisted (not on leave), the player should never be considered "waiting"
        /// so that battles can start immediately when the lord engages.
        /// Called by Harmony via reflection.
        /// </summary>
        [HarmonyPostfix]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance and __result are special injected parameters")]
        public static void Postfix(MobileParty __instance, ref bool __result)
        {
            // Only affect the main party - don't interfere with AI parties
            if (__instance != MobileParty.MainParty)
            {
                return;
            }

            // Check if player is enlisted and actively serving (not on leave)
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true || enlistment.IsOnLeave)
            {
                return;
            }

            // If the native code said we're waiting, override it
            // This allows time to flow and encounters to trigger immediately
            if (__result)
            {
                __result = false;
                // Log once per session that we're overriding waiting state
                ModLogger.LogOnce("waiting_override", "Patch", "Overriding 'waiting' state for enlisted player - allows time flow during battles");
            }
        }
    }
}

