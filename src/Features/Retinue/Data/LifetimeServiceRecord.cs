using System;
using System.Collections.Generic;

namespace Enlisted.Features.Retinue.Data
{
    /// <summary>
    /// Cross-faction lifetime statistics. Aggregates service across all factions.
    /// </summary>
    [Serializable]
    public class LifetimeServiceRecord
    {
        /// <summary>All enemies slain while enlisted (any faction).</summary>
        public int LifetimeKills { get; set; }

        /// <summary>Total days served across all factions.</summary>
        public int TotalDaysServed { get; set; }

        /// <summary>Full contract terms completed (any faction).</summary>
        public int TermsCompleted { get; set; }

        /// <summary>Total enlistments started (any faction).</summary>
        public int TotalEnlistments { get; set; }

        /// <summary>Total battles fought (any faction).</summary>
        public int TotalBattlesFought { get; set; }

        /// <summary>Faction IDs the player has served (for "Factions Served" display).</summary>
        public List<string> FactionsServed { get; set; }

        public LifetimeServiceRecord()
        {
            FactionsServed = new List<string>();
        }

        /// <summary>Adds a faction to the served list if not already present.</summary>
        public void AddFactionServed(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
            {
                return;
            }

            if (!FactionsServed.Contains(factionId))
            {
                FactionsServed.Add(factionId);
            }
        }

        /// <summary>Returns years and months from total days served.</summary>
        public (int years, int months) GetServiceDuration()
        {
            // Bannerlord: ~84 days per season, ~336 days per year
            const int daysPerYear = 336;
            const int daysPerMonth = 28;

            int years = TotalDaysServed / daysPerYear;
            int remainingDays = TotalDaysServed % daysPerYear;
            int months = remainingDays / daysPerMonth;

            return (years, months);
        }

        public override string ToString()
        {
            var (years, months) = GetServiceDuration();
            return $"Lifetime: {years}y {months}m, {TermsCompleted} terms, " +
                   $"{TotalBattlesFought} battles, {LifetimeKills} kills, " +
                   $"{FactionsServed.Count} factions";
        }
    }
}

