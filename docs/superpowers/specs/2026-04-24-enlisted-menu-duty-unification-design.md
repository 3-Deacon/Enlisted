# Enlisted Menu + Duty Unification — Design

**Status:** Draft v2 (2026-04-24). Consolidates the menu restructure, the news-unification body layout, and the Routine→Duty reframe into one surface design. Supersedes parts of three prior docs (see "Relation to prior specs"). V2 corrects eight factual errors flagged by adversarial review — see Changelog at the bottom.

## Problem statement

Today's enlisted menu has three layered problems:

1. **Body flood.** `enlisted_status` body concatenates 15+ short lines across three named sections (KINGDOM REPORTS / COMPANY REPORTS / PLAYER STATUS — see `EnlistedMenuBehavior.cs:3315-3347`). The Camp hub body adds five more sections (SINCE LAST MUSTER / UPCOMING / RECENT ACTIVITY / STATUS + smaller variants — see `EnlistedMenuBehavior.cs:1595-1628`). On fast-forward, several sections tick-rebuild every ~5 seconds and the eye cannot land.
2. **Routine concept was abstract.** The player-agency redesign (v3) proposed seven abstract service stances — *Drill with the Line / Scout Ahead / Keep Your Head Down* etc. — that drift skills through `StateMutator`. The names felt off-register for a soldier-career sim, several overlapped with already-shipped concepts, and the "posture" abstraction didn't answer the grounded question *"what's my job in this army?"*
3. **"Duty" is already the right word, overloaded.** `EnlistedFormationAssignmentBehavior.cs:18-19` comments name the player's combat-class choice as their *"duty"*. `DutiesOfficerRolePatches.cs` uses *"duty"* for party-role assignments (Field Medic / Pathfinder / Provisioner / Siegewright's Aide). The abstract "stance" of v3 was inventing a parallel term for something the code had already landed.

## North star

The player's single most important career choice is their **Duty**: a military occupational specialty that decides battle formation, party role, gear progression, and which authored storylet events find them. Everything else — news, camp, records, orders — organizes around that one anchor.

The menu body is two paragraphs, frozen or live per section. The option list is short and action-clear. Events fire as CK3-style popups with 2–3 visibly-previewed choices; the choice, not the Duty itself, is what drifts traits, gold, health, and scrutiny.

## Menu shapes

### `enlisted_status` (main menu)

**Title:** *Enlisted Status*.

**Body** — two sections, blank line between:

```
DISPATCHES · Week of <month> <day>

<4–5 sentence weekly kingdom digest: war stance, lord's current action,
notable realm news, supply/siege summary, closing outlook line.>

PROSPECTS

<Live, rebuilt on menu re-entry. Current duty, active order or next
expected order, muster countdown, world-state activity hint.>
```

- **DISPATCHES** is frozen between weekly regen triggers (menu re-entry at play-family speed, fast→play transition, 7-day boundary while resident). Cadence inherited from news-unification v2's `DISPATCHES` contract.
- **PROSPECTS** replaces v2's `UPCOMING`. Live-rebuild on `OnEnlistedStatusInit` only, not on tick. Absorbs v2's forecast contract (next commitment, active order hours-remaining, world-state hint, muster countdown) and adds current-Duty line.

**Options** (in order):

1. **Orders** — active + proposed orders.
2. **Audience** — speak with a lord in the army. Wires the existing `OnTalkToSelected` flow at `EnlistedMenuBehavior.cs:3946` (finds nearby lords, shows selection inquiry).
3. **Camp** — admin / paperwork hub.
4. **Visit settlement** — existing contextual option; appears when paused near a settlement.

Removed from today's menu: `HEADLINES [NEW]` option (replaced by inline `[NEW]` markers in Camp's STATUS section), `Equipment` (folded into Camp → Quartermaster), the decisions-accordion header, the evening-intent slot bank.

### `enlisted_camp_hub` (Camp)

**Title:** *Camp*.

**Body** — two sections:

```
COMPANY NEWS

<Live-rebuild on camp-hub-open. Commander's mood / actions, muster
countdown, supply snapshot, company casualties, routine outcomes filtered
to DispatchSourceKind=Routine. Absorbs v2's CAMP ACTIVITIES + SINCE LAST
MUSTER merged into one company-scope paragraph.>

STATUS

<Live-rebuild. Player-scope: duty state opening, physical condition,
up to 3 recent personal-feed items as condensed prose (NOT bullets).
Any headline-tier personal item whose StoryKey is unread gets a
"NEW:" inline prefix. On camp-hub-open, mark-read fires for visible
headlines — same semantics as v2.>
```

**Options** (in order):

1. **Duty** — MOS picker (see Duty concept below).
2. **Quartermaster** — existing `OnQuartermasterSelected` flow; opens conversation with the QM hero, which routes to equipment / provisions / upgrade UIs.
3. **Records** — service history. Sub-menu deferred (see Open items).
4. **Companions** — role assignment for clan companions. Wraps existing `CompanionAssignmentManager`. Sub-menu deferred.
5. **Retinue** — personal troop retinue view. Wraps existing `RetinueManager`. Sub-menu deferred.
6. **Back** — return to `enlisted_status`.

Duty sits first in Camp because a Duty change cascades into a QM gear re-issue — the player's natural flow is *Duty → Quartermaster*, not the reverse.

## Duty concept

A **Duty** is one-of-one: the player's single declared job at any moment. Persists across orders (orders may override temporarily and restore on resolve), saves with the player, and drives four systems:

1. **Battle formation** — Combat Duties set the player's formation slot (Infantry / Ranged / Cavalry / Horse Archer). **This requires new wiring.** Today `CombatClassResolver.Resolve(hero)` at `CombatClassResolver.cs:25-40` reads `character.IsRanged` / `character.IsMounted` (equipment-derived); `EnlistedFormationAssignmentBehavior.cs:466-470` then assigns the player to the formation that class maps to. Phase 1 changes `CombatClassResolver` to query `DutyManager.Current` first for the player, falling back to equipment-derived class when no Duty is set or for non-player heroes. `EnlistedFormationAssignmentBehavior` keeps its in-battle assignment role; only the class-lookup source changes.
2. **Party role** — Support Duties claim the relevant party role (Surgeon / Scout / Quartermaster / Engineer). **This requires new wiring.** Today `DutiesOfficerRolePatches.cs` contains four prefix patches for `MobileParty.EffectiveEngineer/Scout/Quartermaster/Surgeon` getters, but each prefix unconditionally `return true`s after its enlistment-state guard (see lines 57, 104, 150, 192) — the patches run vanilla behavior with no Duty claim. Phase 1 replaces the trailing `return true;` in each patch with a `DutyManager.Current == <support duty>` check; on match, the prefix sets `__result = Hero.MainHero.CharacterObject` and returns `false` to suppress vanilla. On miss, the patch keeps `return true;` as today.
3. **Gear issue** — on Duty change or rank-up, the Quartermaster re-issues equipment drawn from the faction's troop tree at the player's current tier (see "Gear re-issue ritual" below).
4. **Storylet pool** — each Duty owns an authored storylet pool; ambient events fire with frequency keyed to the Duty's intent. Event outcomes drift player state via `StateMutator` (the envelope/gate model from agency v3 survives; only the stance/routine vocabulary is replaced).

### Duty list

Grouped into two sections in the Duty menu. Combat Duties are filtered by the player's enlisted faction's troop tree; Support Duties are universal.

**COMBAT** (filtered by faction — see Faction filtering below):

| Id | Display | Formation | Notes |
| :--- | :--- | :--- | :--- |
| `duty.infantry` | Infantry | Infantry | Universal. Every faction has an infantry branch. |
| `duty.archer` | Archer | Ranged | Universal EXCEPT Vlandia (whose ranged tree is crossbow). |
| `duty.crossbowman` | Crossbowman | Ranged | Vlandia specialty. |
| `duty.cavalry` | Cavalry | Cavalry | Universal but troop quality varies heavily (Aserai Mameluke, Empire Cataphract, Vlandian Banner Knight are strong; Battanian / Sturgian cavalry weaker). |
| `duty.horse_archer` | Horse Archer | HorseArcher | **Faction-filtered by `default_group` attribute, not by culture hardcoding.** Three factions have HA troops in Native data: Khuzait (8 troops, dominant branch); Aserai (Mameluke Cavalry + Mameluke Heavy Cavalry — ids say `..._cavalry`, but `default_group="HorseArcher"`); Empire (Bucellarii, specialty tier-5). Battania / Sturgia / Vlandia have none in the main trees. |

Faction specialties (Fian-path / Ulfhednar / Mameluke / Cataphract) are handled as **named variants of the base combat duty** rather than separate duty ids — e.g. selecting `duty.infantry` as a Sturgian player issues Ulfhednar-branch gear. Specialty-branch display labels can be authored per culture into a `duty_culture_labels.json` lookup; not required for ship.

**SUPPORT** (universal):

| Id | Display | Role claim | Skill focus |
| :--- | :--- | :--- | :--- |
| `duty.pathfinder` | Pathfinder | Scout | Scouting / Riding |
| `duty.field_medic` | Field Medic | Surgeon | Medicine |
| `duty.provisioner` | Provisioner | Quartermaster | Steward / Trade |
| `duty.siegewright` | Siegewright's Aide | Engineer | Engineering |

### Faction filtering

For the Combat section, the Duty menu queries the enlisted faction's troop tree and displays only duties whose troop branch exists. Implementation rule:

- Enumerate the current faction's troop tree from the lord's culture at menu-init.
- For each combat duty, scan the tree for a troop whose `default_group` attribute matches the duty's formation class (`Infantry` / `Ranged` / `Cavalry` / `HorseArcher`) AND whose tier equals the player's tier.
- **If found** — show the option, label with the troop's display name (e.g. *"Infantry · Vlandian Sergeant (T3)"*).
- **If not found at the player's tier** — grey the option and show the reason inline (e.g. *"Archer — Vlandia uses crossbow"*, *"Horse Archer — not in Sturgian troop tree"*, *"Cavalry — Vlandia requires T3"*).

**Filter rule: read `default_group` from troop XML, not the id suffix.** This is the same attribute Bannerlord uses for its own battle-time formation assignment. Two otherwise-misleading cases settle correctly under this rule:

- Vlandian militia troop `vlandian_militia_archer` has id ending `_archer` and display name *"Vlandian Militia Crossbowman"* — both reconcile via `default_group="Ranged"`.
- Aserai troops `aserai_mameluke_cavalry` and `aserai_mameluke_heavy_cavalry` have ids ending `_cavalry` but `default_group="HorseArcher"` — Aserai therefore offers Horse Archer duty, not Cavalry-branded Mameluke.

An id-suffix filter would misclassify both. A `default_group`-based filter gets both right.

### Gear re-issue ritual

Owned by a new `Quartermaster.ReissueForDuty(DutyId duty, int tier)` method. Fires on:

- Duty change via the Duty menu.
- Rank-up that moves the player into a different troop tier on their current Duty.

**What gets swapped.** The player's battle equipment (weapons, armor, mount if applicable) is replaced with the default loadout for the `(faction, duty, tier)` combination, read from the troop's `equipment` slots in `spnpccharacters.xml`.

**What is preserved.** Follows the precedent of `TroopSelectionManager.cs:500`:

- **Quest items** — never touched; persist across Duty changes.
- **Civilian equipment set** — preserved separately (lives in a different equipment slot); unaffected.
- **Previously-upgraded items the player paid gold for** — returned to the player's personal inventory. Not destroyed, not refunded.

**Overflow handling.** If the player's personal inventory is full when upgraded items are returned:

1. Overflow items spill into the party inventory.
2. If the party inventory is also full, remaining items are sold back to the QM at 50% of their base value, gold credited to the player, and a narrative line fires in COMPANY NEWS (*"The quartermaster took your old sword at half-price — no room in the wagons."*).

**Critical-supply block.** When `CompanySupplyManager.Supplies < 15%` (the existing QM-block threshold from `EnlistedMenuBehavior.cs:1419-1433`), `ReissueForDuty` is blocked:

- The Duty-change itself still succeeds (the choice persists in `DutyManager`).
- Equipment swap is deferred; old gear stays equipped.
- A narrative line in COMPANY NEWS informs the player: *"The quartermaster is tight with stores — new kit will wait until supplies recover."*
- The player can retry the reissue from the Quartermaster option once supplies recover to ≥15%.

**Confirmation modal.** None in Phase 1. If playtest feedback shows the swap feels abrupt, a Phase 2 follow-up can add one.

**No refund of gold spent on past upgrades.** Items return to inventory at no gold cost; gold previously spent is not refunded.

### Duty event framework (CK3-style popups)

Each Duty has an authored storylet pool keyed to its intent. The Duty system registers each pool with the storylet backbone; `StoryDirector` fires an event when pacing allows and the Duty's trigger predicates match.

An event is a storylet with 2–3 options, each carrying a `preview` block (per agency v3's preview schema addendum) showing grants / costs / risks inline in the option text. Choice effects route through `StateMutator` under a Modal envelope — the envelope model from agency v3 survives.

**Sample — Infantry Duty, ambient pool, ~every 3 in-game days:**

```
A Fight in the Line

The drill ends in a quarrel. Wulfric, one of the older sergeants, has been
calling you "bookworm" since you joined — sneering the word every time
your sword hand fumbles. Today he's made the joke in front of the company,
and a few men laughed. Wulfric is waiting to see what you'll do.

  › Step up and answer him — fists, not steel
      +1 Valor · +5 Company Mood · 40% wound risk · +2 One-Handed XP

  › Laugh with them and let it pass
      +1 Calculating · −2 respect from younger recruits · no risk

  › Find the lieutenant and report him
      +3 scrutiny on Wulfric · −1 Company Mood · −1 Valor · rat-flag risk
```

Trait effects use **only the vanilla-defined traits** (verified against `TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultTraits.cs:73-81`): Mercy, Valor, Honor, Generosity, Calculating. No custom-added trait ids in this design; a mod-side trait expansion would be its own subsystem decision. The special traits (PersonaCurt / PersonaEarnest / Surgery / RogueSkills / ScoutSkills / Commander / etc.) defined in `DefaultTraits.cs:83-109` are out of scope here.

Passive XP drift continues independently of events: each Duty declares 1–3 primary skills that gain small XP on an hourly or daily tick, magnitudes capped at Trivial-band per agency v3's envelope contract. Events are the only path for Moderate+ magnitude changes.

## Consolidation table — what's absorbed, what's cut

| From | To | Disposition |
| :--- | :--- | :--- |
| Routine concept (agency v3) | Duty | **Replaced.** The seven abstract stances are gone as first-class entities. |
| "Drill with the line" routine | `duty.infantry` | Absorbed. |
| "Practice at the butts" routine | `duty.archer` / `duty.crossbowman` | Absorbed, faction-filtered. |
| "Ride with the scouts" routine | `duty.pathfinder` | Absorbed. |
| "Help the wounded" routine | `duty.field_medic` | Absorbed. |
| "Forage and hustle" routine | `duty.provisioner` | Absorbed. |
| "Learn the books" routine | `duty.siegewright` | Absorbed (Engineer skill focus). |
| "Stay close to the lord" routine | — | **Cut.** Personal posture, not a military job. Trait drift via event choices instead. |
| "Keep your head down" routine | — | **Cut.** Same reason; scrutiny drift comes from choices + time, not a stance. |
| "Routine service" default | — | **Cut.** Every enlistment starts in a real Duty — no null-default. |
| `ServiceStanceManager`, `ServiceStance`, `service_stances.json` | — | Never shipped; design is retired before implementation. |
| `EnlistedFormationAssignmentBehavior` (existing) | Duty system drives formation | Keep the behavior; the Duty system supplies the class instead of the old hardcoded mapping. |
| `DutiesOfficerRolePatches` (existing) | Duty system claims role | Keep the patches; Duty system decides which role to claim. |
| `UPCOMING` section (news v2 main body) | `PROSPECTS` | Renamed. Contract preserved. |
| `YOU` section (news v2 camp body) | `STATUS` | Renamed. Contract preserved. |
| `CAMP ACTIVITIES` + `SINCE LAST MUSTER` (news v2 camp body) | `COMPANY NEWS` | Merged into one company-scope paragraph in Camp. Muster countdown and period recap prose both live inside COMPANY NEWS now. |
| `HEADLINES [NEW]` top-level option | Inline `NEW:` in Camp's STATUS | Replaced. Same mark-read bookkeeping. |
| Main-menu `Equipment` option | Camp → Quartermaster | Moved. No menu entry on main. |

## Relation to prior specs

Three docs from 2026-04-23 have parts that this spec supersedes. Each requires explicit disposition:

### `2026-04-23-news-and-status-unification-design.md` (v2)

- **Supersedes:** section headers (`UPCOMING` → `PROSPECTS`, `YOU` → `STATUS`, `CAMP ACTIVITIES` + `SINCE LAST MUSTER` → `COMPANY NEWS`).
- **Preserves:** the two-feed separation (Kingdom / Personal), the frozen-weekly regen trigger model, `BuildKingdomDigestSection(windowDays)` refactor, the three persisted-state removals (`_dailyBriefCompany` / `_dailyBriefUnit` / `_lastDailyBriefDayNumber`), the `CampaignTimeControlMode` taxonomy (fast-family vs play-family).
- **Disposition:** leave v2 in place for the parts this spec preserves; add a one-line amendment note at the top of v2 pointing to this spec for the section-rename.

### `2026-04-23-player-agency-redesign.md` (v3)

- **Supersedes:** Service stance model (section "Service stances" entirely), `ServiceStance`, `ServiceStanceManager`, `service_stances.json`, `EnvelopeKind.ServiceStance`.
- **Depends on (not-yet-built substrate):** `MutationCategory` / `MagnitudeBand` / `Envelope` / `AgencyGate` / `StateMutator` / `EnvelopeAcceptancePreview`, the storylet `preview` schema addendum, the observer→enforce phasing plan, the validator Phase 16 "preview required on high-stakes" rule, `ChoreThrottle`. **V3 proposed these; none have been built.** `src/Features/Agency/` does not exist; grep for `class StateMutator|class AgencyGate|class Envelope` in `src/` returns no matches. Every trait/gold/health/scrutiny effect in this spec's sample events assumes the substrate exists. Building it is Plan B in the Implementation plans section below, and it is a hard dependency for Plan G (storylet content authoring).
- **Disposition:** v3 requires an amendment commit renaming *"service stance envelope"* to *"Duty envelope"* throughout and replacing the stance list with a pointer to this spec's Duty list. V3 had reserved class offsets 51 (`CampActivityActivity`) and 52 (`ChoreThrottleStore`) in the spec but neither type was ever built. This spec reclaims offset 51 for `DutyActivity`. Offset 52 remains reserved for `ChoreThrottleStore` if a Phase 2 cooldown subsystem is built.

### `2026-04-23-agency-news-status-integration.md` (plan, 1262 lines, committed 2026-04-23)

- **Status:** actionable plan for typed dispatch routing (`DispatchDomain` / `DispatchSourceKind` / `DispatchSurfaceHint`). Still needed. This spec does not invalidate it.
- **Impact:** one `DispatchSurfaceHint` value (`Upcoming`) needs renaming to `Prospects` to match this spec's section header. One `DispatchSourceKind` value (`ServiceStance`) needs renaming to `Duty`. These are small edits to the enum declarations and the ~30 consumer sites.
- **Disposition:** plan continues after a small amendment pass. Implementer of the plan should read this spec first.

## DutyProfile rename — deferred

`Enlisted.Features.Activities.Orders.DutyProfileBehavior` / `DutyProfileId` / `DutyProfileSelector` / `DutyCooldownStore` (save-definer offset 50) + 140 storylets in `ModuleData/Enlisted/Storylets/duty_*.json` + `EnlistedDutyEmitterBehavior` + `EnlistedDutyOpportunityBuilder` collectively use "duty" to mean **what the army is doing** (Garrisoned / Marching / Besieging / etc.). Plan 4 shipped this on 2026-04-22.

Renaming these to `ArmySituation*` is ~12 C# files, 7 JSON ids, a save-offset audit, and 140 storylet pool-id references.

**This spec does not require the rename.** The player-facing word is unambiguous in menu context ("Your duty is Infantry"); internal code retains its three-way overload with code comments documenting it. A rename can happen in a follow-up refactor if the overload proves painful. If the rename *were* in scope here, blast radius would be large enough to warrant its own dedicated plan.

Also unrenamed: the "on duty" / "off duty" wording used by opportunity detection (e.g. `CampOpportunityGenerator.cs:1498`). That refers to whether the player has an active order and is unrelated to both the army situation and the player's Duty. Code comments can clarify; no behavioral change.

## Implementation plans + dependencies

This spec is the single design of record. Implementation is split into eight named plans that can be written, reviewed, and shipped separately. Each plan becomes its own doc under `docs/superpowers/plans/`.

| Plan | Scope | Depends on | Visible to player? |
| :--- | :--- | :--- | :--- |
| **A — Menu body restructure** | `enlisted_status` body: DISPATCHES (frozen weekly per news-v2 regen triggers) + PROSPECTS (live, replaces UPCOMING). `enlisted_camp_hub` body: COMPANY NEWS + STATUS (merges SINCE LAST MUSTER + UPCOMING + CAMP ACTIVITIES into COMPANY NEWS; merges YOU + RECENT ACTIVITY + STATUS into STATUS). Main menu options: Orders / Audience / Camp / Visit settlement (delete HEADLINES option + submenu, fold Equipment into Camp → Quartermaster). Camp menu options: Duty (greyed "coming with Plan C") / Quartermaster / Records (coming soon) / Companions (coming soon) / Retinue (coming soon) / Back. | — | Yes — immediate. |
| **B — Agency substrate** | `MutationCategory`, `MagnitudeBand`, `Envelope`, `AgencyGate`, `StateMutator`, `EnvelopeAcceptancePreview`, storylet `preview` schema, validator Phase 16. Observer-mode rollout per v3's Phase 1 plan: log what would be rejected before enforcing. | — | No (substrate). |
| **C — Duty core** | `DutyId` enum, `DutyRegistry`, `DutyManager` (claims save-definer class offset 51), `DutyActivity`, persistence + transition rules, faction filter reading `default_group` from troop XML, Duty selection menu wired to the Camp option, PROSPECTS + STATUS prose consume current Duty. No formation change, no party-role claim, no gear swap yet. | — | Partial — menu appears, Duty persists, prose reads it. |
| **D — Combat Duty → formation** | `CombatClassResolver.Resolve(hero)` queries `DutyManager.Current` first for the player, falls back to equipment-derived class for non-player heroes or when no Duty. Smoke tests across all four formation classes and mixed-culture battles. | C | Yes — first battle after a Duty change reflects it. |
| **E — Support Duty → party roles** | Replace the four `return true` shells in `DutiesOfficerRolePatches` (`EffectiveEngineer / Scout / Quartermaster / Surgeon`) with `DutyManager.Current == <support duty>` checks that substitute `Hero.MainHero.CharacterObject` into `__result` and return `false` on match. Per-role smoke test verifies the player's skill actually claims the party role. | C | Yes — skill bonuses visible. |
| **F — Gear reissue via Quartermaster** | `Quartermaster.ReissueForDuty(DutyId, int tier)`. Full preservation contract per the "Gear re-issue ritual" section above (quest items / civilian gear preserved; upgraded items return to inventory with party-inventory overflow + 50% sellback; critical-supply block defers reissue). Rank-up hook. | C | Yes — visible gear change on Duty pick. |
| **G — Duty storylet pools + content authoring** | Initial Phase 1 content pass: ~30 events across 9 Duties (6 Infantry, 4 Field Medic, 4 Pathfinder, 2 each for Archer/Crossbowman/Cavalry/Provisioner/Siegewright, 1 HA flavor). Each event declares `agency.role` and uses the preview schema. Routed through agency substrate. | B, C | Yes — CK3-style popups begin firing. |
| **H — Doc amendments** | News-v2 amendment note (section renames: UPCOMING → PROSPECTS; YOU → STATUS; SINCE LAST MUSTER + CAMP ACTIVITIES → merged into COMPANY NEWS). Agency-v3 amendment ("stance envelope" → "Duty envelope" throughout; stance list replaced by pointer to this spec). Integration-plan amendment (`DispatchSurfaceHint.Upcoming` → `Prospects`; `DispatchSourceKind.ServiceStance` → `Duty`). | — | No (docs only). |

### Ship order (recommended)

1. **A** — quickest visible win; fixes the body flood that was the original pain. No dependencies. Ships the menu shell.
2. **H** — in parallel with A since A changes the vocabulary those docs describe.
3. **C** — Duty persistence + Duty menu; unblocks D/E/F/G.
4. **D** — Combat Duty → formation. First real in-battle consequence of picking a Duty.
5. **F** — Gear reissue. Closes the "I look like my Duty" loop.
6. **E** — Support Duty → party roles. Rounds out the Duty experience.
7. **B** — Agency substrate. Observer mode first, collect logs, tune.
8. **G** — Content authoring. CK3-style events land once substrate + Duty core are stable.

Alternative: **B** can run in parallel with **A** and **C** if two tracks are available. B is invisible to the player either way; putting it earlier reduces wait for Plan G.

### Explicit non-goals

- Formation-choice as its own menu option (absorbed into Combat Duty; no separate picker).
- Medic NPC + chronic health system + buy-medicine subsystem (explicitly deferred by the user earlier in the design conversation).
- Faction-specialty Duties as distinct ids (`duty.ulfhednar` / `duty.fian_path` etc.). The `default_group`-based filter labels them as culture-variants of the base Duty; distinct ids can come later.
- `DutyProfile` → `ArmySituation` internal rename. Three-way overload documented; rename deferred.
- Orders / Records / Companions / Retinue sub-menus. Each needs its own small design pass before its plan is written.

## Proposed Duty tuning (initial defaults — revise from Plan B / G log data)

**Pacing is governed by the existing `DensitySettings` in `src/Features/Content/DensitySettings.cs`** — this spec does not invent its own cadence knobs. Live values:

| Knob | Default | Tunable via |
| :--- | :--- | :--- |
| `ModalFloorInGameDays` | **5** (normal) / 3 (dense) / 7 (sparse) | `event_density` in `enlisted_config.json` |
| `ModalFloorWallClockSeconds` | **60** | code default today (Phase 2+ may expose to JSON) |
| `QuietStretchDays` | **14** | code default |
| `CategoryCooldownDays` | **12** | code default |
| `SpeedDownshiftOnModal` | **true** | `speed_downshift_on_modal` in `enlisted_config.json` |

**What this means for Duty events at fast-forward.** Duty-pool events share a single category (working name `duty.event`). The category cooldown is 12 game days. At `SpeedUpMultiplier = 4` (FF default), that's ~3 real minutes minimum between Duty modals. The wall-clock 60-second floor is an additional hard brake. And `SpeedDownshiftOnModal = true` means fast-forward auto-drops to 1× the instant a modal fires — the player is never surprised mid-FF; they see the menu slide in at readable speed.

**Duty tuning inputs — pool weight and drift, not cadence.** Per-duty numbers below control (a) how big each pool is and (b) what the Trivial-band passive XP drift looks like. The actual modal firing rate is bounded by the DensitySettings rails above, not by these entries.

| Duty | Primary skills (Trivial XP drift) | Modal pool size (initial) |
| :--- | :--- | :--- |
| Infantry | One-Handed, Two-Handed, Polearm, Athletics | 6 events |
| Archer | Bow, Throwing | 2 events |
| Crossbowman | Crossbow, Athletics | 2 events |
| Cavalry | Riding, Polearm | 2 events |
| Horse Archer | Bow, Riding | 1 event (faction-flavored variants — Khuzait steppe, Aserai mamluke, Empire bucellarii) |
| Pathfinder | Scouting, Riding | 4 events |
| Field Medic | Medicine | 4 events |
| Provisioner | Trade, Steward | 2 events |
| Siegewright's Aide | Engineering | 2 events |

Trivial XP magnitudes: 1–3 XP per drift tick, per the agency v3 Trivial-band ceiling. Drift ticks fire on the hourly / daily tick — they are NOT Modal, so they bypass the pacing rails entirely and accumulate quietly into STATUS prose. Event-choice XP magnitudes (when a modal resolves): 2–10 in the primary skill, never above Minor-band without a preview.

## Validation

### Gates

- `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` passes.
- `python Tools/Validation/validate_content.py` passes — including agency v3's Phase 16 preview-on-high-stakes and this spec's additions below.

### New validator rails

- **Duty pool coverage** — every combat duty in a faction's available list has at least one authored event after Plan G's initial content pass. Warning-only during Plan G; error once content saturates.
- **Duty-storylet agency metadata** — every authored Duty-pool storylet must declare `agency.role` ∈ {`duty_drift`, `duty_event`}, `agency.source_kind = "Duty"`, and a `duty_id` matching a registered `DutyId`.
- **Trait references** — any storylet effect referencing a trait must use one of the vanilla five (Mercy / Valor / Honor / Generosity / Calculating). Custom trait ids rejected.

### In-game smoke checklist

- Enlist with Vlandia at T1. Open `enlisted_status` → body shows DISPATCHES + PROSPECTS; option list is Orders / Audience / Camp / Visit settlement.
- Open Camp → body shows COMPANY NEWS + STATUS; option list is Duty / Quartermaster / Records / Companions / Retinue / Back.
- Open Duty → Combat section shows Infantry / Crossbowman / Cavalry as available; Archer greyed with *"Vlandia uses crossbow"* tooltip; HA greyed with *"Khuzait only"*. Support section shows all four.
- At T1/T2, confirm Cavalry is greyed with *"Vlandian cavalry requires T3"* (Vlandian cavalry branch starts T3 with Squire — no cavalry troop exists at T1/T2). Rank up to T3 and re-open Duty → Cavalry is available → pick Cavalry → return to Camp → COMPANY NEWS prose mentions the QM re-issue → equipment slot swapped to Vlandian Squire (T3) loadout.
- Fast-forward 3 in-game days in camp → at least one Infantry (after re-picking) ambient event fires as a modal with three choice options + inline effect previews.
- Save → reload → Duty persists; active storylet pool state persists; formation assignment in next battle matches current Duty.
- Enlist with Khuzait → Duty menu shows Horse Archer available; faction filter works.
- Switch Duty mid-campaign → formation assignment in next battle updates; party role claim updates.

## Risks + mitigations

1. **Duty word triple-overload.** Internal code uses "duty" for army situation (Plan 4), player MOS (this spec), and order-active state. Mitigation: player-facing label is unambiguous in menu context; code comments document the overload; rename deferred to a dedicated follow-up.
2. **Faction-filter false negatives.** The Vlandian militia-archer id-vs-name mismatch is a reminder that id suffixes are unreliable. Mitigation: filter by troop equipment / weapon class, not id. Unit test covers the Vlandian militia edge case.
3. **Silent gear swap may feel abrupt.** Phase 1 ships the minimal ritual; player feedback informs whether a confirmation modal + upgrade preservation are needed in Phase 2.
4. **Content authoring burden.** ~30 Phase 1 events across 9 Duties is a non-trivial authoring pass. Mitigation: reuse existing duty_*.json storylet framing where the content maps cleanly; cut to minimum viable coverage for ship.
5. **Save-compat with agency v3's unshipped stance state.** v3 proposed `ServiceStanceManager.Current` as a persisted string. That state was never shipped, so there's no migration. If a dev branch carrying v3 code reaches a save, load-on-new-code silently drops the stance field per TaleWorlds `IDataStore.SyncData` tolerance.
6. **Plan 4 `duty_*` storylet pool references.** 140 storylets in `duty_marching.json` etc. reference army-situation pool ids, not player-Duty pool ids. Pool id namespaces are distinct; no collision. Mitigation: the new Duty-pool ids are prefixed `duty_combat_*` / `duty_support_*` to visually distinguish from army-situation `duty_garrisoned.json` etc.

## Open questions

1. **Duty-transition cooldown.** Should switching Duty have a cost (gold? time? relation dip with the lord? muster-gated)? Initial default (Plan C): free and instant. Tuning deferred to post-D log data.
2. **Support Duty × battle formation.** When a Support Duty (e.g. Field Medic) is active and the army enters battle, what formation does the player join? Initial default (Plan D): Infantry formation. A follow-up may add a second-tier "combat fallback" preference in each Support Duty's definition.
3. **Concurrent Support + Combat?** Could the player claim both a Support role and a Combat Duty simultaneously (e.g. Field Medic + Infantry)? Initial: no — one Duty at a time. A follow-up might allow Support as a secondary if the code supports it.
4. **Per-faction gear-preservation on Duty change.** The minimal ritual returns upgraded items to inventory. Should those items persist across Duty → Duty changes, or get sold back to the QM at book value on the second swap? Initial (Plan F): persist in inventory indefinitely.

## Changelog

- **v1 (2026-04-24):** initial draft. Consolidates news v2, agency v3, integration plan. Faction-troop + trait claims verified against `SandBoxCore/ModuleData/spnpccharacters.xml` and `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultTraits.cs`.
- **v2 (2026-04-24):** corrections from adversarial review (ChatGPT) re-verified against primary sources. Fixes:
  1. **Problem statement** — main `enlisted_status` body has 3 sections (not 4); UPCOMING lives in Camp hub body, not main. Corrected line cites (`EnlistedMenuBehavior.cs:3315-3347` for main; `:1595-1628` for Camp).
  2. **Horse Archer availability** — not Khuzait-only. Khuzait dominant, Aserai (Mameluke Cavalry + Mameluke Heavy Cavalry, id misleading), Empire (Bucellarii). Confirmed via `grep 'default_group="HorseArcher"'` in `spnpccharacters.xml`. Battania/Sturgia/Vlandia truly none.
  3. **Faction-filter rule** — read `default_group` attribute from troop XML, not id suffix or equipment. Handles both Vlandian militia-archer and Aserai mameluke-cavalry id-vs-`default_group` mismatches in one rule.
  4. **Battle formation wiring** — corrected "already wired" to "requires new wiring". Today's path is `CombatClassResolver.cs:25-40` reading equipment flags → `EnlistedFormationAssignmentBehavior.cs:466-470`. Phase 1 change: resolver queries `DutyManager.Current` first.
  5. **Party role wiring** — corrected "already wired" to "requires new wiring". `DutiesOfficerRolePatches` lines 57, 104, 150, 192 all show `return true;` shells — patches currently run vanilla. Phase 1 replaces the returns with Duty-match checks.
  6. **Agency substrate status** — corrected "preserves" to "depends on (not-yet-built substrate)". `src/Features/Agency/` does not exist; nothing from v3 was built. Plan B in Implementation plans builds it; Plan G depends on it.
  7. **Gear reissue contract** — expanded from 4 bullets to a full contract: preservation rules (quest items / civilian set / upgraded items), overflow handling (party inventory → 50% sellback), critical-supply block interaction, explicit no-confirmation-modal decision.
  8. **Vlandia T1 cavalry smoke test** — invalid under tier rule (Vlandian cavalry branch starts T3). Changed to verify Cavalry greyed at T1/T2 with reason tooltip, then rank up and retest at T3.
- **v2 scope shift:** "Phase 1 / Phase 2+" structure replaced with "Implementation plans + dependencies" — per user request, one big spec, multiple named plans (A-H) ship separately. No single-phase boundary.
- **v2 pacing correction (user feedback):** original tuning table fabricated per-Duty cadence values (~2-3 game days) that would trigger modals every ~30-60 real seconds at fast-forward. Pacing is already governed by the existing `DensitySettings` in `src/Features/Content/DensitySettings.cs` (5-day in-game floor, 60-sec wall-clock floor, 12-day category cooldown, `SpeedDownshiftOnModal = true`). Duty events share the `duty.event` category and inherit those rails; spec no longer invents its own. Table now shows pool size + XP drift only.
