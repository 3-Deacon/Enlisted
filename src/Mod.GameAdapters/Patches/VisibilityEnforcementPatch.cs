using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Prevents the native game from making the player party visible when enlisted.
    /// When other NPCs approach, the game sets IsVisible = true for encounters,
    /// but enlisted players should stay invisible (except during battles/sieges).
    /// This patch intercepts visibility changes and blocks them for enlisted players.
    /// </summary>
    // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch(typeof(MobileParty), "IsVisible", MethodType.Setter)]
    public class VisibilityEnforcementPatch
    {
        /// <summary>
        /// Call this immediately BEFORE forcing player out of a settlement.
        /// Ensures visibility is blocked even during the transition frames.
        /// </summary>
        public static void BeginForceHidden()
        {
            ModLogger.Debug("VisibilityEnforcement", "Force hidden mode ENABLED for settlement exit");
        }

        /// <summary>
        /// Call this after hiding is complete and escort is re-established.
        /// </summary>
        public static void EndForceHidden()
        {
            ModLogger.Debug("VisibilityEnforcement", "Force hidden mode DISABLED");
        }
    }
}

