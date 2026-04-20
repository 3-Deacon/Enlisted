using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Camp.Models
{
    /// <summary>
    /// Tracks opportunity presentation and engagement history.
    /// Used for variety maintenance and player preference learning.
    /// </summary>
    [Serializable]
    public class OpportunityHistory
    {
        /// <summary>Last time each opportunity type was presented.</summary>
        public Dictionary<string, float> LastPresentedHours { get; set; } = new Dictionary<string, float>();

        /// <summary>Number of times each opportunity type has been seen.</summary>
        public Dictionary<string, int> TimesSeen { get; set; } = new Dictionary<string, int>();

        /// <summary>Number of times player engaged with each opportunity type.</summary>
        public Dictionary<string, int> TimesEngaged { get; set; } = new Dictionary<string, int>();

        /// <summary>Number of times player ignored each opportunity type.</summary>
        public Dictionary<string, int> TimesIgnored { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets the engagement rate for a specific opportunity type.
        /// Returns 0.5 if no data available.
        /// </summary>
        public float GetEngagementRate(string opportunityType)
        {
            int seen = TimesSeen.TryGetValue(opportunityType, out var s) ? s : 0;
            int engaged = TimesEngaged.TryGetValue(opportunityType, out var e) ? e : 0;

            return seen > 0 ? (float)engaged / seen : 0.5f;
        }

        /// <summary>
        /// Gets hours since this opportunity type was last presented.
        /// Returns a large value if never presented.
        /// </summary>
        public float HoursSincePresented(string opportunityType)
        {
            if (!LastPresentedHours.TryGetValue(opportunityType, out var lastHour))
            {
                return 999f;
            }

            float currentHour = (float)CampaignTime.Now.ToHours;
            return currentHour - lastHour;
        }

        /// <summary>
        /// Records that an opportunity was presented to the player.
        /// </summary>
        public void RecordPresented(string opportunityId, string opportunityType)
        {
            float currentHour = (float)CampaignTime.Now.ToHours;
            LastPresentedHours[opportunityType] = currentHour;
            LastPresentedHours[opportunityId] = currentHour;

            if (!TimesSeen.ContainsKey(opportunityType))
            {
                TimesSeen[opportunityType] = 0;
            }
            TimesSeen[opportunityType]++;
        }

        /// <summary>
        /// Records that the player engaged with an opportunity.
        /// Updates last presented time to start cooldown from engagement.
        /// </summary>
        public void RecordEngaged(string opportunityId, string opportunityType)
        {
            if (!TimesEngaged.ContainsKey(opportunityType))
            {
                TimesEngaged[opportunityType] = 0;
            }
            TimesEngaged[opportunityType]++;

            // Update last presented time to start the cooldown from engagement
            // This prevents the opportunity from reappearing immediately
            float currentHour = (float)CampaignTime.Now.ToHours;
            LastPresentedHours[opportunityType] = currentHour;
            LastPresentedHours[opportunityId] = currentHour;
        }

        /// <summary>
        /// Records that the player ignored an opportunity.
        /// </summary>
        public void RecordIgnored(string opportunityId, string opportunityType)
        {
            _ = opportunityId;
            if (!TimesIgnored.ContainsKey(opportunityType))
            {
                TimesIgnored[opportunityType] = 0;
            }
            TimesIgnored[opportunityType]++;
        }

        /// <summary>
        /// Checks if a specific opportunity was shown recently (within cooldown).
        /// </summary>
        public bool WasRecentlyShown(string opportunityId, int cooldownHours)
        {
            if (!LastPresentedHours.TryGetValue(opportunityId, out var lastHour))
            {
                return false;
            }

            float currentHour = (float)CampaignTime.Now.ToHours;
            return (currentHour - lastHour) < cooldownHours;
        }
    }
}
