using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;

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

        public virtual bool IsEligible(StoryletContext ctx)
        {
            if (ctx == null)
            {
                return false;
            }

            // Scope.context filter — skipped for mid-encounter storylets (spec §10.3).
            if (!string.IsNullOrEmpty(Scope?.Context)
                && !string.Equals(Scope.Context, "any", System.StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Scope.Context, ctx.CurrentContext, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Slot requirements — any unfilled required slot = ineligible.
            var resolved = SlotFiller.Resolve(Scope);
            if (!SlotFiller.AllRequiredFilled(resolved))
            {
                return false;
            }

            ctx.ResolvedSlots = resolved;

            // Trigger predicates.
            if (!TriggerRegistry.Evaluate(Trigger, ctx))
            {
                return false;
            }

            return true;
        }

        public virtual float WeightFor(StoryletContext ctx)
        {
            var w = Weight?.Base ?? 1.0f;
            if (Weight?.Modifiers == null)
            {
                return w;
            }

            foreach (var mod in Weight.Modifiers)
            {
                if (string.IsNullOrEmpty(mod.If))
                {
                    continue;
                }

                if (TriggerRegistry.EvaluateOne(mod.If, ctx))
                {
                    w *= mod.Factor;
                }
            }
            return w;
        }

        public virtual StoryCandidate ToCandidate(StoryletContext ctx)
        {
            if (ctx == null)
            {
                return null;
            }

            return new StoryCandidate
            {
                SourceId = "storylet." + Id,
                CategoryId = string.IsNullOrEmpty(Category) ? "storylet" : Category,
                ProposedTier = Observational ? StoryTier.Log : StoryTier.Modal,
                SeverityHint = 0.4f,
                Beats = new HashSet<StoryBeat>(),
                Relevance = new RelevanceKey
                {
                    SubjectHero = ctx.SubjectHero,
                    TouchesEnlistedLord = ctx.SubjectHero != null &&
                        ctx.SubjectHero == EnlistmentBehavior.Instance?.EnlistedLord,
                },
                EmittedAt = ctx.EvaluatedAt,
                HasObservationalRender = Observational,
                RenderedTitle = SlotFiller.Render(Title, ctx.ResolvedSlots),
                RenderedBody = SlotFiller.Render(Setup, ctx.ResolvedSlots),
                StoryKey = Id
            };
        }
    }
}
