using System;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches
{
    /// <summary>
    /// Compensates for the enlisted player's negative impact on army cohesion.
    ///
    /// When enlisted, the player's party is counted as a separate party in the army,
    /// which causes cohesion penalties:
    /// - Each party in army: -1 cohesion/day
    /// - Low member parties (≤10): additional penalty
    /// - Low morale parties (≤25): additional penalty
    /// - Starving parties: additional penalty
    ///
    /// Since the enlisted player is conceptually embedded with their lord (not a separate
    /// party), these penalties are not thematically appropriate. This patch adds a
    /// compensating bonus to offset the penalties the player's party causes.
    ///
    /// Note: The player receives rations independently through the supply system, but
    /// the cohesion calculation may still see the player party as starving. We compensate
    /// for this because an embedded soldier shouldn't count as a separate starving party.
    ///
    /// Uses Postfix approach for maximum compatibility - doesn't block or modify the
    /// native calculation, just adds a compensating modifier afterward.
    /// </summary>
    [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel),
        nameof(DefaultArmyManagementCalculationModel.CalculateDailyCohesionChange))]
    public static class ArmyCohesionExclusionPatch
    {
        private static readonly TextObject CohesionCompensationText =
            new TextObject("{=enlisted_cohesion}Enlisted soldier (embedded)");

        /// <summary>
        /// Postfix that adds a compensating cohesion bonus to offset the enlisted player's
        /// negative impact on army cohesion. The player is embedded with their lord, so they
        /// shouldn't count as a separate party for cohesion purposes.
        /// Called by Harmony via reflection.
        /// </summary>
        [HarmonyPostfix]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public static void Postfix(Army army, ref ExplainedNumber __result)
        {
            try
            {
                if (!EnlistedActivation.EnsureActive())
                {
                    return;
                }

                // Check if player is enlisted
                var enlistment = EnlistmentBehavior.Instance;
                if (enlistment?.IsEnlisted != true)
                {
                    return;
                }

                var mainParty = MobileParty.MainParty;
                if (mainParty == null)
                {
                    return;
                }

                // Check if player is in this specific army
                if (mainParty.Army != army)
                {
                    return;
                }

                // Player is enlisted and in this army - calculate compensation
                // These values mirror the penalties in CalculateCohesionChangeInternal

                // 1. Compensate for party count penalty (-1 per party in army)
                var partyCountCompensation = 1f;

                // 2. Compensate for low member count penalty if applicable
                // Native applies: -((num3 + 1) / 2) where num3 = count of parties with ≤10 members
                // For a single party, that's -(1 + 1) / 2 = -1, but then halved again = -0.5
                var lowMemberCompensation = 0f;
                if (mainParty.Party.NumberOfHealthyMembers <= 10)
                {
                    lowMemberCompensation = 0.5f;
                }

                // 3. Compensate for low morale penalty if applicable
                // Native applies: -((num2 + 1) / 2) where num2 = count of parties with morale ≤25
                // For a single party, that's -(1 + 1) / 2 = -1, but then halved again = -0.5
                var lowMoraleCompensation = 0f;
                if (mainParty.Morale <= 25.0)
                {
                    lowMoraleCompensation = 0.5f;
                }

                // 4. Compensate for starvation penalty if applicable
                // Native applies: -((num1 + 1) / 2) where num1 = count of starving parties
                // For a single party, that's -(1 + 1) / 2 = -1, but then halved again = -0.5
                // Note: Player receives rations independently, but may still appear as starving to cohesion calculation
                var starvationCompensation = 0f;
                if (mainParty.Party.IsStarving)
                {
                    starvationCompensation = 0.5f;
                }

                var totalCompensation = partyCountCompensation + lowMemberCompensation + lowMoraleCompensation + starvationCompensation;

                // Apply AI multiplier if this is not the player's own army
                // Native code applies 0.25x multiplier to AI armies
                if (army.LeaderParty != MobileParty.MainParty)
                {
                    totalCompensation *= 0.25f;
                }

                // Add the compensation to the result
                __result.Add(totalCompensation, CohesionCompensationText);

                ModLogger.Debug("Cohesion",
                    $"Added cohesion compensation: +{totalCompensation:F2} " +
                    $"(party:{partyCountCompensation}, lowMember:{lowMemberCompensation}, lowMorale:{lowMoraleCompensation}, starving:{starvationCompensation})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("Cohesion", "Error in cohesion exclusion patch", ex);
                // Fail open - don't modify result on error
            }
        }
    }
}
