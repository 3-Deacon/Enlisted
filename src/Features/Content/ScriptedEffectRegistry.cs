using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Stub — Task 6.2 will replace this with the full JSON-backed implementation.
    /// Returns null for all ids, causing EffectExecutor to treat every apply as a primitive.
    /// </summary>
    public static class ScriptedEffectRegistry
    {
        public static IList<EffectDecl> Resolve(string id) => null;
    }
}
