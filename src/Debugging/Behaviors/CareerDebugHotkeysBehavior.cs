using System;
using System.Collections.Generic;
using System.Text;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.CampaignIntelligence;
using Enlisted.Features.CampaignIntelligence.Career;
using Enlisted.Features.Flags;
using Enlisted.Features.Qualities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.InputSystem;

namespace Enlisted.Debugging.Behaviors
{
    /// <summary>
    ///     Polls Ctrl+Shift+{I,A,O,F} on the campaign TickEvent and dumps / fires career-loop
    ///     inspection helpers to the session log. Paired with the Spec 1 Home-focused
    ///     <see cref="DebugHotkeysBehavior"/>; letter choices avoid claims by that behavior
    ///     (H/E/B/Y) and by the native TaleWorlds Debug category (E/P/Tab/R/X/K/M, plus T by
    ///     observed collision).
    ///     <para/>
    ///     Bindings:
    ///     <list type="bullet">
    ///         <item>Ctrl+Shift+I — dumps the current <c>EnlistedLordIntelligenceSnapshot</c> fields.</item>
    ///         <item>Ctrl+Shift+A — dumps <c>OrderActivity.ActiveNamedOrder</c> state (id, phase, intent).</item>
    ///         <item>Ctrl+Shift+O — dumps all five <c>path_*_score</c> qualities plus committed/resisted flags.</item>
    ///         <item>Ctrl+Shift+F — force-fires <c>PathCrossroadsBehavior.FireCrossroadsStorylet</c> at tier 4 for the current top-scoring path, or ranger if no path is scored. Intended for T10 smoke.</item>
    ///     </list>
    /// </summary>
    public sealed class CareerDebugHotkeysBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No persisted state — hotkey polling is entirely runtime.
        }

        private void OnTick(float dt)
        {
            try
            {
                var ctrl = Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
                var shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
                if (!(ctrl && shift))
                {
                    return;
                }

                if (Input.IsKeyPressed(InputKey.I))
                {
                    DumpIntelSnapshot();
                }
                else if (Input.IsKeyPressed(InputKey.A))
                {
                    DumpArcState();
                }
                else if (Input.IsKeyPressed(InputKey.O))
                {
                    DumpPathOverview();
                }
                else if (Input.IsKeyPressed(InputKey.F))
                {
                    ForceCrossroadsFire();
                }
            }
            catch (Exception ex)
            {
                ModLogger.Caught("CAREER-DEBUG", "Career debug hotkey tick failed", ex);
            }
        }

        private static void DumpIntelSnapshot()
        {
            var snapshot = EnlistedCampaignIntelligenceBehavior.Instance?.Current;
            if (snapshot == null)
            {
                ModLogger.Info("CAREER-DEBUG", "intel_dump: no snapshot (not enlisted or pre-recompute)");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("intel_dump: ");
            sb.Append("posture=").Append(snapshot.Posture).Append(' ');
            sb.Append("front_pressure=").Append(snapshot.FrontPressure).Append(' ');
            sb.Append("army_strain=").Append(snapshot.ArmyStrain).Append(' ');
            sb.Append("supply_pressure=").Append(snapshot.SupplyPressure).Append(' ');
            sb.Append("recent_changes=").Append(snapshot.RecentChanges);
            ModLogger.Info("CAREER-DEBUG", sb.ToString());
        }

        private static void DumpArcState()
        {
            var activity = OrderActivity.Instance;
            if (activity == null)
            {
                ModLogger.Info("CAREER-DEBUG", "arc_dump: OrderActivity.Instance is null");
                return;
            }
            var arc = activity.ActiveNamedOrder;
            if (arc == null)
            {
                ModLogger.Info("CAREER-DEBUG", "arc_dump: no active named order");
                return;
            }
            ModLogger.Info("CAREER-DEBUG",
                $"arc_dump: storylet={arc.OrderStoryletId} phase_index={arc.CurrentPhaseIndex} intent={arc.Intent}");
        }

        private static void DumpPathOverview()
        {
            var qualityStore = QualityStore.Instance;
            if (qualityStore == null)
            {
                ModLogger.Info("CAREER-DEBUG", "path_overview: QualityStore is null");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("path_overview: ");
            foreach (var id in PathIds.All)
            {
                var score = qualityStore.Get("path_" + id + "_score");
                sb.Append(id).Append('=').Append(score).Append(' ');
            }
            sb.Append("| committed=").Append(qualityStore.Get("committed_path"));

            var flagStore = FlagStore.Instance;
            if (flagStore != null)
            {
                sb.Append(" | flags:");
                foreach (var id in PathIds.All)
                {
                    if (flagStore.Has("committed_path_" + id))
                    {
                        sb.Append(" committed_").Append(id);
                    }
                    if (flagStore.Has("path_resisted_" + id))
                    {
                        sb.Append(" resisted_").Append(id);
                    }
                }
            }

            ModLogger.Info("CAREER-DEBUG", sb.ToString());
        }

        private static void ForceCrossroadsFire()
        {
            var qualityStore = QualityStore.Instance;
            string pickedPath = "ranger";
            int pickedScore = 0;
            if (qualityStore != null)
            {
                foreach (var id in PathIds.All)
                {
                    var score = qualityStore.Get("path_" + id + "_score");
                    if (score > pickedScore)
                    {
                        pickedScore = score;
                        pickedPath = id;
                    }
                }
            }
            ModLogger.Info("CAREER-DEBUG",
                $"force_crossroads: path={pickedPath} score={pickedScore} tier=4 (debug-forced, guards bypassed)");
            PathCrossroadsBehavior.FireCrossroadsStorylet(pickedPath, 4, pickedScore);
        }
    }
}
