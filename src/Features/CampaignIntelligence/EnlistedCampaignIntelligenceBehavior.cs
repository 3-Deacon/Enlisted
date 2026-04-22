using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Util;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.CampaignIntelligence
{
    /// <summary>
    /// Tracks the enlisted lord's strategic situation on an hourly cadence plus
    /// event-driven invalidation. Produces one EnlistedLordIntelligenceSnapshot
    /// exposed to consumers via <see cref="Current"/>. The accessor returns null
    /// when the player is not enlisted; interior state is not persisted across
    /// enlistments — the snapshot is recomputed from live world state each hour.
    /// </summary>
    [UsedImplicitly("Registered in SubModule.OnGameStart via campaignStarter.AddBehavior.")]
    public sealed class EnlistedCampaignIntelligenceBehavior : CampaignBehaviorBase
    {
        public static EnlistedCampaignIntelligenceBehavior Instance { get; private set; }

        private EnlistedLordIntelligenceSnapshot _working;
        private RecentChangeFlags _pendingChangeFlags;
        private Dictionary<RecentChangeFlags, CampaignTime> _flagFirstSeen
            = new Dictionary<RecentChangeFlags, CampaignTime>();

        public EnlistedCampaignIntelligenceBehavior()
        {
            Instance = this;
        }

        /// <summary>
        /// Read-only access to the most recent strategic snapshot. Returns null when
        /// the player is not currently enlisted, or when no tick has yet populated it.
        /// Consumers MUST treat the returned reference as read-only — mutating it from
        /// outside the behavior will desync the backbone.
        /// </summary>
        public EnlistedLordIntelligenceSnapshot Current
        {
            get
            {
                if (EnlistmentBehavior.Instance == null || !EnlistmentBehavior.Instance.IsEnlisted)
                {
                    return null;
                }
                return _working;
            }
        }

        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore) { }

        internal void EnsureInitialized()
        {
            if (_working == null)
            {
                _working = new EnlistedLordIntelligenceSnapshot();
                _working.Clear();
            }
            if (_flagFirstSeen == null)
            {
                _flagFirstSeen = new Dictionary<RecentChangeFlags, CampaignTime>();
            }
        }
    }
}
