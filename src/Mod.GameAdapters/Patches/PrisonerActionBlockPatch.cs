using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

// ReSharper disable UnusedType.Global - Harmony patches are applied via attributes, not direct code references
// ReSharper disable UnusedMember.Local - Harmony Prefix/Postfix methods are invoked via reflection
// ReSharper disable InconsistentNaming - __instance/__result are Harmony naming conventions
namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    ///     Prevents enlisted soldiers from executing or releasing prisoner lords.
    ///
    ///     As a soldier in service, the player has no authority to decide the fate of captured
    ///     enemy lords. Such decisions belong to the commanding lord, not common soldiers.
    ///     This patch blocks:
    ///     1. Execute button in party screen (PartyScreenLogic.IsExecutable)
    ///     2. "Let prisoner go" conversation option (LordConversationsCampaignBehavior conditions)
    ///
    ///     Prisoner actions ARE allowed during leave or grace periods when operating independently.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch classes discovered via reflection")]
    public static class PrisonerActionBlockPatch
    {
        /// <summary>
        ///     Checks if the player is enlisted and should be blocked from prisoner actions.
        /// </summary>
        private static bool ShouldBlockPrisonerActions()
        {
            // If the mod is not active (e.g., not enlisted playthrough), skip entirely
            if (!EnlistedActivation.IsActive)
            {
                return false;
            }

            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.IsEnlisted != true)
            {
                return false; // Not enlisted - allow prisoner actions
            }

            // Allow prisoner actions when on leave or in grace period - player is operating independently
            if (enlistment.IsOnLeave || enlistment.IsInDesertionGracePeriod)
            {
                return false;
            }

            return true; // Actively serving - block prisoner actions
        }

        #region Party Screen - Block Execute Button

        /// <summary>
        ///     Prevents the Execute button from appearing in party screen when enlisted.
        ///     As a soldier, you have no authority to execute captured lords.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
        [HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.IsExecutable))]
        public static class BlockExecutePatch
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
            private static bool Prefix(ref bool __result, CharacterObject character)
            {
                if (!ShouldBlockPrisonerActions())
                {
                    return true; // Let original method run
                }

                // Only log for hero prisoners (the ones that would actually show execute option)
                if (character?.IsHero == true)
                {
                    ModLogger.Debug(
                        "PrisonerAction",
                        $"Blocked execute option for {character.Name} - enlisted soldiers cannot execute prisoners");
                }

                __result = false;
                return false; // Skip original method
            }
        }

        #endregion

        #region Conversations - Block Release Options

        /// <summary>
        ///     Blocks the "I have decided to free you" conversation option when enlisted.
        ///     Targets: LordConversationsCampaignBehavior.conversation_player_let_prisoner_go_on_condition
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
        [HarmonyPatch("TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior", "conversation_player_let_prisoner_go_on_condition")]
        public static class BlockReleasePrisonerConversationPatch
        {
        }

        /// <summary>
        ///     Blocks "I need to leave. Good-bye, for now." option when talking to enemy prisoners.
        ///     This prevents the indirect release through conversation ending.
        ///     Targets: LordConversationsCampaignBehavior.conversation_player_is_leaving_enemy_prisoner_on_condition
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
        [HarmonyPatch("TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior", "conversation_player_is_leaving_enemy_prisoner_on_condition")]
        public static class BlockLeavePrisonerConversationPatch
        {
        }

        #endregion
    }
}

