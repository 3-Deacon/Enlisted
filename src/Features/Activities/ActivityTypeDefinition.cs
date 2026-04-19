using System.Collections.Generic;

namespace Enlisted.Features.Activities
{
    /// <summary>Loaded from ModuleData/Enlisted/Activities/*.json (surface specs author these). Spec 0 defines the shape only.</summary>
    public sealed class ActivityTypeDefinition
    {
        public string Id { get; set; } = string.Empty;                  // "activity_camp", "activity_muster", ...
        public string DisplayName { get; set; } = string.Empty;         // loc key
        public List<Phase> Phases { get; set; } = new List<Phase>();
        public List<string> ValidIntents { get; set; } = new List<string>();
    }
}
