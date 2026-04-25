using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enlisted.Features.Conditions;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Manages injury types and applies narrative-driven injuries with varied severity.
    /// Injuries are defined in injuries.json with HP percentages, narratives, and recovery times.
    /// Replaces flat HP loss with contextual injury system for better storytelling.
    /// </summary>
    public static class InjurySystem
    {
        private const string LogCategory = "InjurySystem";
        private static string InjuriesPath => Path.Combine(ModulePaths.GetContentPath("Content"), "injuries.json");

        private static List<InjuryDefinition> _injuries;
        private static bool _loaded;

        public static void Initialize()
        {
            LoadInjuries();
        }

        private static void LoadInjuries()
        {
            if (_loaded)
            {
                return;
            }

            try
            {
                if (!File.Exists(InjuriesPath))
                {
                    ModLogger.Warn(LogCategory, $"Injuries file not found at {InjuriesPath}");
                    _injuries = new List<InjuryDefinition>();
                    _loaded = true;
                    return;
                }

                var json = File.ReadAllText(InjuriesPath);
                var root = JObject.Parse(json);
                var injuriesArray = root["injuries"] as JArray;

                if (injuriesArray == null)
                {
                    ModLogger.Warn(LogCategory, "No 'injuries' array found in injuries.json");
                    _injuries = new List<InjuryDefinition>();
                    _loaded = true;
                    return;
                }

                _injuries = new List<InjuryDefinition>();
                foreach (var injToken in injuriesArray)
                {
                    var inj = new InjuryDefinition
                    {
                        Id = injToken["id"]?.ToString() ?? string.Empty,
                        DisplayName = injToken["displayName"]?.ToString() ?? "Injury",
                        HpPercentage = injToken["hpPercentage"]?.Value<float>() ?? 20f,
                        BriefNarrative = injToken["briefNarrative"]?.ToString() ?? "Injured",
                        RecoveryDays = injToken["recoveryDays"]?.Value<int>() ?? 7,
                        Severity = ParseSeverity(injToken["severity"]?.ToString())
                    };

                    var narrativesArray = injToken["narratives"] as JArray;
                    if (narrativesArray != null)
                    {
                        inj.Narratives = narrativesArray.Select(n => n.ToString()).ToList();
                    }

                    _injuries.Add(inj);
                }

                ModLogger.Info(LogCategory, $"Loaded {_injuries.Count} injury definitions");
                _loaded = true;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("InjurySystem", "Failed to load injuries.json", ex);
                _injuries = new List<InjuryDefinition>();
                _loaded = true;
            }
        }

        private static InjurySeverity ParseSeverity(string severity)
        {
            return severity?.ToLower() switch
            {
                "minor" => InjurySeverity.Minor,
                "moderate" => InjurySeverity.Moderate,
                "severe" => InjurySeverity.Severe,
                "critical" => InjurySeverity.Critical,
                _ => InjurySeverity.Minor
            };
        }

        /// <summary>
        /// Gets an injury definition by ID.
        /// </summary>
        public static InjuryDefinition GetInjury(string injuryId)
        {
            if (!_loaded)
            {
                LoadInjuries();
            }

            return _injuries?.FirstOrDefault(i => i.Id == injuryId);
        }

        /// <summary>
        /// Gets a random injury of the specified severity.
        /// </summary>
        public static InjuryDefinition GetRandomInjury(InjurySeverity severity)
        {
            if (!_loaded)
            {
                LoadInjuries();
            }

            var matching = _injuries?.Where(i => i.Severity == severity).ToList();
            if (matching == null || matching.Count == 0)
            {
                return null;
            }

            return matching[MBRandom.RandomInt(matching.Count)];
        }

        /// <summary>
        /// Applies an injury to the player with narrative feedback.
        /// Uses percentage-based HP loss for varied severity.
        /// </summary>
        /// <param name="injuryId">Injury type ID (e.g., "sprained_ankle")</param>
        /// <param name="context">Context string for logging (e.g., "order_outcome", "event")</param>
        /// <returns>The narrative text to display, or empty string if injury not found</returns>
        public static string ApplyInjury(string injuryId, string context = "unknown")
        {
            if (string.IsNullOrWhiteSpace(injuryId))
            {
                return string.Empty;
            }

            var injury = GetInjury(injuryId);
            if (injury == null)
            {
                ModLogger.Warn(LogCategory, $"Injury '{injuryId}' not found in definitions");
                return string.Empty;
            }

            var hero = Hero.MainHero;
            if (hero == null)
            {
                return string.Empty;
            }

            // Calculate HP loss as percentage of max HP
            var maxHp = hero.CharacterObject.MaxHitPoints();
            var hpLoss = (int)Math.Ceiling(maxHp * (injury.HpPercentage / 100f));
            var oldHp = hero.HitPoints;
            var newHp = Math.Max(1, oldHp - hpLoss);

            hero.HitPoints = newHp;

            // Select random narrative from available options
            var narrative = injury.Narratives != null && injury.Narratives.Count > 0
                ? injury.Narratives[MBRandom.RandomInt(injury.Narratives.Count)]
                : $"You suffered a {injury.DisplayName.ToLower()}.";

            // Show feedback message
            var color = injury.Severity switch
            {
                InjurySeverity.Critical => Color.FromUint(0xFFFF0000u), // Bright red
                InjurySeverity.Severe => Color.FromUint(0xFFFF4444u),   // Red
                InjurySeverity.Moderate => Color.FromUint(0xFFFF8800u), // Orange
                _ => Color.FromUint(0xFFFFAA00u)                        // Yellow
            };

            InformationManager.DisplayMessage(new InformationMessage(
                $"{injury.DisplayName}: -{hpLoss} HP",
                color));

            ModLogger.Info(LogCategory,
                $"Applied injury '{injuryId}' ({injury.Severity}, {injury.HpPercentage}% = {hpLoss} HP) from {context}: {oldHp} -> {newHp}");

            return narrative;
        }

        /// <summary>
        /// Gets a brief narrative description for an injury (for use in recaps/status).
        /// </summary>
        public static string GetBriefNarrative(string injuryId)
        {
            var injury = GetInjury(injuryId);
            return injury?.BriefNarrative ?? "injured";
        }

        /// <summary>
        /// Gets all available injury IDs for a given severity level.
        /// Useful for content authors and the orchestrator.
        /// </summary>
        public static List<string> GetInjuryIds(InjurySeverity severity)
        {
            if (!_loaded)
            {
                LoadInjuries();
            }

            return _injuries?
                .Where(i => i.Severity == severity)
                .Select(i => i.Id)
                .ToList() ?? new List<string>();
        }
    }

    /// <summary>
    /// Defines an injury type with narrative and mechanical effects.
    /// Uses InjurySeverity from PlayerConditionModels for consistency with the condition system.
    /// </summary>
    public class InjuryDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public float HpPercentage { get; set; }
        public List<string> Narratives { get; set; } = new List<string>();
        public string BriefNarrative { get; set; }
        public int RecoveryDays { get; set; }
        public InjurySeverity Severity { get; set; }
    }
}
