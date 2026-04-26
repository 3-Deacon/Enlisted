using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Discovery-roll math for Rogue endeavors per Plan 5 §4.6. Effective per-phase
    /// risk is the template's base risk reduced by Charm contributions from the
    /// player and any assigned agents (companion contributes at half weight).
    /// EndeavorRunner calls RollDiscovery before applying scrutiny effects on each
    /// Rogue phase resolution.
    /// </summary>
    public static class ScrutinyRiskCalculator
    {
        // Charm contribution coefficients per Plan 5 §4.6:
        // effective = baseRisk - 0.001 * (player.Charm + 0.5 * sum(companionCharm))
        private const float PlayerCharmWeight = 0.001f;
        private const float CompanionCharmWeight = 0.0005f;

        /// <summary>
        /// Returns the effective per-phase discovery probability after Charm
        /// reductions, clamped to [0.0, 1.0]. Pure function; safe to call from UI
        /// to surface "what's my risk" tooltips without rolling.
        /// </summary>
        public static float ComputeEffectiveRisk(float baseRisk, Hero player, IEnumerable<Hero> agents)
        {
            var reduction = 0f;
            if (player != null)
            {
                var charm = GetCharm(player);
                reduction += PlayerCharmWeight * charm;
            }
            if (agents != null)
            {
                foreach (var agent in agents)
                {
                    if (agent == null)
                    {
                        continue;
                    }
                    var charm = GetCharm(agent);
                    reduction += CompanionCharmWeight * charm;
                }
            }
            var effective = baseRisk - reduction;
            return MBMath.ClampFloat(effective, 0f, 1f);
        }

        /// <summary>
        /// Rolls discovery for a single phase. Returns true when the random draw
        /// falls below the effective risk (player is discovered).
        /// </summary>
        public static bool RollDiscovery(float baseRisk, Hero player, IEnumerable<Hero> agents)
        {
            var risk = ComputeEffectiveRisk(baseRisk, player, agents);
            if (risk <= 0f)
            {
                return false;
            }
            return MBRandom.RandomFloat < risk;
        }

        private static int GetCharm(Hero hero)
        {
            try
            {
                var skill = Skills.All.FirstOrDefault(
                    s => string.Equals(s.StringId, "Charm", StringComparison.OrdinalIgnoreCase));
                if (skill == null)
                {
                    return 0;
                }
                return hero.GetSkillValue(skill);
            }
            catch
            {
                return 0;
            }
        }
    }
}
