using System;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Mod.Core.Logging
{
    /// <summary>Writes the content catalog runtime counts to the conflict log once per session, after catalogs have initialized.</summary>
    public sealed class RuntimeCatalogStatusMarkerBehavior : CampaignBehaviorBase
    {
        private bool _logged;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (_logged) { return; }
            _logged = true;
            ModConflictDiagnostics.LogRuntimeCatalogStatus();
        }
    }
}
