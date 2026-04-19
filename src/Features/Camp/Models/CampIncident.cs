using System.Collections.Generic;
using TaleWorlds.Localization;

namespace Enlisted.Features.Camp.Models
{
    /// <summary>
    /// Represents a background incident that can occur in the camp simulation.
    /// Loaded from simulation_config.json.
    /// </summary>
    public struct CampIncident
    {
        // Identification
        public string Id;
        public string Category;     // "problems", "camp_life", "scrutiny", "discovery", "social"
        public string Severity;     // "flavor", "minor", "notable", "serious", "critical"

        // News text
        public string NewsTextId;   // Localization key (e.g., "{=sim_inc_fight}...")
        public string NewsTextFallback; // Fallback if key missing

        // Selection
        public int Weight;          // Higher = more likely to be selected
        public int CooldownDays;    // Days before this incident can fire again

        // Conditions
        public string RequiresFlag; // Only fire if this flag is active
        public string SetsFlag;     // Set this flag when fired

        // Effects on company needs
        public Dictionary<string, int> Effects;

        /// <summary>
        /// Gets the localized news text for this incident.
        /// </summary>
        public string GetNewsText()
        {
            if (!string.IsNullOrEmpty(NewsTextId))
            {
                return new TextObject(NewsTextId).ToString();
            }
            return NewsTextFallback ?? $"[{Id}]";
        }
    }
}
