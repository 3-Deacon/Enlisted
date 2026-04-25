using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Patches MapScreen's conversation handling to suspend/resume the combat log layer.
    /// This hooks into the same conversation lifecycle that native MapViews use.
    /// </summary>
    public static class CombatLogConversationPatch
    {
        /// <summary>
        /// Patch the IMapStateHandler.OnMapConversationStarts implementation in MapScreen.
        /// This is called when a map conversation (portrait style) begins.
        /// </summary>
        [HarmonyPatch("SandBox.View.Map.MapScreen", "HandleMapConversationInit")]
        public static class MapConversationStartPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                try
                {
                    EnlistedCombatLogBehavior.Instance?.SuspendLayer();
                    ModLogger.Debug("Interface", "Combat log suspended via MapScreen conversation patch");
                }
                catch (System.Exception ex)
                {
                    ModLogger.Caught("Interface", "Error in CombatLogConversationPatch.Start", ex);
                }
            }
        }

        /// <summary>
        /// Patch the IMapStateHandler.OnMapConversationOver implementation in MapScreen.
        /// This is called when a map conversation ends.
        /// Uses TargetMethod because OnMapConversationOver is an explicit interface implementation.
        /// </summary>
        [HarmonyPatch]
        public static class MapConversationEndPatch
        {
            [HarmonyTargetMethod]
            public static System.Reflection.MethodBase TargetMethod()
            {
                // Resolve the explicit interface implementation:
                // void IMapStateHandler.OnMapConversationOver() in SandBox.View.Map.MapScreen
                // Use AccessTools.TypeByName which searches all loaded assemblies (more reliable than Type.GetType)
                var mapScreenType = AccessTools.TypeByName("SandBox.View.Map.MapScreen");
                if (mapScreenType == null)
                {
                    ModLogger.Caught("Bootstrap", "Failed to find MapScreen type for MapConversationEndPatch", null);
                    return null;
                }

                var interfaceType = AccessTools.TypeByName("TaleWorlds.CampaignSystem.GameState.IMapStateHandler");
                if (interfaceType == null)
                {
                    ModLogger.Caught("Bootstrap", "Failed to find IMapStateHandler interface for MapConversationEndPatch", null);
                    return null;
                }

                // Get the explicit interface implementation method
                var interfaceMap = mapScreenType.GetInterfaceMap(interfaceType);
                for (int i = 0; i < interfaceMap.InterfaceMethods.Length; i++)
                {
                    if (interfaceMap.InterfaceMethods[i].Name == "OnMapConversationOver")
                    {
                        ModLogger.Debug("Bootstrap", "MapConversationEndPatch: Found OnMapConversationOver method");
                        return interfaceMap.TargetMethods[i];
                    }
                }

                ModLogger.Caught("Bootstrap", "Failed to find OnMapConversationOver method in MapScreen interface map", null);
                return null;
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                try
                {
                    EnlistedCombatLogBehavior.Instance?.ResumeLayer();
                    ModLogger.Debug("Interface", "Combat log resumed via MapScreen conversation patch");
                }
                catch (System.Exception ex)
                {
                    ModLogger.Caught("Interface", "Error in CombatLogConversationPatch.End", ex);
                }
            }
        }
    }
}
