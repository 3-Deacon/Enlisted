using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Transient opportunity produced by EnlistedDutyOpportunityBuilder. Not
    /// persisted. The emitter picks one per tick, looks up a matching storylet
    /// from the pool, and emits via StoryDirector.
    /// </summary>
    public sealed class DutyOpportunity
    {
        public DutyOpportunityShape Shape { get; set; }
        public string PoolPrefix { get; set; } = string.Empty;
        public string ArchetypeStoryletId { get; set; }
        public float Priority { get; set; } = 1.0f;
        public CampaignTime AsOf { get; set; } = CampaignTime.Zero;

        /// <summary>
        /// Descriptor string identifying which snapshot state produced the
        /// opportunity. Surfaced in ModLogger + debug-hotkey inspection.
        /// </summary>
        public string TriggerReason { get; set; }
    }
}
