using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Lifestyles
{
    /// <summary>
    /// CampaignBehavior that owns LifestyleUnlockStore lifecycle — construction,
    /// SyncData, EnsureInitialized, instance binding. Plan 7 layers milestone
    /// hooks + lifestyle-path catalog; Plan 1 ships the empty host.
    /// </summary>
    public sealed class LifestyleUnlockBehavior : CampaignBehaviorBase
    {
        private LifestyleUnlockStore _store = new LifestyleUnlockStore();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                _ = dataStore.SyncData("_lifestyleUnlockStore", ref _store);
                _store ??= new LifestyleUnlockStore();
                _store.EnsureInitialized();
                LifestyleUnlockStore.SetInstance(_store);
            });
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _store.EnsureInitialized();
            LifestyleUnlockStore.SetInstance(_store);
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _store.EnsureInitialized();
            LifestyleUnlockStore.SetInstance(_store);
        }
    }
}
