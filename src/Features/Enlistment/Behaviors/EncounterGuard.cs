using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Mod.Entry;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Enlistment.Behaviors
{
	/// <summary>
	/// Utility class for managing player party attachment and encounter transitions.
	/// Provides methods for attaching the player's party to the lord's party for following,
	/// and handling encounter state transitions safely.
	/// </summary>
	internal static class EncounterGuard
	{
		/// <summary>
		/// Initializes the EncounterGuard system.
		/// Currently a placeholder as encounter prevention is handled by Harmony patches.
		/// </summary>
		public static void Initialize()
		{
			// Encounter prevention is handled by Harmony patches that prevent
			// player-initiated encounters while enlisted
		}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "May be used for future functionality")]
	private static Hero EnlistCurrentLord()
	{
		// Use public property instead of reflection - no processing cost
		return EnlistmentBehavior.Instance?.CurrentLord;
	}

		/// <summary>
		/// Safely leaves the current player encounter and activates the enlisted status menu.
		/// Encounter exit and menu activation are deferred to the next frame to prevent
		/// timing conflicts with the game's rendering system during state transitions.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "May be called from other modules")]
	internal static void TryLeaveEncounter()
		{
			try
			{
				var hasEncounter = PlayerEncounter.Current != null;
				ModLogger.Debug("EncounterGuard", $"TryLeaveEncounter called: hasEncounter={hasEncounter}");

				if (hasEncounter)
				{
					// Defer encounter finishing to the next frame to avoid timing conflicts
					// This prevents assertion failures that can occur during encounter exit
					NextFrameDispatcher.RunNextFrame(() =>
					{
						var stillHasEncounter = PlayerEncounter.Current != null;
						ModLogger.Debug("EncounterGuard", $"Deferred encounter finish: stillHasEncounter={stillHasEncounter}");

					if (stillHasEncounter)
					{
						PlayerEncounter.LeaveEncounter = true;
						PlayerEncounter.Finish(); // Default parameter forcePlayerOutFromSettlement=true is sufficient
						ModLogger.Debug("EncounterGuard", "PlayerEncounter finished successfully");
					}
					});

				// Defer menu activation to the next frame after encounter exit completes
				// This ensures the menu activates cleanly after state transitions
				NextFrameDispatcher.RunNextFrame(() =>
				{
					ModLogger.Debug("EncounterGuard", "Activating enlisted menu after encounter exit (via SafeActivate)");
					// Use SafeActivateEnlistedMenu to respect siege detection
					EnlistedMenuBehavior.SafeActivateEnlistedMenu();
				}, true);
				}
			}
			catch (Exception ex)
			{
				ModLogger.Caught("EncounterGuard", "Error in TryLeaveEncounter", ex);
			}
		}

		/// <summary>
		/// Attaches the player's party to the lord's party by matching positions directly.
		/// Uses direct position assignment instead of AI escort to avoid complications
		/// with the AI system and prevent assertion failures during battle state transitions.
		/// Only positions the party when not in an active battle.
		/// </summary>
		/// <param name="lord">The lord whose party the player should follow.</param>
		public static void TryAttachOrEscort(Hero lord)
		{
			var main = MobileParty.MainParty;
			var target = lord?.PartyBelongedTo;
			if (main == null || target == null)
			{
				return;
			}

			// 1.3.4 API: Position2D is now GetPosition2D property
			var offset = main.GetPosition2D - target.GetPosition2D;
			var distance = offset.Length;
			var attachmentName = main.AttachedTo?.LeaderHero?.Name?.ToString() ?? main.AttachedTo?.Name?.ToString() ?? "none";
			var targetName = target.LeaderHero?.Name?.ToString() ?? target.Name?.ToString() ?? "unknown";

			// Log only if state changes to reduce spam
			if (main.AttachedTo != target)
			{
				ModLogger.Debug("Following", $"AttachOrEscort check -> target={targetName}, distance={distance:F2}, attachedTo={attachmentName}, mapEvent={(main.Party.MapEvent != null)}");
			}

			// Use Escort AI for following behavior
			// NOTE: We CANNOT use AttachedTo property - the game assumes attached parties are in an army
			// and GetGenericStateMenu() will crash with null reference on mainParty.Army if AttachedTo != null
			// Escort AI provides smooth following without the army requirement
			if (main.Party.MapEvent == null || main.Party.MapEvent.IsFinalized)
			{
				// ALWAYS set escort AI - TargetParty may be set but movement mode might be "hold"
				// after TryReleaseEscort() is called. Re-applying ensures movement continues.
				// 1.3.4 API: SetMoveEscortParty is on MobileParty directly
				main.SetMoveEscortParty(target, MobileParty.NavigationType.Default, false);

				// Set camera to follow the lord's party for better visual experience
					try
					{
						target.Party.SetAsCameraFollowParty();
					}
					catch { /* Best effort */ }
			}
			else
			{
				ModLogger.Debug("Following", "Skipping follow command - player is in active battle");
			}
		}

		// NOTE: TryAttach method was removed because setting AttachedTo property
		// causes GetGenericStateMenu() to crash - it assumes attached parties are in an army
		// and dereferences mainParty.Army which is null for enlisted players

		#region Party Visual Hiding

		/// <summary>
		/// Hides the player's party visual (3D model on map) using the native IsVisible property.
		/// The native MobilePartyVisual system automatically fades out when IsVisible = false.
		/// NOTE: This does NOT hide the player nameplate - the Harmony patch handles that separately.
		/// </summary>
	public static void HidePlayerPartyVisual()
	{
		var mainParty = MobileParty.MainParty;
		if (mainParty == null)
		{
			return;
		}

        var wasVisible = mainParty.IsVisible;
        mainParty.IsVisible = false;
        ModLogger.Info("EncounterGuard", $"HidePlayerPartyVisual: was={wasVisible}, now={mainParty.IsVisible}");
		}

		/// <summary>
		/// Shows the player's party visual (3D model on map) using the native IsVisible property.
		/// </summary>
	public static void ShowPlayerPartyVisual()
	{
		var mainParty = MobileParty.MainParty;
		if (mainParty == null)
		{
			return;
		}

        var wasVisible = mainParty.IsVisible;
        mainParty.IsVisible = true;
        ModLogger.Info("EncounterGuard", $"ShowPlayerPartyVisual: was={wasVisible}, now={mainParty.IsVisible}");
			}

		#endregion
	}
}
