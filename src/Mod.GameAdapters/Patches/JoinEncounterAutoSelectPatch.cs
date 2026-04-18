using System;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Harmony patch that automatically joins enlisted players to their lord's battle,
    ///     bypassing the native "join_encounter" menu that asks "Help X's Party / Don't get involved".
    ///     When an enlisted soldier's lord attacks enemies (like looters) without being in an army,
    ///     the native game shows a choice menu. This patch intercepts that menu and automatically
    ///     joins the battle on the lord's side, then shows the standard encounter menu with
    ///     Attack/Send Troops/Wait options instead.
    /// </summary>
    [HarmonyPatch(typeof(TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior),
        "game_menu_join_encounter_on_init")]
    public static class JoinEncounterAutoSelectPatch
    {
        /// <summary>
        ///     Prefix patch for game_menu_join_encounter_on_init.
        ///     Intercepts the "join_encounter" menu initialization and auto-joins for enlisted players.
        ///     Returns false to skip native on_init when auto-joining (player never sees the menu).
        ///     Called by Harmony via reflection.
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix()
        {
            try
            {
                // DIAGNOSTIC: Log when join_encounter menu is triggered
                var mainParty = MobileParty.MainParty;
                var partyActive = mainParty?.IsActive ?? false;
                var partyVisible = mainParty?.IsVisible ?? false;
                var playerInMapEvent = mainParty?.Party?.MapEvent != null;
                var hasEncounter = PlayerEncounter.Current != null;
                
                ModLogger.Info("EncounterGuard", 
                    $"JOIN_ENCOUNTER MENU TRIGGERED | Active={partyActive}, Visible={partyVisible}, " +
                    $"InMapEvent={playerInMapEvent}, HasEncounter={hasEncounter}");
                
                // Check if player is enlisted
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    // Not enlisted - let native menu show normally
                    ModLogger.Info("EncounterGuard", "JOIN_ENCOUNTER: Not enlisted, allowing native menu");
                    return true;
                }

                var lord = enlistment.CurrentLord;
                var lordParty = lord?.PartyBelongedTo?.Party;
                var lordName = lord?.Name?.ToString() ?? "null";
                var lordArmy = lord?.PartyBelongedTo?.Army;
                var armyLeaderName = lordArmy?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "none";
                var playerInArmy = mainParty?.Army != null;
                
                ModLogger.Info("EncounterGuard", 
                    $"JOIN_ENCOUNTER LORD: Lord={lordName}, Army={armyLeaderName}, PlayerInArmy={playerInArmy}");
                
                if (lordParty == null)
                {
                    ModLogger.Info("EncounterGuard", $"JOIN_ENCOUNTER: Lord party is null (Lord={lordName}), allowing native menu");
                    return true;
                }

                // Get the encountered battle
                var battle = PlayerEncounter.EncounteredBattle;
                if (battle == null)
                {
                    ModLogger.Info("EncounterGuard", "JOIN_ENCOUNTER: No EncounteredBattle, allowing native menu");
                    return true;
                }

                var attackerName = battle.AttackerSide?.LeaderParty?.Name?.ToString() ?? "unknown";
                var defenderName = battle.DefenderSide?.LeaderParty?.Name?.ToString() ?? "unknown";
                var attackerPartyCount = battle.AttackerSide?.Parties?.Count ?? 0;
                var defenderPartyCount = battle.DefenderSide?.Parties?.Count ?? 0;
                ModLogger.Info("EncounterGuard", 
                    $"JOIN_ENCOUNTER BATTLE: Attacker={attackerName} ({attackerPartyCount} parties) vs Defender={defenderName} ({defenderPartyCount} parties)");

                // Check if our lord is involved in this battle
                var lordIsAttacker = battle.AttackerSide.Parties.Any(p => p.Party == lordParty);
                var lordIsDefender = battle.DefenderSide.Parties.Any(p => p.Party == lordParty);
                
                // Also check if any army member is involved
                var armyMemberIsAttacker = lordArmy != null && battle.AttackerSide.Parties.Any(p => lordArmy.Parties.Contains(p.Party?.MobileParty));
                var armyMemberIsDefender = lordArmy != null && battle.DefenderSide.Parties.Any(p => lordArmy.Parties.Contains(p.Party?.MobileParty));
                
                ModLogger.Info("EncounterGuard", 
                    $"JOIN_ENCOUNTER INVOLVEMENT: LordIsAttacker={lordIsAttacker}, LordIsDefender={lordIsDefender}, " +
                    $"ArmyMemberIsAttacker={armyMemberIsAttacker}, ArmyMemberIsDefender={armyMemberIsDefender}");

                if (!lordIsAttacker && !lordIsDefender && !armyMemberIsAttacker && !armyMemberIsDefender)
                {
                    // Lord and army not in this battle - this might be a different encounter
                    // Let the native menu show so player can decide
                    ModLogger.Info("EncounterGuard", $"JOIN_ENCOUNTER: Lord ({lordName}) and army not in battle, allowing native menu");
                    return true;
                }
                
                // If lord isn't directly involved but army member is, use army member's side
                if (!lordIsAttacker && !lordIsDefender)
                {
                    if (armyMemberIsAttacker)
                    {
                        ModLogger.Info("EncounterGuard", "JOIN_ENCOUNTER: Army member is attacker, joining attacker side");
                    }
                    else if (armyMemberIsDefender)
                    {
                        ModLogger.Info("EncounterGuard", "JOIN_ENCOUNTER: Army member is defender, joining defender side");
                    }
                }

                // Determine which side to join (same side as our lord or army)
                BattleSideEnum lordSide;
                if (lordIsAttacker || armyMemberIsAttacker)
                {
                    lordSide = BattleSideEnum.Attacker;
                }
                else
                {
                    lordSide = BattleSideEnum.Defender;
                }
                ModLogger.Info("EncounterGuard", $"JOIN_ENCOUNTER: Joining {lordSide} side (LordDirect={lordIsAttacker || lordIsDefender}, ArmyMember={armyMemberIsAttacker || armyMemberIsDefender})");

                // Check if we can actually join on the lord's side using native faction checks
                bool canJoinNatively = battle.CanPartyJoinBattle(PartyBase.MainParty, lordSide);
                
                // Check if lord is in a minor/bandit faction (not a Kingdom)
                // Non-Kingdom faction lords don't trigger the mercenary join logic, so player's faction state
                // may not have proper war relations with enemies like bandits.
                bool isNonKingdomFactionLord = lord.MapFaction != null && 
                                               !(lord.MapFaction is Kingdom) &&
                                               (lord.MapFaction.IsMinorFaction || lord.MapFaction.IsBanditFaction);
                
                if (!canJoinNatively)
                {
                    if (isNonKingdomFactionLord)
                    {
                        // NON-KINGDOM FACTION FIX: Bypass the native faction check for minor/bandit faction lords.
                        ModLogger.Info("EncounterGuard",
                            $"JOIN_ENCOUNTER: Non-Kingdom faction lord, bypassing faction check for {lordSide} side");
                    }
                    else
                    {
                        // Kingdom lord but faction check failed - this is unexpected
                        ModLogger.Warn("EncounterGuard",
                            $"JOIN_ENCOUNTER: Faction check failed for Kingdom lord ({lordSide} side) - allowing native menu");
                        return true;
                    }
                }

                // Check enemy troop count BEFORE joining to determine if battle is still ongoing
                // Enemy side is opposite of lord's side - if lord attacks, enemies are defenders and vice versa
                var enemySide = lordSide == BattleSideEnum.Attacker ? battle.DefenderSide : battle.AttackerSide;
                var battleAlreadyWon = enemySide.TroopCount <= 0 || battle.HasWinner;

                if (battleAlreadyWon)
                {
                    // Battle is already over (no enemies or has winner) - don't show any encounter menu
                    // This happens after auto-resolve when a stale encounter triggers join_encounter
                    ModLogger.Info("EncounterGuard",
                        $"JOIN_ENCOUNTER: Battle already won (enemies={enemySide.TroopCount}, hasWinner={battle.HasWinner}) - cleaning up");

                    // Clean up the encounter state and return to appropriate menu
                    try
                    {
                        if (PlayerEncounter.Current != null)
                        {
                            PlayerEncounter.LeaveEncounter = true;
                            if (PlayerEncounter.InsideSettlement)
                            {
                                PlayerEncounter.LeaveSettlement();
                            }
                            PlayerEncounter.Finish();
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        ModLogger.Debug("JoinEncounter", $"Encounter cleanup warning: {cleanupEx.Message}");
                    }

                    // Deactivate player party to prevent further stale encounters
                    if (mainParty != null && enlistment.IsEnlisted)
                    {
                        mainParty.IsActive = false;
                        mainParty.IsVisible = false;
                        ModLogger.Info("EncounterGuard", "JOIN_ENCOUNTER: Deactivated party after battle-already-won cleanup");
                    }

                    // Return to appropriate menu based on army status
                    if (mainParty?.Army != null && mainParty.Army.LeaderParty != mainParty)
                    {
                        GameMenu.SwitchToMenu("army_wait");
                        ModLogger.Info("EncounterGuard", "JOIN_ENCOUNTER: Switched to army_wait menu");
                    }
                    else
                    {
                        GameMenu.SwitchToMenu("enlisted_status");
                        ModLogger.Info("EncounterGuard", "JOIN_ENCOUNTER: Switched to enlisted_status menu");
                    }

                    return false; // Skip native menu
                }

                // Battle is ongoing - auto-join on the lord's side
                ModLogger.Info("EncounterGuard",
                    $"JOIN_ENCOUNTER: Auto-joining {lordName}'s battle as {lordSide}");

                try
                {
                    // Join the battle - this is what the native "Help X" option does
                    PlayerEncounter.JoinBattle(lordSide);

                    // Switch to the standard encounter menu (Attack/Send Troops/Wait options)
                    GameMenu.SwitchToMenu("encounter");

                    ModLogger.Info("EncounterGuard", "JOIN_ENCOUNTER: Successfully auto-joined battle, showing encounter menu");

                    // Return false to skip native on_init - the join_encounter menu never displays
                    return false;
                }
                catch (Exception joinEx)
                {
                    // JoinBattle can fail for parties with no troops
                    // Fall back to letting native menu show
                    ModLogger.Caught("JoinEncounter", "Failed to auto-join battle", joinEx);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("JoinEncounter", "Error in join_encounter patch", ex);
                // Fail open - let native menu show
                return true;
            }
        }
    }
}
