using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Interface.Behaviors;
using Enlisted.Features.Retinue.Core;
using Enlisted.Features.Retinue.Data;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Enlisted.Features.Retinue.Systems
{
    /// <summary>
    /// Tracks retinue casualties during battles. Takes a snapshot of troop counts before battle,
    /// then compares against the actual roster after battle to sync our tracking state.
    /// Native casualty handling updates the roster automatically; we just reconcile our state.
    /// </summary>
    public sealed class RetinueCasualtyTracker : CampaignBehaviorBase
    {
        private const string LogCategory = "CASUALTYTRACKER";

        // Veteran emergence configuration
        private const float VeteranEmergenceChance = 0.15f; // 15% chance per eligible battle
        private const int MinBattlesForEmergence = 3;       // Must survive 3+ battles to be eligible

        // Available traits for emerging veterans. These define personality and flavor text.
        private static readonly string[] VeteranTraits =
        {
            "Brave",      // Charges into danger without hesitation
            "Lucky",      // Somehow always survives close calls
            "Sharp-Eyed", // Notices threats before others
            "Steady",     // Unshakeable under pressure
            "Iron Will"   // Never breaks, never retreats
        };

        // Pre-battle snapshot of retinue troop counts
        private Dictionary<string, int> _preBattleCounts;

        // Pre-battle snapshot of named veterans (for death detection)
        private List<string> _preBattleVeteranIds;

        // Track if we're currently in a battle
        private bool _isInBattle;

        public static RetinueCasualtyTracker Instance { get; private set; }

        public RetinueCasualtyTracker()
        {
            Instance = this;
            _preBattleCounts = new Dictionary<string, int>();
            _preBattleVeteranIds = new List<string>();
        }

        public override void RegisterEvents()
        {
            // Hook into battle start/end events
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);

            // Also hook into player battle end for additional tracking
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);

            // Daily sync to catch wounded soldiers who die from wounds between battles
            // This ensures trickle can activate sooner when deaths occur outside of combat
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

            ModLogger.Debug(LogCategory, "RetinueCasualtyTracker registered for battle events and daily sync");
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Pre-battle counts are transient and don't need to be saved
            // They're only valid during an active battle session
        }

        #region Battle Event Handlers

        /// <summary>
        /// Takes a snapshot of retinue counts when battle starts. Naval battles logged with dismount note.
        /// </summary>
        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attackerParty, PartyBase defenderParty)
        {
            try
            {
                if (!IsPlayerInvolvedInMapEvent(mapEvent))
                {
                    return;
                }

                var manager = RetinueManager.Instance;
                if (manager?.State == null || !manager.State.HasRetinue)
                {
                    return;
                }

                TakePreBattleSnapshot(manager.State);
                _isInBattle = true;

                var retinueCount = manager.State.TotalSoldiers;
                var retinueType = manager.State.SelectedTypeId ?? "unknown";
                var isNaval = mapEvent.IsNavalMapEvent;

                if (isNaval)
                {
                    var isMounted = retinueType is "cavalry" or "horse_archers";
                    var note = isMounted ? " [dismounted]" : "";
                    ModLogger.Info(LogCategory, $"NAVAL battle snapshot: {retinueCount} {retinueType}{note}");
                }
                else
                {
                    ModLogger.Info(LogCategory, $"Battle snapshot: {retinueCount} retinue soldiers");
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error in OnMapEventStarted", ex);
            }
        }

        /// <summary>
        /// Called when a map event (battle) ends. Reconciles casualties with our tracking state.
        /// </summary>
        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {
                // Only process if we took a snapshot for this battle
                if (!_isInBattle || _preBattleCounts == null || _preBattleCounts.Count == 0)
                {
                    return;
                }

                // Check if player was involved
                if (!IsPlayerInvolvedInMapEvent(mapEvent))
                {
                    return;
                }

                // Reconcile casualties
                ReconcileCasualties();
                _isInBattle = false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error in OnMapEventEnded", ex);
                _isInBattle = false;
            }
        }

        /// <summary>
        /// Called when a player battle specifically ends. Provides additional casualty tracking opportunity.
        /// </summary>
        private void OnPlayerBattleEnd(MapEvent mapEvent)
        {
            try
            {
                // Only process if we have a pending snapshot
                if (!_isInBattle || _preBattleCounts == null || _preBattleCounts.Count == 0)
                {
                    return;
                }

                // Reconcile if not already done by OnMapEventEnded
                ReconcileCasualties();
                _isInBattle = false;
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error in OnPlayerBattleEnd", ex);
                _isInBattle = false;
            }
        }

        /// <summary>
        /// Daily tick handler - syncs retinue tracking with actual roster to catch
        /// wounded soldiers who died from wounds between battles.
        /// </summary>
        private void OnDailyTick()
        {
            try
            {
                // Skip sync during active battles
                if (_isInBattle)
                {
                    return;
                }

                var manager = RetinueManager.Instance;
                if (manager?.State == null || !manager.State.HasRetinue)
                {
                    return;
                }

                // Perform a quiet sync - only log if corrections are needed
                SyncWithRosterQuiet();
            }
            catch (Exception ex)
            {
                ModLogger.Caught(LogCategory, "Error in daily sync", ex);
            }
        }

        /// <summary>
        /// Quietly syncs retinue tracking with roster, logging only when corrections occur.
        /// Used by daily tick to catch wounded soldiers who died from wounds.
        /// </summary>
        private void SyncWithRosterQuiet()
        {
            var manager = RetinueManager.Instance;
            if (manager?.State == null)
            {
                return;
            }

            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null)
            {
                return;
            }

            var state = manager.State;
            var totalLost = 0;
            var lossLog = new List<string>();

            foreach (var troopId in state.TroopCounts.Keys.ToList())
            {
                var character = CharacterObject.Find(troopId);
                if (character == null)
                {
                    continue;
                }

                var actualCount = roster.GetTroopCount(character);
                var trackedCount = state.GetTroopCount(troopId);

                // Only care about losses (wounded dying from wounds)
                if (actualCount < trackedCount)
                {
                    var lost = trackedCount - actualCount;
                    _ = state.UpdateTroopCount(troopId, -lost);
                    totalLost += lost;
                    lossLog.Add($"{lost}x {character.Name}");
                }
            }

            // Clean up any zero-count entries
            CleanupZeroCountEntries(state);

            // Log if we found soldiers who died from wounds
            if (totalLost > 0)
            {
                ModLogger.Info(LogCategory,
                    $"Wounded casualties: {totalLost} soldiers succumbed to wounds ({string.Join(", ", lossLog)}), " +
                    $"{state.TotalSoldiers} remaining");
            }
        }

        #endregion

        #region Snapshot and Reconciliation

        /// <summary>
        /// Takes a snapshot of the current retinue troop counts and named veterans before battle.
        /// </summary>
        private void TakePreBattleSnapshot(RetinueState state)
        {
            _preBattleCounts = new Dictionary<string, int>();
            _preBattleVeteranIds = new List<string>();

            if (state?.TroopCounts == null)
            {
                return;
            }

            foreach (var kvp in state.TroopCounts)
            {
                if (kvp.Value > 0)
                {
                    _preBattleCounts[kvp.Key] = kvp.Value;
                }
            }

            // Capture list of named veteran IDs for death detection after battle
            if (state.NamedVeterans != null)
            {
                foreach (var veteran in state.NamedVeterans)
                {
                    _preBattleVeteranIds.Add(veteran.Id);
                }
            }

            var veteranCount = _preBattleVeteranIds.Count;
            ModLogger.Debug(LogCategory,
                $"Pre-battle snapshot: {_preBattleCounts.Count} troop types, {_preBattleCounts.Values.Sum()} soldiers, {veteranCount} named veterans");
        }

        /// <summary>
        /// Reconciles the post-battle roster with our tracked counts.
        /// Native casualty handling already updated the roster; we sync our tracking state.
        /// </summary>
        private void ReconcileCasualties()
        {
            var manager = RetinueManager.Instance;
            if (manager?.State == null)
            {
                ClearSnapshot();
                return;
            }

            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null)
            {
                ClearSnapshot();
                return;
            }

            var state = manager.State;
            var totalCasualties = 0;
            var casualtyLog = new List<string>();

            // Compare each tracked troop type against actual roster counts
            foreach (var kvp in _preBattleCounts.ToList())
            {
                var troopId = kvp.Key;
                var preBattleCount = kvp.Value;

                var character = CharacterObject.Find(troopId);
                if (character == null)
                {
                    ModLogger.Warn(LogCategory, $"Could not find character for troop ID: {troopId}");
                    continue;
                }

                // Get the actual count from the roster (source of truth after battle)
                var actualRosterCount = roster.GetTroopCount(character);

                // Calculate losses based on pre-battle vs actual roster
                // Note: The roster is the source of truth after battle
                var casualties = preBattleCount - actualRosterCount;

                if (casualties > 0)
                {
                    // Update our tracking to match roster
                    _ = state.UpdateTroopCount(troopId, -casualties);
                    totalCasualties += casualties;
                    casualtyLog.Add($"{casualties}x {character.Name}");

                    ModLogger.Debug(LogCategory,
                        $"Casualty: {casualties}x {character.Name} (pre: {preBattleCount}, post: {actualRosterCount})");
                }
                else if (casualties < 0)
                {
                    // Somehow we gained troops? This shouldn't happen but log it
                    ModLogger.Warn(LogCategory,
                        $"Unexpected troop gain: {character.Name} pre={preBattleCount}, post={actualRosterCount}");
                }
            }

            // Clean up entries with zero troops
            CleanupZeroCountEntries(state);

            // Log summary and report to news system
            if (totalCasualties > 0)
            {
                ModLogger.Info(LogCategory,
                    $"Battle casualties: {totalCasualties} soldiers lost ({string.Join(", ", casualtyLog)}), " +
                    $"{state.TotalSoldiers} remaining");

                // Report casualties to news feed (wounded count estimated as ~30% of remaining)
                var woundedEstimate = Math.Min(state.TotalSoldiers / 3, totalCasualties / 2);
                EnlistedNewsBehavior.Instance?.AddRetinueCasualtyReport(totalCasualties, woundedEstimate, "the battle");
            }
            else
            {
                ModLogger.Debug(LogCategory, $"Battle ended with no retinue casualties, {state.TotalSoldiers} remaining");
            }

            // Increment battle counter for veteran emergence tracking
            state.BattlesParticipated++;
            ModLogger.Debug(LogCategory, $"Retinue battles participated: {state.BattlesParticipated}");

            // Process named veteran survival and check for deaths
            ProcessVeteranSurvivalAndDeaths(state, totalCasualties);

            // Check for new veteran emergence after battle
            if (state.TotalSoldiers > 0)
            {
                CheckForVeteranEmergence(state, totalCasualties);
            }

            ClearSnapshot();
        }

        /// <summary>
        /// Removes entries from TroopCounts that have zero soldiers.
        /// </summary>
        private static void CleanupZeroCountEntries(RetinueState state)
        {
            if (state?.TroopCounts == null)
            {
                return;
            }

            var toRemove = state.TroopCounts
                .Where(kvp => kvp.Value <= 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _ = state.TroopCounts.Remove(key);
            }

            if (toRemove.Count > 0)
            {
                ModLogger.Debug(LogCategory, $"Cleaned up {toRemove.Count} empty troop entries");
            }
        }

        /// <summary>
        /// Clears the pre-battle snapshot.
        /// </summary>
        private void ClearSnapshot()
        {
            _preBattleCounts?.Clear();
            _preBattleVeteranIds?.Clear();
        }

        #endregion

        #region Named Veteran Processing

        /// <summary>
        /// Processes veteran survival after battle. Marks surviving veterans as having survived,
        /// and handles veteran deaths if casualties occurred.
        /// </summary>
        private void ProcessVeteranSurvivalAndDeaths(RetinueState state, int totalCasualties)
        {
            const string veteranCategory = "Veterans";

            if (state.NamedVeterans == null || state.NamedVeterans.Count == 0)
            {
                return;
            }

            // If there were casualties, some veterans might have died
            // We use a proportional chance based on casualties vs total soldiers
            if (totalCasualties > 0)
            {
                var preBattleTotal = _preBattleCounts?.Values.Sum() ?? state.TotalSoldiers;
                if (preBattleTotal <= 0)
                {
                    preBattleTotal = 1;
                }

                // Death chance for each veteran is proportional to casualty rate
                var casualtyRate = (float)totalCasualties / preBattleTotal;

                // Process veterans in reverse order so we can safely remove dead ones
                for (var i = state.NamedVeterans.Count - 1; i >= 0; i--)
                {
                    var veteran = state.NamedVeterans[i];

                    // Skip wounded veterans - they weren't in the battle
                    if (veteran.IsWounded)
                    {
                        continue;
                    }

                    // Roll for death based on casualty rate (veterans are slightly luckier)
                    var deathRoll = MBRandom.RandomFloat;
                    var veteranDeathChance = casualtyRate * 0.7f; // 30% less likely to die than average soldier

                    if (deathRoll < veteranDeathChance)
                    {
                        // Veteran has died
                        ModLogger.Info(veteranCategory,
                            $"Named veteran {veteran.Name} the {veteran.Trait} has fallen in battle! " +
                            $"(Survived {veteran.BattlesSurvived} battles, {veteran.Kills} kills)");

                        state.NamedVeterans.RemoveAt(i);

                        // Trigger memorial event
                        TriggerVeteranMemorialEvent(veteran);
                    }
                    else
                    {
                        // Veteran survived - record the battle
                        veteran.RecordBattleSurvival();
                        ModLogger.Debug(veteranCategory,
                            $"Veteran {veteran.Name} survived battle #{veteran.BattlesSurvived}");
                    }
                }
            }
            else
            {
                // No casualties - all veterans survive
                state.RecordBattleSurvivalForAllVeterans();
                ModLogger.Debug(veteranCategory,
                    $"All {state.NamedVeterans.Count} veterans survived the battle");
            }
        }

        /// <summary>
        /// Checks if a new named veteran should emerge from the anonymous ranks.
        /// Requires: 3+ battles participated, retinue has soldiers, not at max veterans.
        /// 15% chance per eligible battle.
        /// </summary>
        private void CheckForVeteranEmergence(RetinueState state, int totalCasualties)
        {
            const string veteranCategory = "Veterans";

            // Must have participated in enough battles
            if (state.BattlesParticipated < MinBattlesForEmergence)
            {
                ModLogger.Debug(veteranCategory,
                    $"Not eligible for emergence: only {state.BattlesParticipated} battles (need {MinBattlesForEmergence})");
                return;
            }

            // Must have room for another veteran
            if (!state.CanAddNamedVeteran())
            {
                ModLogger.Debug(veteranCategory,
                    $"Not eligible for emergence: already at max veterans ({RetinueState.MaxNamedVeterans})");
                return;
            }

            // Must have soldiers in the retinue
            if (state.TotalSoldiers <= 0)
            {
                return;
            }

            // Don't create veterans when everyone died
            if (totalCasualties > 0 && state.TotalSoldiers <= 0)
            {
                return;
            }

            // Roll for emergence
            var roll = MBRandom.RandomFloat;
            if (roll >= VeteranEmergenceChance)
            {
                ModLogger.Debug(veteranCategory,
                    $"Veteran emergence roll failed: {roll:F2} >= {VeteranEmergenceChance:F2}");
                return;
            }

            // Create the new veteran
            var newVeteran = CreateNamedVeteran();
            if (newVeteran == null)
            {
                ModLogger.Warn(veteranCategory, "Failed to create named veteran - name generation failed");
                return;
            }

            if (state.AddNamedVeteran(newVeteran))
            {
                ModLogger.Info(veteranCategory,
                    $"New veteran emerged: {newVeteran.Name} the {newVeteran.Trait} " +
                    $"(Total veterans: {state.NamedVeterans.Count})");

                // Show notification to player
                ShowVeteranEmergenceNotification(newVeteran);
            }
        }

        /// <summary>
        /// Creates a new named veteran with a culture-appropriate name and random trait.
        /// </summary>
        private NamedVeteran CreateNamedVeteran()
        {
            const string veteranCategory = "Veterans";

            // Get the player's current lord's culture for name generation
            var enlistment = EnlistmentBehavior.Instance;
            var culture = enlistment?.CurrentLord?.Culture;

            if (culture == null)
            {
                ModLogger.Warn(veteranCategory, "Cannot create veteran: no culture available");
                return null;
            }

            // Generate a name from the culture's name list
            string veteranName;
            try
            {
                // Get name list for male soldiers (most common)
                var nameList = NameGenerator.Current.GetNameListForCulture(culture, false);
                if (nameList == null || nameList.IsEmpty())
                {
                    ModLogger.Warn(veteranCategory, $"No names available for culture {culture.StringId}");
                    return null;
                }

                // Pick a random name
                var nameText = nameList.GetRandomElement();
                veteranName = nameText?.ToString() ?? "Soldier";
            }
            catch (Exception ex)
            {
                ModLogger.Caught("VETERANS", "Error generating veteran name", ex);
                return null;
            }

            // Pick a random trait
            var trait = VeteranTraits.GetRandomElement();

            // Get current campaign time for emergence timestamp
            var currentTime = (float)CampaignTime.Now.ToDays;

            var veteran = new NamedVeteran(veteranName, trait, currentTime);

            ModLogger.Debug(veteranCategory, $"Created veteran: {veteran}");

            return veteran;
        }

        /// <summary>
        /// Shows a notification to the player when a new veteran emerges.
        /// </summary>
        private static void ShowVeteranEmergenceNotification(NamedVeteran veteran)
        {
            var message = new TextObject("{=enl_vet_emergence_msg}One of your soldiers has distinguished themselves in battle. {VETERAN_NAME} the {TRAIT} has earned a name among your retinue.");
            _ = message.SetTextVariable("VETERAN_NAME", veteran.Name);
            _ = message.SetTextVariable("TRAIT", veteran.Trait);

            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Cyan));

            // Record veteran emergence in news feed for Personal Feed display
            EnlistedNewsBehavior.Instance?.AddVeteranEmergence(veteran.Name, veteran.Trait);
        }

        /// <summary>
        /// Triggers a memorial notification when a named veteran dies in battle.
        /// Shows an immediate notification with the veteran's legacy.
        /// </summary>
        private static void TriggerVeteranMemorialEvent(NamedVeteran veteran)
        {
            const string veteranCategory = "Veterans";

            // Show notification about the fallen veteran
            var message = new TextObject("{=enl_vet_fallen_msg}{VETERAN_NAME} the {TRAIT} has fallen in battle. They survived {BATTLES} battles and claimed {KILLS} enemy lives.");
            _ = message.SetTextVariable("VETERAN_NAME", veteran.Name);
            _ = message.SetTextVariable("TRAIT", veteran.Trait);
            _ = message.SetTextVariable("BATTLES", veteran.BattlesSurvived);
            _ = message.SetTextVariable("KILLS", veteran.Kills);

            InformationManager.DisplayMessage(new InformationMessage(message.ToString(), Colors.Red));

            ModLogger.Info(veteranCategory,
                $"Displayed memorial notification for fallen veteran {veteran.Name} the {veteran.Trait}");

            // Record veteran death in news feed for Personal Feed display
            EnlistedNewsBehavior.Instance?.AddVeteranDeath(veteran.Name, veteran.BattlesSurvived, veteran.Kills);

            // Memorial as feed entry: flavor, not crisis. Route through Director as
            // Pertinent (no InteractiveEvent) so status/Camp news surfaces pick it up.
            var evt = EventCatalog.GetEvent("evt_ret_veteran_memorial");
            if (evt == null)
            {
                return;
            }

            var director = StoryDirector.Instance;
            if (director != null)
            {
                string storyKey = "memorial:" + (veteran.Id ?? veteran.Name ?? "unknown"); // Id is the stable per-veteran GUID; Name is fallback
                director.EmitCandidate(new StoryCandidate
                {
                    SourceId = "retinue.memorial." + (veteran.Name ?? "unknown"),
                    CategoryId = "retinue.memorial",
                    ProposedTier = StoryTier.Pertinent,
                    SeverityHint = 0.20f,
                    Beats = { StoryBeat.PlayerBattleEnd },
                    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                    EmittedAt = CampaignTime.Now,
                    RenderedTitle = "Veteran fallen: " + veteran.Name,
                    RenderedBody = evt.SetupFallback,
                    StoryKey = storyKey,
                    DispatchCategory = "retinue",
                    SeverityLevel = 1
                });
                ModLogger.Info(veteranCategory, $"Routed memorial to accordion for {veteran.Name}");
            }
            else
            {
                // Director unavailable — fall back to the original modal queue so memorials
                // don't silently disappear during early boot.
                EventDeliveryManager.Instance?.QueueEvent(evt);
                ModLogger.Info(veteranCategory, $"Queued memorial event (Director unavailable) for {veteran.Name}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Checks if the player is involved in a map event.
        /// </summary>
        private static bool IsPlayerInvolvedInMapEvent(MapEvent mapEvent)
        {
            if (mapEvent == null)
            {
                return false;
            }

            // Check if player's party is directly involved
            var mainParty = PartyBase.MainParty;
            if (mainParty == null)
            {
                return false;
            }

            // Check if main party is in the event
            if (mapEvent.IsPlayerMapEvent)
            {
                return true;
            }

            // Check if player's enlisted lord's party is in the event
            var enlistment = EnlistmentBehavior.Instance;
            if (enlistment is { IsEnlisted: true, CurrentLord.PartyBelongedTo: not null })
            {
                var lordParty = enlistment.CurrentLord.PartyBelongedTo.Party;

                // Check attacker side
                if (mapEvent.AttackerSide?.Parties != null)
                {
                    foreach (var eventParty in mapEvent.AttackerSide.Parties)
                    {
                        if (eventParty?.Party == lordParty || eventParty?.Party == mainParty)
                        {
                            return true;
                        }
                    }
                }

                // Check defender side
                if (mapEvent.DefenderSide?.Parties != null)
                {
                    foreach (var eventParty in mapEvent.DefenderSide.Parties)
                    {
                        if (eventParty?.Party == lordParty || eventParty?.Party == mainParty)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the total casualties from the last battle (for UI display).
        /// </summary>
        public int GetLastBattleCasualties()
        {
            // This would require storing the last battle's casualties
            // For now, return 0 as the casualties are logged but not stored
            return 0;
        }

        /// <summary>
        /// Force-syncs the retinue state with the actual roster.
        /// Use this if tracking gets out of sync for any reason.
        /// </summary>
        public void ForceSyncWithRoster()
        {
            var manager = RetinueManager.Instance;
            if (manager?.State == null)
            {
                return;
            }

            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null)
            {
                return;
            }

            var state = manager.State;
            var corrections = 0;

            foreach (var troopId in state.TroopCounts.Keys.ToList())
            {
                var character = CharacterObject.Find(troopId);
                if (character == null)
                {
                    continue;
                }

                var actualCount = roster.GetTroopCount(character);
                var trackedCount = state.GetTroopCount(troopId);

                if (actualCount != trackedCount)
                {
                    var delta = actualCount - trackedCount;
                    _ = state.UpdateTroopCount(troopId, delta);
                    corrections++;

                    ModLogger.Debug(LogCategory,
                        $"Sync correction: {character.Name} tracked={trackedCount}, actual={actualCount}");
                }
            }

            if (corrections > 0)
            {
                ModLogger.Info(LogCategory, $"Force sync completed with {corrections} corrections");
            }
        }

        #endregion
    }
}

