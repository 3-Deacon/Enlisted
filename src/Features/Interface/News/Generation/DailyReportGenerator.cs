using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Features.Interface.News.Models;
using Enlisted.Features.Interface.News.Templates;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.Interface.News.Generation
{
    public sealed class DailyReportGenerationContext
    {
        public int DayNumber;

        public string CompanySettlementName = string.Empty;
        public string TargetSettlementName = string.Empty;
        public string LastStopSettlementName = string.Empty;
        public string AttachedArmyLeaderName = string.Empty;

        public string KingdomHeadline = string.Empty;
    }

    /// <summary>
    /// Phase 2 generator: selects a bounded set of report lines from the snapshot using templates.
    /// This keeps prose stable and avoids spam by reusing the same lines for the day.
    /// </summary>
    public static class DailyReportGenerator
    {
        public static List<string> Generate(DailyReportSnapshot snapshot, DailyReportGenerationContext context, int maxLines = 8)
        {
            if (snapshot == null)
            {
                return new List<string>();
            }

            context ??= new DailyReportGenerationContext { DayNumber = snapshot.DayNumber };
            maxLines = Math.Max(1, Math.Min(maxLines, 12));

            var candidates = new List<Candidate>();

            // ===== Strategic Context: The overarching "WHY" (HIGH PRIORITY) =====
            if (!string.IsNullOrWhiteSpace(snapshot.StrategicContextTag))
            {
                string narrationId = $"context_narration_{snapshot.StrategicContextTag}";
                TextObject narrationText = GameTexts.FindText(narrationId);

                // If targeting a settlement, add that context if possible
                string narration = narrationText.ToString();
                if (!string.IsNullOrWhiteSpace(context.TargetSettlementName))
                {
                    narration = narration.Replace("{target}", context.TargetSettlementName);
                }
                else
                {
                    narration = narration.Replace("{target}", "the enemy");
                }

                candidates.Add(Candidate.Raw(
                    line: narration,
                    priority: 92, // High priority but below recent critical order outcomes
                    severity: 0,
                    confidence: 1.0f));
            }

            // ===== Player Actions: Recent order outcomes (HIGHEST PRIORITY) =====
            if (EnlistedNewsBehavior.Instance != null)
            {
                var recentOrders = EnlistedNewsBehavior.Instance.GetRecentOrderOutcomes(maxDaysOld: 1);
                foreach (var order in recentOrders)
                {
                    candidates.Add(Candidate.Raw(
                        line: order.BriefSummary,
                        priority: 98,
                        severity: order.Success ? 10 : 70,
                        confidence: 1.0f));
                }

                // ===== Player Actions: Significant reputation changes =====
                var repChanges = EnlistedNewsBehavior.Instance.GetRecentReputationChanges(maxDaysOld: 1);
                foreach (var change in repChanges)
                {
                    candidates.Add(Candidate.Raw(
                        line: change.Message,
                        priority: 85,
                        severity: 30,
                        confidence: 0.9f));
                }

                // ===== Player Actions: Company need changes =====
                var needChanges = EnlistedNewsBehavior.Instance.GetRecentCompanyNeedChanges(maxDaysOld: 1);
                foreach (var change in needChanges)
                {
                    candidates.Add(Candidate.Raw(
                        line: change.Message,
                        priority: 80,
                        severity: change.NewValue < 30 ? 80 : 20,
                        confidence: 0.9f));
                }
            }

            // ===== Company: urgent objective (battle/siege) =====
            if (!string.IsNullOrWhiteSpace(snapshot.BattleTag))
            {
                if (string.Equals(snapshot.BattleTag, "engaged", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(Candidate.Template(
                        templateId: "company_objective_battle",
                        priority: 95,
                        severity: 90,
                        confidence: 0.95f));
                }
                else if (string.Equals(snapshot.BattleTag, "siege", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(Candidate.Template(
                        templateId: "company_objective_siege",
                        priority: 90,
                        severity: 85,
                        confidence: 0.95f));
                }
            }

            // ===== Unit: health deltas (casualties) =====
            if (snapshot.DeadDelta > 0)
            {
                candidates.Add(Candidate.Template(
                    templateId: "unit_casualties_dead",
                    priority: 100,
                    severity: Math.Min(100, snapshot.DeadDelta * 10),
                    confidence: 0.9f,
                    tokens: new Dictionary<string, string> { { "COUNT", snapshot.DeadDelta.ToString() } }));
            }
            else if (snapshot.WoundedDelta > 0)
            {
                candidates.Add(Candidate.Template(
                    templateId: "unit_casualties_wounded",
                    priority: 80,
                    severity: Math.Min(80, snapshot.WoundedDelta * 5),
                    confidence: 0.85f,
                    tokens: new Dictionary<string, string> { { "COUNT", snapshot.WoundedDelta.ToString() } }));
            }
            else if (snapshot.SickDelta > 0)
            {
                candidates.Add(Candidate.Template(
                    templateId: "unit_casualties_sick",
                    priority: 70,
                    severity: Math.Min(70, snapshot.SickDelta * 4),
                    confidence: 0.75f,
                    tokens: new Dictionary<string, string> { { "COUNT", snapshot.SickDelta.ToString() } }));
            }
            // Quiet day: we'll prefer a training/routine line instead of "nothing happened" if we have room.

            // ===== Company: movement =====
            if (!string.IsNullOrWhiteSpace(context.AttachedArmyLeaderName))
            {
                candidates.Add(Candidate.Template(
                    templateId: "company_movement_attached_army",
                    priority: 60,
                    severity: 10,
                    confidence: 0.8f,
                    tokens: new Dictionary<string, string>
                    {
                        { "LEADER", context.AttachedArmyLeaderName.Trim() }
                    }));
            }
            else if (!string.IsNullOrWhiteSpace(context.TargetSettlementName))
            {
                candidates.Add(Candidate.Template(
                    templateId: "company_movement_marching",
                    priority: 60,
                    severity: 10,
                    confidence: 0.85f,
                    tokens: new Dictionary<string, string>
                    {
                        { "SETTLEMENT", context.TargetSettlementName.Trim() }
                    }));
            }
            else if (!string.IsNullOrWhiteSpace(context.CompanySettlementName))
            {
                candidates.Add(Candidate.Template(
                    templateId: "company_movement_holding",
                    priority: 55,
                    severity: 5,
                    confidence: 0.9f,
                    tokens: new Dictionary<string, string>
                    {
                        { "SETTLEMENT", context.CompanySettlementName.Trim() }
                    }));
            }

            // ===== Company: last stop =====
            if (!string.IsNullOrWhiteSpace(context.LastStopSettlementName))
            {
                candidates.Add(Candidate.Template(
                    templateId: "company_stop_settlement",
                    priority: 50,
                    severity: 25,
                    confidence: 0.8f,
                    tokens: new Dictionary<string, string>
                    {
                        { "SETTLEMENT", context.LastStopSettlementName.Trim() }
                    }));
            }

            // ===== Unit: training / routine =====
            if (string.IsNullOrWhiteSpace(snapshot.BattleTag) &&
                snapshot.DeadDelta <= 0 &&
                snapshot.WoundedDelta <= 0 &&
                snapshot.SickDelta <= 0)
            {
                var trainingTemplate = snapshot.TrainingTag switch
                {
                    TrainingTag.Inspection => "unit_training_inspection",
                    TrainingTag.Sparring => "unit_training_sparring",
                    _ => "unit_training_routine"
                };

                candidates.Add(Candidate.Template(
                    templateId: trainingTemplate,
                    priority: 18,
                    severity: 0,
                    confidence: 0.75f));
            }

            // ===== Unit: scrutiny pressure (0-100 scale) =====
            if (snapshot.DisciplineIssues >= 0) // Legacy field name, now contains scrutiny value
            {
                // Critical: 81-100 (Court-martial pending)
                if (snapshot.DisciplineIssues >= 81 || string.Equals(snapshot.DisciplineTag, "breaking", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(Candidate.Template(
                        templateId: "unit_discipline_critical",
                        priority: 55,
                        severity: 60,
                        confidence: 0.85f));
                }
                // Serious: 61-80 (Serious trouble)
                else if (snapshot.DisciplineIssues >= 61 || string.Equals(snapshot.DisciplineTag, "serious", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(Candidate.Template(
                        templateId: "unit_discipline_serious",
                        priority: 45,
                        severity: 40,
                        confidence: 0.8f));
                }
            }

            // ===== Company: needs (food proxy) =====
            candidates.Add(snapshot.Food switch
            {
                FoodBand.Plenty => Candidate.Template("company_food_plenty", priority: 20, severity: 0, confidence: 0.8f),
                FoodBand.Thin => Candidate.Template("company_food_thin", priority: 40, severity: 30, confidence: 0.8f),
                FoodBand.Low => Candidate.Template("company_food_thin", priority: 50, severity: 50, confidence: 0.8f),
                FoodBand.Critical => Candidate.Template("company_food_critical", priority: 70, severity: 80, confidence: 0.85f),
                _ => Candidate.Template("rumor_ambient", priority: 5, severity: 0, confidence: 0.2f),
            });

            // ===== Company: threat =====
            candidates.Add(snapshot.Threat switch
            {
                ThreatBand.High => Candidate.Template("company_threat_high", priority: 75, severity: 80, confidence: 0.8f),
                ThreatBand.Medium => Candidate.Template("company_threat_medium", priority: 55, severity: 50, confidence: 0.7f),
                _ => Candidate.Template("company_threat_low", priority: 15, severity: 0, confidence: 0.6f),
            });

            // ===== Kingdom: headline (best-effort; already grounded by EnlistedNewsBehavior templates) =====
            if (!string.IsNullOrWhiteSpace(context.KingdomHeadline))
            {
                candidates.Add(Candidate.Raw(context.KingdomHeadline.Trim(), priority: 35, severity: 10, confidence: 0.6f));
            }

            // ===== Rumor =====
            candidates.Add(snapshot.Threat >= ThreatBand.Medium
                ? Candidate.Template("rumor_siege", priority: 12, severity: 0, confidence: 0.3f)
                : Candidate.Template("rumor_ambient", priority: 8, severity: 0, confidence: 0.25f));

            // Deduplicate by key (later phases can set real dedupe keys; for now use template id / raw line).
            var deduped = candidates
                .GroupBy(c => c.DedupeKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.Priority)
                    .ThenByDescending(x => x.Severity)
                    .ThenByDescending(x => x.Confidence)
                    .First())
                .OrderByDescending(c => c.Priority)
                .ThenByDescending(c => c.Severity)
                .ThenByDescending(c => c.Confidence)
                .ToList();

            var lines = new List<string>();
            foreach (var c in deduped)
            {
                if (lines.Count >= maxLines)
                {
                    break;
                }

                var line = RenderCandidate(c, context.DayNumber);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            // Absolute fallback: never return empty.
            if (lines.Count == 0)
            {
                lines.Add("Quiet day. Drills and routine.");
            }

            return lines;
        }

        private static string RenderCandidate(Candidate candidate, int dayNumber)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(candidate.RawLine))
            {
                return candidate.RawLine.Trim();
            }

            if (string.IsNullOrWhiteSpace(candidate.TemplateId) ||
                !NewsTemplateLibrary.ById.TryGetValue(candidate.TemplateId, out var template) ||
                template == null)
            {
                return string.Empty;
            }

            var tokens = candidate.Tokens ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Hedge phrase token.
            if (!tokens.ContainsKey("HEDGE"))
            {
                var band = NewsHedging.ToBand(candidate.Confidence);
                tokens["HEDGE"] = NewsHedging.PickPhrase(band, seed: unchecked(dayNumber * 397) ^ candidate.TemplateId.GetHashCode());
            }

            return NewsTemplateRenderer.Render(template.Format, tokens);
        }

        private sealed class Candidate
        {
            public string DedupeKey = string.Empty;
            public int Priority;
            public int Severity;
            public float Confidence;

            public string TemplateId = string.Empty;
            public Dictionary<string, string> Tokens;

            public string RawLine = string.Empty;

            public static Candidate Template(string templateId, int priority, int severity, float confidence, Dictionary<string, string> tokens = null)
            {
                return new Candidate
                {
                    DedupeKey = templateId ?? string.Empty,
                    TemplateId = templateId ?? string.Empty,
                    Priority = priority,
                    Severity = severity,
                    Confidence = Clamp01(confidence),
                    Tokens = tokens
                };
            }

            public static Candidate Raw(string line, int priority, int severity, float confidence)
            {
                return new Candidate
                {
                    DedupeKey = line ?? string.Empty,
                    RawLine = line ?? string.Empty,
                    Priority = priority,
                    Severity = severity,
                    Confidence = Clamp01(confidence)
                };
            }

            private static float Clamp01(float v)
            {
                if (v < 0f)
                {
                    return 0f;
                }
                if (v > 1f)
                {
                    return 1f;
                }
                return v;
            }
        }
    }
}


