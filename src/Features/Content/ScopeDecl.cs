using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>Authored scope declaration: context filter + slot table.</summary>
    public sealed class ScopeDecl
    {
        public string Context { get; set; } = "any";   // "land" | "sea" | "siege" | "settlement" | "any"
        public Dictionary<string, SlotDecl> Slots { get; set; } = new Dictionary<string, SlotDecl>();
    }
}
