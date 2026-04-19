using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Fixes naval battle crashes for enlisted players who don't own ships.
    /// 
    /// The problem: Naval DLC assumes all parties in battle own ships. Enlisted players
    /// don't have ships - they serve on their lord's vessels. This causes crashes:
    /// 1. GetSuitablePlayerShip - MinBy on empty ship collection
    /// 2. GetOrderedCaptainsForPlayerTeamShips - null party when looking up ship owner  
    /// 3. AllocateAndDeployInitialTroopsOfPlayerTeam - null MissionShip when spawning
    /// 4. BehaviorNavalEngageCorrespondingEnemy - null ship for formations without vessels
    /// 
    /// Solution: Four patches that redirect the enlisted player to spawn on the lord's ship
    /// as crew (not captain), and prevent AI behavior crashes for shipless formations.
    /// </summary>
    [HarmonyPatch]
    public static class NavalBattleShipAssignmentPatch
    {
        private const string LogCategory = "Naval";
        
        // Track if we're handling an enlisted player's naval battle
        internal static bool UsingLordShip { get; private set; }
        internal static Hero CurrentLord { get; private set; }
        
        // Track patch activity for diagnostics (per-battle counters)
        internal static int FormationsProcessed { get; set; }
        internal static int FormationsSkipped { get; set; }

        /// <summary>
        /// Patch 1: GetSuitablePlayerShip - prevents the MinBy crash by returning lord's ship.
        /// </summary>
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var navalDlcType = AccessTools.TypeByName("NavalDLC.GameComponents.NavalDLCShipDeploymentModel");
            if (navalDlcType == null)
            {
                // Non-spammy: if War Sails isn't loaded (or API changed), this patch won't apply.
                ModLogger.Expected("Reflection", "reflect_naval_ship_deploy_model_missing",
                    "NavalDLCShipDeploymentModel not found; naval enlisted crash guards will be disabled (War Sails not loaded or API changed).");
                return null;
            }

            var method = AccessTools.Method(navalDlcType, "GetSuitablePlayerShip");
            if (method != null)
            {
                ModLogger.Info(LogCategory, "Naval enlisted crew fix registered (GetSuitablePlayerShip)");
            }

            return method;
        }

        [HarmonyPrefix]
        public static bool Prefix(
            MapEventParty playerMapEventParty,
            MBList<MapEventParty> playerTeamMapEventParties,
            ref Ship __result)
        {
            // Reset battle state trackers
            UsingLordShip = false;
            CurrentLord = null;
            FormationsProcessed = 0;
            FormationsSkipped = 0;

            try
            {
                if (!EnlistedActivation.EnsureActive() || !CampaignSafetyGuard.IsCampaignReady)
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true;
                }

                // Log naval battle start with context
                var lordName = enlistment.CurrentLord?.Name?.ToString() ?? "Unknown";
                var playerTroopCount = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
                var tier = enlistment.EnlistmentTier;
                
                ModLogger.Info(LogCategory, 
                    $"=== NAVAL BATTLE START === Enlisted under {lordName}, Tier {tier}, Party size: {playerTroopCount}");

                // Check if player has their own ships
                var playerShips = playerMapEventParty?.Ships;
                if (playerShips is { Count: > 0 })
                {
                    ModLogger.Info(LogCategory, $"Player owns {playerShips.Count} ship(s) - using player's fleet");
                    return true;
                }

                // Check if team already has ships
                var teamShipCount = 0;
                var teamPartyCount = 0;
                if (playerTeamMapEventParties != null)
                {
                    foreach (var party in playerTeamMapEventParties)
                    {
                        teamPartyCount++;
                        teamShipCount += party?.Ships?.Count ?? 0;
                    }
                }

                ModLogger.Debug(LogCategory, $"Team composition: {teamPartyCount} parties, {teamShipCount} ships total");

                if (teamShipCount > 0)
                {
                    ModLogger.Debug(LogCategory, "Team has ships available - using original assignment");
                    return true;
                }

                // Enlisted player with no ships - get ship from lord's fleet
                var lordParty = enlistment.CurrentLord?.PartyBelongedTo;

                if (lordParty?.Ships == null || lordParty.Ships.Count == 0)
                {
                    ModLogger.Error(LogCategory, 
                        $"CRITICAL: Lord {lordName} has no ships! Player cannot join naval battle safely.");
                    return true;
                }

                // Find best ship from lord's fleet (capacity-aware)
                var lordShips = lordParty.Ships;
                var fittingShips = lordShips.Where(s => s.TotalCrewCapacity >= playerTroopCount).ToList();
                var bestShip = fittingShips.Any()
                    ? fittingShips.OrderByDescending(s => s.HitPoints).First()
                    : lordShips.OrderByDescending(s => s.TotalCrewCapacity).First();

                __result = bestShip;
                UsingLordShip = true;
                CurrentLord = enlistment.CurrentLord;

                // Detailed ship assignment log
                var shipName = bestShip.ShipHull?.Name?.ToString() ?? "vessel";
                var shipHealth = bestShip.HitPoints;
                var shipCapacity = bestShip.TotalCrewCapacity;
                var capacityFit = playerTroopCount <= shipCapacity ? "OK" : "OVERFLOW";
                
                ModLogger.Info(LogCategory,
                    $"Ship assigned: {shipName} (HP:{shipHealth}, Capacity:{shipCapacity}/{playerTroopCount} [{capacityFit}]) " +
                    $"Lord fleet: {lordShips.Count} ships, Fitting: {fittingShips.Count}");

                return false;
            }
            catch (Exception ex)
            {
                // Stable support code + full exception detail (stack trace written once per unique exception).
                ModLogger.Caught(LogCategory, "GetSuitablePlayerShip threw an exception", ex);
                return true;
            }
        }
    }

    /// <summary>
    /// Patch 2: GetOrderedCaptainsForPlayerTeamShips - assigns lord as captain (not player).
    /// This prevents the null reference crash when the ship doesn't belong to any party
    /// in playerTeamMapEventParties.
    /// </summary>
    [HarmonyPatch]
    public static class NavalGetOrderedCaptainsPatch
    {
        private const string LogCategory = "Naval";

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var navalDlcType = AccessTools.TypeByName("NavalDLC.GameComponents.NavalDLCShipDeploymentModel");
            if (navalDlcType == null)
            {
                return null;
            }

            var method = AccessTools.Method(navalDlcType, "GetOrderedCaptainsForPlayerTeamShips");
            if (method != null)
            {
                ModLogger.Info(LogCategory, "Naval captain assignment fix registered");
            }

            return method;
        }

        [HarmonyPrefix]
        public static bool Prefix(
            MBReadOnlyList<MapEventParty> playerTeamMapEventParties,
            MBReadOnlyList<IShipOrigin> playerTeamShips,
            ref List<string> playerTeamCaptainsByPriority)
        {
            try
            {
                if (!NavalBattleShipAssignmentPatch.UsingLordShip)
                {
                    return true;
                }

                if (!EnlistedActivation.EnsureActive() || !CampaignSafetyGuard.IsCampaignReady)
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true;
                }

                var lordHero = NavalBattleShipAssignmentPatch.CurrentLord;
                var shipCount = playerTeamShips?.Count ?? 0;
                var partyCount = playerTeamMapEventParties?.Count ?? 0;
                
                ModLogger.Debug(LogCategory, 
                    $"Captain assignment: {shipCount} ships, {partyCount} parties, Lord={lordHero?.Name}");

                // Build captain list - LORD is captain, player is crew
                playerTeamCaptainsByPriority = new List<string>();
                var ownedShips = 0;
                var borrowedShips = 0;

                if (playerTeamShips != null)
                {
                    foreach (var ship in playerTeamShips)
                    {
                        // Try to find owner party
                        MapEventParty ownerParty = null;
                        if (playerTeamMapEventParties != null)
                        {
                            foreach (var party in playerTeamMapEventParties)
                            {
                                if (party?.Ships != null && party.Ships.Contains(ship))
                                {
                                    ownerParty = party;
                                    break;
                                }
                            }
                        }

                        if (ownerParty != null)
                        {
                            // Ship belongs to a party - use their leader
                            var leader = ownerParty.Party?.LeaderHero;
                            playerTeamCaptainsByPriority.Add(leader?.StringId ?? string.Empty);
                            ownedShips++;
                        }
                        else
                        {
                            // Borrowed ship - lord is captain, player serves as crew
                            borrowedShips++;
                            if (lordHero != null)
                            {
                                playerTeamCaptainsByPriority.Add(lordHero.StringId);
                            }
                            else
                            {
                                playerTeamCaptainsByPriority.Add(string.Empty);
                                ModLogger.Warn(LogCategory, "Borrowed ship has no lord captain assigned!");
                            }
                        }
                    }
                }

                ModLogger.Info(LogCategory, 
                    $"Captains assigned: {ownedShips} owned ships, {borrowedShips} borrowed (player as crew)");

                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "GetOrderedCaptainsForPlayerTeamShips prefix threw an exception", ex);
                return true;
            }
        }
    }

    /// <summary>
    /// Patch 3: AllocateAndDeployInitialTroopsOfPlayerTeam - handles the case where
    /// player team's MissionShip is null (because our borrowed ship wasn't properly spawned).
    /// 
    /// Instead of crashing, we find any valid friendly MissionShip and spawn the player there.
    /// </summary>
    [HarmonyPatch]
    public static class NavalAllocateTroopsPatch
    {
        private const string LogCategory = "Naval";

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var teamSideType = AccessTools.TypeByName("NavalDLC.Missions.MissionLogics.ShipAgentSpawnLogicTeamSide");
            if (teamSideType == null)
            {
                return null;
            }

            var method = AccessTools.Method(teamSideType, "AllocateAndDeployInitialTroopsOfPlayerTeam");
            if (method != null)
            {
                ModLogger.Info(LogCategory, "Naval troop allocation fix registered");
            }

            return method;
        }

        /// <summary>
        /// Prefix that checks if MissionShip will be null and handles it gracefully.
        /// We use reflection to access Naval DLC internals since we can't reference them directly.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(object __instance)
        {
            try
            {
                if (!NavalBattleShipAssignmentPatch.UsingLordShip)
                {
                    return true; // Let original handle non-enlisted cases
                }

                if (!EnlistedActivation.EnsureActive() || !CampaignSafetyGuard.IsCampaignReady)
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true;
                }

                // Get _shipsLogic field via reflection
                var instanceType = __instance.GetType();
                var shipsLogicField = AccessTools.Field(instanceType, "_shipsLogic");
                var agentsLogicField = AccessTools.Field(instanceType, "_agentsLogic");

                if (shipsLogicField == null || agentsLogicField == null)
                {
                    ModLogger.Error(LogCategory, "Cannot find Naval DLC internal fields");
                    return true;
                }

                var shipsLogic = shipsLogicField.GetValue(__instance);
                var agentsLogic = agentsLogicField.GetValue(__instance);

                if (shipsLogic == null || agentsLogic == null)
                {
                    return true;
                }

                // Check if player team's ship assignment has a valid MissionShip
                var getShipAssignmentMethod = AccessTools.Method(shipsLogic.GetType(), "GetShipAssignment");
                if (getShipAssignmentMethod == null)
                {
                    return true;
                }

                // TeamSideEnum 0 = Player, FormationClass 0 = Infantry
                var shipAssignment = getShipAssignmentMethod.Invoke(shipsLogic, [0, 0]);
                if (shipAssignment == null)
                {
                    return true;
                }

                // Check if MissionShip is null
                var missionShipProp = AccessTools.Property(shipAssignment.GetType(), "MissionShip");
                var missionShip = missionShipProp?.GetValue(shipAssignment);

                if (missionShip != null)
                {
                    // Ship exists, let original method run
                    ModLogger.Debug(LogCategory, "Player team has valid MissionShip");
                    return true;
                }

                // MissionShip is null - this is the crash condition
                // Try to find ANY friendly ship for the player to spawn on
                ModLogger.Info(LogCategory, "Player team MissionShip is null - finding alternate spawn");

                var allShipsProp = AccessTools.Property(shipsLogic.GetType(), "AllShips");
                var allShips = allShipsProp?.GetValue(shipsLogic) as System.Collections.IList;

                if (allShips == null || allShips.Count == 0)
                {
                    ModLogger.Error(LogCategory, "No ships available in mission");
                    return false; // Skip original to prevent crash
                }

                // === DIAGNOSTIC LOGGING (only when MissionShip is null - crash condition) ===
                ModLogger.Warn(LogCategory, "=== NAVAL MISSION DIAGNOSTIC (null MissionShip detected) ===");
                ModLogger.Warn(LogCategory, $"Ships in mission: {allShips.Count}, Player team: {Mission.Current?.PlayerTeam?.Side}");
                
                // Compact team info
                if (Mission.Current?.Teams != null)
                {
                    var teamInfo = string.Join(", ", Mission.Current.Teams.Select(t => 
                        $"{t.Side}[{t.TeamIndex}]{(t.IsPlayerTeam ? "*" : "")}"));
                    ModLogger.Warn(LogCategory, $"Teams: {teamInfo}");
                }
                
                // Compact ship info
                var shipInfoList = new List<string>();
                var shipIdx = 0;
                var friendlyCount = 0;
                var enemyCount = 0;
                foreach (var ship in allShips)
                {
                    var shipTeamProp = AccessTools.Property(ship.GetType(), "Team");
                    var shipTeam = shipTeamProp?.GetValue(ship) as Team;
                    var shipFormationProp = AccessTools.Property(ship.GetType(), "Formation");
                    var shipFormation = shipFormationProp?.GetValue(ship) as Formation;
                    
                    var isFriendly = shipTeam?.Side == Mission.Current?.PlayerTeam?.Side;
                    if (isFriendly)
                    {
                        friendlyCount++;
                    }
                    else
                    {
                        enemyCount++;
                    }
                    
                    shipInfoList.Add($"[{shipIdx}]{shipTeam?.Side}/{shipFormation?.FormationIndex}");
                    shipIdx++;
                }
                ModLogger.Warn(LogCategory, $"Ship breakdown: {friendlyCount} friendly, {enemyCount} enemy");
                ModLogger.Debug(LogCategory, $"Ship details: {string.Join(", ", shipInfoList)}");

                // Find a friendly ship (check Team.Side)
                object friendlyShip = null;
                foreach (var ship in allShips)
                {
                    var teamProp = AccessTools.Property(ship.GetType(), "Team");
                    if (teamProp?.GetValue(ship) is not Team team)
                    {
                        continue;
                    }
                    
                    // Player side is typically BattleSideEnum 0 or 1 (Defender/Attacker)
                    // We want ships on the same side as the player
                    if (team.Side == Mission.Current?.PlayerTeam?.Side)
                    {
                        friendlyShip = ship;
                        ModLogger.Info(LogCategory, $"Selected friendly ship on {team.Side} side");
                        break;
                    }
                }

                if (friendlyShip == null)
                {
                    // Fallback: use first ship
                    friendlyShip = allShips[0];
                    ModLogger.Warn(LogCategory, "Using first available ship as fallback");
                }

                // The player's troop origin wasn't registered because they have no ships.
                // Create PartyAgentOrigin for the player.
                var playerParty = MobileParty.MainParty?.Party;
                var playerCharacter = CharacterObject.PlayerCharacter;
                
                if (playerParty == null || playerCharacter == null)
                {
                    ModLogger.Error(LogCategory, "Cannot create player origin - party or character is null");
                    return false;
                }

                var playerOrigin = new PartyAgentOrigin(playerParty, playerCharacter);
                ModLogger.Info(LogCategory, $"Created player origin for {playerCharacter.Name}");

                // Get the MissionShip type for the method lookup
                var missionShipType = friendlyShip.GetType();
                
                // AddReservedTroopToShip on NavalAgentsLogic handles team lookup internally
                var addReservedTroopMethod = AccessTools.Method(agentsLogic.GetType(), "AddReservedTroopToShip",
                    [typeof(IAgentOriginBase), missionShipType]);
                
                if (addReservedTroopMethod != null)
                {
                    var result = addReservedTroopMethod.Invoke(agentsLogic, [playerOrigin, friendlyShip]);
                    ModLogger.Info(LogCategory, $"AddReservedTroopToShip result: {result}");
                }
                else
                {
                    ModLogger.Error(LogCategory, "Could not find AddReservedTroopToShip method");
                    return false;
                }

                // We've added the player to a friendly ship. Now we need to call the methods
                // that the original would call - AssignTroops, InitializeReinforcementTimers, 
                // and CheckSpawnNextBatch - to properly set up the mission state.
                
                // Get TeamSide for method calls (null = Player team = TeamSideEnum 0)
                var teamSideProp = AccessTools.Property(instanceType, "TeamSide");
                var teamSideValue = teamSideProp?.GetValue(__instance);
                var teamSideEnumType = AccessTools.TypeByName("NavalDLC.Missions.TeamSideEnum");
                
                // If TeamSide is null, use 0 (Player)
                if (teamSideValue == null && teamSideEnumType != null)
                {
                    teamSideValue = Enum.ToObject(teamSideEnumType, 0);
                    ModLogger.Debug(LogCategory, "Using TeamSideEnum.Player (0)");
                }
                
                if (teamSideValue != null)
                {
                    // Call AssignTroops to set up troop assignments for all ships
                    var assignTroopsMethod = AccessTools.Method(agentsLogic.GetType(), "AssignTroops",
                        [teamSideValue.GetType(), typeof(bool)]);
                    if (assignTroopsMethod != null)
                    {
                        assignTroopsMethod.Invoke(agentsLogic, [teamSideValue, false]);
                        ModLogger.Debug(LogCategory, "AssignTroops called");
                    }
                    
                    // Initialize reinforcement timers
                    var initTimersMethod = AccessTools.Method(agentsLogic.GetType(), "InitializeReinforcementTimers",
                        [teamSideValue.GetType(), typeof(bool), typeof(bool)]);
                    if (initTimersMethod != null)
                    {
                        initTimersMethod.Invoke(agentsLogic, [teamSideValue, true, true]);
                        ModLogger.Debug(LogCategory, "InitializeReinforcementTimers called");
                    }
                }
                
                // Call CheckSpawnNextBatch to spawn the initial batch of troops
                var checkSpawnMethod = AccessTools.DeclaredMethod(instanceType, "CheckSpawnNextBatch");
                if (checkSpawnMethod != null)
                {
                    checkSpawnMethod.Invoke(__instance, null);
                    ModLogger.Debug(LogCategory, "CheckSpawnNextBatch called");
                }
                
                ModLogger.Info(LogCategory, 
                    $"=== NAVAL DEPLOYMENT COMPLETE === Player spawned as crew on friendly ship " +
                    $"(Formations: {NavalBattleShipAssignmentPatch.FormationsProcessed} processed, " +
                    $"{NavalBattleShipAssignmentPatch.FormationsSkipped} skipped for no ship)");
                
                return false; // Skip original - it would crash on null MissionShip
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, 
                    $"AllocateTroops EXCEPTION: {ex.Message}\n" +
                    $"State: UsingLordShip={NavalBattleShipAssignmentPatch.UsingLordShip}, " +
                    $"Lord={NavalBattleShipAssignmentPatch.CurrentLord?.Name}\n" +
                    $"Stack: {ex.StackTrace}");
                return true; // Fall back to original on error
            }
        }
    }

    /// <summary>
    /// Patch 5: Fixes crash when adding naval behaviors to formations without ships.
    /// We patch OnUnitAddedToFormationForTheFirstTime to check for a ship BEFORE creating
    /// BehaviorNavalEngageCorrespondingEnemy (patching the constructor doesn't work because
    /// the object is already allocated when the prefix runs).
    /// </summary>
    /// <summary>
    /// Patch 5: Fixes crash in NavalTeamAgents.OnShipRemoved during mission cleanup.
    /// When the mission ends and ships are removed, the cleanup code can crash on agents
    /// that are in an invalid state (null Team, already removed, etc.). This patch
    /// wraps the dangerous operations and skips problematic agents.
    /// </summary>
    [HarmonyPatch]
    public static class NavalOnShipRemovedPatch
    {
        private const string LogCategory = "Naval";

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var navalTeamAgentsType = AccessTools.TypeByName("NavalDLC.Missions.MissionLogics.NavalTeamAgents");
            if (navalTeamAgentsType == null)
            {
                return null;
            }

            var method = AccessTools.Method(navalTeamAgentsType, "OnShipRemoved");
            if (method != null)
            {
                ModLogger.Info(LogCategory, "Naval OnShipRemoved safety fix registered");
            }

            return method;
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, object ship)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive() || !CampaignSafetyGuard.IsCampaignReady)
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true;
                }

                // Only intercept if we're using the lord's ship (our special case)
                if (!NavalBattleShipAssignmentPatch.UsingLordShip)
                {
                    return true;
                }

                // Get the ship's agents via reflection and handle cleanup manually
                var instanceType = __instance.GetType();
                
                // TryGetShipAgents(ship, out NavalShipAgents shipAgents)
                var missionShipType = ship.GetType();
                var navalShipAgentsType = AccessTools.TypeByName("NavalDLC.Missions.MissionLogics.NavalShipAgents");
                
                if (navalShipAgentsType == null)
                {
                    return true;
                }
                
                var tryGetShipAgentsMethod = AccessTools.Method(instanceType, "TryGetShipAgents",
                    [missionShipType, navalShipAgentsType.MakeByRefType()]);
                
                if (tryGetShipAgentsMethod == null)
                {
                    ModLogger.Error(LogCategory, "Could not find TryGetShipAgents method");
                    return true;
                }

                object[] parameters = [ship, null]; // Array for reflection with out parameter
                var hasShipAgents = (bool)tryGetShipAgentsMethod.Invoke(__instance, parameters);
                
                if (!hasShipAgents)
                {
                    return false; // No agents to clean up
                }

                var shipAgents = parameters[1];
                
                // Get AgentsLogic for state checks
                var agentsLogicProp = AccessTools.Property(instanceType, "AgentsLogic");
                var agentsLogic = agentsLogicProp?.GetValue(__instance);
                
                var isDeploymentMode = false;
                var isMissionEnding = true; // Default to true for safety
                
                if (agentsLogic != null)
                {
                    var isDeploymentModeProp = AccessTools.Property(agentsLogic.GetType(), "IsDeploymentMode");
                    var isMissionEndingProp = AccessTools.Property(agentsLogic.GetType(), "IsMissionEnding");
                    isDeploymentMode = (bool)(isDeploymentModeProp?.GetValue(agentsLogic) ?? false);
                    isMissionEnding = (bool)(isMissionEndingProp?.GetValue(agentsLogic) ?? true);
                }

                // Get ActiveAgents list
                var activeAgentsProp = AccessTools.Property(shipAgents.GetType(), "ActiveAgents");
                var activeAgents = activeAgentsProp?.GetValue(shipAgents) as System.Collections.IList;

                // Get methods we need
                var removeAgentAuxMethod = AccessTools.Method(instanceType, "RemoveAgentAux");
                var removeTroopOriginAuxMethod = AccessTools.Method(instanceType, "RemoveTroopOriginAux");
                var unassignAgentAuxMethod = AccessTools.Method(instanceType, "UnassignAgentAux");
                
                // Get reserved troops handling
                var reservedTroopsCountProp = AccessTools.Property(shipAgents.GetType(), "ReservedTroopsCount");
                // DequeueReservedTroop has 2 overloads - we need the one that takes only NavalShipAgents
                var dequeueReservedTroopMethod = AccessTools.Method(instanceType, "DequeueReservedTroop",
                    [navalShipAgentsType]);
                var enqueueUnassignedTroopMethod = AccessTools.Method(instanceType, "EnqueueUnassignedTroop");

                ModLogger.Debug(LogCategory, 
                    $"OnShipRemoved: Handling cleanup (DeploymentMode={isDeploymentMode}, MissionEnding={isMissionEnding}, ActiveAgents={activeAgents?.Count ?? 0})");

                if (isDeploymentMode && !isMissionEnding)
                {
                    // Deployment mode cleanup
                    while (activeAgents is { Count: > 0 })
                    {
                        try
                        {
                            if (activeAgents[activeAgents.Count - 1] is Agent agent && unassignAgentAuxMethod != null)
                            {
                                unassignAgentAuxMethod.Invoke(__instance, [shipAgents, agent]);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Debug(LogCategory, $"Unassign failed (safe to ignore): {ex.Message}");
                            // Force remove from list to prevent infinite loop
                            if (activeAgents.Count > 0)
                            {
                                activeAgents.RemoveAt(activeAgents.Count - 1);
                            }
                        }
                    }
                    
                    // Handle reserved troops
                    if (dequeueReservedTroopMethod != null && enqueueUnassignedTroopMethod != null)
                    {
                        var count = (int)(reservedTroopsCountProp?.GetValue(shipAgents) ?? 0);
                        while (count > 0)
                        {
                            try
                            {
                                var troop = dequeueReservedTroopMethod.Invoke(__instance, [shipAgents]);
                                enqueueUnassignedTroopMethod.Invoke(__instance, [troop]);
                                count = (int)(reservedTroopsCountProp?.GetValue(shipAgents) ?? 0);
                            }
                            catch
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // Mission ending cleanup - be extra careful
                    while (activeAgents is { Count: > 0 })
                    {
                        Agent agent = null;
                        try
                        {
                            if (activeAgents[activeAgents.Count - 1] is not Agent extractedAgent)
                            {
                                activeAgents.RemoveAt(activeAgents.Count - 1);
                                continue;
                            }
                            agent = extractedAgent;

                            // Remove from tracking
                            if (removeAgentAuxMethod != null)
                            {
                                removeAgentAuxMethod.Invoke(__instance, [agent, shipAgents]);
                            }
                            
                            // Remove troop origin (with null check)
                            if (removeTroopOriginAuxMethod != null && agent.Origin != null)
                            {
                                removeTroopOriginAuxMethod.Invoke(__instance, new object[] { agent.Origin });
                            }
                            
                            // FadeOut non-main agents (with safety checks)
                            if (agent != Agent.Main && agent.IsActive() && agent.Team != null)
                            {
                                agent.FadeOut(true, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModLogger.Debug(LogCategory, 
                                $"Agent cleanup failed (IsMain={agent?.IsMainAgent}, Team={agent?.Team != null}): {ex.Message}");
                            // Force remove from list to prevent infinite loop
                            if (activeAgents.Count > 0)
                            {
                                activeAgents.RemoveAt(activeAgents.Count - 1);
                            }
                        }
                    }
                    
                    // Handle reserved troops
                    if (dequeueReservedTroopMethod != null && removeTroopOriginAuxMethod != null)
                    {
                        var count = (int)(reservedTroopsCountProp?.GetValue(shipAgents) ?? 0);
                        while (count > 0)
                        {
                            try
                            {
                                var troop = dequeueReservedTroopMethod.Invoke(__instance, [shipAgents]);
                                var originProp = AccessTools.Property(troop.GetType(), "Origin");
                                var origin = originProp?.GetValue(troop);
                                if (origin != null)
                                {
                                    removeTroopOriginAuxMethod.Invoke(__instance, [origin]);
                                }
                                count = (int)(reservedTroopsCountProp?.GetValue(shipAgents) ?? 0);
                            }
                            catch
                            {
                                break;
                            }
                        }
                    }
                }

                // Remove ship from tracking list
                var allShipAgentsField = AccessTools.Field(instanceType, "_allShipAgents");
                if (allShipAgentsField?.GetValue(__instance) is not System.Collections.IList allShipAgents)
                {
                    ModLogger.Debug(LogCategory, "OnShipRemoved: Cleanup complete");
                    return false;
                }

                for (var i = allShipAgents.Count - 1; i >= 0; i--)
                {
                    var shipAgentsEntry = allShipAgents[i];
                    var shipProp = AccessTools.Property(shipAgentsEntry.GetType(), "Ship");
                    var entryShip = shipProp?.GetValue(shipAgentsEntry);
                    if (entryShip == ship)
                    {
                        allShipAgents.RemoveAt(i);
                    }
                }

                ModLogger.Debug(LogCategory, "OnShipRemoved: Cleanup complete");
                return false; // Skip original - we handled it
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "OnShipRemoved prefix threw an exception", ex);
                return true; // Fall back to original on error
            }
        }
    }

    /// <summary>
    /// Patch 6: Fixes crash in BattleObserverMissionLogic.OnAgentRemoved when agent.Team is null.
    /// The base game checks for Team.Invalid but NOT for null. During naval mission cleanup,
    /// agents can have their Team set to null, causing a NullReferenceException.
    /// </summary>
    [HarmonyPatch(typeof(BattleObserverMissionLogic), "OnAgentRemoved")]
    public static class BattleObserverOnAgentRemovedPatch
    {
        private const string LogCategory = "Naval";

        [HarmonyPrefix]
        public static bool Prefix(Agent affectedAgent)
        {
            try
            {
                if (affectedAgent == null || !affectedAgent.IsHuman)
                {
                    return false;
                }

                if (affectedAgent.Team == null)
                {
                    ModLogger.Debug(LogCategory, 
                        $"BattleObserver: Skipping agent with null Team (IsMainAgent: {affectedAgent.IsMainAgent})");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "BattleObserverMissionLogic.OnAgentRemoved prefix threw an exception", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Patch 7: Fixes crash in NavalAgentsLogic.OnAgentRemoved when agent.Team is null.
    /// </summary>
    [HarmonyPatch]
    public static class NavalOnAgentRemovedPatch
    {
        private const string LogCategory = "Naval";

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var navalAgentsLogicType = AccessTools.TypeByName("NavalDLC.Missions.MissionLogics.NavalAgentsLogic");
            if (navalAgentsLogicType == null)
            {
                return null;
            }

            var method = AccessTools.Method(navalAgentsLogicType, "OnAgentRemoved");
            if (method != null)
            {
                ModLogger.Info(LogCategory, "Naval OnAgentRemoved null-team fix registered");
            }

            return method;
        }

        [HarmonyPrefix]
        public static bool Prefix(Agent affectedAgent)
        {
            try
            {
                if (affectedAgent == null || !affectedAgent.IsHuman)
                {
                    return false;
                }

                if (affectedAgent.Team == null)
                {
                    ModLogger.Debug(LogCategory, 
                        $"NavalAgents: Skipping agent with null Team (IsMainAgent: {affectedAgent.IsMainAgent})");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "NavalAgentsLogic.OnAgentRemoved prefix threw an exception", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Patch 6: Fixes crash when adding naval behaviors to formations without ships.
    /// We patch OnUnitAddedToFormationForTheFirstTime to check for a ship BEFORE creating
    /// BehaviorNavalEngageCorrespondingEnemy (patching the constructor doesn't work because
    /// the object is already allocated when the prefix runs).
    /// </summary>
    [HarmonyPatch]
    public static class NavalTeamAiFormationPatch
    {
        private const string LogCategory = "Naval";

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            var teamAiType = AccessTools.TypeByName("NavalDLC.Missions.AI.TeamAI.TeamAINavalComponent");
            if (teamAiType == null)
            {
                return null;
            }

            var method = AccessTools.Method(teamAiType, "OnUnitAddedToFormationForTheFirstTime");
            if (method != null)
            {
                ModLogger.Info(LogCategory, "Naval TeamAI formation patch registered");
            }

            return method;
        }

        [HarmonyPrefix]
        public static bool Prefix(object __instance, Formation formation)
        {
            try
            {
                if (formation == null)
                {
                    ModLogger.Debug(LogCategory, "FormationPatch: null formation received");
                    return true;
                }

                // Track all formations processed
                NavalBattleShipAssignmentPatch.FormationsProcessed++;
                var teamSide = formation.Team?.Side.ToString() ?? "Unknown";
                var formationClass = formation.FormationIndex.ToString();

                // Check if behavior already exists (early exit from original)
                var removeConnectionType = AccessTools.TypeByName("NavalDLC.Missions.AI.Behaviors.BehaviorNavalRemoveConnection");
                if (removeConnectionType != null)
                {
                    var getBehaviorMethod = AccessTools.Method(typeof(FormationAI), "GetBehavior")
                        ?.MakeGenericMethod(removeConnectionType);
                    if (getBehaviorMethod != null)
                    {
                        var existing = getBehaviorMethod.Invoke(formation.AI, null);
                        if (existing != null)
                        {
                            ModLogger.Debug(LogCategory, 
                                $"Formation {formationClass} ({teamSide}): already has behaviors");
                            return false;
                        }
                    }
                }

                // Check if this formation has a ship assigned
                var navalShipsLogicType = AccessTools.TypeByName("NavalDLC.Missions.MissionLogics.NavalShipsLogic");
                var missionShipType = AccessTools.TypeByName("NavalDLC.Missions.Objects.MissionShip");
                
                if (navalShipsLogicType == null)
                {
                    ModLogger.Error(LogCategory, "REFLECTION FAIL: NavalShipsLogic type not found");
                    return true;
                }
                
                if (missionShipType == null)
                {
                    ModLogger.Error(LogCategory, "REFLECTION FAIL: MissionShip type not found");
                    return true;
                }

                var getMissionBehaviorMethod = AccessTools.Method(typeof(Mission), "GetMissionBehavior")
                    ?.MakeGenericMethod(navalShipsLogicType);
                
                if (getMissionBehaviorMethod == null)
                {
                    ModLogger.Error(LogCategory, "REFLECTION FAIL: GetMissionBehavior method not found");
                    return true;
                }
                
                var shipsLogic = getMissionBehaviorMethod.Invoke(Mission.Current, null);
                if (shipsLogic == null)
                {
                    ModLogger.Error(LogCategory, "NavalShipsLogic instance is null - cannot check ship assignment");
                    return true;
                }

                var getShipMethod = AccessTools.Method(shipsLogic.GetType(), "GetShip",
                    [typeof(Formation), missionShipType.MakeByRefType()]);
                
                if (getShipMethod == null)
                {
                    ModLogger.Error(LogCategory, "REFLECTION FAIL: GetShip(Formation, out MissionShip) not found");
                    return true;
                }

                object[] parameters = [formation, null]; // Array for reflection with out parameter
                var hasShip = (bool)getShipMethod.Invoke(shipsLogic, parameters);
                var ship = parameters[1];

                if (!hasShip || ship == null)
                {
                    // No ship for this formation - skip adding behaviors to prevent crash
                    NavalBattleShipAssignmentPatch.FormationsSkipped++;
                    
                    ModLogger.Warn(LogCategory, 
                        $"Formation {formationClass} ({teamSide}): NO SHIP - skipping AI behaviors " +
                        $"(total skipped: {NavalBattleShipAssignmentPatch.FormationsSkipped})");
                    
                    // Just call ForceCalculateCaches and return - don't add crash-prone behaviors
                    formation.ForceCalculateCaches();
                    return false;
                }

                ModLogger.Debug(LogCategory, 
                    $"Formation {formationClass} ({teamSide}): ship assigned - adding behaviors");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Error(LogCategory, 
                    $"NavalTeamAI EXCEPTION for Formation {formation?.FormationIndex}: {ex.Message}\nStack: {ex.StackTrace}");
                return true;
            }
        }
    }
}
