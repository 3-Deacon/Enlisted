using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameComponents;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using EnlistedEncounterBehavior = Enlisted.Features.Combat.Behaviors.EnlistedEncounterBehavior;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Patches the native GetGenericStateMenu() to return our custom enlisted menu when appropriate.
    ///
    /// Root cause: When enlisted, the native system thinks player should be in "army_wait" or
    /// "army_wait_at_settlement" because mainParty.AttachedTo != null. Various native systems
    /// call GetGenericStateMenu() and switch menus accordingly, causing unwanted menu switches
    /// even when the player just wants to stay in the enlisted menu.
    ///
    /// Fix: When player is enlisted and hasn't explicitly chosen to visit a settlement,
    /// override settlement/army menus to stay on the enlisted menu instead.
    /// </summary>
    [HarmonyPatch(typeof(DefaultEncounterGameMenuModel), nameof(DefaultEncounterGameMenuModel.GetGenericStateMenu))]
    public class GenericStateMenuPatch
    {
        /// <summary>
        /// Postfix that overrides the result when player is enlisted.
        /// Returns "enlisted_status" for non-combat situations and "enlisted_battle_wait" for combat.
        /// </summary>
        [HarmonyPostfix]
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
        [SuppressMessage("CodeQuality", "IDE0051", Justification = "Called by Harmony via reflection")]
        [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called by Harmony via reflection")]
        public static void Postfix(ref string __result)
        {
            try
            {
                // Check activation gate first - mod may be disabled
                if (!EnlistedActivation.EnsureActive())
                {
                    return;
                }

                // Check if actually enlisted
                var enlistment = EnlistmentBehavior.Instance;
                var isEnlisted = enlistment?.IsEnlisted == true;

                if (!isEnlisted)
                {
                    return;
                }
                
                // DIAGNOSTIC: Log what menu the native system wants to show
                var originalResult = __result;
                var mainParty = TaleWorlds.CampaignSystem.Party.MobileParty.MainParty;
                var partyActive = mainParty?.IsActive ?? false;
                var partyVisible = mainParty?.IsVisible ?? false;
                var inMapEvent = mainParty?.Party?.MapEvent != null;
                var hasEncounter = PlayerEncounter.Current != null;
                
                // Only log when result is meaningful (not null/empty)
                if (!string.IsNullOrEmpty(__result))
                {
                    ModLogger.Info("MenuGuard", 
                        $"NATIVE MENU REQUEST: '{__result}' | Active={partyActive}, Visible={partyVisible}, " +
                        $"InMapEvent={inMapEvent}, HasEncounter={hasEncounter}");
                }

                // If native wants to push the encounter menu after the battle is already over, suppress it.
                // This prevents post-victory encounter loops (e.g. "Wait in reserve" flashing repeatedly).
                var mainMapEvent = mainParty?.MapEvent;
                var mainBattleOver = mainMapEvent != null && (mainMapEvent.HasWinner || mainMapEvent.IsFinalized);
                var suppressBattleOverEncounter = __result == "encounter" && mainBattleOver;

                // If native wants to push siege strategy menus, only suppress them when the siege state is stale.
                // During a real active siege, the player must be able to open these menus to engage with the siege.
                var isSiegeMenu = __result == "menu_siege_strategies" || __result == "encounter_interrupted_siege_preparations";
                var camp = mainParty?.BesiegerCamp;
                var siegeEvent = camp?.SiegeEvent;
                var settlement = siegeEvent?.BesiegedSettlement;
                var isStaleSiege =
                    camp != null &&
                    (siegeEvent == null ||
                     siegeEvent.ReadyToBeRemoved ||
                     settlement == null ||
                     settlement.Party?.SiegeEvent != siegeEvent ||
                     (camp.MapFaction != null && settlement.MapFaction == camp.MapFaction) ||
                     (siegeEvent.BesiegerCamp != null && siegeEvent.BesiegerCamp != camp));

                // If player is waiting in reserve during battle, use the battle wait menu
                if (EnlistedEncounterBehavior.IsWaitingInReserve)
                {
                    var lordParty = enlistment?.CurrentLord?.PartyBelongedTo;
                    var mapEvent = lordParty?.Party?.MapEvent;

                    // Battle is over when: no MapEvent, OR MapEvent has a winner
                    var battleOver = mapEvent == null || mapEvent.HasWinner;

                    if (!battleOver)
                    {
                        // Battle is ongoing - use battle wait menu
                        if (__result == "army_wait" || __result == "army_wait_at_settlement" || __result == "encounter")
                        {
                            ModLogger.Info("MenuGuard", $"MENU OVERRIDE: '{__result}' -> 'enlisted_battle_wait' (player in reserve, battle ongoing)");
                            __result = "enlisted_battle_wait";
                        }
                        return;
                    }

                    // Battle is over - clear stale reserve flag and continue to normal enlisted menu logic
                    ModLogger.Info("MenuGuard", "Clearing stale reserve flag - battle is over");
                    EnlistedEncounterBehavior.ClearReserveState();
                }

                // Check if player has explicitly clicked "Visit Settlement" from the enlisted menu.
                // If so, don't override - let them use the native settlement menus.
                // We can't rely solely on PlayerEncounter.InsideSettlement because it can be true
                // even when the lord just enters a settlement (not when player clicks Visit).
                var hasExplicitlyVisited = EnlistedMenuBehavior.HasExplicitlyVisitedSettlement;

                if (hasExplicitlyVisited)
                {
                    // Player has explicitly clicked "Visit Settlement" - don't override
                    ModLogger.Info("MenuGuard", $"ALLOWING native menu '{__result}' - player explicitly visited settlement");
                    return;
                }

                // Siege menus: Allow native siege menu to show for army members during active sieges.
                // The native menu will show appropriate options based on role (army member vs commander).
                // Only suppress siege menus when the siege is stale (after victory, etc.) to prevent menu loops.
                if (isSiegeMenu && isStaleSiege)
                {
                    var original = __result;
                    __result = "enlisted_status";
                    ModLogger.Info("Menu", $"GenericStateMenuPatch: {original} -> {__result} (stale siege - cleaning up)");
                    enlistment?.CleanupPostEncounterStateFromPatch($"GenericStateMenuPatch({original})");
                    return;
                }

                if (suppressBattleOverEncounter)
                {
                    var original = __result;
                    __result = "enlisted_status";
                    ModLogger.Info("Menu", $"GenericStateMenuPatch: {original} -> {__result} (suppressing native menu for enlisted)");

                    // Best-effort cleanup so the engine stops reselecting these menus on subsequent ticks.
                    enlistment?.CleanupPostEncounterStateFromPatch($"GenericStateMenuPatch({original})");
                    return;
                }

                // Don't check for settlement encounters here - we want to show the enlisted menu
                // when paused, even at settlements. Player must explicitly click "Visit Settlement"
                // to get the town/castle menu (handled by hasExplicitlyVisited check above).

                // Check if lord is in a battle or siege - if so, don't override combat-related menus
                var lordPartyCheck = enlistment?.CurrentLord?.PartyBelongedTo;
                var lordInBattle = lordPartyCheck?.Party?.MapEvent != null;
                
                // Check siege status - need to check BOTH the lord AND the army leader (if in army)
                var lordInSiege = lordPartyCheck?.SiegeEvent != null || 
                                  lordPartyCheck?.BesiegerCamp != null || 
                                  lordPartyCheck?.BesiegedSettlement != null;
                
                // If lord is in an army, also check if the army leader is besieging
                var lordArmy = lordPartyCheck?.Army;
                if (!lordInSiege && lordArmy != null)
                {
                    var armyLeader = lordArmy.LeaderParty;
                    lordInSiege = armyLeader?.BesiegerCamp != null || 
                                  armyLeader?.BesiegedSettlement != null ||
                                  armyLeader?.SiegeEvent != null;
                }

                if (lordInBattle && __result == "encounter")
                {
                    // Lord is in battle and native wants to show encounter menu - don't override
                    ModLogger.Info("MenuGuard", $"ALLOWING 'encounter' menu - lord is in battle");
                    return;
                }

                // During sieges, let ALL native menus flow (army_wait, menu_siege_strategies, etc.)
                if (lordInSiege)
                {
                    ModLogger.Info("MenuGuard", $"ALLOWING native menu '{__result}' - during siege");
                    return;
                }

                // Override army_wait, army_wait_at_settlement, and settlement menus to stay on enlisted menu
                // This prevents unwanted menu switches when the player hasn't explicitly chosen to visit
                // Settlement menus (town/castle/village) should only appear when player clicks "Visit Settlement"
                // Includes both inside ("castle", "town") and outside ("castle_outside", "town_outside") menus
                if (__result == "army_wait" || 
                    __result == "army_wait_at_settlement" ||
                    __result == "town" ||
                    __result == "town_outside" ||
                    __result == "castle" ||
                    __result == "castle_outside" ||
                    __result == "village")
                {
                    ModLogger.Info("MenuGuard", $"MENU OVERRIDE: '{__result}' -> 'enlisted_status' (keeping enlisted menu)");
                    __result = "enlisted_status";
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("GenericStateMenuPatch", "Error in GetGenericStateMenu patch", ex);
            }
        }
    }
}
