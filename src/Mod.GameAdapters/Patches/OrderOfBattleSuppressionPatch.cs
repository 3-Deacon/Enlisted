using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Prevents the Order of Battle deployment screen from appearing when the player is enlisted.
    ///     As an enlisted soldier, the player should not have deployment control and should spawn
    ///     automatically without the deployment screen, just like a regular soldier in an army.
    ///     This patches the base BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle()
    ///     method which is called by AssignPlayerRoleInTeamMissionController.OnPlayerTeamDeployed()
    ///     to decide whether to show the OOB screen.
    ///     Note: We target the base class because CanPlayerSideDeployWithOrderOfBattle() is defined
    ///     there and not overridden in derived classes (SandboxBattleInitializationModel, etc.).
    ///     Derived classes only override the protected CanPlayerSideDeployWithOrderOfBattleAux() method.
    /// </summary>
    [HarmonyPatch(typeof(BattleInitializationModel),
        nameof(BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle))]
    public class OrderOfBattleSuppressionPatch
    {
        /// <summary>
        ///     Prefix that runs before CanPlayerSideDeployWithOrderOfBattle.
        ///     Returns false to prevent Order of Battle screen when player is enlisted.
        /// </summary>
        private static bool Prefix(ref bool __result)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted == true)
                {
                    // Player is enlisted - skip Order of Battle screen
                    // They should spawn directly into battle as a regular soldier
                    __result = false;
                    ModLogger.LogOnce("oob_suppressed", "Battle",
                        "Order of Battle screen suppressed - enlisted soldiers spawn directly into battle");
                    return false; // Skip original method
                }

                // Player not enlisted - allow normal behavior
                return true; // Continue to original method
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OrderOfBattle", "Error in Order of Battle suppression patch", ex);
                return true; // Fail open - allow normal behavior on error
            }
        }
    }
}
