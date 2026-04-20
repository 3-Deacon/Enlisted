using System;
using System.Collections.Generic;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Mod.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace Enlisted.Features.Qualities
{
    /// <summary>
    /// CampaignBehavior that owns QualityStore lifecycle. Loads JSON quality definitions,
    /// registers the ReadThrough qualities (native TaleWorlds proxies), persists stored
    /// values via SyncData, runs decay on daily tick.
    /// </summary>
    public sealed class QualityBehavior : CampaignBehaviorBase
    {
        private QualityStore _store = new QualityStore();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                dataStore.SyncData("_qualityStore", ref _store);
                _store = _store ?? new QualityStore();
                QualityStore.SetInstance(_store);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUALITY", "QualityBehavior SyncData failed", ex);
                _store = new QualityStore();
                QualityStore.SetInstance(_store);
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            QualityStore.SetInstance(_store);
            // ScriptedEffectRegistry.LoadAll(); // Task 6.2 wires this
            // StoryletCatalog.LoadAll();         // Task 9.2 wires this
            RegisterDefinitions();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            QualityStore.SetInstance(_store);
            // ScriptedEffectRegistry.LoadAll(); // Task 6.2 wires this
            // StoryletCatalog.LoadAll();         // Task 9.2 wires this
            RegisterDefinitions();
        }

        private void OnDailyTick()
        {
            try
            {
                _store.ApplyDecayTick(CampaignTime.Now);
            }
            catch (Exception ex)
            {
                ModLogger.Caught("QUALITY", "Quality decay tick failed", ex);
            }
        }

        /// <summary>Load JSON defs and stitch in the C# ReadThrough proxies.</summary>
        private void RegisterDefinitions()
        {
            var defs = QualityCatalog.LoadAll();

            defs.Add(new QualityDefinition
            {
                Id = "rank_xp",
                DisplayName = "{=q_rank_xp}Rank XP",
                Min = 0,
                Max = int.MaxValue,
                Default = 0,
                Scope = QualityScope.Global,
                Kind = QualityKind.ReadThrough,
                Writable = true,
                Reader = _ => EnlistmentBehavior.Instance?.EnlistmentXP ?? 0,
                Writer = (hero, delta, reason) => EnlistmentBehavior.Instance?.AddEnlistmentXP(delta, reason ?? "Quality")
            });

            defs.Add(new QualityDefinition
            {
                Id = "days_in_rank",
                DisplayName = "{=q_days_in_rank}Days In Rank",
                Min = 0,
                Max = int.MaxValue,
                Default = 0,
                Scope = QualityScope.Global,
                Kind = QualityKind.ReadThrough,
                Writable = false,
                Reader = _ => EnlistmentBehavior.Instance?.DaysInRank ?? 0,
                Writer = null
            });

            defs.Add(new QualityDefinition
            {
                Id = "battles_survived",
                DisplayName = "{=q_battles}Battles Survived",
                Min = 0,
                Max = int.MaxValue,
                Default = 0,
                Scope = QualityScope.Global,
                Kind = QualityKind.ReadThrough,
                Writable = true,
                Reader = _ => EnlistmentBehavior.Instance?.BattlesSurvived ?? 0,
                Writer = (hero, delta, reason) =>
                {
                    var e = EnlistmentBehavior.Instance;
                    if (e == null)
                    {
                        return;
                    }
                    for (var i = 0; i < delta; i++)
                    {
                        e.IncrementBattlesSurvived();
                    }
                }
            });

            defs.Add(new QualityDefinition
            {
                Id = "rank",
                DisplayName = "{=q_rank}Rank",
                Min = 1,
                Max = 9,
                Default = 1,
                Scope = QualityScope.Global,
                Kind = QualityKind.ReadThrough,
                Writable = false,
                Reader = _ => EnlistmentBehavior.Instance?.EnlistmentTier ?? 1,
                Writer = null
            });

            defs.Add(new QualityDefinition
            {
                Id = "days_enlisted",
                DisplayName = "{=q_days_enlisted}Days Enlisted",
                Min = 0,
                Max = int.MaxValue,
                Default = 0,
                Scope = QualityScope.Global,
                Kind = QualityKind.ReadThrough,
                Writable = false,
                Reader = _ =>
                {
                    var e = EnlistmentBehavior.Instance;
                    if (e == null || e.EnlistmentDate == CampaignTime.Zero)
                    {
                        return 0;
                    }
                    return (int)(CampaignTime.Now - e.EnlistmentDate).ToDays;
                },
                Writer = null
            });

            defs.Add(new QualityDefinition
            {
                Id = "lord_relation",
                DisplayName = "{=q_lord_rel}Lord Relation",
                Min = -100,
                Max = 100,
                Default = 0,
                Scope = QualityScope.Global,
                Kind = QualityKind.ReadThrough,
                Writable = true,
                Reader = _ => (int)(EnlistmentBehavior.Instance?.EnlistedLord?.GetRelationWithPlayer() ?? 0),
                Writer = (hero, delta, reason) =>
                {
                    var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                    if (lord == null)
                    {
                        return;
                    }
                    ChangeRelationAction.ApplyPlayerRelation(lord, delta, affectRelatives: false, showQuickNotification: false);
                }
            });

            _store.RegisterDefinitions(defs);
        }
    }
}
