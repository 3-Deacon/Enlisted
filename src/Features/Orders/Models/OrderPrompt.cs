using System.Collections.Generic;

namespace Enlisted.Features.Orders.Models
{
    /// <summary>
    /// Represents a prompt template for the Order Prompt system.
    /// Prompts fire during order phases (15% chance) asking player if they want to investigate.
    /// </summary>
    public class OrderPrompt
    {
        /// <summary>
        /// The title displayed in the prompt dialog (e.g., "Something Stirs").
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The description text explaining what the player notices.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Text for the investigate button (e.g., "Investigate", "Check It Out").
        /// </summary>
        public string InvestigateText { get; set; }

        /// <summary>
        /// Text for the ignore/decline button (e.g., "Stay Focused", "Ignore It").
        /// </summary>
        public string IgnoreText { get; set; }

        /// <summary>
        /// Contexts where this prompt can appear: "land", "sea", or "any".
        /// Filters prompts to contextually appropriate situations.
        /// </summary>
        public List<string> Contexts { get; set; } = new List<string>();

        /// <summary>
        /// Campaign contexts where this prompt is valid (PostBattle, NormalCampaign, etc.).
        /// </summary>
        public List<string> ValidCampaignContexts { get; set; } = new List<string>();
    }
}
