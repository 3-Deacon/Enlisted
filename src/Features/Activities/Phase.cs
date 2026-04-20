using System.Collections.Generic;

namespace Enlisted.Features.Activities
{
    /// <summary>
    /// One phase inside an Activity. Phases come from ActivityTypeDefinition at startup;
    /// an Activity instance carries only a CurrentPhaseIndex in save, phases are re-resolved
    /// from the type def on load.
    /// </summary>
    public sealed class Phase
    {
        public string Id { get; set; } = string.Empty;
        public int DurationHours { get; set; }
        public int FireIntervalHours { get; set; }                       // 0 = fire once per phase; N = fire at most every N in-game hours
        public List<string> Pool { get; set; } = new List<string>();     // storylet ids
        public Dictionary<string, float> IntentBias { get; set; } = new Dictionary<string, float>();  // intent -> weight multiplier
        public PhaseDelivery Delivery { get; set; } = PhaseDelivery.Auto;
        public int PlayerChoiceCount { get; set; } = 4;                  // how many options to offer when PlayerChoice
    }
}
