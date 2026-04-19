using System.Collections.Generic;

namespace Enlisted.Features.Content
{
    /// <summary>
    /// Authored narrative unit. 95% of storylets use the default implementations;
    /// complex cases (chain coordinators, bespoke slot-fillers) subclass.
    /// </summary>
    public class Storylet
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Setup { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "normal";
        public bool Observational { get; set; }                 // news-style storylets have this true + empty Options
        public ScopeDecl Scope { get; set; } = new ScopeDecl();
        public List<string> Trigger { get; set; } = new List<string>();
        public WeightDecl Weight { get; set; } = new WeightDecl();
        public List<EffectDecl> Immediate { get; set; } = new List<EffectDecl>();
        public List<Choice> Options { get; set; } = new List<Choice>();
        public int CooldownDays { get; set; }
        public bool OneTime { get; set; }

        // Default impls are filled in during Stage 9 (Task 9.1) after the registry wiring exists.
        public virtual bool IsEligible(StoryletContext ctx) => true;
        public virtual float WeightFor(StoryletContext ctx) => Weight?.Base ?? 1.0f;
        public virtual StoryCandidate ToCandidate(StoryletContext ctx) => null;
    }
}
