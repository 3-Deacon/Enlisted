using System;
using System.Reflection;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Suppresses formation behavior notification messages for enlisted soldiers.
    ///     The vanilla game shows messages when formation behaviors change, but the soldier
    ///     notification code uses this.ToString() which outputs the full type name like
    ///     "TaleWorlds.MountAndBlade.BehaviorGeneral" instead of a localized message.
    ///     The vanilla code tries to strip "MBModule.Behavior" prefix but the actual classes
    ///     use "TaleWorlds.MountAndBlade" namespace, so the full class name gets displayed:
    ///     "Men! TaleWorlds.MountAndBlade.BehaviorGeneral!"
    ///     
    ///     CRITICAL FIX: We must ONLY suppress for the player's specific formation.
    ///     The previous implementation returned false for ALL formations when enlisted,
    ///     which blocked OnBehaviorActivatedAux() from running - this is where formations
    ///     receive their movement orders! This caused armies to freeze after cavalry skirmishes
    ///     because infantry/main formations never got their Advance/Charge orders.
    ///     
    ///     Now we:
    ///     1. Let ALL non-player formations run vanilla (essential for AI movement!)
    ///     2. For player's formation only: suppress message but still call OnBehaviorActivatedAux()
    ///     3. This preserves AI behavior for the player's squad (companions + retinue)
    /// </summary>

    // Harmony Patch
    // Target: TaleWorlds.MountAndBlade.BehaviorComponent.OnBehaviorActivated()
    // Why: Suppress broken localization messages for enlisted soldiers
    // Safety: Only affects the player's specific formation; all other formations run vanilla
    [HarmonyPatch(typeof(BehaviorComponent), "OnBehaviorActivated")]
    public class FormationMessageSuppressionPatch
    {
        // Cache the protected method reference for performance - we need to call it manually
        // when suppressing the message but preserving AI behavior
        private static readonly MethodInfo OnBehaviorActivatedAuxMethod =
            AccessTools.Method(typeof(BehaviorComponent), "OnBehaviorActivatedAux");

        /// <summary>
        ///     Prefix that suppresses broken behavior notification messages for enlisted soldiers.
        ///     
        ///     CRITICAL: We must only intercept for the player's formation!
        ///     All other formations MUST run vanilla OnBehaviorActivated() or their AI breaks.
        ///     OnBehaviorActivatedAux() is where movement orders are set - blocking it causes
        ///     formations to freeze in place.
        ///     
        ///     For the player's formation (which includes companions and retinue at Tier 4+),
        ///     we suppress the broken message but still call OnBehaviorActivatedAux() to ensure
        ///     the squad continues to receive proper movement orders from the AI.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(BehaviorComponent __instance)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true; // Not enlisted - run vanilla behavior
                }

                // Null safety check for formation
                if (__instance?.Formation == null)
                {
                    return true; // Invalid state - let vanilla handle it
                }

                // CRITICAL: Only intercept for the player's specific formation!
                // All other formations MUST run vanilla or their AI completely breaks.
                // This includes enemy formations, allied formations, and any formation
                // the player is NOT part of.
                if (!__instance.Formation.IsPlayerTroopInFormation)
                {
                    return true; // Player not in this formation - run vanilla (essential for AI!)
                }

                // Player IS in this formation (their squad with companions/retinue)
                // We want to suppress the broken "Men! TaleWorlds.MountAndBlade.BehaviorX!" message
                // BUT we MUST still call OnBehaviorActivatedAux() for AI behavior to work!
                // Without this, the player's squad would freeze and not follow formation orders.
                
                if (__instance.Formation.IsAIControlled && OnBehaviorActivatedAuxMethod != null)
                {
                    // Call the protected OnBehaviorActivatedAux() method via reflection
                    // This sets up movement orders, facing, arrangement, firing orders, etc.
                    OnBehaviorActivatedAuxMethod.Invoke(__instance, null);
                }

                // Skip vanilla OnBehaviorActivated() - we've handled the AI activation above,
                // and we don't want the broken notification message
                return false;
            }
            catch (Exception ex)
            {
                // On any error, fail open to vanilla behavior to avoid breaking battles
                ModLogger.Caught("FormationPatch", "Error in FormationMessageSuppressionPatch", ex);
                return true;
            }
        }
    }
}
