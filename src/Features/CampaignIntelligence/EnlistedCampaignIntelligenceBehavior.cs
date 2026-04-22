using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Owns the EnlistedLordIntelligenceSnapshot lifecycle and the hourly recompute
    /// cadence. Handlers and persistence land in Phases B–F. Registered in SubModule
    /// via campaignStarter.AddBehavior in T7.
    /// </summary>
    public sealed class EnlistedCampaignIntelligenceBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore) { }
    }
}
