# Plan 2 — CK3 Wanderer Companion Substrate: Verification Report

**Status:** 🟡 Code-level verification complete; in-game manual smoke pending human operator (T21-T24).

**Plan:** [2026-04-24-ck3-wanderer-companion-substrate.md](2026-04-24-ck3-wanderer-companion-substrate.md)
**Brief:** [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md)
**Date:** 2026-04-25

---

## §1 — What shipped

### Commits on `development`

| Commit | Phase | Tasks | Summary |
| :--- | :--- | :--- | :--- |
| `57941d2` | Phase 1 | T1-T3 | archetype catalog + spawn factory |
| `da67462` | Phase 2 | T4-T11 | spawn slots + lifecycle handler |
| `8cbe9f7` | Phase 3 | T12-T18 | six archetype dialog catalogs + loader |
| `11d7d82` | Phase 4 | T19-T20 | Camp menu Talk-to companion option |
| `f5f226c` | Phase 5 | T25 | verification doc + CLAUDE.md status |
| (this) | Phase 5+ | follow-up | lint cleanup, QM catalog glob narrowing |

### New files (12)

**Documentation (1)**
- `docs/Features/Companions/companion-archetype-catalog.md` — schema reference for `archetype_catalog.json`

**Verification (1)**
- `docs/superpowers/plans/2026-04-24-ck3-wanderer-companion-substrate-verification.md` — this report

**Content (7)**
- `ModuleData/Enlisted/Companions/archetype_catalog.json` — six companions × three archetypes (18 archetype rolls)
- `ModuleData/Enlisted/Dialogue/companion_sergeant.json` — 20 nodes
- `ModuleData/Enlisted/Dialogue/companion_field_medic.json` — 20 nodes
- `ModuleData/Enlisted/Dialogue/companion_pathfinder.json` — 20 nodes
- `ModuleData/Enlisted/Dialogue/companion_veteran.json` — 20 nodes
- `ModuleData/Enlisted/Dialogue/companion_qm_officer.json` — 20 nodes
- `ModuleData/Enlisted/Dialogue/companion_junior_officer.json` — 20 nodes

**C# code (3)**
- `src/Features/Companions/CompanionSpawnFactory.cs` — predicate-based troop selection, archetype roll, MainParty roster placement
- `src/Features/Companions/CompanionLifecycleHandler.cs` — singleton behavior subscribing OnEnlisted / OnTierChanged / OnDischarged / OnEnlistmentEnded / HeroKilledEvent
- `src/Features/Companions/Data/CompanionDialogueCatalog.cs` — singleton loader for `companion_*.json`, includes node/option/context POCOs

### Edits (5)

- `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` — 24 SyncKey-persisted companion fields, six `GetOrCreateX` methods, `GetSpawnedCompanions` / `GetCompanionTypeId` / `ClearCompanionSlot` / `ReleasePerLordCompanions` / `EnsureCompanionFieldsInitialized`
- `src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs` — Camp menu slot 6 "Talk to..." option, `OnTalkToCompanionSelected` callback, `ShowCompanionSelectionInquiry`, `StartConversationWithCompanion`
- `src/Mod.Entry/SubModule.cs` — registers `CompanionLifecycleHandler` alongside Plan 1 substrate behaviors
- `Enlisted.csproj` — `<CompanionsData>` ItemGroup + MakeDir + Copy in AfterBuild; Compile entries for the three new C# files
- `Tools/Validation/validate_content.py` — Phase 18 (companion dialog catalog schema validation) added; called from `main()` after Phase 17
- `docs/error-codes.md` — regenerated (124 Surfaced calls across 29 categories; +9 codes net across COMPANION + INTERFACE categories)

### Save-data layout

EnlistmentBehavior uses string-keyed `SyncKey` (no numeric `SaveableField` IDs), so no save-definer offset is consumed by Plan 2. Twenty-four new keys land:

```
_sergeantHero, _sergeantArchetype, _sergeantRelationship, _hasMetSergeant,
_fieldMedicHero, _fieldMedicArchetype, _fieldMedicRelationship, _hasMetFieldMedic,
_pathfinderHero, _pathfinderArchetype, _pathfinderRelationship, _hasMetPathfinder,
_veteranHero, _veteranArchetype, _veteranRelationship, _hasMetVeteran,
_qmOfficerHero, _qmOfficerArchetype, _qmOfficerRelationship, _hasMetQmOfficer,
_juniorOfficerHero, _juniorOfficerArchetype, _juniorOfficerRelationship, _hasMetJuniorOfficer
```

Save-class offset budget for Plans 3-7 (54-58 + enum 84) was claimed by Plan 1; Plan 2 consumes none.

---

## §2 — Verification gates passed

- ✅ `MSBuild.exe Enlisted.sln -p:Configuration='Enlisted RETAIL' -p:Platform=x64` — clean (0 warnings, 0 errors) after each of the four phase commits
- ✅ `python Tools/Validation/validate_content.py` — passes; Phase 18 reports `OK: 6 companion dialog catalog(s) validated, 120 nodes total.`
- ✅ `python Tools/Validation/generate_error_codes.py` — registry regenerated; 124 Surfaced call sites across 29 categories; staged
- ✅ `dotnet format Enlisted.sln whitespace --verify-no-changes` and `style --severity warn` — both exit 0 for `src/Features/Companions/**` (the three new C# files and the inherited Conversations/Data/QMDialogueCatalog.cs glob narrowing). Pre-existing CHARSET pollution across the wider codebase (Plan 1 wanderer substrate, Career-loop family Plan 1) is out of Plan 2 scope; tracked separately.
- ✅ All new C# files normalized to CRLF via `Tools/normalize_crlf.ps1` AND BOM-stripped via PowerShell to satisfy the `.editorconfig charset = utf-8` rule. (The `normalize_crlf.ps1` script unconditionally prepends a UTF-8 BOM per CLAUDE.md known footgun; lint enforcement requires UTF-8 without BOM.)
- ✅ Six archetype JSON catalogs deploy to `Modules\Enlisted\ModuleData\Enlisted\Dialogue\` via the existing `<DialogueData>` glob
- ✅ `archetype_catalog.json` deploys to `Modules\Enlisted\ModuleData\Enlisted\Companions\` via the new `<CompanionsData>` ItemGroup + Copy step
- ✅ `CompanionSpawnFactory.SpawnCompanion` API verified against decompile: `HeroCreator.CreateSpecialHero(template, settlement, faction, supporterOfClan, age)`, `HeroDeveloper.SetInitialSkillLevel(skill, value)`, `HeroDeveloper.AddAttribute(attr, value, checkUnspentPoints)`, `Hero.SetTraitLevel(trait, value)`, `Hero.SetName(fullName, firstName)`, `Hero.SetNewOccupation(Occupation.Soldier)`, `AddCompanionAction.Apply(clan, hero)`, `MobileParty.MainParty.MemberRoster.AddToCounts(co, count)`
- ✅ `CompanionLifecycleHandler` event subscriptions cross-checked against `EnlistmentBehavior` static event declarations at `EnlistmentBehavior.cs:8433` (`OnEnlisted`), `:8438` (`OnDischarged`), `:8490` (`OnTierChanged`), `:8496` (`OnEnlistmentEnded`)
- ✅ `EnsureCompanionFieldsInitialized` wired into `EnlistmentBehavior.SyncData` after `dataStore.SyncData` runs (CLAUDE.md known footgun #4 deserialize-skips-ctor pattern), AND into `CompanionLifecycleHandler.OnSessionLaunched` + `OnGameLoaded` (covers fresh-load and reload paths)
- ✅ Camp menu slot 6 free per Plan 1 §6.9 — verified by reading existing `RegisterCampHubMenu` index assignments (1: Records, 2: Companions, 3: Retinue, 4: Quartermaster, 7: My Lord, 100: Back; 5 + 6 free); slot 6 chosen for the new option per the plan

---

## §3 — Pending: in-game manual smoke (T21-T24)

The build + validator gates can't cover runtime spawn, save-load, combat, grievance, or death lifecycle. A human operator must run the four smoke recipes Plan 2 §5 documents:

### T21 — Vanilla `CompanionRolesCampaignBehavior` integration

1. Spawn Field Medic at T3 (use Debug Tools to force `EnlistmentBehavior.SetTier(3)`).
2. Open Camp → Talk to... → Field Medic.
3. Confirm vanilla "About your position in the clan..." option appears.
4. Assign Field Medic as Surgeon. Close conversation.
5. Confirm `MobileParty.MainParty.EffectiveSurgeon == fieldMedicHero` via mod debug log.
6. Confirm Surgery survival bonus reflects Field Medic's Medicine 100.

### T22 — Companion combat

1. Spawn Sergeant at T1 via Debug Tools.
2. Trigger a battle (engage bandits).
3. Confirm Sergeant appears as agent on player's side in deployment screen.
4. Set Sergeant `ShouldCompanionFight = false` via Camp → Companions → toggle.
5. Trigger another battle. Confirm Sergeant does NOT spawn (Plan 1 T6 stay-back gate fix verified active).
6. Repeat at T3, T5, T7 with the other companions.

### T23 — Companion grievance (vanilla `CompanionGrievanceBehavior`)

1. Spawn Sergeant (Valor +1) at T1. Have player retreat from a battle.
2. Wait 1-2 in-game days. Confirm Sergeant approaches with retreat-complaint dialog.
3. Spawn Field Medic (Mercy +1) at T3. Have player participate in a village raid.
4. Wait 1-2 days. Confirm Field Medic approaches with raid-complaint dialog.
5. Spawn QM Officer (Generosity +1) at T7. Skip a wage payment (or use Debug Tools).
6. Wait 1-2 days. Confirm QM Officer approaches with wage-complaint dialog.

### T24 — Companion death lifecycle

1. Spawn Veteran (per-Lord) at T5.
2. Use `KillCharacterAction.ApplyByBattle` (or wound-then-let-die in heavy battle) to kill the Veteran.
3. Confirm `OnHeroKilled` fires and a `companion_killed_in_battle` toast appears.
4. Confirm `_veteranHero` is nulled.
5. Confirm Veteran does NOT respawn until next enlistment + tier ≥ 5 (per-Lord lifecycle).
6. Repeat for Sergeant (per-player) — confirm permanent loss; Sergeant does NOT respawn even on next enlistment.

### Save-load round-trip (also pending)

1. Spawn all 6 companions (force-set tier 7 via Debug Tools).
2. Save → reload.
3. Confirm all 6 heroes persist with original archetype, name, skill, trait set.
4. Confirm 24 SyncKey fields preserved.
5. Load a save predating Plan 2 (no companion fields) — confirm `EnsureCompanionFieldsInitialized` reseats null archetype strings, no NRE on hourly tick or first companion-aware code path.

Pending tasks T21-T24 close on completion of the smoke; T25 (this report) marks Plan 2 ✅ once a human operator signs off.

---

## §4 — Deviations from plan as written

The plan v1 prescribes 25 sequential tasks. Several were collapsed during execution; deviations are intentional and documented inline in the per-task notes below.

| Plan task | Status | Note |
| :--- | :--- | :--- |
| T1 — schema doc | shipped | Schema revised mid-task to use predicate-based troop selection (`tierMin/tierMax/formationClass` with fallback chain) instead of plan's culture-keyed StringId map. Mirrors `EnlistmentBehavior.GetSergeantTierTroopTemplate` precedent and is more robust to game patches and modded factions. Doc updated to match. |
| T2 — archetype catalog JSON | shipped | csproj `<CompanionsData>` ItemGroup + MakeDir + Copy added per CLAUDE.md "non-recursive wildcard" rule. Catalog reflects predicate-based schema. |
| T3 — `CompanionSpawnFactory` | shipped | Predicate-based template selection with three-step fallback (drop formation → drop tier → BasicTroop). Civilian-equipment overlay (`civilianStyle: wealthy`) deferred to a follow-up — `civilianStyle` field is parsed but unused for now. Field Medic / QM Officer ship with their soldier-template default civilian equipment instead of merchant/preacher attire. Acceptable for code-only Plan 2; refactor will dedupe with `EnlistmentBehavior.TryApplyQuartermasterWealthyCulturalAttire`. |
| Pre-T4 — pin SaveableField IDs | collapsed (no-op) | Advisor recommended pinning a 24-slot numeric block. Reading the file showed `EnlistmentBehavior` uses string-keyed `SyncData` via the `SyncKey` helper, not numeric `SaveableField` attributes. Collision risk is just unique string keys; no offset block to pin. |
| T4-T9 — six per-companion spawn methods | bundled | Each individual T-task adds the same shape (4 fields + SyncKey + GetOrCreateX) to the same file. Implemented in a single edit pass with consistent naming, then routed through one private `GetOrCreateCompanion` helper. Functionally equivalent to six independent edits; cleaner diff. |
| T10 — SyncData + EnsureInitialized | shipped | `EnsureCompanionFieldsInitialized()` reseats null archetype strings; called from `SyncData` after `dataStore.SyncData` and from `CompanionLifecycleHandler.OnSessionLaunched` + `OnGameLoaded`. Mirrors `FlagStore.EnsureInitialized` / `QualityStore.EnsureInitialized` precedent. |
| T11 — `CompanionLifecycleHandler` | shipped | Singleton via `Instance` static accessor (set in `RegisterEvents` so it's available before T19/T20 menu callbacks run). Subscribes the four `EnlistmentBehavior` static events (`OnEnlisted`, `OnTierChanged`, `OnDischarged`, `OnEnlistmentEnded`) plus `CampaignEvents.HeroKilledEvent`. `TrySpawnAtCurrentTier` backfill covers pre-Plan-2 saves and lord-switch re-spawns. |
| T12-T17 — six archetype dialog catalogs | shipped | One consolidated `companion_<id>.json` per companion instead of plan's three-file split (`<id>_intro.json` / `<id>_dialogue.json` / `<id>_advice.json`). Single file is cleaner to validate and navigate; topic separation lives in node-id prefixes (`<id>_intro_*` / `<id>_topic_*` / `<id>_goodbye`). Six topics covered per catalog: introduction (3 archetype variants), introduction acknowledgement (3), root hub (1), two archetype-flavored topics (3+3), recent-battle gated on `has_recent_battle: true` (3), advice (3), goodbye (1). 20 nodes per catalog × 6 catalogs = 120 nodes. Authored via parallel implementer subagents using Sergeant as the structural template. |
| T18 — `CompanionDialogueCatalog` loader + Phase 18 validator | shipped | Plan 1 collapsed the loader stub and pushed it to Plan 2 (Plan 1 verification §4). Plan 2 ships the full loader with eight context fields specific to companions (companion_type, archetype, is_introduced, relationship_tier, player_tier, tier_min, tier_max, has_recent_battle). Phase 18 validates schemaVersion=1, dialogueType="companion", node ids present, archetype + companion_type values match `archetype_catalog.json` whitelist, and every option's next_node resolves within the same file. |
| T19-T20 — Camp menu Talk-to surface | shipped | Index 6 confirmed free; ShowCompanionSelectionInquiry mirrors ShowLordSelectionInquiry; StartConversationWithCompanion mirrors StartConversationWithLord (sea-aware via `MobileParty.MainParty.IsCurrentlyAtSea` + `conversation_scene_sea_multi_agent`). |
| Post-T25 — QM catalog log pollution fix | shipped | `QMDialogueCatalog.LoadFromJson` previously scanned `*.json` and warned six times per launch on the new `companion_*.json` files (rejected by its `dialogueType == "quartermaster"` check). Glob narrowed to `qm_*.json` to match the catalog scope. Verified by reading the new line at `QMDialogueCatalog.cs:56-60`. |
| Post-T25 — lint cleanup | shipped | IDE0011 (single-line if without braces, AGENTS.md Code Standards), IDE0005 (unused `TaleWorlds.Roster` + `TaleWorlds.ObjectSystem` usings) cleaned in `CompanionSpawnFactory.cs` and `CompanionLifecycleHandler.cs`. IDE0058 (Expression value never used) on `GetOrCreateX()` calls left in place — those return Hero for callers that need it; current call sites are fire-and-forget by design (matches the QM precedent). |
| T21-T24 — runtime smokes | pending human | This is a code-only verification at this stage. Smokes require a running game session and Debug Tools — see §3 for the four recipes. |
| T25 — verification report | this file | Status 🟡 mirroring Plan 1's verification format. Closes to ✅ when human operator signs off on T21-T24. |

---

## §5 — Architecture brief compliance

Cross-checked against [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md):

- §1 (save-definer offsets) — Plan 2 adds zero offsets; uses string-keyed SyncData on EnlistmentBehavior. No collision with Plans 1, 3-7, or surface-spec ranges.
- §2 (namespace conventions) — `Enlisted.Features.Companions` (Plan 2 owner) and `Enlisted.Features.Companions.Data` (subnamespace for catalog POCOs); no collisions.
- §3 (dialog token prefixes) — All node ids and textIds use the locked `companion_<archetype>_*` prefix per architecture brief §3.
- §4 (schema rules) — `List<T>` only (rule 1, no HashSet); `EnsureCompanionFieldsInitialized` reseats null fields (rule 3); `schemaVersion: 1` + `dialogueType: "companion"` discriminator (rules 4-5); flat underscore namespace for context fields (`companion_type`, `is_introduced`, etc., rule 6).
- §5 ("do not" list) — All six spawned heroes use `Occupation.Soldier` (rule 2, pitfall #11); no `Campaign.Current.X` dereferenced at registration (rule 3); no `HashSet<T>` (rule 4); no QualityStore writes (rule 5); no scripted-effect ids (rule 6); no `int.MinValue` sentinels (rule 7); no `EventDeliveryManager` direct calls (rule 8).
- §6 (modal pipeline) — Not applicable to Plan 2 (no modal storylets; only conversation surfacing). `ModalEventBuilder` will be exercised by Plans 3-7.

---

## §6 — Hand-off to downstream plans

Plans 3-7 inherit the following stable surface from Plan 2:

- **`CompanionLifecycleHandler.Instance.GetSpawnedCompanions()`** — returns `List<Hero>` of currently-spawned, alive companions. Plan 3 (Rank-Ceremony Arc) reads this for ceremony witness selection. Plan 5 (Endeavor System) reads this for endeavor companion-presence gates.
- **`EnlistmentBehavior.Instance.GetCompanionTypeId(Hero)`** — returns string typeId or null. Useful for type-discriminated logic in any downstream plan.
- **`EnlistmentBehavior.Instance.ClearCompanionSlot(Hero)`** — null the slot matching a hero. Plan 6 (Roll of Patrons) loaned-knight cleanup uses its own pathway, but the helper is available for any plan that needs to remove a Plan-2 companion outside death/discharge.
- **`CompanionDialogueCatalog.Instance.GetNode(nodeId, ctx)`** — specificity-ranked variant lookup. Plans 3-7 hooking conversation dialogs into the catalog use this directly.
- **Six archetype catalogs** with stable node ids (`companion_<id>_intro_greeting`, `_root`, `_topic_*`, `_goodbye`) — Plans 3-7 can author additional nodes against these catalogs by adding new `companion_*.json` files (the loader picks up all matching files).
- **Camp menu slot 6** wired to `OnTalkToCompanionSelected` — Plans 3-7 can layer additional pre-conversation logic here, but the surface itself is stable.

---

## §7 — Sign-off

Plan 2 is ✅ once a human operator runs the four smoke recipes in §3 and confirms:

- All six companions spawn at correct tier triggers
- Save-load round-trip preserves all 24 SyncKey fields including a load of a pre-Plan-2 save
- Vanilla role-assignment dialog fires for spawned companions
- Combat: companions fight when stay-back is OFF, don't when ON, at all tiers
- Trait-gated grievances fire from vanilla `CompanionGrievanceBehavior` for Sergeant / Field Medic / QM Officer
- `OnHeroKilled` clears slots correctly; per-Lord respawns on next enlistment, per-player permanent loss

Until then, status remains 🟡. Plans 3-7 may begin parallel implementation against the substrate — Plan 2's exposed surface is stable.

---

## §8 — References

- [Plan 2 — Companion Substrate](2026-04-24-ck3-wanderer-companion-substrate.md) — owning plan
- [Plan 1 — Architecture Foundation](2026-04-24-ck3-wanderer-architecture-foundation.md)
- [Plan 1 verification report](2026-04-24-ck3-wanderer-architecture-foundation-verification.md)
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)
- [Spec v6 §3.10](../specs/2026-04-24-ck3-wanderer-systems-analysis.md) — design source
- [Companion archetype catalog schema](../../Features/Companions/companion-archetype-catalog.md)
- [AGENTS.md](../../../AGENTS.md)
- [CLAUDE.md](../../../CLAUDE.md)
