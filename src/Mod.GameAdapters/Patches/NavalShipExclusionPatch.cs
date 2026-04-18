using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Protects enlisted player's ships from sea damage while serving under a lord.
    ///     When sailing at sea, ships normally take attrition damage over time. Since the player
    ///     is just a soldier, their personal ships shouldn't degrade from the lord's voyage.
    ///     Target: NavalDLC.GameComponents.NavalDLCCampaignShipDamageModel.GetHourlyShipDamage
    ///     This method is called by SeaDamageCampaignBehavior.HourlyTickParty for each ship.
    /// </summary>
    // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch]
    public class NavalShipDamageProtectionPatch
    {
        // Track if we've logged protection details this session (for diagnostic purposes)
        private static bool _hasLoggedProtectionDetails;

        /// <summary>
        ///     Intercept ship damage calculation before it runs.
        ///     Returns 0 damage for player's ships when enlisted.
        /// </summary>
        [HarmonyPatch("NavalDLC.GameComponents.NavalDLCCampaignShipDamageModel", "GetHourlyShipDamage")]
        [HarmonyPrefix]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
        private static bool Prefix(MobileParty owner, Ship ship, ref int __result)
        {
            try
            {
                // Early exit if campaign not ready - let original method run
                if (!CampaignSafetyGuard.IsCampaignReady)
                {
                    return true;
                }

                // Check enlistment state
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true; // Not enlisted, normal damage applies
                }

                // Check if this is the player's party
                if (owner == null || owner != MobileParty.MainParty)
                {
                    return true; // Not player's party, normal damage applies
                }

                // Player is enlisted and this is their ship - prevent damage
                __result = 0;

                // Log protection activation once per session with diagnostic details
                if (!_hasLoggedProtectionDetails)
                {
                    _hasLoggedProtectionDetails = true;

                    var shipCount = MobileParty.MainParty?.Ships?.Count ?? 0;
                    var lordName = enlistment.EnlistedLord?.Name?.ToString() ?? "Unknown";

                    ModLogger.Info("Naval",
                        $"Ship damage protection active - Player ships ({shipCount}) protected while serving under {lordName}");
                }

                // Debug-level logging for detailed diagnostics (only when debug enabled)
                ModLogger.Debug("Naval",
                    $"Protected ship '{ship?.ShipHull?.Name?.ToString() ?? "Unknown"}' from sea attrition damage");

                return false; // Skip original method - we've set result to 0
            }
            catch (Exception ex)
            {
                // Log full exception for troubleshooting, but don't break the game
                ModLogger.Caught("NavalShipDamageProtection", "Error in ship damage protection patch", ex);

                return true; // Fail-safe: let original method run on error
            }
        }

        /// <summary>
        ///     Reset the logging flag when a new campaign starts or loads.
        ///     Called from SubModule or EnlistmentBehavior on campaign start.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "May be called for session reset")]
        public static void ResetSessionLogging()
        {
            _hasLoggedProtectionDetails = false;
            ModLogger.Debug("Naval", "Ship damage protection session logging reset");
        }
    }
}
