using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.Patrons
{
    /// <summary>
    /// Player-relationship-history store. Accumulates one PatronEntry per former
    /// lord across the active mod run; cleared on full retirement (mod-silences
    /// boundary). Per-hero dedup is by MBGUID at insert time. Plan 6 wires the
    /// discharge hook + favor-grant pipeline; Plan 1 ships the empty container
    /// and lifecycle scaffolding.
    /// </summary>
    [Serializable]
    public sealed class PatronRoll
    {
        public static PatronRoll Instance { get; private set; }

        public List<PatronEntry> Entries { get; set; } = new List<PatronEntry>();

        internal static void SetInstance(PatronRoll instance) => Instance = instance;

        /// <summary>
        /// Re-seats null list field after deserialization. Save data produced
        /// before the field existed deserializes with Entries left as null —
        /// field initializers don't run on the deserialization path that skips
        /// the ctor. Call after SyncData and on load.
        /// </summary>
        public void EnsureInitialized()
        {
            if (Entries == null)
            {
                Entries = new List<PatronEntry>();
            }
        }

        public bool Has(MBGUID heroId)
        {
            EnsureInitialized();
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].HeroId == heroId)
                {
                    return true;
                }
            }
            return false;
        }

        public PatronEntry GetEntry(MBGUID heroId)
        {
            EnsureInitialized();
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].HeroId == heroId)
                {
                    return Entries[i];
                }
            }
            return null;
        }

        public void Clear()
        {
            EnsureInitialized();
            Entries.Clear();
        }

        /// <summary>
        /// Records a patron entry on mid-career discharge. Re-discharge of the
        /// same lord updates the existing entry (cumulative DaysServed; cooldowns
        /// preserved) rather than duplicating. Caller (EnlistmentBehavior) must
        /// invoke before nulling _enlistedLord / _enlistmentDate so DaysServed
        /// captures correctly. Full retirement does NOT call this — that path
        /// goes through Clear() instead, per spec §0 mod-silence boundary.
        /// </summary>
        public void OnDischarge(Hero lord, string dischargeBand)
        {
            if (lord == null)
            {
                return;
            }

            EnsureInitialized();

            var enlistment = EnlistmentBehavior.Instance;
            int freshDaysServed = enlistment != null ? (int)enlistment.DaysServed : 0;
            int maxRank = enlistment?.EnlistmentTier ?? 0;

            var entry = new PatronEntry
            {
                HeroId = lord.Id,
                DaysServed = freshDaysServed,
                MaxRankReached = maxRank,
                DischargedOn = CampaignTime.Now,
                LastFavorRequestedOn = CampaignTime.Zero,
                PerFavorCooldownsCsv = string.Empty,
                RelationSnapshotOnDischarge = (int)lord.GetRelationWithPlayer(),
                IsDead = !lord.IsAlive,
                FactionAtDischarge = lord.MapFaction?.StringId ?? string.Empty
            };

            int existingIdx = -1;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].HeroId == lord.Id)
                {
                    existingIdx = i;
                    break;
                }
            }

            if (existingIdx >= 0)
            {
                var prior = Entries[existingIdx];
                entry.DaysServed += prior.DaysServed;
                entry.PerFavorCooldownsCsv = prior.PerFavorCooldownsCsv ?? string.Empty;
                Entries[existingIdx] = entry;
            }
            else
            {
                Entries.Add(entry);
            }

            ModLogger.Expected("PATRONS", "discharge_recorded",
                "patron entry recorded on discharge",
                LogCtx.Of(
                    "lordId", lord.StringId,
                    "daysServed", entry.DaysServed,
                    "tier", entry.MaxRankReached,
                    "band", dischargeBand ?? string.Empty));
        }

        /// <summary>
        /// Marks the patron entry for victim as dead. Called by PatronRollBehavior
        /// from CampaignEvents.HeroKilledEvent. Dead patrons stay on the Roll
        /// (audience flow greys them) but cannot grant favors — see
        /// PatronFavorResolver.TryGrantFavor.
        /// </summary>
        public void OnHeroKilled(Hero victim)
        {
            if (victim == null)
            {
                return;
            }
            EnsureInitialized();
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].HeroId == victim.Id && !Entries[i].IsDead)
                {
                    Entries[i].IsDead = true;
                    ModLogger.Expected("PATRONS", "patron_died",
                        "patron entry marked dead on hero kill",
                        LogCtx.Of("victimId", victim.StringId));
                    return;
                }
            }
        }

        /// <summary>
        /// Returns alive patron entries whose mapped Hero is currently within
        /// maxDistance (campaign-map units) of the player MainParty. Heroes
        /// without a current party fall back to their HomeSettlement position.
        /// Returns an empty enumeration if MainParty or MBObjectManager isn't
        /// ready (early boot / no campaign).
        /// </summary>
        public IEnumerable<PatronEntry> AvailableNearby(float maxDistance = 2.0f)
        {
            EnsureInitialized();
            var mainParty = MobileParty.MainParty;
            if (mainParty == null || MBObjectManager.Instance == null)
            {
                yield break;
            }
            Vec2 mainPos = mainParty.GetPosition2D;
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry.IsDead)
                {
                    continue;
                }
                var hero = MBObjectManager.Instance.GetObject(entry.HeroId) as Hero;
                if (hero == null || !hero.IsAlive)
                {
                    continue;
                }
                Vec2 heroPos;
                if (hero.PartyBelongedTo != null)
                {
                    heroPos = hero.PartyBelongedTo.GetPosition2D;
                }
                else if (hero.CurrentSettlement != null)
                {
                    heroPos = hero.CurrentSettlement.GetPosition2D;
                }
                else if (hero.HomeSettlement != null)
                {
                    heroPos = hero.HomeSettlement.GetPosition2D;
                }
                else
                {
                    continue;
                }
                if (mainPos.Distance(heroPos) <= maxDistance)
                {
                    yield return entry;
                }
            }
        }
    }
}
