using System;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using EnlistedEncounterBehavior = Enlisted.Features.Combat.Behaviors.EnlistedEncounterBehavior;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Harmony patch that prevents unwanted encounters from being created when the player is enlisted.
    /// Intercepts encounter creation and checks if it involves legitimate battles with the lord's party.
    /// This prevents random encounters while allowing battle participation when the lord fights.
    /// </summary>
    [HarmonyPatch(typeof(EncounterManager), "StartPartyEncounter")]
    public class EncounterSuppressionPatch
    {
        /// <summary>
        /// Prefix method that runs before EncounterManager.StartPartyEncounter.
        /// Checks if the encounter involves the enlisted player and whether it's a legitimate battle.
        /// Returns false to prevent unwanted encounters, true to allow legitimate battles.
        /// Called by Harmony via reflection.
        /// </summary>
        /// <param name="attackerParty">The attacking party in the encounter.</param>
        /// <param name="defenderParty">The defending party in the encounter.</param>
        /// <returns>True to allow the encounter, false to prevent it.</returns>
        [HarmonyPrefix]
        public static bool Prefix(PartyBase attackerParty, PartyBase defenderParty)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                // Check if player is enlisted or just ended enlistment (grace period)
                var enlistment = EnlistmentBehavior.Instance;
                var isEnlisted = enlistment?.IsEnlisted == true;
                var hasGraceProtection = enlistment?.HasActiveGraceProtection == true;

                // Check if player just ended enlistment and is still in a MapEvent/Encounter
                var mainParty = MobileParty.MainParty?.Party;
                var mainMobileParty = MobileParty.MainParty;
                var playerInMapEvent = mainParty?.MapEvent != null;
                var playerInEncounter = PlayerEncounter.Current != null;
                var playerInBattleState = playerInMapEvent || playerInEncounter;
                var justEndedEnlistment = !isEnlisted && playerInBattleState;

                // Check if this encounter involves the main party
                if (mainParty == null)
                {
                    return true; // Allow normal encounter creation
                }

                var mainPartyInvolved = attackerParty == mainParty || defenderParty == mainParty;
                if (!mainPartyInvolved)
                {
                    return true; // Allow normal encounter creation
                }

                // DIAGNOSTIC: Log every encounter attempt involving the player
                var attackerName = attackerParty?.Name?.ToString() ?? "null";
                var defenderName = defenderParty?.Name?.ToString() ?? "null";
                var partyActive = mainMobileParty?.IsActive ?? false;
                var partyVisible = mainMobileParty?.IsVisible ?? false;
                ModLogger.Info("EncounterGuard",
                    $"ENCOUNTER ATTEMPT: Attacker={attackerName}, Defender={defenderName} | " +
                    $"Enlisted={isEnlisted}, Active={partyActive}, Visible={partyVisible}, " +
                    $"InMapEvent={playerInMapEvent}, InEncounter={playerInEncounter}");

                // If player is already a prisoner, allow encounters (captor movement)
                // Log with party state to help diagnose "attacked while prisoner" bugs
                if (Hero.MainHero?.IsPrisoner == true)
                {
                    var inSettlement = Hero.MainHero?.CurrentSettlement?.Name?.ToString();

                    // PlayerActive: True while prisoner = BUG (party should be deactivated)
                    ModLogger.Info("Captivity",
                        $"Encounter while prisoner (Attacker: {attackerName}, PlayerActive: {partyActive}, Settlement: {inSettlement ?? "none"})");
                    return true;
                }

                // Block encounters when waiting in reserve AND still enlisted
                // Only block if actively enlisted - if enlistment ended, clear stale flag and allow
                var isInReserve = EnlistedEncounterBehavior.IsWaitingInReserve;
                if (isInReserve && isEnlisted)
                {
                    ModLogger.Info("EncounterGuard", $"BLOCKED: Waiting in reserve (Attacker={attackerName})");
                    return false;
                }

                // If reserve flag is stale (enlistment ended but flag wasn't cleared), clear it
                else if (isInReserve) // here isEnlisted must be false if still in reserve
                {
                    ModLogger.Info("EncounterGuard", "Clearing stale reserve flag during encounter creation");
                    EnlistedEncounterBehavior.ClearReserveState();
                }

                // Suppress encounters if enlistment just ended and player is still in MapEvent/Encounter
                if (justEndedEnlistment)
                {
                    ModLogger.Info("EncounterGuard", $"BLOCKED: Enlistment just ended, still in battle state (MapEvent={playerInMapEvent}, Encounter={playerInEncounter})");
                    return false;
                }

                if (hasGraceProtection)
                {
                    ModLogger.Info("EncounterGuard", $"BLOCKED: Grace protection active (Attacker={attackerName})");
                    return false;
                }

                // If not enlisted and not in grace period, allow normal encounters
                if (!isEnlisted)
                {
                    ModLogger.Info("EncounterGuard", $"ALLOWED: Not enlisted (Attacker={attackerName})");
                    return true;
                }

                // CRITICAL: Prevent encounters with the lord's party itself
                var enlistedLord = enlistment!.CurrentLord;
                var lordParty = enlistedLord?.PartyBelongedTo?.Party;

                var lordName = enlistedLord?.Name?.ToString() ?? "null";
                var lordInMapEvent = lordParty?.MapEvent != null;
                var lordArmy = enlistedLord?.PartyBelongedTo?.Army;
                var armyLeaderName = lordArmy?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "none";
                var armyLeaderInMapEvent = lordArmy?.LeaderParty?.Party?.MapEvent != null;
                var playerInArmy = mainMobileParty?.Army != null;
                var playerArmyLeaderName = mainMobileParty?.Army?.LeaderParty?.LeaderHero?.Name?.ToString() ?? "none";

                // Check if ANY party in the army is in a MapEvent
                var anyArmyPartyInBattle = false;
                var armyBattlePartyName = "none";
                if (lordArmy != null)
                {
                    foreach (var armyParty in lordArmy.Parties)
                    {
                        if (armyParty?.Party?.MapEvent != null)
                        {
                            anyArmyPartyInBattle = true;
                            armyBattlePartyName = armyParty.LeaderHero?.Name?.ToString() ?? armyParty.Name?.ToString() ?? "unknown";
                            break;
                        }
                    }
                }

                ModLogger.Info("EncounterGuard",
                    $"LORD STATE: Lord={lordName}, LordInMapEvent={lordInMapEvent}, " +
                    $"Army={armyLeaderName}, ArmyLeaderInMapEvent={armyLeaderInMapEvent}");
                ModLogger.Info("EncounterGuard",
                    $"ARMY STATE: PlayerInArmy={playerInArmy}, PlayerArmyLeader={playerArmyLeaderName}, " +
                    $"AnyArmyPartyInBattle={anyArmyPartyInBattle}, BattleParty={armyBattlePartyName}");

                if (lordParty != null)
                {
                    // Check if this encounter is between the player and the lord's party (not a battle)
                    var isEncounterWithLord = (attackerParty == mainParty && defenderParty == lordParty) ||
                                               (attackerParty == lordParty && defenderParty == mainParty);

                    if (isEncounterWithLord)
                    {
                        // Check if the lord is in a battle - if so, this might be legitimate
                        if (!lordInMapEvent)
                        {
                            // Allow encounters when player party is visible/active - indicates player-initiated conversation
                            var isPlayerInitiated = partyVisible || partyActive;

                            if (isPlayerInitiated)
                            {
                                ModLogger.Info("EncounterGuard", $"ALLOWED: Player-initiated encounter with lord (Lord={lordName})");
                                return true;
                            }

                            // Party is invisible and inactive - suppress unwanted automatic encounter
                            ModLogger.Info("EncounterGuard", $"BLOCKED: Unwanted encounter with lord's party (Lord={lordName})");
                            return false;
                        }
                    }

                    // Check if this is a legitimate battle involving the lord's party or army
                    var lordInvolved = attackerParty == lordParty || defenderParty == lordParty;

                    if (lordInvolved && lordInMapEvent)
                    {
                        ModLogger.Info("EncounterGuard", $"ALLOWED: Lord's battle (Lord={lordName})");
                        return true;
                    }

                    // Check if the lord's army is involved in this encounter
                    if (lordArmy != null)
                    {
                        var armyLeaderInvolved = attackerParty == lordArmy.LeaderParty?.Party || defenderParty == lordArmy.LeaderParty?.Party;

                        // Also check if the OTHER party in the encounter is part of the army's battle
                        var otherParty = attackerParty == mainParty ? defenderParty : attackerParty;
                        var otherPartyName = otherParty?.Name?.ToString() ?? "null";
                        var otherPartyInArmyBattle = false;

                        if (anyArmyPartyInBattle && lordArmy.LeaderParty?.Party?.MapEvent != null)
                        {
                            var armyMapEvent = lordArmy.LeaderParty.Party.MapEvent;
                            otherPartyInArmyBattle = armyMapEvent.InvolvedParties?.Any(p => p == otherParty) == true;
                        }

                        ModLogger.Info("EncounterGuard",
                            $"ARMY BATTLE CHECK: ArmyLeaderInvolved={armyLeaderInvolved}, " +
                            $"OtherParty={otherPartyName}, OtherPartyInArmyBattle={otherPartyInArmyBattle}");

                        if (armyLeaderInvolved && armyLeaderInMapEvent)
                        {
                            ModLogger.Info("EncounterGuard", $"ALLOWED: Army leader battle (Army={armyLeaderName})");
                            return true;
                        }

                        // NEW: Also allow if any army party is in a battle with this enemy
                        if (anyArmyPartyInBattle && otherPartyInArmyBattle)
                        {
                            ModLogger.Info("EncounterGuard", $"ALLOWED: Army member battle with {otherPartyName}");
                            return true;
                        }
                    }
                }

                // CRITICAL FIX: If player is already in a MapEvent (joined the lord's battle via MapEventSide),
                // do NOT allow creating a separate encounter. The player is already participating in a battle.
                // Previously this check allowed encounters when active+MapEvent, but that was wrong - it let
                // the native game create duplicate encounters with enemy parties the lord was fighting.
                if (mainParty.MapEvent != null)
                {
                    var mapEventType = mainParty.MapEvent.EventType.ToString();
                    ModLogger.Info("EncounterGuard", $"BLOCKED: Already in MapEvent ({mapEventType}) - duplicate encounter prevented");
                    return false;
                }

                // Suppress unwanted encounter - player is enlisted but not in an active battle
                ModLogger.Info("EncounterGuard", $"BLOCKED: Enlisted player, no valid battle context (Attacker={attackerName})");
                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("EncounterSuppression", "Error in encounter suppression patch", ex);
                return true; // Fail open - allow encounter if we can't determine state
            }
        }
    }
}
