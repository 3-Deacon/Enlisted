using System.Collections.Generic;

namespace Enlisted.Features.Orders.Models
{
    /// <summary>
    /// Data for a specific outcome instance (text, effects, chain references).
    /// Used by PromptCatalog to resolve pre-rolled outcomes.
    /// </summary>
    public class PromptOutcomeData
    {
        /// <summary>
        /// Display text shown to player when outcome resolves (e.g., "Just the wind.").
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Effects to apply: gold, skillXp, reputation, supplies, injury, etc.
        /// </summary>
        public Dictionary<string, object> Effects { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// For EventChain outcomes: the chain ID to trigger.
        /// </summary>
        public string ChainId { get; set; }

        /// <summary>
        /// For EventChain outcomes: specific event ID to fire as chain start.
        /// </summary>
        public string TriggerEvent { get; set; }

        /// <summary>
        /// Delay in hours before consequence fires (for delayed outcomes).
        /// </summary>
        public float? DelayHours { get; set; }
    }
}
