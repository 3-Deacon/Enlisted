using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>Authored weight: base value plus conditional multiplicative modifiers.</summary>
    public sealed class WeightDecl
    {
        public float Base { get; set; } = 1.0f;
        public List<WeightModifier> Modifiers { get; set; } = new List<WeightModifier>();
    }

    public sealed class WeightModifier
    {
        public string If { get; set; } = string.Empty;     // named trigger predicate id
        public float Factor { get; set; } = 1.0f;
    }
}
