using System;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Adds +2 morale from "Officer's Mess" to the main party while enlisted at T7+.
    /// Postfix on the public <c>DefaultPartyMoraleModel.GetEffectivePartyMorale</c>
    /// (NOT the private <c>CalculateFoodVarietyMoraleBonus</c> — patching public methods
    /// avoids string-name targeting and survives Bannerlord patches better). Reading (a)
    /// per Plan 4 design lock: flat +2 regardless of food state, legible in the morale
    /// breakdown tooltip.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony patch class discovered via reflection")]
    [HarmonyPatch(typeof(DefaultPartyMoraleModel), nameof(DefaultPartyMoraleModel.GetEffectivePartyMorale))]
    public class OfficerMessMoralePatch
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Called by Harmony via reflection")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedParameter.Local", Justification = "Parameters required to match Harmony patch signature")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony convention: __result is a special injected parameter")]
        private static void Postfix(MobileParty mobileParty, ref ExplainedNumber __result)
        {
            try
            {
                if (mobileParty != MobileParty.MainParty)
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

                __result.Add(2f, new TextObject("{=officer_mess_bonus}Officer's Mess"));
            }
            catch (Exception ex)
            {
                ModLogger.Caught("OFFICER", "officer_mess_morale_postfix_failed", ex);
            }
        }
    }
}
