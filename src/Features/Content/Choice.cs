using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>One option inside a Storylet.</summary>
    public sealed class Choice
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;          // "{=id}Fallback"
        public string Tooltip { get; set; } = string.Empty;       // "{=id}Fallback"
        public List<string> Trigger { get; set; } = new List<string>();       // option-level gate
        public List<EffectDecl> Effects { get; set; } = new List<EffectDecl>();
        public List<EffectDecl> EffectsSuccess { get; set; } = new List<EffectDecl>();
        public List<EffectDecl> EffectsFailure { get; set; } = new List<EffectDecl>();
        public ChainDecl Chain { get; set; }                       // optional
    }
}
