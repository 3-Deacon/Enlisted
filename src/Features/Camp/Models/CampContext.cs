using System.Collections.Generic;
using Enlisted.Features.Content.Models;

namespace Enlisted.Features.Camp.Models
{
    /// <summary>
    /// Snapshot of camp context for opportunity generation.
    /// Combines day phase, muster cycle position, mood, and recent events.
    /// </summary>
    public class CampContext
    {
        /// <summary>Current phase of the military day (synced with Order System).</summary>
        public DayPhase DayPhase { get; set; }

        /// <summary>Days since last muster (0-12, resets at muster).</summary>
        public int DaysSinceLastMuster { get; set; }

        /// <summary>Current camp mood affecting opportunity selection.</summary>
        public CampMood CurrentMood { get; set; }

        /// <summary>Number of soldiers currently on duty (affects who's available).</summary>
        public int SoldiersOnDuty { get; set; }

        /// <summary>Number of soldiers off duty (potential participants).</summary>
        public int SoldiersOffDuty { get; set; }

        /// <summary>Recent camp events from the last 24-48 hours.</summary>
        public List<string> RecentEvents { get; set; } = new List<string>();

        /// <summary>Current activity level from world state.</summary>
        public ActivityLevel ActivityLevel { get; set; }

        /// <summary>Whether the player is currently on an order (on duty).</summary>
        public bool PlayerOnDuty { get; set; }

        /// <summary>Current world situation for context-aware filtering.</summary>
        public LordSituation LordSituation { get; set; }

        /// <summary>Whether the player is in the 3-day new enlistment grace period.</summary>
        public bool IsNewEnlistmentGrace { get; set; }

        /// <summary>Whether the player is currently on probation.</summary>
        public bool IsOnProbation { get; set; }

        /// <summary>Whether it's muster day (no opportunities).</summary>
        public bool IsMusterDay { get; set; }

        /// <summary>Current supply level (0-100) for filtering.</summary>
        public int SupplyLevel { get; set; }

        /// <summary>Whether the player is injured.</summary>
        public bool PlayerInjured { get; set; }

        /// <summary>Player's current gold.</summary>
        public int PlayerGold { get; set; }

        /// <summary>Whether in the 6-hour baggage window post-muster.</summary>
        public bool InBaggageWindow { get; set; }
    }
}
