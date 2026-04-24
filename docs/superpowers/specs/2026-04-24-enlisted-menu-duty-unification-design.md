# Enlisted Menu + Duty Unification — Design

**Status:** Draft v1 (2026-04-24). Consolidates the menu restructure, the news-unification body layout, and the Routine→Duty reframe into one surface design. Supersedes parts of three prior docs (see "Relation to prior specs").

## Problem statement

Today's enlisted menu has three layered problems:

1. **Body flood.** `enlisted_status` body concatenates 15–20 short lines across four named sections (KINGDOM REPORTS / COMPANY REPORTS / PLAYER STATUS / UPCOMING). On fast-forward, several sections tick-rebuild every ~5 seconds and the eye cannot land. See `EnlistedMenuBehavior.cs:~3320-3491` for the current render path.
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

1. **Battle formation** — Combat Duties set the player's formation slot (Infantry / Ranged / Cavalry / Horse Archer) via `EnlistedFormationAssignmentBehavior`. Already wired; the Duty system just supplies the class.
2. **Party role** — Support Duties claim the relevant party role (Surgeon / Scout / Quartermaster / Engineer) via the Harmony patches in `DutiesOfficerRolePatches.cs`. Already wired; the Duty system just claims the role.
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
| `duty.horse_archer` | Horse Archer | HorseArcher | **Khuzait only** (verified against `SandBoxCore/ModuleData/spnpccharacters.xml` — only `khuzait_horse_archer` and `khuzait_heavy_horse_archer` exist in the troop data). |

Faction specialties (Fian-path / Ulfhednar / Mameluke / Cataphract) are handled as **named variants of the base combat duty** rather than separate duty ids — e.g. selecting `duty.infantry` as a Sturgian player issues Ulfhednar-branch gear. Specialty-branch display labels can be authored per culture into a `duty_culture_labels.json` lookup; not required for ship.

**SUPPORT** (universal):

| Id | Display | Role claim | Skill focus |
| :--- | :--- | :--- | :--- |
| `duty.pathfinder` | Pathfinder | Scout | Scouting / Riding |
| `duty.field_medic` | Field Medic | Surgeon | Medicine |
| `duty.provisioner` | Provisioner | Quartermaster | Steward / Trade |
| `duty.siegewright` | Siegewright's Aide | Engineer | Engineering |

### Faction filtering

For the Combat section, the Duty menu must query the enlisted lord's culture and display only duties whose troop branch exists in that culture. Implementation note:

- Enumerate the current faction's troop tree from the lord's culture at menu-init.
- For each combat duty, check whether the culture has a troop at the player's tier in that branch.
- **If yes** — show the option, label with the troop's display name at that tier (e.g. *"Infantry · Vlandian Sergeant (T3)"*).
- **If no** — grey the option and show the reason inline (e.g. *"Archer — Vlandia uses crossbow"*, *"Horse Archer — Khuzait only"*).

**Vlandia id-vs-name caveat** (discovered during verification): the Vlandian militia troop `vlandian_militia_archer` has id `…_archer` but display name *"Vlandian Militia Crossbowman"*. The filter must inspect the troop's actual equipment / weapon class, not the id suffix, to classify the branch correctly.

### Gear re-issue ritual (first-cut, deliberately narrow)

On Duty change or rank-up that moves the player into a different troop tier:

1. Quartermaster silently swaps the player's equipped loadout to the troop's default equipment for the new (faction, duty, tier) combination.
2. Any previously-upgraded items the player paid gold for return to the player's inventory (not destroyed). The player can re-equip them manually at the QM.
3. No confirmation modal fires. The change is narrated in the Camp body's COMPANY NEWS section *"The quartermaster re-issued your kit — Vlandian Sergeant by rank, mail and kite shield."*
4. Gold cost of past upgrades is **not** refunded.

This is deliberately minimal. A richer ritual — confirmation modal, gear-preservation negotiation, "keep the old sword" option — is a follow-up if the minimal version feels too abrupt.

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
- **Preserves:** `MutationCategory` / `MagnitudeBand` / `Envelope` / `AgencyGate` / `StateMutator` / `EnvelopeAcceptancePreview`, the `preview` schema addendum on storylets, the observer→enforce phasing plan, the validator Phase 16 "preview required on high-stakes" rule, `ChoreThrottle` for short activity overrides. These are the substrate; what sits on top changes, the substrate doesn't.
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

## Phasing

This spec describes **Phase 1** of the larger design: the menu shell + Duty reframe. The open items below are intentional deferrals, not gaps.

### In scope (Phase 1)

- `enlisted_status` body rewrite: DISPATCHES (frozen weekly) + PROSPECTS (live).
- `enlisted_camp_hub` body rewrite: COMPANY NEWS + STATUS.
- Main menu option list: Orders / Audience / Camp / Visit settlement.
- Camp menu option list: Duty / Quartermaster / Records / Companions / Retinue / Back.
- Duty system: `DutyId` enum, `DutyRegistry`, `DutyActivity` (claims next-available save-definer class offset 51; v3 had proposed 51 for `CampActivityActivity` but nothing was built, so the offset is free), `DutyManager` for persistence and transitions, combat faction filter, minimal gear re-issue ritual.
- Duty → formation wire (replaces hardcoded mapping in `EnlistedFormationAssignmentBehavior`).
- Duty → party role wire (replaces hardcoded mapping in `DutiesOfficerRolePatches`).
- Minimum 1 Combat Duty storylet pool (`duty.infantry` — ~6 events to start) + 1 Support Duty pool (`duty.field_medic` — ~4 events) authored as proof-of-content. Other duty pools get 1 sample each; full content fill is phase 3.
- News-spec v2 amendment for section renames.
- Agency v3 amendment for "stance envelope" → "Duty envelope" language.
- Integration plan amendment for `Upcoming` → `Prospects`, `ServiceStance` → `Duty` rename.

### Deferred to Phase 2+ (sub-menu design + content)

- **Orders sub-menu** — active + proposed orders list, accept flow, preview rendering. Inherits preview contract from agency v3.
- **Records sub-menu** — scope TBD. Candidate content: rank history, orders tally, paths walked, commendations, discipline record. Needs its own mini-design.
- **Companions sub-menu** — wraps `CompanionAssignmentManager`. Role-assignment UI.
- **Retinue sub-menu** — wraps `RetinueManager`. Recruitment-grant log, casualty report, retinue equipment surface.
- **Per-Duty event cadence tuning** — initial values proposed below; Phase 1 logs inform Phase 2 numbers.
- **Storylet pool authoring** — ~8–12 events per Duty pool. Authoring work for Phase 3.
- **Gear re-issue confirmation modal + upgrade preservation** — richer ritual if the minimal silent swap feels too abrupt.

### Deferred to later phases (explicitly out of scope)

- Formation-choice as its own menu option (absorbed into Combat Duty).
- Medic NPC + chronic health system + buy-medicine subsystem.
- Faction-specialty Duties as distinct ids (`duty.ulfhednar` / `duty.fian_path` etc.). Phase 1 uses faction-variant labels only.
- `DutyProfile` → `ArmySituation` internal rename.

## Proposed Duty tuning (Phase 1 defaults — revise from log data)

| Duty | Primary skills (Trivial XP drift) | Event cadence | Storylet pool size (Phase 1) |
| :--- | :--- | :--- | :--- |
| Infantry | One-Handed, Two-Handed, Polearm, Athletics | ~3 days | 6 events |
| Archer | Bow, Throwing | ~3 days | 2 events |
| Crossbowman | Crossbow, Athletics | ~3 days | 2 events |
| Cavalry | Riding, Polearm | ~3 days | 2 events |
| Horse Archer | Bow, Riding | ~3 days | 1 event (Khuzait-only flavor) |
| Pathfinder | Scouting, Riding | ~2 days (scout events are more frequent) | 4 events |
| Field Medic | Medicine | ~2 days | 4 events |
| Provisioner | Trade, Steward | ~4 days | 2 events |
| Siegewright's Aide | Engineering | ~4 days | 2 events |

Trivial XP magnitudes: 1–3 XP per drift tick, per the agency v3 Trivial-band ceiling. Event-choice XP magnitudes: 2–10 in the primary skill, never above Minor-band without a preview.

## Validation

### Gates

- `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` passes.
- `python Tools/Validation/validate_content.py` passes — including agency v3's Phase 16 preview-on-high-stakes and this spec's additions below.

### New validator rails

- **Duty pool coverage** — every combat duty in a faction's available list has at least one authored event in Phase 1. Warning-only for Phase 1 content; error in Phase 3.
- **Duty-storylet agency metadata** — every authored Duty-pool storylet must declare `agency.role` ∈ {`duty_drift`, `duty_event`}, `agency.source_kind = "Duty"`, and a `duty_id` matching a registered `DutyId`.
- **Trait references** — any storylet effect referencing a trait must use one of the vanilla five (Mercy / Valor / Honor / Generosity / Calculating). Custom trait ids rejected.

### In-game smoke checklist

- Enlist with Vlandia at T1. Open `enlisted_status` → body shows DISPATCHES + PROSPECTS; option list is Orders / Audience / Camp / Visit settlement.
- Open Camp → body shows COMPANY NEWS + STATUS; option list is Duty / Quartermaster / Records / Companions / Retinue / Back.
- Open Duty → Combat section shows Infantry / Crossbowman / Cavalry as available; Archer greyed with *"Vlandia uses crossbow"* tooltip; HA greyed with *"Khuzait only"*. Support section shows all four.
- Pick Cavalry → return to Camp → COMPANY NEWS prose mentions the QM re-issue → equipment slot swapped to Vlandian T1 cavalry loadout.
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

1. **Duty-transition cooldown.** Should switching Duty have a cost (gold? time? relation dip with the lord? muster-gated)? Default for Phase 1: free and instant. Defer tuning to Phase 2 log data.
2. **Support Duty × battle formation.** When a Support Duty (e.g. Field Medic) is active and the army enters battle, what formation does the player join? Default for Phase 1: Infantry formation. Phase 2 may add a second-tier "combat fallback" preference in each Support Duty's definition.
3. **Concurrent Support + Combat?** Could the player claim both a Support role and a Combat Duty simultaneously (e.g. Field Medic + Infantry)? Phase 1: no — one Duty at a time. Phase 2 might allow Support as a secondary if the code supports it.
4. **Per-faction gear-preservation on Duty change.** The minimal ritual returns upgraded items to inventory. Should those items persist across Duty → Duty changes, or get sold back to the QM at book value on the second swap? Phase 1: persist in inventory indefinitely.

## Changelog

- **v1 (2026-04-24):** initial draft. Consolidates news v2, agency v3, integration plan. Faction-troop + trait claims verified against `SandBoxCore/ModuleData/spnpccharacters.xml` and `TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.CharacterDevelopment/DefaultTraits.cs`.
