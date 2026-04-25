using System;
using System.Collections.Generic;
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

        public void Clear()
        {
            EnsureInitialized();
            Entries.Clear();
        }
    }
}
