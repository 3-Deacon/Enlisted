namespace Enlisted.Features.Content.Models
{
    /// <summary>
    /// Player behavior tracking for content preferences.
    /// Learns from choices over time.
    /// </summary>
    public class PlayerPreferences
    {
        /// <summary>0=social only, 1=combat only, 0.5=balanced.</summary>
        public float CombatVsSocial { get; set; } = 0.5f;

        /// <summary>0=always safe, 1=always risky.</summary>
        public float RiskyVsSafe { get; set; } = 0.5f;

        /// <summary>0=selfish, 1=dutiful.</summary>
        public float LoyalVsSelfServing { get; set; } = 0.5f;

        /// <summary>Total choices made for debugging.</summary>
        public int TotalChoicesMade { get; set; }

        /// <summary>Combat-oriented choices.</summary>
        public int CombatChoices { get; set; }

        /// <summary>Social-oriented choices.</summary>
        public int SocialChoices { get; set; }

        /// <summary>Risky choices.</summary>
        public int RiskyChoices { get; set; }

        /// <summary>Safe choices.</summary>
        public int SafeChoices { get; set; }
    }
}
