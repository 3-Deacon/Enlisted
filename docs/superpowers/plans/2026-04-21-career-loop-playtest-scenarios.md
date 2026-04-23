# Plan 5 — Career Loop Playtest Scenarios A–G

**Purpose:** Human-operator smoke runbook for the integrated Plans 1-5 surface. Each scenario is designed to exercise one intended interaction pattern and produce observable session-log markers that prove the wiring.

**Running environment:** Bannerlord with the Enlisted mod installed. Log path: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Enlisted\Debugging\Session-*.log`. Open in a tailing viewer during play.

**Debug hotkeys available** (Plan 5 T15 — all Ctrl+Shift):
- `I` — dump intelligence snapshot
- `A` — dump active named-order arc state
- `O` — dump path-score overview + committed/resisted flags
- `F` — force-fire the current top-path's T4 crossroads (bypasses score / already-committed guards; useful when the player has not naturally reached T4)

**Pass / fail** — each scenario lists its own pass criteria. Treat ANY `ModLogger.Surfaced` call in the session log during the run as a FAIL unless the scenario explicitly expects one.

---

## Scenario A — Peaceful garrison run

**Setup:** Load a save where the player is enlisted with a lord whose party is stationed in a friendly town and whose faction is at peace. Alternatively: enlist fresh, wait in-game until the column arrives at a friendly settlement.

**Steps:**
1. Remain at the settlement for 30 in-game days.
2. At day 5, 15, 25, press Ctrl+Shift+O and record the path-score values.
3. Accept at least 3 named orders (any archetype) and see each through to resolve.

**Expected log markers:**
- `DUTY heartbeat: enlisted=true snapshot=true tracked=...` every ~12 game-hours.
- `DUTY daily_counts: garrisoned=N ...` every 24 game-hours, N>0 on most days.
- `DUTY emitted Episodic storylet=duty_garrisoned_*` at least 6 times across 30 days.
- Each accepted named order logs `arc_splice_started ...` on accept and `arc_splice_ended ...` on resolve.
- `INTEL/hourly_recompute` fires hourly without gaps.

**Pass criteria:** zero `Surfaced` calls. Ambient storylets fire at cadence. 3 named-order accept→resolve cycles complete.

---

## Scenario B — Marching army campaign

**Setup:** Find a save (or force via debug / world state) where the enlisted lord has just joined an army that is marching toward an objective (enemy party or siege target). Confirm via `Ctrl+Shift+I` that the snapshot shows non-trivial `FrontPressure` and the lord's party is in an army.

**Steps:**
1. Ride with the army for 20 in-game days (or until the army dissolves, whichever is sooner).
2. Every 5 days press Ctrl+Shift+I and record `Posture` / `FrontPressure` / `ArmyStrain`.
3. Observe combat encounters; let the player participate passively.

**Expected log markers:**
- `DUTY emitted ArcScale storylet=...` at least 2 times across 20 days (arc-scale duty prompts during marching).
- `DUTY daily_counts: marching=N` non-zero while actively marching; transitions to `garrisoned=N` when the army pauses at a settlement.
- `DUTYPROFILE transition_emitted: transition_marching_to_garrisoned ...` on arrival at a settlement mid-march.
- Plan 2 AI intervention markers (`AI_BIAS: ...`) when the enlisted lord's party engages in target-score / army-formation / pursuit decisions.

**Pass criteria:** arc-scale prompts appear. Profile transitions fire. AI biases recorded. Zero `Surfaced`.

---

## Scenario C — Full siege cycle

**Setup:** Enlist with a lord whose party is part of a faction at war and whose army is approaching an enemy settlement. Alternatively: use console to teleport the enlisted lord's party to a settlement and trigger siege.

**Steps:**
1. Besiege the settlement. Wait through the siege prep phase (~3-5 days).
2. When the assault triggers, let the player participate.
3. Observe the breach + capture.
4. Continue for 3 days post-capture.

**Expected log markers:**
- `DUTY daily_counts: besieging=N` during siege prep, N > 0.
- `DUTY emitted ArcScale storylet=order_siege_works ...` at least once during siege prep.
- On siege-end: `DUTYPROFILE transition_emitted: transition_besieging_to_marching ...` or `transition_besieging_to_garrisoned ...` depending on post-capture movement.
- `INTEL` snapshot shows `Posture=OffensiveSiege` during siege, transitions out afterward.

**Pass criteria:** arc-scale siege prompt fires. Transition closure fires on siege-end. Zero `Surfaced`.

---

## Scenario D — Raid campaign

**Setup:** Enlist with a lord who has active raid orders (or manipulate a save such that the lord's party is raiding enemy villages).

**Steps:**
1. Raid at least 3 villages over ~10 in-game days.
2. Observe option-choice storylets during each raid.

**Expected log markers:**
- `DUTY daily_counts: raiding=N` non-zero during active raids.
- Ambient storylets from `duty_raiding_*` pool fire during raiding days.
- If the lord has positive Mercy: `duty_raiding_spare_village_trait_1` should become eligible and may fire (observable via `DUTY emitted Episodic storylet=duty_raiding_spare_village_trait_1`).
- If the lord has negative or zero Mercy: `duty_raiding_prisoner_harsh_1` should become eligible (id may fire during the run).
- Post-raid: `floor_aftermath_notice` storylets from Plan 3 signals fire in the news feed.

**Pass criteria:** at least one trait-gated storylet fires whose gate matches the actual lord's trait level. Plan 3 aftermath signals appear. Zero `Surfaced`.

---

## Scenario E — Lord captured + released

**Setup:** Enlist with a lord. Force the lord's capture via combat defeat (join a battle the enlisted army loses).

**Steps:**
1. Observe the capture event. Player is now captured alongside the lord.
2. Remain captured for ~5 in-game days.
3. Escape or be released (vanilla mechanic).
4. Continue for 3 days post-release.

**Expected log markers:**
- On capture: `DUTYPROFILE transition ...` flips profile to `imprisoned`.
- `DUTY daily_counts: imprisoned=N` non-zero during captivity.
- Ambient storylets from `duty_imprisoned_*` pool fire.
- On release: `DUTYPROFILE transition_emitted: transition_imprisoned_to_* ...`.
- If the scenario ends with enlistment ending (e.g., lord dies in the process): `prior_service_culture_<id>` and `prior_service_rank_<N>` flags set (observable via Ctrl+Shift+O post-un-enlistment).

**Pass criteria:** profile flips to imprisoned on capture + back on release. Imprisoned pool fires. Zero `Surfaced`.

---

## Scenario F — Cross-culture service

**Setup:** Create a player with one culture (e.g., Vlandian) and enlist with a lord of a different culture (e.g., Khuzait). Confirm with `Ctrl+Shift+I` that the intel snapshot reports the lord's culture, not the player's.

**Steps:**
1. Remain enlisted for 15 in-game days, cycling through garrisoned + marching profiles.
2. Observe ambient storylet firings.

**Expected log markers:**
- `DUTY emitted Episodic storylet=duty_garrisoned_sword_form_1__khuzait` (or other culture overlay matching the lord's Khuzait culture) fires at least once, in preference to the `duty_garrisoned_sword_form_1` base.
- Switching to a different-culture lord (unenlist + enlist elsewhere) shows a matching swap in which overlays are eligible.
- If the lord's culture has no overlay for a given base (e.g., Khuzait lord but a base storylet only has Empire/Sturgia/Vlandia overlays), the base fires.

**Pass criteria:** at least 3 distinct `__<culture>` overlay ids fire during the run, all matching the lord's culture. No cross-culture leak (never a `__battanian` overlay firing for a Khuzait lord). Zero `Surfaced`.

---

## Scenario G — T9 full-career arc

**Setup:** Fresh save; enlist with a lord. Either play naturally until tier advancement or use the mod's `Ctrl+Shift+X` debug hotkey (existing Home-surface debug) to grant enlistment XP and force tier-up faster.

**Steps:**
1. Grind / advance to T4. Verify a `path_crossroads_<path>_t4` Modal fires. Pick `commit`.
2. Verify `Ctrl+Shift+O` now shows `committed_<path>` flag and `committed_path=1`.
3. Continue to T6. Verify `path_crossroads_<path>_t6` Modal fires.
4. Continue to T7. Verify at least one `order_scout_<path>_t7` or `order_escort_<path>_t7` variant fires in place of the base scout/escort order when a named order is proposed.
5. Continue to T9. Verify `path_crossroads_<path>_t9` Modal fires.
6. **Fresh save for resist check:** start a new save, enlist, grind to T4, pick `resist` on the crossroads. Verify `Ctrl+Shift+O` shows `resisted_<path>` flag. Over next 5 in-game days of natural skill gains that would bump the resisted path, observe halved bump amounts in the `PATH` log category.

**Expected log markers:**
- `crossroads_emitted: path_crossroads_<path>_t4 path=<path> tier=4 score=...` on the T4 fire.
- `committed path=<path>` on option-commit.
- `resisted path=<path>` on option-resist.
- `Expected("PATH", "bump_resisted_<path>", "BumpPath halved by path_resisted_<path> flag" ...)` — at most one entry per resisted path per minute (60s per-key throttle on Expected). The ctx dict carries `original` and `halved` amounts, so the runner can confirm the halving arithmetic.

**Pass criteria:** all three crossroads (T4/T6/T9) fire on the commit-path save. A T7+ committed-path variant fires at T7+. On the resist-path save, `resisted_<path>` flag persists and `PathScorer.BumpPath` halves future bumps. Zero `Surfaced`.

---

## Scenario H — Fast-forward soak (throttle verification)

**Setup:** Same starting state as Scenario A (enlisted, peaceful garrison). This scenario is a time-compression soak; the intent is to verify the tick-driven log surface survives extreme speed AND that the news-feed throttle correctly silences at >4× per AGENTS.md pitfall #21.

**Steps:**
1. From the peaceful garrison base state, switch time to 4× speed. Let ~5 in-game days pass.
2. Observe the session log. News-feed entries (Plan 3 signal accordion output) SHOULD continue to arrive at this speed.
3. Switch to 8× or 16× speed (whichever the UI exposes). Let another ~5 in-game days pass.
4. Observe again. News-feed entries SHOULD now drop out (intentional — `OrdersNewsFeedThrottle.TryClaim()` rejects when `Campaign.Current.TimeControlMode == SpeedUpMultiplier` with multiplier > 4×). This is not a bug.
5. Confirm tick-driven log entries still fire at 16×:
   - `INTEL/hourly_recompute` — should continue hourly.
   - `DUTY heartbeat: ...` — should continue every ~12 in-game hours.
   - `DUTY daily_counts: ...` — should continue every 24 in-game hours.
   - `PATH session_heartbeat: ...` — should continue per its cadence.
6. Drop back to 1×. News-feed entries should resume immediately.

**Expected log markers:** same set as Scenarios A-G; the difference at 16× is the news-feed entries specifically go silent while tick logs continue.

**Pass criteria:** at 16×, news-feed entries stop appearing. Tick-driven heartbeats continue without gaps. Returning to 1× immediately restores news-feed output. Zero `Surfaced`.

**Reference:** AGENTS.md "Common pitfalls" #21 documents the throttle-by-speed behaviour. Missing news-feed entries at 16× is NOT a bug — filing one against this scenario wastes debug time. Tick-driven logs dropping out at 16× IS a bug — file it.

Covers Plan 4 T27 (fast-forward soak) from the Plan 4 verification doc's pending-smoke set.

---

## Failure-mode reference

If a scenario reports log entries that LOOK wrong, check against these known-safe patterns first:

- `DUTY no_opportunity_storylet ...` — builder produced candidates but none passed `IsEligibleForEmit`. Expected in edge cases (cooldown-exhausted pool); becomes a problem only if it fires on every tick.
- `Expected("PATH", "crossroads_no_scored_path", ...)` at T4 — genuine early-career with no skill lean yet. The crossroads correctly deferred. Try grinding one skill harder to produce a lean, then hit Ctrl+Shift+F to force-fire.
- `Expected("PATH", "crossroads_missing_storylet", ...)` — genuine authoring gap. Should NOT fire for the 15 ids shipped in T6; file a bug if it does.

Report failures with the full log excerpt + save file if possible.
