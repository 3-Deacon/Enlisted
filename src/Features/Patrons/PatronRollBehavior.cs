using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// CampaignBehavior that owns PatronRoll lifecycle — construction, SyncData,
    /// EnsureInitialized, instance binding. Plan 6 layers discharge / OnHeroKilled /
    /// favor-grant logic onto this behavior; Plan 1 ships the empty host.
    /// </summary>
    public sealed class PatronRollBehavior : CampaignBehaviorBase
    {
        private PatronRoll _store = new PatronRoll();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                _ = dataStore.SyncData("_patronRoll", ref _store);
                _store ??= new PatronRoll();
                _store.EnsureInitialized();
                PatronRoll.SetInstance(_store);
            });
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _store.EnsureInitialized();
            PatronRoll.SetInstance(_store);
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _store.EnsureInitialized();
            PatronRoll.SetInstance(_store);
        }
    }
}
