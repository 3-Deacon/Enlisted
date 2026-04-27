# Plan 6 — CK3 Wanderer Roll of Patrons: Verification Report

**Status:** 🟡 **Code-level verification complete; in-game smoke pending human operator (T16 / T17 scenarios in §4 below).** All 18 tasks shipped on `feature/plan6-roll-of-patrons` across two commits.

**Plan:** [2026-04-24-ck3-wanderer-roll-of-patrons.md](2026-04-24-ck3-wanderer-roll-of-patrons.md)
**Brief:** [docs/architecture/ck3-wanderer-architecture-brief.md](../../architecture/ck3-wanderer-architecture-brief.md)
**Branch:** `feature/plan6-roll-of-patrons` (worktree at `.worktrees/plan6-roll-of-patrons/`)
**Date:** 2026-04-27

**Commits on the branch:**
- `2911ebe` — pre-execution scaffolding (locks block + verification doc seed)
- `4c6ef61` — Phase A (T1-T6) substrate population
- `f498477` — Phase B+C (T7-T15) wiring + dialogs + 6 favor outcome storylets

**Sibling worktrees on the repo:**
- `feature/plan4-officer-trajectory` (Plan 4 in-progress, separate session — NOT yet on `development`)
- `feature/plan5-endeavor-system` (Plan 5 code + content shipped 2026-04-26, in-game smoke pending — NOT yet on `development`)

---

## §1 — What shipped

### Substrate population (Phase A — T1-T6, commit `4c6ef61`)

- **`FavorKind`** populated with 6 enum values (LetterOfIntroduction, GoldLoan, TroopLoan, AudienceArrangement, MarriageFacilitation, AnotherContract). Numeric values frozen — they're persisted in CSV cooldowns on `PatronEntry.PerFavorCooldownsCsv`.
- **`PatronEntry`** helper methods: `GetCooldown(FavorKind) → CampaignTime?`, `SetCooldown(FavorKind, CampaignTime)`, `IsFavorAvailable(FavorKind, CampaignTime now) → bool`. CSV format `"<kindId>:<hours>;<kindId>:<hours>"` using `CampaignTime.ToHours` (NumTicks isn't public surface; hour-resolution is sufficient for 30-180 day cooldowns).
- **`PatronRoll`** populated: `OnDischarge(Hero, string band)` instance method (re-discharge updates existing entry — cumulative DaysServed, preserved cooldowns), `OnHeroKilled(Hero victim)`, `AvailableNearby(maxDistance=2.0f)`, `GetEntry(MBGUID)`.
- **`PatronRollBehavior`** subscribes to `CampaignEvents.HeroKilledEvent` and routes victims to `_store.OnHeroKilled(...)`. Subscription must live on the behavior (not the POCO) since `AddNonSerializedListener` requires `CampaignBehaviorBase`.
- **`PatronFavorResolver`** new static class: `TryGrantFavor(entry, kind, out reason) → bool` (the central can-grant + grant gate), `IsKindAvailable(entry, kind) → bool` (used by dialog conditions). Per-kind condition checks (relation thresholds, patron gold, party space, runtime availability). AnotherContract degrades gracefully via `Type.GetType("Enlisted.Features.Endeavors.EndeavorRunner, Enlisted") != null` — Plan 5 isn't on development yet, so the favor option is hidden until that branch merges.
- **Favor catalog reference doc** at `docs/Features/Patrons/patron-favor-catalog.md` — living reference for the six favors (cooldowns / conditions / outcome storylet IDs / refusal loc-keys).

### Wiring + dialogs + content (Phase B+C — T7-T15, commit `f498477`)

- **Discharge hook in `EnlistmentBehavior.StopEnlist`** — single chokepoint via `TryUpdatePatronRollOnServiceEnd(reason, isHonorable)`. Risk M1 audit found ~17 `StopEnlist(...)` callers; the plan body's two-site design (T7 mid-career / T8 retirement) collapses into one helper that branches on the reason string. Mid-career discharges call `OnDischarge`; full-retirement reasons (`"Honorable retirement..."` / `"Honorable discharge - renewal term"`) call `Clear()`. Band derivation tries `_lastDischargeBand` first (set by `FinalizePendingDischarge`), then infers from reason keywords (deserter / captured / lord_lost / faction_switch / honorable / washout).
- **`PatronAudienceExtension`** new helper class: dialog-condition helpers `IsConversationTargetPatron()` and `GetEntryForTarget()` reading `Hero.OneToOneConversationHero`.
- **`AddPatronDialogs(starter)`** in `EnlistedDialogManager` — registers a "Call in a favor" branch at `lord_pretalk` priority 112. Six favor option lines, each gated by `IsKindAvailable`; failing options simply don't appear (vanilla pattern, no greying). Selecting an available favor calls `TryGrantFavor → fires outcome storylet → returns to lord_pretalk`. PATRON_NAME token set in the acknowledge condition (Lock 1 — Plan 6 self-wires the token until Plan 4's centralized `SetCommonDialogueVariables` wiring lands).
- **6 favor outcome storylets** at `ModuleData/Enlisted/Storylets/patron_*.json` (5 files: `patron_letter_of_introduction`, `patron_gold_loan`, `patron_troop_loan`, `patron_audience`, `patron_marriage`, `patron_another_contract` — last two share the `patron_marriage.json` + `patron_another_contract.json` file pair per plan §3). Each has 3 options that branch flavor + tune effects. Effects use existing primitives (`give_gold`, `grant_renown`, `set_flag`, `relation_change` with `target_slot: "Patron"`) and existing scripted effects (`ceremony_trait_drift_*`). No new scripted effects authored.
- **48 new loc-keys** synced to `ModuleData/Languages/enlisted_strings.xml` as UTF-8.

### Verification gates (current state)

- ✅ `dotnet build -c "Enlisted RETAIL" -p:Platform=x64` succeeds with 0 warnings, 0 errors.
- ✅ `python Tools/Validation/validate_content.py` passes (37 warnings + 44 info; same baseline as `development` — Plan 6 added zero new warnings).
- ✅ Error-codes registry regenerated (`docs/error-codes.md`) for line shifts in `EnlistmentBehavior.cs`.
- ✅ Save-definer offsets unchanged (Plan 1 already claimed 54 / 55 / 84).
- ⏳ In-game smoke pending — see §4 runbook for the human operator.

---

## §2 — Verification gates

- ✅ Build clean
- ✅ Validators pass (no new warnings)
- ✅ All 6 favor kinds populated (T1, FavorKind enum)
- ✅ Patron entries created on discharge (T3 + T7) — single chokepoint at StopEnlist
- ✅ Patron deaths flag entries correctly (T4 + PatronRollBehavior subscription)
- ✅ AvailableNearby works (T5)
- ✅ PatronFavorResolver enforces cooldowns + conditions (T6)
- ✅ Audience dialog branches surface for patrons (T9 + T15)
- ✅ Full retirement clears Roll (T7+T8 collapsed into TryUpdatePatronRollOnServiceEnd)
- ⏳ Save-load round-trip across edge cases (T16 — pending in-game smoke)
- ⏳ Cross-system flag integration (T17 — pending in-game smoke)
- ✅ Verification report committed (this doc)

---

## §3 — Pending work (in-game smoke)

T16 (save-load round-trip) and T17 (cross-system flag integration) are in-game scenarios for the human operator. Code-level verification is complete; behavior under live play remains to be observed. The runbook in §4 below names eight scenarios covering both tasks.

---

## §4 — In-game smoke runbook (T16 + T17)

The human operator runs these scenarios on the `feature/plan6-roll-of-patrons` build. After each scenario, check `Modules/Enlisted/Debugging/Session-A_*.log` for `PATRONS` category entries.

### Scenario 1 — Discharge → patron entry (T3 + T7)

1. Enlist with Lord A (Vlandian preferred for cleanest dialog token resolution).
2. Serve 30 days.
3. Trigger discharge (any path — fire, captured, faction-switch, deserter, lord-lost; ResolveSmuggleDischarge is the smuggle path).
4. **Expect:** `PATRONS / discharge_recorded` log entry with lord StringId + DaysServed=30 + tier + band.
5. **Verify:** `PatronRoll.Entries.Count == 1`; entry has correct HeroId + DaysServed=30 + IsDead=false + post-delta RelationSnapshotOnDischarge.

### Scenario 2 — Re-enlist + re-discharge same lord (cumulative DaysServed)

1. Re-enlist with Lord A. Serve 20 more days. Discharge again.
2. **Expect:** Second `PATRONS / discharge_recorded` log entry.
3. **Verify:** `PatronRoll.Entries.Count == 1` (NOT duplicated); entry's DaysServed=50 (cumulative); cooldowns from prior discharge preserved.

### Scenario 3 — Patron death (T4)

1. Use Debug Tools to kill Lord A (`KillCharacterAction.ApplyByMurder` or equivalent).
2. **Expect:** `PATRONS / patron_died` log entry.
3. **Verify:** Lord A's PatronEntry has `IsDead = true`. Other entries untouched.
4. **Side effect:** Lord A no longer appears in `AvailableNearby` queries (filtered out).

### Scenario 4 — Patron audience surfaces "Call in a favor" branch (T9 + T15)

1. Enlist with Lord B (different from Lord A).
2. Position MainParty near Lord A's party (use Debug Tools to teleport if needed).
3. Open Camp menu → Talk to lord → select Lord A.
4. **Expect:** `lord_pretalk` shows the player line *"My lord, I have served you faithfully in the past..."*.
5. Pick the line.
6. **Verify:** Patron acknowledges with PATRON_NAME interpolated into the greeting text.
7. **Verify:** Up to 6 favor options visible (those that pass `IsKindAvailable` — a fresh patron with relation ≥ 30 should show all 6 except AnotherContract until Plan 5 merges).

### Scenario 5 — Gold loan smoke (T11 + favor cooldown)

1. From Scenario 4 with relation ≥ 10 + patron Gold ≥ 5000.
2. Pick "A loan of coin, lord." → `patron_gold_loan` storylet fires as modal.
3. Pick "With interest, lord. When I can." option.
4. **Verify:** +5000 denars granted via `give_gold` (visible in UI per AGENTS.md Rule #3).
5. **Verify:** Patron relation -3 (`relation_change` target_slot=Patron applied).
6. **Verify:** Flag `patron_debt_5000` set.
7. Re-open audience with Lord A. **Verify:** "A loan of coin, lord." option NO LONGER appears (60-day cooldown active).

### Scenario 6 — AnotherContract integration (Plan 5 dependency)

When Plan 5 has not merged to development:
1. From Scenario 4, **verify:** "A contract, lord." option does NOT appear in the favor menu (hidden via reflection check in `PatronFavorResolver.IsKindAvailable`).

After Plan 5 merges to development:
1. Pick "A contract, lord." → `patron_another_contract` storylet fires.
2. Pick "I'll take it, lord." → +2500 denars + flag `patron_contract_offered_trusted`.
3. **Verify (Plan 5 dependency):** `ContractActivity` spawns with patron-flavored origin metadata. (Until Plan 5's runtime is loaded, the flag is set but no contract activity spawns — graceful degradation.)

### Scenario 7 — Full retirement clears the Roll (T8)

1. From any state with `PatronRoll.Entries.Count > 0` (carry over from Scenarios 1-3).
2. Trigger full retirement via `ProcessFirstTermRetirement` or `ProcessRenewalRetirement`. Reason string will contain "Honorable retirement" or "Honorable discharge - renewal term".
3. **Expect:** `StopEnlist` fires; `TryUpdatePatronRollOnServiceEnd` matches the retirement-reason path; `PatronRoll.Clear()` called.
4. **Verify:** `PatronRoll.Entries.Count == 0` after retirement completes.
5. **Verify:** No "Call in a favor" options surface in any subsequent lord audiences (mod surface silenced).

### Scenario 8 — Save-load round-trip (T16)

1. **Empty roll round-trip.** Save before any discharge. Reload. Verify `PatronRoll.Entries.Count == 0` and no errors in the log.
2. **3-entry mix.** Build to 3 patron entries (1 alive with no cooldowns, 1 with active GoldLoan cooldown, 1 dead). Save. Reload.
   - **Verify:** All 3 entries present with correct HeroId / DaysServed / cooldowns / IsDead flags.
   - **Verify:** PerFavorCooldownsCsv parses correctly via `GetCooldown(FavorKind.GoldLoan)`.
3. **Mid-favor save.** Save while a patron favor modal is queued (after `TryGrantFavor` set the cooldown but before player picks a storylet option). Reload.
   - Acceptable behavior: storylet re-fires (cleanest), OR the modal is skipped and the cooldown remains stamped (acceptable — player got the cooldown without the payoff).
4. **Pre-Plan-6 save.** Load a save predating Plan 6 (e.g. a `development`-baseline save).
   - **Verify:** `EnsureInitialized()` reseats `Entries` to `new List<PatronEntry>()` cleanly. No NRE.
   - **Verify:** Subsequent gameplay creates entries normally.

### Scenario 9 — Cross-system flag integration (T17)

This is a sanity check that ceremony / endeavor / lifestyle flags don't collide with patron flags.

1. Make a ceremony choice (e.g. T1→T2 ceremony with `obey` option). FlagStore now has `ceremony_choice_t2_obey`.
2. Discharge from Lord A. Reload save. Trigger Scenario 5 with Lord A.
3. **Verify:** Scripted effect for the patron favor option doesn't accidentally clear or interfere with ceremony flags. (None of the patron storylets touch `ceremony_*` flags — confirm by reading the storylet JSON if in doubt.)
4. **Verify:** Patron flags (`patron_debt_5000`, `patron_letter_introduction_pending`, etc.) coexist cleanly alongside ceremony / lifestyle flags in the save.

---

## §5 — Plan-vs-codebase divergences caught during execution

Per AGENTS.md pitfall #22, all plan-prescribed APIs were verified against the actual codebase + decompile. Findings:

1. **`[SaveableField]` attribute (plan §4.3 PatronEntry struct example).** Pre-existing convention — `PatronEntry.cs` from Plan 1 already used `[Serializable]` + auto-properties. Plan 6 continued the convention. Lock 2 fix #1 captured this up front.

2. **`Enum.GetValues<FavorKind>()` (plan §6 T15 example).** C# 7.3 (mod target) doesn't support the generic overload. Used `Enum.GetValues(typeof(FavorKind)).Cast<FavorKind>()` instead. Lock 2 fix #2 captured this up front.

3. **`lord.GetRelationWithPlayer()` (plan §4.2 + §6 T6).** Both `Hero.GetRelation(Hero)` and `Hero.GetRelationWithPlayer()` exist as vanilla extensions. The codebase uses both shapes. Plan 6 chose `GetRelationWithPlayer()` for cleaner reads. **Drift fix:** the method returns `float`, not `int`. Cast required when assigning to `RelationSnapshotOnDischarge` (int) and when comparing against int thresholds.

4. **`Hero.OneToOneConversationHero`** — confirmed against decompile (`Hero.cs:886`, static accessor delegating to `Campaign.Current.ConversationManager.OneToOneConversationHero`). Lock 2 fix #4 closed.

5. **`EnlistmentBehavior.Instance.DaysServed`** — exists at `EnlistmentBehavior.cs:8602` as a `float` (not `int` as plan body assumed). Cast `(int)enlistment.DaysServed` in the patron entry constructor. Lock 2 fix #5 closed.

6. **`MBObjectManager.Instance.GetObject<Hero>(MBGUID)`** — DOES NOT EXIST as a generic overload (despite Plan 6 hand-off claiming it was verified during Plan 5). The actual `MBObjectManager` API is `GetObject<T>(string)` (string lookup) or `GetObject(MBGUID) → MBObjectBase` (non-generic). Used `MBObjectManager.Instance.GetObject(entry.HeroId) as Hero` per Plan 5's pattern. **Lock 2 fix #6 was wrong — corrected here.**

7. **`HeroKilledEvent` subscription site (plan §6 T4).** Plan body says "Subscribe to `CampaignEvents.HeroKilledEvent` in `PatronRoll.cs`." Wrong — `AddNonSerializedListener` requires a `CampaignBehaviorBase`. Subscription lives in `PatronRollBehavior.RegisterEvents` and the handler calls `_store.OnHeroKilled(...)`. (advisor flagged this up front; captured here for the record.)

8. **`MobileParty.Position2D` / `Settlement.Position2D`** — DO NOT EXIST. Both types expose `GetPosition2D` returning `Vec2`. `Settlement.GatePosition` returns `CampaignVec2` (different type, not directly assignable to `Vec2`). Plan 5's code in `PatronRoll.AvailableNearby` uses `GetPosition2D` consistently for both lord parties and home settlements.

9. **`CampaignTime.NumTicks`** — NOT public. Used `(long)expiry.ToHours` for the CSV cooldown encoding instead. Hour-resolution is sufficient for 30-180 day cooldowns; daily-precision tick math would have been overkill anyway.

10. **`MobileParty.LimitedPartySize`** — DOES NOT EXIST. The actual property is `PartyBase.PartySizeLimit` (and `MobileParty.NumberOfAllMembers`). Used `partyBase.PartySizeLimit - party.MemberRoster.TotalManCount >= 2` for the troop-loan party-space check.

11. **`StopEnlist(...)` callers** — plan body Risk M1 said "audit all discharge paths." Audit found ~17 sites across `EnlistmentBehavior.cs` + `EnlistedDialogManager.cs` + `EventDeliveryManager.cs`. Plan body's "search for `IsEnlisted = false`" hint produced zero matches (the field is named `_isEnlisted` and isn't directly assigned in most paths; `StopEnlist` is the chokepoint). Final design: single hook inside `StopEnlist` itself with reason-based discriminator. Avoids per-site fragility.

12. **`ModLogger.Expected(category, key, summary, ctx)`** — `ctx` parameter is `IDictionary<string, object>`, not anonymous types. Used `LogCtx.Of("key1", value1, "key2", value2, ...)` per existing codebase pattern.

13. **`grant_renown` primitive** — DOES exist in `EffectExecutor.cs:112` as a registered primitive. Plan 6 storylets use it directly rather than authoring new scripted effects.

14. **Storylet `trigger` field** — `"always"` is NOT a registered trigger predicate; would fail eligibility check with "unknown trigger" warning. Used empty array `"trigger": []` instead — `TriggerRegistry.Evaluate` returns `true` for empty/null. Patron storylets are imperatively fired via `ModalEventBuilder.FireDecisionOutcome` and don't need eligibility gating anyway.

15. **`sync_event_strings.py --generate` encoding bug (NOT a Plan 6 fix).** The generator emits Windows-1252 instead of UTF-8 — em-dashes and other special characters round-trip as cp1252 single bytes. Worked around by reading source storylets directly and writing UTF-8 patron entries via Python. Pre-existing bytes in `enlisted_strings.xml` (~176 cp1252-suspicious) are NOT introduced by Plan 6; that's a project-wide cleanup task for a separate spec.

16. **`{LORD_NAME}` vs `{PATRON_NAME}` in patron storylet content** — caught at the verification advisor checkpoint. All six patron favor outcome storylets initially used `{LORD_NAME}` in title + setup text, but `SetCommonDialogueVariables` binds `LORD_NAME` to the player's currently-enlisted lord (`enlistment?.EnlistedLord?.Name`). When the player audiences a former patron Lord A while enlisted with Lord B, the storylet would have rendered "Lord B pours wine for you both" — the wrong lord. AGENTS.md pitfall #23 exactly. Fix: replace `{LORD_NAME}` → `{PATRON_NAME}` in all 6 storylet JSON files (12 token replacements) and in the matching enlisted_strings.xml entries (12 replacements in the patron block). The `PATRON_NAME` token is set by `PatronAcknowledgeCondition` immediately before the favor option line renders, so it's bound to the correct hero by the time the modal storylet fires (same frame).

---

## §6 — Hand-off surface (Plan 7 may use)

After Plan 6 ships:

- `Enlisted.Features.Patrons.PatronRoll.Instance` exposes `OnDischarge(Hero, string band)` / `OnHeroKilled(Hero)` / `Clear()` / `AvailableNearby(maxDistance)` / `Has(MBGUID)` / `GetEntry(MBGUID)`
- `Enlisted.Features.Patrons.PatronEntry` POCO with helpers `GetCooldown(FavorKind)` / `SetCooldown(FavorKind, CampaignTime)` / `IsFavorAvailable(FavorKind, CampaignTime)`
- `Enlisted.Features.Patrons.PatronFavorResolver.TryGrantFavor(entry, kind, out refusalReason)` — central can-grant check + grant + storylet fire
- `Enlisted.Features.Patrons.PatronFavorResolver.IsKindAvailable(entry, kind)` — used by dialog conditions to gate option visibility
- `Enlisted.Features.Patrons.PatronAudienceExtension.IsConversationTargetPatron()` / `GetEntryForTarget()`
- 6 favor outcome storylets at `ModuleData/Enlisted/Storylets/patron_*.json` — authors of new favor variants can copy the shape
- `EnlistmentBehavior.TryUpdatePatronRollOnServiceEnd(reason, isHonorable)` is the single chokepoint for any future patron-lifecycle work — reason-based discriminator decides Clear() vs OnDischarge

### Plan 7 polish items pre-deferred from Plan 6

- **TroopLoan favor's actual troop spawning.** Plan 6 ships TroopLoan as a gold-credit + flavor-text payoff (storylet effects use `give_gold`/`grant_renown`). Plan 7 should add a `give_troop_to_party` primitive (or scripted effect) and an auto-removal scheduler if 7-day temporary loans are wanted.
- **Per-patron cooldown.** Currently per-favor cooldowns only — a patron with 6 favor types could grant 6 favors back-to-back if conditions pass. Playtest will reveal whether this needs throttling.
- **News-feed integration of patron deaths.** Currently silent on `OnHeroKilled` — only logs `PATRONS / patron_died`. News-v2 substrate is a separate spec; Plan 7 can wire to it once available.
- **Cultural variants per favor outcome.** Each storylet has only the base variant (no Vlandian / Sturgian / Khuzait / etc. flavor). Plan 3's ceremony pattern is the precedent.
- **PATRON_NAME centralization cleanup.** Plan 6 inlines `MBTextManager.SetTextVariable("PATRON_NAME", ...)` in the patron acknowledge condition (Lock 1). When Plan 4 merges with the centralized `SetCommonDialogueVariables` shape, delete the inline call.
- **Refusal-reason surface.** Currently a hidden option means "not available" with no in-dialog explanation. UX would benefit from a refusal-reason floor (e.g. NPC-line "I'd grant it, but you asked recently") — requires either a separate dialog flow or an Inquiry popup, both Plan 7 work.
- **Renown reward via direct primitive.** TroopLoan / Letter / Marriage storylets all use `grant_renown` for a flat 5-10 renown bump. The cooldown-per-favor design already gates this, but if playtest finds renown stacks too fast across patrons, Plan 7 can switch to a once-per-patron flag.

---

## §7 — References

- [Plan 6 — CK3 Wanderer Roll of Patrons](2026-04-24-ck3-wanderer-roll-of-patrons.md) — owning plan with locks 1-3 at the top.
- [Architecture brief](../../architecture/ck3-wanderer-architecture-brief.md) — locked decisions Plan 6 inherits.
- [Plan 1 verification](2026-04-24-ck3-wanderer-architecture-foundation-verification.md) — substrate (`PatronRoll`/`PatronEntry`/`FavorKind` shells at offsets 54/55/84, `ModalEventBuilder` helper).
- [Plan 5 verification](2026-04-24-ck3-wanderer-endeavor-system-verification.md) — integration target for the AnotherContract favor (Lock 1 second clause).
- [Storylet backbone reference](../../Features/Content/storylet-backbone.md) — base schema for favor outcome storylets.
- [Patron favor catalog](../../Features/Patrons/patron-favor-catalog.md) — living reference for the six favors.
- [AGENTS.md](../../../AGENTS.md) — Critical Rule #3 (gold transactions), Pitfall #14 (HashSet not saveable), Pitfall #22 (plan-vs-codebase drift discipline).
- [CLAUDE.md](../../../CLAUDE.md) — project conventions (csproj wildcards, CRLF, error-codes regen, deserialization-skips-ctor → EnsureInitialized pattern).
