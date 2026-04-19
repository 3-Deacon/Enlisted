# Event Meaning System — Design Spec

**Date:** 2026-04-19
**Status:** Draft.
**Scope:** Give authored events *meaning* under the StoryDirector that shipped 2026-04-18. Introduces a context taxonomy, a variant selector, and three cooperating depth layers (identity tags, lord memory, consequence arcs) that together make events read differently based on who the player is, which lord they serve, and where in a narrative arc they sit. Culls the event library from ~557 one-shots down to ~200 intentional events in a single authoring pass. One unified spec, three sequential implementation plans.
**Companion work (prerequisites):** three small plumbing fixes identified during design review — see §5.

---

## 1. Problem

The StoryDirector correctly gates *when* events fire (5-day in-game floor, 60s wall-clock, per-category cooldown, relevance filter, severity classifier). The Event Pacing spec left *authoring* explicitly out of scope. Meaning was deferred.

The current content surface:

- **557 interactive events across 18 JSON files.** Heavy thematic duplication: 84 escalation-threshold events (same five "you're in trouble" beats at every tier); 44 post-battle moral incidents; 28 illness-onset variants; overlapping supply systems (`pressure_arc` + `company.crisis`).
- **Context-scoping is a stub.** Event JSONs carry a `context` field with only four values: `leaving_battle`, `during_siege`, `leaving_settlement`, and `Any`. The `Any` bucket — the majority — fires with no situational filter. The campaign's richer situations (march, camp-night, court audience, garrison, patrol, winter quarters, pay muster, assault) are not expressible.
- **Identity is invisible.** The promotion system stamps `disciplinarian` / `merciful` / `aggressive` / `cautious` tags on the player. Zero events ever read those tags back. The player becomes someone the world never acknowledges.
- **Lord relationships are stateless.** The same event fires the same way under a cruel lord vs. a kind lord. Betraying a lord once does not change any future interaction with that lord. There is no per-lord memory.
- **Arcs exist only where someone hand-wired `triggers_event`.** Desertion, mutiny, and T2–T9 promotion are multi-act. Escalation thresholds fire as independent one-shot toasts. Scrutiny 3→4→5→6→7 in 20 in-game days produces five disconnected "you're in trouble" events with no sense of progression, complication, or climax.
- **Beat catalog has gaps.** `LordCaptured`, `LordKilled`, `WarDeclared`, `PeaceSigned` exist in the `StoryBeat` enum. Zero events catch them. The Director listens for world drama that no one writes.

The resulting feeling: the Director's pacing is sound, but the content it gates feels generic, random, and repetitive. No sense that *this event* is about *this player* serving *this lord* in *this situation* as part of *this arc*. CK3 and Viking Conquest do that; Enlisted does not yet.

## 2. Goals

- **Depth per event over breadth of event count.** The surviving library is ~100 events, not ~200 — roughly 85% of them are arc acts, the rest are flat flavor one-shots. Each surviving event carries 4–8 variant reads (identity, lord memory, arc act, state tier, trait tier) and every one earns its slot. Meaning concentrates in fewer buckets, each doing more work.
- **Arcs are the default authoring unit for any system with a through-line.** ~12 arcs cover every pressure, progression, and relationship track in the campaign. Flat one-shots survive only for genuinely self-contained flavor beats (post-battle moral choice, camp-night reflection, town/village ambient, quiet stretch).
- Every surviving event declares a specific **context**. `Any` is retired.
- Variant readers span five axes: identity tag, lord memory marker, arc act, gameplay state tier, native trait tier.
- **One authoring pass** culls ~557 → ~93 and rewrites every survivor into schema v2 — variants, gates, arc declarations all authored in the same pass.
- Lord-memory and arc hooks layer onto surviving events in Plans 2 and 3 — no third or fourth full-library rewrite.
- Pre-existing plumbing gaps in the Director surfaced during design review are fixed as prerequisites, not carried into the new layers.

## 3. Non-goals

- Not a rewrite of the Director's gating, relevance, or severity logic. Context filtering and variant selection run as **additive layers** around the existing Director.
- Not a broader "event meaning canonicalization" refactor of the entire StoryCandidate → DispatchItem → consumer contract. That work, if warranted, is a separate spec. This spec does define new contracts *at the boundaries it touches* (§7) but does not re-plumb downstream consumers broadly.
- Not adding new event-delivery channels. Modal goes through `EventDeliveryManager`; non-modal goes through `EnlistedNewsBehavior` as today.
- Not adding new gameplay mechanics (no new traits, no new reputation axes, no new save-defined systems beyond `LordMemoryStore` and `ArcTracker`).
- Not adding naval infrastructure. The mod already supports the War Sails DLC as a first-class target: `NavalBattleShipAssignmentPatch` fixes enlisted-player crashes in naval battles (`src/Mod.GameAdapters/Patches/NavalBattleShipAssignmentPatch.cs`); `CampOpportunityGenerator` already gates opportunities with `notAtSea` / `atSea` booleans (`:970–984`); `CampRoutineProcessor.IsPartyAtSea()` reads native `MobileParty.IsCurrentlyAtSea` (`:410–419`). This spec treats `at_sea` as a first-class context in the taxonomy (§8) and suppresses land-only events on water, mirroring the existing opportunity-gating pattern. Authoring a naval event library — and completing the half-wired naval tokens (`{SHIP_NAME}`, `{BOATSWAIN_NAME}`, `{NAVIGATOR_NAME}`, `{DESTINATION_PORT}`, `{DAYS_AT_SEA}` at `EventDeliveryManager.cs:2590–2660`, which currently return hardcoded fallback strings not real ship/port state) — is **deferred** to a follow-up content phase after Plan 1 ships. Plan 1's cull rewrites surviving events with explicit context declarations; any event that doesn't include `at_sea` in its contexts is land-only and will not fire on water.

## 4. Core principles

1. **Context is declared, not inferred.** Every event states the 1+ situations it is eligible for. Emit-time filtering drops candidates whose declared contexts don't match the live campaign state. No fuzzy substring matching, no heuristic classification.
2. **Variants stack, they don't fork.** One event JSON can read differently based on identity, lord memory, and arc act — but the event still has one canonical `id`, one `beats` set, one `severity_hint`. Variants only affect *rendered text* and *option availability*, never severity or tier.
3. **Vocabularies are closed enums.** Contexts, identity tags, lord-memory markers, and arc names all come from finite, documented enums. Authors pick; they don't invent. The validator enforces.
4. **Fall back to base silently.** If no variant condition matches, the event renders its base `setup` / `text`. Variant failure is never a surfaced error.
5. **One authoring pass, three feature plans.** The cull happens once, in Plan 1. Plans 2 and 3 layer lord-memory and arc hooks *onto* surviving events — they do not touch JSONs the cull already deleted.
6. **Close the Director's plumbing before stacking.** Three pre-existing Director gaps (SeverityClassifier doc, EventDeliveryManager save discipline, muster substring matching) are fixed as prerequisites so the new layers don't inherit them.
7. **Every TaleWorlds API this spec names has been verified against the v1.3.13 decompile at `../Decompile/`.** No web search, no Context7, no training-knowledge guesses. Cited call sites for `Hero.GetTraitLevel`, `DefaultTraits.*`, `TraitLevelingHelper.OnIncidentResolved`, `ChangeRelationAction.ApplyPlayerRelation`, `MobileParty.IsMoving`/`BesiegerCamp`/`CurrentSettlement`/`IsCurrentlyAtSea`, `MapEvent.IsSiegeAssault`, `CampaignTime.GetHourOfDay`/`GetSeasonOfYear`, `CampaignEvents.HourlyTickEvent`/`SettlementEntered`/`MapEventEnded`/`OnSiegeEventStartedEvent`, `MBObjectManager.GetObject<ItemObject>`, `EquipmentIndex`, `Hero.BattleEquipment[]` — all in scope at v1.3.13. Implementation plans cite the decompile path + line when calling any of these. The `IsCurrentlyAtSea` predicate resolves against the War Sails DLC at runtime; when the DLC isn't loaded the property returns `false` and the `at_sea` context is dormant (no special-case needed in the mod).

## 5. Prerequisites

Three small fixes to land **before or alongside Plan 1**. Each is independently scoped; all are compatible with the spec but were identified as load-bearing during design review.

### 5.1 SeverityClassifier doc/code mismatch

`src/Features/Content/SeverityClassifier.cs:9` docstring states the candidate is "bounded by the strictest BeatMaxTier among the candidate's beats." The code at lines 66–77 does the opposite — it picks the **most permissive** cap:

```csharp
StoryTier tierCap = StoryTier.Log;
foreach (var beat in c.Beats) {
    if (BeatMaxTier.TryGetValue(beat, out var cap) && (int)cap > (int)tierCap) {
        tierCap = cap;
    }
}
```

All current emitters pass single-beat sets, so min == max and no runtime behavior differs. The bug is dormant but misleading.

**Fix:** Reword the docstring to "bounded by the most permissive BeatMaxTier among the candidate's beats." The current behavior is the one we want (so a Modal-eligible beat does not get demoted by an accompanying minor beat); only the comment is wrong.

### 5.2 EventDeliveryManager queue persistence

`src/Features/Content/StoryDirector.cs:49–55` persists `_lastModalDay`, `_lastModalUtcTicks`, `_categoryCooldowns`, and `_deferredInteractive`. `src/Features/Content/EventDeliveryManager.cs:55–59` persists nothing — the pending event queue is explicitly transient.

Flow: `Route()` calls `delivery.QueueEvent(evt)` then synchronously writes `_lastModalDay = today`. If the player saves between queue-add and event-show (UI not yet ready), on reload: the pacing counter is persisted, the queue is empty. The event is lost and the floor is burned.

**Fix:** Persist `EventDeliveryManager._pendingEvents` via a `SyncData` entry keyed by event `Id` (the event catalog is authored content — on load, resolve IDs back to `EventDefinition` instances rather than serializing the full object graph). Register the container in `EnlistedSaveDefiner`. Keep the queue bounded (same cap as `_deferredInteractive`: 32) so a pathological pile-up can't grow unbounded in save.

### 5.3 Typed muster filtering

`src/Features/Enlistment/Behaviors/MusterMenuHandler.cs:3691–3696` counts battles by substring-matching on `HeadlineKey` and `Category`:

```csharp
item.Category?.Contains("battle") == true ||
item.HeadlineKey?.ToLowerInvariant().Contains("battle") == true ||
item.HeadlineKey?.ToLowerInvariant().Contains("victory") == true ||
item.HeadlineKey?.ToLowerInvariant().Contains("defeat") == true
```

This breaks silently on key renames. It is also the pattern Plan 3's arc system will need to read cleanly — substring matching there would compound the fragility.

**Fix:** Add a typed field to `DispatchItem` carrying the originating `StoryBeat` (or a compact `DispatchKind` enum if that's cleaner than exposing `StoryBeat` through the news layer). Update `StoryDirector.WriteDispatchItem` (`StoryDirector.cs:308`) to populate it from the candidate's `Beats`. Rewrite the muster count to `item.Beats?.Contains(StoryBeat.LordMajorBattleEnd) == true || item.Beats?.Contains(StoryBeat.PlayerBattleEnd) == true`. Same pattern for `EnlistedNewsBehavior.BuildEventHeadline` at `:3629` — switch on typed fields, not `eventId.Contains("dice")`.

---

## 6. Architecture

The Director already gates *when* events fire. This design adds two cooperating layers — one between emit and route (Context Filter), one between route and render (Variant Selector).

```
 SOURCE EMITS                       ┌───────────── NEW LAYERS ─────────────┐
 ────────────▶  StoryCandidate ───▶ │ Context ─▶ Relevance ─▶ Severity ─▶  │ ──▶ MODAL / NEWS
                                    │  Filter     (existing)   Classifier  │     (existing delivery)
                                    └──────────────────────┬───────────────┘
                                                           ▼
                                                    Variant Selector
                                                    (reads identity tags,
                                                     lord memory, arc act;
                                                     writes rendered text
                                                     and gates options)
```

- **Context Filter** — new service, runs immediately after `EmitCandidate` and before the existing `RelevanceFilter`. Queries a `ContextDetector` for the current live context set; drops candidates whose declared contexts do not overlap the live set. Candidates whose JSON still declares `Any` (transitional, during the cull) pass through unchanged. In Plan 3 this same layer gains a second gate: candidates whose event declares an `arc` block only pass when the declared `(arcId, actId)` matches `ArcTracker.CurrentAct(arcId)`. Free-firing events (no `arc` declaration) are unaffected.
- **Variant Selector** — new service, runs at render time after the Director decides to route a candidate. Reads the three depth services (`PlayerIdentity`, `LordMemoryStore`, `ArcTracker`), selects the highest-specificity matching variant of the event's text fields, populates `StoryCandidate.RenderedTitle` / `RenderedBody`, and filters options via declared gates. Falls through to base text when no variant matches.
- **Three depth services** — `PlayerIdentity` (ships Plan 1), `LordMemoryStore` (ships Plan 2), `ArcTracker` (ships Plan 3). Each exposes a single read-only query surface the Variant Selector consumes. The selector is written in Plan 1 with null-safe handles for the later services so Plans 2 and 3 can plug in without modifying the selector.

## 7. Contracts at the layer boundaries

The review surfaced that the Director's downstream contracts (StoryCandidate → DispatchItem → muster/news consumers) are informal and partly string-based today. This spec does not refactor those contracts broadly, but it does define the contracts **at the surfaces it touches**.

### 7.1 StoryCandidate fields the Variant Selector writes

- `RenderedTitle` — the title after variant selection. Never null after selector runs.
- `RenderedBody` — the setup text after variant selection. Never null after selector runs.
- `SelectedVariantKeys` (new field) — the list of `if_*` conditions that matched, in specificity order. Debug / logging use only.

The Variant Selector never writes `ProposedTier`, `SeverityHint`, `Beats`, `Relevance`, `CategoryId`, or `SourceId`. Severity and tier are the Director's domain; context and variants do not influence them.

### 7.2 DispatchItem extensions (§5.3 prerequisite)

`DispatchItem` gains one field: `Beats` (typed) or `Kind` (compact enum derived from `Beats`). Choice of name/shape is an implementation detail for the prerequisite PR; the contract is that downstream consumers (muster, news) read typed values instead of substring-matching strings.

The `RenderedBody` already on `StoryCandidate` flows into `DispatchItem` as a new `Body` field. `RenderedBody` stops being dead weight for observational candidates.

### 7.3 Semantic predicates (§5.3 follow-through)

Consumers that previously read `Severity >= 2` to mean "headline" (e.g. `EnlistedMenuBehavior.cs:1557`) gain a semantic helper: `DispatchItem.IsHeadline` (returns `Tier == Headline`). The magic-number read stays compiling during the transition but is scheduled for removal in Plan 1. The tier→int mapping at `StoryDirector.cs:316` becomes a derived presentation concern, not a semantic contract.

### 7.4 Effect vocabulary and application contract

Events affect gameplay by writing typed fields on their option's `effects` block. The current codebase supports 30+ effect types applied by `EventDeliveryManager.ApplyEffects` (`EventDeliveryManager.cs:518`). This spec closes the vocabulary and enforces it.

**Closed effect enum (authoritative list).** Plan 1 adds a new documentation artifact `docs/Features/Content/effect-vocabulary.md` that enumerates every supported effect key, its type, its scale convention, and its handler. The validator gains a phase that rejects unknown keys in the `effects` block. Additions to the enum happen by doc-first PR + validator update, not by authoring new keys directly.

Effect categories today (full enumeration lives in `effect-vocabulary.md`; this table is the index):

| Category | Keys |
|:--|:--|
| XP / skills | `skillXp`, `dynamicSkillXp`, `traitXp`, `troopXp` |
| Reputation | `lordRep`, `renown`, `retinueLoyalty` |
| Escalation | `scrutiny`, `medicalRisk` |
| Resource | `gold`, `foodLoss`, `companyNeeds` |
| Character | `hpChange`, `applyWound`, `illnessOnset`, `injuryOnset`, `beginTreatment`, `worsenCondition` |
| Party / troops | `troopLoss`, `troopWounded`, `retinueGain`, `retinueLoss`, `retinueWounded` |
| Baggage | `grantTemporaryBaggageAccess`, `baggageDelayDays`, `randomBaggageLoss`, `bagCheckChoice` |
| Gear grant | `grantGear` *(new in Plan 3 — gated to `heirloom_earned` arc's Presentation act; see §12.3)* |
| Lifecycle | `promotes`, `triggersDischarge`, `chainEventId` |
| Identity | `characterTag` *(new in Plan 1 — see §10.1)*, `writeLordMemory` *(new in Plan 2 — see §11.2)* |

**Scale conventions** (documented in `effect-vocabulary.md`, referenced here):

- `scrutiny`: 0–100 integer, deltas typically ±1 to ±10. Sentinel-scaled values like `10000000` seen in legacy JSONs mean "move to next tier boundary"; these are a legacy pattern being retired during the Plan 1 cull in favor of explicit integer deltas.
- `medicalRisk`: 0–5 integer tier.
- `lordRep`: 0–100 internal, mapped to `Honor` trait via `IncidentEffectTranslator`.
- `gold`: unbounded signed integer; routes through `GiveGoldAction.ApplyBetweenCharacters`, never raw `ChangeHeroGold`.
- `troopLoss` / `troopWounded` / `retinueGain` / etc.: absolute counts, not percentages.
- `companyNeeds`: dictionary with keys `Readiness`, `Supplies`, `Equipment`, `Rest` (morale key deprecated — see §16).
- `grantGear`: object `{ "itemId": "<ItemObject.StringId>", "slot": "Weapon0|Weapon1|Weapon2|Weapon3|Head|Body|Leg|Gloves|Cape|Horse|HorseHarness", "replaceExisting": true|false }`. Slot values map to the v1.3.13 `EquipmentIndex` enum (decompile-verified at `TaleWorlds.Core.EquipmentIndex:3–27`; battle equipment excludes `ExtraWeaponSlot`). Resolves the `itemId` via `MBObjectManager.Instance.GetObject<ItemObject>(stringId)`. Writes through the existing mod plumbing at `src/Features/Equipment/Behaviors/EquipmentManager.cs` (which already uses `hero.BattleEquipment[EquipmentIndex.X] = new EquipmentElement(item)` for the enlistment equipment loop); Plan 3 adds a single new method there that takes `(EquipmentIndex slot, ItemObject item, bool replaceExisting)`. Authored only on arc-declared options (validator enforces the `heirloom_earned:presentation` arc-act gate); bypassing that gate is a validator hard-fail.

**Application order** is the declaration order in the source file (top-to-bottom in `EventDeliveryManager.ApplyEffects`). Effects are independent — no effect reads the outcome of another in the same option. Option gates apply **before** effects; if every option's gate fails, the event aborts pre-emit and no effects fire.

**Fallback discipline.** Each effect handler is null-safe: if its target service is unavailable (`Hero.MainHero`, `EscalationManager`, `CompanySupplyManager`, etc.), the effect is skipped with a `ModLogger.Expected` info log. No `ModLogger.Surfaced` from effect-side fallbacks — effect failures are not player-facing errors.

**Deprecated effect keys.** Listed in `effect-vocabulary.md` with their deprecation date. Plan 1's cull removes all usage of deprecated keys; the validator hard-fails them post-cull. Current deprecations: the `morale` key of `companyNeeds` (removed 2026-01-11; see `CLAUDE.md` deprecated-systems section).

## 8. Context taxonomy

Closed enum of 14 contexts. Authors pick 1+ per event. Events without any context declared fail validation.

| Context | Active when |
|:--|:--|
| `march` | Party moving map-to-map, no active siege/battle |
| `battle_aftermath` | Within 3 in-game hours after a battle's end event |
| `siege_camp` | Parked at a besieged settlement, no active assault |
| `assault` | Active siege assault in progress |
| `garrison` | Enlisted lord is garrisoning / holding a settlement |
| `court_audience` | Inside own-faction settlement with lord present, no active threat |
| `town_visit` | Inside non-hostile town, not in court |
| `village_visit` | Inside a village |
| `patrol` | On an active patrol order |
| `pursuit` | On an active intercept/pursuit order against a named target |
| `winter_quarters` | Winter season AND parked at own-faction settlement 5+ in-game days |
| `pay_muster` | 12-day muster cycle firing (wages paid or denied this tick) |
| `camp_night` | Parked on the map (not besieging), between 20:00 and 06:00 |
| `promotion_window` | Eligible for next-rank advance AND lord is present |
| `at_sea` | `MobileParty.IsCurrentlyAtSea == true` (War Sails DLC; gracefully `false` when DLC not loaded — events declaring `at_sea` simply never fire in that case). Mirrors the existing opportunity-gating pattern at `CampOpportunityGenerator.cs:970–984`. |

Multiple contexts can be simultaneously active (e.g. `march` + `camp_night`). An event declaring any one of the live contexts passes the filter.

**Land/sea mutual exclusion.** The default policy for the Plan 1 cull is: an event whose `contexts` list does **not** include `at_sea` is treated as land-only and the Context Filter drops it while `at_sea` is active. This matches the mod's existing `notAtSea` default on opportunities (most content is land-content) and ensures we don't ship land flavor firing during naval play. Naval-specific events declare `at_sea` (optionally alongside `march` or `battle_aftermath` for naval-battle-adjacent beats). Amphibious events that should fire in either state declare both `at_sea` and the relevant land context explicitly.

### 8.1 ContextDetector service

New service under `src/Features/Content/`. Subscribes to `CampaignEvents.HourlyTickEvent` and computes the active context bitmask from campaign state, caching the result. Invalidates on the v1.3.13 state-change events that could flip a context: `CampaignEvents.SettlementEntered`, `CampaignEvents.OnSettlementLeftEvent`, `CampaignEvents.MapEventStarted`, `CampaignEvents.MapEventEnded`, `CampaignEvents.OnSiegeEventStartedEvent`, `CampaignEvents.OnSiegeEventEndedEvent`, mod-specific order set/complete fires, and a season-change check on daily tick.

**State sources (v1.3.13 decompile-verified; existing-mod-code-verified where noted):**
- `march`, `garrison`, `camp_night` — `MobileParty.IsMoving`, `MobileParty.CurrentSettlement`
- `siege_camp`, `assault` — `MobileParty.BesiegerCamp`, `MapEvent.IsSiegeAssault`
- `battle_aftermath` — cache `CampaignTime.Now` inside the `MapEventEnded` handler; expire after 3 in-game hours
- `camp_night` hour range — `CampaignTime.Now.GetHourOfDay`
- `winter_quarters` — `CampaignTime.Now.GetSeasonOfYear == Seasons.Winter`
- `court_audience` — `Settlement.OwnerClan == EnlistedLord.Clan` **AND** `EnlistedLord.CurrentSettlement == Hero.MainHero.CurrentSettlement` (lord present in the player's settlement, not any lord generically)
- `pay_muster`, `promotion_window` — mod-specific, queried from `EnlistmentBehavior`
- `at_sea` — `MobileParty.IsCurrentlyAtSea` from the native party API. Reuse the exact predicate used by the existing mod: `CampRoutineProcessor.IsPartyAtSea()` at `src/Features/Camp/CampRoutineProcessor.cs:410–419`, with the "in settlement or besieging → treat as land" override from `CampOpportunityGenerator.cs:969–974`. When War Sails isn't loaded, `IsCurrentlyAtSea` is always `false` and this context is simply never active.

```csharp
public sealed class ContextDetector
{
    public ContextSet Current { get; }
    public bool IsActive(Context c);
    public bool MatchesAny(IReadOnlyCollection<Context> declared);
}
```

`ContextSet` is a `[Flags]` enum. Query cost is a bitwise AND — cheap at emit time.

## 9. Event schema evolution

All new fields are optional. Existing events without the new fields still load; validator warns on missing `contexts` during the cull, hard-fails after the cull.

```json
{
  "id": "mi_wounded_enemy",

  "contexts": ["battle_aftermath"],
  "beats": ["LordMajorBattleEnd"],
  "severity_hint": 0.45,
  "weight": 1.0,

  "titleId": "mi_wounded_enemy_title",
  "title": "The Wounded Enemy",
  "setupId": "mi_wounded_enemy_setup",
  "setup": "A wounded enemy soldier lies in the mud.",

  "setup_variants": [
    { "if_tag": "merciful",
      "text": "A wounded enemy soldier lies in the mud. You've always struggled with cruelty." },
    { "if_tag": "disciplinarian",
      "text": "A wounded enemy soldier lies in the mud. But mercy is a luxury you can't afford." },
    { "if_lord_memory": "betrayed_once",
      "text": "A wounded enemy soldier lies in the mud. Like the last man you spared." },
    { "if_arc_act": "scrutiny_crisis:crisis",
      "text": "A wounded enemy lies in the mud. Everything feels like a sign now." }
  ],

  "options": [
    {
      "id": "help",
      "text": "Help him back to camp",
      "tooltip": "Mercy. Scrutiny +1, Lord rep -2. (50% detected)",

      "gate": {
        "requires_tag": null,
        "forbid_tag": "disciplinarian",
        "requires_lord_memory": null,
        "requires_arc_act": null
      },

      "risk_shown": 0.5,
      "effects": { "traitXp": { "Mercy": 10 }, "scrutiny": 3000000 },

      "result_variants": [
        { "if_tag": "merciful", "text": "It feels right. As always." },
        { "if_lord_memory": "rewards_mercy",
          "text": "The lord's eyes soften when you explain." }
      ]
    }
  ]
}
```

### 9.1 Variant resolution

Single pass, deterministic:

1. Filter variants whose conditions all match current player / lord / arc state.
2. Sort matches by specificity **descending** (most conditions first). Ties broken by declaration order in the JSON.
3. First after sort wins — i.e. the most-specific matching variant. If none match, use base `setup` / `text`.

Conditions are AND-combined within a single variant (a variant with both `if_tag: merciful` and `if_lord_memory: rewards_mercy` requires both to be true). OR semantics are expressed by authoring multiple variants.

### 9.2 Option gates

`gate` filters options before the event renders. An option whose gate fails is removed from the option list the player sees. If **all** options fail their gates, the event is aborted pre-emit with a `ModLogger.Expected("CONTENT", "no_eligible_options", ...)` info-level log — the event never reaches the modal delivery path and does not consume pacing floors.

### 9.3 Full variant/gate condition vocabulary

Variants and option gates share the same closed-vocabulary set of conditions. The Variant Selector and the option-gate filter both resolve through one `ConditionEvaluator` helper.

**Identity axis (Plan 1):**
- `if_tag: <tagName>` / `requires_tag: <tagName>` / `forbid_tag: <tagName>` — reads `PlayerIdentity.HasTag`

**Lord-memory axis (Plan 2):**
- `if_lord_memory: <markerName>` / `requires_lord_memory: <markerName>` — reads `LordMemoryStore.Has(currentLord, marker)`

**Arc axis (Plan 3):**
- `if_arc_act: <arcId>:<actId>` / `requires_arc_act: <arcId>:<actId>` — reads `ArcTracker.CurrentAct(arcId) == actId`

**State axis (Plan 1 — see §9.4):**
- `if_scrutiny_above: <n>`, `if_scrutiny_below: <n>`, `if_scrutiny_tier: low|medium|high|extreme`
- `if_medical_risk_above: <n>`, `if_medical_risk_tier: low|medium|high`
- `if_lord_rep_above: <n>`, `if_lord_rep_below: <n>`, `if_lord_rep_tier: hostile|neutral|friendly|trusted`
- `if_supply_below: <n>`, `if_supply_tier: critical|low|normal|full`
- `if_readiness_below: <n>`, `if_readiness_tier: broken|strained|ready`
- `if_retinue_size_above: <n>` *(live from Plan 1, reads retinue manager count)*

**Trait axis (Plan 1):**
- `if_trait: <traitName>:<tier>` / `if_trait_above: <traitName>:<level>` — reads native TaleWorlds trait levels (Honor, Mercy, Valor, Calculating, Generosity, Oral Cunning, etc.). Tiers map to TaleWorlds' trait level bands; exact thresholds documented in `state-thresholds.md`.
- Enables narrative callbacks against accumulated `traitXp` effects without duplicating TaleWorlds' native trait-based option gates.

All `if_*_tier` values resolve through named thresholds documented in `docs/Features/Content/state-thresholds.md`. Authors prefer tier names over raw numbers; numeric forms exist for edge cases.

### 9.4 StateReader service (Plan 1)

New service under `src/Features/Content/`, peer to `ContextDetector`. Exposes typed queries used by both the Variant Selector and the validator:

```csharp
public sealed class StateReader
{
    public int Scrutiny { get; }
    public int MedicalRisk { get; }
    public int LordRep { get; }            // current enlisted lord
    public int SupplyLevel { get; }         // 0-100
    public int Readiness { get; }           // 0-100
    public int RetinueSize { get; }

    public ScrutinyTier ScrutinyTier { get; }
    public MedicalTier MedicalTier { get; }
    // ... one tier accessor per state axis

    public int TraitLevel(string traitName);  // reads native Hero trait level
    public TraitTier TraitTier(string traitName);
}
```

Backed by the existing `EscalationManager`, `CompanySupplyManager`, `EnlistmentBehavior.CompanyNeeds`, `RetinueManager`, and (for trait queries) `Hero.MainHero.GetTraitLevel` via `DefaultTraits`. No new persistence — these managers already persist the underlying state.

The StateReader is cached per-emit (the same values are used by both the pre-emit gate filter and the render-time variant selector). Cache invalidates at the start of each `EmitCandidate` call.

### 9.5 Validator additions

`Tools/Validation/validate_content.py` gains a phase:

- Every event must declare at least one `contexts` entry (no `Any`) — hard fail after cull merges.
- Every `contexts` value must resolve to the closed context enum.
- Every `if_tag` value must resolve to the closed identity-tag enum.
- Every `if_lord_memory` value must resolve to the closed memory-marker enum.
- Every `if_arc_act` value must parse as `arcName:actName` with both halves in the closed arc catalog.
- Every `if_*_tier` value must resolve to the tier enum documented in `state-thresholds.md`.
- Every `beats` value must resolve to the `StoryBeat` enum.
- Every key in `effects` must resolve to the closed effect vocabulary (§7.4). Unknown keys hard-fail.

## 10. Depth layer 1 — Identity tags (Plan 1)

### 10.1 Current state and the gap Plan 1 closes

The spec's earlier draft claimed tags were "already shipping data." That was wrong. Investigation during design review found:

- Promotion event JSONs declare `"character_tag": "disciplinarian"` (and variants) on their options.
- `EventCatalog.ParseEffectsObject` (`EventCatalog.cs:996–1064`) **does not parse the key**.
- `EventDeliveryManager.ApplyEffects` (`EventDeliveryManager.cs:518`) **does not apply it.**
- No `PlayerIdentity` service exists; no storage persists tags.

The tags authored in promotion events are currently silently dropped. The fix is owned by Plan 1 because the whole identity-variant axis depends on it.

### 10.2 PlayerIdentity service

```csharp
public sealed class PlayerIdentity : CampaignBehaviorBase
{
    // Read side (consumed by Variant Selector)
    public bool HasTag(string tag);
    public IReadOnlyCollection<string> ActiveTags { get; }
    public string DominantTag { get; }  // most-applied, for severity bonus

    // Write side (consumed by EventDeliveryManager.ApplyEffects)
    public void AddTag(string tag, string source);
    public void RemoveTag(string tag, string source);
}
```

Persists a `Dictionary<string, int>` (tag → times-applied count) via `SyncData`. Registered in `EnlistedSaveDefiner`. `DominantTag` is the key with the highest count; ties resolve by first-applied.

### 10.3 Plan 1 closes the parse-and-apply chain

Three small code changes land in Plan 1 alongside the reader/writer service:

- `EventEffects` class gains a `CharacterTag` field (string).
- `EventCatalog.ParseEffectsObject` reads `character_tag` / `characterTag` into that field.
- `EventDeliveryManager.ApplyEffects` calls `PlayerIdentity.AddTag(effects.CharacterTag, eventId)`.

The validator enforces every `character_tag` effect value against the closed identity-tag enum (§10.4).

### 10.4 Closed tag vocabulary

Finite list, documented in a new `docs/Features/Content/identity-tags.md`:

- `disciplinarian`, `merciful`
- `aggressive`, `cautious`
- `honorable`, `pragmatic`
- `loyal`, `ambitious`
- `social`, `solitary`

Authors can reference any tag in `if_tag` / `requires_tag` / `forbid_tag`. The validator rejects unknown tags.

### 10.5 Severity bonus

The Director's `SeverityClassifier.Classify` gains a +0.05 bonus when the event's `primary_tag` (new optional field) matches the player's `DominantTag`. Nudges tag-aligned events toward Modal without overriding explicit tier caps. Small hook, one plumbed value — does not restructure the classifier.

## 11. Depth layer 2 — Lord memory (Plan 2)

### 11.1 LordMemoryStore service

```csharp
public sealed class LordMemoryStore : CampaignBehaviorBase
{
    public void Set(Hero lord, string marker);
    public bool Has(Hero lord, string marker);
    public void Clear(Hero lord, string marker);
    public IReadOnlyCollection<string> Markers(Hero lord);
}
```

Backing store: `Dictionary<MBGUID, HashSet<string>>` — one entry per lord ever interacted with. Registered in `EnlistedSaveDefiner` with a reserved ID in the mod's 735xxx range.

**Save-definer container registrations required** (verified against `src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs:96–100`, which currently registers only string-keyed dictionaries):

```csharp
ConstructContainerDefinition(typeof(Dictionary<MBGUID, HashSet<string>>));
ConstructContainerDefinition(typeof(HashSet<string>));
```

`MBGUID` itself is natively serializable (decompile-verified at `TaleWorlds.ObjectSystem.MBObjectBase:15` — `[SaveableProperty(2)] public MBGUID Id { get; set; }`). Plan 2 adds these two `ConstructContainerDefinition` calls to the save definer in the same PR as the `LordMemoryStore` behavior class.

Implicit "current lord" overload:

```csharp
public bool Has(string marker); // uses EnlistmentBehavior.Instance.CurrentLord
```

The `if_lord_memory` variant condition uses the implicit form.

### 11.2 Closed memory-marker vocabulary

Finite list, documented in `docs/Features/Content/lord-memory-markers.md`:

- `betrayed_once`, `betrayed_twice`
- `spared_mutineer`, `executed_mutineer`
- `denied_promotion`, `earned_promotion`
- `pardoned_infraction`, `reported_infraction`
- `refused_order_once`, `obeyed_despite_cost`
- `rewards_mercy`, `punishes_mercy`
- `trusts_you`, `distrusts_you`
- `saved_in_battle`, `abandoned_in_battle`

Authors write markers via new option effect: `"write_lord_memory": "betrayed_once"`. The validator enforces the enum.

### 11.3 Relationship transfer

On `OnHeroChangedLord` / retirement / transfer, the current-lord pointer moves. The `LordMemoryStore` **does not** clear markers — they remain scoped to the old lord. If the player re-enlists with the same lord later, the markers return. This is intentional: lords remember.

## 12. Depth layer 3 — Consequence arcs (Plan 3)

Arcs replace the independent-threshold firing pattern. Finite catalog, declarative JSON.

### 12.1 Arc model

```json
{
  "id": "scrutiny_crisis",
  "preconditions": { "scrutiny_min": 5 },
  "abort_if": { "scrutiny_max": 2 },
  "acts": [
    { "id": "rumors",       "advance_on": "arc_start",                "tier": "Modal" },
    { "id": "complication", "advance_on": "next_escalation_beat",     "tier": "Headline" },
    { "id": "crisis",       "advance_on": "scrutiny_min:8",           "tier": "Modal" },
    { "id": "resolution",   "advance_on": "next_muster_or_court",     "tier": "Modal" }
  ]
}
```

### 12.2 ArcTracker service

```csharp
public sealed class ArcTracker : CampaignBehaviorBase
{
    // Singleton-arc API — one instance per arc ID.
    public void TryStart(string arcId);
    public string CurrentAct(string arcId);  // null if not active
    public bool IsActive(string arcId);
    public void Abort(string arcId);

    // Instanced-arc API — multiple concurrent instances keyed by subject.
    // Used by per-veteran and per-lord arcs (e.g. veteran_loyalty.{veteranId}).
    public void TryStart(string arcId, string subjectKey);
    public string CurrentAct(string arcId, string subjectKey);
    public bool IsActive(string arcId, string subjectKey);
    public void Abort(string arcId, string subjectKey);
}
```

Persists arc state (current act + start day, per arcId and per subjectKey) via `EnlistedSaveDefiner`. Singleton arcs are keyed by `arcId` alone; instanced arcs are keyed by `(arcId, subjectKey)`. Starting an already-active arc is a no-op (`ModLogger.Expected`).

**Subject keys** are stringly-typed IDs resolved per arc:

- `veteran_loyalty` uses `NamedVeteran.Id` — the mod-generated stable string ID assigned at veteran emergence (`src/Features/Retinue/Data/NamedVeteran.cs:16,74`). Veterans are POCO data (not native `Hero` objects), serialized as part of `RetinueState`. IDs persist across save/load.
- `lord_trust`, `reputation_crisis`, `heirloom_earned` use the enlisted lord's `Hero.Id.InternalValue.ToString()` — `Hero.Id` is `MBGUID`, marked `[SaveableProperty]` on `MBObjectBase`, stable across save/load (decompile-verified at `TaleWorlds.ObjectSystem.MBObjectBase:15`).

The arc JSON declares its `subject_source` (one of `veteran`, `enlisted_lord`, `none`); `ArcTracker` resolves the key at `TryStart` time and the Variant Selector passes it through when reading `CurrentAct`.

### 12.3 Arc catalog (Plan 3 delivery)

Twelve arcs at launch. Arcs are the default authoring unit — any system with a narrative through-line is an arc.

**Pressure systems (singleton):**

1. **`scrutiny_crisis`** — discipline/behavior spiral. Rumors → Promotion Blocked → Court Martial → Discharge or Redemption. Four acts, ~4 events.
2. **`medical_crisis`** — chronic medical risk spiral. Onset → Worsening → Critical → Treatment or Invalidation. Four acts, ~4 events.
3. **`reputation_crisis`** — lord-relationship erosion. Cool Looks → Public Rebuke → Trial of Loyalty → Reinstatement or Dishonor. Four acts, ~4 events.
4. **`supply_famine`** — replaces `pressure_arc` + `company.crisis`. Scarcity → Rationing → Hunger → Breaking Point. Four acts, ~8 events.
5. **`illness_outbreak`** — replaces the 28 illness onset variants. Onset → Spread → Quarantine → Aftermath. Four acts, ~6 events.

**Progression (singleton):**

6. **`promotion_trial`** — per-rank proving. Already de-facto arcs in the existing JSON; Plan 3 formalizes their state and lets `if_arc_act` read their progression. Repeated across T2→T9, ~20 events total.
7. **`heirloom_earned`** — rare gift-of-gear path. Lord Notices → Private Trial → Presentation (gear granted) → Legacy. Four acts, ~4 events. Triggers only at high lord reputation + specific tier thresholds; fires at most once per lord.

**Relational (singleton):**

8. **`lord_trust`** — escalating intimacy with enlisted lord. Respect → Confidence → Counsel → Inner Circle. Four acts, ~4 events. Shares gating with `reputation_crisis` (they're inverse arcs; only one active at a time).
9. **`deserter_temptation`** — the pull to leave. Whispers → Plan Offered → Point of No Return → Departure or Recommitment. Four acts, ~4 events. Replaces the standalone desertion chain currently in `events_pay_mutiny.json`.
10. **`pay_revolt`** — tension → mutiny-prep → eruption → aftermath. Partially exists today; Plan 3 formalizes. Four acts, ~8 events. Shares gating with `deserter_temptation` (can coexist; desertion is personal, revolt is collective).

**Relational (instanced):**

11. **`veteran_loyalty`** — per-veteran retinue arc. One instance per named veteran. Early Days → Tested Bond → Friction → Right-Hand or Defection. Four acts × N active instances. Replaces the bulk of today's 98 retinue events. Authored template events fire per-veteran with veteran name injected via placeholder; the arc catalog defines the template once.
12. **`retinue_mutiny`** — collective retinue rebellion (singleton, even though it touches all veterans). Muttering → Ringleader Identified → Confrontation → Purge or Reform. Four acts, ~4 events. Fires only if multiple `veteran_loyalty` instances simultaneously reach `friction` act.

### 12.4 Arc interactions

Some arcs are mutually exclusive; some chain. Documented in the arc JSON via `conflicts_with` and `unlocks`:

- `reputation_crisis` and `lord_trust` are inverse — only one active at a time. The other is aborted when one starts.
- `deserter_temptation` and `pay_revolt` can coexist but share a category cooldown — the Director fires at most one per modal slot.
- `retinue_mutiny` requires at least 3 concurrent `veteran_loyalty` instances at or past the `friction` act.
- `heirloom_earned` requires `lord_trust` at `inner_circle` + rank T5+ + lord rep trait ≥ `friendly`. Fires at most once per lord per save.

### 12.5 Events join arcs declaratively

```json
{
  "id": "scrutiny_rumors_whispers",
  "arc": { "id": "scrutiny_crisis", "act": "rumors" },
  "contexts": ["camp_night", "march"],
  ...
}
```

The Director queries `ArcTracker` on emit: a candidate with an `arc` declaration only fires when the arc's current act matches the event's declared act. Events without `arc` declarations are free-firing (most of the library).

## 13. Cull strategy

The cull happens **once**, in Plan 1, as a single large PR. Every surviving event is rewritten into schema v2 (contexts + tag variants). Plans 2 and 3 add lord-memory variants and arc declarations onto surviving events — they never touch JSONs the cull already deleted.

### 13.1 Cull principles

1. **Arcs are the default authoring unit** for any system with a through-line. Flat one-shots survive only for genuinely self-contained flavor beats.
2. Every surviving event declares ≥1 specific context. If the situation can't be named, the event dies.
3. Every surviving event has a decision no other event makes, AND carries at least 4 variant reads (identity / lord-memory / arc-act / state-tier / trait-tier) so a single authored event does the work of several.
4. Threshold clusters collapse into arcs. `scrutiny_crisis`, `medical_crisis`, `reputation_crisis` absorb the 84 escalation-threshold events.
5. Pressure-system duplicates merge into `supply_famine` (replaces `pressure_arc` + `company.crisis`).
6. Illness variants collapse into `illness_outbreak` (28 → 6 arc events).
7. Retinue narrative moves almost entirely into instanced `veteran_loyalty` arcs + the singleton `retinue_mutiny`. Flat retinue one-shots are reserved for specific non-arc moments (post-battle heroism incidents, casualty memorials).
8. Post-battle / siege / town / village flavor incidents consolidate aggressively — each category reduces to a handful of context-weighted, variant-rich one-shots.
9. **Rule of thumb: if an author finds themselves writing a second flat one-shot in the same category with a similar decision, it's probably a 2-act arc instead.**

### 13.2 Projected totals (curated library)

| Group | Today | Arc? | After cull |
|:--|--:|:--|--:|
| Escalation thresholds (scrutiny / medical / rep) | 84 | 3 arcs × ~4 acts | ~12 |
| Supply pressure / crisis | ~40 | 1 arc × ~8 acts | ~8 |
| Illness | 28 | 1 arc × ~6 acts | ~6 |
| Pay tension + mutiny | 51 | `pay_revolt` arc (~8) + `deserter_temptation` arc (~4) | ~12 |
| Retinue | 98 | `veteran_loyalty` template (4 events) + `retinue_mutiny` (~4) + ~6 flat casualty/heroism incidents | ~14 |
| Promotion | 26 | `promotion_trial` arc across 7 rank gates | ~20 |
| Lord relationship | (spread across many files today) | `lord_trust` arc (~4) + `heirloom_earned` arc (~4) | ~8 |
| Post-battle incidents | 44 | flat, variant-rich | ~6 |
| Siege incidents | 40 | flat, variant-rich | ~6 |
| Town / village / leaving / waiting | 96 | flat, variant-rich | ~12 |
| Quiet stretch + bag check | 16 | flat | ~5 |
| **Total** | **~557** | — | **~100 (±10)** |

All figures above are **indicative targets**, not budgets. The Plan 1 cull PR lands the exact count. The band ±10 around ~100 gives authors room to split or merge an arc late in the cull without re-spec'ing.

**Arc share of library:** roughly ~85 of ~100 events (~85%) are arc acts. Only ~15 are flat one-shots, reserved for situations where the fire-and-forget model is genuinely correct.

**Variant density target:** every event in the curated library declares at least 4 variant conditions across the five axes (identity / lord-memory / arc-act / state / trait). The validator warns on events with fewer; the cull review rejects events with fewer than 2.

### 13.3 Deletions are one atomic commit

Deleted JSON files land in a single PR-sized deletion commit at the head of Plan 1. Reviewers can evaluate the cull as one decision. Surviving rewrites follow in commits grouped by subsystem. CI validator must pass every commit.

## 14. Phasing

One unified spec, three sequential plans, plus the three prerequisite fixes.

### 14.1 Prerequisites (land first, or alongside Plan 1's first commits)

- **PR-a:** SeverityClassifier docstring fix (1 line).
- **PR-b:** `EventDeliveryManager` queue persistence + bounded cap + save-definer entry.
- **PR-c:** `DispatchItem` typed-kind field + `RenderedBody` passthrough + muster/news typed-filter migration. Includes `DispatchItem.IsHeadline` helper.

### 14.2 Plan 1 — Foundations (big)

- Context enum + `ContextDetector` service.
- Context filter integration into `StoryDirector.EmitCandidate`.
- Event schema v2 (contexts, severity_hint, weight, variants, gates).
- `VariantSelector` service with null-safe hooks for lord-memory / arc services.
- `ConditionEvaluator` helper shared by variant selector and option-gate filter.
- `StateReader` service (scrutiny / medical / lord-rep / supply / readiness / retinue-size) + state-axis variant conditions (`if_scrutiny_above`, `if_lord_rep_tier`, etc.).
- `PlayerIdentity` **reader + writer** service (Plan 1 closes the `character_tag` parse/apply gap — see §10.1–10.3).
- `+0.05` severity bonus hook in `SeverityClassifier` (event's `primary_tag` matches player's `DominantTag`).
- Effect vocabulary closed-enum documentation (`effect-vocabulary.md`) + validator phase rejecting unknown effect keys.
- **The cull:** 557 → ~100 (±10) authored into schema v2 (arcs authored as placeholder stubs for Plan 3 — arc-act state doesn't fire yet, but the declarations are in place so Plan 3 can go live without touching JSONs again).
- Validator phase for schema v2 (contexts, tags, state conditions, effect keys, beats).
- Documentation: `context-taxonomy.md`, `identity-tags.md`, `state-thresholds.md`, `effect-vocabulary.md`, `event-schema-v2.md`.

### 14.3 Plan 2 — Lord memory

- `LordMemoryStore` behavior + save-definer entry.
- Closed marker vocabulary + `lord-memory-markers.md`.
- New option effect: `write_lord_memory`.
- `VariantSelector` wired to the store.
- Retrofit ~30–40 high-leverage events with lord-memory variants and write effects. Authored additions only — no structural rewrites of Plan 1 survivors.

### 14.4 Plan 3 — Consequence arcs

- Arc JSON schema + `ArcTracker` behavior (both singleton and instanced APIs) + save-definer entry.
- Twelve initial arcs authored: `scrutiny_crisis`, `medical_crisis`, `reputation_crisis`, `supply_famine`, `illness_outbreak`, `promotion_trial`, `heirloom_earned`, `lord_trust`, `deserter_temptation`, `pay_revolt`, `veteran_loyalty` (instanced), `retinue_mutiny`.
- Arc-act gate integrated into the Director's eligibility filter (candidates declaring `arc` only fire when the arc's current act matches).
- Arc interaction rules (`conflicts_with`, `unlocks`, subject-key resolution) authored in arc JSONs and enforced by `ArcTracker`.
- `grantGear` effect handler (writes into enlistment equipment plumbing) + validator enforcement that it only appears on `heirloom_earned:presentation` options.
- Retire the free-firing `EscalationManager` threshold path — all three escalation axes (scrutiny, medical, reputation) now run exclusively through their respective arcs.
- Retire the standalone desertion chain in `events_pay_mutiny.json` — its content migrates into `deserter_temptation`.
- `VariantSelector` wired to `ArcTracker`; `if_arc_act` variants activate.

## 15. Error handling & testing

- **Validator is the primary gate.** CI fails on unknown contexts, unknown tags, unknown markers, unknown arc refs, unknown beats. No "soft warning" bucket for schema v2 in Plan 1 post-cull.
- **Variant Selector is a pure function** given (player state, lord state, arc state, event). Unit-testable without a running campaign. Plan 1 ships variant-resolution unit tests covering: no-match falls back to base, highest-specificity match wins, ties resolve by declaration order, null services (pre-Plan-2/3) never throw.
- **ContextDetector unit tests** cover each context's trigger conditions against fake campaign state.
- **Arc state save-surface smoke tests** in Plan 3 — (a) start a singleton arc, save, reload, confirm `CurrentAct(arcId)` is unchanged; (b) start two instances of `veteran_loyalty` on different veteran subject keys, save, reload, confirm both `CurrentAct(arcId, subjectKey)` values are unchanged. Same save-surface pattern for `LordMemoryStore` in Plan 2 (per-lord marker persistence across reload).
- **Arc interaction tests** in Plan 3 — verify `reputation_crisis` aborts an active `lord_trust` and vice versa; verify `retinue_mutiny` only starts when ≥3 `veteran_loyalty` instances sit at `friction`; verify `heirloom_earned` requires the full precondition stack (T5+, lord_trust:inner_circle, lord rep trait ≥ friendly) and fires at most once per (lord, save).
- **Validator tests** — a golden-file test harness passes a small set of hand-crafted malformed JSONs (unknown context, unknown tag, unknown effect key, ungated `grantGear` outside `heirloom_earned:presentation`, `morale` used in `companyNeeds`) and confirms each one produces the expected hard-fail.
- **Fallback discipline.** If the Variant Selector finds no variant match, it returns base text silently. No `ModLogger.Surfaced` call. If every option's gate fails, the event aborts pre-emit and logs `ModLogger.Expected` (info tier, no toast).
- **Context-check performance.** `ContextDetector.MatchesAny` is a bitwise op; called once per emit. `EmitCandidate` path adds no measurable cost.
- **No regressions in the Director's pacing contract.** The context filter drops candidates before relevance/severity run; it never alters tier, never consumes floors, never writes pacing state.

## 16. Out of scope (explicit)

- Broader "meaning canonicalization" refactor of the full StoryCandidate → DispatchItem → consumer chain. This spec touches the chain where it must (prerequisite 5.3 + §7 contracts) but does not re-plumb downstream consumers beyond that.
- New gameplay mechanics (no new traits, no new reputation axes, no new naval infrastructure — the existing War Sails integration at `NavalBattleShipAssignmentPatch.cs` and `CampOpportunityGenerator`'s `notAtSea`/`atSea` gating is reused as-is; `at_sea` just becomes a first-class context in the event taxonomy).
- New trait system integration beyond the existing `traitXp` effect on options.
- Generative / procedural event content. Every surfaced event is hand-authored JSON.
- Rewriting the `EscalationManager` firing model for non-arc events. Plan 3 retires only the five arc-adopting threshold systems.
- UI changes to the enlisted menu, muster menu, or camp hub beyond what the typed-filter migration (§5.3) requires.
- Changes to the Director's density dial, wall-clock floor, or category-cooldown defaults.
- **Free-form gear grants.** The quartermaster gear loop stays a pull-model — QM conversation, bag-check flow, muster resupply. Events can consume baggage (`randomBaggageLoss`, `grantTemporaryBaggageAccess`, `baggageDelayDays`, `bagCheckChoice`) freely. Direct gear grants via `grantGear` are **gated to the `heirloom_earned` arc's Presentation act** (see §7.4, §12.3). No other arc or flat event can author `grantGear`; the validator hard-fails otherwise. This keeps gear progression narratively scarce — an heirloom is a once-per-lord, once-per-save moment, not a loot drop.
- **Food/ration grants.** `foodLoss` exists; there is no `foodGain`. Company supply can be delta'd up or down via `companyNeeds.Supplies` (the writer path); personal rations live in the native Bannerlord party inventory and are consumed by the existing muster flow. Event-driven positive food grants are out of scope.
- **Order-state writes.** Events cannot start, complete, fail, or swap orders via effects. Orders are a separate subsystem; event effects observe order state (via the arc layer in Plan 3) but do not mutate it.
- **Native party morale.** `companyNeeds.morale` is deprecated (removed 2026-01-11 per `CLAUDE.md`). Events must use other `companyNeeds` keys; `morale` in an effect block is a validator hard-fail post-Plan-1. The fatigue budget (0–24) remains functional and is writable through its existing effect path.
- **Direct dispatch/news writes from events.** Events emit via the Director; the Director writes `DispatchItem` entries. Events cannot bypass the Director to write news directly.
- **New save-defined subsystems beyond `PlayerIdentity` (Plan 1), `LordMemoryStore` (Plan 2), `ArcTracker` (Plan 3).** The `StateReader` service holds no persistent state of its own — it reads from existing managers' save surfaces.
