using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.Managers;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Ensures quartermaster conversations use the correct scene (sea/land) based
    /// on the enlisted lord's party position. Without this, QM conversations
    /// default to land scenes even when the party is at sea.
    ///
    /// Patch retained even with QuartermasterPartyResolver available because some
    /// engine paths reach OpenMapConversation without going through our migrated
    /// call sites — this is the safety net.
    /// </summary>
    [HarmonyPatch(typeof(ConversationManager), "OpenMapConversation")]
    internal class QuartermasterConversationScenePatch
    {
        [HarmonyPrefix]
        public static void Prefix(
            ref ConversationCharacterData playerCharacterData,
            ref ConversationCharacterData conversationPartnerData)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return;
                }

                var qmHero = enlistment.GetOrCreateQuartermaster();
                if (qmHero == null || conversationPartnerData.Character != qmHero.CharacterObject)
                {
                    return; // Not a QM conversation
                }

                var effectiveParty = QuartermasterPartyResolver.GetConversationParty(qmHero);
                if (effectiveParty == null)
                {
                    ModLogger.Warn("Interface",
                        "QM conversation scene patch: resolver returned null — leaving partner data unchanged");
                    return;
                }

                conversationPartnerData = new ConversationCharacterData(
                    character: conversationPartnerData.Character,
                    party: effectiveParty,
                    noHorse: conversationPartnerData.NoHorse,
                    noWeapon: conversationPartnerData.NoWeapon,
                    spawnAfterFight: conversationPartnerData.SpawnedAfterFight,
                    isCivilianEquipmentRequiredForLeader: false,
                    isCivilianEquipmentRequiredForBodyGuardCharacters: false,
                    noBodyguards: conversationPartnerData.NoBodyguards
                );

                ModLogger.Debug("Interface",
                    "QM conversation scene fix: effective party resolved via QuartermasterPartyResolver");
            }
            catch (System.Exception ex)
            {
                ModLogger.Error("Interface", "Error in QuartermasterConversationScenePatch", ex);
            }
        }
    }
}
