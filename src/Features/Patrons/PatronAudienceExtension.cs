using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// Dialog-condition helpers that resolve the current 1:1 conversation target
    /// against the PatronRoll. Used by EnlistedDialogManager.AddPatronDialogs to
    /// gate the "call in a favor" branch on whether the lord the player is talking
    /// to is actually a former patron.
    /// </summary>
    public static class PatronAudienceExtension
    {
        public static bool IsConversationTargetPatron()
        {
            var target = Hero.OneToOneConversationHero;
            if (target == null)
            {
                return false;
            }
            var roll = PatronRoll.Instance;
            return roll != null && roll.Has(target.Id);
        }

        public static PatronEntry GetEntryForTarget()
        {
            var target = Hero.OneToOneConversationHero;
            if (target == null)
            {
                return null;
            }
            return PatronRoll.Instance?.GetEntry(target.Id);
        }
    }
}
