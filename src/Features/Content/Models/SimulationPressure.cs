using System.Collections.Generic;

namespace Enlisted.Features.Content.Models
{
    /// <summary>
    /// Simulation pressure from company/player state.
    /// Modifies realistic frequency up or down.
    /// </summary>
    public class SimulationPressure
    {
        /// <summary>0-100 scale.</summary>
        public float Value { get; set; }

        /// <summary>Human-readable reasons.</summary>
        public List<string> Sources { get; set; } = new List<string>();

        /// <summary>
        /// Converts pressure to frequency modifier.
        /// High pressure = more frequent events.
        /// </summary>
        /// <returns>
        /// 0 pressure = 1.0x (no change)
        /// 50 pressure = 1.15x
        /// 100 pressure = 1.3x
        /// </returns>
        public float GetFrequencyModifier()
        {
            return 1.0f + (Value / 100f * 0.3f);
        }
    }
}
