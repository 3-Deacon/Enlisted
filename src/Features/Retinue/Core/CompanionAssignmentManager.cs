using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Retinue.Core
{
    /// <summary>
    /// Manages companion battle participation settings ("Fight" vs "Stay Back")
    /// and endeavor-assignment settings (locked to a player-driven endeavor for
    /// the duration of its run, per Plan 5 of the CK3 wanderer cluster).
    /// Companions marked "stay back" don't spawn in battle; companions assigned
    /// to an endeavor are unavailable for other endeavors until the active one
    /// completes.
    /// </summary>
    public sealed class CompanionAssignmentManager : CampaignBehaviorBase
    {
        private const string LogCategory = "CompanionAssignment";

        // Track battle participation per companion
        // Key: Hero.StringId, Value: true = fight (default), false = stay back
        // Synced via SyncData, not SaveableField
        private Dictionary<string, bool> _companionBattleParticipation;

        // Track endeavor assignment per companion (Plan 5 substrate)
        // Key: Hero.StringId, Value: true = locked to active endeavor, false/missing = available
        private Dictionary<string, bool> _companionEndeavorAssignment;

        public static CompanionAssignmentManager Instance { get; private set; }

        public CompanionAssignmentManager()
        {
            _companionBattleParticipation = new Dictionary<string, bool>();
            _companionEndeavorAssignment = new Dictionary<string, bool>();
            Instance = this;
        }

        public override void RegisterEvents()
        {
            // No events needed - state is managed via UI and checked during battle spawn / endeavor selection
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                _ = dataStore.SyncData("_companionBattleParticipation", ref _companionBattleParticipation);
                _companionBattleParticipation ??= new Dictionary<string, bool>();

                _ = dataStore.SyncData("_companionEndeavorAssignment", ref _companionEndeavorAssignment);
                _companionEndeavorAssignment ??= new Dictionary<string, bool>();

                ModLogger.Debug(LogCategory, $"SyncData: {_companionBattleParticipation.Count} battle assignments, {_companionEndeavorAssignment.Count} endeavor assignments loaded");
            });
        }

        /// <summary>
        /// Checks if a companion should spawn and fight in battle.
        /// Returns true by default if no setting exists.
        /// </summary>
        public bool ShouldCompanionFight(Hero companion)
        {
            if (companion == null)
            {
                return true;
            }

            var hasEntry = _companionBattleParticipation.TryGetValue(companion.StringId, out var fights);
            var result = !hasEntry || fights;

            ModLogger.Trace(LogCategory,
                $"ShouldFight check: {companion.Name} -> {(result ? "Fight" : "Stay Back")} (hasEntry={hasEntry})");

            return result;
        }

        /// <summary>
        /// Sets whether a companion should fight in battle or stay back.
        /// </summary>
        public void SetCompanionParticipation(Hero companion, bool shouldFight)
        {
            if (companion == null)
            {
                return;
            }

            _companionBattleParticipation[companion.StringId] = shouldFight;

            ModLogger.Debug(LogCategory,
                $"Set {companion.Name} participation to {(shouldFight ? "Fight" : "Stay Back")}");
        }

        /// <summary>
        /// Toggles a companion's battle participation setting.
        /// </summary>
        public void ToggleCompanionParticipation(Hero companion)
        {
            if (companion == null)
            {
                return;
            }

            var currentSetting = ShouldCompanionFight(companion);
            SetCompanionParticipation(companion, !currentSetting);
        }

        /// <summary>
        /// Gets all player companions currently in the player's party.
        /// Available from T1 (enlistment start) - no tier restriction.
        /// Returns empty list if player is not enlisted.
        /// </summary>
        public List<Hero> GetAssignableCompanions()
        {
            var result = new List<Hero>();

            var enlistment = EnlistmentBehavior.Instance;
            // V2.0: Companions manageable from T1 (no tier gate)
            if (enlistment?.IsEnlisted != true)
            {
                return result;
            }

            var mainParty = MobileParty.MainParty;
            if (mainParty?.MemberRoster == null)
            {
                return result;
            }

            // Get companions from player's party
            foreach (var element in mainParty.MemberRoster.GetTroopRoster())
            {
                if (element.Character is { IsHero: true, HeroObject: { IsPlayerCompanion: true, IsAlive: true, IsHumanPlayerCharacter: false } hero })
                {
                    result.Add(hero);
                }
            }

            return result.OrderBy(h => h.Name.ToString()).ToList();
        }

        /// <summary>
        /// Gets count of companions set to fight.
        /// </summary>
        public int GetFightingCompanionCount()
        {
            return GetAssignableCompanions().Count(ShouldCompanionFight);
        }

        /// <summary>
        /// Gets count of companions set to stay back.
        /// </summary>
        public int GetStayBackCompanionCount()
        {
            return GetAssignableCompanions().Count(c => !ShouldCompanionFight(c));
        }

        /// <summary>
        /// Returns true if the companion is currently locked to an active endeavor
        /// (Plan 5). Default false (available) when no entry exists.
        /// </summary>
        public bool IsAssignedToEndeavor(Hero companion)
        {
            if (companion == null)
            {
                return false;
            }
            return _companionEndeavorAssignment.TryGetValue(companion.StringId, out var assigned) && assigned;
        }

        /// <summary>
        /// Sets whether a companion is locked to an active endeavor. Plan 5 calls
        /// this on endeavor start (true) and endeavor end (false).
        /// </summary>
        public void SetAssignedToEndeavor(Hero companion, bool assigned)
        {
            if (companion == null)
            {
                return;
            }
            _companionEndeavorAssignment[companion.StringId] = assigned;
            ModLogger.Debug(LogCategory,
                $"Set {companion.Name} endeavor assignment to {(assigned ? "Locked" : "Available")}");
        }

        /// <summary>
        /// Clears all companion participation settings (battle + endeavor).
        /// Called on full retirement to start fresh next enlistment.
        /// </summary>
        public void ClearAllSettings()
        {
            var battleCount = _companionBattleParticipation.Count;
            var endeavorCount = _companionEndeavorAssignment.Count;
            _companionBattleParticipation.Clear();
            _companionEndeavorAssignment.Clear();
            ModLogger.Info(LogCategory, $"Cleared {battleCount} battle and {endeavorCount} endeavor assignments");
        }
    }
}
