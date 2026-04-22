using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Owns the EnlistedLordIntelligenceSnapshot lifecycle and the hourly recompute
    /// cadence. Registered in SubModule via campaignStarter.AddBehavior.
    /// </summary>
    public sealed class EnlistedCampaignIntelligenceBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore) { }
    }
}
