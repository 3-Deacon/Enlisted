using System;
using System.Collections.Generic;
using System.Linq;
using Enlisted.Features.Activities.Orders;
using Enlisted.Features.Content;
using Enlisted.Features.Enlistment.Behaviors;
using Enlisted.Features.Equipment.UI;
using Enlisted.Mod.Core.Logging;
using Enlisted.Mod.Core.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Enlisted.Features.CampaignIntelligence.Duty
{
    /// <summary>
    /// Hourly-tick duty emitter. Gated by IsEnlisted. Reads the intelligence
    /// snapshot + current duty profile, invokes DutyOpportunityBuilder,
    /// throttle-claims via OrdersNewsFeedThrottle, picks one opportunity,
    /// emits via StoryDirector. Cooldown state persists in DutyCooldownStore
    /// via SaveableTypeDefiner offset 50.
    /// </summary>
    [UsedImplicitly("Registered in SubModule.OnGameStart via campaignStarter.AddBehavior.")]
    public sealed class EnlistedDutyEmitterBehavior : CampaignBehaviorBase
    {
        public static EnlistedDutyEmitterBehavior Instance { get; private set; }

        private DutyCooldownStore _cooldowns = new DutyCooldownStore();
        private readonly Queue<string> _recentEmittedIds = new Queue<string>();
        private int _lastHeartbeatHourTick = int.MinValue / 2;
        private int _lastDailyCountReportHourTick = int.MinValue / 2;

        private readonly Dictionary<string, int> _sessionEmissionsByProfile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private const int HEARTBEAT_INTERVAL_HOURS = 12;
        private const int DAILY_COUNT_REPORT_INTERVAL_HOURS = 24;
        private const int DEFAULT_COOLDOWN_HOURS = 36;
        private const int NAMED_ORDER_COMPLETION_COOLDOWN_HOURS = 8;
        private const int NAMED_ORDER_GLOBAL_CADENCE_HOURS = 168;
        private const int RECENT_HISTORY_SIZE = 3;
        private const float RECENT_PENALTY_PER_HIT = 0.7f;

        public override void RegisterEvents()
        {
            Instance = this;
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            try
            {
                _ = dataStore.SyncData("_dutyCooldowns", ref _cooldowns);
                _cooldowns = _cooldowns ?? new DutyCooldownStore();
                _cooldowns.EnsureInitialized();
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTY", "SyncData failed", ex);
                _cooldowns = new DutyCooldownStore();
            }
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            _cooldowns = _cooldowns ?? new DutyCooldownStore();
            _cooldowns.EnsureInitialized();
        }

        private void OnHourlyTick()
        {
            try
            {
                LogHeartbeatIfDue();
                LogDailyCountsIfDue();

                if (EnlistmentBehavior.Instance?.IsEnlisted != true)
                {
                    return;
                }

                var activity = OrderActivity.Instance;
                if (activity == null)
                {
                    return;
                }

                var snapshot = EnlistedCampaignIntelligenceBehavior.Instance?.Current;
                if (snapshot == null)
                {
                    return;
                }

                var emissionProfile = ResolveEmissionProfile(activity);
                var candidates = EnlistedDutyOpportunityBuilder.Build(snapshot, emissionProfile);
                if (candidates == null || candidates.Count == 0)
                {
                    return;
                }

                var namedOrderCandidates = candidates
                    .Where(c => c.Shape == DutyOpportunityShape.ArcScale)
                    .ToList();
                var episodicCandidates = candidates
                    .Where(c => c.Shape != DutyOpportunityShape.ArcScale)
                    .ToList();

                if (activity.ActiveNamedOrder == null)
                {
                    if (TryGetNamedOrderEmissionBlockReason(activity, out var blockReason))
                    {
                        LogNamedOrderRejected(namedOrderCandidates, blockReason, activity, emissionProfile);
                    }
                    else if (TryEmitNamedOrder(activity, namedOrderCandidates, emissionProfile))
                    {
                        return;
                    }
                }

                if (activity.ActiveNamedOrder != null)
                {
                    ModLogger.Debug("DUTY",
                        $"routine duty emission suppressed while named order is active: {activity.ActiveNamedOrder.OrderStoryletId}/{activity.ActiveNamedOrder.Intent}");
                    return;
                }

                if (episodicCandidates.Count == 0)
                {
                    return;
                }

                if (!OrdersNewsFeedThrottle.TryClaim())
                {
                    return;
                }

                foreach (var opp in episodicCandidates)
                {
                    var storylet = ResolveStoryletForOpportunity(opp);
                    if (storylet == null)
                    {
                        continue;
                    }

                    EmitOpportunity(opp, storylet);
                    return;
                }

                ModLogger.Expected("DUTY", "no_opportunity_storylet",
                    "no eligible storylet found for any episodic candidate",
                    new Dictionary<string, object>
                    {
                        { "candidate_count", episodicCandidates.Count },
                        { "named_order_candidate_count", namedOrderCandidates.Count },
                        { "profile", emissionProfile ?? activity.CurrentDutyProfile },
                        { "active_named_order", activity.ActiveNamedOrder?.OrderStoryletId ?? "none" }
                    });
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTY", "OnHourlyTick threw", ex);
            }
        }

        public static void RecordNamedOrderCompletion(string orderId, string intent)
        {
            try
            {
                var completedAt = CampaignTime.Now;
                var activity = OrderActivity.Instance;
                if (activity != null)
                {
                    activity.LastNamedOrderCompletedAt = completedAt;
                    activity.LastNamedOrderCompletedId = orderId ?? string.Empty;
                    activity.LastNamedOrderCompletedIntent = intent ?? string.Empty;
                }

                var instance = Instance;
                if (instance != null)
                {
                    instance._cooldowns = instance._cooldowns ?? new DutyCooldownStore();
                    instance._cooldowns.EnsureInitialized();
                    instance._cooldowns.RecordNamedOrderCompletion(orderId, intent, completedAt);
                }

                ModLogger.Info("DUTY",
                    $"named-order completion recorded id={orderId ?? "unknown"} intent={intent ?? "unknown"}");
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTY", "RecordNamedOrderCompletion threw", ex);
            }
        }

        private bool TryGetNamedOrderEmissionBlockReason(OrderActivity activity, out string reason)
        {
            reason = string.Empty;

            if (activity?.ActiveNamedOrder != null)
            {
                reason = $"active_named_order id={activity.ActiveNamedOrder.OrderStoryletId} intent={activity.ActiveNamedOrder.Intent}";
                return true;
            }

            if (ShouldSuppressNamedOrderEmission(out reason))
            {
                return true;
            }

            if (IsNamedOrderCompletionCooldownActive(activity, out reason))
            {
                return true;
            }

            if (IsNamedOrderGlobalCadenceActive(out reason))
            {
                return true;
            }

            return false;
        }

        private bool IsNamedOrderGlobalCadenceActive(out string reason)
        {
            reason = string.Empty;

            var emittedAt = _cooldowns?.LastNamedOrderEmittedAt ?? CampaignTime.Zero;
            if (emittedAt == CampaignTime.Zero)
            {
                return false;
            }

            var elapsedHours = (CampaignTime.Now - emittedAt).ToHours;
            if (elapsedHours >= NAMED_ORDER_GLOBAL_CADENCE_HOURS)
            {
                return false;
            }

            reason = $"named_order_service_cadence id={_cooldowns?.LastNamedOrderEmittedId ?? "unknown"} profile={_cooldowns?.LastNamedOrderEmittedProfile ?? "unknown"} cooldown={elapsedHours:0.0}/{NAMED_ORDER_GLOBAL_CADENCE_HOURS}h";
            return true;
        }

        private bool IsNamedOrderCompletionCooldownActive(OrderActivity activity, out string reason)
        {
            reason = string.Empty;

            var completedAt = CampaignTime.Zero;
            var completedId = string.Empty;
            var completedIntent = string.Empty;

            PickLatestCompletion(activity?.LastNamedOrderCompletedAt ?? CampaignTime.Zero,
                activity?.LastNamedOrderCompletedId,
                activity?.LastNamedOrderCompletedIntent,
                ref completedAt,
                ref completedId,
                ref completedIntent);

            PickLatestCompletion(_cooldowns?.LastNamedOrderCompletedAt ?? CampaignTime.Zero,
                _cooldowns?.LastNamedOrderCompletedId,
                _cooldowns?.LastNamedOrderCompletedIntent,
                ref completedAt,
                ref completedId,
                ref completedIntent);

            if (completedAt == CampaignTime.Zero)
            {
                return false;
            }

            var elapsedHours = (CampaignTime.Now - completedAt).ToHours;
            if (elapsedHours >= NAMED_ORDER_COMPLETION_COOLDOWN_HOURS)
            {
                return false;
            }

            reason = $"arc_completion_cooldown id={completedId ?? "unknown"} intent={completedIntent ?? "unknown"} cooldown={elapsedHours:0.0}/{NAMED_ORDER_COMPLETION_COOLDOWN_HOURS}h";
            return true;
        }

        private static void PickLatestCompletion(
            CampaignTime candidateAt,
            string candidateId,
            string candidateIntent,
            ref CampaignTime completedAt,
            ref string completedId,
            ref string completedIntent)
        {
            if (candidateAt == CampaignTime.Zero)
            {
                return;
            }

            if (completedAt != CampaignTime.Zero && candidateAt.ToHours <= completedAt.ToHours)
            {
                return;
            }

            completedAt = candidateAt;
            completedId = candidateId ?? string.Empty;
            completedIntent = candidateIntent ?? string.Empty;
        }

        private static void LogNamedOrderRejected(List<DutyOpportunity> namedOrderCandidates, string reason, OrderActivity activity, string emissionProfile)
        {
            var candidateId = namedOrderCandidates?.FirstOrDefault()?.ArchetypeStoryletId ?? "none";
            var count = namedOrderCandidates?.Count ?? 0;
            ModLogger.Info("DUTY",
                $"named-order rejected: reason={reason} candidate={candidateId} candidates={count} profile={emissionProfile ?? activity?.CurrentDutyProfile ?? "unknown"} active={activity?.ActiveNamedOrder?.OrderStoryletId ?? "none"}");
        }

        private static bool ShouldSuppressNamedOrderEmission(out string reason)
        {
            reason = string.Empty;

            try
            {
                if (MusterMenuHandler.Instance?.IsMusterSequenceActive == true)
                {
                    reason = "muster_active";
                    return true;
                }

                if (MusterMenuHandler.Instance?.IsQuartermasterConversationFromMusterActive == true)
                {
                    reason = "muster_quartermaster_conversation_active";
                    return true;
                }

                if (QuartermasterEquipmentSelectorBehavior.IsOpen)
                {
                    reason = "quartermaster_grid_ui_active";
                    return true;
                }

                if (QuartermasterEquipmentSelectorBehavior.IsUpgradeScreenOpen)
                {
                    reason = "quartermaster_upgrade_ui_active";
                    return true;
                }

                if (QuartermasterProvisionsBehavior.IsOpen)
                {
                    reason = "quartermaster_provisions_ui_active";
                    return true;
                }

                var delivery = EventDeliveryManager.Instance;
                if (delivery?.HasActiveOrPendingNamedOrderResolveEvent == true)
                {
                    var eventId = delivery.ActiveOrPendingNamedOrderResolveEventId;
                    reason = string.IsNullOrEmpty(eventId)
                        ? "named_order_resolve_choice_active"
                        : "named_order_resolve_choice_active:" + eventId;
                    return true;
                }

                if (delivery?.HasActiveOrPendingPromotionEvent == true)
                {
                    var eventId = delivery.ActiveOrPendingPromotionEventId;
                    reason = string.IsNullOrEmpty(eventId)
                        ? "promotion_event_active"
                        : "promotion_event_active:" + eventId;
                    return true;
                }

                if (delivery?.HasActiveOrPendingModalEvent == true)
                {
                    var eventId = delivery.ActiveOrPendingModalEventId;
                    reason = string.IsNullOrEmpty(eventId)
                        ? "modal_event_active"
                        : "modal_event_active:" + eventId;
                    return true;
                }

                var menuId = Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId;
                if (!string.IsNullOrEmpty(menuId)
                    && (menuId.StartsWith("enlisted_muster", StringComparison.OrdinalIgnoreCase)
                        || menuId.StartsWith("enlisted_qm", StringComparison.OrdinalIgnoreCase)))
                {
                    reason = "blocking_menu_active:" + menuId;
                    return true;
                }

                var mainParty = MobileParty.MainParty;
                if (mainParty?.Party?.MapEvent != null || mainParty?.MapEvent != null)
                {
                    reason = "player_party_in_map_event";
                    return true;
                }

                var lordParty = EnlistmentBehavior.Instance?.EnlistedLord?.PartyBelongedTo;
                if (lordParty?.Party?.MapEvent != null || lordParty?.MapEvent != null)
                {
                    reason = "lord_party_in_map_event";
                    return true;
                }

                if (PlayerEncounter.Current != null && !PlayerEncounter.InsideSettlement)
                {
                    reason = "active_player_encounter";
                    return true;
                }
            }
            catch (Exception ex)
            {
                reason = "suppression_check_exception";
                ModLogger.Caught("DUTY", "ShouldSuppressNamedOrderEmission threw; failing closed", ex);
                return true;
            }

            return false;
        }

        private bool TryEmitNamedOrder(OrderActivity activity, List<DutyOpportunity> namedOrderCandidates, string emissionProfile)
        {
            if (activity == null || activity.ActiveNamedOrder != null)
            {
                return false;
            }

            if (namedOrderCandidates == null || namedOrderCandidates.Count == 0)
            {
                ModLogger.Expected("DUTY", "no_named_order_candidates",
                    "no named-order candidates produced for current duty profile",
                    new Dictionary<string, object>
                    {
                        { "profile", emissionProfile ?? activity.CurrentDutyProfile ?? "unknown" },
                        { "order_activity_active", true },
                        { "active_named_order", "none" }
                    });
                return false;
            }

            var rejected = new List<string>();
            foreach (var opp in namedOrderCandidates)
            {
                if (!TryResolveNamedOrderStorylet(opp, out var storylet, out var rejectionReason))
                {
                    rejected.Add($"{opp?.ArchetypeStoryletId ?? "unknown"}:{rejectionReason}");
                    continue;
                }

                ModLogger.Info("DUTY",
                    $"selected named-order storylet={storylet.Id} profile={emissionProfile ?? activity.CurrentDutyProfile} reason={opp.TriggerReason} candidates={namedOrderCandidates.Count}");
                EmitOpportunity(opp, storylet);
                return true;
            }

            ModLogger.Expected("DUTY", "no_named_order_storylet",
                "no eligible storylet found for any named-order candidate",
                new Dictionary<string, object>
                {
                    { "profile", emissionProfile ?? activity.CurrentDutyProfile ?? "unknown" },
                    { "named_order_candidate_count", namedOrderCandidates.Count },
                    { "active_named_order", "none" },
                    { "rejected_named_orders", string.Join(",", rejected.Take(8)) }
                });
            return false;
        }

        private static string ResolveEmissionProfile(OrderActivity activity)
        {
            var committed = activity?.CurrentDutyProfile ?? DutyProfileIds.Wandering;
            try
            {
                var lordParty = EnlistmentBehavior.Instance?.EnlistedLord?.PartyBelongedTo;
                var observed = DutyProfileSelector.Resolve(lordParty);
                if (!string.Equals(observed, committed, StringComparison.OrdinalIgnoreCase))
                {
                    ModLogger.Expected("DUTY", "profile_emission_observed_override",
                        $"named-order emission using observed profile {observed} over committed {committed}");
                }
                return observed;
            }
            catch (Exception ex)
            {
                ModLogger.Caught("DUTY", "ResolveEmissionProfile threw", ex);
                return committed;
            }
        }

        private bool TryResolveNamedOrderStorylet(DutyOpportunity opp, out Storylet storylet, out string rejectionReason)
        {
            storylet = null;
            rejectionReason = "unknown";

            if (opp == null)
            {
                rejectionReason = "candidate_null";
                return false;
            }

            if (opp.Shape != DutyOpportunityShape.ArcScale)
            {
                rejectionReason = "not_named_order";
                return false;
            }

            if (string.IsNullOrEmpty(opp.ArchetypeStoryletId))
            {
                rejectionReason = "missing_storylet_id";
                return false;
            }

            var direct = StoryletCatalog.GetById(opp.ArchetypeStoryletId);
            if (direct == null)
            {
                rejectionReason = "storylet_missing";
                return false;
            }

            if (!TryIsEligibleForEmit(direct, out rejectionReason))
            {
                return false;
            }

            storylet = direct;
            rejectionReason = string.Empty;
            return true;
        }

        private Storylet ResolveStoryletForOpportunity(DutyOpportunity opp)
        {
            if (opp == null)
            {
                return null;
            }

            if (opp.Shape == DutyOpportunityShape.ArcScale)
            {
                if (string.IsNullOrEmpty(opp.ArchetypeStoryletId))
                {
                    return null;
                }
                var direct = StoryletCatalog.GetById(opp.ArchetypeStoryletId);
                if (direct == null)
                {
                    return null;
                }
                return IsEligibleForEmit(direct) ? direct : null;
            }

            return PickEpisodicFromPool(opp.PoolPrefix);
        }

        private Storylet PickEpisodicFromPool(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
            {
                return null;
            }

            var eligible = new List<(Storylet s, float weight)>();

            foreach (var s in StoryletCatalog.All)
            {
                if (s?.Id == null || !s.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!IsEligibleForEmit(s))
                {
                    continue;
                }

                var w = s.WeightFor(null);
                foreach (var recent in _recentEmittedIds)
                {
                    if (string.Equals(recent, s.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        w *= RECENT_PENALTY_PER_HIT;
                    }
                }

                if (w > 0f)
                {
                    eligible.Add((s, w));
                }
            }

            if (eligible.Count == 0)
            {
                return null;
            }

            // Overlay preference: storylet ids with "__<culture>" suffix are culture overlays
            // for the base id preceding the suffix. When an overlay is eligible, drop its base
            // sibling from the pool so the culture flavor wins instead of a random mix.
            var overlaidBaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in eligible)
            {
                var idx = e.s.Id.IndexOf("__", StringComparison.Ordinal);
                if (idx > 0)
                {
                    overlaidBaseIds.Add(e.s.Id.Substring(0, idx));
                }
            }
            if (overlaidBaseIds.Count > 0)
            {
                eligible = eligible
                    .Where(e => !overlaidBaseIds.Contains(e.s.Id))
                    .ToList();
                if (eligible.Count == 0)
                {
                    return null;
                }
            }

            var total = 0f;
            foreach (var e in eligible)
            {
                total += e.weight;
            }

            var roll = MBRandom.RandomFloat * total;
            var acc = 0f;
            foreach (var e in eligible)
            {
                acc += e.weight;
                if (roll <= acc)
                {
                    return e.s;
                }
            }

            return eligible[eligible.Count - 1].s;
        }

        private bool IsEligibleForEmit(Storylet storylet)
        {
            return TryIsEligibleForEmit(storylet, out _);
        }

        private bool TryIsEligibleForEmit(Storylet storylet, out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (storylet == null || string.IsNullOrEmpty(storylet.Id))
            {
                rejectionReason = "storylet_null_or_missing_id";
                return false;
            }

            var nowHours = (int)CampaignTime.Now.ToHours;
            if (_cooldowns.LastFiredAt.TryGetValue(storylet.Id, out var last))
            {
                var cooldownHours = storylet.CooldownDays > 0
                    ? storylet.CooldownDays * 24
                    : DEFAULT_COOLDOWN_HOURS;
                if (nowHours - (int)last.ToHours < cooldownHours)
                {
                    rejectionReason = $"cooldown:{nowHours - (int)last.ToHours}/{cooldownHours}";
                    return false;
                }
            }

            if (!TriggerRegistry.Evaluate(storylet.Trigger, null))
            {
                rejectionReason = "trigger_failed";
                return false;
            }

            // Enlisted lord's culture gates RequiresCulture / ExcludesCulture. Empty lists =
            // no gate. StringIds are lowercase per the engine (empire / sturgia / vlandia /
            // battania / khuzait / aserai). Null lord or missing culture data is treated as
            // "no match" for RequiresCulture so overlays never fire pre-enlistment.
            if (storylet.RequiresCulture.Count > 0 || storylet.ExcludesCulture.Count > 0)
            {
                var lordCulture = EnlistmentBehavior.Instance?.EnlistedLord?.Culture?.StringId;
                if (string.IsNullOrEmpty(lordCulture))
                {
                    if (storylet.RequiresCulture.Count > 0)
                    {
                        rejectionReason = "required_culture_missing_lord_culture";
                        return false;
                    }
                }
                else
                {
                    if (storylet.RequiresCulture.Count > 0
                        && !storylet.RequiresCulture.Any(c =>
                            string.Equals(c, lordCulture, StringComparison.OrdinalIgnoreCase)))
                    {
                        rejectionReason = $"required_culture_mismatch:{lordCulture}";
                        return false;
                    }
                    if (storylet.ExcludesCulture.Any(c =>
                        string.Equals(c, lordCulture, StringComparison.OrdinalIgnoreCase)))
                    {
                        rejectionReason = $"excluded_culture:{lordCulture}";
                        return false;
                    }
                }
            }

            // Enlisted lord's personality traits gate RequiresLordTrait / ExcludesLordTrait.
            // Trait StringIds are PascalCase per DefaultTraits.cs (Mercy / Valor / Honor /
            // Generosity / Calculating). Trait level range is [-2, 2]; "requires" matches
            // lords with level > 0 (positive-trait lord), "excludes" matches the same and
            // fails (content hidden from positive-trait lord). Null lord = any trait gate
            // fails so trait-gated content never fires pre-enlistment.
            if (storylet.RequiresLordTrait.Count > 0 || storylet.ExcludesLordTrait.Count > 0)
            {
                var lord = EnlistmentBehavior.Instance?.EnlistedLord;
                if (lord == null)
                {
                    if (storylet.RequiresLordTrait.Count > 0)
                    {
                        rejectionReason = "required_trait_missing_lord";
                        return false;
                    }
                }
                else
                {
                    foreach (var traitId in storylet.RequiresLordTrait)
                    {
                        if (string.IsNullOrEmpty(traitId))
                        {
                            continue;
                        }
                        var trait = MBObjectManager.Instance.GetObject<TraitObject>(traitId);
                        if (trait == null || lord.GetTraitLevel(trait) <= 0)
                        {
                            rejectionReason = $"required_trait_missing:{traitId}";
                            return false;
                        }
                    }
                    foreach (var traitId in storylet.ExcludesLordTrait)
                    {
                        if (string.IsNullOrEmpty(traitId))
                        {
                            continue;
                        }
                        var trait = MBObjectManager.Instance.GetObject<TraitObject>(traitId);
                        if (trait != null && lord.GetTraitLevel(trait) > 0)
                        {
                            rejectionReason = $"excluded_trait:{traitId}";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private void EmitOpportunity(DutyOpportunity opp, Storylet storylet)
        {
            var director = StoryDirector.Instance;
            if (director == null)
            {
                ModLogger.Expected("DUTY", "no_director", "StoryDirector.Instance null at emit");
                return;
            }

            if (opp.Shape == DutyOpportunityShape.ArcScale)
            {
                EmitArcScaleModal(opp, storylet, director);
            }
            else
            {
                EmitEpisodicLog(opp, storylet, director);
            }
        }

        private void EmitArcScaleModal(DutyOpportunity opp, Storylet storylet, StoryDirector director)
        {
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
                ModLogger.Expected("DUTY", "arc_buildmodal_null", "BuildModal returned null for arc-scale storylet",
                    new Dictionary<string, object>
                    {
                        { "storylet_id", storylet.Id }
                    });
                return;
            }

            director.EmitCandidate(new StoryCandidate
            {
                SourceId = "duty.arcscale",
                CategoryId = string.IsNullOrEmpty(storylet.Category) ? "duty" : storylet.Category,
                ProposedTier = StoryTier.Modal,
                SeverityHint = 0.50f,
                Beats = { StoryBeat.OrderPhaseTransition },
                Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                ChainContinuation = true,
                EmittedAt = CampaignTime.Now,
                InteractiveEvent = evt,
                RenderedTitle = storylet.Title,
                RenderedBody = storylet.Setup,
                StoryKey = storylet.Id
            });

            var emittedAt = CampaignTime.Now;
            _cooldowns.LastFiredAt[storylet.Id] = emittedAt;
            var profile = OrderActivity.Instance?.CurrentDutyProfile ?? "unknown";
            _cooldowns.RecordNamedOrderEmission(storylet.Id, profile, emittedAt);
            IncrementProfileCount("arcscale");

            ModLogger.Info("DUTY",
                $"emitted arcscale storylet={storylet.Id} reason={opp.TriggerReason}");
        }

        private void EmitEpisodicLog(DutyOpportunity opp, Storylet storylet, StoryDirector director)
        {
            director.EmitCandidate(new StoryCandidate
            {
                SourceId = "duty.episodic",
                CategoryId = string.IsNullOrEmpty(storylet.Category) ? "duty" : storylet.Category,
                ProposedTier = StoryTier.Log,
                Relevance = new RelevanceKey { TouchesEnlistedLord = true },
                EmittedAt = CampaignTime.Now,
                RenderedTitle = storylet.Title,
                RenderedBody = storylet.Setup,
                StoryKey = storylet.Id
            });

            _cooldowns.LastFiredAt[storylet.Id] = CampaignTime.Now;
            var profile = OrderActivity.Instance?.CurrentDutyProfile ?? "unknown";
            IncrementProfileCount(profile);

            // Track recent history for weighted-diversity picker.
            _recentEmittedIds.Enqueue(storylet.Id);
            while (_recentEmittedIds.Count > RECENT_HISTORY_SIZE)
            {
                _recentEmittedIds.Dequeue();
            }

            // Log-tier storylets don't render a modal, so single-option effects auto-apply.
            if (storylet.Options?.Count == 1)
            {
                var mainOpt = storylet.Options.FirstOrDefault(o => o.Id == "main");
                if (mainOpt?.Effects != null)
                {
                    EffectExecutor.Apply(mainOpt.Effects, null);
                }
            }

            ModLogger.Info("DUTY",
                $"emitted episodic storylet={storylet.Id} reason={opp.TriggerReason}");
        }

        private void IncrementProfileCount(string profile)
        {
            if (string.IsNullOrEmpty(profile))
            {
                return;
            }
            _sessionEmissionsByProfile.TryGetValue(profile, out var count);
            _sessionEmissionsByProfile[profile] = count + 1;
        }

        private void LogDailyCountsIfDue()
        {
            var nowHour = (int)CampaignTime.Now.ToHours;
            if (nowHour - _lastDailyCountReportHourTick < DAILY_COUNT_REPORT_INTERVAL_HOURS)
            {
                return;
            }
            _lastDailyCountReportHourTick = nowHour;

            if (_sessionEmissionsByProfile.Count == 0)
            {
                ModLogger.Info("DUTY", "daily_counts: no emissions in the last 24h");
                return;
            }

            var parts = _sessionEmissionsByProfile
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => $"{kvp.Key}={kvp.Value}");
            ModLogger.Info("DUTY", "daily_counts: " + string.Join(" ", parts));

            _sessionEmissionsByProfile.Clear();
        }

        private void LogHeartbeatIfDue()
        {
            var nowHour = (int)CampaignTime.Now.ToHours;
            if (nowHour - _lastHeartbeatHourTick < HEARTBEAT_INTERVAL_HOURS)
            {
                return;
            }
            _lastHeartbeatHourTick = nowHour;

            var isEnlisted = EnlistmentBehavior.Instance?.IsEnlisted == true;
            var hasSnap = EnlistedCampaignIntelligenceBehavior.Instance?.Current != null;
            var tracked = _cooldowns?.LastFiredAt?.Count ?? 0;
            ModLogger.Info("DUTY",
                $"heartbeat: enlisted={isEnlisted} snapshot={hasSnap} tracked={tracked}");
        }
    }
}
