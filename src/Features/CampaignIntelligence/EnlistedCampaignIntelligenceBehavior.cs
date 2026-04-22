using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

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
        private bool _primedOnce;
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

        public override void RegisterEvents()
        {
            // Static events persist across Campaign teardown; guard against duplicate
            // subscription when RegisterEvents is re-invoked on save/load cycles.
            EnlistmentBehavior.OnEnlisted -= HandleOnEnlisted;
            EnlistmentBehavior.OnEnlisted += HandleOnEnlisted;
            EnlistmentBehavior.OnEnlistmentEnded -= HandleOnEnlistmentEnded;
            EnlistmentBehavior.OnEnlistmentEnded += HandleOnEnlistmentEnded;

            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, HandleHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, HandleDailyTick);
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, HandleWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(this, HandleMakePeace);
            CampaignEvents.ArmyCreated.AddNonSerializedListener(this, HandleArmyCreated);
            CampaignEvents.ArmyDispersed.AddNonSerializedListener(this, HandleArmyDispersed);
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, HandleMapEventStarted);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void HandleOnEnlisted(Hero lord)
        {
            try
            {
                EnsureInitialized();
                _pendingChangeFlags = RecentChangeFlags.None;
                _flagFirstSeen.Clear();
                _working.Clear();
                _primedOnce = false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTEL", "enlist_init_failed", ex);
            }
        }

        private void HandleOnEnlistmentEnded(string reason, bool voluntary)
        {
            try
            {
                _working?.Clear();
                _pendingChangeFlags = RecentChangeFlags.None;
                _flagFirstSeen.Clear();
                _primedOnce = false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("INTEL", "clear_on_end_failed", ex);
            }
        }

        private void HandleHourlyTick()
        {
            if (EnlistmentBehavior.Instance == null || !EnlistmentBehavior.Instance.IsEnlisted)
            {
                return;
            }

            var lord = EnlistmentBehavior.Instance.EnlistedLord;
            if (lord == null)
            {
                ModLogger.Expected("INTEL", "no_enlisted_lord", "hourly tick with IsEnlisted but EnlistedLord null");
                return;
            }

            try
            {
                EnsureInitialized();

                var pending = _pendingChangeFlags;
                _pendingChangeFlags = RecentChangeFlags.None;

                var inputs = SnapshotCollector.Collect(lord, pending);

                var fresh = new EnlistedLordIntelligenceSnapshot();
                fresh.Clear();

                // First real tick after enlist passes previous=null so DetectDeltas returns None.
                // Subsequent ticks pass _working so deltas are computed against the prior snapshot.
                var previousForDeltas = _primedOnce ? _working : null;
                SnapshotClassifier.Classify(ref inputs, previousForDeltas, fresh);
                fresh.LastRefreshed = CampaignTime.Now;

                // Record first-seen time for flag bits set this tick so the daily decay pass can clear bits older than 24h.
                var priorFlags = _primedOnce ? _working.RecentChanges : RecentChangeFlags.None;
                var newBits = fresh.RecentChanges & ~priorFlags;
                foreach (RecentChangeFlags bit in System.Enum.GetValues(typeof(RecentChangeFlags)))
                {
                    if (bit == RecentChangeFlags.None)
                    {
                        continue;
                    }
                    if ((newBits & bit) != 0)
                    {
                        _flagFirstSeen[bit] = CampaignTime.Now;
                    }
                }

                _working = fresh;
                _primedOnce = true;

                ModLogger.Expected("INTEL", "hourly_recompute",
                    "tick posture=" + fresh.Posture
                    + " obj=" + fresh.Objective
                    + " front=" + fresh.FrontPressure
                    + " strain=" + fresh.ArmyStrain
                    + " supply=" + fresh.SupplyPressure
                    + " flags=" + fresh.RecentChanges);
            }
            catch (System.Exception ex)
            {
                ModLogger.Caught("INTEL", "hourly_recompute_failed", ex);
            }
        }

        private void HandleDailyTick()
        {
            if (_working == null)
            {
                return;
            }

            var decayCutoff = CampaignTime.Now - CampaignTime.Days(1f);
            var stale = RecentChangeFlags.None;

            foreach (var kv in _flagFirstSeen)
            {
                if (kv.Value < decayCutoff)
                {
                    stale |= kv.Key;
                }
            }

            if (stale != RecentChangeFlags.None)
            {
                _working.RecentChanges &= ~stale;
                foreach (RecentChangeFlags bit in System.Enum.GetValues(typeof(RecentChangeFlags)))
                {
                    if (bit == RecentChangeFlags.None)
                    {
                        continue;
                    }
                    if ((stale & bit) != 0)
                    {
                        _flagFirstSeen.Remove(bit);
                    }
                }
            }
        }

        private void HandleWarDeclared(IFaction side1, IFaction side2, DeclareWarAction.DeclareWarDetail detail)
        {
            if (!IsRelevantFaction(side1) && !IsRelevantFaction(side2))
            {
                return;
            }
            _pendingChangeFlags |= RecentChangeFlags.WarDeclared | RecentChangeFlags.NewThreat;
        }

        private void HandleMakePeace(IFaction side1, IFaction side2, MakePeaceAction.MakePeaceDetail detail)
        {
            if (!IsRelevantFaction(side1) && !IsRelevantFaction(side2))
            {
                return;
            }
            _pendingChangeFlags |= RecentChangeFlags.MakePeace;
        }

        private void HandleArmyCreated(Army army)
        {
            if (!IsEnlistedLordArmy(army))
            {
                return;
            }
            _pendingChangeFlags |= RecentChangeFlags.ArmyFormed | RecentChangeFlags.ObjectiveShift;
        }

        private void HandleArmyDispersed(Army army, Army.ArmyDispersionReason reason, bool isPlayerArmy)
        {
            if (!IsEnlistedLordArmy(army))
            {
                return;
            }
            _pendingChangeFlags |= RecentChangeFlags.ArmyDispersed | RecentChangeFlags.DispersalRisk;
        }

        private void HandleMapEventStarted(MapEvent ev, PartyBase attacker, PartyBase defender)
        {
            var lordPartyBase = EnlistmentBehavior.Instance?.EnlistedLord?.PartyBelongedTo?.Party;
            if (lordPartyBase == null)
            {
                return;
            }
            if (attacker == lordPartyBase || defender == lordPartyBase)
            {
                _pendingChangeFlags |= RecentChangeFlags.MapEventJoined | RecentChangeFlags.NewThreat;
            }
        }

        private static bool IsRelevantFaction(IFaction faction)
        {
            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            if (lord == null || faction == null)
            {
                return false;
            }
            return faction == lord.MapFaction || faction == lord.Clan?.Kingdom;
        }

        private static bool IsEnlistedLordArmy(Army army)
        {
            var lord = EnlistmentBehavior.Instance?.EnlistedLord;
            if (lord == null || army == null)
            {
                return false;
            }
            if (army.LeaderParty?.LeaderHero == lord)
            {
                return true;
            }
            // Army.Parties enumeration — iterate if available. Some dispersion events may fire
            // with Parties already cleared; LeaderParty check above is the primary path.
            if (army.Parties != null)
            {
                for (int i = 0; i < army.Parties.Count; i++)
                {
                    if (army.Parties[i]?.LeaderHero == lord)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

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
