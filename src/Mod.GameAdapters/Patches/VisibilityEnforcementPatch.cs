using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Encounters;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

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
        // Force-hide state: when true, blocks ALL visibility changes (overrides everything)
        // This is set after forced settlement exit to prevent race conditions
        private static bool _forceHidden;
        private static float _forceHiddenUntil;
        private static bool? _lastPrisonerVisibilityValue;
        private static string _lastPrisonerVisibilitySettlement;

        /// <summary>
        /// Call this immediately BEFORE forcing player out of a settlement.
        /// Ensures visibility is blocked even during the transition frames.
        /// </summary>
        public static void BeginForceHidden()
        {
            _forceHidden = true;
            // Force hidden for 2 seconds to cover any transition delays
            _forceHiddenUntil = Campaign.CurrentTime + (2f / 24f / 60f); // 2 seconds in campaign time
            ModLogger.Debug("VisibilityEnforcement", "Force hidden mode ENABLED for settlement exit");
        }

        /// <summary>
        /// Call this after hiding is complete and escort is re-established.
        /// </summary>
        public static void EndForceHidden()
        {
            _forceHidden = false;
            ModLogger.Debug("VisibilityEnforcement", "Force hidden mode DISABLED");
        }

        /// <summary>
        /// Prefix that prevents visibility from being set to true for enlisted players.
        /// Returns false to block the setter, true to allow normal visibility.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance is a special injected parameter")]
        private static bool Prefix(MobileParty __instance, bool value)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                // Skip during character creation - use safe guard to prevent crashes
                var mainParty = CampaignSafetyGuard.SafeMainParty;
                if (mainParty == null)
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;
                
            // If player is a prisoner, allow all visibility changes (captivity system needs control)
            // Throttle logging to changes in value/settlement to avoid spam while being marched
            if (__instance == mainParty && Hero.MainHero?.IsPrisoner == true)
                {
                    var settlement = Hero.MainHero?.CurrentSettlement?.Name?.ToString() ?? "none";
                if (_lastPrisonerVisibilityValue != value || _lastPrisonerVisibilitySettlement != settlement)
                {
                    ModLogger.Info("Captivity", 
                        $"Party visibility allowed while prisoner (value: {value}, settlement: {settlement})");
                    _lastPrisonerVisibilityValue = value;
                    _lastPrisonerVisibilitySettlement = settlement;
                }
                    return true;
                }
            else if (_lastPrisonerVisibilityValue.HasValue)
            {
                // Reset throttle cache once no longer prisoner
                _lastPrisonerVisibilityValue = null;
                _lastPrisonerVisibilitySettlement = null;
            }

                // Only check for main party
                if (__instance != mainParty)
                {
                    // Fix for "Lord disappearing" issue:
                    // If the party being modified is the Lord's party we are enlisted with,
                    // we must ensure it stays visible on the map even if game mechanics (forests/ambush) try to hide it.
                    // The player "shares" the lord's vision, so the lord should never be hidden from the player.
                    var enlistedLord = enlistment?.CurrentLord;
                    var isLordParty = false;

                    if (enlistedLord != null)
                    {
                        // Check both PartyBelongedTo and LeaderHero for robustness
                        isLordParty = __instance == enlistedLord.PartyBelongedTo || __instance.LeaderHero == enlistedLord;
                    }

                    if (isLordParty)
                    {
                        // If trying to hide (value == false) and the lord is on the map (not in settlement)
                        if (!value && __instance.CurrentSettlement == null && __instance.IsActive)
                        {
                            var isEnlistedCheck = enlistment?.IsEnlisted == true;
                            var onLeaveCheck = enlistment?.IsOnLeave == true;

                            // If enlisted OR on leave, force visibility (block hiding)
                            if (isEnlistedCheck || onLeaveCheck)
                            {
                                // CRITICAL FIX: If the party is ALREADY invisible, blocking the "hide" call won't help.
                                // We must force it to be visible.
                                if (!__instance.IsVisible)
                                {
                                    ModLogger.Info("VisibilityEnforcement", "Lord party was invisible during hide attempt - Forcing IsVisible=true");
                                    // This will trigger the setter again with value=true.
                                    // Our Prefix allows value=true when OnLeave/Enlisted (see logic below), so this is safe.
                                    __instance.IsVisible = true;
                                }
                                else
                                {
                                    // Only log occasionally to avoid spam when it's working correctly
                                    // ModLogger.Debug("VisibilityEnforcement", "Blocking Lord visibility hide (party is already visible)");
                                }

                                return false;
                            }
                            else
                            {
                                // Debug logging to diagnose why blocking failed
                                ModLogger.Debug("VisibilityEnforcement", "Allowed Lord hide: Enlisted=false, Leave=false");
                            }
                        }
                    }

                    return true; // Allow normal visibility for other parties
                }

                // Only check when trying to make visible (value = true)
                if (!value)
                {
                    return true; // Always allow making invisible
                }

                // FORCE HIDDEN CHECK: Overrides everything during settlement exit transition
                // This prevents race conditions where CurrentSettlement/PlayerEncounter aren't cleared yet
                if (_forceHidden)
                {
                    // Auto-expire after 2 seconds in case EndForceHidden() wasn't called
                    if (Campaign.CurrentTime > _forceHiddenUntil)
                    {
                        _forceHidden = false;
                        ModLogger.Debug("VisibilityEnforcement", "Force hidden mode auto-expired");
                    }
                    else
                    {
                        ModLogger.Debug("VisibilityEnforcement", "BLOCKED by force hidden mode (settlement exit in progress)");
                        return false;
                    }
                }

                var isEnlisted = enlistment?.IsEnlisted == true;
                var onLeave = enlistment?.IsOnLeave == true;
                var inGrace = enlistment?.IsInDesertionGracePeriod == true;

                // Fresh campaigns, leave, or grace periods should behave like vanilla.
                // Note: This check fires very frequently (every visibility set attempt) so we don't log it
                if (!isEnlisted || onLeave || inGrace)
                {
                    return true;
                }

                var playerEncounter = PlayerEncounter.Current != null;
                var playerBattle = mainParty.Party?.MapEvent != null || mainParty.Party?.SiegeEvent != null;

                // Check if lord or their army is in battle - if so, we MUST allow visibility for encounter system
                var lordParty = enlistment?.CurrentLord?.PartyBelongedTo;
                var lordInBattle = lordParty?.Party?.MapEvent != null || lordParty?.Party?.SiegeEvent != null;
                // Army battles: the army leader's party holds the MapEvent, not necessarily each member's party
                var armyInBattle = lordParty?.Army?.LeaderParty?.Party?.MapEvent != null || lordParty?.Army?.LeaderParty?.Party?.SiegeEvent != null;
                var anyBattleActive = lordInBattle || armyInBattle;

                // Check if player is in a settlement - if so, allow visibility for town/castle menus
                var playerInSettlement = mainParty.CurrentSettlement != null;

                // When the native game needs us visible (battle, encounter, in settlement) we must allow it,
                // otherwise the encounter system loops and eventually asserts (rglSkeleton.cpp:1197).
                // CRITICAL: Also allow visibility when LORD or ARMY is in battle - native needs player visible to show encounter menu
                // NOTE: We removed "!embeddedWithLord" check because IsEmbeddedWithLord() can return false temporarily
                // during transitions (e.g., after settlement exit before TargetParty is set).
                // If enlisted and not in battle/encounter/settlement, ALWAYS block visibility.
                if (playerEncounter || playerBattle || anyBattleActive || playerInSettlement)
                {
                    if (anyBattleActive && !playerBattle)
                    {
                        ModLogger.Debug("VisibilityEnforcement", $"Allowing visibility - battle active (lord:{lordInBattle}, army:{armyInBattle}), player needs encounter menu");
                    }
                    else if (playerInSettlement)
                    {
                        ModLogger.Debug("VisibilityEnforcement", "Allowing visibility - player in settlement");
                    }
                    else
                    {
                        ModLogger.Debug("VisibilityEnforcement", "Allowing visibility - native encounter/battle requires it");
                    }
                    return true;
                }

                // Otherwise we're still tucked inside the lord's escort, keep the party hidden.
                // Note: This fires frequently so we don't log it to avoid spam
                return false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("VisibilityEnforcement", "Error in visibility enforcement patch", ex);
                return true; // Fail open - allow visibility if we can't determine state
            }
        }
    }
}

