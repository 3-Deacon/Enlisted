using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Qualities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Accumulates per-path specialization scores from intent picks (+3 each) and major skill-XP gains (+1 per >=50 XP event). Maps skill StringIds and intent ids to one of five paths (ranger / enforcer / support / diplomat / rogue) and writes the score to a path_{name}_score quality capped at 100.</summary>
    public sealed class PathScorer : CampaignBehaviorBase
    {
        private static readonly Dictionary<string, string> SkillToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Bow", "ranger" },
            { "Scouting", "ranger" },
            { "Riding", "ranger" },
            { "OneHanded", "enforcer" },
            { "Polearm", "enforcer" },
            { "Athletics", "enforcer" },
            { "Leadership", "enforcer" },
            { "Medicine", "support" },
            { "Engineering", "support" },
            { "Trade", "support" },
            { "Steward", "support" },
            { "Charm", "diplomat" },
            { "Roguery", "rogue" }
        };

        private static readonly Dictionary<string, string> IntentToPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "diligent", "enforcer" },
            { "train_hard", "ranger" },
            { "sociable", "diplomat" },
            { "scheme", "rogue" },
            { "slack", "rogue" }
        };

        public override void RegisterEvents()
        {
            // Static events persist across Campaign teardown; guard against duplicate
            // subscription when RegisterEvents is re-invoked on save/load cycles.
            EnlistmentBehavior.OnXPGained -= OnXPGained;
            EnlistmentBehavior.OnXPGained += OnXPGained;
            EnlistmentBehavior.OnTierChanged -= OnTierChanged;
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
        }

        public override void SyncData(IDataStore dataStore) { }

        public static void OnIntentPicked(string intent)
        {
            if (string.IsNullOrEmpty(intent))
            {
                return;
            }
            if (!IntentToPath.TryGetValue(intent, out var path))
            {
                return;
            }
            BumpPath(path, 3, "intent_pick");
        }

        private static void OnXPGained(int amount, string source)
        {
            try
            {
                if (amount < 50)
                {
                    return;
                }
                var skill = ExtractSkillFromSource(source);
                if (string.IsNullOrEmpty(skill))
                {
                    return;
                }
                if (!SkillToPath.TryGetValue(skill, out var path))
                {
                    return;
                }
                BumpPath(path, 1, "xp_gain");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PATH", "OnXPGained threw", ex);
            }
        }

        private static void OnTierChanged(int oldTier, int newTier)
        {
            try
            {
                ModLogger.Info("PATH", $"Tier changed {oldTier} -> {newTier}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PATH", "OnTierChanged threw", ex);
            }
        }

        private static void BumpPath(string path, int amount, string reason)
        {
            try
            {
                var qualityKey = $"path_{path}_score";
                var current = QualityStore.Instance?.Get(qualityKey) ?? 0;
                QualityStore.Instance?.Set(qualityKey, Math.Max(0, Math.Min(100, current + amount)), reason);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PATH", "BumpPath threw", ex);
            }
        }

        private static string ExtractSkillFromSource(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return null;
            }
            var tokens = source.Split(new[] { ':', '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (SkillToPath.ContainsKey(token))
                {
                    return token;
                }
            }
            for (int i = 0; i < tokens.Length - 1; i++)
            {
                var joined = tokens[i] + tokens[i + 1];
                if (SkillToPath.ContainsKey(joined))
                {
                    return joined;
                }
            }
            return null;
        }
    }
}
