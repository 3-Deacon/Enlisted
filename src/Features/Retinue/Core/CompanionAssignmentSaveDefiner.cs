using System.Collections.Generic;
using Enlisted.Mod.Core.Util;
using TaleWorlds.SaveSystem;

namespace Enlisted.Features.Retinue.Core
{
    /// <summary>
    /// Registers save container definitions used by companion assignment state.
    ///
    /// Bannerlord's base save definer does not include Dictionary&lt;string, bool&gt;,
    /// and the save system does not auto-generate missing container definitions.
    /// This is discovered via reflection at runtime.
    /// </summary>
    [UsedImplicitly("Discovered via reflection by TaleWorlds.SaveSystem (SaveableTypeDefiner scan).")]
    public sealed class CompanionAssignmentSaveDefiner : SaveableTypeDefiner
    {
        public CompanionAssignmentSaveDefiner() : base(735201)
        {
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<string, bool>));
        }
    }
}


