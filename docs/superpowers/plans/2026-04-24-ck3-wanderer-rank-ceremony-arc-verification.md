# Plan 3 — CK3 Wanderer Rank-Ceremony Arc: Verification Report

**Status:** 🟡 Code-level verification complete; in-game manual smoke pending human operator (T18 + T19 + per-ceremony fire recipes).

**Plan:** [2026-04-24-ck3-wanderer-rank-ceremony-arc.md](2026-04-24-ck3-wanderer-rank-ceremony-arc.md)
**Brief:** [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md)
**Date:** 2026-04-26

---

## §1 — What shipped

### Storylets per retained tier transition

| Tier transition | newTier | File | Storylets | Options |
| :-: | :-: | :-- | :-: | :-: |
| T1→T2 | 2 | `ceremony_t1_to_t2.json` | 4 | 12 |
| T2→T3 | 3 | `ceremony_t2_to_t3.json` | 4 | 16 |
| T4→T5 | 5 | `ceremony_t4_to_t5.json` | 4 | 12 |
| **T6→T7 (THE COMMISSION)** | 7 | `ceremony_t6_to_t7.json` | 4 | 12 |
| T7→T8 | 8 | `ceremony_t7_to_t8.json` | 4 | 12 |
| | | **5 files** | **20** | **64** |

Each ceremony file ships base + Vlandian + Sturgian + Imperial cultural variants. Battania falls back to Vlandian; Khuzait → Sturgian; Aserai → Imperial; anything else → base.

### New files (10)

**Documentation (3)**
- `docs/Features/Ceremonies/ceremony-storylet-schema.md` — schema reference (T1)
- `docs/Features/Ceremonies/ceremony-flag-conventions.md` — flag naming (T2)
- `docs/superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc-verification.md` — this report

**C# code (3)**
- `src/Features/Ceremonies/CeremonyCultureSelector.cs` — culture → variant suffix mapping (T8)
- `src/Features/Ceremonies/CeremonyWitnessSelector.cs` — per-tier witness slot dictionary (T5)
- `src/Features/Ceremonies/CeremonyProvider.cs` — text-variable bag + ResolvedSlots seeding + ModalEventBuilder.FireCeremony delegate (T3)

**Content (5)**
- `ModuleData/Enlisted/Storylets/ceremony_t1_to_t2.json` (T9)
- `ModuleData/Enlisted/Storylets/ceremony_t2_to_t3.json` (T10)
- `ModuleData/Enlisted/Storylets/ceremony_t4_to_t5.json` (T12)
- `ModuleData/Enlisted/Storylets/ceremony_t6_to_t7.json` (T14)
- `ModuleData/Enlisted/Storylets/ceremony_t7_to_t8.json` (T15)

### Edits (5)

- `src/Features/Ceremonies/RankCeremonyBehavior.cs` — populated `OnTierChanged` body (was Plan 1 log-only stub) — calls `CeremonyProvider.FireCeremonyForTier(newTier)` after rank-up + range gates (T4)
- `Enlisted.csproj` — three new `<Compile Include>` entries for the new C# files. No new ItemGroup needed for ceremony JSON — the existing `<StoryletsData>` glob (line 714) deploys them automatically.
- `ModuleData/Enlisted/Effects/scripted_effects.json` — +44 ceremony scripted effects (10 trait drift + 34 witness reactions × 7 archetypes including `lord`)
- `Tools/Validation/validate_content.py` — Phase 20 (ceremony storylet completeness) added; called from `main()` after Phase 18 (T17)
- `ModuleData/Languages/enlisted_strings.xml` — 188 ceremony loc-keys integrated under "Plan 3 Rank Ceremonies" comment block (sync_event_strings.py output)
- `CLAUDE.md` — current-status block updated with Plan 3 entry

### Save-data layout

Plan 3 consumes **zero save-definer offsets**. Choice memory rides on `FlagStore`'s existing serialization via two bool key patterns:

| Key | Type | When set |
| :-- | :-- | :-- |
| `ceremony_fired_t{N}` | bool | By every option's effects list at choice resolution |
| `ceremony_choice_t{N}_<choice_id>` | bool | By the picked option's effects list |

Plan 1 originally reserved offset 59 for ceremony state; the brief footnote reads "Rank Ceremony state lives in `FlagStore` — no offset claimed." That decision held.

---

## §2 — Verification gates passed

- ✅ `dotnet build Enlisted.sln -c 'Enlisted RETAIL' -p:Platform=x64` — clean (0 warnings, 0 errors) after every phase commit
- ✅ `python Tools/Validation/validate_content.py` — passes with 0 errors. Phase 12 (scripted-effect refs) confirms all `ceremony_trait_drift_*` and `ceremony_witness_reaction_*` IDs resolve. Phase 20 (Plan 3) reports `OK: 5 required ceremony base storylet(s) authored across 5 file(s).` No new warnings/info introduced by ceremony content.
- ✅ `python Tools/Validation/sync_event_strings.py` — zero unresolved ceremony strings after the 188-line integration. (Pre-existing `floor_tension_*` warnings are out of scope.)
- ✅ `dotnet format Enlisted.sln whitespace --verify-no-changes --include 'src/Features/Ceremonies/'` — exit 0
- ✅ `dotnet format Enlisted.sln style --verify-no-changes --severity warn --include 'src/Features/Ceremonies/'` — exit 0 after IDE0005 fix on `CeremonyProvider.cs` (removed unused `TaleWorlds.Core` using)
- ✅ All new C# files CRLF-normalized via `Tools/normalize_crlf.ps1` and BOM-stripped (the script unconditionally prepends a BOM; `.editorconfig charset = utf-8` rejects BOMs — Plan 2 verification §2 documents this footgun)
- ✅ `RankCeremonyBehavior.OnTierChanged` populated (no longer log-only stub); guards `newTier > previousTier` and `newTier ∈ [2, 9]` before delegating
- ✅ Witness-archetype catalog matches `CeremonyWitnessSelector.IsWitnessAtTier` table for every authored ceremony — verified by Python script that parses each ceremony's `apply` IDs against the per-tier allowed-archetype set:
  - newTier=2: `{sergeant}` ✓
  - newTier=3: `{sergeant}` ✓
  - newTier=5: `{field_medic, lord, sergeant}` ✓
  - newTier=7: `{field_medic, lord, pathfinder, sergeant, veteran}` ✓
  - newTier=8: `{junior_officer, qm_officer, veteran}` ✓
- ✅ Every ceremony storylet declares `category: "ceremony"` (Phase 20 hard check)
- ✅ Every ceremony option sets both `ceremony_choice_t{N}_<choice_id>` and `ceremony_fired_t{N}` flags (verified per option in all 5 files)
- ✅ All setup text uses tokens `{PLAYER_NAME}`, `{PLAYER_RANK}`, `{LORD_NAME}` rather than literal "soldier" / "the lord" (per AGENTS.md pitfall #23 + architecture brief §3)

---

## §3 — Pending: in-game manual smoke

Build + validator gates can't cover runtime modal pipeline, save-load, MBTextManager interpolation, or witness-relation drift. A human operator must run the smoke recipes the plan §5 documents:

### T18 — End-to-end T1→T2 single-ceremony smoke

1. Build clean.
2. Launch game; enlist with a Vlandian lord (so culture variant selection produces `ceremony_t1_to_t2_vlandian`).
3. Confirm Sergeant spawned at T1 (Plan 2 lifecycle).
4. Use Debug Tools to grant T2 XP requirements (or force-tier directly via `EnlistmentBehavior.SetTier(2)`).
5. Player accepts the proving event → `SetTier(2)` fires → `OnTierChanged(1, 2)` → `RankCeremonyBehavior.OnTierChanged` → `CeremonyProvider.FireCeremonyForTier(2)` → modal pops up.
6. Confirm:
   - Modal title reads "First blood".
   - Setup text interpolates `{PLAYER_NAME}` to the player's hero name.
   - 3 options visible (own steel / sergeant / luck).
   - Tooltips render correctly.
7. Pick the "trust_sergeant" option. Confirm:
   - Modal closes.
   - `FlagStore.Instance.Has("ceremony_fired_t2")` returns true.
   - `FlagStore.Instance.Has("ceremony_choice_t2_trust_sergeant")` returns true.
   - `Hero.MainHero.GetTraitLevel(DefaultTraits.Honor)` increased by 1.
   - `Hero.MainHero.GetRelation(<sergeant>)` increased by 10 (strong_approve).
   - In-game notification "Sergeant approves of your decision" fires (vanilla).
8. Force-set tier to 2 again. Confirm ceremony does NOT re-fire (dedup gate engaged).

### T19 — T6→T7 commission three-path smoke (CRITICAL regression check)

Run all three promotion paths separately:

**Path A — Auto proving-event:**
1. Set tier 6, grant XP/days/battles/relation requirements for T7.
2. Wait for `PromotionBehavior.CheckForPromotion` → proving-event modal.
3. Player accepts → `SetTier(7)` fires.
4. Confirm commission ceremony modal fires immediately after.
5. Pick a commission option → `ceremony_fired_t7` set.

**Path B — Decline-then-dialog:**
1. Same setup. Player declines proving event.
2. Initiates Lord conversation, asks for promotion via dialog branch.
3. Lord grants → `SetTier(7)` → ceremony fires.
4. Confirm dedup flag prevents proving-event-side ceremony from re-firing.

**Path C — Direct dialog-request:**
1. Tier 6, grant requirements but don't trigger proving event.
2. Initiates Lord conversation, asks for promotion.
3. Lord grants → `SetTier(7)` → ceremony fires once.

All three paths must result in exactly ONE ceremony modal at T7. Code-level analysis (verified at `EnlistmentBehavior.SetTier:9882` — single `OnTierChanged?.Invoke` call per tier transition) plus dedup gate at `CeremonyProvider:42` makes double-fire impossible from the wiring; T19 verifies the empirical UX.

### Per-ceremony fire smoke

For each of T2, T3, T5, T8 ceremonies, run the §5 ceremony-fire recipe (force-tier, accept, verify modal+effects). Single end-to-end pass per ceremony is sufficient for ship.

### Cultural variant matrix

Verify the four culture branches (Vlandian, Sturgian, Imperial, base) each produce the expected variant by enlisting with each faction and force-tier-ing through one ceremony. The mapping at `CeremonyCultureSelector.SelectVariantSuffix`:
- `vlandia` / `battania` → vlandian
- `sturgia` / `khuzait` → sturgian
- `empire` / `aserai` → imperial
- anything else → base (acts as catch-all but unlikely to fire in vanilla play)

---

## §4 — Deviations from plan as written

The plan v2 prescribes 20 sequential tasks. Several were materially modified during execution; the deltas are recorded as **Locks 1-6** at the top of the plan document and summarized here.

| Plan task | Status | Note |
| :--- | :--- | :--- |
| T1 — schema doc | shipped | Wrote against the actual storylet schema (`Storylet.cs`) and confirmed witness slot resolution survives `BuildModal`. |
| T2 — flag conventions doc | shipped | **Lock 2:** plan referenced `FlagStore.GetString` which does not exist. Doc + flag schema specify one bool flag per option (`ceremony_choice_t{N}_<choice_id>`). |
| T3 — `CeremonyProvider` | shipped | Pre-populates `ResolvedSlots["witness_<archetype>"] = hero` so existing `relation_change` primitive resolves correctly at `DrainPendingEffects` time. Added explicit text-variable bag set (`PLAYER_NAME` / `PLAYER_RANK` / `LORD_NAME` / `PLAYER_TIER`) so a ceremony fired before any QM/companion conversation has run still interpolates correctly. |
| T4 — `RankCeremonyBehavior.OnTierChanged` | shipped | Static-event subscription pattern (`-=` then `+=`) per `PathCrossroadsBehavior` precedent for save/load duplicate-subscription guard. |
| T5 — `CeremonyWitnessSelector` | shipped | **Per-advisor:** Lord (T4→T5 + T6→T7) added OUTSIDE the `GetSpawnedCompanions()` loop — Lord is `EnlistmentBehavior.EnlistedLord`, not a Plan-2 companion. |
| T6 — trait drift | shipped | **Lock 3:** plan proposed a new `trait_drift` C# primitive. Existing `EffectExecutor.DoTraitXp` (line 240) already does ±1 step via `Math.Sign(amount)`. Ceremony scripted effects use existing `trait_xp` primitive directly. **Zero C# changes for trait drift.** |
| T7 — witness reactions | shipped | **Lock 4:** plan proposed a new `companion_relation` C# primitive. Existing `EffectExecutor.DoRelationChange` (line 496) already resolves `Hero` from `ctx.ResolvedSlots[target_slot]`. Ceremony scripted effects use existing `relation_change` primitive directly. **Zero C# changes for witness reactions.** |
| T8 — `CeremonyCultureSelector` | shipped | One static class, two methods. Mapping per plan §4.3. |
| T9 — T1→T2 ceremony | shipped | Authored manually as tone reference for parallel subagent dispatch. |
| T10 — T2→T3 | shipped | Parallel implementer subagent (subagent_type=general-purpose). |
| **T11 — T3→T4** | ❌ **DROPPED** | **Lock 1:** Option A. PathCrossroads at newTier=4 covers this tier-up moment. |
| T12 — T4→T5 | shipped | Parallel implementer subagent. Heavy moral beat (lord orders something targeting civilians). |
| **T13 — T5→T6** | ❌ **DROPPED** | **Lock 1:** Option A. PathCrossroads at newTier=6 covers this tier-up moment. |
| T14 — T6→T7 commission | shipped | Parallel implementer subagent. Marquee narrative beat preserved (newTier=7 has no PathCrossroads collision). Includes `grant_renown -10` on `try_to_refuse` per plan §4.4. |
| T15 — T7→T8 | shipped | Parallel implementer subagent. |
| **T16 — T8→T9** | ❌ **DROPPED** | **Lock 1:** Option A. PathCrossroads at newTier=9 covers the endgame moment. |
| T17 — Phase 20 validator | shipped | Fail-closed on missing required base storylets (per advisor); warn on missing optional cultural variants. Pattern after Phase 18. Scope explicitly narrow — no archetype/trait/flag-name validation (Phase 12 covers `apply` IDs; broader checks would tie Phase 20 to Plan 2 archetype catalog and rot when Plan 6 introduces new archetypes). |
| T18 — T1→T2 end-to-end smoke | code-level done | Trace verified: `SetTier(2)` → `OnTierChanged(1,2)` → `RankCeremonyBehavior.OnTierChanged` (lines 28-46) → `CeremonyProvider.FireCeremonyForTier(2)` (lines 32-79) → `CeremonyCultureSelector.ResolveStoryletId(2)` → `ModalEventBuilder.FireCeremony` → `StoryletCatalog.GetById` → `StoryletEventAdapter.BuildModal` → `StoryDirector.EmitCandidate` → `EventDeliveryManager.QueueEvent` → modal popup → player picks → `OnOptionSelected` → `StoryletEventAdapter.DrainPendingEffects` (line 513) → `EffectExecutor.Apply` → trait drift + `relation_change` (resolves `target_slot: witness_sergeant` from pre-populated `ResolvedSlots`) + `set_flag` calls. In-game smoke pending. |
| T19 — T6→T7 three-path smoke | code-level done | Single-Invoke guarantee at `EnlistmentBehavior.SetTier:9882` plus dedup gate at `CeremonyProvider:42` makes double-fire impossible from wiring. All three promotion paths converge on `SetTier(7)` regardless of dialog vs proving-event entry. In-game smoke pending. |
| T20 — verification report | this file | 🟡 mirroring Plan 2's verification format. Closes to ✅ when human operator signs off on T18 + T19 + per-ceremony smokes. |

---

## §5 — Architecture brief compliance

Cross-checked against [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md):

- **§1 (save-definer offsets)** — Plan 3 adds zero offsets. Brief reservation note ("Rank Ceremony state lives in `FlagStore` — no offset claimed") observed.
- **§2 (namespace conventions)** — `Enlisted.Features.Ceremonies` (Plan 3 owner). New types: `RankCeremonyBehavior` (Plan 1 stub populated), `CeremonyProvider`, `CeremonyWitnessSelector`, `CeremonyCultureSelector`. No collisions.
- **§3 (dialog token prefixes + interpolation contract)** — All ceremony storylets reference `{PLAYER_NAME}`, `{PLAYER_RANK}`, `{LORD_NAME}` and the tokens are populated by `CeremonyProvider.SetCommonTextVariables` before modal opens.
- **§4 (schema rules)** — Flat underscore namespace for all flag keys (rule 6); inline `{=key}Fallback` pattern for all loc-keys; `EnsureInitialized` not needed (no new save state).
- **§5 ("do not" list)** — No vanilla TaleWorlds type re-registered (rule 1); no `Occupation.Wanderer` heroes spawned (rule 2 — Plan 3 spawns no heroes); no `Campaign.Current.X` dereferenced at registration (rule 3 — `RankCeremonyBehavior` only registers the event subscription); no `HashSet<T>` (rule 4); no read-only QualityStore writes (rule 5); no ad-hoc scripted-effect ids (rule 6 — all 44 entries registered before storylet content references them); no `int.MinValue` sentinels (rule 7); no `EventDeliveryManager` direct calls (rule 8 — modal pipeline routes through `ModalEventBuilder.FireCeremony` → `StoryDirector.EmitCandidate`).
- **§6 (modal pipeline + OnTierChanged consumers)** — `RankCeremonyBehavior` is the third subscriber to `OnTierChanged` (after `PathScorer` and `PathCrossroadsBehavior`). **Lock 1 records the explicit collision-avoidance decision (Option A — skip ceremonies at newTier 4/6/9).**

---

## §6 — Hand-off to Plans 4-7

Plan 3 ships the following stable surface for downstream plans:

### For Plan 4 (Officer Trajectory)
- `FlagStore.Instance.Has("ceremony_choice_t7_humble_accept")` / `_proud_accept` / `_try_to_refuse` — read these to flavor officer-tier dialog. Lord could address player as "Steady captain" (humble) vs "Bold captain" (proud).
- T6→T7 ceremony narratively references the banner / cape / weapon-modifier change but applies no actual gear. Plan 4 can hook a separate `OnTierChanged(_, 7)` subscriber to apply gear, or add gear-apply effects to the commission options' effect lists.

### For Plan 5 (Endeavor System)
- Endeavor categories may gate or flavor on ceremony choice flags. E.g. `ceremony_choice_t3_frugal` unlocks a "Run a tight dice game" endeavor seed.
- Witness-relation drift via ceremony reactions affects `Hero.GetRelation` directly; downstream endeavor success-rolls that read companion relations inherit ceremony drift naturally.

### For Plan 6 (Roll of Patrons)
- Patron favor outcomes may flavor on ceremony choice flags ("you've always been calculating, so I'm not surprised you're asking for a loan").
- Ceremony witness drift (Plan 3 layer) compounds with patron-relation snapshots (Plan 6 layer); patron roll algorithm reads vanilla relation field, which is already drifted.

### For Plan 7 (Personal Kit + Lifestyle + Smoke)
- Lifestyle unlock pre-conditions may include ceremony choice flags (e.g. Forager lifestyle gates on `ceremony_choice_t3_frugal`).
- Phase 20 validator already in place; Plan 7 polish pass can extend to require all 3 cultural variants per ceremony (currently warning-only).

### Stable API surface

| Symbol | Use case |
| :-- | :-- |
| `CeremonyProvider.FireCeremonyForTier(int newTier)` | Public; downstream plans can fire a ceremony manually if needed (e.g. force replay for testing). Dedup gate ensures idempotency. |
| `CeremonyWitnessSelector.GetWitnessesForCeremony(int newTier)` | Returns `Dictionary<string, Hero>` keyed by `witness_<archetype>` slot name. |
| `CeremonyCultureSelector.SelectVariantSuffix()` | Returns `vlandian` / `sturgian` / `imperial` / `base`. |
| `CeremonyCultureSelector.ResolveStoryletId(int newTier)` | Returns full storylet ID for the player's culture and the given tier transition. |

---

## §7 — Sign-off

Plan 3 is ✅ once a human operator runs:

- [ ] T18 T1→T2 end-to-end smoke (single ceremony, base path)
- [ ] T19 T6→T7 commission three-path smoke (auto / decline / dialog — single ceremony per save)
- [ ] T2→T3, T4→T5, T7→T8 per-ceremony smokes (one fire each)
- [ ] Cultural variant matrix smoke (one ceremony per Vlandian / Sturgian / Imperial / base culture)
- [ ] Save-load round-trip preserves ceremony flags

Until then, status remains 🟡. Plans 4-7 may begin parallel implementation against the substrate — Plan 3's exposed surface is stable.

---

## §8 — References

- [Plan 3 — Rank-Ceremony Arc](2026-04-24-ck3-wanderer-rank-ceremony-arc.md) — owning plan (locks 1-6 at top)
- [Plan 1 verification](2026-04-24-ck3-wanderer-architecture-foundation-verification.md)
- [Plan 2 verification](2026-04-24-ck3-wanderer-companion-substrate-verification.md)
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)
- [Spec v6 §3.9 + §6.8 + §9](../specs/2026-04-24-ck3-wanderer-systems-analysis.md) — design source
- [Ceremony storylet schema](../../Features/Ceremonies/ceremony-storylet-schema.md)
- [Ceremony flag conventions](../../Features/Ceremonies/ceremony-flag-conventions.md)
- [AGENTS.md](../../../AGENTS.md)
- [CLAUDE.md](../../../CLAUDE.md) (Plan 3 status entry under Current project status)
