# Plan 6 — CK3 Wanderer Roll of Patrons: Verification Report (PRE-EXECUTION SCAFFOLDING — 2026-04-27)

**Status:** 🟡 **Pre-execution scaffolding only. 0 of 18 tasks complete. Worktree + branch + locks in place; ready for T1 execution by the next session.** This document is the canonical hand-off record for the next operator picking up Plan 6.

**Plan:** [2026-04-24-ck3-wanderer-roll-of-patrons.md](2026-04-24-ck3-wanderer-roll-of-patrons.md)
**Brief:** [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md)
**Branch:** `feature/plan6-roll-of-patrons` (worktree at `.worktrees/plan6-roll-of-patrons/`)
**Date:** 2026-04-27

**Sibling worktrees on the repo:**
- `feature/plan4-officer-trajectory` (Plan 4 in-progress, separate session — NOT yet on `development`)
- `feature/plan5-endeavor-system` (Plan 5 code + content shipped 2026-04-26, in-game smoke pending — NOT yet on `development`)

---

## §1 — What's been scaffolded (no code shipped yet)

This is a pre-execution document. The only artifacts so far are:

1. **Worktree + branch** — `.worktrees/plan6-roll-of-patrons/` on branch `feature/plan6-roll-of-patrons`, branched from `development` at `3953f9b`, pushed to `origin`. `packages/` directory copied from main repo so `dotnet build` works out-of-box.

2. **Locks block in plan file** — three pre-execution locks added to the Plan 6 plan file. Read those first; they override the body where they conflict:
   - **Lock 1** — Plan 4 + Plan 5 dependency strategy: DECOUPLE. Plan 6 self-contains both hooks (`PATRON_NAME` token wiring + `AnotherContract` favor graceful degradation when `EndeavorRunner` isn't loaded). No rebase on Plan 4 or Plan 5 branches needed.
   - **Lock 2** — Plan-vs-codebase drift checklist: 6 items the implementer verifies against `../Decompile/` before writing code (`[SaveableField]` → auto-properties, `Enum.GetValues<T>()` → non-generic + Cast, `lord.GetRelationWithPlayer()`, `Hero.OneToOneConversationHero`, `EnlistmentBehavior.Instance.DaysServed`, `MBObjectManager.Instance.GetObject<Hero>(MBGUID)`).
   - **Lock 3** — Branch logistics: worktree configured + pushed; no save-definer offsets needed (Plan 1 already claimed 54/55/84).

3. **EXECUTION PROGRESS block at top of plan file** — points the next session here.

4. **18-task tracking** — Plan 6's T1-T18 seeded in the task system (TaskList view).

**No C# files modified, no content authored, no validator changes. Build + validator at `development` baseline (3953f9b).**

---

## §2 — Verification gates (current state)

- ✅ Worktree clean; branch tracks `origin/feature/plan6-roll-of-patrons`.
- ✅ `packages/` directory in place; `dotnet build` will succeed at the development baseline.
- ⏳ No code yet — gates re-run as Plan 6 phases ship.

---

## §3 — Pending work — full 18-task breakdown

Plan 6 §6 details each task; this is the bird's-eye list with implementer-relevant footguns + the locks they touch.

### Phase A — substrate population (T1-T6)

- **T1 — `FavorKind` enum populated + favor-catalog doc.** Replace Plan 1 stub `None = 0` with 6 enum values. New `docs/Features/Patrons/patron-favor-catalog.md`. *No locks affect this.*
- **T2 — `PatronEntry` helper methods.** Add `GetCooldown` / `SetCooldown` / `IsFavorAvailable` (CSV cooldown encoding per Lock §4.5 in plan body). *Lock 2 fix #1: existing `PatronEntry.cs` uses auto-properties; continue the convention. Don't introduce `[SaveableField]`.*
- **T3 — `PatronRoll.OnDischarge` handler.** Create entry on mid-career discharge. *Lock 2 fix #5: verify `EnlistmentBehavior.Instance.DaysServed` exists; if missing, derive `(CampaignTime.Now - EnlistmentDate).ToDays` or add the property.*
- **T4 — `PatronRoll.OnHeroKilled` handler.** Subscribe to `CampaignEvents.HeroKilledEvent`; mark `IsDead`. *Verify event signature against decompile (`KillCharacterAction.KillCharacterActionDetail` is the detail enum type; verify exact spelling).*
- **T5 — `PatronRoll.AvailableNearby` query.** Returns nearby patron entries. *Lock 2 fix #6: `MBObjectManager.Instance?.GetObject(guid) as Hero` works — verified by Plan 5.*
- **T6 — `PatronFavorResolver`.** New class with `TryGrantFavor(entry, kind, out reason)`. *Lock 1 second clause: AnotherContract favor checks `EndeavorRunner.Instance != null` before granting; hidden if Plan 5 isn't loaded. Lock 2 fix #3: use the actual `Hero.GetRelation(Hero)` API or the mod-side `GetRelationWithPlayer` extension if it exists — grep first.*

### Phase B — wiring + dialog integration (T7-T9, T15)

- **T7 — Hook discharge in `EnlistmentBehavior`.** Inject `PatronRoll.Instance?.OnDischarge(_enlistedLord, reason)` at all mid-career discharge code paths. *Risk M1 in plan body: audit ALL discharge paths. Search for `IsEnlisted = false` assignments to find them.*
- **T8 — Hook full retirement teardown.** Call `PatronRoll.Instance?.Clear()` in retirement code path. *Locked by spec §0 — Roll evaporates on retirement, no CK3-style persistence.*
- **T9 — `PatronAudienceExtension` helper.** Static helper used by dialog conditions. *Lock 2 fix #4: verify `Hero.OneToOneConversationHero` static property exists and namespace.*
- **T15 — Patron dialog branches in `EnlistedDialogManager`.** ~10 dialog lines at `lord_pretalk` priority 112. *Lock 1 first clause: also wire `MBTextManager.SetTextVariable("PATRON_NAME", ...)` in this method. Lock 2 fix #2: use `Enum.GetValues(typeof(FavorKind)).Cast<FavorKind>()` not the C# 8 generic overload.*

### Phase C — favor outcome storylets (T10-T14)

- **T10 — `patron_letter_of_introduction.json`.** 2-3 options.
- **T11 — `patron_gold_loan.json`.** 2-3 options. *AGENTS.md Critical Rule #3: use `give_gold` primitive (routes through `GiveGoldAction` for UI visibility).*
- **T12 — `patron_troop_loan.json`.** Adds 1-2 high-tier knights to MainParty for 7 days. *Verify `AddCompanionAction` + `RemoveCompanionAction.ApplyByFire` against decompile.*
- **T13 — `patron_audience.json`.** Patron arranges meeting with a third lord.
- **T14 — `patron_marriage.json` + `patron_another_contract.json`.** Marriage candidate intro + ContractActivity spawn. *Lock 1 second clause: AnotherContract storylet's effect that spawns the contract is wrapped in a runtime check; if `EndeavorRunner.Instance == null`, the option is hidden by T6.*

### Phase D — verification + smoke (T16-T18)

- **T16 — Save-load round-trip end-to-end.** Cover empty roll, 3-entry mix, mid-favor-grant, pre-Plan-6 save with `EnsureInitialized` reseating Entries.
- **T17 — Cross-system flag integration.** Verify `ceremony_choice_t<N>_<id>` + lifestyle unlock flags are queryable from patron favor outcome storylets.
- **T18 — Plan 6 verification report.** Update this doc from PRE-EXECUTION → 🟡 (code shipped, in-game smoke pending) → ✅ after smoke. Author the in-game smoke runbook in §4 below.

---

## §4 — Resume runbook (for the next session)

### Picking up the work

1. **Find the worktree.** `git worktree list` in the main repo shows `.worktrees/plan6-roll-of-patrons` on branch `feature/plan6-roll-of-patrons`. `cd` into that directory before doing anything.

2. **Verify the build.** `dotnet build -c 'Enlisted RETAIL' -p:Platform=x64` from the worktree should succeed in ~2s (development baseline). If the build fails on missing `Microsoft.NETFramework.ReferenceAssemblies.net472.targets`, copy packages: `cp -r ../../packages packages`.

3. **Verify the validator state.** `python Tools/Validation/validate_content.py` exits 0 with development baseline warnings only. Any new endeavor / patron warning means a regression.

4. **Read the plan in this order:**
   - **Locks 1-3** at the top — these override the body where they conflict.
   - The plan body §0 + §1 + §4 (locked design decisions) + §6 (task list).
   - This verification doc's §3 for per-task footgun pointers.

### Picking up at T1 (start of Phase A)

T1 is small (enum population + new favor-catalog doc). Edit `src/Features/Patrons/FavorKind.cs` to replace `None = 0` with the 6 favor kinds per plan §6 T1. Author `docs/Features/Patrons/patron-favor-catalog.md` documenting each favor's cooldown / conditions / outcome shape (plan §4.2 has the table; expand into prose).

After T1: `dotnet build` should still pass cleanly. Commit T1 alone or batch with T2 (small substrate addition).

### Execution-shape recommendations

Plan 6 is **18 tasks, ~3-4 days estimated**. Smaller than Plan 5's 30. Shape suggestions:
- **No subagent dispatch needed** — Plan 5's parallelism made sense for 90+ storylets across 5 categories. Plan 6 has only 6 favor outcome storylets total; they can be authored in main thread.
- **Sequential execution works** — Phase A (T1-T6) → Phase B (T7-T9, T15) → Phase C (T10-T14) → Phase D (T16-T18). Each phase is reviewable as a separate commit.
- **Storylet exemplar already exists** — Plan 3's ceremony storylets (`ceremony_t1_to_t2.json` etc.) and Plan 5's endeavor storylets (`endeavor_*.json`) are both on sibling branches OR development. Either can serve as tone reference for Plan 6's patron favor storylets.

### Known gaps + watch-outs

- **`PATRON_NAME` token is a Plan 6 responsibility now (Lock 1).** Plan 4 was supposed to wire it; Plan 6 inlines the wiring in T15's `AddPatronDialogs` method. Cleanup item if Plan 4 ever merges and adds the centralized version.
- **`AnotherContract` favor degrades gracefully when Plan 5 isn't loaded (Lock 1).** T6's `PatronFavorResolver` checks `EndeavorRunner.Instance != null`. T15's dialog branch reads the same check via `PatronFavorResolver.TryGrantFavor` returning false. Test paths with + without Plan 5 to confirm.
- **Plan-vs-codebase drift discipline (Lock 2).** Plan 1's verification §5 caught 7 divergences. Plan 5's verification §5 caught 17. Stay vigilant per AGENTS.md pitfall #22 — verify prescribed APIs against `../Decompile/` + actual source as you go; append findings to this doc's §5 below as you discover them.
- **Discharge code paths audit (T7).** Plan body Risk M1 flags this — multiple paths in EnlistmentBehavior may flip `IsEnlisted = false`. Find them all before T7 ships, or T3 OnDischarge fires inconsistently. Smoke-test each path separately.
- **Mid-favor-grant save-load (T16).** If a patron-favor modal is mid-air when the player saves, the cooldown was already set but the storylet effects haven't applied. Reload should either show the modal again (cleanest) or skip with effects auto-applied (acceptable). Verify this corner.

### In-game smoke runbook seed (T18 finalizes this)

A draft of the smoke scenarios. The human operator runs these in-game once Plan 6 ships:

1. **Discharge → patron entry.** Enlist with Lord A; serve 30 days; discharge (any reason). Verify `PatronRoll.Entries.Count == 1`, entry has correct HeroId + DaysServed=30 + RelationSnapshot.
2. **Re-enlist + re-discharge same lord.** Enlist with Lord A again; serve 20 more days; discharge. Verify `PatronRoll.Entries.Count == 1` (not duplicated), entry's DaysServed=50 (cumulative), cooldowns preserved.
3. **Patron death.** Use Debug Tools to kill Lord A. Verify entry's `IsDead = true`.
4. **Patron audience.** Enlist with Lord B; position MainParty near Lord A; open Camp → Talk to my Lord → select Lord A. Verify "Call in a favor" branch appears. Select; verify 6 favor options surface (or greyed with reason).
5. **Gold loan smoke.** Pick GoldLoan favor with eligible patron (relation ≥ 10, hero gold ≥ 5000). Verify storylet fires, options apply gold + relation drift, debt flag set. Re-open audience; verify GoldLoan greyed (60-day cooldown).
6. **AnotherContract smoke (Plan 5 + Plan 6 integration test).** Pick AnotherContract favor on a patron when Plan 5's runtime is loaded (post-merge). Verify ContractActivity spawns with patron-flavored origin. Repeat without Plan 5 loaded; verify the favor option is hidden.
7. **Full retirement.** Trigger full retirement. Verify `PatronRoll.Entries` is cleared; mod surface goes silent.
8. **Save-load round-trip.** Save mid-PatronRoll (3 entries, 1 cooldown active); reload; verify everything intact. Save predating Plan 6 (e.g. development save); reload; verify EnsureInitialized reseats Entries to empty list.

---

## §5 — Plan-vs-codebase divergences (filled in during execution)

*(Empty at scaffolding. Implementer appends findings here as Lock 2's checklist items get verified against the codebase.)*

---

## §6 — Hand-off surface (Plan 7 may use)

After Plan 6 ships, Plan 7 (final polish + smoke) inherits these public APIs:

- `Enlisted.Features.Patrons.PatronRoll.Instance.OnDischarge(lord, reason)` / `OnHeroKilled` / `Clear()` / `AvailableNearby(maxDistance)` / `Has(MBGUID)` / `GetEntry(MBGUID)`
- `Enlisted.Features.Patrons.PatronEntry` POCO with helpers `GetCooldown(FavorKind)` / `SetCooldown(FavorKind, CampaignTime)` / `IsFavorAvailable(FavorKind, CampaignTime)`
- `Enlisted.Features.Patrons.PatronFavorResolver.TryGrantFavor(entry, kind, out refusalReason)` — central can-grant check + grant + storylet fire
- `Enlisted.Features.Patrons.PatronAudienceExtension.IsConversationTargetPatron()` / `GetEntryForTarget()`
- 6 favor outcome storylets at `ModuleData/Enlisted/Storylets/patron_*.json` — authors of new favor variants can copy the shape

Plan 7 polish items pre-deferred from Plan 6:
- Per-patron cooldown if playtest shows favor abuse
- News-feed integration of patron deaths (when news-v2 substrate ships)
- Cultural variants per favor outcome
- Deletion of Plan 6's inline `PATRON_NAME` token wiring once Plan 4 merges with the centralized version

---

## §7 — References

- [Plan 6 — CK3 Wanderer Roll of Patrons](2026-04-24-ck3-wanderer-roll-of-patrons.md) — owning plan with locks 1-3 at the top.
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md) — locked decisions Plan 6 inherits.
- [Plan 1 verification](2026-04-24-ck3-wanderer-architecture-foundation-verification.md) — the substrate Plan 6 populates (PatronRoll/PatronEntry shells + FavorKind enum stub at offsets 54/55/84).
- [Plan 5 verification](2026-04-24-ck3-wanderer-endeavor-system-verification.md) — the integration target for the AnotherContract favor (Lock 1 second clause).
- [Storylet backbone reference](../../Features/Content/storylet-backbone.md) — base schema for favor outcome storylets.
- [AGENTS.md](../../../AGENTS.md) — Critical Rule #3 (gold transactions), Pitfall #14 (HashSet not saveable), Pitfall #22 (plan-vs-codebase drift discipline).
- [CLAUDE.md](../../../CLAUDE.md) — project conventions (csproj wildcards, CRLF, error-codes regen, deserialization-skips-ctor → EnsureInitialized pattern).
