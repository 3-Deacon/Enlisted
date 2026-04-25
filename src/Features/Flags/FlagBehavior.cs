using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Flags
{
    /// <summary>CampaignBehavior that owns FlagStore lifecycle + SyncData + hourly expiry sweep.</summary>
    public sealed class FlagBehavior : CampaignBehaviorBase
    {
        private FlagStore _store = new FlagStore();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                _ = dataStore.SyncData("_flagStore", ref _store);
                if (_store == null)
                {
                    _store = new FlagStore();
                }
                _store.EnsureInitialized();
                FlagStore.SetInstance(_store);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FLAGS", "FlagBehavior SyncData failed", ex);
                _store = new FlagStore();
                FlagStore.SetInstance(_store);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _store.EnsureInitialized();
            FlagStore.SetInstance(_store);
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            try
            {
                _store.EnsureInitialized();
                _store.PurgeMissingHeroes();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FLAGS", "FlagStore load-time purge failed", ex);
            }
            FlagStore.SetInstance(_store);
        }

        private void OnHourlyTick()
        {
            try
            {
                _store.PurgeExpired(CampaignTime.Now);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("FLAGS", "FlagStore hourly purge failed", ex);
            }
        }
    }
}
