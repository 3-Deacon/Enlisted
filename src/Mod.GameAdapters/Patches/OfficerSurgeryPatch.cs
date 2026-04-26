using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Adds +0.15 (15%) to the surgery survival chance for <see cref="Hero.MainHero"/>
    /// when leading the main party at T7+. Postfix on
    /// <c>DefaultPartyHealingModel.GetSurgeryChance</c>; the method returns <c>float</c>
    /// (NOT ExplainedNumber), so the bonus does not appear in any tooltip — surgery
    /// chance has no UI breakdown like ExplainedNumber-backed stats do.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch(typeof(DefaultPartyHealingModel), nameof(DefaultPartyHealingModel.GetSurgeryChance))]
    public class OfficerSurgeryPatch
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Parameters required to match Harmony patch signature")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
        private static void Postfix(PartyBase party, ref float __result)
        {
            try
            {
                if (party?.MobileParty?.LeaderHero != Hero.MainHero)
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

                __result += 0.15f;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "officer_surgery_postfix_failed", ex);
            }
        }
    }
}
