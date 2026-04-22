using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Features.Qualities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Accumulates per-path specialization scores from intent picks (+3 each) and skill level-ups on the main hero (+change per level, where change is the skill-level delta supplied by HeroGainedSkill). Maps skill StringIds and intent ids to one of five paths (ranger / enforcer / support / diplomat / rogue) and writes the score to a path_{name}_score quality clamped to [0, 100].</summary>
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
            CampaignEvents.HeroGainedSkill.AddNonSerializedListener(this, OnHeroGainedSkill);

            // Static event persists across Campaign teardown; guard against duplicate
            // subscription when RegisterEvents is re-invoked on save/load cycles.
            EnlistmentBehavior.OnTierChanged -= OnTierChanged;
            EnlistmentBehavior.OnTierChanged += OnTierChanged;

            // One-shot session-start heartbeat: snapshot current path scores so smoke
            // tests confirm the behavior is registered even when no skill XP accrues.
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunchedHeartbeat);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunchedHeartbeat(CampaignGameStarter _)
        {
            try
            {
                var store = QualityStore.Instance;
                var ranger = store?.Get("path_ranger_score") ?? 0;
                var enforcer = store?.Get("path_enforcer_score") ?? 0;
                var support = store?.Get("path_support_score") ?? 0;
                var diplomat = store?.Get("path_diplomat_score") ?? 0;
                var rogue = store?.Get("path_rogue_score") ?? 0;
                ModLogger.Info("PATH", $"session_heartbeat: ranger={ranger} enforcer={enforcer} support={support} diplomat={diplomat} rogue={rogue}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PATH", "session heartbeat threw", ex);
            }
        }

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

        private void OnHeroGainedSkill(Hero hero, SkillObject skill, int change, bool shouldNotify)
        {
            try
            {
                if (hero != Hero.MainHero || skill == null || change <= 0)
                {
                    return;
                }
                if (!SkillToPath.TryGetValue(skill.StringId, out var path))
                {
                    return;
                }
                BumpPath(path, change, "skill_level");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PATH", "OnHeroGainedSkill threw", ex);
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
                if (FlagStore.Instance?.Has("path_resisted_" + path) == true)
                {
                    amount = (int)(amount * 0.5f);
                }
                var current = QualityStore.Instance?.Get(qualityKey) ?? 0;
                QualityStore.Instance?.Set(qualityKey, Math.Max(0, Math.Min(100, current + amount)), reason);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PATH", "BumpPath threw", ex);
            }
        }
    }
}
