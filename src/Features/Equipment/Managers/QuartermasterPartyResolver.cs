using Enlisted.Features.Enlistment.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Equipment.Managers
{
    /// <summary>
    /// Resolves the effective party for a conversation with the quartermaster hero.
    ///
    /// The QM is spawned via HeroCreator.CreateSpecialHero and parked in the
    /// enlisted lord's home settlement — it has no MobileParty of its own
    /// (qm.PartyBelongedTo is always null). Bannerlord's conversation scene
    /// selection (land / sea / terrain) reads the party's position, so passing
    /// null produces degraded scene selection.
    ///
    /// This resolver substitutes the enlisted lord's party, giving every call
    /// site the same "who is the QM's effective party" answer and making scene
    /// selection consistent.
    ///
    /// Decompile reference:
    /// ConversationCharacterData (../Decompile/TaleWorlds.CampaignSystem/
    /// TaleWorlds.CampaignSystem.Conversation/ConversationCharacterData.cs:27)
    /// tolerates a null PartyBase without throwing — but a null-party
    /// conversation has degraded scene selection.
    /// </summary>
    public static class QuartermasterPartyResolver
    {
        /// <summary>
        /// Returns a non-null party for a conversation with the QM hero, or null
        /// if both the QM and the enlisted lord are unreachable (illegal state
        /// during enlistment).
        ///
        /// Callers that receive null MUST log E-QM-PARTY-001 and abort the
        /// conversation open; do not pass null into ConversationCharacterData.
        /// </summary>
        public static PartyBase GetConversationParty(Hero qmHero)
        {
            if (qmHero?.PartyBelongedTo?.Party is { } qmParty)
            {
                return qmParty;
            }

            var enlistment = EnlistmentBehavior.Instance;
            return enlistment?.EnlistedLord?.PartyBelongedTo?.Party;
        }
    }
}
