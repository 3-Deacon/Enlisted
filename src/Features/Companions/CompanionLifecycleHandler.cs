using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Enlisted.Features.Companions
{
    /// <summary>
    /// Hosts companion-substrate event subscriptions and provides a singleton
    /// accessor for UI surfaces (Camp menu Talk-to inquiry). EnlistmentBehavior
    /// owns the SaveableField storage and the GetOrCreateX / ClearCompanionSlot /
    /// ReleasePerLordCompanions methods; this behavior decides *when* to call them.
    /// </summary>
    public sealed class CompanionLifecycleHandler : CampaignBehaviorBase
    {
        public static CompanionLifecycleHandler Instance { get; private set; }

        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);

            EnlistmentBehavior.OnEnlisted += OnEnlisted;
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
            EnlistmentBehavior.OnDischarged += OnDischarged;
            EnlistmentBehavior.OnEnlistmentEnded += OnEnlistmentEnded;
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No own save state. Companion fields live on EnlistmentBehavior.
        }

        public List<Hero> GetSpawnedCompanions()
        {
            var enlistment = EnlistmentBehavior.Instance;
            return enlistment != null ? enlistment.GetSpawnedCompanions() : new List<Hero>();
        }

        // ---------- Session lifecycle ----------

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            CompanionSpawnFactory.LoadCatalog();
            EnlistmentBehavior.Instance?.EnsureCompanionFieldsInitialized();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            EnlistmentBehavior.Instance?.EnsureCompanionFieldsInitialized();

            // If a save predates Plan 2 and the player is already enlisted at a
            // higher tier, backfill any unlocked companions whose slots are still
            // null. Spawn order matches the unlock-tier ordering.
            TrySpawnAtCurrentTier();
        }

        // ---------- Enlistment / tier hooks ----------

        private void OnEnlisted(Hero lord)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return;
                }

                // T1 unlock — Sergeant. Always re-evaluated on every enlistment;
                // per-player slot persists if already populated.
                enlistment.GetOrCreateSergeant();

                // Backfill higher-tier slots when the player rejoins service at a
                // tier above the unlock floor (e.g. discharged at T5, re-enlists
                // at T5 — Veteran needs to re-spawn for the new lord).
                TrySpawnAtCurrentTier();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION", "on_enlisted_failed", ex);
            }
        }

        private void OnTierChanged(int previousTier, int newTier)
        {
            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null || !enlistment.IsEnlisted)
                {
                    return;
                }

                if (newTier >= 3 && previousTier < 3)
                {
                    enlistment.GetOrCreateFieldMedic();
                    enlistment.GetOrCreatePathfinder();
                }
                if (newTier >= 5 && previousTier < 5)
                {
                    enlistment.GetOrCreateVeteran();
                }
                if (newTier >= 7 && previousTier < 7)
                {
                    enlistment.GetOrCreateQmOfficer();
                    enlistment.GetOrCreateJuniorOfficer();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION", "on_tier_changed_failed", ex);
            }
        }

        private void TrySpawnAtCurrentTier()
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                return;
            }

            try
            {
                var tier = enlistment.EnlistmentTier;
                if (tier >= 1)
                {
                    enlistment.GetOrCreateSergeant();
                }
                if (tier >= 3)
                {
                    enlistment.GetOrCreateFieldMedic();
                    enlistment.GetOrCreatePathfinder();
                }
                if (tier >= 5)
                {
                    enlistment.GetOrCreateVeteran();
                }
                if (tier >= 7)
                {
                    enlistment.GetOrCreateQmOfficer();
                    enlistment.GetOrCreateJuniorOfficer();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION", "backfill_at_tier_failed", ex);
            }
        }

        // ---------- Discharge hooks ----------

        private void OnDischarged(string reason)
        {
            try
            {
                EnlistmentBehavior.Instance?.ReleasePerLordCompanions();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION", "on_discharged_failed", ex);
            }
        }

        private void OnEnlistmentEnded(string reason, bool isHonorable)
        {
            // ReleasePerLordCompanions is idempotent — calling it from both
            // OnDischarged and OnEnlistmentEnded is safe (ReleaseSlot null-guards).
            try
            {
                EnlistmentBehavior.Instance?.ReleasePerLordCompanions();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION", "on_enlistment_ended_failed", ex);
            }
        }

        // ---------- Death cleanup ----------

        private void OnHeroKilled(Hero victim, Hero killer,
            KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            if (victim == null) return;

            try
            {
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment == null) return;

                var typeId = enlistment.GetCompanionTypeId(victim);
                if (string.IsNullOrEmpty(typeId)) return;

                if (enlistment.ClearCompanionSlot(victim))
                {
                    ModLogger.Surfaced("COMPANION", "companion_killed_in_battle", null,
                        LogCtx.Of("typeId", typeId, "name", victim.Name?.ToString() ?? "?"));
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMPANION", "on_hero_killed_failed", ex);
            }
        }
    }
}
