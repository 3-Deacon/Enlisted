using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.PersonalKit
{
    /// <summary>
    /// Hourly + daily tick handler that (in Plan 7) reads QualityStore slots
    /// 'kit_bedroll_level' / 'kit_sharpening_stone_level' / 'kit_field_kit_level'
    /// and applies the corresponding bonuses (XP drift, healing rate, morale).
    /// State lives in QualityStore — no own save state, no offset claimed.
    /// Plan 1 ships the wired tick seams; Plan 7 layers bonus-application logic
    /// and the QM dialog catalog that lets the player buy upgrades.
    /// </summary>
    public sealed class PersonalKitTickHandler : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnHourlyTick()
        {
        }

        private void OnDailyTick()
        {
        }
    }
}
