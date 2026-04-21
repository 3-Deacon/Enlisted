using System;
using System.Collections.Generic;
using Enlisted.Features.Content;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace Enlisted.Features.Activities.Orders
{
    /// <summary>
    /// The career-arc activity that runs continuously while enlisted. Holds duty
    /// profile state, an optional active named-order arc, and a cached combat class.
    /// Owns arc reconstruction in ReconstructArcOnLoad — re-extracts the arc block
    /// from the source storylet so CurrentPhaseIndex resolves correctly across
    /// save/load (the base Activity._cachedPhases is [NonSerialized]).
    /// </summary>
    [Serializable]
    public sealed class OrderActivity : Activity
    {
        public const string TYPE_ID = "order_activity";

        public string CurrentDutyProfile { get; set; } = "wandering";
        public Dictionary<string, int> PendingProfileMatches { get; set; } = new Dictionary<string, int>();
        public int LastProfileSampleHourTick { get; set; }
        public NamedOrderState ActiveNamedOrder { get; set; }
        public FormationClass CachedCombatClass { get; set; } = FormationClass.Infantry;
        public int CombatClassResampleHourTick { get; set; }

        public static OrderActivity Instance => ActivityRuntime.Instance?.FindActive<OrderActivity>();

        public override void OnStart(ActivityContext ctx)
        {
            TypeId = TYPE_ID;
            EnsureInitialized();
            CachedCombatClass = CombatClassResolver.Resolve(Hero.MainHero);
        }

        public override void Tick(bool hourly)
        {
            // Ambient ticking handled by phase pool selector + DailyDriftApplicator in later tasks.
        }

        public override void OnPhaseEnter(Phase phase)
        {
        }

        public override void OnPhaseExit(Phase phase)
        {
        }

        public override void Finish(ActivityEndReason reason)
        {
            ActiveNamedOrder = null;
        }

        /// <summary>
        /// Re-extracts the active named-order's arc from its source storylet and
        /// re-splices arc phases so CurrentPhaseIndex resolves after save/load.
        /// The base Activity._cachedPhases is [NonSerialized] so without this the
        /// arc state would evaporate on reload.
        /// </summary>
        public void ReconstructArcOnLoad()
        {
            EnsureInitialized();
            if (ActiveNamedOrder == null || string.IsNullOrEmpty(ActiveNamedOrder.OrderStoryletId))
            {
                return;
            }

            try
            {
                var storylet = StoryletCatalog.GetById(ActiveNamedOrder.OrderStoryletId);
                if (storylet?.Arc == null)
                {
                    ModLogger.Expected("ARC", "stale_arc_id_on_load",
                        $"OrderStoryletId='{ActiveNamedOrder.OrderStoryletId}' missing or has no arc; clearing");
                    ActiveNamedOrder = null;
                    return;
                }
                NamedOrderArcRuntime.RebuildSplicedPhasesOnLoad(this, storylet);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("ARC", "arc reconstruction threw", ex);
                ActiveNamedOrder = null;
            }
        }

        public void EnsureInitialized()
        {
            if (PendingProfileMatches == null)
            {
                PendingProfileMatches = new Dictionary<string, int>();
            }
            if (ActiveNamedOrder != null && ActiveNamedOrder.AccumulatedOutcomes == null)
            {
                ActiveNamedOrder.AccumulatedOutcomes = new Dictionary<string, float>();
            }
        }
    }

    // TODO Task 14: Delete this stub and replace with a real NamedOrderArcRuntime.cs
    // in its own file under Enlisted.Features.Activities.Orders. This stub exists
    // solely to make Phase A (Tasks 1-9) build self-contained.
    internal static class NamedOrderArcRuntime
    {
        public static void RebuildSplicedPhasesOnLoad(OrderActivity activity, Storylet storylet)
        {
        }
    }
}
