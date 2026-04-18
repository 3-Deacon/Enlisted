using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Bindings that let the RelevanceFilter decide whether a story candidate is about
    /// something the player cares about (their lord, their kingdom, a settlement they've
    /// visited, a hero they know, or a nearby world position). Sources set whatever
    /// fields apply; unset fields are safe defaults.
    /// </summary>
    public struct RelevanceKey
    {
        public Hero SubjectHero;
        public Hero ObjectHero;
        public Settlement SubjectSettlement;
        public Kingdom SubjectKingdom;
        public IFaction SubjectFaction;
        public Vec2 SubjectPosition;
        public bool TouchesEnlistedLord;
        public bool TouchesPlayerKingdom;

        public static RelevanceKey Empty => default;
    }
}
