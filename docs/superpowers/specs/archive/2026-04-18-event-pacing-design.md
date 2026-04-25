# Event Pacing Redesign — Design Spec

**Date:** 2026-04-18
**Status:** Complete — all four phases shipped (2026-04-18). All 15 production `EventDeliveryManager.Instance.QueueEvent(...)` call sites route through `StoryDirector.EmitCandidate`. Five `ShowInquiry` sites annotated as intentional bypass. `ChainContinuation` flag bypasses in-game floor + category cooldown for promotions, chain events, and bag checks (still honors 60s wall-clock). `StoryDirector.OnDailyTick` flushes one deferred interactive per tick (FIFO cap 32). Supply pressure and veteran memorials demoted to accordion-only (`Pertinent` tier). `EventDeliveryManager` is now the downstream executor used only by the Director, the debug-tool bypass, and Director-unavailable fallback paths. Implementation plan with full commit table at [docs/superpowers/plans/2026-04-18-event-pacing.md](../plans/2026-04-18-event-pacing.md).
**Scope:** Unify event/popup pacing across all story-generating systems in Enlisted. Replace six independent schedulers with a single director that routes every candidate through a three-tier presentation model. Resolve the "too many popups, chaotic" feedback by applying narrative causality + wall-clock interrupt discipline + menu-first consumption.

---

## 1. Problem

The mod currently has **six independent story-sources**, each with its own scheduler:

1. `CompanySimulationBehavior` — daily roster sim + 0–2 camp incidents/day + pressure-arc escalators
2. `CampRoutineProcessor` — phase-transition outcome rolls (Excellent/Good/Normal/Poor/Mishap)
3. `CampOpportunityGenerator` — pre-scheduled contextual opportunities
4. `OrderProgressionBehavior` — order-duty events (8–15%/phase, up to ~46%/day at Intense)
5. `ContentOrchestrator` — crisis injection + activity-level analyzer
6. `EscalationManager` — threshold-triggered scrutiny / medical / rep events

Plus a world-event surface via `EnlistedNewsBehavior` + `MapIncidentManager`.

Each scheduler is polite on its own. Collectively they compound: no system sees what another just fired, and `MapIncidentManager` routes everything through `EventDeliveryManager` as a blocking `MultiSelectionInquiryData` modal over the enlisted menu.

Two deeper failure modes on top:

- **Time-speed elasticity.** Bannerlord runs at 1x or 3x. Any daily/hourly-tick-based rule compresses at fast-forward — 5 in-game days can pass in ~60 wall-seconds. In-game floors alone cannot prevent modal clumping.
- **Raw world-event firehose.** `EnlistedNewsBehavior` captures every war, siege, hero death, settlement ownership change in the campaign. Piping that raw into any `[NEW]`-badged UI just relocates the spam.

The result the designer names: "chaotic, too many popups, can happen at sea, on land, in siege, hard to adapt to all contexts."

## 2. Goals

- Interrupt the player (modal tier) only for narrative moments that deserve it, with a ~5-day in-game floor and a ~60-second wall-clock floor
- Make most content *menu-visible* rather than modal — consumed at the player's pause-cadence
- Ground every surfaced event in a world or simulation *beat* the player can name; eliminate heartbeat rolls
- Keep the campaign's existing simulation outputs (patrols, company drama, escalation) — just change *how* they reach the player
- Give the player one tuning dial (density: Sparse / Normal / Dense)

## 3. Non-goals

- No new content authoring in this spec — we re-route existing candidates, we don't add JSON
- No overhaul of `EnlistedSaveDefiner`'s save surface beyond what the Director needs
- No changes to muster-menu flow, quartermaster dialogs, or camp opportunity UI — their pull-model is already correct
- No custom Gauntlet UI; re-use `enlisted_status` accordion pattern that already exists for orders and decisions

## 4. Core principles

1. **State-gate, don't clock-gate.** Content surfaces on *beats* (world events + simulation state transitions), not on timers. Random probability rolls are removed from the trigger layer; they may remain *inside* a beat for selecting which candidate fires.
2. **Sources emit, Director decides.** Every story-source emits `StoryCandidate` objects into a queue. None of them call `ShowInquiry` or `MBInformationManager` directly. The `StoryDirector` owns the player's attention.
3. **Three tiers, scaled to interrupt cost.** Modal interrupts are expensive currency — spent rarely, on critical forks. Accordion items are cheap — the player sees them at their own cadence. Log entries are free — they're ambient flavor.
4. **Menu-open is the consumption surface.** Non-modal content accumulates in `enlisted_status`. Opening the menu auto-pauses the campaign. Wall-clock speed is therefore irrelevant for non-modal tiers by construction.
5. **Burst-and-lull over steady drip.** Quiet in-game stretches are valid and intentional. A fallback pool covers pathological dead zones (≥14 days no modal) with slow-tempo introspection content.
6. **One player-facing dial.** Density (Sparse / Normal / Dense) scales the Modal-tier floors. No per-category knobs. Everything else is authored.

## 5. Architecture

### 5.1 The Director pattern

```
┌─────────────────────────────────────────────────┐
│                 Story-sources                   │
│                                                 │
│  CompanySimulationBehavior  OrderProgression    │
│  CampRoutineProcessor       EscalationManager   │
│  CampOpportunityGenerator   MapIncidentManager  │
│  EnlistedNewsBehavior (world-event capture)     │
└───────────────────┬─────────────────────────────┘
                    │ emit StoryCandidate
                    ▼
┌─────────────────────────────────────────────────┐
│                  StoryDirector                  │
│                                                 │
│  1. Relevance filter   (drops irrelevant)       │
│  2. Severity classify  (Modal/Headline/Pert/Log)│
│  3. Aggregator         (compress similar runs)  │
│  4. Surfacing          (fire now / digest / log)│
└───────┬────────────────────┬──────────────────┬─┘
        │                    │                  │
        ▼                    ▼                  ▼
    Modal popup         enlisted_status     Log ring
    (blocks map)        accordion           (scrollable)
```

### 5.2 Story-sources: new contract

Each source refactors from "fire directly" to "emit candidate." Concretely:

**Before (today):**
```csharp
// CompanySimulationBehavior.OnDailyTick
if (supplyDaysLow >= 3) {
    EventDeliveryManager.QueueEvent(supplyPressureStage1Event);
}
```

**After:**
```csharp
// CompanySimulationBehavior.OnDailyTick
if (supplyDaysLow >= 3) {
    StoryDirector.Instance.EmitCandidate(new StoryCandidate {
        SourceId = "company_sim.supply_pressure",
        Stage = 1,
        ProposedTier = StoryTier.Headline,
        SeverityHint = 0.4f,
        Payload = supplyPressureStage1Event,
        Beats = { StoryBeat.CompanyPressureThreshold },
    });
}
```

The source no longer decides *when* the player sees this. It only decides *that the condition is met*.

### 5.3 Tier model

| Tier | Where it lands | Interrupts? | Rate limit |
| :--- | :--- | :--- | :--- |
| **Modal** | `MultiSelectionInquiryData` over the map | **Yes** — blocks input | In-game 120h floor (density-scaled) + wall-clock 60s floor |
| **Headline** (accordion) | `enlisted_status` accordion — new option with `[NEW]` badge, auto-expand | No — visible next time menu opens | Relevance filter + per-category in-game cooldown |
| **Pertinent** (accordion) | `enlisted_status` accordion — new option, no auto-expand | No | Relevance filter + aggregation; released on muster day / camp entry digest |
| **Log** | Scrollable list inside a "News" accordion | No — no badge | None (free-firing flavor) |

### 5.4 StoryCandidate schema

```csharp
public sealed class StoryCandidate
{
    public string SourceId { get; set; }            // "company_sim.supply_pressure"
    public string CategoryId { get; set; }          // "company_pressure"  (for cooldown bucket)
    public StoryTier ProposedTier { get; set; }     // source's suggestion; Director may demote
    public float SeverityHint { get; set; }         // 0.0 – 1.0; feeds severity classifier
    public HashSet<StoryBeat> Beats { get; set; }   // which beats this responds to
    public RelevanceKey Relevance { get; set; }     // player's-lord? visited-settlement? hero-known?
    public CampaignTime EmittedAt { get; set; }     // for aggregation window
    public Action<StoryTier> Payload { get; set; }  // deferred delivery when Director greenlights
}
```

Notes:
- `ProposedTier` is advisory. The Director may demote (Modal → Headline) if floors unmet, or (rarely) promote (Headline → Modal) if severity score + beat cap allow.
- `Payload` is a delegate so the source stays in control of HOW to render (inquiry data, accordion entry, log line) — the Director only tells it which tier was granted.
- `RelevanceKey` is a small struct with explicit flags:

```csharp
public struct RelevanceKey
{
    public Hero SubjectHero;            // if any — null allowed
    public Hero ObjectHero;              // secondary hero — null allowed
    public Settlement SubjectSettlement; // if any — null allowed
    public Kingdom SubjectKingdom;       // if any — null allowed
    public IFaction SubjectFaction;      // if any — null allowed
    public Vec2 SubjectPosition;         // world-space if applicable; default (0,0)
    public bool TouchesEnlistedLord;     // pre-computed by source if known
    public bool TouchesPlayerKingdom;    // pre-computed by source if known
}
```

The relevance filter (§7) reads these fields plus live game state to decide pass/drop.

## 6. The beat-anchored trigger model

The `StoryDirector` subscribes to a curated finite list of beats. **No free-running timers.** Between beats, the Director does nothing.

### 6.1 Beat taxonomy

| Beat | Event source | Fires when |
| :--- | :--- | :--- |
| `LordCaptured` / `LordKilled` | `HeroTakenPrisoner`, `HeroKilled` filtered to `_enlistedLord` | Lord's hero state changes |
| `LordMajorBattleStart/End` | `OnMapEventStarted/Ended` filtered to lord's party | Lord enters/exits a MapEvent |
| `PlayerBattleEnd` | `OnPlayerBattleEnd` | Player-participated battle concludes |
| `SiegeBegin/End` | siege campaign events filtered to lord's settlement | Siege around lord |
| `WarDeclared` / `PeaceSigned` | faction events filtered to player's kingdom | Political state change |
| `ArmyFormed` / `ArmyDispersed` | `OnArmyCreated` / `ArmyDispersed` | Lord's army state changes |
| `SettlementEntered` / `SettlementLeft` | `OnSettlementEntered` / `OnSettlementLeft` | Player enters/exits a settlement |
| `PaydayBeat` | `MusterMenuHandler` cycle tick | Muster (12-day cycle) |
| `OrderPhaseTransition` | `OrderProgressionBehavior.ProcessPhase` | Dawn / Midday / Dusk / Night transitions (4/day) |
| `OrderComplete` | `OrderManager` completion hook | Order ends |
| `CompanyCrisisThreshold` | `CompanySimulationBehavior` crisis triggers | Supply crisis, epidemic, desertion wave |
| `CompanyPressureThreshold` | `CompanySimulationBehavior` pressure arc (stages 1–3) | Consecutive low-supply / low-morale days |
| `EscalationThreshold` | `EscalationManager` threshold evaluation | Scrutiny/medical-risk/lord-rep boundary |
| `QuietStretchTimeout` | Internal timer — 14 in-game days since last Modal | Safety net for pathological silence |

### 6.2 Beat → tier eligibility

Not every beat is modal-eligible. The table below is the authoring contract for which beats *may* promote to Modal tier; all others are capped at Headline or below regardless of what the source proposes.

| Beat | Max tier |
| :--- | :--- |
| `LordCaptured`, `LordKilled` | **Modal** (critical) |
| `LordMajorBattleEnd` (decisive outcome) | **Modal** |
| `PaydayBeat` | **Modal** (already a menu-sequence — uses Modal slot) |
| `CompanyCrisisThreshold` | **Modal** |
| `EscalationThreshold` (scrutiny/medical crisis) | **Modal** |
| `OrderComplete` (if narrative fork) | **Modal** |
| `QuietStretchTimeout` | **Modal** (fallback pool) |
| `WarDeclared`, `PeaceSigned` | Headline |
| `ArmyFormed/Dispersed` | Headline |
| `SiegeBegin/End` | Headline (unless player is in it → Modal) |
| `SettlementEntered/Left` | Pertinent |
| `LordMajorBattleStart` | Headline |
| `OrderPhaseTransition` | Log (ambient duty recap) |
| `CompanyPressureThreshold` stage 1–2 | Pertinent (stage 3 → Modal via CrisisThreshold) |

## 7. Relevance filter

A candidate passes only if its `RelevanceKey` satisfies **any one** of:

- Involves the **enlisted lord** (lord is the subject or participant)
- Involves the **player's kingdom** (war it enters/exits, its losses, its territory changes)
- Involves a **settlement the player has visited** (tracked in existing visit history)
- Involves a **hero the player knows** (quartermaster, NCO, retinue, any hero with relation > 0)
- Happens within **7 days' travel** of the player's current position (for battles/skirmishes — uses MobileParty distance helpers)
- Involves the **enemy faction the lord's party is actively engaged against** (read from lord's current `MapEvent.AttackerSide`/`DefenderSide` if in battle, else the lord's `Clan.Kingdom` war targets list)

Events that qualify on *none* of these are dropped before they touch any queue. This is expected to cut `EnlistedNewsBehavior`'s output by ~85%.

## 8. Severity classifier

After the relevance filter, each candidate is scored 0.0–1.0. The score combines:

- `SeverityHint` from the source (authoring signal)
- Beat weight (LordCaptured 0.95; PaydayBeat 0.90; CrisisThreshold 0.85; OrderComplete 0.50; Headline beats 0.30; Pertinent beats 0.15)
- Player stake (enlisted-lord involvement +0.15, player's-kingdom +0.10, visited-settlement +0.05)

Final tier is `max(ProposedTier, SeverityToTier(score))` constrained by the beat's `Max tier` cap (§6.2):

- score ≥ 0.70 → **Modal**
- score ≥ 0.35 → **Headline**
- score ≥ 0.10 → **Pertinent**
- score  < 0.10 → **Log**

## 9. Aggregation

Before surfacing, the Director holds Pertinent and Log candidates in a rolling 48-hour window (in-game time). On release, it aggregates:

- **Same-category, same-window.** Three `battle_report` entries tagged "Sturgia war" → one line: "Three battles in the Sturgian war this week — won 2, lost 1."
- **Same-hero runs.** Two "Lord X of Battania" items → collapse to one.
- **No aggregation for Modal or Headline** — those fire immediately on their beats.

## 10. Surfacing rules

### 10.1 Modal tier

A Modal fires only if **all** of:
- Beat is in the Modal-eligible list (§6.2)
- `CurrentDay - _lastModalDay >= ModalFloorInGameDays` (density-scaled, §12)
- `(DateTime.UtcNow - _lastModalWallClockAt).TotalSeconds >= 60`
- Per-category cooldown elapsed (default 12 in-game days)

If *any* condition fails, the candidate is demoted:
- Headline-eligible → accordion headline
- Otherwise → accordion pertinent
- The Modal slot remains open for the next eligible beat

### 10.2 Accordion headline

Fires immediately when a Headline-eligible beat delivers a qualifying candidate. Renders as a new option on `enlisted_status` with `[NEW]` badge and auto-expand (same as today's orders/decisions pattern).

### 10.3 Accordion pertinent + log

Pertinent items accumulate in an aggregation buffer. They're released as a single digest on:
- `PaydayBeat` — muster menu intro shows "Since you last mustered:" section
- `SettlementEntered` — if the player has been on the road > 2 in-game days, a "News from the road" digest item appears in `enlisted_camp_hub`

Log items land in a scrollable "News" accordion entry on `enlisted_status`, no badge. Capped at 200 entries with FIFO eviction.

### 10.4 Quiet-stretch fallback

If `CurrentDay - _lastModalDay >= 14`, the next hourly tick fires one Modal from a reserved "quiet stretch" pool (introspection, comrade banter, letter from home, sergeant check-in). This content is authored slow-tempo — it celebrates the quiet, doesn't disrupt it. Pool resides in new JSON file `ModuleData/Enlisted/events/quiet_stretch_pool.json`.

## 11. Time discipline

### 11.1 In-game floor

`ModalFloorInGameDays` is the authored floor scaled by density setting:
- Sparse: 7 days
- Normal: 5 days
- Dense: 3 days

Counted in in-game days via `CampaignTime.Now.ToDays`.

### 11.2 Wall-clock guard

`ModalFloorWallClockSeconds` is fixed at 60s regardless of density. Tracked via `DateTime.UtcNow` — unaffected by game speed or pause. Protects against the FastForward-compression case where 5 in-game days pass in ~20 wall-seconds.

### 11.3 Speed downshift on Modal

When a Modal fires and `Campaign.Current.TimeControlMode == StoppableFastForward`, the Director sets `TimeControlMode = StoppablePlay` (not `Stop` — not jarring). Matches the engine's built-in enemy-proximity slowdown. This is implemented via the existing `QuartermasterManager.CapturedTimeMode` pattern. Togglable via settings.

### 11.4 Density slider

Single setting `enlisted_event_density`, read from `ModuleData/Enlisted/Config/enlisted_config.json`:

```json
{
  "event_density": "normal",   // "sparse" | "normal" | "dense"
  "speed_downshift_on_modal": true
}
```

No MCM UI required for v1 — config file edit is acceptable. MCM surface can be added later.

## 12. Migration plan — what changes in each source

### 12.1 `CompanySimulationBehavior`
- Keep: roster sim, pressure-arc stage counters, crisis threshold detection
- **Remove**: direct `EventDeliveryManager.QueueEvent` calls
- **Add**: emits `StoryCandidate` on crisis / pressure-stage / significant roster change

### 12.2 `CampRoutineProcessor`
- Keep: outcome roll logic (Excellent/Mishap)
- **Remove**: `AddToNewsFeed` direct call
- **Add**: emits Log-tier candidates on Normal outcomes, Pertinent on Excellent/Mishap

### 12.3 `CampOpportunityGenerator`
- **No changes** — opportunities are pulled from camp menu, not pushed. Already correctly tiered (player-initiated).

### 12.4 `OrderProgressionBehavior`
- **Remove**: `TryGetOrderEvent` random probability roll (8–15% per phase)
- **Remove**: `ProcessPhase` direct event firing
- **Add**: emits Log-tier recap candidate per phase transition, Pertinent on meaningful outcome, Modal-eligible on order-fork moments
- **Keep**: phase recap tracking (used for muster digest)

### 12.5 `ContentOrchestrator`
- Keep: activity level analyzer, world-state reading
- **Remove**: direct crisis routing into `EventDeliveryManager`
- **Add**: exposes `CurrentActivityLevel` + `WorldState` as read-only for Director to consume
- Possibly **rename** to `WorldStateAnalyzer` to reflect reduced scope

### 12.6 `EscalationManager`
- Keep: threshold tracking, decay, reputation math
- **Remove**: `QueueEvent` calls on threshold cross
- **Add**: emits `StoryCandidate` with Modal-eligible tier on threshold cross

### 12.7 `MapIncidentManager`
- **Largest change.** Today it's the only world→content bridge and it modals everything.
- **Keep**: JSON catalog, incident selection logic
- **Remove**: direct `EventDeliveryManager.QueueEvent`
- **Add**: emits `StoryCandidate` with tier derived from incident JSON (new `"tier"` field — default "pertinent" for `entering_town`, `leaving_settlement`, "headline" for `leaving_battle`, "modal" only for explicit authored forks)

### 12.8 `EnlistedNewsBehavior`
- **Keep as capture layer** — it already listens to the right world events
- **Change**: instead of writing directly to `_personalFeed`, emit `StoryCandidate` per captured event. The Director's relevance filter decides which survive.
- **Add**: Director writes back to `_personalFeed` (for the Log accordion render) only AFTER filtering + aggregation

### 12.9 `EventDeliveryManager`
- **Refocus.** Today it's the only renderer. It stays — but it becomes the Modal-tier renderer only, called by the Director.
- **Remove**: direct `QueueEvent` public API (becomes internal); external callers route through Director.
- **Keep**: `MultiSelectionInquiryData` rendering, effect application, FIFO queue behavior (now bounded to Modal tier only).

### 12.9.1 Accordion and log rendering (renderer responsibility split)

Non-modal tiers do not go through `EventDeliveryManager`. The split is:

| Tier | Renderer | How it reaches the screen |
| :--- | :--- | :--- |
| **Modal** | `EventDeliveryManager` | Called by Director; shows `MultiSelectionInquiryData` modal |
| **Headline** | `EnlistedMenuBehavior` (existing accordion) | Director writes into a new state bag `StoryDirector.HeadlineAccordionState`; menu's existing `AddGameMenuOption` loop reads from it and appears as a `[NEW]`-badged option, auto-expand (same pattern as current `_ordersCollapsed` flag) |
| **Pertinent** | `EnlistedMenuBehavior` (existing accordion) | Released on digest trigger → Director writes to `StoryDirector.PertinentDigestState`; rendered as non-auto-expand accordion option under "News since last check-in" |
| **Log** | `EnlistedMenuBehavior` (new "News" accordion entry) | Director appends to `_logRing`; menu renders a collapsed "News" section on `enlisted_status` that expands to show the last 200 entries |

This keeps all rendering inside systems that already exist; the Director owns state, not pixels.

### 12.10 New files to create

- `src/Features/Content/StoryDirector.cs` — the broker
- `src/Features/Content/StoryCandidate.cs` — the candidate struct/class
- `src/Features/Content/StoryBeat.cs` — beat enum
- `src/Features/Content/StoryTier.cs` — tier enum
- `src/Features/Content/RelevanceFilter.cs` — relevance evaluation
- `src/Features/Content/SeverityClassifier.cs` — scoring
- `src/Features/Content/StoryAggregator.cs` — 48-hour window compression
- `src/Features/Content/QuietStretchPool.cs` — fallback content loader
- `ModuleData/Enlisted/events/quiet_stretch_pool.json` — fallback content authoring
- Register all new C# files in `Enlisted.csproj`
- Register save-relevant classes in `EnlistedSaveDefiner`

## 13. Save surface

The Director's state is lightweight — persisted in `SyncData()`:

- `_lastModalDay` (int in-game-day number)
- `_lastModalUtcTicks` (long DateTime.UtcNow.Ticks)
- `_categoryCooldowns` (Dictionary<string, int>)
- `_pertinentDigestBuffer` (List<StoryCandidatePersistent>)
- `_logRing` (bounded ring buffer, 200 entries)

`StoryCandidatePersistent` is the serializable subset. It contains:

```csharp
public sealed class StoryCandidatePersistent
{
    public string SourceId;          // routes payload reconstruction back to source on load
    public string CategoryId;        // for cooldown bucket
    public StoryTier GrantedTier;    // tier the Director assigned at emit time
    public float Severity;           // final computed score
    public int EmittedDayNumber;     // CampaignTime day at emit
    public int EmittedHour;          // hour-of-day at emit
    public string PayloadKey;        // source-defined opaque key for payload reconstruction
    public string RenderedTitle;     // pre-localized title text (used if reconstruction fails)
    public string RenderedBody;      // pre-localized body text  (used if reconstruction fails)
}
```

The `Payload` delegate is not persisted. On load, the source is asked to reconstruct via `IStorySource.RehydratePayload(PayloadKey)`. If reconstruction returns null, the Director falls back to rendering `RenderedTitle` + `RenderedBody` as a read-only entry (no interactive options). If even that fails, the entry drops silently with a `ModLogger.Log("E-PACE-001", ...)` warning.

Register in the project's existing `EnlistedSaveDefiner` (`src/Mod.Core/SaveSystem/EnlistedSaveDefiner.cs`). The definer uses a single `BaseId = 735000` with per-override offsets; the implementation plan uses class offset `30`, enum offsets `80`–`81`, and a container definition for `List<StoryCandidatePersistent>` (Dictionary<string,int> is already registered in the definer). Method pattern matches existing code:

```csharp
// In DefineClassTypes():
AddClassDefinition(typeof(StoryCandidatePersistent), 30);
// In DefineEnumTypes():
AddEnumDefinition(typeof(StoryTier), 80);
AddEnumDefinition(typeof(StoryBeat), 81);
// In DefineContainerDefinitions():
ConstructContainerDefinition(typeof(List<StoryCandidatePersistent>));
```

## 14. Testing

### 14.1 Unit tests

- Relevance filter: fabricate world events, assert pass/drop under each relevance key
- Severity classifier: assert tier assignment at each score boundary
- Aggregator: feed 3 same-category candidates inside 48h, assert single output
- Modal floor: emit back-to-back Modal candidates, assert second demoted
- Wall-clock guard: simulate `DateTime.UtcNow` ticks, assert 60s rule applies
- Speed downshift: set `TimeControlMode = StoppableFastForward`, fire Modal, assert mode becomes `StoppablePlay`

### 14.2 Integration scenarios

- **Quiet war week:** no qualifying beats for 12 in-game days → QuietStretchTimeout at day 14 → fallback Modal
- **Active battle week:** 3 major beats in 5 in-game days → first fires Modal, second demoted to Headline (floor), third demoted to Pertinent (category cooldown)
- **FastForward compression:** player at 3x speed, 4 in-game days pass in ~30 wall-seconds with 2 qualifying Modal beats → first fires, second deferred to Pertinent (wall-clock guard)
- **Muster digest:** 5 Pertinent + 8 Log candidates accumulate over 10 in-game days → muster menu opens with aggregated "Since you last mustered" section
- **Save/load in the middle of a digest:** save during a 48h aggregation window, load → buffer restored, digest still releases on next muster

## 15. Risks and mitigations

- **Delegate payloads can't be serialized.** *Mitigation:* split `StoryCandidate` into transient (`Payload`) + persistent (`StoryCandidatePersistent`). Sources must be able to reconstruct the payload on load from the persistent snapshot, or accept graceful drop.
- **Director becomes the single point of failure.** *Mitigation:* if the Director throws, candidates are logged and dropped — player never sees a crash. All `EmitCandidate` calls wrapped in `try/catch` with `ModLogger`.
- **Simulation sources still tick under the hood** — players who expect "slower sim" won't get that from this change. *Clarification:* this spec changes delivery, not simulation rate. If the simulation itself feels too busy, that's a separate tuning pass.
- **Wall-clock guard edge cases around suspend/resume.** If the player alt-tabs for minutes, `DateTime.UtcNow` advances normally and the guard unblocks. *Acceptable* — the guard's purpose is to prevent rapid-clump within a playing session.
- **Density slider makes QA harder.** *Mitigation:* ship on "normal" only for v1 (configured in JSON but single value). Expose sparse/dense after real-play tuning.

## 16. Out of scope (explicit)

- New content (events, decisions, order events, incidents)
- Dialogue/conversation changes
- Battle participation mechanics
- Equipment / quartermaster
- Retirement flow
- Any changes to `Enlistment/EncounterGuard` or the escort-AI attachment trick
- Rank progression
- MCM settings UI (JSON config only for v1)

## 17. Open questions

- Does the muster cycle remain 12 days, or should density slider scale it? **Current answer: keep 12.**
- Should the "News" log accordion be a new top-level hub option or a submenu under `enlisted_camp_hub`? **Recommend: top-level on `enlisted_status`, collapsed by default.**
- Do we need a "missed content" recovery path if a save-load drops a mid-aggregation candidate? **Recommend: no — accept silent drop for v1.**

## 18. References

Design informed by:
- [RimWorld Storyteller analysis](https://en.number13.de/rimworld-storyteller/) — recovery-period pattern, per-category pacing
- [CK3 Event Modding wiki](https://ck3.paradoxwikis.com/Event_modding) — pulse + per-event cooldown
- [Paradox: Event frequency is too high (CK3)](https://forum.paradoxplaza.com/forum/threads/event-frequency-is-too-high.1629362/) — proof that even Paradox acknowledges steady-drip as failure mode
- [Nexus: Banner Flavor Events](https://www.nexusmods.com/mountandblade2bannerlord/mods/9137) — state-gated + player-visible throttle + density slider
- [Viking Conquest with Freelancer (ModDB)](https://www.moddb.com/mods/viking-conquest-with-freelancer) — context-segregated channels
- [Total War Wiki: Event (Warhammer)](https://totalwarwarhammer.fandom.com/wiki/Event) — "one event per turn, turns can be empty"
- [EU3 Wiki: Mean time to happen](https://eu3.paradoxwikis.com/Mean_time_to_happen) — MTTH primitive

---

**Next step:** On designer approval, hand off to `superpowers:writing-plans` to produce the implementation plan (step-wise migration per story-source, with review checkpoints).
