using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using EnlistedEncounterBehavior = Enlisted.Features.Combat.Behaviors.EnlistedEncounterBehavior;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Prevents party activation after enlistment discharge when in a vulnerable state.
    /// This prevents crashes from encounters being created immediately after army defeat.
    /// When the lord's army is defeated, the player party is in a battle/encounter state,
    /// and activating it causes the game to create a "Attack or Surrender" encounter which crashes.
    /// This patch blocks activation until the battle/encounter state is fully cleared.
    /// </summary>
    [HarmonyPatch(typeof(MobileParty), "IsActive", MethodType.Setter)]
    public class PostDischargeProtectionPatch
    {
        /// <summary>
        /// Prefix that prevents activation if enlistment just ended and player is in battle state.
        /// Returns false to prevent activation, true to allow normal activation.
        /// Harmony invokes this method via reflection - static analysis cannot detect runtime usage.
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance is a special injected parameter")]
        [SuppressMessage("CodeQuality", "IDE0051", Justification = "Called by Harmony via reflection")]
        [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Called by Harmony via reflection")]
        public static bool Prefix(MobileParty __instance, bool value)
        {
            try
            {
                // Skip during character creation when campaign isn't fully initialized
                if (Campaign.Current == null)
                {
                    return true;
                }

                var campaign = Campaign.Current;
                var mainPartyMobile = campaign?.MainParty;

                // Only check for main party once the campaign is initialized
                if (mainPartyMobile == null || __instance != mainPartyMobile)
                {
                    return true; // Allow normal activation for other parties
                }

                // Only check when trying to activate (value = true)
                if (!value)
                {
                    return true; // Always allow deactivation
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null)
                {
                    return true;
                }

                // If player is a prisoner, allow all activation changes (captivity system needs control)
                // Log this to diagnose "attacked while prisoner" - activation shouldn't normally happen
                if (Hero.MainHero?.IsPrisoner == true)
                {
                    var settlement = Hero.MainHero.CurrentSettlement?.Name?.ToString() ?? "none";
                    ModLogger.Info("Captivity",
                        $"Party activation allowed while prisoner (settlement: {settlement})");
                    return true;
                }

                // Block activation when waiting in reserve AND still enlisted
                // Only block if actively enlisted - if enlistment ended, allow activation even if flag is stale
                var isInReserve = EnlistedEncounterBehavior.IsWaitingInReserve;
                if (isInReserve && enlistment.IsEnlisted)
                {
                    ModLogger.Debug("PostDischargeProtection", "Blocked party activation - player waiting in reserve");
                    return false;
                }

                // If reserve flag is stale (enlistment ended but flag wasn't cleared), clear it
                if (isInReserve && !enlistment.IsEnlisted)
                {
                    ModLogger.Debug("PostDischargeProtection", "Clearing stale reserve flag during activation");
                    EnlistedEncounterBehavior.ClearReserveState();
                }

                // Only intervene when the player has just been discharged and is still in their grace/cleanup window.
                if (enlistment.IsEnlisted || !enlistment.IsInDesertionGracePeriod)
                {
                    return true;
                }

                // Check if player is in a vulnerable state after discharge - block to prevent crash-inducing encounter
                // Must check BOTH MapEvent AND PlayerEncounter - player can be in encounter state
                // (e.g. surrender negotiations) without an active MapEvent
                var mainParty = mainPartyMobile.Party;
                var playerInMapEvent = mainParty?.MapEvent != null;
                var playerInEncounter = campaign.PlayerEncounter != null;

                if (playerInMapEvent || playerInEncounter)
                {
                    ModLogger.Info("PostDischargeProtection",
                        $"Prevented party activation - discharged but still in battle state (MapEvent: {playerInMapEvent}, Encounter: {playerInEncounter})");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PostDischargeProtection", "Error in activation protection patch", ex);
                return true; // Fail open - allow activation if we can't determine state
            }
        }
    }
}
