using System;
using System.Collections.Generic;
using System.Linq;

namespace Enlisted.Features.Interface.News.Templates
{
    public enum NewsTemplateCategory
    {
        Unknown = 0,
        Unit = 1,
        Company = 2,
        Kingdom = 3,
        Rumor = 4
    }

    public enum ConfidenceBand
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// A stable “template id → format string” entry used by Daily Report generation.
    /// Templates are intentionally simple and do not execute arbitrary code.
    /// </summary>
    public sealed class NewsTemplate
    {
        public NewsTemplate(string id, NewsTemplateCategory category, string format)
        {
            Id = id ?? string.Empty;
            Format = format ?? string.Empty;
        }

        public string Id { get; }
        public string Format { get; }
    }

    /// <summary>
    /// Maps confidence levels into safe “hedge language” to avoid overstating uncertain intel.
    /// Phase 2 will pick deterministically; Phase 1 just defines the vocabulary.
    /// </summary>
    public static class NewsHedging
    {
        public static ConfidenceBand ToBand(float confidence)
        {
            if (confidence >= 0.75f)
            {
                return ConfidenceBand.High;
            }

            if (confidence >= 0.45f)
            {
                return ConfidenceBand.Medium;
            }

            return ConfidenceBand.Low;
        }

        public static IReadOnlyList<string> GetPhrases(ConfidenceBand band)
        {
            return band switch
            {
                ConfidenceBand.High => High,
                ConfidenceBand.Medium => Medium,
                _ => Low
            };
        }

        public static string PickPhrase(ConfidenceBand band, int seed)
        {
            var list = GetPhrases(band);
            if (list.Count == 0)
            {
                return string.Empty;
            }

            var idx = Math.Abs(seed) % list.Count;
            return list[idx] ?? string.Empty;
        }

        // Declarative: we generally omit hedging when confidence is high.
        private static readonly List<string> High = new List<string> { string.Empty };

        // Uncertain / likely.
        private static readonly List<string> Medium = new List<string>
        {
            "Likely",
            "Scouts think",
            "It seems"
        };

        // Rumor framing.
        private static readonly List<string> Low = new List<string>
        {
            "Rumor has it",
            "Hard to say, but",
            "Camp talk says"
        };
    }

    /// <summary>
    /// Minimal placeholder renderer for templates using {TOKEN} style placeholders.
    /// </summary>
    public static class NewsTemplateRenderer
    {
        public static string Render(string format, IReadOnlyDictionary<string, string> tokens)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            if (tokens == null || tokens.Count == 0)
            {
                return format.Trim();
            }

            var result = format;
            foreach (var kvp in tokens)
            {
                var key = kvp.Key ?? string.Empty;
                if (key.Length == 0)
                {
                    continue;
                }

                result = result.Replace("{" + key + "}", kvp.Value ?? string.Empty);
            }

            // Small cleanup: normalize repeated spaces introduced by empty hedges/optional tokens.
            while (result.Contains("  "))
            {
                result = result.Replace("  ", " ");
            }

            return result.Trim();
        }
    }

    /// <summary>
    /// Phase 1: Template library stubs.
    /// These are intentionally small; Phase 2+ will select among them based on snapshot facts.
    /// </summary>
    public static class NewsTemplateLibrary
    {
        public static IReadOnlyList<NewsTemplate> UnitCasualties { get; } = new List<NewsTemplate>
        {
            new NewsTemplate("unit_casualties_quiet", NewsTemplateCategory.Unit,
                "The unit kept its feet today. No new names added to the ledger."),
            new NewsTemplate("unit_casualties_wounded", NewsTemplateCategory.Unit,
                "{COUNT} wounded today. Bandages and gritted teeth all around."),
            new NewsTemplate("unit_casualties_dead", NewsTemplateCategory.Unit,
                "{COUNT} lost today. The tents feel quieter after."),
            new NewsTemplate("unit_casualties_sick", NewsTemplateCategory.Unit,
                "Sickness took {COUNT} off the line. The surgeon has his hands full.")
        };

        public static IReadOnlyList<NewsTemplate> UnitTraining { get; } = new List<NewsTemplate>
        {
            new NewsTemplate("unit_training_routine", NewsTemplateCategory.Unit,
                "Routine drills and corrections. Nothing glamorous, but it adds up."),
            new NewsTemplate("unit_training_inspection", NewsTemplateCategory.Unit,
                "Inspection day. Boots polished, straps tightened, tempers tested."),
            new NewsTemplate("unit_training_sparring", NewsTemplateCategory.Unit,
                "Sparring ran long. Bruises earned, lessons learned.")
        };

        // Scrutiny templates (formerly UnitDiscipline)
        // Scrutiny tracks rule-breaking and insubordination on a 0-100 scale
        public static IReadOnlyList<NewsTemplate> UnitDiscipline { get; } = new List<NewsTemplate>
        {
            new NewsTemplate("unit_discipline_serious", NewsTemplateCategory.Unit,
                "You're under watch. The NCOs are looking for any excuse."),
            new NewsTemplate("unit_discipline_critical", NewsTemplateCategory.Unit,
                "One more incident and you'll face a court-martial. The officers aren't joking.")
        };

        public static IReadOnlyList<NewsTemplate> CompanyMovement { get; } = new List<NewsTemplate>
        {
            new NewsTemplate("company_movement_marching", NewsTemplateCategory.Company,
                "{HEDGE} the company is marching toward {SETTLEMENT}."),
            new NewsTemplate("company_movement_holding", NewsTemplateCategory.Company,
                "{HEDGE} we hold position near {SETTLEMENT}."),
            new NewsTemplate("company_movement_attached_army", NewsTemplateCategory.Company,
                "{HEDGE} we march with the host of {LEADER}."),
            new NewsTemplate("company_stop_settlement", NewsTemplateCategory.Company,
                "We stopped at {SETTLEMENT} to resupply and take stock."),
            new NewsTemplate("company_objective_battle", NewsTemplateCategory.Company,
                "Steel is out today. The company is engaged."),
            new NewsTemplate("company_objective_siege", NewsTemplateCategory.Company,
                "The company is committed to siege operations.")
        };

        public static IReadOnlyList<NewsTemplate> CompanyNeeds { get; } = new List<NewsTemplate>
        {
            new NewsTemplate("company_food_plenty", NewsTemplateCategory.Company,
                "Rations look solid. The cooks even had room to complain about seasoning."),
            new NewsTemplate("company_food_thin", NewsTemplateCategory.Company,
                "Rations are thinning. You can feel it in the men’s mood."),
            new NewsTemplate("company_food_critical", NewsTemplateCategory.Company,
                "Rations are critical. Every mouthful is counted twice.")
        };

        public static IReadOnlyList<NewsTemplate> CompanyThreat { get; } = new List<NewsTemplate>
        {
            new NewsTemplate("company_threat_low", NewsTemplateCategory.Company,
                "The road feels quiet. Scouts report little movement."),
            new NewsTemplate("company_threat_medium", NewsTemplateCategory.Company,
                "Scouts report trouble nearby. Watches are doubled."),
            new NewsTemplate("company_threat_high", NewsTemplateCategory.Company,
                "Threat is high. Steel stays close at hand, even in camp.")
        };

        public static IReadOnlyList<NewsTemplate> KingdomHeadlines { get; } = new List<NewsTemplate>
        {
            new NewsTemplate("kingdom_headline_war", NewsTemplateCategory.Kingdom,
                "War spreads: {FACTION_A} and {FACTION_B} clash again."),
            new NewsTemplate("kingdom_headline_siege", NewsTemplateCategory.Kingdom,
                "Talk of a siege near {SETTLEMENT}. No one agrees whose banner will break first.")
        };

        public static IReadOnlyList<NewsTemplate> Rumors { get; } = new List<NewsTemplate>
        {
            new NewsTemplate("rumor_ambient", NewsTemplateCategory.Rumor,
                "Camp talk runs in circles — who’s paid, who’s promoted, who’s doomed."),
            new NewsTemplate("rumor_siege", NewsTemplateCategory.Rumor,
                "{HEDGE} a siege tightens somewhere up the road, but names change with every telling.")
        };

        public static IReadOnlyList<NewsTemplate> All { get; } = UnitCasualties
            .Concat(UnitTraining)
            .Concat(UnitDiscipline)
            .Concat(CompanyMovement)
            .Concat(CompanyNeeds)
            .Concat(CompanyThreat)
            .Concat(KingdomHeadlines)
            .Concat(Rumors)
            .ToList();

        public static IReadOnlyDictionary<string, NewsTemplate> ById { get; } =
            All
                .Where(t => !string.IsNullOrWhiteSpace(t.Id))
                .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }
}


