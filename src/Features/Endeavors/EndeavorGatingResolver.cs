using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;

namespace Enlisted.Features.Endeavors
{
    /// <summary>
    /// Hybrid gating resolver per Plan 5 §4.2: an endeavor unlocks when
    /// (player skill ≥ self_gate_skill_threshold for any axis) OR a spawned
    /// companion matches any companion_slot archetype. Required companion slots
    /// (slot.Required = true) make the companion side mandatory rather than
    /// alternative. Tier_min, flag_gates, and flag_blockers are ALL applied as
    /// AND filters on top of the hybrid skill/companion gate.
    /// </summary>
    public static class EndeavorGatingResolver
    {
        public enum LockReason
        {
            Available,
            BelowTier,
            FlagGateMissing,
            FlagBlockerSet,
            SkillAndCompanionMissing,
            RequiredCompanionMissing,
        }

        public readonly struct Resolution
        {
            public bool IsAvailable { get; }
            public LockReason Reason { get; }
            public string DetailKey { get; }

            public Resolution(bool isAvailable, LockReason reason, string detailKey)
            {
                IsAvailable = isAvailable;
                Reason = reason;
                DetailKey = detailKey;
            }
        }

        public static bool IsAvailable(EndeavorTemplate template, Hero player, IEnumerable<Hero> spawnedCompanions)
        {
            return Resolve(template, player, spawnedCompanions).IsAvailable;
        }

        public static Resolution Resolve(EndeavorTemplate template, Hero player, IEnumerable<Hero> spawnedCompanions)
        {
            if (template == null || player == null)
            {
                return new Resolution(false, LockReason.SkillAndCompanionMissing, string.Empty);
            }

            // Tier gate
            var tier = EnlistmentBehavior.Instance?.EnlistmentTier ?? 1;
            if (tier < template.TierMin)
            {
                return new Resolution(false, LockReason.BelowTier, $"tier {tier} < {template.TierMin}");
            }

            // Flag gates: all listed flags must be set
            var flagStore = FlagStore.Instance;
            if (flagStore != null)
            {
                foreach (var fg in template.FlagGates)
                {
                    if (!flagStore.Has(fg))
                    {
                        return new Resolution(false, LockReason.FlagGateMissing, fg);
                    }
                }
                foreach (var fb in template.FlagBlockers)
                {
                    if (flagStore.Has(fb))
                    {
                        return new Resolution(false, LockReason.FlagBlockerSet, fb);
                    }
                }
            }

            var spawnedList = spawnedCompanions as IList<Hero> ?? spawnedCompanions?.ToList() ?? new List<Hero>();

            // Required slots: every slot.Required = true must have a matching spawned companion
            foreach (var slot in template.CompanionSlots)
            {
                if (!slot.Required)
                {
                    continue;
                }
                if (!HasMatchingCompanion(slot.Archetype, spawnedList))
                {
                    return new Resolution(false, LockReason.RequiredCompanionMissing, slot.Archetype);
                }
            }

            // Hybrid: skill axis pass OR any non-required slot has a matching companion
            if (PlayerMeetsSkillThreshold(player, template))
            {
                return new Resolution(true, LockReason.Available, string.Empty);
            }
            foreach (var slot in template.CompanionSlots)
            {
                if (HasMatchingCompanion(slot.Archetype, spawnedList))
                {
                    return new Resolution(true, LockReason.Available, string.Empty);
                }
            }

            return new Resolution(false, LockReason.SkillAndCompanionMissing, string.Empty);
        }

        public static bool PlayerMeetsSkillThreshold(Hero player, EndeavorTemplate template)
        {
            if (player == null || template == null || template.SelfGateSkillThreshold <= 0)
            {
                return false;
            }
            foreach (var axisId in template.SkillAxis)
            {
                var skill = ResolveSkill(axisId);
                if (skill == null)
                {
                    continue;
                }
                if (player.GetSkillValue(skill) >= template.SelfGateSkillThreshold)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasMatchingCompanion(string archetype, IEnumerable<Hero> spawnedCompanions)
        {
            if (string.IsNullOrEmpty(archetype) || spawnedCompanions == null)
            {
                return false;
            }
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return false;
            }
            foreach (var hero in spawnedCompanions)
            {
                if (hero == null || !hero.IsAlive)
                {
                    continue;
                }
                var typeId = enlistment.GetCompanionTypeId(hero);
                if (string.Equals(typeId, archetype, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static TaleWorlds.Core.SkillObject ResolveSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return null;
            }
            return Skills.All.FirstOrDefault(
                s => string.Equals(s.StringId, skillId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
