using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enlisted.Features.Camp;
using Enlisted.Features.Company;
using Enlisted.Features.Conditions;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Orders.Behaviors;
using Enlisted.Features.Ranks;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Generates player-specific forecasts for the YOU section of the Main Menu.
    /// Produces NOW (current status) and AHEAD (upcoming predictions) text incorporating
    /// duty status, health, conditions, and culture-aware rank names.
    /// </summary>
    public class ForecastGenerator
    {
        private const string LogCategory = "ForecastGen";

        private enum Priority { Low, Medium, High, Critical }

        private readonly EnlistmentBehavior _enlistment;
        private readonly EscalationManager _escalation;

        public ForecastGenerator(EnlistmentBehavior enlistment, EscalationManager escalation)
        {
            _enlistment = enlistment;
            _escalation = escalation;
        }

        /// <summary>
        /// Builds the complete player status forecast (NOW and AHEAD sections).
        /// </summary>
        public (string Now, string Ahead) BuildPlayerStatus()
        {
            try
            {
                var now = BuildNowText();
                var ahead = BuildAheadText();
                return (now, ahead);
            }
            catch (System.Exception ex)
            {
                ModLogger.Error(LogCategory, "Failed to build player status", ex);
                return ("Status unavailable.", "Forecast unavailable.");
            }
        }

        /// <summary>
        /// Builds the NOW text: player's current duty status, physical state, and immediate commitments.
        /// </summary>
        private string BuildNowText()
        {
            var hero = Hero.MainHero;
            var sb = new StringBuilder();

            // Duty Status - simplified to just check if order exists
            var currentOrder = OrderManager.Instance?.GetCurrentOrder();
            if (currentOrder != null)
            {
                var orderDisplayTitle = Orders.OrderCatalog.GetDisplayTitle(currentOrder);
                if (string.IsNullOrEmpty(orderDisplayTitle))
                {
                    orderDisplayTitle = "Orders";
                }
                sb.Append(new TextObject("{=menu_you_on_duty}On duty - {ORDER_NAME}, day {DAY} of {TOTAL}.")
                    .SetTextVariable("ORDER_NAME", orderDisplayTitle)
                    .SetTextVariable("DAY", "1")
                    .SetTextVariable("TOTAL", "3")
                    .ToString());
            }
            else
            {
                sb.Append(new TextObject("{=menu_you_off_duty}You're off duty and well-rested.").ToString());
            }

            // Physical State
            if (hero.IsWounded)
            {
                sb.Append(" ");
                sb.Append(new TextObject("{=menu_you_wounded}You're wounded - movement impaired. Off duty until you recover.").ToString());
            }

            // Player commitments (scheduled activities)
            var generator = CampOpportunityGenerator.Instance;
            var nextCommitment = generator?.GetNextCommitment();
            if (nextCommitment != null)
            {
                sb.Append(" ");
                var hoursUntil = generator.GetHoursUntilCommitment(nextCommitment);
                var activity = nextCommitment.Title?.ToLower() ?? "an activity";
                var phase = nextCommitment.ScheduledPhase?.ToLower() ?? "later";

                if (hoursUntil < 1f)
                {
                    sb.Append(new TextObject("{=menu_you_commitment_soon}It's almost time for {ACTIVITY}.").SetTextVariable("ACTIVITY", activity).ToString());
                }
                else if (hoursUntil <= 6f)
                {
                    sb.Append(new TextObject("{=menu_you_commitment_today}You've committed to {ACTIVITY} this {PHASE}.")
                        .SetTextVariable("ACTIVITY", activity)
                        .SetTextVariable("PHASE", phase)
                        .ToString());
                }
                else
                {
                    sb.Append(new TextObject("{=menu_you_commitment}You've committed to {ACTIVITY} at {PHASE} ({HOURS}h).")
                        .SetTextVariable("ACTIVITY", activity)
                        .SetTextVariable("PHASE", phase)
                        .SetTextVariable("HOURS", ((int)hoursUntil).ToString())
                        .ToString());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds the AHEAD text: upcoming events, warnings, and predictions based on orchestrator state.
        /// Prioritizes critical warnings (hunger, morale, discipline) over routine forecasts.
        /// Integrates with CompanySimulationBehavior for pressure-based warnings.
        /// </summary>
        private string BuildAheadText()
        {
            var forecasts = new List<(string text, Priority priority)>();
            var cultureId = RankHelper.GetCultureId(_enlistment);

            // === PARTY STATE WARNINGS (from Background Simulation) ===
            var sim = CompanySimulationBehavior.Instance;
            var needs = _enlistment?.CompanyNeeds;

            if (sim != null)
            {
                // Supply warnings - escalating urgency based on consecutive days of low supplies
                if (sim.Pressure.DaysLowSupplies >= 2)
                {
                    forecasts.Add((
                        new TextObject("{=menu_ahead_supplies_critical}The men are hungry. Supplies won't last.").ToString(),
                        Priority.Critical));
                }
                else if (needs != null && needs.Supplies < 40)
                {
                    forecasts.Add((
                        new TextObject("{=menu_ahead_supplies_low}Rations are getting thin.").ToString(),
                        Priority.High));
                }

                // Readiness warnings (morale removed - using readiness as mood indicator)
                if (needs != null && needs.Readiness < 30)
                {
                    forecasts.Add((
                        new TextObject("{=menu_ahead_readiness_low}The men are exhausted.").ToString(),
                        Priority.High));
                }

                // Health warnings - many sick soldiers spreading illness
                if (sim.Roster != null && sim.Roster.SickCount > 5)
                {
                    forecasts.Add((
                        new TextObject("{=menu_ahead_sick_high}Fever spreading through camp.").ToString(),
                        Priority.High));
                }

                // Wounded warnings - high casualty rate affecting readiness
                if (sim.Roster != null && sim.Roster.TotalSoldiers > 0 &&
                    (float)sim.Roster.WoundedCount / sim.Roster.TotalSoldiers > 0.2f)
                {
                    forecasts.Add((
                        new TextObject("{=menu_ahead_wounded_many}Many wounded need care.").ToString(),
                        Priority.High));
                }

                // Desertion warnings - recent losses to desertion
                if (sim.Pressure.RecentDesertions > 0)
                {
                    forecasts.Add((
                        new TextObject("{=menu_ahead_desertion}Men have been slipping away.").ToString(),
                        Priority.High));
                }
            }

            // Scrutiny warnings from escalation tracks (0-100 scale)
            if (_escalation?.State != null && _escalation.State.Scrutiny > 70)
            {
                forecasts.Add((
                    new TextObject("{=menu_ahead_scrutiny_high}Officers are watching closely.").ToString(),
                    Priority.Medium));
            }

            // === ORDER/EVENT FORECASTS ===
            // Check for pending orders (no active order means one might be coming)
            var orderManager = OrderManager.Instance;
            if (orderManager != null && orderManager.GetCurrentOrder() == null)
            {
                // Player is off-duty, hint that orders may be coming
                var ncoTitle = RankHelper.GetNCOTitle(cultureId);
                forecasts.Add((
                    new TextObject("{=menu_ahead_order_coming}{NCO_TITLE}'s been making lists.")
                        .SetTextVariable("NCO_TITLE", ncoTitle)
                        .ToString(),
                    Priority.Low));
            }

            // Check for upcoming muster (pay day)
            if (_enlistment != null && _enlistment.IsEnlisted)
            {
                var nextPayday = _enlistment.NextPaydaySafe;
                if (nextPayday != CampaignTime.Zero)
                {
                    double daysUntilMuster = (nextPayday - CampaignTime.Now).ToDays;
                    if (daysUntilMuster <= 3 && daysUntilMuster > 0)
                    {
                        forecasts.Add((
                            new TextObject("{=menu_ahead_muster_soon}Pay day approaches.").ToString(),
                            Priority.Medium));
                    }
                }
            }

            // Default if nothing else - quiet day forecast
            if (forecasts.Count == 0)
            {
                forecasts.Add((
                    new TextObject("{=menu_ahead_default}Quiet. Almost too quiet.").ToString(),
                    Priority.Low));
            }

            // === MEDICAL PRESSURE WARNINGS (Phase 6H) ===
            AddMedicalForecast(forecasts);

            // Sort by priority descending, take top 2 most urgent forecasts
            var topForecasts = forecasts
                .OrderByDescending(f => f.priority)
                .Take(2)
                .Select(f => f.text);

            return string.Join(" ", topForecasts);
        }

        /// <summary>
        /// Adds medical-related forecasts based on player condition and medical pressure.
        /// Part of Phase 6H medical system orchestration.
        /// </summary>
        private void AddMedicalForecast(List<(string text, Priority priority)> forecasts)
        {
            // Get medical pressure from SimulationPressureCalculator
            var (medicalAnalysis, pressureLevel) = SimulationPressureCalculator.GetMedicalPressure();

            // Critical: Severe condition requiring immediate attention
            if (medicalAnalysis.HasSevereCondition)
            {
                var condType = GetConditionTypeName();
                forecasts.Add((
                    new TextObject("{=menu_ahead_condition_severe}Your {CONDITION} is getting worse. Find a surgeon.")
                        .SetTextVariable("CONDITION", condType)
                        .ToString(),
                    Priority.Critical));
                return;
            }

            // High: Untreated condition worsening over time
            if (medicalAnalysis.HasCondition && medicalAnalysis.IsUntreated && medicalAnalysis.DaysUntreated >= 2)
            {
                forecasts.Add((
                    new TextObject("{=menu_ahead_condition_untreated}You've been ignoring that {CONDITION}. It won't heal itself.")
                        .SetTextVariable("CONDITION", GetConditionTypeName())
                        .ToString(),
                    Priority.High));
                return;
            }

            // Moderate: Has condition but under care, or high medical risk
            if (medicalAnalysis.HasCondition && !medicalAnalysis.IsUntreated)
            {
                forecasts.Add((
                    new TextObject("{=menu_ahead_condition_treated}Treatment is helping. Stay off your feet.").ToString(),
                    Priority.Low));
                return;
            }

            // Warning: Building medical risk without condition yet
            if (pressureLevel == MedicalPressureLevel.High && !medicalAnalysis.HasCondition)
            {
                forecasts.Add((
                    new TextObject("{=menu_ahead_medical_risk_high}You're pushing too hard. Rest before you collapse.").ToString(),
                    Priority.High));
                return;
            }

            if (pressureLevel == MedicalPressureLevel.Moderate && !medicalAnalysis.HasCondition)
            {
                forecasts.Add((
                    new TextObject("{=menu_ahead_medical_risk_moderate}You need rest soon.").ToString(),
                    Priority.Medium));
            }
        }

        /// <summary>
        /// Gets the localized name of the player's current condition for display.
        /// </summary>
        private string GetConditionTypeName()
        {
            var cond = PlayerConditionBehavior.Instance?.State;
            if (cond == null)
            {
                return new TextObject("{=condition_generic}ailment").ToString();
            }

            // Check for injury first (more specific)
            if (cond.CurrentInjury != InjurySeverity.None)
            {
                return cond.CurrentInjury switch
                {
                    InjurySeverity.Minor => new TextObject("{=condition_injury_minor}wound").ToString(),
                    InjurySeverity.Moderate => new TextObject("{=condition_injury_moderate}injury").ToString(),
                    InjurySeverity.Severe => new TextObject("{=condition_injury_severe}serious wound").ToString(),
                    InjurySeverity.Critical => new TextObject("{=condition_injury_critical}grievous wound").ToString(),
                    _ => new TextObject("{=condition_generic}ailment").ToString()
                };
            }

            // Check for illness
            if (cond.CurrentIllness != IllnessSeverity.None)
            {
                return cond.CurrentIllness switch
                {
                    IllnessSeverity.Mild => new TextObject("{=condition_illness_minor}cough").ToString(),
                    IllnessSeverity.Moderate => new TextObject("{=condition_illness_moderate}fever").ToString(),
                    IllnessSeverity.Severe => new TextObject("{=condition_illness_severe}sickness").ToString(),
                    IllnessSeverity.Critical => new TextObject("{=condition_illness_critical}plague").ToString(),
                    _ => new TextObject("{=condition_generic}ailment").ToString()
                };
            }

            return new TextObject("{=condition_generic}ailment").ToString();
        }
    }
}
