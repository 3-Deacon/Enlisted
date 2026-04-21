# Enlisted Campaign Intelligence — Design Spec

**Date:** 2026-04-21
**Status:** Draft
**Scope:** Add an enlisted-only campaign-intelligence backbone that makes the enlisted lord behave more strategically on the campaign map while simultaneously generating soldier-facing work and grounded story signals from the same live campaign state. Phase 1 applies only to the enlisted lord, only while the player is actively enlisted, and reverts to vanilla immediately when enlistment ends.

---

## 1. Problem

The current Enlisted campaign experience has three separate weaknesses that compound each other:

1. The enlisted lord still inherits too much vanilla campaign stupidity.
   Common failures include poor pursuit selection, weak target prioritization, low-discipline army formation, and acting on thin or poorly interpreted information.
2. Player-facing campaign life is richer than vanilla, but it is still too often downstream of broad state labels rather than the exact strategic pressures the lord is living through.
3. Story/news/event content already has pacing and routing infrastructure, but the repo does not yet have a canonical shared layer for "what is happening strategically around the enlisted lord right now" that can drive AI, player duties, and story projection together.

If these are solved separately, the result will miss the point:

- smarter AI with weak player payoff becomes invisible engineering
- more player jobs without better campaign logic becomes repetitive content on top of nonsense movement
- richer stories without a shared truth layer become semantically vague and risk contradicting live campaign state

The user direction for this design is:

- all three systems must be anchored together
- realism wins by default
- the player experiences strategy only through imperfect soldier-visible signals
- the player is still just a soldier, though higher enlisted ranks may unlock limited indirect influence through reports, warnings, and recommendations that the lord may ignore
- the campaign should usually have long quiet stretches, with both genuine campaign crises and smaller strain-driven incidents surfacing during those stretches

---

## 2. Goals

- Make the enlisted lord behave more coherently on the campaign map across movement, target choice, army management, and information use.
- Generate soldier-facing duties directly from the enlisted lord's real strategic situation instead of from a disconnected quest layer.
- Route story, news, and menu-visible content from the same assessed state, while preserving imperfect information and soldier perspective.
- Keep the experience grounded in live campaign state, while allowing authored dramatic interpretation that does not contradict reality.
- Restrict the entire phase-1 system to the enlisted lord only, and only while the player is actively enlisted.
- Revert immediately to vanilla behavior and vanilla interpretation when enlistment ends.

---

## 3. Non-goals

- No global rewrite for every AI lord in Calradia.
- No battle-AI work in this spec.
- No command UI that lets the player directly steer the lord.
- No omniscient player-facing strategic dashboard.
- No assumption that the current `ActivityRuntime`, `StoryDirector`, or downstream news/menu consumers preserve arbitrary rich runtime semantics unless this spec defines the preserved projection explicitly.
- No promise that every strategic assessment must become immediate surfaced content.

---

## 4. Core Principles

1. **One hidden truth layer, multiple imperfect outputs.**
   The system computes one enlisted-lord strategic assessment from live campaign state. AI overrides, player duties, and story routing all read from that same hidden truth. The player never sees it directly.

2. **Smarter does not mean omniscient.**
   The enlisted lord should become meaningfully better, not perfect. Personality, strain, incomplete information, and local uncertainty should still produce mistakes.

3. **The player remains operationally subordinate.**
   The player can observe, report, carry out work, survive, and at higher ranks occasionally recommend or warn. The player cannot directly control the lord's strategy.

4. **Quiet is normal.**
   The system should support long low-intensity stretches. It should not force a major strategic or story beat every few days just to prove the system exists.

5. **Real state first, authored interpretation second.**
   Story content can dramatize what soldiers believe, fear, or misread, but it cannot contradict the live campaign state that produced it.

6. **Prefer model-driven AI intervention.**
   Replace or wrap campaign models first. Use narrow Harmony patches only where vanilla behavior loops hard-code outcomes that scoring layers cannot correct.

7. **Phase-1 scope must be reversible.**
   Every override is gated behind active enlistment and enlisted-lord identity. When service ends, the rewrite stops immediately and vanilla resumes.

---

## 5. Recommended Approach

This spec adopts a hybrid of two earlier approaches discussed during brainstorming:

- **AI-first rewrite** as the primary outcome
- **shared campaign-intelligence backbone** as the substrate that makes the rewrite visible and useful to the player

This is the only version that satisfies the user's "anchor all three together" requirement without collapsing into three disconnected projects.

Core loop:

`live campaign state -> enlisted-lord intelligence assessment -> smarter enlisted-lord weighting/behavior -> imperfect soldier-visible signals and duties -> player outcomes and story routing -> updated live campaign state`

The loop is unified, but the player only experiences the right-hand side of it.

---

## 6. Architecture

### 6.1 Primary runtime pieces

#### `EnlistedCampaignIntelligenceBehavior`

Campaign behavior that exists solely to observe live campaign state while enlisted, recompute a compact strategic assessment on a fixed cadence, and expose the latest enlisted-lord snapshot to the rest of Enlisted.

Responsibilities:

- verify whether the player is actively enlisted
- identify the enlisted lord and the lord's current party/army context
- gather raw live-state inputs
- compute the assessed snapshot
- emit "recent change" flags for routing and duty generation
- clear state when enlistment ends

This behavior is the authoritative source of truth for the new system.

#### `EnlistedLordIntelligenceSnapshot`

Compact assessed state for the enlisted lord only. This is not a raw dump of TaleWorlds objects. It is a reduced model with fields meaningful to both AI and content.

Initial field families:

- `StrategicPosture`
  `march`, `shadowing_enemy`, `frontier_defense`, `relief`, `offensive_siege`, `opportunistic_raid`, `recovery`, `regroup`, `army_gather`, `garrison_hold`
- `ObjectiveType`
  `none`, `move`, `pursue`, `defend_settlement`, `relieve_settlement`, `besiege_settlement`, `raid_village`, `gather_army`, `recover`
- `ObjectiveConfidence`
  low / medium / high
- `FrontPressure`
  none / low / medium / high / critical
- `ArmyStrain`
  composite assessment driven by cohesion, food, wounded ratio, morale proxies, attached-party condition, inactivity, prisoner burden
- `SupplyPressure`
  none / tightening / strained / critical
- `InformationConfidence`
  how much reliable signal the lord plausibly has for the current objective
- `EnemyContactRisk`
  low / medium / high
- `PursuitViability`
  whether a current or candidate chase is actually catchable and worthwhile
- `RecoveryNeed`
  none / low / medium / high
- `PrisonerStakes`
  none / low / medium / high
- `PlayerTrustWindow`
  how much indirect influence the player's rank/trust currently supports
- `RecentChangeFlags`
  objective shift, new threat, siege transition, dispersal risk, confirmed scout info, supply break, prisoner update

#### `EnlistedLordAiController`

Thin coordinator that applies enlisted-only strategic improvements to the enlisted lord. This is not a replacement AI engine.

Responsibilities:

- gate all overrides by enlistment state and enlisted-lord identity
- read the latest intelligence snapshot
- use model wrappers as the first intervention layer
- invoke narrow Harmony-backed corrections only where required
- stop all intervention immediately on enlistment end

#### `EnlistedCampaignSignalBuilder`

Projects the hidden assessment into imperfect player-visible signals.

Output families:

- `rumor`
- `camp_talk`
- `dispatch`
- `order_shift`
- `quartermaster_warning`
- `scout_return`
- `prisoner_report`
- `aftermath_notice`
- `tension_incident`
- `silence_with_implication`

Each signal includes:

- source perspective
- confidence level
- timeliness
- whether it is rumor, observation, or confirmed instruction
- player relevance

#### `EnlistedDutyOpportunityBuilder`

Generates believable soldier work from the same intelligence snapshot.

Primary duty families:

- recon
- dispatch/courier
- supply and strain mitigation
- prisoner and aftermath work
- siege-adjacent operational support
- camp discipline and morale incidents

No duty is generated unless the live state and current snapshot support it.

### 6.2 Relationship to Spec 2 Orders Surface

This spec does **not** replace Spec 2's continuous duty/profile layer.

The ownership split is explicit:

- `DutyProfileSelector` and `DutyProfileBehavior` remain the authoritative continuous classifier for "what kind of service posture the enlisted lord's party is currently in."
- `DailyDriftApplicator` remains the passive background skill-XP and service-texture layer by default.
- `OrderActivity` remains the enacted-duty surface: accepted named orders, active order arcs, profile-driven drift, and related order-display state.
- `EnlistedCampaignIntelligenceBehavior` is upstream of that layer. It adds strategic assessment that can:
  - influence which opportunities are generated
  - influence which named orders are plausible or urgent
  - influence order transitions, cancellations, or revised-order moments
  - damp or temporarily bias some background outputs when a higher-priority discrete opportunity is active

Phase 1 rule:

- continuous posture and drift **compose with** discrete campaign-intelligence opportunities
- they are not superseded globally by the new builder

This preserves Spec 2 as the day-to-day enlisted service substrate while allowing the campaign-intelligence layer to inject episodic work shaped by real strategic context.

### 6.3 Integration boundary

Phase 1 touches only:

- the enlisted lord
- the enlisted lord's current army state if present
- nearby enemy or settlement context directly relevant to that lord's current objective
- player-facing soldier work and story signals derived from that local context

It does not attempt to solve global AI coherence.

---

## 7. Live Inputs to the Intelligence Backbone

The backbone should gather only live state that the engine already exposes or that Enlisted already tracks.

### 7.1 Lord and party state

- lord hero state: alive, prisoner, wounded, active
- party position, movement, target, default behavior, short-term behavior
- current settlement / besieged settlement / map event / siege event
- army membership, leader status, gathering state, cohesion, morale
- food runway and recent starvation signals
- party size ratio, wounded ratio, attached-party condition
- current naval/land state where applicable

### 7.2 Strategic neighborhood

- nearby hostile parties and estimated relative danger
- nearby allied support and relief plausibility
- threatened or recently attacked friendly settlements
- target settlement value and strategic relevance
- whether the frontier is heating up or stabilizing
- recent raid, siege, war, and peace transitions relevant to the lord's current operational space

### 7.3 Information evidence

- track evidence relevant to nearby threats or pursuits
- observed enemy buildup near settlements
- confirmed or partial garrison observations
- prisoner-of-war importance and captivity location relevance
- changes in war state, army formation, army dispersal, or settlement control

### 7.4 Player relevance

- current enlisted rank/tier
- current trust/relation window with the lord
- whether the player is in a role where better reporting is plausible
- whether the player is currently near enough to plausibly observe, carry, or affect the current situation

---

## 8. Lord Logic Design

The enlisted lord should improve across four coupled failure areas. Phase 1 treats them together because isolating one will produce obvious regressions in the others.

### 8.1 Movement and pursuit

Primary fixes:

- stop low-value chases
- stop chasing targets that are faster and operationally worthless
- stop allowing bait to drag the lord off an active front
- stop drifting into purposeless movement when stronger local needs exist

Mechanics:

- compute `PursuitViability` from speed delta, terrain/naval feasibility, distance to hostile safe zones, distance from current objective, local support strength, and likely catch window
- sharply penalize pursuits that pull the lord away from threatened friendly settlements or recovery needs
- increase abort likelihood when fresh local strategic pressure outweighs chase value

Expected result:

- the lord still chases sometimes
- but he no longer feels magnetized by every tempting target in range

### 8.2 Strategic target choice

Primary fixes:

- defend or relieve nearby friendly holdings before speculative raiding
- choose siege targets more selectively
- stop committing to offensives when local readiness is too poor
- stop ignoring frontier pressure and nearby hostile buildup

Mechanics:

- use front pressure, target value, relief urgency, food/readiness, expected enemy response, and objective confidence to reweight defend/relieve/raid/siege choices
- prefer local coherent operations over deep opportunism unless the snapshot strongly supports it

Expected result:

- the enlisted lord feels like he is trying to conduct a plausible campaign, not rolling between disconnected impulses

### 8.3 Army management

Primary fixes:

- gather armies less casually
- stop long operations with poor food or poor attached-party readiness
- recover and regroup more often after real strain
- reduce pointless cohesion bleed and idle drift

Mechanics:

- tighten army creation thresholds when supply pressure or strain is elevated
- bias toward regroup/recovery when `RecoveryNeed` exceeds operational urgency
- improve call-to-army candidate selection under food, distance, and readiness constraints
- treat army strain as behavior-changing, not just as a future failure penalty

Expected result:

- armies form less often for nonsense
- when they do form, they look more purposeful and less self-destructive

### 8.4 Information use

Primary fixes:

- react more intelligently to tracks, recent movement, frontier threat, prisoner significance, and changing settlement risk
- avoid acting as if every candidate objective is equally well understood

Mechanics:

- explicit `InformationConfidence` field for the current objective
- recent scout/track/prisoner evidence can increase confidence
- rumor-heavy or stale situations keep confidence low and bias toward caution, recon, or delay

Expected result:

- the lord sometimes hesitates or probes first instead of instantly committing
- when the player brings credible information at higher rank/trust, it can affect confidence rather than directly forcing an outcome

### 8.5 Intervention layers

Preferred order:

1. `TargetScoreCalculatingModel` wrapper
2. `ArmyManagementCalculationModel` wrapper
3. `MobilePartyAIModel` wrapper
4. `SettlementValueModel` wrapper if needed
5. narrow Harmony patches against vanilla behavior loops only where the model layer cannot prevent bad commits

Non-goal:

- do not replace the whole party-think pipeline unless later evidence proves the model layer cannot carry the required behavior quality

---

## 9. Player Duty Surfaces

Player-facing work must emerge from the enlisted lord's actual strategic situation.

### 9.1 Recon

Examples:

- shadow hostile movement
- stay near an enemy fortification long enough to build an observation report
- confirm whether a reported hostile host is real
- determine whether relief is approaching a siege
- read tracks and return with uncertain but useful observations

Why it matters:

- recon is the cleanest soldier-facing way to make information quality affect the larger campaign without violating the player's subordinate role

### 9.2 Dispatch and courier work

Examples:

- carry messages between the lord, scouts, quartermaster staff, allied settlements, or attached parties
- deliver revised orders after a front shift
- arrive too late and discover the situation already changed

Why it matters:

- dispatch work naturally supports imperfect information, delay, failure, and the feeling of being inside a moving army rather than above it

### 9.3 Supply and strain mitigation

Examples:

- escort food wagons
- recover missing stores or scattered pack animals
- pressure or protect villages in support of the column
- help the quartermaster stabilize deteriorating supply conditions
- track down stragglers or resolve local disorder caused by strain

Why it matters:

- it turns army strain into lived soldier experience instead of hidden strategic math

### 9.4 Prisoner and aftermath work

Examples:

- guard important captives
- identify politically valuable prisoners
- move prisoners to safer custody
- search for captured comrades
- respond to escape rumors or exchange messages

Why it matters:

- prisoner stakes create narrative gravity during both success and failure states

### 9.5 Siege-adjacent work

Examples:

- watch walls
- screen the camp perimeter
- escort tools and engineers
- identify weak approaches
- monitor sortie risk
- prepare for incoming relief

Why it matters:

- it gives campaign-map siege life real texture before any battle mission starts

### 9.6 Camp and discipline incidents

Examples:

- ration tension
- wounded unease
- rumor escalation
- theft
- desertion suspicion
- forager trouble
- conflicting scout stories

Why it matters:

- these smaller incidents keep long quiet stretches alive without requiring full strategic crises

### 9.7 Rank effects

- lower ranks: labor, exposure, fragmented information
- middle ranks: courier, observation, small trusted responsibilities
- higher enlisted ranks: better reporting channels, warnings, and recommendations that may bias the lord's confidence but can still be ignored

Hard limit:

- no direct strategic control at any rank

### 9.8 Relationship to `OrderActivity`

The new opportunity layer does not replace `OrderActivity`.

Ownership split:

- `OrderActivity` continues to represent accepted, active, player-lived duty arcs
- the intelligence backbone supplies upstream context that shapes:
  - which named orders become available
  - when revised orders or cancellations make sense
  - when front shifts or strain changes should surface as order transitions
- intelligence-driven duties that are not full named-order arcs may coexist beside `OrderActivity` as lighter operational work, but they must not silently fork a second competing "active order" state machine

Phase 1 rule:

- one authoritative enacted-duty layer remains in Spec 2
- campaign intelligence is the upstream assessor and opportunity source for that layer
- if a front shift produces a "revised orders" moment, it should feed through the existing order/story routing surface rather than bypassing it with a parallel hidden order system

---

## 10. Story and Routing Design

The story layer must treat strategic truth and player-visible content as different things.

### 10.1 Hidden truth

The intelligence snapshot is the authoritative internal assessment. It is not emitted directly to player-facing systems.

### 10.2 Signal projection

Signals are lossy projections of that truth. The player receives:

- rumor
- camp talk
- dispatches
- order changes
- quartermaster warnings
- scout returns
- prisoner news
- aftermath notices
- low-level tension incidents

The same underlying state may produce different signals at different confidence levels over time.

Example:

- low confidence: men whisper enemy riders were seen to the east
- medium confidence: quartermaster warns stores may need to move sooner than expected
- high confidence: orders shift and a courier confirms relief is likely coming

### 10.3 Content rule

Authored content may:

- dramatize
- interpret
- rumorize
- narrow to soldier perspective

Authored content may not:

- assert events that the live campaign state does not support
- imply strategic certainty the system does not actually have
- turn the player's limited perspective into omniscient narration

### 10.4 Routing contract

Because existing Enlisted routing already flattens context in `StoryDirector` and downstream consumers, this design requires an explicit projection contract for surfaced signals.

At minimum each projected signal must preserve:

- speaking/source perspective
- confidence level
- recency
- what changed
- what the player is expected to infer or do
- whether the signal is rumor, observation, or confirmed instruction

This avoids relying on arbitrary rich runtime payloads surviving through current consumers.

### 10.5 `StoryCandidate` metadata decision

This spec chooses the explicit-field route.

`StoryCandidate` should gain a small optional signal-metadata block rather than encoding signal meaning into `SourceId`, `CategoryId`, or rendered text conventions.

Recommended optional fields:

- `SignalKind`
  `rumor`, `camp_talk`, `dispatch`, `order_shift`, `quartermaster_warning`, `scout_return`, `prisoner_report`, `aftermath_notice`, `tension_incident`
- `SignalConfidence`
  low / medium / high
- `SignalPerspective`
  e.g. quartermaster, scout, veteran, camp rumor, direct order, aftermath witness
- `SignalRecency`
  fresh / recent / stale

Rules:

- these fields are optional for existing emit sites
- existing `StoryCandidate` producers remain valid until they opt into richer signal projection
- `StoryCandidatePersistent` should remain intentionally thin; only persist the minimum subset needed for deferred delivery or replay-safe reconstruction

This is preferred over text-convention encoding because it keeps routing contracts inspectable, avoids semantic drift in string ids, and gives downstream consumers explicit structured meaning when they need it.

### 10.6 Player outcome feedback

Player duty outcomes do not directly write dramatic story beats.
They update the underlying assessed state or confidence, which then changes future signals, available duties, order shifts, or aftermath stories.

This preserves the integrated loop while keeping story routing grounded.

---

## 11. Pacing Design

Phase 1 should bias toward long quiet stretches punctuated by meaningful change.

### 11.1 Default cadence

- long periods of marching, waiting, routine, low-confidence signals, and camp atmosphere are normal
- no need to surface a major incident on a fixed timer

### 11.2 Small incidents during quiet

Quiet periods may still generate minor tension if supported by live strain:

- subtle supply friction
- discipline slippage
- rumor churn
- scout uncertainty
- prisoner unease
- wounded restlessness

These should stay low-intensity and often optional.

### 11.3 Major spikes

Major pressure spikes should come from:

- enemy contact
- threatened friendly settlements
- relief races
- siege transitions
- army dispersal risk
- supply collapse
- prisoner stakes
- decisive order reversals

They should feel earned by prior state, not dropped in because the system needs content.

### 11.4 Information timing

Reports can be:

- delayed
- partial
- obsolete
- wrong
- ignored

That delay between truth, perception, and confirmation is a core part of the intended realism.

---

## 12. Persistence and Runtime Contract

This is a critical section because earlier Enlisted design work already showed that save/load and semantic survival are common failure points.

### 12.1 Persist only compact state

Persist:

- the compact enlisted-lord intelligence snapshot if the player is still enlisted
- durable player-facing facts already encountered or committed
- active duty state that cannot be safely recomputed from current world state alone
- recent change flags only if they are required to avoid obvious duplicate surfacing after load

Do not persist:

- broad raw world scans
- transient nearby-party enumerations
- arbitrary deep strategic context objects

### 12.2 Recompute volatile assessment

On load, recompute:

- front pressure
- local threat
- pursuit viability
- nearby support
- information confidence
- settlement urgency

Those are volatile live-world judgments and should not be serialized as if they were durable authored facts.

### 12.3 Revert behavior on enlistment end

When enlistment ends:

- stop enlisted-lord AI intervention immediately
- clear enlisted-only intelligence state
- stop emitting enlisted-only duties from the backbone
- stop projecting enlisted-lord strategic signals
- return the lord fully to vanilla logic without continuity guarantees

The design explicitly prefers a hard revert over a continuity handoff because the user asked for vanilla reversion.

---

## 13. Error Handling

- Missing or unavailable lord/party state should fail quiet and yield no assessed snapshot for that tick.
- If the enlisted lord temporarily has no party, the intelligence layer must degrade gracefully instead of assuming campaign end.
- Projection/routing failures must never break the live campaign.
- If a duty opportunity becomes invalid because the world changed, it should expire or be replaced naturally rather than force stale content.
- If the model layer and narrow patch layer disagree, the patch layer should never force behavior outside the enlisted-only gate.

Logging should follow current Enlisted `ModLogger` conventions with literal category and summary strings.

---

## 14. Verification Strategy

Before implementation approval:

- verify every TaleWorlds hook against `../Decompile`
- enumerate all exact model replacement sites and any required Harmony targets
- prove the enlisted-only gate for each intervention path
- define the save/load contract for any durable new state
- define the exact projection contract from hidden assessment to surfaced signal payloads
- count downstream consumer sites before promising broad story/news integration

Testing focus:

- enlisted-only lord improves while enlisted
- non-enlisted lords remain vanilla
- enlistment end reverts immediately
- long quiet stretches do not go dead, but do stay quiet
- recon/dispatch/supply/prisoner/siege-adjacent duties only appear when state supports them
- story signals never contradict live campaign reality
- higher-rank influence biases information use indirectly but never grants control
- save/load does not duplicate, lose, or invent strategic state in obvious ways

---

## 15. File and Ownership Direction

Concrete filenames may shift during planning, but phase-1 ownership should remain crisp.

Suggested additions:

- `src/Features/CampaignIntelligence/EnlistedCampaignIntelligenceBehavior.cs`
- `src/Features/CampaignIntelligence/EnlistedLordIntelligenceSnapshot.cs`
- `src/Features/CampaignIntelligence/EnlistedLordAiController.cs`
- `src/Features/CampaignIntelligence/EnlistedCampaignSignalBuilder.cs`
- `src/Features/CampaignIntelligence/EnlistedDutyOpportunityBuilder.cs`
- `src/Features/CampaignIntelligence/Models/`
  wrappers around target score, army management, mobile-party AI, and possibly settlement value

Any new C# files must be registered in `Enlisted.csproj`.

---

## 16. Phase-1 Outcome

If implemented correctly, phase 1 should produce this player experience:

- the enlisted lord behaves more coherently on the campaign map
- the player never sees a command console explaining why
- instead, the player reads strategy through imperfect reports, order shifts, camp strain, and lived work
- quiet marches remain quiet enough to feel believable
- when pressure spikes, they feel like consequences of the campaign rather than authored interruptions
- higher enlisted ranks make the player a better observer and messenger, not a hidden general

This is the intended realism-first version of "smarter lord behavior that is more fun for the player."
