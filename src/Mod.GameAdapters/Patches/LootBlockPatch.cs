using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;

// ReSharper disable UnusedType.Global - Harmony patches are applied via attributes, not direct code references
// ReSharper disable UnusedMember.Local - Harmony Prefix/Postfix methods are invoked via reflection
// ReSharper disable InconsistentNaming - __instance/__result are Harmony naming conventions; _fields follow project convention
namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Tier-gated loot system for enlisted soldiers.
    /// 
    /// Low-tier soldiers (T1-T3) don't receive personal loot - all goes to the lord.
    /// They are compensated through wages and gold share from battle spoils.
    /// 
    /// Veterans (T4+) have earned the privilege of keeping battle loot:
    /// - T4-T5: Allowed loot (veteran's share)
    /// - T6: Allowed loot (household guard picks first)
    /// - T7-T9: Allowed loot (commander privilege)
    /// 
    /// Uses a two-layer approach:
    /// 1. Data level: Redirects loot to empty/dummy rosters during battle processing
    /// 2. UI level: Skips the loot screen entirely
    /// 
    /// Loot IS allowed during leave or grace periods when operating independently.
    /// 
    /// CRITICAL: We return EMPTY rosters, not null. Returning null causes crashes in
    /// MapEvent.LootDefeatedPartyItems() which doesn't handle null rosters gracefully.
    /// </summary>
    // ReSharper disable once UnusedType.Global - Harmony patch classes discovered via reflection
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch classes discovered via reflection")]
    public static class LootBlockPatch
    {
        // Minimum tier required to receive personal loot (T4 = Veteran)
        private const int MinimumLootTier = 4;
        
        // Dummy rosters that receive loot but are never used
        // These prevent crashes while still blocking loot from going to player
        private static readonly ItemRoster _dummyItemRoster = new ItemRoster();
        private static readonly TroopRoster _dummyMemberRoster = TroopRoster.CreateDummyTroopRoster();
        private static readonly TroopRoster _dummyPrisonerRoster = TroopRoster.CreateDummyTroopRoster();
        
        private static bool ShouldBlockLoot()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return false; // Not enlisted - allow loot
            }
            
            // Allow loot when on leave or in grace period - player is operating independently
            if (enlistment.IsOnLeave || enlistment.IsInDesertionGracePeriod)
            {
                return false;
            }
            
            // Phase 4: Tier-gated loot - veterans (T4+) get personal loot
            // T1-T3: Blocked (compensated via gold share)
            // T4+: Allowed (earned the privilege)
            if (enlistment.EnlistmentTier >= MinimumLootTier)
            {
                ModLogger.Debug("LootBlock", $"Loot allowed - T{enlistment.EnlistmentTier} veteran privilege");
                return false; // Allow loot for veterans
            }
            
            return true; // Block loot for T1-T3 grunts
        }
        
        /// <summary>
        /// Log that loot was blocked and track the count for summary reporting.
        /// </summary>
        private static void LogLootBlocked(string lootType)
        {
            ModLogger.IncrementSummary("loot_blocked");
            ModLogger.Trace("Gold", $"Blocked {lootType} loot - enlisted soldiers don't receive personal loot");
        }
        
        /// <summary>
        /// Clears the dummy rosters periodically to prevent memory buildup.
        /// Called after battles complete.
        /// </summary>
        public static void ClearDummyRosters()
        {
            try
            {
                _dummyItemRoster?.Clear();
                _dummyMemberRoster?.Clear();
                _dummyPrisonerRoster?.Clear();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        #region Data Level - Block Loot Assignment

        /// <summary>
        /// Returns empty item roster, preventing item loot from reaching player.
        /// CRITICAL: Returns empty roster, not null - null causes crashes in LootDefeatedPartyItems.
        /// </summary>
        // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
        [HarmonyPatch(typeof(MapEventParty), nameof(MapEventParty.RosterToReceiveLootItems), MethodType.Getter)]
        public static class ItemLootPatch
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance and __result are special injected parameters")]
            private static bool Prefix(MapEventParty __instance, ref ItemRoster __result)
            {
                if (__instance.Party != PartyBase.MainParty)
                {
                    return true;
                }
                
                if (!ShouldBlockLoot())
                {
                    return true;
                }
                
                // Return empty dummy roster instead of null to prevent crashes
                __result = _dummyItemRoster;
                LogLootBlocked("item");
                return false;
            }
        }

        /// <summary>
        /// Returns empty troop roster, preventing troop loot from reaching player.
        /// CRITICAL: Returns empty roster, not null - null causes crashes in LootDefeatedPartyItems.
        /// </summary>
        // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
        [HarmonyPatch(typeof(MapEventParty), nameof(MapEventParty.RosterToReceiveLootMembers), MethodType.Getter)]
        public static class MemberLootPatch
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance and __result are special injected parameters")]
            private static bool Prefix(MapEventParty __instance, ref TroopRoster __result)
            {
                if (__instance.Party != PartyBase.MainParty)
                {
                    return true;
                }
                
                if (!ShouldBlockLoot())
                {
                    return true;
                }
                
                // Return empty dummy roster instead of null to prevent crashes
                __result = _dummyMemberRoster;
                LogLootBlocked("member");
                return false;
            }
        }

        /// <summary>
        /// Returns empty prisoner roster, preventing prisoner loot from reaching player.
        /// CRITICAL: Returns empty roster, not null - null causes crashes in LootDefeatedPartyItems.
        /// </summary>
        // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
        [HarmonyPatch(typeof(MapEventParty), nameof(MapEventParty.RosterToReceiveLootPrisoners), MethodType.Getter)]
        public static class PrisonerLootPatch
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance and __result are special injected parameters")]
            private static bool Prefix(MapEventParty __instance, ref TroopRoster __result)
            {
                if (__instance.Party != PartyBase.MainParty)
                {
                    return true;
                }
                
                if (!ShouldBlockLoot())
                {
                    return true;
                }
                
                // Return empty dummy roster instead of null to prevent crashes
                __result = _dummyPrisonerRoster;
                LogLootBlocked("prisoner");
                return false;
            }
        }

        #endregion

        #region UI Level - Skip Loot Screen

        /// <summary>
        /// Skips the loot screen and jumps directly to End state.
        /// This prevents crashes from banner/figurehead loot screens.
        /// Also clears dummy rosters to prevent memory buildup.
        /// </summary>
        // ReSharper disable once UnusedType.Global - Harmony patch class discovered via reflection
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
        [HarmonyPatch(typeof(PlayerEncounter), "DoLootParty")]
        public static class LootScreenPatch
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __instance is a special injected parameter")]
            private static bool Prefix(PlayerEncounter __instance)
            {
                try
                {
                    if (!ShouldBlockLoot())
                    {
                        return true;
                    }

                    ModLogger.Info("LootBlock", "Skipping loot screen - enlisted soldiers don't receive personal loot");
                    
                    // Clear dummy rosters to prevent memory buildup
                    ClearDummyRosters();
                    
                    // Skip ALL loot states (party, inventory, ships/figureheads) and go directly to End
                    var mapEventStateField = typeof(PlayerEncounter).GetField("_mapEventState",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (mapEventStateField != null)
                    {
                        mapEventStateField.SetValue(__instance, PlayerEncounterState.End);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    ModLogger.Caught("LootBlock", "Error in loot screen patch", ex);
                    return true; // Allow loot on error to prevent breaking gameplay
                }
            }
        }

        #endregion
    }
}
