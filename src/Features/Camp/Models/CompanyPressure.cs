namespace Enlisted.Features.Camp.Models
{
    /// <summary>
    /// Tracks pressure build-up for crisis triggers.
    /// Consecutive days of bad conditions lead to escalating events.
    /// </summary>
    public class CompanyPressure
    {
        // Days of consecutive low values (0-100 scale, "low" typically means below 40)
        public int DaysLowSupplies { get; set; }
        // Note: DaysLowMorale removed (morale system no longer exists)
        // Note: DaysLowRest removed 2026-01-11 (Rest system removed)
        public int DaysLowDiscipline { get; set; }

        // Count of desertions in recent days (decays over time)
        public int RecentDesertions { get; set; }

        // Days with high sickness rate (>20% of company sick)
        public int DaysHighSickness { get; set; }

        /// <summary>
        /// Resets all pressure counters. Called when starting fresh or changing lords.
        /// </summary>
        public void Reset()
        {
            DaysLowSupplies = 0;
            // Note: DaysLowMorale removed (morale system no longer exists)
            // Note: DaysLowRest removed 2026-01-11 (Rest system removed)
            DaysLowDiscipline = 0;
            RecentDesertions = 0;
            DaysHighSickness = 0;
        }

        /// <summary>
        /// Gets a simple pressure score for diagnostics (0-100).
        /// </summary>
        public int GetPressureScore()
        {
            int score = 0;
            score += DaysLowSupplies * 8;
            // Note: DaysLowMorale removed (morale system no longer exists)
            // Note: DaysLowRest removed 2026-01-11 (Rest system removed)
            score += DaysLowDiscipline * 6;
            score += RecentDesertions * 4;
            score += DaysHighSickness * 7;

            return score > 100 ? 100 : score;
        }

        /// <summary>
        /// Indicates whether any crisis threshold is close to triggering.
        /// </summary>
        public bool IsCrisisImminent()
        {
            return DaysLowSupplies >= 2 ||
                   // Note: DaysLowMorale removed (morale system no longer exists)
                   // Note: DaysLowRest removed 2026-01-11 (Rest system removed)
                   RecentDesertions >= 4 ||
                   DaysHighSickness >= 2;
        }
    }
}
