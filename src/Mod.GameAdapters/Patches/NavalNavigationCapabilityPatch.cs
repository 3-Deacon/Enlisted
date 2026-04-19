using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Makes enlisted players inherit naval navigation capability from their lord or army leader.
    ///     
    ///     Problem: When an army travels by sea, the game checks if all member parties have
    ///     HasNavalNavigationCapability (i.e., own ships). Enlisted players typically don't own
    ///     ships - they serve on their lord's vessels. Without this patch, the player gets kicked
    ///     from the army with the menu "menu_player_kicked_out_from_army_navigation_incapability".
    ///     
    ///     Solution: Patch HasNavalNavigationCapability to return true for the player's party when:
    ///     1. Player is enlisted
    ///     2. Their lord OR the army leader has naval capability (owns ships)
    ///     
    ///     This allows the player to travel with the army by sea as they would realistically be
    ///     on the lord's ship, not sailing their own vessel.
    /// </summary>
    [HarmonyPatch(typeof(MobileParty), nameof(MobileParty.HasNavalNavigationCapability), MethodType.Getter)]
    public static class NavalNavigationCapabilityPatch
    {
        private const string LogCategory = "Naval";
        
        // Track if we've logged the capability inheritance this session
        private static bool _hasLoggedInheritance;
        
        // Track diagnostic context for blueprint-friendly debugging
        private static int _checksPerformed;
        private static int _inheritancesGranted;

        /// <summary>
        ///     Postfix that overrides HasNavalNavigationCapability for enlisted players.
        ///     If the player is enlisted and their lord (or army leader) has ships,
        ///     returns true so the player can travel with the army by sea.
        /// </summary>
        [HarmonyPostfix]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", 
            Justification = "Harmony convention: __instance and __result are special injected parameters")]
        public static void Postfix(MobileParty __instance, ref bool __result)
        {
            try
            {
                // Only override if the result is currently false (no ships)
                if (__result)
                {
                    // Player already has ships - no need to inherit
                    ModLogger.Debug(LogCategory, 
                        "Naval capability check: Player has own ships, inheritance not needed");
                    return;
                }

                // Only intercept for the main party
                if (__instance == null || !__instance.IsMainParty)
                {
                    return;
                }
                
                _checksPerformed++;

                // Early exit if campaign not ready
                if (!CampaignSafetyGuard.IsCampaignReady)
                {
                    return;
                }

                // Check if mod is active
                if (!EnlistedActivation.EnsureActive())
                {
                    return;
                }

                // Check if player is enlisted
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    ModLogger.Debug(LogCategory, 
                        $"Naval capability check #{_checksPerformed}: Player not enlisted, no inheritance");
                    return;
                }

                var lord = enlistment.CurrentLord;
                var lordParty = lord?.PartyBelongedTo;

                // If lord or lord's party is gone, can't inherit capability
                if (lordParty == null)
                {
                    ModLogger.Debug(LogCategory, 
                        $"Naval capability check #{_checksPerformed}: Lord party unavailable, no inheritance");
                    return;
                }

                // Check if lord has naval capability (owns ships)
                // IMPORTANT: We must access the underlying Ships collection directly to avoid infinite recursion
                // If we called lordParty.HasNavalNavigationCapability here, it would work fine since lordParty != MainParty
                // But using Ships.Count is more direct and clearer about what we're checking
                var lordShipCount = lordParty.Ships?.Count ?? 0;
                var lordHasShips = lordShipCount > 0;
                
                if (lordHasShips)
                {
                    __result = true;
                    _inheritancesGranted++;
                    
                    var atSea = __instance.IsCurrentlyAtSea;
                    var inArmy = __instance.Army != null;
                    
                    if (!_hasLoggedInheritance)
                    {
                        _hasLoggedInheritance = true;
                        ModLogger.Info(LogCategory,
                            $"Naval capability inherited from lord {lord.Name} ({lordShipCount} ships) - " +
                            $"player can travel with army by sea [AtSea={atSea}, InArmy={inArmy}]");
                    }
                    
                    ModLogger.Debug(LogCategory,
                        $"Naval check #{_checksPerformed}: Inherited from lord {lord.Name} " +
                        $"({lordShipCount} ships) [Total grants: {_inheritancesGranted}, AtSea={atSea}, InArmy={inArmy}]");
                    return;
                }

                // Check army leader as fallback (lord might be in an army led by someone with ships)
                var army = lordParty.Army ?? __instance.Army;
                var armyLeader = army?.LeaderParty;
                
                if (armyLeader != null && armyLeader != __instance)
                {
                    var armyLeaderShipCount = armyLeader.Ships?.Count ?? 0;
                    var armyLeaderHasShips = armyLeaderShipCount > 0;
                    
                    if (armyLeaderHasShips)
                    {
                        __result = true;
                        _inheritancesGranted++;
                        
                        var atSea = __instance.IsCurrentlyAtSea;
                        var leaderName = armyLeader.LeaderHero?.Name?.ToString() ?? "Army Leader";
                        
                        if (!_hasLoggedInheritance)
                        {
                            _hasLoggedInheritance = true;
                            ModLogger.Info(LogCategory,
                                $"Naval capability inherited from army leader {leaderName} ({armyLeaderShipCount} ships) - " +
                                $"player can travel with army by sea [AtSea={atSea}]");
                        }
                        
                        ModLogger.Debug(LogCategory,
                            $"Naval check #{_checksPerformed}: Inherited from army leader {leaderName} " +
                            $"({armyLeaderShipCount} ships) [Total grants: {_inheritancesGranted}, AtSea={atSea}]");
                        return;
                    }
                }
                
                // No inheritance available - log diagnostic
                ModLogger.Debug(LogCategory,
                    $"Naval check #{_checksPerformed}: No inheritance available - " +
                    $"lord has {lordShipCount} ships, army leader has {armyLeader?.Ships?.Count ?? 0} ships " +
                    $"[AtSea={__instance.IsCurrentlyAtSea}]");
            }
            catch (Exception ex)
            {
                // Fail-safe: log error but don't break the game
                ModLogger.Caught(LogCategory, "Error in naval navigation capability patch", ex);
                // Leave __result unchanged on error
            }
        }

        /// <summary>
        ///     Reset the logging flag when a new campaign starts or loads.
        /// </summary>
        public static void ResetSessionLogging()
        {
            var wasActive = _hasLoggedInheritance;
            var previousChecks = _checksPerformed;
            var previousGrants = _inheritancesGranted;
            
            _hasLoggedInheritance = false;
            _checksPerformed = 0;
            _inheritancesGranted = 0;
            
            if (wasActive)
            {
                ModLogger.Info(LogCategory, 
                    $"Naval navigation capability session reset: {previousGrants} inheritances granted over {previousChecks} checks");
            }
            else
            {
                ModLogger.Debug(LogCategory, "Naval navigation capability inheritance logging reset");
            }
        }
    }
}
