using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Camp.Models
{
    /// <summary>
    /// Represents a single scheduled commitment to an activity.
    /// </summary>
    [Serializable]
    public class ScheduledCommitment
    {
        /// <summary>ID of the opportunity (e.g., "opp_sparring_match").</summary>
        public string OpportunityId { get; set; }

        /// <summary>ID of the target decision to fire.</summary>
        public string TargetDecisionId { get; set; }

        /// <summary>Title of the opportunity for display.</summary>
        public string Title { get; set; }

        /// <summary>Phase when this should fire (Dawn/Midday/Dusk/Night).</summary>
        public string ScheduledPhase { get; set; }

        /// <summary>Day number when this should fire.</summary>
        public int ScheduledDay { get; set; }

        /// <summary>Campaign time when commitment was made (for persistence).</summary>
        public float CommitTimeHours { get; set; }

        /// <summary>Display text for the commitment.</summary>
        public string DisplayText { get; set; }
    }

    /// <summary>
    /// Tracks player's scheduled activity commitments.
    /// Supports multiple commitments queued by phase.
    /// </summary>
    [Serializable]
    public class PlayerCommitments
    {
        /// <summary>List of scheduled commitments.</summary>
        public List<ScheduledCommitment> Commitments { get; set; } = new List<ScheduledCommitment>();

        // Legacy fields for backwards compatibility
        /// <summary>ID of the scheduled activity (null if no commitment).</summary>
        [Obsolete("Use Commitments list instead")]
        public string ScheduledActivityId { get; set; }

        /// <summary>Time when the activity is scheduled (null if no commitment).</summary>
        [Obsolete("Use Commitments list instead")]
        public float? ScheduledTimeHours { get; set; }

        /// <summary>Display text for the YOU section (e.g., "You've agreed to join the card game tonight").</summary>
        [Obsolete("Use Commitments list instead")]
        public string CommitmentDisplayText { get; set; }

        /// <summary>Whether the player has any active commitments.</summary>
#pragma warning disable CS0618 // Intentional: checking legacy field for backwards compatibility
        public bool HasCommitment => Commitments.Count > 0 || !string.IsNullOrEmpty(ScheduledActivityId);
#pragma warning restore CS0618

        /// <summary>
        /// Adds a new scheduled commitment.
        /// </summary>
        public void AddCommitment(string opportunityId, string targetDecisionId, string title,
            string scheduledPhase, int scheduledDay, string displayText)
        {
            // Remove any existing commitment for the same opportunity
            _ = Commitments.RemoveAll(c => c.OpportunityId == opportunityId);

            var commitment = new ScheduledCommitment
            {
                OpportunityId = opportunityId,
                TargetDecisionId = targetDecisionId,
                Title = title,
                ScheduledPhase = scheduledPhase,
                ScheduledDay = scheduledDay,
                CommitTimeHours = (float)CampaignTime.Now.ToHours,
                DisplayText = displayText
            };

            Commitments.Add(commitment);
        }

        /// <summary>
        /// Removes a commitment by opportunity ID.
        /// </summary>
        public bool RemoveCommitment(string opportunityId)
        {
            var count = Commitments.RemoveAll(c => c.OpportunityId == opportunityId);
            return count > 0;
        }

        /// <summary>
        /// Gets a commitment by opportunity ID.
        /// </summary>
        public ScheduledCommitment GetCommitment(string opportunityId)
        {
            return Commitments.FirstOrDefault(c => c.OpportunityId == opportunityId);
        }

        /// <summary>
        /// Checks if the player is committed to a specific opportunity.
        /// </summary>
        public bool IsCommittedTo(string opportunityId)
        {
            return Commitments.Any(c => c.OpportunityId == opportunityId);
        }

        /// <summary>
        /// Gets commitments that should fire for a given phase and day.
        /// </summary>
        public List<ScheduledCommitment> GetCommitmentsForPhase(string phase, int day)
        {
            return Commitments
                .Where(c => c.ScheduledPhase == phase && c.ScheduledDay == day)
                .OrderBy(c => c.CommitTimeHours)
                .ToList();
        }

        /// <summary>
        /// Gets the next commitment to fire (closest in time).
        /// </summary>
        public ScheduledCommitment GetNextCommitment()
        {
            return Commitments
                .OrderBy(c => c.ScheduledDay)
                .ThenBy(c => GetPhaseOrder(c.ScheduledPhase))
                .FirstOrDefault();
        }

        /// <summary>
        /// Clears all commitments.
        /// </summary>
        public void ClearAllCommitments()
        {
            Commitments.Clear();
#pragma warning disable CS0618 // Intentional: clearing legacy fields for backwards compatibility
            ScheduledActivityId = null;
            ScheduledTimeHours = null;
            CommitmentDisplayText = null;
#pragma warning restore CS0618
        }

        /// <summary>
        /// Calculates hours until a commitment fires.
        /// </summary>
        public float GetHoursUntilCommitment(ScheduledCommitment commitment)
        {
            if (commitment == null)
            {
                return 0f;
            }

            var currentDay = (int)CampaignTime.Now.ToDays;
            var currentHour = CampaignTime.Now.GetHourOfDay;

            var targetHour = GetPhaseHour(commitment.ScheduledPhase);
            var daysUntil = commitment.ScheduledDay - currentDay;

            if (daysUntil < 0)
            {
                return 0f;
            }

            var hoursUntil = daysUntil * 24 + (targetHour - currentHour);
            return Math.Max(0, hoursUntil);
        }

        /// <summary>
        /// Calculates hours until the next commitment.
        /// </summary>
        public float GetHoursUntilNextCommitment()
        {
            var next = GetNextCommitment();
            return next != null ? GetHoursUntilCommitment(next) : 0f;
        }

        /// <summary>
        /// Gets the hour of day for a phase.
        /// </summary>
        public static int GetPhaseHour(string phase)
        {
            return phase switch
            {
                "Dawn" => 6,
                "Midday" => 12,
                "Dusk" => 18,
                "Night" => 0,
                _ => 12
            };
        }

        /// <summary>
        /// Gets the sort order for phases.
        /// </summary>
        private static int GetPhaseOrder(string phase)
        {
            return phase switch
            {
                "Dawn" => 0,
                "Midday" => 1,
                "Dusk" => 2,
                "Night" => 3,
                _ => 4
            };
        }

        /// <summary>
        /// Calculates what day a phase will next occur.
        /// If the phase has already passed today, returns tomorrow.
        /// </summary>
        public static int CalculateScheduledDay(string phase)
        {
            var currentHour = CampaignTime.Now.GetHourOfDay;
            var currentDay = (int)CampaignTime.Now.ToDays;
            var phaseHour = GetPhaseHour(phase);

            // Special case: Night (0) - if it's past midnight and before 3am, it's still "today's night"
            if (phase == "Night")
            {
                return currentHour < 3 ? currentDay : currentDay + 1;
            }

            // For other phases, if we've passed that hour, schedule for next day
            return currentHour >= phaseHour ? currentDay + 1 : currentDay;
        }

        // Legacy compatibility methods
        [Obsolete("Use AddCommitment instead")]
        public void CommitTo(string activityId, float hoursFromNow, string displayText)
        {
            ScheduledActivityId = activityId;
            ScheduledTimeHours = (float)CampaignTime.Now.ToHours + hoursFromNow;
            CommitmentDisplayText = displayText;
        }

        [Obsolete("Use ClearAllCommitments instead")]
        public void ClearCommitment()
        {
            ClearAllCommitments();
        }

        [Obsolete("Use GetCommitmentsForPhase instead")]
        public bool IsTimeToFire()
        {
            if (!HasCommitment || !ScheduledTimeHours.HasValue)
            {
                return false;
            }

            var currentHour = (float)CampaignTime.Now.ToHours;
            return currentHour >= ScheduledTimeHours.Value;
        }

        [Obsolete("Use GetHoursUntilCommitment instead")]
        public float GetHoursUntilActivity()
        {
            if (!HasCommitment || !ScheduledTimeHours.HasValue)
            {
                return 0f;
            }

            var currentHour = (float)CampaignTime.Now.ToHours;
            return Math.Max(0, ScheduledTimeHours.Value - currentHour);
        }
    }
}
