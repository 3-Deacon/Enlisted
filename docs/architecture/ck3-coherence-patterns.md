# CK3 Coherence Patterns — Reference for Plans 2-7

**Status:** Reference material. Companion to [`ck3-wanderer-architecture-brief.md`](ck3-wanderer-architecture-brief.md). The brief is the contract; this doc is research that informs future spec amendments. Verbs here are descriptive, not imperative — research doesn't decide.

**Source provenance:**
- **Primary (highest weight):** Local CK3 install at `Crusader Kings III/game/common/`, cited by file path.
- **Primary-cited dev diaries:** Numbered Paradox forum dev diaries with named author + date.
- **Wiki-only:** [`ck3.paradoxwikis.com`](https://ck3.paradoxwikis.com/) — secondary; weighted below primary install when claims conflict.

**Research date:** 2026-04-25.

---

## Reading guide — three buckets

CK3 ships base game + ten DLCs over five years against a character that carries dozens of mutually-affecting properties (traits, lifestyles, education, stress, dread, prowess, dynasty perks, faith tenets, culture traditions, court types, councillor positions). The base game still works. The patterns below are how it does that. They split into three buckets — different costs, different commitment levels:

- **Bucket A — Substrate extensions.** Require new C# types, new save-definer offsets, or new runtime layers before adoption. Adopting all of these would be a "Plan 0.5" sized between Plan 1 and Plan 2.
- **Bucket B — Authoring discipline.** Rules and conventions that need writing down (and validator enforcement). No new substrate.
- **Bucket C — Process patterns.** Workflow-level. Apply to *how* plans get built, not *what* they build.

Plus:
- **Bucket D — Decisions to revisit.** Findings that conflict with the locked brief and would require a brief amendment to adopt.

---

## Bucket A — Substrate extensions

### A1. Sourced named modifier bundles (`EnlistedCharacterModifier`)

**CK3 mechanism (wiki + install).** Modifiers are *named bundles*: `add_character_modifier = { modifier = my_court_position_bonus years = 5 }`. The character carries a list of named modifiers, not a melted-together stat sheet — when a trait is removed, an activity ends, or a duration expires, the engine subtracts that exact bundle by name. Definition lives in `common/modifiers/` (formerly `character_modifiers/`).

**Coherence problem solved.** Sourcing. Removal is O(1) by name regardless of how many other modifiers stack on the same stat. No "which modifier owned this +5?" forensics.

**Mapped against substrate.** Our `QualityStore` (typed numeric, no name/expiry pair) and `FlagStore` (named bools with expiry, no value) together approximate this — but neither is a *named bundle of typed numeric effects with engine-tracked expiry*. A thin `EnlistedCharacterModifier = { Id, SourceCategory, AppliedAt, ExpiresAt?, Adds[], Mults[] }` layered over `QualityStore` would close the gap. `QualityStore.Read` resolves by walking the active-modifier list (or maintains a derived cache invalidated on apply/remove/expire).

**Cost.** One new save-definer offset (in 51-70 range), one `ModifierStore` class, integrate into `QualityStore` read path. Plans 4 (Lifestyles), 6 (Patrons), 5 (Endeavors/Contracts) all want this for "X grants Y for Z days" effects that today must be hand-rolled per behavior.

---

### A2. Suffix-typed `_add` vs `_mult` quality keys

**CK3 mechanism (wiki, [Modifier_list](https://ck3.paradoxwikis.com/Modifier_list)).** Every modifier key is suffixed by stacking discipline. `diplomacy_add` is a flat addend; `stress_gain_mult` is a percentage multiplier on the *base*. Two `+50% _mult`s yield `+100%`, not `+125%` — multipliers sum into a base coefficient, not a running total.

**Coherence problem solved.** Resolution is order-independent (sum of all `_add`, then `1 + sum-of-all-_mult` applied to base). N modifiers from N sources flow through the same two-bucket math. The suffix is the contract — no per-modifier "is this stacking right?" arguments.

**Mapped against substrate.** Our `QualityStore` keys today don't carry stacking semantics — `lord_relation`, `scrutiny`, `readiness` are all flat values mutated by direct writes. As Plans 2-7 grant qualities, splitting keys into `<key>_add` vs `<key>_mult` namespaces and folding through a single resolver gives author-time guarantees.

**Cost.** Doubles quality-key surface. Forces every consumer to commit to additive vs multiplicative semantics at authoring time. Validator Phase 12 should reject any quality key not ending in one of the two suffixes once adopted.

---

### A3. Capability-flag registry (`FeatureGate.Has(...)`)

**CK3 mechanism (wiki, [Triggers_list](https://ck3.paradoxwikis.com/Triggers_list)).** `has_dlc_feature = <feature_key>` gates content on a *capability key*, not a DLC SKU. Royal Court's `royal_court`, `hybridize_culture`, `diverge_culture` are three separate keys even though they ship together — gates are per-capability, not per-purchase. New DLCs add new keys without touching existing ones.

**Coherence problem solved.** Capability namespace is independent of ship cycle. If Paradox bundles Royal Court into the base game tomorrow, every `has_dlc_feature = royal_court` check still works.

**Mapped against substrate.** Replace `if (PatronRollBehavior != null)` and `OrderActivity.Instance != null` checks with named gates: `FeatureGate.Has("patrons")`, `FeatureGate.Has("ceremonies")`. Each Plan publishes one or more named features at registration; consumers query the registry. Plan 7 (Lifestyles) reading patron count goes `if (FeatureGate.Has("patrons")) { var n = PatronRegistry.Count(); … }` and the absent-Patron path no-ops cleanly without NRE.

**Cost.** A small registry class + a registration call in each Plan's `OnGameStart`. Doc-level rule: cross-Plan reads gate at the call site, never via tight coupling.

---

### A4. Threshold-leveled qualities with discrete breakpoint events

**CK3 mechanism (wiki, [Stress](https://ck3.paradoxwikis.com/Stress) / [Dread](https://ck3.paradoxwikis.com/Dread)).** Stress is `0-300+` continuous, but the *interesting* surface is three breakpoint levels (100 / 200 / 300), each unlocking different content. Mental-break events fire only on first crossing, gated by a 5-year cooldown (8 with Mental Resilience perk). Dread mirrors with Intimidated/Terrified states keyed off `dread - boldness`.

**Coherence problem solved.** A continuous quality cross-feeding N other systems would need N×continuous integrations. Threshold levels collapse to N×{level0, level1, level2, level3} — discrete cells that storylets can author against. Cross-feeds become "if scrutiny_level ≥ 2 then storylet pool X is eligible" instead of arithmetic on raw values.

**Mapped against substrate.** Keep `QualityStore` continuous, expose a `QualityLevel(key)` accessor with per-quality breakpoints declared in JSON. Storylets and triggers read levels via `quality_level >= 2`, not raw values. Per-threshold-crossing storylet hooks carry a CK3-style cooldown so wobbling at the boundary doesn't fire repeatedly.

**Cost.** Schema for breakpoint declarations; one accessor on `QualityStore`; new `TriggerRegistry` predicate `quality_level_at_least`. Apply immediately to `scrutiny`, `lord_relation`, `loyalty`, `readiness` so Plans 2-7 cross-coupling speaks in levels.

---

### A5. Indirect cross-feed via modifier intermediary

**CK3 mechanism (wiki, [Stress](https://ck3.paradoxwikis.com/Stress)).** Stress does not directly modify dread, piety, or prestige. The pathway is: Stress threshold → mental-break event → grants trait (Wrathful, Melancholic) → trait carries `dread_baseline_add` or `monthly_piety_mult`. Wiki explicit: "no mechanical pathway exists where stress directly modifies dread, prestige, or piety generation rates."

**Coherence problem solved.** A direct cross-feed graph between K qualities is a K² edge problem. Routing through traits/modifiers as the only mediator makes it `K → traits → K`, where traits/modifiers are author-controlled named entities tunable in one place. New qualities don't need to know about old qualities — they publish thresholds → grant modifiers; modifiers hold the cross-effects.

**Mapped against substrate.** Plans 2-7 should NOT have a Patron's `favor` value directly mutate `scrutiny`. Instead: Patron favor ≥ threshold → applies a *named character modifier* (`favored_by_patron_X`, duration 30 days) → modifier carries `scrutiny_add = -5`. Same for Contracts feeding Lifestyles, Ceremonies feeding Endeavor unlocks. Authoring rule: cross-plan effects route through `EnlistedCharacterModifier` bundles (A1), never through direct cross-system writes.

**Cost.** Builds on A1 + A2 + A4 — together they form the "indirect cross-feed" substrate. Build a `WhoModifies(qualityKey)` query into `QualityStore` from day one or accept "why did my scrutiny drop?" debugging pain.

---

### A6. Named beat dispatcher (`OnActionDispatcher`)

**CK3 mechanism (install, `common/on_action/_on_actions.info`).** `_on_actions.info:102-117` is explicit: a DLC must NOT redefine an existing on_action's effect block. Instead, the vanilla on_action contains an `on_actions = { my_dlc_on_action }` line, and the DLC ships `my_dlc_on_action` as its own file. Verified in `dlc/ep3/grand_ambitions_story_cycle_on_action.txt`, `dlc/tgp/`. DLC files (`fp1_on_actions.txt`, `ep3_on_actions.txt`, `tgp_*.txt`) are siblings, never redefinitions.

The fixed pulse set: `yearly_global_pulse`, `on_yearly_playable`, `three_year_playable_pulse`, `five_year_playable_pulse`, `quarterly_playable_pulse`, `random_yearly_playable_pulse`, plus state-transition on_actions in their own files (`death.txt`, `birthday.txt`, `marriage_concubinage.txt`, `war_on_actions.txt`, `traits_on_actions.txt`, `culture_on_actions.txt`).

**Coherence problem solved.** A single fan-in/fan-out tree means engine code only emits ~10 named beats; everything authored hangs off them. New content or DLC additions never need new engine plumbing.

**Mapped against substrate.** Today our hourly + daily ticks are subscribed to directly via `CampaignEvents.HourlyTickEvent`. Plans 2-7 each subscribe their own listeners independently — that's already additive (mirrors the "append, never overwrite" property). What's missing is the *named-beat layer* between tick and behavior: a small enum of public Beat IDs (`Hourly`, `Daily`, `OnEnlist`, `OnRankAdvanced`, `OnLordIntelChange`, `OnDutyProfileTransition`, `OnContractAccepted`, …) fired by exactly one C# site, with `StoryletCatalog` registering per-beat via `firesOn: ["Daily", "OnRankAdvanced"]`. Builders read the catalog by beat instead of standing up bespoke tick handlers.

**Cost.** One enum + dispatcher table; one-time refactor of `DutyOpportunityBuilder` / `SignalBuilder` to register against the dispatcher. Save-definer impact zero (declarative). Payoff: Plans 3 (Rank Ceremonies) and 7 (Lifestyles) both want to react to rank changes without coupling — beats decouple them.

---

### A7. Per-storylet `cooldown` + `weight` + `triggered_only` fields

**CK3 mechanism (install, primary).** Pacing lives on the *individual event*, not the dispatcher. `childhood.0001` carries `cooldown = { years = 2 }`. Counted **1162+ `is_triggered_only=yes`** events across 100 sampled event files (likely majority of vanilla content), and 52 explicit `cooldown` blocks in 6 childhood files alone. `weight_multiplier` is per-event AND per-on_action; ambient firing inside `random_events = { … }` picks one weighted entry plus a weight-0 slot for "no event fires." `chance_to_happen` gates the entire pick.

`is_triggered_only = yes` switches an event to "explicit fire only" — only `trigger_event = { id = … }` from another script can launch them. Vast majority of vanilla content is triggered-only: continuation events, response events, story_cycle phases, scheme outcomes.

**Coherence problem solved.** Content authors own their own pacing relative to peers in the same pool. Adding 80 new wanderer events doesn't crowd existing ones because each carries its own cooldown and weights are relative inside the pool. The triggered-only flag splits "engine pick what fits now" from "specific situation owns when this fires" inside one content format.

**Mapped against substrate.** Our `StoryDirector` already has the global modal floor (5 in-game days / 60 wall-clock seconds / `CategoryCooldownDays = 3`) — that's a rail CK3 lacks at the dispatcher level, and it's correct for our scale (we have far fewer authored storylets per pool). What's missing is the *per-storylet* rail: optional `cooldown: { days: N }`, `weight: float`, and `triggered_only: true` fields on `Storylet`. Today those are hand-rolled via `DutyCooldownStore` (offset 50) plus ad-hoc `FlagStore` flags.

**Cost.** Three optional Storylet fields, one read site in builders, one write site in a "claim" call after fire. `triggered_only:true` is excluded from ambient builders; validator can enforce "rank-ceremony storylets must be triggered_only:true" preventing accidental ambient firing of once-per-career arcs.

---

## Bucket B — Authoring discipline

These are rules to write down and (where possible) lift into validator phases. No new substrate.

### B1. Per-Plan ID prefix discipline

**Observation (install + wiki, [Downloadable_content](https://productionwiki-ck3.paradoxwikis.com/Downloadable_content)).** Each CK3 DLC has a code (`fp1`, `ep1`, `fp2`, `ep2`, `bp1`, `bp2`, `ce1`, …) and ships content under `events/dlc/<code>/`. Modifiers and events don't collide because authors don't ship colliding names — it's authoring discipline plus filesystem partitioning, not engine enforcement.

**Adoption.** Per-Plan prefixes for every authored ID — quality keys, flag keys, scripted-effect IDs, storylet IDs. Plan 6 lives under `Storylets/Patrons/` with IDs prefixed `patron.*`. Plan 7 under `Storylets/Lifestyles/` with `lifestyle.*`. `QualityStore` keys go `patron.favor.alagier`, not `favor_alagier`. `FlagStore` flags go `patron.protege.active`. `validate_content.py` Phase-N can check the prefix matches the directory.

**Cost.** Verbose IDs. Worth it because cross-Plan grep ("which Plan owns this key?") becomes deterministic.

---

### B2. `EnsureInitialized()` audit on every save store

**Observation (Paradox forums, primary).** CK3 has no documented mid-save migration system. The community-manager guidance for Royal Court was: "the update to 1.5.0 will almost certainly create issues with those saved games." T&T was identical. New state default-initializes to zero on load; new content begins firing through `on_actions` from the next pulse — there is no retrofit pass.

**Adoption.** Already encoded as CLAUDE.md pitfall #18. Mirror this on every Plan 2-7 store. `EnsureInitialized()` reseats null collections, called from `SyncData`, `OnSessionLaunched`, `OnGameLoaded`. Defaults must be game-meaningful (zero patron favor; empty lifestyle list; no active contract — never throw on absent state). Existing-save smoke for each Plan: load a save from before the Plan shipped, verify no NRE and content begins firing on the next tick.

**Cost.** Per-store boilerplate. Already partly in our pitfalls list — codifying the mandatory "load a pre-Plan save and smoke" verification step is the new piece.

---

### B3. Cycle detection at load time, not runtime cap

**Observation (wiki, [Scripted_effects](https://ck3.paradoxwikis.com/Scripted_effects)).** "A scripted_effect can use other scripted_effects, but recursion is not allowed." CK3 forbids cycles at load. Our `EffectExecutor` caps expansion at depth 8 and silently truncates with `Expected("EFFECT", "scripted_depth_limit", …)`.

**Adoption.** Move depth-cap detection from runtime to a load-time validator phase that fails the build on cycles. ~50 lines of Python walking the `scripted_effects.json` graph. Converts a runtime no-op (chain mysteriously stops at depth 8) into authoring-time error before Plans 2-7 add content.

**Cost.** One validator phase. Pure win.

---

### B4. No-emission weight slot in ambient pools

**Observation (install, `yearly_groups_on_actions.txt`).** Inside any on_action, `random_events = { chance_to_happen = 65; 200 = 0; 100 = event_id_a; 400 = event_id_b }` — the `200 = 0` slot says "with weight 200, fire nothing." Ensures rare events don't always fire when only one valid candidate exists.

**Adoption.** `DutyOpportunityBuilder` and `SignalBuilder` builders should explicitly support a "no-emission weight" field (`silenceWeight: 200`) so when a pool has only one eligible storylet today, the builder still has meaningful chance of ambient silence. Currently builders that find 1 eligible candidate fire it deterministically — that's a pool-starvation bug class.

**Cost.** ~10 lines per builder.

---

### B5. Sparse trait `opposites` + `tensions`, not full N×N matrix

**Observation (wiki, [Trait_modding](https://ck3.paradoxwikis.com/Trait_modding)).** Trait files declare hard exclusion via `opposites = { craven }` (1-2 entries each), soft tension via `compatibility = { gluttonous = 20 drunkard = @pos_compat_low }`, `group_equivalence = lunatic` to alias variants. Most traits stay at O(small constant) declarations regardless of total trait count.

**Adoption.** As Plan 2 (Companion Substrate) and Plan 7 (Lifestyles) grant traits/perks, give every trait a sparse `Opposites[]` (hard-blocks) and `Tensions[]` (soft narrative hooks for storylet eligibility). `group_equivalence` maps to a tag system — all "ascetic-flavor" perks share tag `ascetic` so storylet triggers can `has_tag = ascetic` without enumerating every perk.

**Cost.** Authoring discipline + a validator phase that warns on dangling references (Plan 7 declaring opposites against a Plan 2 trait that doesn't exist yet).

---

### B6. Strict perk-tree prerequisites with one explicit override

**Observation (wiki, [Lifestyle](https://ck3.paradoxwikis.com/Lifestyle) / [Lifestyles_modding](https://ck3.paradoxwikis.com/Lifestyles_modding)).** Perks unlock in tree order — "A perk can only be unlocked if all perks above have been unlocked." Single documented escape: rare travel events. `1000 XP` per perk, set per-lifestyle via `xp_per_level`.

**Coherence point.** Strict ordering collapses a lifestyle tree of 9 perks to 9 reachable states (positions 1-9 along the path), not 2^9. Designers can author "you have perk-3" content without worrying about every subset.

**Adoption.** Plan 7 (Lifestyles) and Plan 4 (Endeavors) encode perk trees with linear `parent = X` chains, not free DAGs. Storylets gate on `has_perk = X` (which implies all parents). Reserve one explicit "skip-prereq" hook (mirroring CK3's travel events) for narrative payoffs — e.g. a Patron favor or Rank Ceremony granting an off-path perk. Validator rejects perk grants without prereq satisfied.

**Cost.** Less expressive than a true skill graph. Acceptable; CK3 ships with this and it's not the bottleneck.

---

## Bucket C — Process patterns

### C1. Phase Lock in last 20% of every plan

**Source.** [CK3 Dev Diary #191 "2025 in Review,"](https://forum.paradoxplaza.com/forum/threads/dev-diary-191-2025-in-review.1888979/) Game Director Alex Oltner (Rageair), Dec 15, 2025. On Coronations DLC failures: "The issues boiled down to two things: we were changing things too recklessly late in development, and we didn't play enough longer sessions towards the end of the cycle." Process change: "We're having several developers constantly play longer sessions up until release."

**Adoption.** In the last ~20% of any plan, lock the substrate: no new effect primitives, no new save offsets, no schema changes. Concentrate on long-session play. Formalize this as a "Phase Lock" gate in plan verification docs.

---

### C2. Bi-weekly observation playtest, not just scripted scenarios

**Source.** [CK3 Dev Diary #35 "User Testing Before Release,"](https://forum.paradoxplaza.com/forum/developer-diary/ck3-dev-diary-35-user-testing-before-release.1408176/) Hanna (User Research, Paradox), July 28, 2020. "Throughout the development of the game, we've also run bi-monthly 1 hour playtests with 2-4 players." Mixed-experience cohort: "12 participants, 6 of them with 200-800h of CK2 playtime and 6 of them 'newbies'." Methodology: "observation … one or several surveys and then an interview."

**Adoption.** Queue 2-3 paired ~1h unscripted play sessions per plan with structured note template (what confused, what wasn't fun, what menus broke), separate from the scripted scenarios in [`docs/superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md`](../superpowers/plans/2026-04-21-career-loop-playtest-scenarios.md). Scripted scenarios verify what we predicted; observation catches what we didn't.

---

### C3. Post-merge regression re-verify

**Source.** [CK3 Dev Diary #92 "Anatomy of a Game: From Report to Resolution,"](https://forum.paradoxplaza.com/forum/threads/ck3-dev-diary-92-anatomy-of-a-game-from-report-to-resolution.1518991/) jakeowaty (QA, Paradox), Apr 5, 2022. "Sometimes a different set of dominos … gets randomly pushed and some older issues reappear" — they re-verify after fix deployment.

**Adoption.** When a plan ships, run the prior plan's verification doc once more. Today we verify the new plan and assume prior plans stay green; explicit re-prove is the discipline. Especially relevant for the wanderer family where Plans 2-7 all touch shared substrate (`StoryDirector`, `QualityStore`, `FlagStore`).

---

### C4. Scripted-content layer designed against "no foot guns"

**Source.** [Anatomy of a Game: The Script System,](https://forum.paradoxplaza.com/forum/threads/anatomy-of-a-game-the-script-system.1484918/) blackninja9939 (programmer, CK3), Aug 5, 2021. Quote: "It is much quicker to change things in script and makes those changes easier to maintain in a controlled environment." "No foot guns, one should not be able to cause catastrophic issues from the script."

**Adoption.** Already partially encoded — `validate_content.py` Phase 12 (read-only quality protection) and Phase 11 (banned `ModLogger.Error` API) are foot-gun gates. When designing Plan 2-7 effect primitives, the test is "can a content author cause a catastrophic state by composing this with existing primitives?" — not just "does the primitive itself work?" Bias toward additional validator phases over runtime defenses.

---

### C5. Feature visibility over event-pack content

**Source.** [CK3 Dev Diary #161 "2024 in Review,"](https://forum.paradoxplaza.com/forum/threads/dev-diary-161-2024-in-review.1720433/) Game Director Alex Oltner (Rageair), Dec 11, 2024. Quote: "It's generally better to focus on something visible and more instantly available rather than just collections of events." On Legends of the Dead: "Dev Diaries presented the features in a much too embellished and flowery way, making them appear as something they weren't."

**Adoption.** Each of Plans 2-7 should ship a load-bearing visible surface (a menu, a UI affordance, a status line) — not just storylet pools. Already present in Plan 5 (path crossroads UI) and Plans 1-4 (quality/intel surfacing); preserve this for the wanderer Plans 2-7. Plan diaries (verification docs) should be factual, not embellished.

---

## Bucket D — Decisions to revisit

### D1. `StoryArc` as a third runtime alongside `Storylet` and `Activity`

**This is a conflict with the locked brief, not a pattern to adopt.** Surfacing it because the source quality is high and the cost of pivot is bounded.

**Primary-source finding (install).** `common/story_cycles/` contains 50+ files in our local install — `story_cycle_pet_dog.txt`, `ep3_story_cycle_grand_ambitions.txt`, `story_cycle_house_feud.txt`, `bp2_story_cycle_imaginary_friend.txt`, etc. Each defines an arc with `on_setup`, `on_end`, `effect_group` lists invoked by their own on_actions, persistent variables (`var:dog_fur_color`), `visualization` UI, optional `decisions` block. Story_cycles are *not* deprecated — DLCs add 4-12 new ones each (EP3 ships 9, BP2 ships 4, TGP ships 2).

CK3 has **four orthogonal content runtimes**:
- `event` — one-shot fire-and-respond.
- `activity` — scheduled, locale-bound, intent-driven engagement (feast, hunt, tour, coronation, pilgrimage, tournament). `pulse_actions/`, intent system, locales, guest invite rules.
- `story_cycle` — long-running thematic arc with persistent state and ambient fires across years. **No schedule, no locale, no guests.**
- `scheme` — multi-agent, progress-meter, secrecy axis. Out of scope here.

**Conflict with the locked brief.** Our brief and systems-analysis spec §3.8 lock `EndeavorActivity` (offset 57) and `ContractActivity` (offset 56) as `Activity` subclasses sharing `OrderActivity`'s backbone. Plan 1 has shipped registering both at those offsets. The CK3-shape argument is that Endeavors and Contracts have *no schedule, no locale, no guests* — they match `story_cycle`, not `activity`. Shoving them into `ActivityRuntime` overloads it with semantics it doesn't fit.

**Two sides:**

- **Brief's case (sibling-Activity).** `OrderActivity` is shipped with duty-profile state, named-order arc state, and reconstruction code. Endeavors and Contracts can reuse the lifecycle/serialization/storylet-pool plumbing. Plan 1 shipped on this assumption; reversal requires a brief amendment, save-definer offset shuffle, and code already-shipped to be relocated. Cost is bounded (Plan 1 substrate is small) but non-zero.
- **Story_cycle case (CK3 precedent).** `Activity` semantics in CK3 explicitly require schedule + locale + guests; our `OrderActivity` reuses the name but doesn't carry those semantics. As Plans 4-7 add Endeavors/Contracts/Patron arcs, the `ActivityRuntime` accretes runtime semantics that diverge further from what `Activity` means in CK3. Building `StoryArc` now (separate runtime, separate save-definer offset, `OnSetup`/`OnEnd`/`Variables`/`InterimStorylets`) keeps each runtime clean.

**Status.** Plan 5 (Endeavors) brainstorm hasn't started. Cheapest moment to revisit is in that brainstorm. The decision belongs to the user, not this doc.

---

### D2. Scheme runtime (skip — for now)

**Source (install, primary).** `common/schemes/scheme_types/*.txt` (40+ types including DLC). Each scheme: `skill`, `category` (hostile/personal/contract), `target_type`, `is_secret`, `cooldown = { years = 10 }`, `maximum_breaches`, `base_progress_goal`, `phases_per_agent_charge`, `success_chance_growth_per_skill_point`. Verified in `abduct_scheme.txt`. Agent system in `common/schemes/agent_types/`, scheme-wide pulse_actions in `common/schemes/pulse_actions/general_scheme_pulse_actions.txt`.

**Adoption recommendation.** Skip. None of Plans 2-7 need a true scheme surface — Patrons, Endeavors, Contracts are all overt, not progress-metered against a target's resistance. If a future plan starts demanding "cultivate this lord's trust over months with multiple opposed agents," reach for the scheme model then. Building a scheme runtime is months of work; nothing in the current roadmap requires it.

---

## Lower-confidence findings (speculation, not parity)

These came from the wiki-only research domain and lack primary-install verification. Listed for completeness; treat as speculation.

- **`composite_trigger` JSON layer.** Hybrid C# + JSON-defined AND/OR/NOT over named triggers with `$param$` substitution. Plausible but our `TriggerRegistry` C# composition might be simpler.
- **`for_each_companion` / `random_companion` content-side iteration.** CK3's `every_X` iterators are engine primitives; replicating in JSON would require an enumerator dispatcher. Worthwhile only for Plan 2 (Companion Substrate); generalizing risks O(N) hot-path work.

---

## Caveats — what doesn't transplant cleanly

1. **CK3's commercial separability.** DLCs ship on independent cycles, are sold separately, players own arbitrary subsets. Enlisted ships as one binary; every player gets every Plan; there is no per-DLC purchase gate. Patterns that *depend* on the commercial reality (DLC-specific patch locks, paywall UI, separate save formats per DLC subset) don't apply. The transplanted patterns work because they share the same *shape* (feature absent → predicates false → content invisible) even though the cause differs (Plan-not-yet-written or feature-disabled-by-config rather than not-purchased).

2. **Bannerlord lacks per-character monthly tick + trait-as-engine-primitive.** CK3's authoring discipline assumes (a) a uniform decay/refresh cadence the engine drives, (b) traits as a first-class object with engine-enforced opposites/groups, (c) `add_character_modifier` with built-in expiry timers. We'd be reimplementing all three on top of `CampaignEvents` ticks and JSON-defined traits.

3. **Declarative-merge vs. C#-subscription layer shift.** CK3 leans heavily on its declarative scripting layer (PDX script, `common/scripted_triggers`, JOMINI engine) for additive load-merging. Enlisted's substrate is C# with JSON content, so the merge is at C# behavior subscription + JSON catalog union, not at language level. The patterns transplant; the mechanism sometimes shifts up one layer.

4. **Source-quality variance.** Agent 2's findings (Bucket A6, A7; Bucket D1) are primary-install-cited and weight high. Bucket D1 (StoryArc) directly contradicts the locked brief — that's the only finding that requires a decision-relitigation, and the conflict is surfaced explicitly in Bucket D rather than buried.

---

## References

- [CK3 Wanderer Architecture Brief](ck3-wanderer-architecture-brief.md) — the contract Plans 2-7 reference.
- [CK3 Wanderer Mechanics Systems Analysis (v6)](../superpowers/specs/2026-04-24-ck3-wanderer-systems-analysis.md) — the design source.
- Local CK3 install at `Crusader Kings III/game/common/` — primary source for install-cited findings.
- [`ck3.paradoxwikis.com`](https://ck3.paradoxwikis.com/) — wiki source for B1-B6 and lower-confidence findings.
- Numbered Paradox forum dev diaries (cited per practice in Bucket C).
