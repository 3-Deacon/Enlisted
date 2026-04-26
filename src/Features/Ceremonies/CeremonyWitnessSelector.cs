using System.Collections.Generic;
using Enlisted.Features.Companions;
using Enlisted.Features.Enlistment.Behaviors;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Ceremonies
{
    /// <summary>
    /// Picks ceremony witnesses for a given tier transition and returns them
    /// keyed by their storylet slot name (witness_sergeant, witness_field_medic,
    /// witness_lord, ...). CeremonyProvider seeds StoryletContext.ResolvedSlots
    /// from this map so scripted relation_change effects can address each
    /// witness via target_slot. Companions absent at the given tier (e.g.
    /// Veteran at T4→T5 — only spawns at T5) are simply omitted; the
    /// relation_change primitive handles missing slots gracefully.
    /// Witness composition per §4.4 of the rank-ceremony plan, narrowed to the
    /// five retained tier transitions (newTier 2/3/5/7/8).
    /// </summary>
    public static class CeremonyWitnessSelector
    {
        public static Dictionary<string, Hero> GetWitnessesForCeremony(int newTier)
        {
            var witnesses = new Dictionary<string, Hero>();
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return witnesses;
            }

            var spawned = CompanionLifecycleHandler.Instance?.GetSpawnedCompanions()
                          ?? new List<Hero>();
            foreach (var hero in spawned)
            {
                if (hero == null || !hero.IsAlive)
                {
                    continue;
                }
                var typeId = enlistment.GetCompanionTypeId(hero);
                if (string.IsNullOrEmpty(typeId))
                {
                    continue;
                }
                if (IsWitnessAtTier(typeId, newTier))
                {
                    witnesses["witness_" + typeId] = hero;
                }
            }

            // Lord witness is not in GetSpawnedCompanions(); add separately for
            // T4→T5 (lord watching obedience choice) and T6→T7 (commission).
            if (newTier == 5 || newTier == 7)
            {
                var lord = enlistment.EnlistedLord;
                if (lord != null && lord.IsAlive)
                {
                    witnesses["witness_lord"] = lord;
                }
            }

            return witnesses;
        }

        /// <summary>
        /// Witness presence rule per ceremony tier. Returns true when the
        /// archetype is part of the ceremony's witness list AND would be
        /// spawned by Plan 2 lifecycle at the given tier (Sergeant T1+,
        /// Field Medic + Pathfinder T3+, Veteran T5+, QM Officer + Junior
        /// Officer T7+).
        /// </summary>
        private static bool IsWitnessAtTier(string archetype, int newTier)
        {
            switch (newTier)
            {
                case 2: // T1→T2 — Sergeant only
                    return archetype == "sergeant";
                case 3: // T2→T3 — Sergeant only (recruits in setup are NPCs)
                    return archetype == "sergeant";
                case 5: // T4→T5 — Sergeant + Field Medic (lord added separately)
                    return archetype == "sergeant"
                        || archetype == "field_medic";
                case 7: // T6→T7 commission — Sergeant + Field Medic + Pathfinder + Veteran (lord added separately)
                    return archetype == "sergeant"
                        || archetype == "field_medic"
                        || archetype == "pathfinder"
                        || archetype == "veteran";
                case 8: // T7→T8 — Junior Officer + QM Officer + Veteran
                    return archetype == "junior_officer"
                        || archetype == "qm_officer"
                        || archetype == "veteran";
                default:
                    return false;
            }
        }
    }
}
