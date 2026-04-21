using System;
using System.Collections.Generic;
using Enlisted.Features.Content;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>Splices arc phases from a named-order storylet onto the active OrderActivity, unsplices on clear, and rebuilds the combined phase list after save/load by re-deriving from ActiveNamedOrder.OrderStoryletId.</summary>
    public static class NamedOrderArcRuntime
    {
        public static void SpliceArc(Storylet sourceStorylet, string intent)
        {
            try
            {
                if (sourceStorylet?.Arc == null)
                {
                    return;
                }
                var activity = OrderActivity.Instance;
                if (activity == null)
                {
                    return;
                }
                if (activity.ActiveNamedOrder != null)
                {
                    ModLogger.Expected("ARC", "splice_already_active",
                        $"Cannot splice {sourceStorylet.Id}: order {activity.ActiveNamedOrder.OrderStoryletId} still active");
                    return;
                }

                activity.ActiveNamedOrder = new NamedOrderState
                {
                    OrderStoryletId = sourceStorylet.Id,
                    StartedAt = CampaignTime.Now,
                    CurrentPhaseIndex = 0,
                    Intent = intent ?? string.Empty,
                    CombatClassAtAccept = activity.CachedCombatClass,
                };

                BuildAndSeatPhases(activity, sourceStorylet);
                // Advance to the first arc phase (baseCount in the combined list) so the
                // arc starts ticking immediately rather than waiting out the base phase
                // duration. Reseat the time anchors so TryAdvancePhase's elapsed math
                // starts from the splice moment.
                var baseCount = (activity.Phases?.Count ?? 0) - sourceStorylet.Arc.Phases.Count;
                activity.CurrentPhaseIndex = Math.Max(0, baseCount);
                activity.StartedAt = CampaignTime.Now;
                activity.LastAutoFireHour = -1;
                ModLogger.Info("ARC", $"spliced {sourceStorylet.Id} (intent={intent})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ARC", "SpliceArc threw", ex);
            }
        }

        /// <summary>Resets the activity's Phases to the base type-def phases and sets CurrentPhaseIndex to 0, dropping the player back to the start of the duty-profile flow when the arc resolves or is cancelled.</summary>
        public static void UnspliceArc()
        {
            try
            {
                var activity = OrderActivity.Instance;
                if (activity?.ActiveNamedOrder == null)
                {
                    return;
                }
                var orderId = activity.ActiveNamedOrder.OrderStoryletId;
                activity.ActiveNamedOrder = null;
                activity.ResolvePhasesFromType(ActivityRuntime.Instance?.GetTypes());
                activity.CurrentPhaseIndex = 0;
                ModLogger.Info("ARC", $"unspliced {orderId}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ARC", "UnspliceArc threw", ex);
            }
        }

        public static void RebuildSplicedPhasesOnLoad(OrderActivity activity, Storylet sourceStorylet)
        {
            try
            {
                if (activity?.ActiveNamedOrder == null || sourceStorylet?.Arc == null)
                {
                    return;
                }
                BuildAndSeatPhases(activity, sourceStorylet);
                ModLogger.Info("ARC", $"reconstructed {sourceStorylet.Id} on load (phaseIndex={activity.CurrentPhaseIndex})");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ARC", "RebuildSplicedPhasesOnLoad threw", ex);
            }
        }

        private static void BuildAndSeatPhases(OrderActivity activity, Storylet sourceStorylet)
        {
            var combined = new List<Phase>();

            activity.ResolvePhasesFromType(ActivityRuntime.Instance?.GetTypes());
            if (activity.Phases != null)
            {
                combined.AddRange(activity.Phases);
            }

            foreach (var arcPhase in sourceStorylet.Arc.Phases)
            {
                combined.Add(new Phase
                {
                    Id = arcPhase.Id,
                    DurationHours = arcPhase.DurationHours,
                    FireIntervalHours = arcPhase.FireIntervalHours,
                    Pool = ResolvePoolByPrefix(arcPhase.PoolPrefix),
                    Delivery = string.Equals(arcPhase.Delivery, "player_choice", StringComparison.OrdinalIgnoreCase)
                        ? PhaseDelivery.PlayerChoice
                        : PhaseDelivery.Auto
                });
            }

            activity.Phases = combined;
        }

        private static List<string> ResolvePoolByPrefix(string prefix)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(prefix))
            {
                return result;
            }
            foreach (var s in StoryletCatalog.All)
            {
                if (s?.Id != null && s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(s.Id);
                }
            }
            return result;
        }
    }
}
