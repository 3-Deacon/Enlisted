using System;
using System.Collections.Generic;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Flags;
using Enlisted.Features.Qualities;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace Enlisted.Features.CampaignIntelligence.Career
{
    /// <summary>Listens for EnlistmentBehavior.OnTierChanged and at milestone tiers {4, 6, 9} picks the highest-scoring path_&lt;id&gt;_score quality across PathIds.All (stable first-match on tie), then emits a Modal path_crossroads_&lt;path&gt;_t&lt;tier&gt; storylet via StoryDirector.EmitCandidate with ChainContinuation=true. Skips emission when no path has a positive score or when the picked path is already committed at a sub-7 milestone.</summary>
    public sealed class PathCrossroadsBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            // Static event persists across Campaign teardown; guard against duplicate
            // subscription when RegisterEvents is re-invoked on save/load cycles.
            EnlistmentBehavior.OnTierChanged -= OnTierChanged;
            EnlistmentBehavior.OnTierChanged += OnTierChanged;
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnTierChanged(int oldTier, int newTier)
        {
            try
            {
                if (newTier != 4 && newTier != 6 && newTier != 9)
                {
                    return;
                }

                var qualityStore = QualityStore.Instance;
                if (qualityStore == null)
                {
                    ModLogger.Expected("PATH", "crossroads_no_quality_store",
                        "QualityStore.Instance null at crossroads milestone");
                    return;
                }

                string pickedPath = null;
                int pickedScore = 0;
                foreach (var id in PathIds.All)
                {
                    var score = qualityStore.Get("path_" + id + "_score");
                    if (score > pickedScore)
                    {
                        pickedScore = score;
                        pickedPath = id;
                    }
                }

                if (pickedPath == null || pickedScore <= 0)
                {
                    ModLogger.Expected("PATH", "crossroads_no_scored_path",
                        "No path scored above zero at crossroads milestone",
                        new Dictionary<string, object>
                        {
                            { "tier", newTier }
                        });
                    return;
                }

                var flagStore = FlagStore.Instance;
                if (flagStore != null && flagStore.Has("committed_path_" + pickedPath) && newTier < 7)
                {
                    ModLogger.Expected("PATH", "crossroads_already_committed",
                        "Path already committed at sub-T7 milestone",
                        new Dictionary<string, object>
                        {
                            { "path", pickedPath },
                            { "tier", newTier }
                        });
                    return;
                }

                var storyletId = "path_crossroads_" + pickedPath + "_t" + newTier.ToString();
                var storylet = StoryletCatalog.GetById(storyletId);
                if (storylet == null)
                {
                    ModLogger.Expected("PATH", "crossroads_missing_storylet",
                        "Crossroads storylet not found in catalog",
                        new Dictionary<string, object>
                        {
                            { "storylet_id", storyletId }
                        });
                    return;
                }

                var ctx = new StoryletContext
                {
                    CurrentContext = "any",
                    EvaluatedAt = CampaignTime.Now,
                    SourceStorylet = storylet
                };

                EffectExecutor.Apply(storylet.Immediate, ctx);

                var evt = StoryletEventAdapter.BuildModal(storylet, ctx, null);
                if (evt == null)
                {
                    ModLogger.Expected("PATH", "crossroads_buildmodal_null",
                        "BuildModal returned null for crossroads storylet",
                        new Dictionary<string, object>
                        {
                            { "storylet_id", storylet.Id }
                        });
                    return;
                }

                StoryDirector.Instance?.EmitCandidate(new StoryCandidate
                {
                    SourceId = "path.crossroads",
                    CategoryId = "path.crossroads",
                    ProposedTier = StoryTier.Modal,
                    ChainContinuation = true,
                    EmittedAt = CampaignTime.Now,
                    InteractiveEvent = evt,
                    RenderedTitle = storylet.Title,
                    RenderedBody = storylet.Setup,
                    StoryKey = storylet.Id
                });

                ModLogger.Info("PATH",
                    $"crossroads_emitted: {storylet.Id} path={pickedPath} tier={newTier} score={pickedScore}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("PATH", "OnTierChanged crossroads threw", ex);
            }
        }
    }
}
