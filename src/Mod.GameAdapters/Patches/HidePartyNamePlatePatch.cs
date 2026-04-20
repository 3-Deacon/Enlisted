using System;
using System.Reflection;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Prevents the player's party nameplate from appearing when enlisted.
    /// Works by nulling out the PlayerNameplate on PartyNameplatesVM, which is the same
    /// mechanism the game uses when the player enters a settlement.
    /// 
    /// Uses manual patching to avoid type resolution issues during character creation.
    /// This class is NOT auto-patched by Harmony - it's applied manually in SubModule.
    /// </summary>
    public static class HidePartyNamePlatePatch
    {
        // Cached references for PlayerNameplate management
        private static Type _partyNameplatesVmType;
        private static PropertyInfo _playerNameplateProperty;
        private static MethodInfo _playerNameplateClearMethod;
        private static bool _isNameplateHidden;
        private static int _updateCallCount;

        /// <summary>
        /// Manually applies the patch to PartyNameplatesVM.Update().
        /// Call this from SubModule instead of using HarmonyPatchAll.
        /// </summary>
        public static void ApplyManualPatches(Harmony harmony)
        {
            try
            {
                ModLogger.Info("HidePartyNamePlatePatch", "Applying nameplate hiding patch...");

                // Find PartyNameplatesVM - this is the VM that manages all nameplates on the map
                _partyNameplatesVmType = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.PartyNameplatesVM");
                if (_partyNameplatesVmType == null)
                {
                    ModLogger.Info("HidePartyNamePlatePatch", "PartyNameplatesVM type not found - skipping patch");
                    return;
                }

                // Cache the PlayerNameplate property - this is what we'll set to null
                _playerNameplateProperty = _partyNameplatesVmType.GetProperty("PlayerNameplate");
                if (_playerNameplateProperty == null)
                {
                    ModLogger.Info("HidePartyNamePlatePatch", "PlayerNameplate property not found - skipping patch");
                    return;
                }

                // Cache the Clear method on PartyPlayerNameplateVM for proper cleanup
                var playerNameplateVmType = AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.PartyPlayerNameplateVM");
                if (playerNameplateVmType != null)
                {
                    _playerNameplateClearMethod = playerNameplateVmType.GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
                }

                // Patch the Update method - this runs every frame and manages nameplate state
                var updateMethod = _partyNameplatesVmType.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public);
                if (updateMethod != null)
                {
                    var postfix = typeof(HidePartyNamePlatePatch).GetMethod(nameof(UpdatePostfix),
                        BindingFlags.Static | BindingFlags.NonPublic);
                    _ = harmony.Patch(updateMethod, postfix: new HarmonyMethod(postfix));
                    ModLogger.Info("HidePartyNamePlatePatch", "SUCCESS: Patched PartyNameplatesVM.Update");
                }
                else
                {
                    ModLogger.Info("HidePartyNamePlatePatch", "PartyNameplatesVM.Update method not found");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("HidePartyNamePlatePatch", "Failed to apply patch", ex);
            }
        }

        /// <summary>
        /// After the game's Update() processes nameplates, we hide the player's nameplate
        /// by clearing it and setting it to null - exactly what the game does when entering a settlement.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance is a special injected parameter")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        private static void UpdatePostfix(object __instance)
        {
            _updateCallCount++;

            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return;
                }

                if (!CampaignSafetyGuard.IsCampaignReady)
                {
                    return;
                }

                var isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted == true;
                var isOnLeave = EnlistmentBehavior.Instance?.IsOnLeave == true;
                var hasLord = EnlistmentBehavior.Instance?.CurrentLord != null;
                var shouldHide = isEnlisted || (hasLord && !isOnLeave);

                if (shouldHide)
                {
                    var currentNameplate = _playerNameplateProperty?.GetValue(__instance);

                    if (currentNameplate != null)
                    {
                        if (!_isNameplateHidden)
                        {
                            ModLogger.Info("HidePartyNamePlatePatch", "Hiding player nameplate");
                        }

                        // Clear and null - same as game's OnSettlementEntered behavior
                        _ = (_playerNameplateClearMethod?.Invoke(currentNameplate, null));
                        _playerNameplateProperty?.SetValue(__instance, null);

                        _isNameplateHidden = true;
                    }
                }
                else if (_isNameplateHidden)
                {
                    // Player no longer enlisted - game will recreate nameplate naturally
                    ModLogger.Info("HidePartyNamePlatePatch", "Allowing nameplate recreation");
                    _isNameplateHidden = false;
                }
            }
            catch (Exception ex)
            {
                // Only log errors occasionally to avoid spam
                if (_updateCallCount % 1000 == 1)
                {
                    ModLogger.Caught("HidePartyNamePlatePatch", "Error in UpdatePostfix", ex);
                }
            }
        }
    }
}
