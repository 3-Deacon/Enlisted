using System;
using System.Collections.Generic;

namespace Enlisted.Features.Lifestyles
{
    /// <summary>
    /// Set of feature ids the player has unlocked through lifestyle progression
    /// (Forager / Drillmaster / Confidant / etc., per Plan 7). Stored as List
    /// because HashSet isn't a saveable container — runtime dedup on Unlock.
    /// Triggers, menu options, and content filters consult IsUnlocked via
    /// TriggerRegistry predicates (e.g. "lifestyle:forager_provisions").
    /// </summary>
    [Serializable]
    public sealed class LifestyleUnlockStore
    {
        public static LifestyleUnlockStore Instance { get; private set; }

        public List<string> UnlockedFeatures { get; set; } = new List<string>();

        internal static void SetInstance(LifestyleUnlockStore instance) => Instance = instance;

        public void EnsureInitialized()
        {
            if (UnlockedFeatures == null)
            {
                UnlockedFeatures = new List<string>();
            }
        }

        public bool IsUnlocked(string featureId)
        {
            if (string.IsNullOrEmpty(featureId))
            {
                return false;
            }
            EnsureInitialized();
            return UnlockedFeatures.Contains(featureId);
        }

        public void Unlock(string featureId)
        {
            if (string.IsNullOrEmpty(featureId))
            {
                return;
            }
            EnsureInitialized();
            if (!UnlockedFeatures.Contains(featureId))
            {
                UnlockedFeatures.Add(featureId);
            }
        }
    }
}
