using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Declarative arc schema parsed from storylet JSON. DurationHours must be > 0
    /// for the arc to splice — zero is treated as unconfigured.
    /// </summary>
    public sealed class ArcSpec
    {
        public int DurationHours { get; set; }
        public List<ArcPhaseSpec> Phases { get; set; } = new List<ArcPhaseSpec>();
        public string OnTransitionInterruptStorylet { get; set; } = string.Empty;
    }

    /// <summary>
    /// Single phase within a declarative arc schema. DurationHours and FireIntervalHours
    /// must be > 0 for the phase to schedule storylet emissions.
    /// </summary>
    public sealed class ArcPhaseSpec
    {
        public string Id { get; set; } = string.Empty;
        public int DurationHours { get; set; }
        public string PoolPrefix { get; set; } = string.Empty;
        public int FireIntervalHours { get; set; }
        public string Delivery { get; set; } = "auto";  // "auto" | "player_choice"
    }
}
