using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using Enlisted.Mod.Core;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Mod.GameAdapters.Patches
{
	/// <summary>
	/// Static flag to track when we're in the middle of a discharge operation.
	/// This prevents relation penalties from being applied during kingdom restoration.
	/// </summary>
	public static class DischargeState
	{
		/// <summary>
		/// Flag indicating if we're currently discharging the player from enlistment.
		/// When true, relation penalties will be suppressed.
		/// </summary>
		public static bool IsDischarging { get; set; }
	}

	/// <summary>
	/// Harmony patch that intercepts ChangeRelationAction to prevent relation penalties
	/// during enlistment discharge. This suppresses the -20 relation penalty that would
	/// normally be applied when leaving a kingdom during discharge.
	/// </summary>
	[HarmonyPatch(typeof(ChangeRelationAction), "ApplyRelationChangeBetweenHeroes")]
	public class DischargeRelationPenaltyPatch
	{
		/// <summary>
		/// Prefix method that runs before ChangeRelationAction.ApplyRelationChangeBetweenHeroes.
		/// Suppresses negative relation changes during discharge.
		/// Called by Harmony via reflection.
		/// </summary>
		[HarmonyPrefix]
		public static bool Prefix(Hero hero, Hero gainedRelationWith, int relationChange, bool showQuickNotification)
		{
			try
			{
                if (!EnlistedActivation.EnsureActive())
                {
                    return true;
                }

				// Only suppress negative relation changes for the player during discharge
				var involvesPlayer = hero == Hero.MainHero || gainedRelationWith == Hero.MainHero;

				if (!involvesPlayer)
				{
					return true; // Not involving the player - allow normal behavior
				}

				var otherHero = hero == Hero.MainHero ? gainedRelationWith : hero;
				var otherHeroName = otherHero?.Name?.ToString() ?? "null";

				ModLogger.Debug("Discharge", $"RelationChange intercepted: player<->{otherHeroName}, change={relationChange}, isDischarging={DischargeState.IsDischarging}, showNotification={showQuickNotification}");

				// Check if we're discharging and this is a negative relation change
				// The typical penalty when leaving a kingdom is -20 per clan leader
				if (DischargeState.IsDischarging && relationChange < 0)
				{
					// Suppress the relation penalty during discharge
					// This prevents the -20 penalty that would normally apply when leaving a kingdom
					// The flag is set in DischargeHelper.RestoreKingdomWithoutPenalties before calling
					// ChangeKingdomAction methods, so any relation changes during that time are suppressed
					ModLogger.Info("Discharge", $"Suppressed relation penalty of {relationChange} during discharge (OtherHero: {otherHeroName})");
					return false; // Suppress the penalty
				}

				return true; // Allow normal relation changes
			}
			catch (Exception ex)
			{
				ModLogger.Caught("Discharge", "Error in relation penalty suppression", ex);
				return true; // Fail open - allow normal behavior
			}
		}
	}

	/// <summary>
	/// Helper class to manage discharge state and prevent relation penalties.
	/// Used by EnlistmentBehavior when restoring the player's original kingdom.
	/// </summary>
	public static class DischargeHelper
	{
		/// <summary>
		/// Execute a kingdom change action without applying relation penalties.
		/// Used when discharging the player from enlistment to restore their original kingdom.
		/// </summary>
		public static void RestoreKingdomWithoutPenalties(Clan clan, Kingdom targetKingdom)
		{
			var clanName = clan?.Name?.ToString() ?? "null";
			var currentKingdomName = clan?.Kingdom?.Name?.ToString() ?? "independent";
			var targetKingdomName = targetKingdom?.Name?.ToString() ?? "independent";

			ModLogger.Debug("Discharge", $"RestoreKingdomWithoutPenalties called: clan={clanName}, current={currentKingdomName}, target={targetKingdomName}");

			try
			{
				// Mark that we're discharging to suppress penalties
				DischargeState.IsDischarging = true;
				ModLogger.Debug("Discharge", "DischargeState.IsDischarging set to TRUE - relation penalties will be suppressed");

				try
				{
					var currentKingdom = clan?.Kingdom;
					var isMercenary = clan?.IsUnderMercenaryService ?? false;

					ModLogger.Debug("Discharge", $"Current state: currentKingdom={currentKingdom?.Name?.ToString() ?? "null"}, isMercenary={isMercenary}");

					if (currentKingdom != null && targetKingdom == null)
					{
						// Leave kingdom to become independent (use mercenary leave since we joined as mercenary)
						ModLogger.Debug("Discharge", "Executing ApplyByLeaveKingdomAsMercenary to become independent");
						ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(clan, false);
						ModLogger.Info("Discharge", "Restored player clan to independent status without penalties");
					}
					else if (currentKingdom != targetKingdom && targetKingdom != null)
					{
						// Join a different kingdom (1.3.4 API: added CampaignTime parameter)
						ModLogger.Debug("Discharge", $"Executing ApplyByJoinToKingdom to join {targetKingdomName}");
						ChangeKingdomAction.ApplyByJoinToKingdom(clan, targetKingdom, default(CampaignTime), false);
						ModLogger.Info("Discharge", $"Restored player clan to {targetKingdomName} without penalties");
					}
					else
					{
						ModLogger.Debug("Discharge", "No kingdom change needed - current matches target");
					}
				}
				finally
				{
					// Always clear the discharge flag, even if an error occurs
					DischargeState.IsDischarging = false;
					ModLogger.Debug("Discharge", "DischargeState.IsDischarging set to FALSE - relation penalties restored to normal");
				}
			}
			catch (Exception ex)
			{
				ModLogger.Caught("Discharge", "Error restoring kingdom without penalties", ex);
				DischargeState.IsDischarging = false; // Ensure flag is cleared
				throw; // Re-throw to allow error handling in caller
			}
		}
	}
}

