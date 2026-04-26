using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Adds +6 HP/day to <see cref="Hero.MainHero"/>'s daily healing while enlisted at T7+.
    /// Postfix on <c>DefaultPartyHealingModel.GetDailyHealingHpForHeroes</c> appends an
    /// "Officer's Tent" line to the ExplainedNumber breakdown.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch(typeof(DefaultPartyHealingModel), nameof(DefaultPartyHealingModel.GetDailyHealingHpForHeroes))]
    public class OfficerTentHealingPatch
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Parameters required to match Harmony patch signature")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
        private static void Postfix(PartyBase party, ref ExplainedNumber __result)
        {
            try
            {
                if (party?.MobileParty != MobileParty.MainParty)
                {
                    return;
                }

                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                if (enlistment.EnlistmentTier < 7)
                {
                    return;
                }

                __result.Add(6f, new TextObject("{=officer_tent_bonus}Officer's Tent"));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "officer_tent_healing_postfix_failed", ex);
            }
        }
    }
}
