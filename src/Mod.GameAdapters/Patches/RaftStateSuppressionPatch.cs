using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Prevents the "stranded at sea" raft state menu from appearing for enlisted players.
    ///
    /// Root cause: When an army disbands at sea, the Naval DLC's RaftStateCampaignBehavior
    /// detects that the player's party has no ships (HasNavalNavigationCapability = false)
    /// and triggers the raft state, showing the "Your party is stranded at sea" menu.
    ///
    /// For enlisted players, this is incorrect because they're "embedded" with their lord
    /// and use the lord's ships for navigation. When the army disbands, the player should
    /// remain with the lord's party (which the mod's OnArmyDispersed handler sets up),
    /// not be treated as stranded.
    ///
    /// This patch intercepts the raft state menu activation and suppresses it for enlisted
    /// players who are still with a lord that has naval capability.
    /// </summary>
    public static class RaftStateSuppressionPatch
    {
        private const string LogCategory = "Naval";

        private static MethodInfo _deactivateRaftStateMethod;
        private static bool _patchApplied;

        /// <summary>
        /// Registers the raft state suppression patch with Harmony.
        /// Must be called during mod initialization after Naval DLC is loaded.
        /// </summary>
        public static void TryApplyPatch(Harmony harmony)
        {
            if (_patchApplied)
            {
                return;
            }

            try
            {
                // Find RaftStateCampaignBehavior type from Naval DLC
                var raftStateBehaviorType = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.RaftStateCampaignBehavior");
                if (raftStateBehaviorType == null)
                {
                    ModLogger.Debug(LogCategory, "Naval DLC not loaded - raft state suppression patch not needed");
                    return;
                }

                // Find the OnMobilePartyRaftStateChanged method
                var targetMethod = AccessTools.Method(raftStateBehaviorType, "OnMobilePartyRaftStateChanged");
                if (targetMethod == null)
                {
                    ModLogger.Warn(LogCategory, "Could not find OnMobilePartyRaftStateChanged method");
                    return;
                }

                // Cache deactivate method for later use
                var raftStateChangeActionType = typeof(RaftStateChangeAction);
                _deactivateRaftStateMethod = AccessTools.Method(raftStateChangeActionType, "DeactivateRaftStateForParty");

                // Apply the prefix patch
                var prefixMethod = AccessTools.Method(typeof(RaftStateSuppressionPatch), nameof(OnMobilePartyRaftStateChangedPrefix));
                harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));

                _patchApplied = true;
                ModLogger.Info(LogCategory, "Raft state suppression patch registered");
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Failed to apply raft state suppression patch", ex);
            }
        }

        /// <summary>
        /// Prefix that intercepts the raft state menu activation.
        /// For enlisted players with a lord that has naval capability, suppresses the menu
        /// and deactivates any incorrectly applied raft state.
        /// </summary>
        /// <param name="mobileParty">The party whose raft state changed.</param>
        /// <returns>True to run original method, false to skip it.</returns>
        public static bool OnMobilePartyRaftStateChangedPrefix(MobileParty mobileParty)
        {
            try
            {
                // Only intercept for the main party
                if (mobileParty == null || !mobileParty.IsMainParty)
                {
                    return true;
                }

                // Check if mod is active
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                // Check if player is enlisted
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true;
                }

                var lord = enlistment.CurrentLord;
                var lordParty = lord?.PartyBelongedTo;

                // If lord or lord's party is gone, allow normal raft state handling
                if (lordParty == null)
                {
                    return true;
                }

                // Check if lord has naval capability (owns ships)
                // If lord has ships, enlisted player shouldn't be stranded - they're on the lord's ship
                if (lordParty.HasNavalNavigationCapability)
                {
                    ModLogger.Info(LogCategory,
                        $"Suppressing raft state menu - enlisted under {lord.Name} who has naval capability");

                    // Deactivate the incorrectly applied raft state
                    if (mobileParty.IsInRaftState && _deactivateRaftStateMethod != null)
                    {
                        try
                        {
                            _deactivateRaftStateMethod.Invoke(null, new object[] { mobileParty });
                            ModLogger.Debug(LogCategory, "Deactivated incorrectly applied raft state");
                        }
                        catch (Exception deactivateEx)
                        {
                            ModLogger.Caught(LogCategory, "Failed to deactivate raft state", deactivateEx);
                        }
                    }

                    // Sync player position with lord to ensure they stay together
                    if (lordParty.IsCurrentlyAtSea)
                    {
                        mobileParty.IsCurrentlyAtSea = true;
                        mobileParty.Position = lordParty.Position;
                        mobileParty.SetMoveModeHold();
                        lordParty.Party.SetAsCameraFollowParty();

                        ModLogger.Debug(LogCategory, "Synced player position with lord after raft state suppression");
                    }

                    // Skip the original method which would show the raft state menu
                    return false;
                }

                // Lord has no ships either - check if army leader has ships
                var army = lordParty.Army;
                if (army?.LeaderParty?.HasNavalNavigationCapability == true)
                {
                    ModLogger.Info(LogCategory,
                        $"Suppressing raft state menu - lord's army leader has naval capability");

                    // Deactivate the incorrectly applied raft state
                    if (mobileParty.IsInRaftState && _deactivateRaftStateMethod != null)
                    {
                        try
                        {
                            _deactivateRaftStateMethod.Invoke(null, new object[] { mobileParty });
                            ModLogger.Debug(LogCategory, "Deactivated incorrectly applied raft state");
                        }
                        catch (Exception deactivateEx)
                        {
                            ModLogger.Caught(LogCategory, "Failed to deactivate raft state", deactivateEx);
                        }
                    }

                    return false;
                }

                // Lord and army have no ships - legitimate stranding situation
                // Allow normal raft state handling
                ModLogger.Info(LogCategory,
                    "Allowing raft state - lord has no naval capability (legitimate stranding)");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error in raft state suppression prefix", ex);
                // Fail open - allow original method to run
                return true;
            }
        }

        /// <summary>
        /// Additional patch to intercept OnPartyLeftArmy and prevent raft state activation
        /// when the enlisted player leaves an army at sea.
        /// </summary>
        public static void TryApplyOnPartyLeftArmyPatch(Harmony harmony)
        {
            try
            {
                var raftStateBehaviorType = AccessTools.TypeByName("NavalDLC.CampaignBehaviors.RaftStateCampaignBehavior");
                if (raftStateBehaviorType == null)
                {
                    return;
                }

                var targetMethod = AccessTools.Method(raftStateBehaviorType, "OnPartyLeftArmy");
                if (targetMethod == null)
                {
                    ModLogger.Debug(LogCategory, "OnPartyLeftArmy method not found - skipping secondary patch");
                    return;
                }

                var prefixMethod = AccessTools.Method(typeof(RaftStateSuppressionPatch), nameof(OnPartyLeftArmyPrefix));
                harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));

                ModLogger.Info(LogCategory, "OnPartyLeftArmy suppression patch registered");
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Failed to apply OnPartyLeftArmy patch", ex);
            }
        }

        /// <summary>
        /// Prefix for OnPartyLeftArmy that prevents raft state activation for enlisted players
        /// when the army disbands at sea.
        /// </summary>
        public static bool OnPartyLeftArmyPrefix(MobileParty party, Army army)
        {
            try
            {
                // Only intercept for the main party
                if (party == null || !party.IsMainParty)
                {
                    return true;
                }

                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return true;
                }

                var lord = enlistment.CurrentLord;
                var lordParty = lord?.PartyBelongedTo;

                // If lord has naval capability, skip the native OnPartyLeftArmy handling
                // which would trigger raft state for the shipless player
                if (lordParty?.HasNavalNavigationCapability == true)
                {
                    ModLogger.Info(LogCategory,
                        $"Suppressing OnPartyLeftArmy raft check - still enlisted under {lord.Name} with ships");
                    return false;
                }

                // Check army leader as fallback
                if (army?.LeaderParty?.HasNavalNavigationCapability == true)
                {
                    ModLogger.Info(LogCategory, "Suppressing OnPartyLeftArmy raft check - army leader has ships");
                    return false;
                }

                // No naval capability available - allow normal handling
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error in OnPartyLeftArmy prefix", ex);
                return true;
            }
        }
    }
}

