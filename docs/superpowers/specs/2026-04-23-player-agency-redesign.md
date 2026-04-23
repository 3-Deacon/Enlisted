# Player Agency Redesign — Design (v3)

**Status:** Draft v3 (2026-04-23). Revised after game-design feedback clarified the target pattern: CK3 / Viking Conquest-style long-running postures, not repeated short camp clicks. v2's envelope model stays; the core loop is now persistent **service stances** with short camp activities demoted to optional interventions.

**North star (unchanged):** the mod should stop silently changing the player's soldier as if they were an NPC. The "orders over duties" vision holds.

**Contract (revised):** not "nothing changes between decisions" but **"no unpreviewed authored player-state consequences."** The player chooses a persistent service stance, accepts orders, takes short camp interventions, or responds to modals. Each envelope declares what categories of mutation are allowed, up to what magnitude, across what duration, at what risk bands, with what interruption points. Baseline service physics (tiny XP drift, small condition tick, supply consumption on the march, daily wage accrual while serving) continues **inside the current service stance**. Orders temporarily override the stance. High-stakes mutations (lose 3 men, +20 scrutiny, trait-level change, gold gain above a threshold) require either a modal at the moment of impact OR an explicit preview at envelope acceptance.

---

## Problem statement

Audit found 22 auto-firing systems mutating player-visible state on daily / hourly ticks without player input. v1's response was "gate every mutation behind an active `DecisionScope`; reject everything between scopes." Spec review found this over-corrects: breaks save/load, under-scopes the migration, and makes Bannerlord time feel fake for categories of change that are legitimately continuous inside service (a soldier's pay doesn't pause when he's between named orders).

The user's actual pain: rewards and penalties **that feel like they happened TO the soldier without foreshadowing or consent**. Tiny ambient XP gains are annoying because they're unasked-for; random sickness outbreaks are insulting because they couldn't be anticipated.

Sharper rule: **everything the player feels as "a consequence landed on me" must have been previewed at envelope acceptance or must fire as a modal at the landing moment.** Inside a previewed envelope, low-magnitude physics runs free.

---

## Envelope model

An **envelope** is a player-acknowledged activity or posture with a declared mutation contract. Four envelope types:

| Envelope | Triggered by | Typical duration | Example allowed mutations |
| :--- | :--- | :--- | :--- |
| **Order envelope** | Player accepts a named / daily order | Hours to days | Skill XP in expected skills, small conditions (fatigue, minor wounds from combat), path bumps for the order's path tag, small gold earned, scrutiny shifts with magnitude caps |
| **Service stance envelope** | Player selects a stance; defaults to Routine Service on enlistment | Until changed, invalidated, or overridden by an order | Low-magnitude drift matching the stance: drilling XP, scouting drift, scrutiny quieting, wound-care progress, supply help, lord proximity |
| **Activity override envelope** | Player picks a short camp intervention from the slot bank | Minutes to hours | One-shot category-scoped action: Rest allows Condition; Patrol allows Gold/Scrutiny; Attend allows Relation/Rank XP; etc. |
| **Modal envelope** | Player opens a `StoryDirector`-delivered Modal (incident, dialogue, proclamation) | Single decision | Only the option's declared effects |

The service stance is the answer to "FF in a peaceful garrison has zero consequences" without turning the game into a chore menu. It exists so baseline physics and RPG texture continue while Bannerlord time runs quickly. Its contents are previewed at enlistment and whenever the player changes stance.

Envelopes form a stack. Most specific wins: modal > order > activity override > service stance. When an order completes, the previous service stance resumes automatically.

---

## Service stances

Service stances are the CK3 / Viking Conquest layer: choose a posture, let time run, receive occasional summaries or meaningful interruptions. The player is not expected to pick a new camp activity every few hours.

| Stance | Intent | Low-stakes drift | High-stakes interruptions |
| :--- | :--- | :--- | :--- |
| **Routine Service** | Default follow-the-column behavior | Wage ledger, tiny fatigue/recovery drift, no path score | Direct orders, muster, major incidents |
| **Drill with the Line** | Improve fighting competence | Small weapon/Athletics XP, readiness nudges | Training injury, sergeant challenge, sparring rivalry |
| **Scout Ahead** | Lean ranger / reconnaissance | Scouting/Riding drift, ranger intent weight | Ambush clue, getting lost, lord asks for a report |
| **Stay Close to the Lord** | Build proximity and trust | Tiny relation/rank-XP chance, attendant storylets | Political errands, awkward audience, favoritism backlash |
| **Keep Your Head Down** | Avoid attention | Very slow scrutiny relief, fewer social opportunities | Suspected shirking, missed chance, NCO check-in |
| **Help the Wounded** | Support / medicine | Medicine drift, condition recovery help | Fever modal, supply shortage, mercy/reputation choice |
| **Forage and Hustle** | Supplies / money / rogue edge | Supplies stabilize, tiny gold chance, rogue intent weight | Theft accusation, patrol clash, risky find |

The stance is player-facing and persistent. It is selected from the camp/status surface, saved as a string id, and restored on load. New enlistments start in `routine_service`. Orders may suppress or replace stance drift while active; after the order resolves, the prior stance resumes unless the order explicitly changes it.

Stance outcomes are summarized, not spammed. Example: "Two days drilling: +24 One-Handed XP; readiness steady." Modals are reserved for high-stakes interruptions or authored choices.

---

## Core abstractions

| Type | Role |
| :--- | :--- |
| `MutationCategory` | `[Flags]` enum: `Skill / Condition / Gold / Quality / Flag / Scrutiny / Path / Relation / CompanyRoster`. A mutation carries its category. |
| `MagnitudeBand` | `Trivial / Minor / Moderate / Major`. Each envelope declares a per-category ceiling. A mutation whose magnitude exceeds the envelope's band is reclassified "high-stakes" and must be pre-previewed or modal-gated. |
| `Envelope` | Poco: `EnvelopeKind` (ServiceStance / Order / ActivityOverride / Modal), `Source` (stance id / storylet id / activity id), `StartedAt`, `ExpiresAt`, `AllowedCategories`, `PerCategoryMaxBand` (dict), `PreviewedIds` (HashSet of storylet / effect ids pre-announced at acceptance). |
| `ServiceStance` | Data definition for a persistent soldier posture: id, label, preview text, drift categories, summary cadence, interruption pool, and invalidation rules. |
| `ServiceStanceManager` | Holds the current stance id, defaults to `routine_service`, persists the id via behavior `SyncData`, and exposes stance-derived envelope data to `AgencyGate`. |
| `AgencyGate` | Static service. Does NOT own a persisted stack. On every `StateMutator.Try{…}` call, derives the current envelope set by reading: `ServiceStanceManager.Current`, `ActivityRuntime._active` (orders + short camp overrides), `StoryDirector` active modal id, `EnlistmentBehavior.IsEnlisted`. No separate save serialization. |
| `StateMutator` | Gateway: `TryApplySkillXp(hero, skill, amount, reason, magnitudeHint)`, `TryAdjustCondition(...)`, `TryGiveGold(...)`, `TryWriteQuality(...)`, `TryWriteFlag(...)`, `TryAdjustScrutiny(...)`, `TryBumpPath(...)`, `TryAdjustRelation(...)`, `TryMutateRoster(...)`. Each call resolves the current envelope, checks category + magnitude band, applies or rejects. |
| `EnvelopeAcceptancePreview` | Rendered at stance selection, order acceptance, and activity override start: list of `PreviewedIds` bundled with "will grant / may cost / risks" summary. Text comes from stance data or the storylet's new `preview` field (see schema addendum). |
| `DispatchDomain` | Typed news/feed family: `Kingdom / Personal / Camp`. Prevents stance/player updates from leaking into realm dispatches. |
| `DispatchSourceKind` | Typed producer meaning: `ServiceStance / Order / ActivityOverride / ModalIncident / Routine / Battle / Muster / Promotion / Condition / Flavor / Unknown`. Replaces menu-side string guessing. |
| `DispatchSurfaceHint` | Preferred display surface: `Auto / Dispatches / Upcoming / You / SinceLastMuster / CampActivities / ModalOnly`. A hint, not an effects gate. |
| `IncidentResponseModal` | Converts `CompanySimulationBehavior.ProcessIncidents` auto-apply into candidate emission → modal → player picks → effects fire under the modal envelope. |
| `ChoreThrottle` | Per-(category, source) cooldown registry for short activity overrides and repeated stance interruptions. E.g. Rest cannot resolve the same "surgeon checks the bandage" storylet more than once per 12 game-hours. Enforced in `StoryletCatalog.PickEligible` filter. |

### File map

| New file | Purpose |
| :--- | :--- |
| `src/Features/Agency/MutationCategory.cs` | `[Flags]` enum |
| `src/Features/Agency/MagnitudeBand.cs` | enum + helper `ClassifyAmount(category, amount)` |
| `src/Features/Agency/Envelope.cs` | POCO |
| `src/Features/Agency/AgencyGate.cs` | Static derivation service (no save state) |
| `src/Features/Agency/StateMutator.cs` | Gateway |
| `src/Features/Agency/ServiceStance.cs` | Data definition for persistent stances |
| `src/Features/Agency/ServiceStanceManager.cs` | Current stance, persistence, summary cadence |
| `src/Features/Agency/EnvelopeAcceptancePreview.cs` | Render helper called from order/activity acceptance flows |
| `src/Features/Agency/ChoreThrottle.cs` | Per-(category, source) cooldown tracking (save-definer offset 52) |
| `src/Features/Activities/Camp/CampActivityActivity.cs` | Concrete short override `Activity` subclass (save-definer offset 51) |
| `src/Features/Activities/Camp/CampActivityStarter.cs` | Lifecycle starter |
| `src/Features/Activities/Camp/CampActivityMenuProvider.cs` | Slot-bank provider |
| `src/Features/Activities/Camp/CampActivityTypeJson.cs` | Two-phase template loader |
| `ModuleData/Enlisted/Activities/camp_activity.json` | Two-phase ActivityTypeDefinition (see "Activity timing" below) |
| `ModuleData/Enlisted/ServiceStances/service_stances.json` | Stance definitions, preview text, drift rules, interruption pools |
| `ModuleData/Enlisted/Storylets/camp_*.json` | Short camp override pools |
| `ModuleData/Enlisted/Storylets/incident_response_*.json` | Incident response chains |
| `ModuleData/Enlisted/Effects/scripted_effects_preview.json` | Preview-metadata addendum (optional authoring field) |

---

## Activity override timing (fix for v1 blocker #3)

v1 assumed camp activities have 2–12h durations and resolve time-gated. `ActivityRuntime.ResolvePlayerChoice` at `ActivityRuntime.cs:379-383` calls `AdvancePhase` immediately, so a single `PhaseDelivery.PlayerChoice` phase resolves right away. Fix: **two-phase short override activity**.

```
Phase 0: "pick_override"      — PhaseDelivery.PlayerChoice, duration 0
                                Player selects the short override (Rest / Patrol / Attend / etc.)
                                ResolvePlayerChoice advances to Phase 1 immediately
                                Intent is locked onto the Activity

Phase 1: "execute_<intent>"   — PhaseDelivery.Auto, duration = intent-specific
                                  (Drill: 3h, Rest: 12h, Tend: 6h, Patrol: 6h,
                                   Scheme: 4h, Attend: 2h, Study: 2h)
                                fire_interval_hours = 0 (fire once at end)
                                pool contains resolution storylets for that intent
                                On duration elapse or fire, Auto runtime resolves
                                  → effects apply via StateMutator
                                Activity ends (OnBeat "activity_complete" pops envelope)
```

This matches the `FireIntervalHours` + phase-duration contract already in the runtime (see `Activity.LastAutoFireHour` gating at `storylet-backbone.md` Phase Flow). Home Evening is a one-phase shortcut because its player-choice IS the resolution; short camp overrides separate intent from timed execution because the physics needs the time envelope to justify the reward.

Short overrides are not the default loop. The stance layer carries routine time; overrides are for "I want to do this specific thing now."

---

## Story content + news/status integration

This spec must consume the existing content library, not fork around it. Every authored story surface gets an envelope role:

| Existing content | Envelope role | Surface behavior |
| :--- | :--- | :--- |
| `ModuleData/Enlisted/Storylets/duty_*.json` | Service-stance drift, stance interruptions, and order-phase color | Low-stakes entries batch into stance summaries; high-stakes entries become Modal candidates. |
| `ModuleData/Enlisted/Storylets/order_*.json` | Order envelopes | Acceptance preview appears in `UPCOMING`; order-phase beats continue through `StoryDirector`. |
| `ModuleData/Enlisted/Storylets/floor_*.json` | News/status flavor and low-priority pertinent dispatch | Used for stance summaries, `YOU` prose, and camp atmosphere; no direct state writes. |
| `culture_overlays.json` / `lord_trait_overlays.json` | Modifiers and prose overlays | Adjust stance/order storylet weights and copy; do not create independent mutation paths. |
| `home_*.json` / `home_evening.json` | Home surface activities | Remain Home-specific. They can read the current service stance for flavor but do not replace it. |
| `path_crossroads*.json` / `path_*_t7_variants.json` | Modal/order career beats | Modal or order envelopes with explicit previews for path commitments/resists. |
| Legacy `ModuleData/Enlisted/Events/*.json` incidents | Incident modal migration source | Converted to `IncidentResponseModal` or pertinent news. |
| `Decisions/camp_opportunities.json` and `camp_decisions.json` | Candidate source for short overrides and stance-change affordances | Existing opportunities become either one-shot overrides or stance-interruption hooks. |

### Typed dispatch routing contract

Agency/news integration adds typed routing metadata that survives from storylet/event emission into `DispatchItem` and the menu builders. This is intentionally smaller than a full event-meaning rewrite.

```csharp
public enum DispatchDomain
{
    Kingdom,
    Personal,
    Camp
}

public enum DispatchSourceKind
{
    Unknown,
    ServiceStance,
    Order,
    ActivityOverride,
    ModalIncident,
    Routine,
    Battle,
    Muster,
    Promotion,
    Condition,
    Flavor
}

public enum DispatchSurfaceHint
{
    Auto,
    Dispatches,
    Upcoming,
    You,
    SinceLastMuster,
    CampActivities,
    ModalOnly
}
```

Rules:

- `DispatchDomain` decides the feed family: realm feed, personal feed, or camp-scope display.
- `DispatchSourceKind` explains what produced the item.
- `DispatchSurfaceHint` is a preferred display surface, not an effects gate. `AgencyGate` still decides what mutations are legal.
- `StoryCandidate` carries these fields for new emissions. `DispatchItem` persists them so `EnlistedMenuBehavior` and `MusterMenuHandler` do not infer meaning from `HeadlineKey`, `Severity`, or formatted English.
- Existing `Tier`, `Beats`, `Severity`, `StoryKey`, and `Body` stay. Typed routing metadata is additive.

| Content | Domain | SourceKind | SurfaceHint |
| :--- | :--- | :--- | :--- |
| War declared | `Kingdom` | `Flavor` or `ModalIncident` | `Dispatches` |
| Two days drilling summary | `Personal` | `ServiceStance` | `You` |
| Current scout order accepted | `Personal` | `Order` | `Upcoming` |
| Scout order completed | `Personal` | `Order` | `SinceLastMuster` |
| Extra Drill resolved | `Camp` | `ActivityOverride` | `CampActivities` |
| Wound fever choice | `Personal` | `ModalIncident` | `ModalOnly`, then `You` / `SinceLastMuster` outcome |
| Floor rumor with no effect | `Camp` or `Kingdom` | `Flavor` | `Auto` |

### Enlisted status menu contract

The stance system plugs into the news/status unification spec rather than adding another competing menu surface:

- `enlisted_status` keeps the main `DISPATCHES` + `UPCOMING` structure. `UPCOMING` adds a compact **Current stance** line because stance is part of "what happens next" for the player: "Current stance: Drill with the Line. Orders override it."
- `DISPATCHES` only reads `DispatchDomain.Kingdom`. Stance summaries, order outcomes, personal condition changes, and camp overrides never enter the kingdom digest.
- `UPCOMING` is a live forecast, not a historical feed. It is built from current state: active order, current stance, next commitment, muster timer, world pressure, and pending player-action hooks.
- `enlisted_camp_hub` remains the player-scope surface: `YOU`, `SINCE LAST MUSTER`, `CAMP ACTIVITIES`.
- Camp hub `YOU` reads `DispatchDomain.Personal` and prefers `SurfaceHint.You`. It shows current stance, condition, active order override, and up to 3 personal-feed items with `[NEW]` semantics from the news spec.
- Camp hub `SINCE LAST MUSTER` reads period-bounded personal items, especially `Order`, `ServiceStance`, `Battle`, `Condition`, `Promotion`, `Muster`, and `ModalIncident`. It must not lose the current 12-day recap contract.
- Camp hub `CAMP ACTIVITIES` shows company state plus short override options. It can show recent `ActivityOverride` outcomes when they explain camp state. It is no longer the core activity loop.
- `Flavor` items appear only when they add texture and do not crowd out consequence items.

### News feed contract

Stance outcomes route through the personal feed, not the combat log and not a new feed:

- Add a producer method such as `EnlistedNewsBehavior.AddStanceSummary(...)` or reuse `AddPersonalDispatch(...)` with typed `DispatchDomain`, `DispatchSourceKind`, and `DispatchSurfaceHint` arguments.
- Batch routine stance changes every 2-3 in-game days or at muster boundaries, whichever comes first.
- Headline-tier personal items are reserved for high-stakes stance interruptions, order outcomes, promotions, wounds, and discipline events.
- Low-stakes summaries render as condensed prose in `YOU` and `SINCE LAST MUSTER`: "Two days drilling: sword work improved; readiness held."
- `DISPATCHES` stays realm-scope. Stance summaries never enter the kingdom feed.

The design goal is: fast-forward produces readable accumulated state in the status/news surfaces, while only meaningful authored choices interrupt time.

---

## Time-control taxonomy (authoritative, decompile-verified)

Bannerlord exposes exactly three map speeds via `Campaign.Current.TimeControlMode` (a `CampaignTimeControlMode`). Seven enum values; tick math at `Campaign.cs:843-875` buckets them by the `num` assignment:

| Behavior | Enum values | `TickMapTime` `num` |
| :--- | :--- | :--- |
| **Paused** | `Stop`, `FastForwardStop` | `0` |
| **Play (1×)** | `StoppablePlay`, `UnstoppablePlay` | `0.25 × realDt` |
| **Fast-forward** | `StoppableFastForward`, `UnstoppableFastForward`, `UnstoppableFastForwardForPartyWaitTime` | `0.25 × realDt × SpeedUpMultiplier` (default `4f`) |

**Play-family** (player reading or game not advancing): `Stop`, `StoppablePlay`, `UnstoppablePlay`, `FastForwardStop`.
**Fast-family**: `StoppableFastForward`, `UnstoppableFastForward`, `UnstoppableFastForwardForPartyWaitTime`.

`SpeedUpMultiplier` default `4f` (`Campaign.cs:373, :615`); settable at runtime. Unstoppable variants are game-forced (army-wait, hideout). Treat Unstoppable/Stoppable siblings identically for envelope logic.

---

## Systems converted (fuller migration table, replaces v1's narrow list)

| System | Current behavior | New behavior |
| :--- | :--- | :--- |
| `CompanySimulationBehavior.ProcessRosterRecovery` / `ProcessNewConditions` | Daily random sickness/desertion roll with auto-applied roster mutations | Delete auto-apply. Produce candidate events; surface via `IncidentResponseModal`. |
| `CompanySimulationBehavior.ProcessIncidents` | Daily incident rolls with auto-apply | Refactor to candidate emission only. Modal responses apply effects. |
| `CompanySimulationBehavior.CheckPressureArcEvents` | Calls both `StoryDirector.EmitCandidate` (Modal) AND `EventDeliveryManager.QueueEvent` (non-interactive pertinent) at `:702, :723` | Convert all to Modal-or-previewed. Replace the non-interactive `QueueEvent` arm with `StoryDirector.EmitCandidate(Pertinent)` that renders a news line but does NOT write state. |
| `PlayerConditionBehavior.ApplyDailyRecoveryAndRisk` | Daily countdown + auto-raise MedicalRisk | Under service stance envelope: allow tiny Trivial-band auto-healing based on stance (`Help the Wounded` helps more; `Forage and Hustle` helps less). Anything faster requires Rest / Tend / Seek Medic short override. MedicalRisk auto-raise becomes Modal ("wound fever — [seek medic / ride it out]"). |
| `EscalationManager.ApplyPassiveDecay` | Time-based scrutiny decay | Delete generic time-based decay. Scrutiny drops through `Keep Your Head Down`, Patrol, or honorable-order resolves. Very stale scrutiny can fade at Trivial band only when the current stance allows it. |
| `PathScorer.OnHeroGainedSkill` | Every vanilla skill XP bump → path score | Delete. Path bumps only via `OnIntentPicked` (intent-scoped) + order resolves that preview their path tag. |
| `EnlistmentBehavior.OnDailyTick` (wage, supply, desertion, leave, captivity, grace period) | Many state writes | Split: **Service stance envelope allows** wage ledger accrual, daily supply consumption, company-supply tick, and stance-specific Trivial drift. **Service stance denies** desertion-penalty application (must modal: "X days AWOL — [rejoin / accept penalty]"). Grace period + captivity state sync are read-only and allowed. |
| `EnlistmentBehavior.OnHourlyTick` (party visibility, sea state) | Read-only sync | Keep. No mutations. |
| `CampRoutineProcessor.ApplyOutcome` | Routine XP / gold / supply / news writes, triggered by phase-end | Becomes stance-summary producer plus short-override resolver. Routine outcomes batch into personal feed; short override effects apply through `StateMutator`. |
| `EffectExecutor.Apply` primitives | Direct `QualityStore.Add`, `FlagStore.Set`, `GiveGoldAction`, skill XP, relation changes | Route every primitive through `StateMutator.Try{…}`. Adds one indirection layer; no behavior change under normal storylet flow because storylets run inside envelopes. Calls from non-envelope contexts (debug hotkeys, boot) get explicitly whitelisted via a `StateMutator.ApplyBootstrap(...)` escape hatch. |
| `EventDeliveryManager` direct hooks (baggage, raid tracking) | Direct mutations | Audit. Convert any state write to route through `StateMutator`; the `Modal` these ride on IS the envelope. |
| `DailyDriftApplicator` | Phase-3 Signal Projection applies drift to qualities on daily tick | Gate on current service stance and active order. Drift magnitudes are Trivial-band by design and summarized in personal feed. |
| `QualityBehavior.OnDailyTick` (decay) | Rule-based decay from `quality_defs.json` | Keep, but remove decay blocks from player-facing qualities (scrutiny, readiness, supplies, medical_risk, loyalty). Decay survives only for housekeeping. |
| `FlagStore.OnHourlyTick` (expiry) | Data plumbing | Keep. |
| `StoryDirector.OnDailyTick` (pacing, retries) | Content delivery | Keep. |
| `EnlistedCampaignIntelligenceBehavior.HandleHourlyTick` | Snapshot recompute + candidate emission | Keep snapshot (read-only). Candidate emission must be Modal-or-Pertinent-news, never direct state write. |
| `CampOpportunityGenerator.OnHourlyTick` (scheduled commitments) | Commitments fire on time | Scheduled commitments become either stance interruptions, order proposals, or short override envelopes. Before fire, player saw the commitment scheduled or the current stance allowed it. |
| `CampLifeBehavior.OnDailyTick` + listeners | Pressure recompute | Keep as read-only. |
| Menu-path gold writes (`CampMenuHandler`, `QuartermasterManager`) | Direct `GiveGoldAction.Apply…` | Route through `StateMutator.TryGiveGold`. Menu clicks ARE player-initiated Modal envelopes. |
| Pension accrual + discharge reward writes | Direct gold + flag writes | Route through `StateMutator`. Discharge ceremony IS the Modal envelope. |

**Validator rail addition:** a new Phase in `validate_content.py` scans `src/` for direct writes to player-facing stores (`QualityStore.Set/Add`, `FlagStore.Set`, `Hero.AddSkillXp`, `GiveGoldAction.ApplyBetweenCharacters`, `ChangeRelationAction.Apply...`, `hero.ChangeHeroGold` if any snuck back in) and flags any site that is not inside `StateMutator`. This is the "compile-time discipline" that prevents regression.

---

## What stays passive (correctly)

- Kingdom / faction / lord events (war, siege, clan leader death). Fire as Modals requesting a response.
- Catalog loading, JSON hot-reload, save/load plumbing.
- Intelligence snapshot recompute (read-only).
- Flag expiry sweeps.
- Pressure / world-state computation (read-only).

World-scope mutation writes are gated: a kingdom war declaration CAN emit a news-feed item (the news feed is a display) but must go through Modal to mutate anything player-facing.

---

## Stance and override UX

The main anti-chore rule is structural: **stance is persistent; short overrides are optional.** The player should not have to click every few in-game hours to feel progress.

Short overrides still need rails:

1. **Per-override cooldown** (`ChoreThrottle`): picking the same override within N in-game hours is blocked with a flavor message.
2. **Diminishing returns**: repeating the same override within a muster period halves the reward each time.
3. **Lord patience**: repeated Attend Lord overrides flip from opportunity to annoyance.
4. **Failure chance**: Patrol / Scheme / Study carry authored failure branches that subtract instead of add. Failure outcomes are previewed at acceptance.
5. **Opportunity cost**: an override advances time and temporarily suppresses stance drift.

Stances should have soft friction rather than hard cooldowns. Repeated stance changes inside one day may reduce summary yield or trigger an NCO comment, but should not block the player from correcting a bad posture.

---

## FF pacing fix (fix for v1 blocker #4)

Three layers:

1. **Service stance persists across FF**. At FF in a peaceful garrison, current stance is active → wage accrues, supply consumes, Trivial-band stance drift runs. State is moving but nothing high-stakes lands without a modal.
2. **Modal prompts interrupt FF** (vanilla behavior — `Campaign.Current.TimeControlMode = Stop` on Modal display). So any high-stakes event forces the player into play-family at the moment of decision.
3. **No forced reprimand for passive waiting by default.** If the player has no selected stance, `Routine Service` is active. After 5-7 in-game days with no stance change, no order, and no modal, show an inline status/camp suggestion: "You've kept to routine service. Choose a stance if you want to shape your career." Reprimand modals are reserved for negligent stances, missed direct orders, or authored discipline failures.

Combination: player CAN FF through peaceful garrison, earning stance-appropriate summaries. The game only stops them when a real choice matters.

---

## Save / migration (fix for v1 blocker #1)

**Envelope derivation is not persisted.** On save-load, `AgencyGate.GetCurrentEnvelopes()` recomputes by reading:

1. `EnlistmentBehavior.IsEnlisted` + `ServiceStanceManager.Current` → adds ServiceStance envelope if true.
2. `ActivityRuntime._active` (persisted via `SyncData` at `ActivityRuntime.cs:52`) → for each active activity, derives an Order or ActivityOverride envelope.
3. `StoryDirector` active-modal state (if modal is mid-display on save) → derives Modal envelope.

A save mid-order reloads with the order still active (`_active` list restored) → `GetCurrentEnvelopes` returns the Order envelope above the saved service stance → resolve-phase effects apply cleanly. No ephemeral scope stack to worry about.

**`ServiceStanceManager.Current`** persists as a string through `SyncData`; no save-definer offset. **`ChoreThrottle`** per-(category, source) cooldowns DO persist (save-definer offset 52).

**Existing saves:** accumulated `scrutiny`, `readiness`, `supplies`, `medical_risk` values load as-is. Decay rules removed → values persist until a player action changes them. One-shot migration on first load after patch: zero any in-flight `InjuryDaysRemaining` / `IllnessDaysRemaining` above 7 (prevents "old save healed you via deleted countdown" confusion).

**Save-definer offsets claimed:**
- 51 — `CampActivityActivity`
- 52 — `ChoreThrottleStore`

---

## Storylet schema addendum

Add an optional `preview` field to storylets used as envelope entry points (order-accept, camp override entry, modal display). Service stance previews live in `service_stances.json`. Add an `agency` metadata block to classify routing and authoring rules.

```json
{
  "id": "order_scout_ranger_t1",
  "agency": {
    "role": "order_accept",
    "domain": "personal",
    "sourceKind": "order",
    "surfaceHint": "upcoming"
  },
  "preview": {
    "grants": ["scouting_xp_moderate", "ranger_path_bump_small"],
    "may_cost": ["fatigue_minor", "small_wound_risk"],
    "risks": ["ambush_encounter"]
  },
  ...
}
```

`EnvelopeAcceptancePreview` reads this and renders it into the acceptance dialogue / modal body so the player SEES the contract before consenting. Storylets without a `preview` block are treated as "Trivial contract — no advertised consequences beyond the storylet's own option effects."

`agency.role` values:

| Role | Use | Default route |
| :--- | :--- | :--- |
| `stance_drift` | Low-stakes accumulated stance progress | `Personal / ServiceStance / You`, batched |
| `stance_interrupt` | A stance-created choice or complication | Modal, then `Personal / ServiceStance / You` |
| `order_accept` | Offer/acceptance text for an order | `Personal / Order / Upcoming` |
| `order_phase` | Mid-order beat | `Personal / Order / You` or Log/Pertinent |
| `order_outcome` | Order completion/failure | `Personal / Order / SinceLastMuster` |
| `activity_override` | Short camp action outcome | `Camp / ActivityOverride / CampActivities`, optionally Personal |
| `modal_incident` | Player-facing choice with consequences | Modal, then Personal |
| `news_flavor` | Observational color only | Auto, no player-state writes |
| `realm_dispatch` | War/siege/kingdom information | `Kingdom / Flavor / Dispatches` |

Hard authoring rules:

- `news_flavor` cannot have effects. Validator rejects it.
- `realm_dispatch` cannot mutate player state.
- `stance_drift` can only use Trivial/Minor effects and must be batchable.
- `stance_interrupt`, `modal_incident`, and Moderate+ outcomes require preview text.
- `order_accept` and `activity_override` require preview text because they are explicit opt-ins.
- Existing legacy `EventDefinition` JSON can be migrated gradually by adapter rules, but new content should use storylet `agency` metadata.

Stance batching contract:

- Individual `stance_drift` storylets do not each create visible news.
- They write into a `StanceSummaryAccumulator`.
- Every 2-3 in-game days, at muster, or when the player opens camp after a long FF stretch, the accumulator emits one personal dispatch:
  - `Domain = Personal`
  - `SourceKind = ServiceStance`
  - `SurfaceHint = You`
  - `Tier = Log` or `Pertinent`
  - `Body = condensed prose + key numbers`

Validator addition: Phase 16 (new) — any storylet with effects whose declared magnitude exceeds Trivial MUST carry a `preview` block. Missing preview on a high-stakes storylet is a build error.

---

## Playflows (v3 stance loop)

### Playflow 1 — Fresh recruit, Day 1, peaceful garrison

1. Enlistment dialogue → `Routine Service` becomes active. Preview: daily wage, routine supply use, tiny condition drift, and no major personal consequences without an order, stance, activity override, or modal.
2. Main menu: `DISPATCHES` + `UPCOMING`. `UPCOMING` shows `Current stance: Routine Service`. Camp → `YOU` shows the same stance plus the latest personal-feed item.
3. Player selects **Drill with the Line**. `ServiceStanceManager.Current = drill_line`; status/camp preview shows likely skill XP, readiness gain, fatigue risk, and no relation/path writes.
4. Player fast-forwards 2 in-game days. No modal spam. The stance creates one batched personal-feed summary. `YOU` marks it `[NEW]`; `SINCE LAST MUSTER` includes the rollup.
5. Player sees a short **Extra Drill** camp override. Accepting it pushes `ActivityOverride` for 3h, resolves a focused storylet, then returns to **Drill with the Line** automatically.

### Playflow 2 — Scout order arc

1. `UPCOMING` shows "Scout the eastern ridge — grants: `scouting_xp_moderate`, `ranger_path_bump_small`; may cost: `fatigue_minor`; risks: `ambush_encounter`. [Accept]" (preview rendered from storylet's `preview` block).
2. Accept → Order envelope pushes. 3-day arc. Current service stance remains saved underneath but does not tick while the order owns the player.
3. Day 2: mid-arc Modal fires ("tracks in the mud"). Modal envelope pushes on top. Player picks Follow. Modal envelope resolves → mutations apply under the modal's declared contract → Modal envelope pops.
4. Day 3: arc resolves. Order envelope pops. Previous service stance resumes and `YOU` gets an order-completion dispatch.

### Playflow 3 — Kingdom war mid-activity

1. Player in **Drill with the Line**. Vlandia declares war (world-scope passive event).
2. Event emits a Modal candidate ("runners cry through camp — read the proclamation"). `StoryDirector` queues it. Game does NOT force-pause because the Modal isn't immediate-impact (read-only lore).
3. The next stance-summary window closes. Proclamation Modal fires after. Player reads. No state mutation — the Modal's effects list is empty; this is informational.
4. Day later, Named order "Join the march on Pravend" proposes. Order envelope acceptance preview warns: "may cost `march_fatigue_major`, risks `front_line_battle`." Player opts in or out.

### Playflow 4 — Wounded, recovery

1. Battle storylet wrote `InjurySeverity=Moderate, days=7` via the gate under a Modal envelope. Scope popped.
2. Under `Routine Service`, Trivial-band auto-heal is allowed very slowly. Previewed at enlistment and summarized only at muster or when condition changes visibly.
3. Player wants faster recovery → chooses **Help the Wounded / Light Duty** stance if eligible, or accepts a short **Rest in Camp** override: 12h scope, resolves `InjuryDays -= 2`.
4. Repeated Rest overrides hit `ChoreThrottle`; staying in a recovery stance is the non-annoying long-running option.

### Playflow 5 — Scrutiny pressure

1. Scrutiny at 68. No time-based decay.
2. `UPCOMING`: "Lieutenant requests to speak. [Attend]" — preview: "may grant `scrutiny_down_major`, may cost `relation_down_small`, risks `caught_in_lie_flag`."
3. Modal fires with options. Player picks. Effects under the modal envelope.
4. Alternatively the player selects **Keep Your Head Down**. After a few days, a personal-feed summary reports small scrutiny relief and lost opportunity for XP/readiness.
5. If scrutiny stays >= 70 for 14 days without change, only stances that plausibly address it can create tiny decay. `Routine Service` does not magically erase pressure.

### Playflow 6 — T4 path crossroads

1. Rank 4 reached. `PathCrossroadsBehavior` emits Modal via `StoryDirector.EmitCandidate(ChainContinuation=true)`. Modal envelope pushes.
2. Storylet has its own `preview` block: "Commit grants `+1 focus(Bow) / +10 renown / commits path=ranger permanently`. Resist grants `path_resisted_ranger flag (halves future score bumps)`."
3. Player picks Commit. `commit_path` primitive fires through `StateMutator.TryWriteFlag` under the Modal envelope's contract. Envelope pops.

---

## Phasing (v3)

- **Phase 1 — Define + instrument.** Author `MutationCategory` + `MagnitudeBand` + `Envelope` + `AgencyGate` + `StateMutator`. Make `StateMutator.Try{…}` observer-only: calls underlying stores and logs what would have been rejected. Add `validate_content.py` Phase 16 (preview-on-high-stakes). Deploy; collect one week of logs.
- **Phase 2 — Audit feedback.** Read Phase 1 logs. Any unexpected mutation sources (mods that write to `QualityStore` directly, boot-time writes) get explicit whitelist entries via `StateMutator.ApplyBootstrap(...)`. Tune magnitude-band thresholds.
- **Phase 3 — Convert menu + effect-executor paths.** `EffectExecutor` primitives route through `StateMutator`. Menu gold writes route through `StateMutator`. Routine outcomes route through `StateMutator`. Still observer mode.
- **Phase 4 — Flip gate to enforce.** `StateMutator.Try{…}` now rejects mutations that fail category / magnitude check. Top-5 passive handlers either get proper envelopes, get removed, or get converted to Modal-or-Pertinent-news emission. Player-facing quality decay rules deleted.
- **Phase 5 — Ship service stances.** Add `ServiceStance`, `ServiceStanceManager`, `service_stances.json`, save/load of current stance, and stance selection from `enlisted_status` / `enlisted_camp_hub`.
- **Phase 6 — Wire typed news/status routing.** Add `DispatchDomain`, `DispatchSourceKind`, and `DispatchSurfaceHint` to `StoryCandidate` / `DispatchItem` / `EnlistedNewsBehavior.AddPersonalDispatch`. Show current stance in `UPCOMING` and `YOU`; include stance rollups in `SINCE LAST MUSTER`; keep `DISPATCHES` realm-scope.
- **Phase 7 — Ship short activity overrides.** Two-phase template, 5-7 optional slots, `ChoreThrottle` save-definer offset 52, preview metadata on every slot. Overrides return to the previous stance.
- **Phase 8 — Convert incident handlers.** `CompanySimulationBehavior.Process{RosterRecovery,NewConditions,Incidents}` rewired through `IncidentResponseModal`. `CheckPressureArcEvents` converts the `QueueEvent` arm to Pertinent-news.
- **Phase 9 — Content tuning.** Re-balance rewards so stance progression pace is acceptable under fast-forward. Author missing previews on existing orders and classify migrated storylets by `agency.role` + typed dispatch metadata.
- **Phase 10 — FF handling.** Verify fast-forward produces batched stance summaries, high-stakes modal interrupts, and soft status suggestions after long Routine Service. No default forced reprimand.

Budget: 3–4 weeks. Each phase is reviewable independently.

---

## Risks

1. **Service stance too permissive.** If Routine Service or any stance allows too much, we're back to v1's complaint. Mitigation: Phase 2 tune from Phase 1 log data; Trivial band capped tightly and relation/path writes stay opt-in only.
2. **Preview-authoring burden.** Every high-stakes storylet needs a `preview` block. Phase 7 content pass, Phase 16 validator catches misses.
3. **Migration surface risk.** Phase 3 routes many mutation sites through `StateMutator`. Observer mode (Phase 1–3) catches surprises before Phase 4 enforces.
4. **Modal fatigue.** If we modal every incident, players get spam. Mitigation: `StoryDirector` pacing already handles this via per-category cooldowns.
5. **Plan 1–5 smoke regressions.** Scenarios A–H must re-run post-Phase 4. Envelope-lookup on `ActivityRuntime._active` is cheap (O(active count), typically 0–2).
6. **FF in service stance still invisible.** Wage accrues, supply consumes, and stance drift happens quickly. Mitigation: personal-feed stance summaries every 2-3 in-game days or at muster, plus compact status-menu counters.
7. **Content classification drift.** Existing duty/floor/home/order/path storylets can be routed to the wrong surface. Mitigation: require `agency.role` plus typed `DispatchDomain` / `DispatchSourceKind` / `DispatchSurfaceHint` during migration and validate flavor-only storylets cannot mutate player state.
8. **Status menu clutter.** Adding stance, dispatches, recaps, and camp overrides can overfill the menus. Mitigation: `UPCOMING` gets one compact stance line; `YOU` gets current stance + up to 3 personal-feed items; `SINCE LAST MUSTER` stays the recap home; `CAMP ACTIVITIES` stays optional.
9. **ChoreThrottle authoring burden.** Every short override needs a cooldown key. Keep shared throttles where possible (all Extra Drill storylets share `drill_daily` throttle, etc.).

---

## Testing

| Test | Method | Expected |
| :--- | :--- | :--- |
| **Routine FF baseline** | Enlisted + lord stationary + no order/activity. FF for 2 in-game days. | `Routine Service` active → wage/supply/allowed tiny drift observed, no high-stakes state change, one batched personal-feed summary at threshold/muster. |
| **Stance selection persists** | Select Drill stance → save → reload → open status. | `ServiceStanceManager.Current == drill_line`; `UPCOMING` and `YOU` render current stance. |
| **Stance paused by order** | Drill stance active → accept scout order → resolve order. | Order effects apply; Drill stance does not tick during order; Drill resumes after order completion. |
| **Scope-matched mutation** | Accept scout order → run to resolve. | Mutations match storylet's `preview` block's `grants` / `may_cost`. Nothing in excess. |
| **Envelope stack derivation on save-load** | Save mid-order → reload → observe envelope stack. | Order envelope derived correctly from `ActivityRuntime._active`. Resolve-phase effects land. |
| **Activity override two-phase** | Pick Extra Drill from optional slot bank → time advances 3h → end-storylet fires. | `StateMutator` applies effects; override ends and previous stance resumes cleanly. |
| **ChoreThrottle cooldown** | Extra Drill → immediately attempt Extra Drill again. | Slot rejects with "sergeant frowns." |
| **Preview rendering** | Accept an order with `preview` block. | Acceptance dialogue shows grants / costs / risks. |
| **Non-previewed high-stakes storylet** | Author a test storylet with Major-band effects and no `preview`. | `validate_content.py` Phase 16 rejects at build. |
| **Incident modal** | Force incident candidate → verify modal renders, effects apply only on pick. | Pass. |
| **Personal-feed routing** | Force stance summary, order completion, and realm dispatch. | Stance/order items appear under `YOU` / `SINCE LAST MUSTER`; realm dispatch appears under `DISPATCHES`; combat log unchanged. |
| **Typed dispatch routing survives** | Emit one item for each source kind: stance, order, activity override, modal incident, realm dispatch. Save/reload, then open status/camp. | `DispatchItem` retains domain/source/surface fields; menu builders route without `HeadlineKey` or rendered-text inference. |
| **Soft Routine suggestion** | Stay in Routine Service 5-7 in-game days with no order/modal. | Inline status/camp suggestion appears; no forced pause or discipline modal. |
| **Plans 1–5 scenarios A–H** | Re-run all. | All pass. |

---

## Open questions

1. **Service stance Trivial bands per category** — specific numbers need Phase 1 log data. Start with: skill XP ≤ 10 per real-game-day on focused stances; gold ± ≤ 5; condition day ≤ 0.15 / real-game-day; scrutiny ± ≤ 1 per 14 days only on relevant stances; no path or relation writes.
2. **Pension accrual** — lives in service stance or order envelope? Lean stance (it's continuous) — but then the "paid at muster" modal IS what prevents the bookkeeping from feeling unearned.
3. **How many short overrides Phase 7** — 7 in spec; fallback to 5 if authoring is slow (drop Scheme, Study; add them later).
4. **What about `PathScorer.OnHeroGainedSkill`** — deletion is correct under the new contract (path bumps are Major-band, need explicit intent). But Plans 1–5 rely on path scores existing; the storylet-backbone spec may need a pointer update.
5. **`validate_content.py` Phase 16 — which `MagnitudeBand` threshold triggers "preview required"?** Lean: Moderate and above require preview. Trivial / Minor are exempt.
6. **ChoreThrottle save-definer offset 52** — verify no collision with Plan 4's `DutyCooldownStore` at 50 and Plan 1's `EnlistedLordIntelligenceSnapshot` at 48. Gap at 51 for `CampActivityActivity`. 52 is free.
7. **Status menu density** — final copy/layout must be tested at narrow resolutions. If cramped, keep current stance in `UPCOMING` and put detailed stance history only in `YOU` / `SINCE LAST MUSTER`.

---

## Relation to other specs

- **News-unification spec v2** (`docs/superpowers/specs/2026-04-23-news-and-status-unification-design.md`): integrated, not paused. `DISPATCHES` remains realm-scope; `UPCOMING` gains current stance + pending order/activity hooks; `YOU` owns current stance and recent personal-feed items; `SINCE LAST MUSTER` owns stance/order rollups; `CAMP ACTIVITIES` owns optional short overrides and company-state context.
- **Plan 5 Career Loop** (`docs/superpowers/plans/2026-04-21-career-loop-closure.md`): crossroads already scope-correct (Modal envelope on fire). No change. `PathScorer.OnHeroGainedSkill` deletion removes the auto-bump path, so path scores will accumulate slower — authors may want to tune intent-pick bumps upward (current `+3` in `PathScorer.cs:86`).
- **Plan 1 Campaign Intelligence**: snapshot stays read-only.
- **Plan 4 Duty Opportunities**: ambient duty storylets emit via `StoryDirector` as Modal candidates — already envelope-correct. Audit Phase 3 to confirm no direct writes bypass the gateway.
- **Plan 2 Lord AI**: zero player-state mutations, only biases `MBGameModel`. Unaffected.

---

## Changelog vs v1/v2

- **Contract reframed** from "no state change between decisions" to "no unpreviewed high-stakes consequences." Baseline service physics allowed inside envelopes.
- **Envelope model** replaces `DecisionScope` stack. Envelopes derive from persistent state (`ActivityRuntime._active`, `EnlistmentBehavior.IsEnlisted`, `StoryDirector` modal state) — no separate ephemeral stack to persist. Fixes save/load.
- **Service stances** replace v2's generic Service envelope as the default enlisted loop. Player chooses a persistent posture, fast-forwards naturally, and receives batched personal summaries.
- **Migration table widened** to include `EffectExecutor`, `EventDeliveryManager`, `CampRoutineProcessor`, `DailyDriftApplicator`, menu gold paths, pension, discharge. Plus a new `validate_content.py` Phase 16 that blocks new direct writes at build time.
- **Camp activities demoted** to short activity overrides (instant pick-intent + timed execute-intent) to match `ActivityRuntime.ResolvePlayerChoice` actual runtime behavior without making them the core loop.
- **Status/news integration added**: typed dispatch routing, current stance in `UPCOMING` and `YOU`, personal-feed stance summaries, muster rollups, and realm dispatch separation.
- **Override-spam mitigations** retained only for optional short overrides: `ChoreThrottle` cooldown, diminishing returns, lord patience, failure chances, opportunity cost.
- **FF-safe pacing** now means persistent stance summaries + modal interrupts + soft Routine Service nudges, not a default idle-reprimand.
- **False "keep" claims fixed.** `CheckPressureArcEvents` needs its `QueueEvent` arm converted. `EnlistmentBehavior.OnDailyTick` explicitly split between Service-allowed (wage/supply) and Modal-required (desertion).
- **Preview metadata** added to storylet schema; rendered at envelope acceptance; enforced by validator Phase 16.
- **Two save-definer offsets claimed**: 51 (`CampActivityActivity`), 52 (`ChoreThrottleStore`). Was 1 in v1.
