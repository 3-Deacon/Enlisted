# AGENTS.md — Features/Content

> Parent: [../../../AGENTS.md](../../../AGENTS.md)
> Handbook: [../../../docs/Features/Content/](../../../docs/Features/Content/)

This file owns rules for the Content subsystem: storylet backbone, event delivery, scripted-effects catalog, and the ModalEventBuilder pipeline. Source-of-truth for any file under `src/Features/Content/`.

---

## Storylet backbone

Content is authored as **storylets** (`ModuleData/Enlisted/Storylets/*.json`), not as legacy `EventDefinition` JSON. State lives in `QualityStore` (typed numeric, global or per-hero) and `FlagStore` (named booleans with expiry). Durable player engagements are `Activity` subclasses with phases and intent-biased storylet pools. Effects are either named scripted effects from `ModuleData/Enlisted/Effects/scripted_effects.json` (preferred — the seed catalog has 22 entries) or registered primitives (`quality_add`, `set_flag`, `give_gold`, etc.). Triggers are named C# predicates resolved by `TriggerRegistry`. Reference: [docs/Features/Content/storylet-backbone.md](../../../docs/Features/Content/storylet-backbone.md) (living doc — seed catalogs, trigger/slot/primitive lists, save-definer offsets, pitfalls).

---

## Save-definer offsets (cross-ref)

Storylet/content classes claim offsets in [src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs](../../Mod.Core/SaveSystem/EnlistedSaveDefiner.cs). See [../../Mod.Core/SaveSystem/AGENTS.md](../../Mod.Core/SaveSystem/AGENTS.md) for the full offset table and the class-vs-enum disjointness rule. Spec 0 owns class offsets 40-44 and enum offsets 82-83.

---

## StoryDirector routing

Modal events go through `StoryDirector.EmitCandidate(...)`, not `EventDeliveryManager.Instance.QueueEvent(...)` directly. The Director gates Modal firing with a 5-day in-game floor + 60s wall-clock floor + per-category cooldown, and writes non-Modal items to the news feed as accordion entries.

```csharp
// CORRECT - gated by pacing, supports deferral + accordion routing
StoryDirector.Instance?.EmitCandidate(new StoryCandidate
{
    SourceId = "myfeature.context",
    CategoryId = "myfeature.subcategory",
    ProposedTier = StoryTier.Modal,
    SeverityHint = 0.5f,
    Beats = { StoryBeat.OrderPhaseTransition },
    Relevance = new RelevanceKey { TouchesEnlistedLord = true },
    EmittedAt = CampaignTime.Now,
    InteractiveEvent = evt,
    RenderedTitle = evt.TitleFallback,
    RenderedBody = evt.SetupFallback,
    StoryKey = evt.Id
});
// Fallback for when Director isn't registered yet (early boot only):
// EventDeliveryManager.Instance?.QueueEvent(evt);
```

Use `ChainContinuation = true` for player-opted continuations (promotions, bag checks, chain events) so the in-game floor + category cooldown don't defer them (60s wall-clock still applies). Spec: [docs/superpowers/specs/archive/2026-04-18-event-pacing-design.md](../../../docs/superpowers/specs/archive/2026-04-18-event-pacing-design.md).

---

## Pitfall: Direct QueueEvent bypasses pacing

Calling `EventDeliveryManager.Instance.QueueEvent(evt)` directly bypasses
StoryDirector pacing (no floor, no cooldown, no deferral). The only
legitimate direct-call sites are (a) Director-null fallbacks inside a
migrated caller, (b) the debug tool at `src/Debugging/Behaviors/DebugToolsBehavior.cs:141`,
and (c) the Director's own internal `Route()`. Everything else must use
`StoryDirector.Instance?.EmitCandidate(...)` — see StoryDirector routing above.

---

## Pitfall: Unknown scripted-effect id

Inventing a new scripted-effect id without adding it to
`ModuleData/Enlisted/Effects/scripted_effects.json`. Phase 12 blocks
unknown `apply` values. Prefer reusing the seed catalog (`rank_xp_minor`,
`lord_relation_up_*`, `scrutiny_down_*`, etc.) over minting one-off names.

---

## Pitfall: Scripted-effect cycles

Authoring a scripted effect (in `ModuleData/Enlisted/Effects/scripted_effects.json`)
whose body references another scripted effect that eventually references it
back. `EffectExecutor` caps expansion at depth 8 and logs
`Expected("EFFECT", "scripted_depth_limit", ...)` — the chain no-ops at the
cap, but a cyclic catalog is a JSON bug worth surfacing in the session log.

---

## Pitfall: CampaignBehaviorManager empty at OnGameStart

`Campaign.Current.CampaignBehaviorManager` returns an empty collection at
`OnGameStart` — behaviors have been registered via `AddBehavior` but not yet
published to the Campaign. Read `campaignStarter.CampaignBehaviors` instead
(decompile: `TaleWorlds.CampaignSystem/CampaignGameStarter.cs:19` — the
starter holds the list directly). A diagnostic that iterates
`Campaign.Current.GetCampaignBehaviors<T>()` at OnGameStart NREs in
`Enumerable.OfType` or prints zero behaviors, depending on the call shape.

---

## Pitfall: Catalogs init after OnGameStart

Content catalogs (Storylet, Event, ActivityType, Scripted
Effects) initialize AFTER `OnGameStart`, during their owning behaviors'
`OnSessionLaunchedEvent` handlers. A diagnostic that reads catalog counts
at OnGameStart reports `0 loaded` even when load ultimately succeeds.
Defer diagnostic reads to `OnSessionLaunchedEvent` (see
`RuntimeCatalogStatusMarkerBehavior` for the pattern).

---

## Pitfall: News-feed throttle silent at >4x speed

`OrdersNewsFeedThrottle.TryClaim()` is designed to reject at extreme
fast-forward speeds — news feed entries at extreme fast-forward would flood.
This is intentional silence, not a bug. When smoke-testing the Orders surface,
run at 1x–4x to see news output; tick-driven `ModLogger` entries (DRIFT,
DUTYPROFILE heartbeats, PATH heartbeats) still log at any speed.

API note: `Campaign.Current.SpeedUpMultiplier` is a `float` property
(default `4f`). The implementation tests only this property:
```csharp
// CORRECT — matches OrdersNewsFeedThrottle
if (Campaign.Current?.SpeedUpMultiplier > 4f) { /* suppress */ }
```
Do NOT add a `TimeControlMode == StoppableFastForward` guard — that would miss
the `UnstoppableFastForward` case and contradict the implementation.

---

## Pitfall: Plan-vs-codebase API drift

Plans in `docs/superpowers/plans/` can drift before a plan is fully executed. Spec 2 saw this repeatedly (`ContentOrchestrator` listed with zero migration sites; Tasks 19/20/21 required audit-expansion). Before implementing a multi-file task from an older plan, grep prescribed file paths and symbol names and confirm they still exist with the cardinality the plan assumed.

---

## See also

- [docs/Features/Content/storylet-backbone.md](../../../docs/Features/Content/storylet-backbone.md) — living reference for the Spec 0 backbone
- [../../Mod.Core/SaveSystem/AGENTS.md](../../Mod.Core/SaveSystem/AGENTS.md) — full offset table
- [../../../ModuleData/Enlisted/AGENTS.md](../../../ModuleData/Enlisted/AGENTS.md) — JSON authoring rules for storylet/event content
