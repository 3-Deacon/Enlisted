using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace Enlisted.Features.Retinue.Core
{
    /// <summary>
    /// Manages companion battle participation settings ("Fight" vs "Stay Back").
    /// Companions marked "stay back" don't spawn in battle, making them immune to all battle outcomes.
    /// </summary>
    public sealed class CompanionAssignmentManager : CampaignBehaviorBase
    {
        private const string LogCategory = "CompanionAssignment";

        // Track battle participation per companion
        // Key: Hero.StringId, Value: true = fight (default), false = stay back
        // Synced via SyncData, not SaveableField
        private Dictionary<string, bool> _companionBattleParticipation;

        public static CompanionAssignmentManager Instance { get; private set; }

        public CompanionAssignmentManager()
        {
            _companionBattleParticipation = new Dictionary<string, bool>();
            Instance = this;
        }

        public override void RegisterEvents()
        {
            // No events needed - state is managed via UI and checked during battle spawn
        }

        public override void SyncData(IDataStore dataStore)
        {
            SaveLoadDiagnostics.SafeSyncData(this, dataStore, () =>
            {
                _ = dataStore.SyncData("_companionBattleParticipation", ref _companionBattleParticipation);
                _companionBattleParticipation ??= new Dictionary<string, bool>();

                ModLogger.Debug(LogCategory, $"SyncData: {_companionBattleParticipation.Count} companion assignments loaded");
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
        /// Clears all companion participation settings.
        /// Called on full retirement to start fresh next enlistment.
        /// </summary>
        public void ClearAllSettings()
        {
            var count = _companionBattleParticipation.Count;
            _companionBattleParticipation.Clear();
            ModLogger.Info(LogCategory, $"Cleared {count} companion participation settings");
        }
    }
}
