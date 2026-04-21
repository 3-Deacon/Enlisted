using System;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// Resolves a hero's effective combat class (Infantry / Ranged / Cavalry / HorseArcher)
    /// from their CharacterObject's IsRanged / IsMounted flags.
    /// </summary>
    public static class CombatClassResolver
    {
        public static FormationClass Resolve(Hero hero)
        {
            try
            {
                if (hero?.CharacterObject == null)
                {
                    return FormationClass.Infantry;
                }

                var character = hero.CharacterObject;

                if (character.IsRanged && character.IsMounted)
                {
                    return FormationClass.HorseArcher;
                }

                if (character.IsMounted)
                {
                    return FormationClass.Cavalry;
                }

                if (character.IsRanged)
                {
                    return FormationClass.Ranged;
                }

                return FormationClass.Infantry;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("COMBATCLASS", "Resolve failed; defaulting to Infantry", ex);
                return FormationClass.Infantry;
            }
        }
    }
}
