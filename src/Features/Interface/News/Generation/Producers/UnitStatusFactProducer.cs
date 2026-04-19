using System;
using Enlisted.Features.Escalation;
using Enlisted.Features.Interface.News.Models;
namespace Enlisted.Features.Interface.News.Generation.Producers
{
    /// <summary>
    /// Unit facts: wounded/sick/dead/replacements deltas and a light training tag (best-effort).
    /// </summary>
    public sealed class UnitStatusFactProducer : IDailyReportFactProducer
    {
        public void Contribute(DailyReportSnapshot snapshot, CampNewsContext context)
        {
            if (snapshot == null || context == null)
            {
                return;
            }

            try
            {
                var party = context.LordParty;
                var manCount = party?.MemberRoster?.TotalManCount ?? -1;
                var woundedCount = party?.MemberRoster?.TotalWounded ?? -1;

                // Delta computation (best-effort) using the persisted CampNewsState baselines.
                var state = context.NewsState;
                if (state != null)
                {
                    var baseline = state.GetBaselineRosterCounts();

                    if (baseline.LastManCount >= 0 && manCount >= 0)
                    {
                        var delta = manCount - baseline.LastManCount;
                        snapshot.ReplacementsDelta = delta > 0 ? delta : 0;
                        snapshot.DeadDelta = delta < 0 ? -delta : 0;
                    }

                    if (baseline.LastWoundedCount >= 0 && woundedCount >= 0)
                    {
                        snapshot.WoundedDelta = woundedCount - baseline.LastWoundedCount;
                    }

                    // Update baselines for tomorrow.
                    if (manCount >= 0 && woundedCount >= 0)
                    {
                        state.SetBaselineRosterCounts(manCount, woundedCount);
                    }
                }

                // Troop sickness signal isn't modelled yet (Phase 4+ producer can add it).
                snapshot.SickDelta = 0;

                // Health delta band (helps template selection stay stable).
                var totalDelta = Math.Abs(snapshot.DeadDelta) + Math.Abs(snapshot.WoundedDelta) + Math.Abs(snapshot.SickDelta);
                snapshot.HealthDeltaBand = totalDelta switch
                {
                    0 => HealthDeltaBand.None,
                    <= 2 => HealthDeltaBand.Minor,
                    <= 5 => HealthDeltaBand.Moderate,
                    _ => HealthDeltaBand.Major
                };

                // Training status is determined by the unit's recent activities.
                snapshot.TrainingTag = TrainingTag.Routine;

                // Scrutiny (0-100 scale): tracks rule-breaking and insubordination
                var scrutiny = EscalationManager.Instance?.State?.Scrutiny ?? -1;
                snapshot.DisciplineIssues = scrutiny; // Legacy field name (TODO: rename to ScrutinyValue)
                snapshot.DisciplineTag = scrutiny switch
                {
                    < 0 => string.Empty,
                    <= 20 => "clean",       // Clear record
                    <= 40 => "minor",       // Minor infractions
                    <= 60 => "troubled",    // Under watch
                    <= 80 => "serious",     // Serious trouble
                    _ => "breaking"         // Court-martial pending
                };
            }
            catch
            {
                // Best-effort only; daily report generation should never crash the day.
            }
        }
    }
}


