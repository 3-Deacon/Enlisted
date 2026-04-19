using System;
using System.Collections.Generic;
using Enlisted.Features.Company;
using Enlisted.Features.Content.Models;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Escalation;
using Enlisted.Features.Ranks.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

// MedicalPressureLevel and MedicalPressureAnalysis are in Models namespace

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Calculates simulation pressure from company and player state.
    /// High pressure indicates stressful situations that may warrant more frequent events.
    /// </summary>
    public static class SimulationPressureCalculator
    {
        private const string LogCategory = "Pressure";

        /// <summary>
        /// Calculates the current simulation pressure based on company needs,
        /// escalation state, player health, and location.
        /// </summary>
        /// <returns>SimulationPressure with value (0-100) and source descriptions.</returns>
        public static SimulationPressure CalculatePressure()
        {
            float pressure = 0;
            var sources = new List<string>();

            // Check company needs
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment?.CompanyNeeds != null)
            {
                var needs = enlistment.CompanyNeeds;

                // Low supplies adds pressure
                var supplies = needs.GetNeedValue(CompanyNeed.Supplies);
                if (supplies < 30)
                {
                    pressure += 20;
                    sources.Add("Low Supplies");
                }

                // Morale removed - readiness is primary stress indicator now
            }

            // Check escalation state
            var escalation = EscalationManager.Instance?.State;
            if (escalation != null)
            {
                // High scrutiny (merged with discipline) adds pressure
                if (escalation.Scrutiny > 70) // 0-100 scale
                {
                    pressure += 20;
                    sources.Add("Under Scrutiny");
                }

                // High medical risk adds pressure
                if (escalation.MedicalRisk > 3)
                {
                    pressure += 15;
                    sources.Add("Medical Risk");
                }
            }

            // Check player health
            var hero = CampaignSafetyGuard.SafeMainHero;
            if (hero != null && hero.HitPoints < hero.MaxHitPoints * 0.5f)
            {
                pressure += 15;
                sources.Add("Wounded");
            }

            // Check if in enemy territory
            var situation = WorldStateAnalyzer.AnalyzeSituation();
            if (situation.InEnemyTerritory)
            {
                pressure += 15;
                sources.Add("Enemy Territory");
            }

            // Check if promotion is blocked by reputation (creates urgency for rep-building)
            var (needsRep, repGap) = CheckPromotionReputationNeed();
            if (needsRep && repGap > 0)
            {
                // Modest pressure increase when promotion is reputation-blocked
                // This surfaces the need without overwhelming other pressures
                pressure += Math.Min(15, repGap);
                sources.Add($"Needs +{repGap} Rep for Promotion");
            }

            return new SimulationPressure
            {
                Value = Math.Min(100, pressure),
                Sources = sources
            };
        }

        /// <summary>
        /// Calculates medical pressure analysis for orchestrator integration.
        /// Integrates with PlayerConditionBehavior to detect active injuries and illnesses.
        /// </summary>
        public static (MedicalPressureAnalysis Analysis, MedicalPressureLevel Level) GetMedicalPressure()
        {
            var escalation = EscalationManager.Instance?.State;
            var hero = CampaignSafetyGuard.SafeMainHero;
            var conditions = Conditions.PlayerConditionBehavior.Instance;
            var state = conditions?.State;

            // Check for active conditions (must have days remaining > 0)
            var hasCondition = state?.HasAnyCondition == true;
            var hasSevereCondition = false;
            
            if (hasCondition && state != null)
            {
                // Severe condition = Severe or Critical severity WITH days remaining
                var hasSevereInjury = state.CurrentInjury >= Conditions.InjurySeverity.Severe && state.InjuryDaysRemaining > 0;
                var hasSevereIllness = state.CurrentIllness >= Conditions.IllnessSeverity.Severe && state.IllnessDaysRemaining > 0;
                hasSevereCondition = hasSevereInjury || hasSevereIllness;
            }

            var analysis = new MedicalPressureAnalysis
            {
                HasCondition = hasCondition,
                HasSevereCondition = hasSevereCondition,
                MedicalRisk = escalation?.MedicalRisk ?? 0,
                HealthPercent = hero != null ? (float)hero.HitPoints / hero.MaxHitPoints * 100f : 100f,
                DaysSinceLastTreatment = 0
            };

            return (analysis, analysis.PressureLevel);
        }

        /// <summary>
        /// Checks if player is close to promotion but lacks sufficient soldier reputation.
        /// Returns the reputation gap (how much more is needed) or 0 if not applicable.
        /// </summary>
        public static (bool NeedsReputation, int ReputationGap) CheckPromotionReputationNeed()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var promotion = PromotionBehavior.Instance;

            if (enlistment?.IsEnlisted != true || promotion == null)
            {
                return (false, 0);
            }

            // Check if player can promote (gets failure reasons if not)
            var (canPromote, failureReasons) = promotion.CanPromote();

            if (canPromote || failureReasons.Count == 0)
            {
                return (false, 0); // Already can promote or not enlisted
            }

            // Check if scrutiny is blocking factor (low reputation cases now use scrutiny)
            bool scrutinyBlocking = false;
            foreach (var reason in failureReasons)
            {
                if (reason.StartsWith("Scrutiny too high:"))
                {
                    scrutinyBlocking = true;
                    break;
                }
            }

            if (!scrutinyBlocking)
            {
                return (false, 0); // Not blocked by scrutiny
            }

            // Calculate how much scrutiny reduction is needed
            var currentTier = enlistment.EnlistmentTier;
            var targetTier = currentTier + 1;
            var requirements = Features.Ranks.Behaviors.PromotionRequirements.GetForTier(targetTier);
            
            var escalation = EscalationManager.Instance?.State;
            var currentScrutiny = escalation?.Scrutiny ?? 0;
            var maxScrutiny = requirements.MaxScrutiny;
            var scrutinyGap = currentScrutiny - maxScrutiny; // How much over limit

            if (scrutinyGap > 0)
            {
                ModLogger.Debug(LogCategory, 
                    $"Promotion scrutiny need detected: T{currentTier}→T{targetTier} max scrutiny {maxScrutiny}, player has {currentScrutiny} (gap: {scrutinyGap})");
                return (true, scrutinyGap);
            }

            return (false, 0);
        }
    }
}
