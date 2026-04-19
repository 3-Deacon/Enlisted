using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>An effect to apply. `Apply` is the primitive id (quality_add, set_flag, ...) or scripted effect id.</summary>
    public sealed class EffectDecl
    {
        public string Apply { get; set; } = string.Empty;
        public Dictionary<string, object> Params { get; set; } = new Dictionary<string, object>();
    }
}
