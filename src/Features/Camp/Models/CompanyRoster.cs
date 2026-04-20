using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace Enlisted.Features.Camp.Models
{
    /// <summary>
    /// Wrapper around the party's TroopRoster that adds mod-specific overlay tracking
    /// for sick, missing, and dead soldiers.
    /// </summary>
    public class CompanyRoster
    {
        private const string LogCategory = "Simulation";

        private readonly MobileParty _party;

        // Track missing soldiers (not yet confirmed deserters) with their days missing
        private readonly Dictionary<string, int> _missingSoldiers = new Dictionary<string, int>();

        // Overlay tracking (saved separately in behavior)
        public int SickCount { get; set; }
        public int MissingCount => _missingSoldiers.Count;
        public int DeadThisCampaign { get; set; }

        // Read from game (live)
        public int TotalSoldiers => _party?.MemberRoster?.TotalManCount ?? 0;
        public int TotalRegulars => TotalSoldiers - HeroCount;
        public int HeroCount => _party?.MemberRoster?.TotalHeroes ?? 0;
        public int WoundedCount => _party?.MemberRoster?.TotalWounded ?? 0;
        public int FitForDuty => Math.Max(0, TotalSoldiers - WoundedCount - SickCount);

        // Calculated rates
        public float CasualtyRate => TotalSoldiers > 0 ? (float)(WoundedCount + SickCount) / TotalSoldiers : 0f;
        public float SicknessRate => TotalSoldiers > 0 ? (float)SickCount / TotalSoldiers : 0f;

        public CompanyRoster(MobileParty party)
        {
            _party = party ?? throw new ArgumentNullException(nameof(party));
        }

        /// <summary>
        /// Wounds random healthy regular troops using the game API.
        /// They will heal via the native Medicine system. Never wounds heroes.
        /// </summary>
        public void WoundRandomTroop(int count = 1)
        {
            if (_party?.MemberRoster == null || count <= 0)
            {
                return;
            }

            var roster = _party.MemberRoster;
            for (int i = 0; i < count; i++)
            {
                var troop = GetRandomHealthyRegularTroop(roster);
                if (troop != null)
                {
                    _ = roster.AddToCounts(troop, count: 0, insertAtFront: false, woundedCount: 1);
                    ModLogger.Debug(LogCategory, $"Wounded a regular troop: {troop.Name}");
                }
                else
                {
                    ModLogger.Debug(LogCategory, "No healthy regular troop available to wound");
                    break;
                }
            }
        }

        /// <summary>
        /// Removes a random regular troop for desertion. ACTUALLY REMOVES FROM PARTY.
        /// Never removes heroes.
        /// </summary>
        public void RemoveDeserter(int count = 1)
        {
            if (_party?.MemberRoster == null || count <= 0)
            {
                return;
            }

            var roster = _party.MemberRoster;
            for (int i = 0; i < count; i++)
            {
                var troop = GetRandomNonHeroTroop(roster);
                if (troop != null)
                {
                    _ = roster.AddToCounts(troop, count: -1);
                    ModLogger.Info(LogCategory, $"A regular troop deserted: {troop.Name}");
                }
                else
                {
                    ModLogger.Warn(LogCategory, "No regular troops available to remove for desertion");
                    break;
                }
            }
        }

        /// <summary>
        /// Kills a sick regular soldier. ACTUALLY REMOVES FROM PARTY.
        /// Never removes heroes.
        /// </summary>
        public void KillSickSoldier()
        {
            if (SickCount <= 0 || _party?.MemberRoster == null)
            {
                return;
            }

            SickCount = Math.Max(0, SickCount - 1);
            DeadThisCampaign++;

            var roster = _party.MemberRoster;
            var troop = GetRandomNonHeroTroop(roster);
            if (troop != null)
            {
                _ = roster.AddToCounts(troop, count: -1);
                ModLogger.Info(LogCategory, $"A sick soldier died: {troop.Name}");
            }
            else
            {
                ModLogger.Warn(LogCategory, "No regular troops available to remove for sickness death");
            }
        }

        /// <summary>
        /// Adds a soldier to the missing list. They will be confirmed deserted after 3 days.
        /// </summary>
        public void AddMissing()
        {
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            _missingSoldiers[id] = 0;
            ModLogger.Debug(LogCategory, $"Soldier marked missing: {id}");
        }

        /// <summary>
        /// Process daily tick for missing soldiers. Returns IDs of those confirmed deserted.
        /// </summary>
        public List<string> ProcessMissingDays()
        {
            var deserted = new List<string>();

            foreach (var key in _missingSoldiers.Keys.ToList())
            {
                _missingSoldiers[key]++;
                if (_missingSoldiers[key] >= 3)
                {
                    deserted.Add(key);
                }
            }

            return deserted;
        }

        /// <summary>
        /// Resolve a missing soldier - either returned or confirmed deserted.
        /// </summary>
        public void ResolveMissing(string id, bool returned)
        {
            if (!_missingSoldiers.ContainsKey(id))
            {
                return;
            }

            _ = _missingSoldiers.Remove(id);

            if (!returned)
            {
                RemoveDeserter(1);
            }
            else
            {
                ModLogger.Debug(LogCategory, $"Missing soldier returned: {id}");
            }
        }

        /// <summary>
        /// Gets a random healthy regular troop for wounding.
        /// Excludes heroes and already wounded troops.
        /// </summary>
        private CharacterObject GetRandomHealthyRegularTroop(TroopRoster roster)
        {
            var healthyRegulars = roster.GetTroopRoster()
                .Where(e => !e.Character.IsHero && e.Number > e.WoundedNumber)
                .ToList();

            if (healthyRegulars.Count == 0)
            {
                return null;
            }

            int totalHealthyCount = healthyRegulars.Sum(e => e.Number - e.WoundedNumber);
            if (totalHealthyCount <= 0)
            {
                return null;
            }

            int roll = MBRandom.RandomInt(totalHealthyCount);
            int cumulative = 0;

            foreach (var element in healthyRegulars)
            {
                cumulative += element.Number - element.WoundedNumber;
                if (roll < cumulative)
                {
                    return element.Character;
                }
            }

            return healthyRegulars[0].Character;
        }

        /// <summary>
        /// Gets a random non-hero troop for removal (desertion or death).
        /// Never removes heroes or the lord.
        /// </summary>
        private CharacterObject GetRandomNonHeroTroop(TroopRoster roster)
        {
            var regulars = roster.GetTroopRoster()
                .Where(e => !e.Character.IsHero && e.Number > 0)
                .ToList();

            if (regulars.Count == 0)
            {
                ModLogger.Warn(LogCategory, "No regular troops to remove - only heroes remain or roster is empty");
                return null;
            }

            int totalCount = regulars.Sum(e => e.Number);
            if (totalCount <= 0)
            {
                return null;
            }

            int roll = MBRandom.RandomInt(totalCount);
            int cumulative = 0;

            foreach (var element in regulars)
            {
                cumulative += element.Number;
                if (roll < cumulative)
                {
                    return element.Character;
                }
            }

            return regulars[0].Character;
        }
    }
}
