# Plan 6 — CK3 Wanderer Mechanics: Roll of Patrons

**Status:** Draft v1 (2026-04-24). Sixth of seven plans implementing the [CK3 Wanderer Mechanics Systems Analysis (v6)](../specs/2026-04-24-ck3-wanderer-systems-analysis.md). See spec §8 for the full plan structure.

**Scope:** Long-tail relationship list of every Lord the player has served. Patrons accumulate during enlistment career; player can call in favors (gold loan, troop loan, letter of introduction, audience arrangement, marriage facilitation, another contract) when meeting former patrons. **Per §0 scoping, the Roll evaporates on full retirement** — explicitly enlisted-only persistence, NOT CK3-style post-retirement.

**Estimated tasks:** 18. **Estimated effort:** 3-4 days with AI-driven implementation.

**Dependencies:** Plans 1-3 are required and on `development`. Plans 4 + 5 are on feature branches and NOT yet on `development` — see Lock 1 below for the decoupling strategy that lets Plan 6 ship without rebasing on either.

---

## 📍 EXECUTION PROGRESS — 2026-04-27 (PRE-EXECUTION SCAFFOLDING — hand-off)

**Status:** 🟡 **0 of 18 tasks complete. Worktree + branch + task tracking + locks in place; ready for T1 execution by the next session.**

**Branch:** `feature/plan6-roll-of-patrons` (worktree at `.worktrees/plan6-roll-of-patrons/`, branched from `development` at `3953f9b`, pushed to `origin`).
**Sibling worktrees on the repo:** `feature/plan4-officer-trajectory` (Plan 4 in-progress, separate session) and `feature/plan5-endeavor-system` (Plan 5 code + content shipped 2026-04-26, in-game smoke pending).

**Verification gates as of scaffolding (2026-04-27):**
- ✅ Worktree clean; branch tracks origin.
- ⏳ No code yet — build + validator gates pass at the `development` baseline (3953f9b).

**Resume path for the next session:**
1. `cd .worktrees/plan6-roll-of-patrons`.
2. Read **Locks 1-3 below** — they override the plan body where they conflict and capture three load-bearing decisions made during scaffolding (Plan 4/5 dependency decoupling, plan-vs-codebase drift fixes, branch logistics).
3. Read the verification doc at `docs/superpowers/plans/2026-04-24-ck3-wanderer-roll-of-patrons-verification.md` — has the canonical T1-T18 task breakdown + per-task footguns + the in-game smoke runbook seed.
4. Start at T1 (FavorKind enum population + favor-catalog doc). Plan 6's tasks are sequential and mostly small; can execute serially in main thread without subagent dispatch (unlike Plan 5's 30-task scope).

---

## 🔒 LOCKED 2026-04-27 — readiness amendments (pre-execution)

This block consolidates the pre-execution readiness audit. Locks override the plan body where they conflict. Same pattern Plans 3 + 5 used.

### Lock 1 — Plan 4 + Plan 5 dependency strategy: DECOUPLE (BLOCKING for §0 dependency line + T14 + T15)

Plan 6 §0 reads "Plans 1-5 must be complete." Reality (as of 2026-04-27): Plans 1-3 on `development`; Plans 4 + 5 are on sibling feature branches not yet merged. Two specific Plan 4/5 hooks the body relies on:

- **Plan 4 — `PATRON_NAME` text variable.** §0 ref 19 reads "Plan 4 verification — `PATRON_NAME` text variable already extended into `SetCommonDialogueVariables` at T16." Plan 4 hasn't shipped this yet on its branch (Plan 4 is mid-execution as of 2026-04-27). Decoupling fix: **Plan 6 wires `PATRON_NAME` itself** in T15 — add the `MBTextManager.SetTextVariable("PATRON_NAME", patronEntry.HeroName ?? Hero name lookup via MBObjectManager)` call inside `EnlistedDialogManager.AddPatronDialogs` *just before* opening the patron favor sub-tree. ~3 lines. When Plan 4 eventually merges, the Plan 6 wiring can be deleted in favor of Plan 4's centralized SetCommonDialogueVariables call (one-line cleanup).

- **Plan 5 — `ContractActivity` for `AnotherContract` favor.** §6 T14 references `ContractActivity` for the `patron_another_contract.json` storylet outcome. Plan 5 ships `ContractActivity` (offset 56) — Plan 1 actually claimed the offset, so the type EXISTS on `development`, but the runtime infrastructure (EndeavorRunner / EndeavorPhaseProvider) only exists on Plan 5's branch. Decoupling fix: **`AnotherContract` favor degrades gracefully when Plan 5's runtime isn't loaded.** T6's `PatronFavorResolver.TryGrantFavor` checks `Type.GetType("Enlisted.Features.Endeavors.EndeavorRunner") != null` (or equivalent reflection-free check via `EndeavorRunner.Instance != null`); if absent, the favor option is hidden from the dialog branch (T15) with no user-visible "this favor is unavailable" message. When Plan 5 merges to development, the AnotherContract path becomes live without further Plan 6 changes.

**Net:** Plan 6 ships entirely on `feature/plan6-roll-of-patrons` branched from `development`. No rebasing on Plan 4 or Plan 5 branches. The two hooks above are the only Plan 4/5 dependencies; both are decoupled at the implementation level.

### Lock 2 — Plan-vs-codebase drift fixes (BLOCKING — implementer verifies inline per AGENTS.md pitfall #22)

The plan body prescribes several APIs / patterns that need verification against the actual codebase + decompile before T1 implementer work begins. This is the same pattern Plans 1 + 5's verification §5 captured in retrospect; recording up front for Plan 6 saves the catch-and-correct cycle:

1. **`[SaveableField]` attribute (§4.3 PatronEntry struct example).** Mod convention: `[Serializable]` class with public auto-properties. `PatronEntry.cs` already exists from Plan 1 — verify the existing shape and continue the convention (auto-properties, NOT `[SaveableField]`). Plan 1's verification §5 fix #4 codified this.

2. **`Enum.GetValues<FavorKind>()` (§6 T15 example).** C# 7.0 (mod's target) does not support the generic overload. Use `Enum.GetValues(typeof(FavorKind)).Cast<FavorKind>()` instead.

3. **`lord.GetRelationWithPlayer()` (§4.2 conditions + §6 T6 resolver).** Verify against decompile: `Hero.GetRelation(Hero other)` is the actual API; some shipped mod code uses a `GetRelationWithPlayer()` extension/helper. Grep `src/` for `GetRelationWithPlayer` to confirm whether a mod-side helper exists, and verify against `Decompile/TaleWorlds.CampaignSystem/Hero.cs` for the vanilla method shape.

4. **`Hero.OneToOneConversationHero` (§6 T9 PatronAudienceExtension).** Verify against decompile: this is the standard 1:1 conversation accessor in the campaign system, but confirm the static property name + namespace (likely `Hero.OneToOneConversationHero` static, but verify).

5. **`EnlistmentBehavior.Instance.DaysServed` (§6 T3 discharge handler).** Verify this property exists. Grep the existing `EnlistmentBehavior.cs` for `DaysServed` — the mod has `DaysInRank` and `DaysEnlisted`; `DaysServed` may need to be derived (`CampaignTime.Now - EnlistmentDate`) or a new property added. If the property doesn't exist, T3 implementer adds it (small addition; trivial save backfill needed).

6. **`MBObjectManager.Instance.GetObject<Hero>(MBGUID)` (§6 T5 + T6).** This DOES work — verified during Plan 5 Phase A. But note Plan 5's T3 used the non-generic `MBObjectManager.Instance?.GetObject(guid) as Hero` form for null-safety; either is fine.

### Lock 3 — Branch logistics (NOT BLOCKING — already configured)

Worktree at `.worktrees/plan6-roll-of-patrons/` branched from `development` at commit `3953f9b`. Branch `feature/plan6-roll-of-patrons` is pushed to origin with upstream tracking. `packages/` directory copied from main repo (per Plan 5 worktree setup pattern; if missing, `cp -r ../../packages packages` from the worktree).

No save-definer offsets needed — `PatronRoll` (54), `PatronEntry` (55), `FavorKind` enum (84) are all claimed by Plan 1 (commit `aa3ef16`, on `development`). Plan 6 only populates the existing shells.

---

## §0 — Read these first

### Required prior plan documentation
1. **[Plan 1](2026-04-24-ck3-wanderer-architecture-foundation.md)** + verification — PatronRoll + PatronEntry empty shells exist at offsets 54-55; FavorKind enum stub at offset 84.
2. **[Plan 2](2026-04-24-ck3-wanderer-companion-substrate.md)** + verification — Audience flow precedent.
3. **[Plan 3](2026-04-24-ck3-wanderer-rank-ceremony-arc.md)** + verification — `FlagStore` + ChangeRelationAction patterns.
4. **[Plan 4](2026-04-24-ck3-wanderer-officer-trajectory.md)** + verification — `PATRON_NAME` text variable already extended into `SetCommonDialogueVariables` at T16.
5. **[Plan 5](2026-04-24-ck3-wanderer-endeavor-system.md)** + verification — `ContractActivity` exists; Plan 6 leverages for "another contract" favor.
6. **[Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md)**.

### Required spec reading
7. **[Spec v6 §0 Scoping](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — **CRITICAL.** Patron lifecycle is enlisted-only; Roll evaporates on retirement.
8. **[Spec v6 §3.3 Roll of Patrons](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — design source. PatronRoll structure, lifecycle, favor catalog.
9. **[Spec v6 §6.6 Vanilla preferences](../specs/2026-04-24-ck3-wanderer-systems-analysis.md)** — relation drift via `ChangeRelationAction.ApplyPlayerRelation`.

### Required project guidance
10. **[AGENTS.md](../../../AGENTS.md)** — Critical Rule #3 (Gold Transactions — use `GiveGoldAction` for visibility), Pitfall #14 (saveable container constraints — `List<PatronEntry>` not `HashSet`).
11. **[CLAUDE.md](../../../CLAUDE.md)** — Pitfall #4 (deserialization skips ctor; EnsureInitialized required), Pitfall #14 (HashSet not saveable).

### Required existing-code orientation
12. **`src/Features/Patrons/PatronRoll.cs`** — empty shell from Plan 1 T9. Plan 6 populates.
13. **`src/Features/Patrons/PatronEntry.cs`** — empty shell from Plan 1 T9. Plan 6 populates lifecycle methods.
14. **`src/Features/Patrons/FavorKind.cs`** — stub enum from Plan 1 T4 (`None = 0` only). Plan 6 populates 6 kinds.
15. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3640`** + discharge handlers — Plan 6 hooks here to create patron entries on discharge.
16. **`src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs`** — find the retirement/full-discharge path (where IsEnlisted flips false AND IsRetiring is true). Plan 6 hooks here for `PatronRoll.Clear()`.
17. **`src/Features/Interface/Behaviors/EnlistedMenuBehavior.cs:3946-4020`** — `OnTalkToSelected` audience flow. Plan 6 doesn't modify the menu; instead adds dialog branches at `lord_pretalk` priority 112 that activate when target is on PatronRoll.
18. **`src/Features/Conversations/Behaviors/EnlistedDialogManager.cs`** — Plan 6 adds new `AddPatronDialogs` method registering favor branches.

### Required decompile orientation
19. **`Decompile/TaleWorlds.CampaignSystem/Hero.cs:2025` `GetRelation`** — read patron's current relation with player.
20. **`Decompile/TaleWorlds.CampaignSystem.Actions/ChangeRelationAction.cs`** — apply favor-cost relation deltas.
21. **`Decompile/TaleWorlds.CampaignSystem.Actions/GiveGoldAction.cs`** — for gold loan favor (per AGENTS.md Rule #3).
22. **`Decompile/TaleWorlds.CampaignSystem/CampaignEventDispatcher.cs:612` `OnHeroKilled`** — event Plan 6 subscribes to for patron-death lifecycle.
23. **`Decompile/TaleWorlds.CampaignSystem.Actions/AddCompanionAction.cs`** + **`RemoveCompanionAction.cs`** — for troop loan favor (temporarily add knight to player party).
24. **`Decompile/TaleWorlds.CampaignSystem/MobileParty.cs:1027` `MemberRoster.AddToCounts`** — for troop loan (add temporary knight).

---

## §1 — What this plan delivers

After Plan 6 ships:

- **`PatronRoll` Campaign behavior live** — accumulates entries every time the player discharges from a Lord (mid-career discharge, not full retirement). Each entry stores: hero MBGUID, days served, max rank reached, discharge date, last-favor-requested, per-favor cooldowns (CSV-encoded), relation snapshot at discharge, faction at discharge, alive-status flag.
- **6 favor kinds populated** in `FavorKind` enum: `LetterOfIntroduction`, `GoldLoan`, `TroopLoan`, `AudienceArrangement`, `MarriageFacilitation`, `AnotherContract`. Each has its own cooldown + condition + outcome.
- **Audience flow extended** — when player initiates "Talk to..." with a nearby Lord (existing `OnTalkToSelected`), a new dialog branch at `lord_pretalk` priority 112 fires IF target is on PatronRoll: *"My lord, I have served you faithfully in the past..."* opens favor-request sub-tree.
- **6 favor outcome storylets** authored — each favor kind has a 2-3 option storylet for accept/refuse/conditional with effects.
- **Lifecycle teardown on retirement** — when full retirement fires (player chooses to leave soldiering entirely), `PatronRoll.Clear()` empties the list; mod surface silences.
- **`OnHeroKilled` handler** — when a patron dies (via vanilla campaign event), corresponding entry's `IsDead` flag flips. Dead patrons stay on the Roll (greyed in dialog) but cannot grant favors.
- **`PATRON_NAME` text variable** in dialog (Plan 4 T16 already populated this from PatronRoll).

**Player-visible delta:** Each Lord you've previously served becomes a callable contact. Talking to a former patron in another army (or visiting their court) opens a "call in a favor" sub-tree. Builds long-tail relationship economy across the career.

---

## §2 — Subsystems explored

| Audit | Finding | Spec |
| :-- | :-- | :-- |
| Hero relations | `Hero.GetRelation` + `ChangeRelationAction.ApplyPlayerRelation` for cost-of-favor relation drift | §6.5 |
| Hero death lifecycle | `KillCharacterAction.OnHeroKilled` event subscribers; Plan 6 marks `IsDead` flag, doesn't remove entry | §6.5 |
| BarterManager | Existing negotiation system; v1 skips it (per spec §3.3 "skip BarterManager for v1") in favor of simpler Inquiry flow | §6.5 |
| Audience flow | `OnTalkToSelected` finds nearby lords; mod adds player line at `lord_pretalk` with patron-check condition | §3.3 |
| Saveable containers | `List<PatronEntry>` registered in Plan 1 T3; CSV-encoded cooldowns avoid Dictionary<FavorKind, CampaignTime> save complexity | §4.1 ledger |

---

## §3 — Subsystems Plan 6 touches

### Files modified

| File | Change | Tasks |
| :-- | :-- | :-- |
| `src/Features/Patrons/PatronRoll.cs` (Plan 1 stub) | Populate full Campaign behavior — discharge hook, OnHeroKilled, Clear, AvailableNearby, TryGrantFavor | T2-T6 |
| `src/Features/Patrons/PatronEntry.cs` (Plan 1 stub) | Add helper methods (CsvCooldownGetter, CsvCooldownSetter, IsAvailable) | T2 |
| `src/Features/Patrons/FavorKind.cs` (Plan 1 stub) | Populate 6 enum members beyond `None` | T1 |
| `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3640+` | Hook discharge to call `PatronRoll.OnDischarge(lord, reason)` | T7 |
| `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` (retirement path) | Hook full-retirement to call `PatronRoll.Clear()` | T8 |
| `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs` | Add `AddPatronDialogs` method registering ~10 favor-branch dialog lines | T9, T15 |
| `Tools/Validation/validate_content.py` | (No new phase needed; Plan 6 doesn't add catalog content beyond storylets covered by existing Phase 12) | — |

### Files created

| File | Purpose | Tasks |
| :-- | :-- | :-- |
| `src/Features/Patrons/PatronFavorResolver.cs` | `TryGrantFavor` implementation — checks cooldowns, conditions, applies outcome | T6 |
| `src/Features/Patrons/PatronAudienceExtension.cs` | Helper used by dialog conditions to check if conversation target is on Roll | T9 |
| `ModuleData/Enlisted/Storylets/patron_letter_of_introduction.json` | Letter favor outcome storylet | T10 |
| `ModuleData/Enlisted/Storylets/patron_gold_loan.json` | Gold loan favor outcome storylet | T11 |
| `ModuleData/Enlisted/Storylets/patron_troop_loan.json` | Troop loan favor outcome storylet | T12 |
| `ModuleData/Enlisted/Storylets/patron_audience.json` | Audience arrangement favor storylet | T13 |
| `ModuleData/Enlisted/Storylets/patron_marriage.json` | Marriage facilitation favor storylet | T14 |
| `ModuleData/Enlisted/Storylets/patron_another_contract.json` | Another contract favor (links to Plan 5 ContractActivity) | T14 |
| `docs/Features/Patrons/patron-favor-catalog.md` | Favor catalog reference doc | T1 |
| `docs/superpowers/plans/2026-04-24-ck3-wanderer-roll-of-patrons-verification.md` | Plan 6 verification report | T18 |

### Subsystems Plan 6 does NOT touch

- News-feed integration of patron deaths (deferred — direct-to-news fallback for v1; news-v2 substrate is separate spec)
- BarterManager (skipped per spec §3.3 v1 decision)
- Companion spawning
- Officer Trajectory equipment / dialog
- Endeavor System (Plan 5 owns ContractActivity; Plan 6 just emits an "another contract" favor outcome that creates one)

---

## §4 — Locked design decisions

### §4.1 Patron lifecycle (LOCKED)

- **Patron entry created** when `EnlistmentBehavior.IsEnlisted` flips false AND it's NOT full retirement (i.e. mid-career discharge — fired, captured, lord lost, faction-switch, grace-period mid-flow). 
- **Per-player only** — patrons accumulate across enlistments. Sergeant + Field Medic + Pathfinder are also per-player but those are companions (Plan 2); patrons are heroes you've served (former lords).
- **Full retirement clears the Roll** via `PatronRoll.Clear()`. CK3 patrons would persist; Enlisted explicitly trades that for cleaner mod boundary per §0 scoping.

### §4.2 6 FavorKind values (LOCKED)

| FavorKind | Cooldown | Conditions | Outcome (typical) |
| :-- | :-- | :-- | :-- |
| `LetterOfIntroduction` | 30 days | Patron alive, opinion ≥ 0 | +5 renown, gain weak hook on a lord in patron's faction (storylet flavor) |
| `GoldLoan` | 60 days | Opinion ≥ 10, patron has ≥ 5000 gold | Receive 5000 denars; patron relation -3; track debt for future repayment storylet |
| `TroopLoan` | 90 days | Opinion ≥ 20, party space available | Add 1-2 high-tier knights to player MainParty for 7 days; auto-removed via scheduled `RemoveCompanionAction.ApplyByFire` |
| `AudienceArrangement` | 45 days | Patron alive, opinion ≥ 5 | Patron arranges meeting with a third lord; opens audience flow with that lord on next encounter |
| `MarriageFacilitation` | 180 days | Opinion ≥ 30 | Patron introduces player to a marriage candidate from their clan/court; storylet outcome (eligibility check) |
| `AnotherContract` | 30 days | Opinion ≥ 0, ContractActivity available (Plan 5 dependency) | Patron offers a high-priority contract; spawns ContractActivity (Plan 5) with patron-flavored origin |

### §4.3 PatronEntry storage (LOCKED)

```csharp
[Serializable]
public sealed class PatronEntry
{
    [SaveableField(1)] public MBGUID HeroId;
    [SaveableField(2)] public int DaysServed;
    [SaveableField(3)] public int MaxRankReached;
    [SaveableField(4)] public CampaignTime DischargedOn;
    [SaveableField(5)] public CampaignTime LastFavorRequestedOn;
    [SaveableField(6)] public string PerFavorCooldownsCsv = ""; // "GoldLoan:1234567890;TroopLoan:1234567899" (kind:campaignTimeNumericTicks)
    [SaveableField(7)] public int RelationSnapshotOnDischarge;
    [SaveableField(8)] public bool IsDead;
    [SaveableField(9)] public string FactionAtDischarge = ""; // by id, not reference
    
    // Helper methods Plan 6 T2 implements:
    public CampaignTime? GetCooldown(FavorKind kind);
    public void SetCooldown(FavorKind kind, CampaignTime expiry);
    public bool IsFavorAvailable(FavorKind kind, CampaignTime now);
}
```

### §4.4 Audience integration (LOCKED)

Plan 6 does NOT modify the `OnTalkToSelected` menu flow. Instead, it adds dialog branches at `lord_pretalk` (vanilla token) with priority 112:

```csharp
starter.AddPlayerLine(
    "patron_call_in_favor",
    "lord_pretalk",
    "patron_favor_hub",
    "{=patron_call_in_favor_text}My lord, I have served you faithfully in the past. I come seeking your aid now.",
    () => PatronAudienceExtension.IsConversationTargetPatron(),
    null,
    112);
```

When player initiates conversation with any lord (via existing audience flow OR via map encounter), if target is on PatronRoll the favor option appears in the dialog. Vanilla flow handles the rest.

### §4.5 Cooldown encoding (LOCKED)

Per CLAUDE.md known issue #14, `Dictionary<FavorKind, CampaignTime>` is NOT directly saveable. Plan 6 encodes cooldowns as a CSV string `"<kind_id>:<ticks>;<kind_id>:<ticks>"` per spec §3.3 design:

- `kind_id` = enum integer value (e.g. `1` for `LetterOfIntroduction`)
- `ticks` = `CampaignTime.NumTicks` long value

Helper methods (`GetCooldown`, `SetCooldown`) parse/serialize this string transparently.

### §4.6 Favor outcome architecture (LOCKED)

Each favor outcome is a storylet (modal popup with 2-3 options). When player selects a favor in the audience dialog, Plan 6:

1. Calls `PatronFavorResolver.TryGrantFavor(patronEntry, FavorKind.GoldLoan)` — checks conditions + cooldowns
2. If granted: dispatches the matching storylet via `ModalEventBuilder.FireSimpleModal("patron.gold_loan", ctx, chainContinuation: false)` (Plan 1 helper from §6.8)
3. Storylet options encode the actual outcome (accept gold and pay relation cost, accept with caveat, decline)
4. Selected option's effects apply via `EffectExecutor` (existing) — gold via `GiveGoldAction`, relation via `ChangeRelationAction`, debt-tracking via `set_flag`

If NOT granted (cooldown active or condition unmet): `Inquiry` popup explains why ("Lord Crassus says: I'd like to help, but you asked recently. Come back in 12 days.")

---

## §5 — Tooling and safeguards

Inherits Plans 1-5. Plan 6-specific:

### Patron audience smoke recipe

For each favor (T10-T14):
1. Build clean.
2. Use Debug Tools to:
   - Set IsEnlisted = true with a Vlandian lord (Lord A)
   - Build up service days + relation
   - Discharge from Lord A (creates patron entry)
   - Re-enlist with another lord (Lord B)
3. Use Debug Tools to position MainParty near Lord A's party (or in Lord A's settlement).
4. Open Camp → Talk to my Lord → select Lord A.
5. Verify dialog branch "My lord, I have served you faithfully..." appears.
6. Select; verify favor sub-tree opens with all 6 favor kinds (or greyed-with-explanation if cooldown/conditions don't allow).
7. Pick a favor; verify storylet modal opens; pick an option; verify effects apply.
8. Re-enter the Lord A audience after favor; verify cooldown activates (favor greyed).

### Patron lifecycle smoke recipe

1. Enlist with Lord A; serve 30 days; discharge.
2. Verify `PatronRoll.Entries` has 1 entry with correct HeroId + DaysServed=30.
3. Enlist with Lord B; serve 60 days; discharge.
4. Verify `PatronRoll.Entries` has 2 entries.
5. Use Debug Tools to kill Lord A (`KillCharacterAction.ApplyByMurder` or equivalent).
6. Verify Lord A's entry has `IsDead = true`.
7. Open audience with Lord A's clan members or visit their tomb — verify dead patron is greyed in audience flow.
8. Trigger full retirement — verify `PatronRoll.Entries` is cleared.

---

## §6 — Tasks (sequential)

### T1 — `FavorKind` enum populated + favor catalog doc

**Goal:** Replace Plan 1 T4 stub `None = 0` with the 6 favor kinds. Author favor catalog reference doc.

**Files:**
- Edit `src/Features/Patrons/FavorKind.cs`
- New `docs/Features/Patrons/patron-favor-catalog.md`

**Concrete:**

```csharp
namespace Enlisted.Features.Patrons
{
    public enum FavorKind
    {
        None = 0,
        LetterOfIntroduction = 1,
        GoldLoan = 2,
        TroopLoan = 3,
        AudienceArrangement = 4,
        MarriageFacilitation = 5,
        AnotherContract = 6
    }
}
```

**Verification:** Build clean. Save-load round-trip with one favor kind set on a stub PatronEntry round-trips correctly (enum serializes fine; was registered at offset 84 in Plan 1 T4).

### T2 — `PatronEntry` helper methods

**Goal:** Add CSV cooldown helpers + IsFavorAvailable.

**Files:** Edit `src/Features/Patrons/PatronEntry.cs`.

**Concrete:**

```csharp
public CampaignTime? GetCooldown(FavorKind kind)
{
    if (string.IsNullOrEmpty(PerFavorCooldownsCsv)) return null;
    var parts = PerFavorCooldownsCsv.Split(';');
    foreach (var part in parts)
    {
        var kv = part.Split(':');
        if (kv.Length != 2) continue;
        if (int.TryParse(kv[0], out var k) && (FavorKind)k == kind &&
            long.TryParse(kv[1], out var ticks))
        {
            return new CampaignTime(ticks);
        }
    }
    return null;
}

public void SetCooldown(FavorKind kind, CampaignTime expiry)
{
    var existing = (PerFavorCooldownsCsv ?? "").Split(';').Where(s => !string.IsNullOrEmpty(s)).ToList();
    existing.RemoveAll(s => s.StartsWith($"{(int)kind}:"));
    existing.Add($"{(int)kind}:{expiry.NumTicks}");
    PerFavorCooldownsCsv = string.Join(";", existing);
}

public bool IsFavorAvailable(FavorKind kind, CampaignTime now)
{
    var cooldown = GetCooldown(kind);
    return !cooldown.HasValue || cooldown.Value <= now;
}
```

**Verification:** Unit smoke: set cooldown for GoldLoan to T+60 days; IsFavorAvailable returns false at T+30 and true at T+90.

### T3 — `PatronRoll` discharge handler

**Goal:** Subscribe to discharge event in EnlistmentBehavior; create entry.

**Files:** Edit `src/Features/Patrons/PatronRoll.cs`.

**Concrete (key method):**

```csharp
public void OnDischarge(Hero lord, /* DischargeReason */ object reason)
{
    if (lord == null || EnlistmentBehavior.Instance == null) return;
    
    // Don't add if already on the Roll (re-enlisted with same lord, then discharged again)
    var existingIdx = _entries.FindIndex(e => e.HeroId == lord.Id);
    
    var entry = new PatronEntry
    {
        HeroId = lord.Id,
        DaysServed = EnlistmentBehavior.Instance.DaysServed,  // assume this property exists
        MaxRankReached = EnlistmentBehavior.Instance.EnlistmentTier,
        DischargedOn = CampaignTime.Now,
        LastFavorRequestedOn = CampaignTime.Never,
        PerFavorCooldownsCsv = "",
        RelationSnapshotOnDischarge = lord.GetRelationWithPlayer(),
        IsDead = !lord.IsAlive,
        FactionAtDischarge = lord.MapFaction?.StringId ?? ""
    };
    
    if (existingIdx >= 0)
    {
        // Update existing entry (re-enlisted/re-discharged): keep old DaysServed cumulative,
        // refresh discharge-on, keep cooldowns (don't reset by re-discharge)
        var prior = _entries[existingIdx];
        entry.DaysServed += prior.DaysServed;
        entry.PerFavorCooldownsCsv = prior.PerFavorCooldownsCsv;  // preserve cooldowns
        _entries[existingIdx] = entry;
    }
    else
    {
        _entries.Add(entry);
    }
    
    ModLogger.Expected("PATRONS", "discharge_recorded",
        $"Patron entry for {lord.Name}: {entry.DaysServed} days, T{entry.MaxRankReached}",
        new { lordId = lord.StringId, daysServed = entry.DaysServed });
}
```

**Verification:** Smoke: enlist + discharge; confirm entry created. Re-enlist + discharge same lord; confirm entry updated (cumulative days), not duplicated.

### T4 — `PatronRoll.OnHeroKilled` handler

**Goal:** Mark patron entries as dead when their hero dies.

**Files:** Edit `src/Features/Patrons/PatronRoll.cs`. Subscribe to `CampaignEvents.HeroKilledEvent`.

**Concrete:**

```csharp
private void OnHeroKilledEvent(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
{
    if (victim == null) return;
    var entry = _entries.FirstOrDefault(e => e.HeroId == victim.Id);
    if (entry == null) return;
    
    entry.IsDead = true;
    ModLogger.Expected("PATRONS", "patron_died",
        $"Patron {victim.Name} died ({detail})",
        new { victimId = victim.StringId, detail = detail.ToString() });
}
```

**Verification:** Smoke: patron dies; confirm entry's IsDead = true.

### T5 — `PatronRoll.AvailableNearby` query

**Goal:** Returns list of patrons currently in proximity to the player (for audience flow).

**Files:** Edit `src/Features/Patrons/PatronRoll.cs`.

**Concrete:**

```csharp
public IEnumerable<PatronEntry> AvailableNearby(float maxDistance = 2.0f)
{
    var mainPos = MobileParty.MainParty?.GetPosition2D ?? Vec2.Zero;
    foreach (var entry in _entries)
    {
        if (entry.IsDead) continue;
        var hero = MBObjectManager.Instance.GetObject<Hero>(entry.HeroId);
        if (hero == null || !hero.IsAlive) continue;
        var heroPos = hero.PartyBelongedTo?.GetPosition2D ?? hero.HomeSettlement?.GatePosition ?? Vec2.Zero;
        if (mainPos.Distance(heroPos) <= maxDistance)
        {
            yield return entry;
        }
    }
}
```

**Verification:** Use Debug Tools to position near a patron's party; verify AvailableNearby returns the entry.

### T6 — `PatronFavorResolver`

**Goal:** Centralized "can grant favor?" + "grant favor" logic.

**Files:** New `src/Features/Patrons/PatronFavorResolver.cs`.

**Concrete:**

```csharp
public static class PatronFavorResolver
{
    public static bool TryGrantFavor(PatronEntry entry, FavorKind kind, out string refusalReason)
    {
        refusalReason = "";
        var hero = MBObjectManager.Instance.GetObject<Hero>(entry.HeroId);
        if (hero == null || !hero.IsAlive)
        {
            refusalReason = "{=patron_dead}This patron is no longer with us.";
            return false;
        }
        
        // Check cooldown
        if (!entry.IsFavorAvailable(kind, CampaignTime.Now))
        {
            refusalReason = "{=patron_cooldown}Asked too recently.";
            return false;
        }
        
        // Check kind-specific conditions per §4.2
        var relation = hero.GetRelationWithPlayer();
        switch (kind)
        {
            case FavorKind.LetterOfIntroduction:
                if (relation < 0) { refusalReason = "{=patron_relation_low}Relations are too cold."; return false; }
                break;
            case FavorKind.GoldLoan:
                if (relation < 10 || hero.Gold < 5000) { refusalReason = "..."; return false; }
                break;
            // ...
        }
        
        // Grant: set cooldown, fire outcome storylet
        entry.SetCooldown(kind, CampaignTime.DaysFromNow(GetCooldownDays(kind)));
        ModalEventBuilder.FireSimpleModal($"patron.{KindToStorylet(kind)}", BuildContext(entry, kind), chainContinuation: false);
        return true;
    }
    
    private static int GetCooldownDays(FavorKind kind) => kind switch
    {
        FavorKind.LetterOfIntroduction => 30,
        FavorKind.GoldLoan => 60,
        FavorKind.TroopLoan => 90,
        FavorKind.AudienceArrangement => 45,
        FavorKind.MarriageFacilitation => 180,
        FavorKind.AnotherContract => 30,
        _ => 0
    };
    
    // ...
}
```

**Verification:** Unit smoke: grant a favor; verify cooldown set + storylet fires.

### T7 — Hook discharge in EnlistmentBehavior

**Goal:** Wire `PatronRoll.OnDischarge(lord, reason)` to the discharge event.

**Files:** Edit `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs:3640+`.

**Concrete:** Find the discharge code path (where IsEnlisted flips false in the mid-career scenarios — fired, captured, lord lost, faction-switch, grace-period). Inject:

```csharp
// At end of mid-career discharge logic, before resetting fields:
PatronRoll.Instance?.OnDischarge(_enlistedLord, reason);
```

**Footgun:** Per §4.1, this fires for mid-career discharges ONLY. Full retirement uses a different code path (T8 hooks that separately and calls Clear()).

**Verification:** Smoke per §5 patron lifecycle recipe.

### T8 — Hook full retirement teardown

**Goal:** When player chooses full retirement (mod silence), call `PatronRoll.Clear()`.

**Files:** Edit `src/Features/Enlistment/Behaviors/EnlistmentBehavior.cs` retirement path.

**Concrete:**

```csharp
// In retirement code path, after setting IsRetiring:
PatronRoll.Instance?.Clear();
```

**Verification:** Smoke: trigger retirement; confirm `PatronRoll.Entries` is empty after; mod surface silenced.

### T9 — `PatronAudienceExtension` helper

**Goal:** Helper used by dialog conditions.

**Files:** New `src/Features/Patrons/PatronAudienceExtension.cs`.

**Concrete:**

```csharp
public static class PatronAudienceExtension
{
    public static bool IsConversationTargetPatron()
    {
        var target = Hero.OneToOneConversationHero;
        return target != null && PatronRoll.Instance?.Has(target.Id) == true;
    }
    
    public static PatronEntry GetEntryForTarget()
    {
        var target = Hero.OneToOneConversationHero;
        if (target == null) return null;
        return PatronRoll.Instance?.GetEntry(target.Id);
    }
}
```

**Verification:** Unit smoke: enter conversation with a patron; helper returns true. With non-patron lord; helper returns false.

### T10-T14 — Favor outcome storylets

**Goal:** Author 6 favor outcome storylets.

**Files:**
- T10: `ModuleData/Enlisted/Storylets/patron_letter_of_introduction.json`
- T11: `ModuleData/Enlisted/Storylets/patron_gold_loan.json`
- T12: `ModuleData/Enlisted/Storylets/patron_troop_loan.json`
- T13: `ModuleData/Enlisted/Storylets/patron_audience.json`
- T14: `ModuleData/Enlisted/Storylets/patron_marriage.json` + `patron_another_contract.json`

**Each storylet structure:** prompt + 2-3 options (accept / accept-with-caveat / decline). Each option has effects (gold via GiveGoldAction, relation via ChangeRelationAction, follow-up flags) + flavor text.

**Example for `patron_gold_loan`:**

```json
{
  "id": "patron.gold_loan",
  "title": "A Loan from {PATRON_NAME}",
  "setup": "{PATRON_NAME} pours you wine and considers your request. \"Five thousand denars... It's not a small sum, friend, but I owe you something for {PAST_CAMPAIGN}. You'll repay me?\"",
  "options": [
    {
      "id": "accept_with_promise",
      "text": "I will repay, my lord. With interest, when I can.",
      "effects": [
        { "apply": "give_gold", "args": [5000] },
        { "apply": "patron_relation_drift_minor_negative" },
        { "apply": "set_flag", "args": ["patron.debt.lord_id", "5000"] }
      ]
    },
    {
      "id": "accept_with_humility",
      "text": "I am in your debt, lord. I will not forget.",
      "effects": [
        { "apply": "give_gold", "args": [5000] },
        { "apply": "patron_relation_drift_minor_neutral" },
        { "apply": "set_flag", "args": ["patron.debt.lord_id", "5000"] }
      ]
    },
    {
      "id": "decline",
      "text": "On reflection, my lord, I withdraw the request.",
      "effects": [
        { "apply": "patron_relation_drift_minor_positive" }
      ]
    }
  ]
}
```

**Verification:** `validate_content.py` Phase 12 passes (storylet refs resolve). Smoke per favor recipe.

### T15 — Patron dialog branches in `EnlistedDialogManager`

**Goal:** Add ~10 dialog branches for favor sub-tree at `lord_pretalk` priority 112.

**Files:** Edit `src/Features/Conversations/Behaviors/EnlistedDialogManager.cs`.

**Concrete (per spec §3.3 dialog example):**

```csharp
private void AddPatronDialogs(CampaignGameStarter starter)
{
    // Player initiates: "Call in a favor"
    starter.AddPlayerLine(
        "patron_call_in_favor",
        "lord_pretalk",
        "patron_favor_hub",
        "{=patron_call_in_favor}My lord, I have served you faithfully in the past. I come seeking your aid now.",
        () => PatronAudienceExtension.IsConversationTargetPatron(),
        null,
        112);
    
    // Patron acknowledges (NPC line)
    starter.AddDialogLine(
        "patron_acknowledge",
        "patron_favor_hub",
        "patron_favor_options",
        "{=patron_acknowledge}{PATRON_GREETING_BY_RELATION} What do you need?",
        () => { SetPatronFlavorVariables(); return true; },
        null,
        112);
    
    // 6 favor option player lines (each with condition + consequence)
    foreach (var kind in Enum.GetValues<FavorKind>())
    {
        if (kind == FavorKind.None) continue;
        AddFavorOptionLine(starter, kind);
    }
    
    // Refusal text (shown when favor unavailable)
    starter.AddDialogLine(
        "patron_favor_refused",
        "patron_favor_refused_state",
        "lord_pretalk",
        "{=patron_favor_refused}{REFUSAL_REASON}",
        null,
        null,
        112);
}
```

**Verification:** Smoke: open patron audience; 6 favor options surface; greyed when unavailable.

### T16 — Save-load round-trip end-to-end

**Goal:** Comprehensive save-load smoke covering all PatronRoll edge cases.

**Test cases:**
1. Empty PatronRoll round-trips clean.
2. PatronRoll with 3 entries (1 alive, 1 dead, 1 with active cooldowns) round-trips clean.
3. Mid-favor-grant save (cooldown set, storylet about to fire) round-trips clean.
4. Save predating Plan 6 (PatronRoll didn't exist) loads with `EnsureInitialized` reseating Entries to empty list.

### T17 — Cross-system flag integration

**Goal:** Verify ceremony choice flags + lifestyle unlock flags + endeavor flags are queryable from patron favor outcome storylets.

**Test:** `patron_audience.json` storylet has option text variant based on `ceremony.t4.choice == "obey"` (player who obeyed questionable orders gets a different flavor when arranging audiences).

### T18 — Plan 6 verification report

**Goal:** Document all smokes, content stats, sign-off.

**Files:** New `docs/superpowers/plans/2026-04-24-ck3-wanderer-roll-of-patrons-verification.md`.

---

## §7 — Risks

### Risk M1 — Discharge event not properly hooked (MEDIUM)

**Vector:** EnlistmentBehavior may have multiple discharge code paths (fired, captured, lord lost, faction-switch). Missing one = no patron entry created.

**Mitigation:**
- T7 audits all discharge paths (search for `IsEnlisted = false` assignments)
- Smoke each path separately

### Risk M2 — CSV cooldown encoding edge cases (MEDIUM)

**Vector:** Empty string vs null vs malformed CSV. Parsing may NRE or silently skip cooldowns.

**Mitigation:**
- T2 helper methods handle all three cases
- Unit smoke verifies edge cases

### Risk M3 — Hero MBGUID instability across some saves (LOW-MEDIUM)

**Vector:** Per CLAUDE.md, MBGUID should be stable, but pre-Bannerlord-1.x saves may have differences.

**Mitigation:**
- Verify MBGUID retrieval works in T5 nearby query

### Risk L1 — Favor abuse (LOW)

**Vector:** Player accumulates dozens of patrons; can spam favors.

**Mitigation:**
- Per-favor cooldowns (30-180 days each)
- Per-patron cooldown not enforced (left to natural per-favor cooldowns; may add in Plan 7 if playtest shows abuse)

---

## §8 — Verification gates

- [ ] Build clean
- [ ] Validators pass
- [ ] All 6 favor kinds operate (T10-T14)
- [ ] Patron entries created on discharge (T3 + T7)
- [ ] Patron deaths flag entries correctly (T4)
- [ ] AvailableNearby works (T5)
- [ ] PatronFavorResolver enforces cooldowns + conditions (T6)
- [ ] Audience dialog branches surface for patrons (T9 + T15)
- [ ] Full retirement clears Roll (T8)
- [ ] Save-load round-trip across edge cases (T16)
- [ ] Cross-system flag integration (T17)
- [ ] Verification report committed

---

## §9 — Definition of done

Plan 6 complete when 18 tasks ✅, §8 gates pass, report committed.

---

## §10 — Hand-off to Plan 7

### For Plan 7 (Polish + Smoke)
- Per-patron cooldown if playtest shows abuse
- News-feed integration of patron deaths (when news-v2 substrate ships)
- Custom 3D mesh for patron portraits (deferred)
- Tune favor stat numbers (gold loan amount, troop loan size, etc.)
- Cultural variants per favor outcome

---

## §11 — Out of scope

- BarterManager-driven favors (skipped per spec §3.3 v1)
- Post-retirement patron access (explicitly excluded per scoping)
- News-feed patron updates (separate spec)

---

## §12 — References

- Plans 1-5 + verification reports
- Spec v6 §3.3 + §0 + §6.5 + §6.6 + §6.8
- AGENTS.md Critical Rule #3 (gold transactions)
- CLAUDE.md known issues #4, #14
- Existing `EnlistmentBehavior` discharge paths
- Existing `EnlistedDialogManager`
- Decompile: `Hero.GetRelation`, `ChangeRelationAction`, `GiveGoldAction`, `KillCharacterAction.OnHeroKilled`, `AddCompanionAction`, `RemoveCompanionAction`
