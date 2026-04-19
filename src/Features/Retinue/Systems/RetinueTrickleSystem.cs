using System;
using Enlisted.Features.Retinue.Core;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Enlisted.Features.Retinue.Systems
{
    /// <summary>
    /// Handles the free, slow replenishment of retinue soldiers via daily tick.
    /// Uses context-aware rates based on battle outcomes, territory, and peace/war status.
    ///
    /// Trickle Rates:
    ///   Victory (within 3 days): 1 per 2 days (battle survivors join up)
    ///   Defeat (within 5 days): 0 - BLOCKED (morale recovering)
    ///   Friendly territory: 1 per 3 days (local levies assigned)
    ///   On campaign (default): 1 per 4-5 days (transfers from rearguard)
    ///   At peace (5+ days no battle): 1 per 2 days (training complete)
    ///
    /// V2.0: Now requires Commander rank (T7+) for trickle to activate.
    /// V2.1: Context-aware trickle rates based on campaign state.
    /// </summary>
    public sealed class RetinueTrickleSystem : CampaignBehaviorBase
    {
        private const string LogCategory = "TRICKLE";

        // Configurable trickle parameters
        private const int SoldiersPerTrickle = 1;

        // Context-aware trickle intervals (in days)
        private const int VictoryInterval = 2;       // Fast: battle survivors join
        private const int PeaceInterval = 2;         // Fast: training complete
        private const int FriendlyTerritoryInterval = 3;  // Medium: local levies
        private const int CampaignInterval = 4;      // Slow: rearguard transfers
        private const int DefeatBlockDays = 5;       // Blocked after defeat
        private const int VictoryBonusDays = 3;      // Victory bonus window
        private const int PeaceThresholdDays = 5;    // Days without battle for peace bonus

        // Friendly territory search radius
        private const float FriendlyTerritoryRadius = 30f;

        public static RetinueTrickleSystem Instance { get; private set; }

        public RetinueTrickleSystem()
        {
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            ModLogger.Debug(LogCategory, "RetinueTrickleSystem registered for daily tick and battle tracking");
        }

        /// <summary>
        /// Tracks battle outcomes when map events (battles) end.
        /// Updates RetinueState with last battle time and result.
        /// </summary>
        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                // Only track battles where the player participated
                if (!mapEvent.IsPlayerMapEvent)
                {
                    return;
                }

                // Only track actual battles (not raids/sieges without combat)
                if (mapEvent.EventType != MapEvent.BattleTypes.FieldBattle &&
                    mapEvent.EventType != MapEvent.BattleTypes.Siege &&
                    mapEvent.EventType != MapEvent.BattleTypes.SallyOut)
                {
                    return;
                }

                var state = RetinueManager.Instance?.State;
                if (state == null)
                {
                    return;
                }

                // Record battle outcome with granular handling (EC5)
                state.LastBattleTime = CampaignTime.Now;

                // Determine outcome with withdrawal/draw detection
                if (mapEvent.HasWinner)
                {
                    // Clear winner exists
                    state.LastBattleWon = mapEvent.WinningSide == mapEvent.PlayerSide;
                    state.LastBattleOutcome = state.LastBattleWon
                        ? Data.BattleOutcome.Victory
                        : Data.BattleOutcome.Defeat;
                }
                else
                {
                    // No winner - was it a withdrawal or a draw?
                    // Check if player side took casualties - indicates a tactical withdrawal
                    var playerSideEnum = mapEvent.PlayerSide;
                    var playerSideData = mapEvent.GetMapEventSide(playerSideEnum);
                    var playerCasualties = playerSideData?.TroopCasualties ?? 0;

                    if (playerCasualties > 0)
                    {
                        // Took casualties without resolution = withdrawal
                        state.LastBattleOutcome = Data.BattleOutcome.Withdrawal;
                        state.LastBattleWon = false; // Treat as non-victory for legacy
                    }
                    else
                    {
                        // No casualties, no resolution = draw/standoff
                        state.LastBattleOutcome = Data.BattleOutcome.Draw;
                        state.LastBattleWon = false; // Treat as non-victory for legacy
                    }
                }

                ModLogger.Info(LogCategory, $"Battle ended: {state.LastBattleOutcome}. Trickle rate will be adjusted.");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("TRICKLE", "Error tracking battle outcome", ex);
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Trickle state is stored in RetinueState, managed by RetinueManager
            // No additional serialization needed here
        }

        /// <summary>
        /// Daily tick handler - checks eligibility and potentially adds a soldier.
        /// Uses context-aware trickle intervals based on battle outcomes and territory.
        /// </summary>
        private void OnDailyTick()
        {
            try
            {
                if (!CanTrickleReplenish(out var reason))
                {
                    // Only log if there's a specific blocking reason (not just "no type selected")
                    if (!string.IsNullOrEmpty(reason) && !reason.Contains("no type"))
                    {
                        ModLogger.Debug(LogCategory, $"Trickle skipped: {reason}");
                    }
                    return;
                }

                var manager = RetinueManager.Instance;
                var state = manager?.State;
                if (state == null)
                {
                    return;
                }

                // Get current trickle context and interval
                var context = GetCurrentTrickleContext(state);
                var interval = GetContextualInterval(context);

                // Check if trickle is blocked (after defeat)
                if (context == TrickleContext.PostDefeat)
                {
                    var resumeIn = DefeatBlockDays - (int)state.GetDaysSinceLastBattle();
                    ModLogger.Debug(LogCategory, $"Trickle blocked: morale recovering, resumes in {resumeIn} days");
                    return;
                }

                // Increment days since last trickle
                state.DaysSinceLastTrickle++;

                // Check if we've reached the contextual trickle interval
                if (state.DaysSinceLastTrickle < interval)
                {
                    ModLogger.Debug(LogCategory,
                        $"Trickle waiting: day {state.DaysSinceLastTrickle}/{interval} ({context})");
                    return;
                }

                // Reset counter
                state.DaysSinceLastTrickle = 0;

                // Calculate safe add count with overfill protection
                var safeCount = CalculateSafeAddCount(SoldiersPerTrickle);
                if (safeCount <= 0)
                {
                    ModLogger.Debug(LogCategory, "Trickle skipped: already at capacity");
                    return;
                }

                // Add the soldier
                if (manager.TryAddSoldiers(safeCount, state.SelectedTypeId, out var added, out var message))
                {
                    var currentCount = state.TotalSoldiers;
                    var enlistment = EnlistmentBehavior.Instance;
                    var tierCapacity = RetinueManager.GetTierCapacity(enlistment?.EnlistmentTier ?? RetinueManager.CommanderTier1);

                    ModLogger.Info(LogCategory,
                        $"Trickle added {added} soldier ({currentCount}/{tierCapacity}) via {context}");

                    // Show contextual notification to player
                    ShowTrickleNotification(context);
                }
                else
                {
                    ModLogger.Debug(LogCategory, $"Trickle failed: {message}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error in daily trickle tick", ex);
            }
        }

        /// <summary>
        /// Determines the current trickle context based on battle history and location.
        /// Priority: PostDefeat (blocked) > PostVictory (bonus) > FriendlyTerritory > Peace > Default
        /// </summary>
        private TrickleContext GetCurrentTrickleContext(Data.RetinueState state)
        {
            var daysSinceBattle = state.GetDaysSinceLastBattle();

            // Check post-battle states first (highest priority)
            if (daysSinceBattle >= 0)
            {
                // Use granular outcome if available (EC5), fall back to LastBattleWon for legacy saves
                var outcome = state.LastBattleOutcome;

                // Handle each outcome type with appropriate time window
                switch (outcome)
                {
                    case Data.BattleOutcome.Defeat:
                        // Post-defeat: blocked for 5 days
                        if (daysSinceBattle < DefeatBlockDays)
                        {
                            return TrickleContext.PostDefeat;
                        }
                        break;

                    case Data.BattleOutcome.Victory:
                        // Post-victory: bonus rate for 3 days
                        if (daysSinceBattle < VictoryBonusDays)
                        {
                            return TrickleContext.PostVictory;
                        }
                        break;

                    case Data.BattleOutcome.Withdrawal:
                        // Withdrawal: no bonus, no penalty - uses default rate for 3 days
                        if (daysSinceBattle < VictoryBonusDays)
                        {
                            return TrickleContext.PostWithdrawal;
                        }
                        break;

                    case Data.BattleOutcome.Draw:
                        // Draw: slight bonus for 3 days (treated like friendly territory)
                        if (daysSinceBattle < VictoryBonusDays)
                        {
                            return TrickleContext.PostDraw;
                        }
                        break;

                    case Data.BattleOutcome.Unknown:
                    default:
                        // Fall back to legacy LastBattleWon check
                        if (!state.LastBattleWon && daysSinceBattle < DefeatBlockDays)
                        {
                            return TrickleContext.PostDefeat;
                        }
                        if (state.LastBattleWon && daysSinceBattle < VictoryBonusDays)
                        {
                            return TrickleContext.PostVictory;
                        }
                        break;
                }

                // Long peace (5+ days without battle): training complete
                if (daysSinceBattle >= PeaceThresholdDays)
                {
                    return TrickleContext.Peace;
                }
            }
            else
            {
                // No battle tracked yet, treat as peace
                return TrickleContext.Peace;
            }

            // Check territory
            if (IsInFriendlyTerritory())
            {
                return TrickleContext.FriendlyTerritory;
            }

            // Default: on campaign
            return TrickleContext.Default;
        }

        /// <summary>
        /// Gets the trickle interval in days for a given context.
        /// </summary>
        private static int GetContextualInterval(TrickleContext context)
        {
            return context switch
            {
                TrickleContext.PostVictory => VictoryInterval,        // 2 days
                TrickleContext.Peace => PeaceInterval,                 // 2 days
                TrickleContext.FriendlyTerritory => FriendlyTerritoryInterval, // 3 days
                TrickleContext.PostDraw => FriendlyTerritoryInterval,  // 3 days (slight bonus)
                TrickleContext.PostWithdrawal => CampaignInterval,     // 4 days (neutral, no bonus/penalty)
                TrickleContext.PostDefeat => int.MaxValue,             // Blocked
                _ => CampaignInterval                                   // 4 days
            };
        }

        /// <summary>
        /// Checks if the lord's party is in or near a friendly settlement (same faction).
        /// A settlement is friendly if its owner is in the same kingdom as the lord's faction.
        /// </summary>
        public static bool IsInFriendlyTerritory()
        {
            var enlistment = EnlistmentBehavior.Instance;
            var lordParty = enlistment?.EnlistedLord?.PartyBelongedTo;
            if (lordParty == null)
            {
                return false;
            }

            // Check if currently in a friendly settlement
            var currentSettlement = lordParty.CurrentSettlement;
            if (currentSettlement != null)
            {
                var isFriendly = currentSettlement.MapFaction == lordParty.MapFaction ||
                                 (currentSettlement.OwnerClan?.Kingdom == lordParty.MapFaction as Kingdom);
                if (isFriendly)
                {
                    return true;
                }
            }

            // Check if we're near friendly territory by examining the closest settlement
            // Using SettlementHelper to find settlements around the party
            var searchData = Settlement.StartFindingLocatablesAroundPosition(
                lordParty.Position.ToVec2(), FriendlyTerritoryRadius);

            for (var settlement = Settlement.FindNextLocatable(ref searchData);
                 settlement != null;
                 settlement = Settlement.FindNextLocatable(ref searchData))
            {
                // Check if this settlement belongs to our faction
                if (settlement.MapFaction == lordParty.MapFaction ||
                    (settlement.OwnerClan?.Kingdom == lordParty.MapFaction as Kingdom))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets information about the current trickle rate for UI display.
        /// </summary>
        /// <returns>Tuple of (context description, days until next recruit, is blocked)</returns>
        public static (string ContextDescription, int DaysUntilNext, bool IsBlocked) GetTrickleStatusInfo()
        {
            var manager = RetinueManager.Instance;
            var state = manager?.State;
            if (state == null || !state.HasTypeSelected)
            {
                return ("No retinue", -1, true);
            }

            var context = Instance?.GetCurrentTrickleContext(state) ?? TrickleContext.Default;
            var interval = GetContextualInterval(context);

            if (context == TrickleContext.PostDefeat)
            {
                var resumeIn = DefeatBlockDays - (int)state.GetDaysSinceLastBattle();
                return ("Morale recovering", resumeIn, true);
            }

            var daysUntilNext = Math.Max(0, interval - state.DaysSinceLastTrickle);
            var desc = context switch
            {
                TrickleContext.PostVictory => "Victory bonus",
                TrickleContext.PostDraw => "Standoff recovery",
                TrickleContext.PostWithdrawal => "Regrouping",
                TrickleContext.Peace => "Training complete",
                TrickleContext.FriendlyTerritory => "Friendly territory",
                _ => "On campaign"
            };

            return (desc, daysUntilNext, false);
        }

        /// <summary>
        /// Checks if trickle replenishment can occur.
        /// V2.0: Requires Commander rank (T7+) for retinue trickle.
        /// </summary>
        /// <param name="reason">Out: reason if cannot trickle</param>
        /// <returns>True if trickle is allowed</returns>
        private static bool CanTrickleReplenish(out string reason)
        {
            reason = null;

            // Must be enlisted at Commander tier (T7+)
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null || !enlistment.IsEnlisted)
            {
                reason = "not enlisted";
                return false;
            }

            if (enlistment.EnlistmentTier < RetinueManager.CommanderTier1)
            {
                reason = $"tier {enlistment.EnlistmentTier} < {RetinueManager.CommanderTier1} (Commander rank required)";
                return false;
            }

            // Must have a retinue type selected
            var manager = RetinueManager.Instance;
            if (manager?.State == null)
            {
                reason = "no retinue manager";
                return false;
            }

            if (!manager.State.HasTypeSelected)
            {
                reason = "no type selected";
                return false;
            }

            // Must have room for more soldiers (tier capacity)
            var tierCapacity = RetinueManager.GetTierCapacity(enlistment.EnlistmentTier);
            var currentSoldiers = manager.State.TotalSoldiers;
            if (currentSoldiers >= tierCapacity)
            {
                reason = $"tier capacity full ({currentSoldiers}/{tierCapacity})";
                return false;
            }

            // Must have party space available
            var party = PartyBase.MainParty;
            if (party == null)
            {
                reason = "no party";
                return false;
            }

            var partySpace = party.PartySizeLimit - party.NumberOfAllMembers;
            if (partySpace <= 0)
            {
                reason = "party full";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculates how many soldiers can safely be added without exceeding limits.
        /// Uses the shared capacity check: Math.Min(tierCapacity - current, partyLimit - partyMembers)
        /// </summary>
        /// <param name="requested">Number of soldiers requested</param>
        /// <returns>Safe number to add (0 or more)</returns>
        private static int CalculateSafeAddCount(int requested)
        {
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment == null)
            {
                return 0;
            }

            var manager = RetinueManager.Instance;
            if (manager?.State == null)
            {
                return 0;
            }

            // Tier capacity check
            var tierCapacity = RetinueManager.GetTierCapacity(enlistment.EnlistmentTier);
            var currentSoldiers = manager.State.TotalSoldiers;
            var tierAvailable = tierCapacity - currentSoldiers;

            // Party size check
            var party = PartyBase.MainParty;
            if (party == null)
            {
                return 0;
            }

            var partySpace = party.PartySizeLimit - party.NumberOfAllMembers;

            // Take the more restrictive limit
            var maxCanAdd = Math.Min(tierAvailable, partySpace);
            var safeCount = Math.Min(requested, maxCanAdd);

            if (safeCount < requested)
            {
                ModLogger.Debug(LogCategory,
                    $"Overfill protection: requested {requested}, allowing {safeCount} " +
                    $"(tier: {tierAvailable}, party: {partySpace})");
            }

            return Math.Max(0, safeCount);
        }

        /// <summary>
        /// Shows a contextual notification when a soldier is added via trickle.
        /// Message varies based on how the soldier was acquired.
        /// </summary>
        private static void ShowTrickleNotification(TrickleContext context)
        {
            TextObject msg = context switch
            {
                TrickleContext.PostVictory => new TextObject("{=ct_trickle_battle_survivor}A survivor from the battle has joined your retinue."),
                TrickleContext.FriendlyTerritory => new TextObject("{=ct_trickle_local_levy}A local levy has been assigned to your command."),
                TrickleContext.Peace => new TextObject("{=ct_trickle_training_complete}A recruit has completed training and joined your retinue."),
                _ => new TextObject("{=ct_trickle_transfer}A soldier from the rearguard has been transferred to your retinue.")
            };

            MBInformationManager.AddQuickInformation(msg, soundEventPath: "event:/ui/notification/quest_update");
        }
    }

    /// <summary>
    /// Represents the current campaign context for determining trickle rates.
    /// </summary>
    public enum TrickleContext
    {
        /// <summary>Default on-campaign rate (1 per 4 days).</summary>
        Default,

        /// <summary>Victory within 3 days grants faster replenishment (1 per 2 days).</summary>
        PostVictory,

        /// <summary>Defeat within 5 days blocks replenishment entirely.</summary>
        PostDefeat,

        /// <summary>Withdrawal within 3 days - no bonus, no penalty (default rate).</summary>
        PostWithdrawal,

        /// <summary>Draw within 3 days grants slight bonus (1 per 3 days).</summary>
        PostDraw,

        /// <summary>Near friendly settlement grants medium rate (1 per 3 days).</summary>
        FriendlyTerritory,

        /// <summary>5+ days without battle grants peace bonus (1 per 2 days).</summary>
        Peace
    }
}

