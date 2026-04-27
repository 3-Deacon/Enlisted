using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// CampaignBehavior that owns PatronRoll lifecycle — construction, SyncData,
    /// EnsureInitialized, instance binding, and the HeroKilledEvent subscription
    /// that flips PatronEntry.IsDead. Discharge (mid-career) and full-retirement
    /// hooks live in EnlistmentBehavior, which calls PatronRoll.Instance methods
    /// directly — those event surfaces don't have a generic CampaignEvents hook
    /// to subscribe to.
    /// </summary>
    public sealed class PatronRollBehavior : CampaignBehaviorBase
    {
        private PatronRoll _store = new PatronRoll();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
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

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            _store?.OnHeroKilled(victim);
        }
    }
}
