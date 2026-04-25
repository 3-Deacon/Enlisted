using System.Collections.Generic;
using Enlisted.Features.Content.Models;
using Enlisted.Mod.Core.Logging;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Tracks player behavior and choices to learn content preferences.
    /// Used by the Content Orchestrator to personalize event and opportunity selection.
    /// </summary>
    public static class PlayerBehaviorTracker
    {
        private const string LogCategory = "BehaviorTracker";

        // Behavior counts by tag (e.g., "combat", "social", "risky", "safe", "loyal", "selfish")
        private static Dictionary<string, int> _behaviorCounts = new Dictionary<string, int>();

        // Content engagement tracking (event ID -> times engaged/dismissed)
        private static Dictionary<string, int> _contentEngagement = new Dictionary<string, int>();

        // Opportunity type engagement tracking for learning system
        private static Dictionary<string, int> _opportunityTypePresented = new Dictionary<string, int>();
        private static Dictionary<string, int> _opportunityTypeEngaged = new Dictionary<string, int>();

        // Cached preferences (recalculated when choices change)
        private static PlayerPreferences _cachedPreferences;
        private static bool _preferencesDirty = true;

        /// <summary>
        /// Records a player choice with the given tag.
        /// Tags should be consistent: "combat", "social", "risky", "safe", "loyal", "selfish".
        /// </summary>
        public static void RecordChoice(string choiceTag)
        {
            if (string.IsNullOrEmpty(choiceTag))
            {
                return;
            }

            var tag = choiceTag.ToLowerInvariant();
            if (!_behaviorCounts.ContainsKey(tag))
            {
                _behaviorCounts[tag] = 0;
            }

            _behaviorCounts[tag]++;
            _preferencesDirty = true;

            ModLogger.Debug(LogCategory, $"Recorded choice: {tag} (total: {_behaviorCounts[tag]})");
        }

        /// <summary>
        /// Records that content was delivered to the player.
        /// Used for tracking engagement patterns.
        /// </summary>
        public static void RecordContentDelivered(string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
            {
                return;
            }

            if (!_contentEngagement.ContainsKey(contentId))
            {
                _contentEngagement[contentId] = 0;
            }

            _contentEngagement[contentId]++;
            ModLogger.Debug(LogCategory, $"Content delivered: {contentId}");
        }

        /// <summary>
        /// Records that an opportunity type was presented to the player.
        /// Called when opportunities are shown in the camp menu.
        /// </summary>
        public static void RecordOpportunityPresented(string opportunityType)
        {
            if (string.IsNullOrEmpty(opportunityType))
            {
                return;
            }

            var type = opportunityType.ToLowerInvariant();
            if (!_opportunityTypePresented.ContainsKey(type))
            {
                _opportunityTypePresented[type] = 0;
            }

            _opportunityTypePresented[type]++;
            _preferencesDirty = true;
        }

        /// <summary>
        /// Records that the player engaged with an opportunity type.
        /// Called when player selects an opportunity from the menu.
        /// </summary>
        public static void RecordOpportunityEngagement(string opportunityType, bool engaged)
        {
            if (string.IsNullOrEmpty(opportunityType))
            {
                return;
            }

            var type = opportunityType.ToLowerInvariant();

            if (engaged)
            {
                if (!_opportunityTypeEngaged.ContainsKey(type))
                {
                    _opportunityTypeEngaged[type] = 0;
                }

                _opportunityTypeEngaged[type]++;
                ModLogger.Debug(LogCategory, $"Opportunity engaged: {type} (total: {_opportunityTypeEngaged[type]})");
            }

            _preferencesDirty = true;
        }

        /// <summary>
        /// Gets the engagement rate for a specific opportunity type.
        /// Returns a value between 0 (never engages) and 1 (always engages).
        /// Returns 0.5 if insufficient data.
        /// </summary>
        public static float GetOpportunityEngagementRate(string opportunityType)
        {
            if (string.IsNullOrEmpty(opportunityType))
            {
                return 0.5f;
            }

            var type = opportunityType.ToLowerInvariant();

            int presented = _opportunityTypePresented.TryGetValue(type, out var p) ? p : 0;
            int engaged = _opportunityTypeEngaged.TryGetValue(type, out var e) ? e : 0;

            // Need minimum of 3 presentations to have meaningful data
            if (presented < 3)
            {
                return 0.5f;
            }

            return (float)engaged / presented;
        }

        /// <summary>
        /// Calculates a fitness modifier based on learned player preferences.
        /// Positive values for types the player likes, negative for ignored types.
        /// Implements the 70/30 split: 70% learned preference, 30% variety.
        /// </summary>
        public static float GetLearningModifier(string opportunityType)
        {
            if (string.IsNullOrEmpty(opportunityType))
            {
                return 0f;
            }

            var type = opportunityType.ToLowerInvariant();

            int presented = _opportunityTypePresented.TryGetValue(type, out var p) ? p : 0;

            // Need minimum data to apply learning
            if (presented < 5)
            {
                return 0f;
            }

            float engagementRate = GetOpportunityEngagementRate(type);

            // 70/30 split: only apply 70% of the full modifier
            const float learningWeight = 0.7f;

            // High engagement (>60%) = bonus, low engagement (<30%) = penalty
            if (engagementRate > 0.6f)
            {
                // +15 at full learning, scaled by learning weight
                return 15f * learningWeight;
            }

            if (engagementRate < 0.3f)
            {
                // -10 at full learning, scaled by learning weight
                return -10f * learningWeight;
            }

            // Middle ground: no modifier
            return 0f;
        }

        /// <summary>
        /// Gets opportunity type tracking data for saving.
        /// </summary>
        public static Dictionary<string, int> GetOpportunityPresentedForSave()
        {
            return new Dictionary<string, int>(_opportunityTypePresented);
        }

        /// <summary>
        /// Gets opportunity engagement data for saving.
        /// </summary>
        public static Dictionary<string, int> GetOpportunityEngagedForSave()
        {
            return new Dictionary<string, int>(_opportunityTypeEngaged);
        }

        /// <summary>
        /// Loads opportunity tracking data from saved state.
        /// </summary>
        public static void LoadOpportunityState(Dictionary<string, int> presented, Dictionary<string, int> engaged)
        {
            _opportunityTypePresented = presented ?? new Dictionary<string, int>();
            _opportunityTypeEngaged = engaged ?? new Dictionary<string, int>();
            _preferencesDirty = true;
            ModLogger.Debug(LogCategory, $"Loaded opportunity state: {_opportunityTypePresented.Count} types tracked");
        }

        /// <summary>
        /// Gets the current player preferences based on recorded choices.
        /// </summary>
        public static PlayerPreferences GetPreferences()
        {
            if (_preferencesDirty || _cachedPreferences == null)
            {
                _cachedPreferences = CalculatePreferences();
                _preferencesDirty = false;
            }

            return _cachedPreferences;
        }

        /// <summary>
        /// Resets all tracked behavior data.
        /// Called when starting a new game or for testing.
        /// </summary>
        public static void Reset()
        {
            _behaviorCounts.Clear();
            _contentEngagement.Clear();
            _opportunityTypePresented.Clear();
            _opportunityTypeEngaged.Clear();
            _cachedPreferences = null;
            _preferencesDirty = true;
            ModLogger.Info(LogCategory, "Behavior tracking reset");
        }

        /// <summary>
        /// Loads behavior data from saved state.
        /// </summary>
        public static void LoadState(Dictionary<string, int> behaviorCounts, Dictionary<string, int> contentEngagement)
        {
            _behaviorCounts = behaviorCounts ?? new Dictionary<string, int>();
            _contentEngagement = contentEngagement ?? new Dictionary<string, int>();
            _preferencesDirty = true;
            ModLogger.Debug(LogCategory, $"Loaded state: {_behaviorCounts.Count} behavior tags, {_contentEngagement.Count} content entries");
        }

        /// <summary>
        /// Gets behavior counts for saving.
        /// </summary>
        public static Dictionary<string, int> GetBehaviorCountsForSave()
        {
            return new Dictionary<string, int>(_behaviorCounts);
        }

        /// <summary>
        /// Gets content engagement for saving.
        /// </summary>
        public static Dictionary<string, int> GetContentEngagementForSave()
        {
            return new Dictionary<string, int>(_contentEngagement);
        }

        private static PlayerPreferences CalculatePreferences()
        {
            var prefs = new PlayerPreferences();

            // Count choices by category
            int combatChoices = GetCount("combat");
            int socialChoices = GetCount("social");
            int riskyChoices = GetCount("risky");
            int safeChoices = GetCount("safe");
            int loyalChoices = GetCount("loyal");
            int selfishChoices = GetCount("selfish");

            // Calculate combat vs social preference (0 = social, 1 = combat, 0.5 = balanced)
            int combatSocialTotal = combatChoices + socialChoices;
            prefs.CombatVsSocial = combatSocialTotal > 0
                ? (float)combatChoices / combatSocialTotal
                : 0.5f;

            // Calculate risky vs safe preference (0 = safe, 1 = risky)
            int riskTotal = riskyChoices + safeChoices;
            prefs.RiskyVsSafe = riskTotal > 0
                ? (float)riskyChoices / riskTotal
                : 0.5f;

            // Calculate loyal vs self-serving preference (0 = selfish, 1 = loyal)
            int loyaltyTotal = loyalChoices + selfishChoices;
            prefs.LoyalVsSelfServing = loyaltyTotal > 0
                ? (float)loyalChoices / loyaltyTotal
                : 0.5f;

            // Store raw counts for debugging
            prefs.TotalChoicesMade = combatSocialTotal + riskTotal + loyaltyTotal;
            prefs.CombatChoices = combatChoices;
            prefs.SocialChoices = socialChoices;
            prefs.RiskyChoices = riskyChoices;
            prefs.SafeChoices = safeChoices;

            return prefs;
        }

        private static int GetCount(string tag)
        {
            return _behaviorCounts.TryGetValue(tag, out var count) ? count : 0;
        }
    }
}
