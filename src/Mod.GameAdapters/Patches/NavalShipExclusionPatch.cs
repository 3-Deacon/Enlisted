using Enlisted.Mod.Core.Logging;
using HarmonyLib;

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
        /// <summary>
        ///     Reset the logging flag when a new campaign starts or loads.
        ///     Called from SubModule or EnlistmentBehavior on campaign start.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "May be called for session reset")]
        public static void ResetSessionLogging()
        {
            ModLogger.Debug("Naval", "Ship damage protection session logging reset");
        }
    }
}
