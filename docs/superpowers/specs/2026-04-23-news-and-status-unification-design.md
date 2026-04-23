# News + Status Unification — Design

**Status:** Draft (2026-04-23). Design spec for consolidating the Enlisted news/headlines surfaces across `enlisted_status` and `enlisted_camp_hub`.

**Problem statement.** News headlines are currently surfaced in three places: (1) the main `enlisted_status` body paragraph's `KINGDOM REPORTS` section (live-rebuilt on every tick, top 2 dispatches + world-state sentences), (2) a dedicated `HEADLINES [NEW]` top-level menu option that drills into an `enlisted_headlines` submenu listing unread headline-tier dispatches, and (3) the `enlisted_camp_hub` body's `RECENT ACTIVITY` section (personal-feed bullets). The same underlying feed also drives the muster period-digest. The duplication costs a top-level menu slot, confuses "where's the news?", and — critically — the live-rebuild at fast-forward churns the text faster than the player can read it.

**North star.** Clean scope split:

- **Main menu (`enlisted_status`)** → Kingdom news + upcoming duties.
- **Camp hub (`enlisted_camp_hub`)** → The player (personal feed, condition, duty status) + camp activities (company situation, routine outcomes).
- **No separate HEADLINES option.** The digest is part of the main body.
- **Stable at any time-control speed.** The text never churns while the player is trying to read.

---

## Final surface layout

### `enlisted_status` body — 2 sections

```
DISPATCHES · Week of <month> <day>                         [Header]
  <5-sentence weekly kingdom digest>                       [body text]

UPCOMING                                                   [Header]
  <live: active order + muster countdown + decisions>      [body text]
```

**`DISPATCHES`** — a 7-day rolling digest of kingdom-scope news. Body is **frozen between regeneration triggers** (see "Regeneration triggers" below). Sentence budget: 4–5. Content mix:

1. Lead with the most impactful kingdom-feed item from the last 7 days (via `GetVisibleKingdomFeedItems(...)` filtered to the window).
2. Secondary kingdom item if one exists.
3. War-stance line (peace / active war / desperate / multi-front) sourced from `WorldStateAnalyzer.AnalyzeSituation()` — snapshot at regeneration time, not live.
4. Siege summary (ours / theirs) — same snapshot.
5. Closing outlook sentence keyed to the stance.

The masthead `DISPATCHES · Week of <month> <day>` teaches the player the cadence — a stable date in the header explains why the body matches across multiple menu views.

**`UPCOMING`** — live-rebuilt on menu activation (cheap). Shows:

- Active named order title + elapsed hours if one is active; otherwise "No orders at present."
- Pending-decisions count (mirrors the Decisions accordion header).
- Imminent muster warning if `IsPayMusterPending`.

No sentence budget — this is a 2–3 line bulleted or short-prose block, live because its content is immediate-action relevant.

### `enlisted_camp_hub` body — 2 sections

```
YOU                                                        [Header]
  <live: duty state + condition + personal feed + rank>    [body text]

CAMP ACTIVITIES                                            [Header]
  <live: company state + routine outcomes + muster hint>   [body text]
```

**`YOU`** — absorbs the current main-menu `PLAYER STATUS` prose plus the camp hub's current `YOUR STATUS` + `RECENT ACTIVITY` content. Live-rebuild on camp-hub activation; cheap because it reads live state fields plus the top 3 `GetVisiblePersonalFeedItems(3)` entries. Content:

- Duty state opening (on-duty / off-duty with culture-aware NCO/officer titles).
- Physical condition (injury / illness / wound — uses the existing `PlayerConditionBehavior.State`).
- Up to 3 recent personal-feed events as a condensed prose continuation (not bulleted).
- Closing outlook per existing `BuildPlayerNarrativeParagraph` style.

**`CAMP ACTIVITIES`** — absorbs the current main-menu `COMPANY REPORTS` plus the camp hub's current company-status content. Live-rebuild. Content:

- Company mood + supplies + readiness + logistics sourced from `EnlistmentBehavior.CompanyNeeds` and `CampLifeBehavior`.
- Recent routine outcomes pulled from the personal feed where routine-source items exist.
- Muster-pending alert if `IsPayMusterPending`.

---

## Regeneration triggers (kingdom digest only)

The main-menu `DISPATCHES` body has its own cadence; every other section rebuilds live. The digest body is generated ONLY when one of three triggers fires AND `currentDay - _lastEditionDay >= 7`:

### Trigger 1 — Menu re-entry (`OnEnlistedStatusInit`)

Every return from a submenu / battle / settlement visit / dialogue / muster ceremony calls `OnEnlistedStatusInit`. This is the primary "player is about to read" signal at any time-control speed.

### Trigger 2 — Time-control step-down (speed → stop/normal)

Hook in `OnEnlistedStatusTick`: compare current `Campaign.Current.TimeControlMode` to the cached `_lastTimeControlMode`. On the transition from any `SpeedUpMultiplier` (4× / 8× / 16×) back to `Stop` / `Stoppable`, fire the regen check. Interpretation: the player paused to read.

### Trigger 3 — 1×-resident tick check

Hook in `OnEnlistedStatusTick`: if current `TimeControlMode` is Stop / Stoppable (not speed-up) AND `currentDay - _lastEditionDay >= 7`, fire the regen. Handles the case where the player is sitting at 1× or paused for a long stretch with the main menu resident and no submenu hops — at 1× in particular, 7 in-game days = ~7 real hours of uninterrupted play, which is plausible in long sessions.

At 1× the text visibly changes once per ~7 real hours. That's not churn; it's a weekly newspaper being handed over.

### Never regenerate

- On tick while `TimeControlMode` is in `SpeedUpMultiplier` (4× / 8× / 16×). Covered by Trigger 2's step-down.
- On any campaign state change (war declared, settlement captured, etc.). These fire the Modal event channel; they don't perturb the digest.
- On news-feed additions. New Headline-tier dispatches wait for the next edition.

### Implementation surface

Three new fields on `EnlistedMenuBehavior`:

```csharp
private string _cachedKingdomDigest = string.Empty;
private int _lastEditionDay = int.MinValue / 2;                 // sentinel per AGENTS.md pitfall #19
private TimeControlMode _lastTimeControlMode = TimeControlMode.Stop;
```

One new helper on `EnlistedNewsBehavior`:

```csharp
public string BuildKingdomDigestSection(int windowDays = 7);    // extracts from the orphaned
                                                                // BuildDailyBriefSection pieces
```

One new method on `EnlistedMenuBehavior`:

```csharp
private void TryRegenerateKingdomDigest(TriggerReason reason);  // checks 7-day gate, fires Build...
```

Trigger-reason enum is internal-only; used for logging (`ModLogger.Info("INTERFACE", ...)`) to aid smoke debugging.

---

## Removals (stale code cleanup)

| Item | Current location | Why it goes |
| :--- | :--- | :--- |
| `enlisted_headlines_entry` option | `EnlistedMenuBehavior.cs:928-945` | Superseded by in-body digest |
| `enlisted_headlines` submenu + `Back` option | `:1203-1232` | Superseded by in-body digest |
| `FormatHeadlines` / `CountUnreadHeadlines` / `MarkHeadlinesViewed` / `GetUnreadHighSeverity` / `_viewedHeadlineStoryKeys` field | `:1516-1567` + field | Manual "unread headlines" tracking retired — digest presence IS the read signal |
| `BuildDailyBriefSection()` | `EnlistedNewsBehavior.cs:477-650` | Refactored into `BuildKingdomDigestSection(int)`; unused company/player-branch sub-builders deleted |
| `_dailyBriefCompany`, `_dailyBriefUnit` fields + SyncData lines | `EnlistedNewsBehavior.cs:69-74, 267-276` | Only the kingdom half survives; rename the survivor to `_cachedKingdomDigest` moved to menu-behavior (removes the persisted state entirely — digest is ephemeral cache, not save state) |
| `_lastDailyBriefDayNumber` SyncData line | `:273` | Ephemeral cache doesn't need persistence |
| Main body `COMPANY REPORTS` section + `BuildCampNarrativeParagraph` | `EnlistedMenuBehavior.cs:3332-3338` + helper | Moved to Camp `CAMP ACTIVITIES` |
| Main body `PLAYER STATUS` section + `BuildPlayerNarrativeParagraph` | `:3341-3347` + helper | Moved to Camp `YOU` |
| Camp body `UPCOMING` section + its helper | `:1604-1609` | Moved to Main `UPCOMING` |

Helpers that survive the move (sometimes renamed to reflect their new home) include `BuildBriefPlayerRecap`, `BuildBriefPlayerForecast`, `BuildKingdomForecastLine`, `BuildCompanyForecastLine`, `BuildSupplyContextLine`, `BuildCasualtyReportLine`, `BuildRecentEventLine`, `BuildPendingEventsLine`, `BuildFlagContextLine`, `GetNCOTitle`, `GetOfficerTitle`. Their callsites move; their bodies stay.

---

## Scope boundaries

**In scope.**

- `enlisted_status` body rewrite (2 sections, weekly kingdom digest + live upcoming).
- `enlisted_camp_hub` body rewrite (2 sections, live YOU + live CAMP ACTIVITIES).
- Removing the HEADLINES option + submenu + helpers.
- Refactoring `BuildDailyBriefSection` → `BuildKingdomDigestSection`.
- Killing the persisted daily-brief state.
- Three-trigger regeneration wiring with sentinel-safe field initializers.
- Updating `docs/Features/Content/home-surface.md` + `docs/INDEX.md` to reflect the new layout where relevant.

**Out of scope.**

- `MusterMenuHandler` period-digest rendering. Uses the same feed but serves the pay-day ceremony, which is a distinct ritual the player opts into. Unchanged.
- `CampRoutineProcessor.AddToNewsFeed` and other producer-side code. Producers keep feeding the same `EnlistedNewsBehavior.AddRoutineOutcome` / `AddPersonalFeedItem` APIs.
- Combat log widget (`EnlistedCombatLog`). Orthogonal surface, different lifecycle.
- Conversation/dialogue news hooks (QM gossip, lord chatter). Separate content problem.
- Localization keys for any new headers (`{=dispatches_header}`, `{=camp_you_header}`, etc.) — authored during implementation, not designed here.

---

## Risks + mitigations

**Risk 1 — Player plays for 7+ real hours at 1× without opening any submenu.** The body text visibly changes once per week-of-play in this case. RP-acceptable (newspaper-handover metaphor) but worth naming. Mitigation: masthead shows the week-of date, so a player who notices the change sees why.

**Risk 2 — `TimeControlMode` enum values.** The code must match the actual Bannerlord `TimeControlMode` surface (`Stop`, `Stoppable`, `UnstoppablePlay`, `FastForward`, `UnstoppableFastForward` — verify against `../Decompile/TaleWorlds.CampaignSystem/TimeControlMode.cs` at implementation time per AGENTS.md Critical Rule #1). Treat "speed-up" as anything in the fast family; treat "stop/normal" as `Stop` + `Stoppable` + `UnstoppablePlay`.

**Risk 3 — Compiled-in 7-day constant.** Edition length is pinned to 7 days in the spec. If playtest feedback prefers a 12-day edition (muster-cycle aligned), the change is a one-constant edit in `BuildKingdomDigestSection`'s call site + the masthead label. Not a re-design.

**Risk 4 — `int.MinValue` sentinel on `_lastEditionDay`.** AGENTS.md pitfall #19 documents the `int.MinValue` wrap-around hazard. Use `int.MinValue / 2` as the sentinel; arithmetic `currentDay - _lastEditionDay` on a fresh state yields `currentDay + (int.MaxValue / 2)` which safely exceeds 7, triggering regen on first check.

**Risk 5 — Digest regeneration during a menu transition (init firing while time control is still settling).** Fire the regen call from `OnEnlistedStatusInit` after the existing time-control-restore logic, not before. Guard against `Campaign.Current` null (pre-session) with the existing patterns.

---

## Validation

No validator rails required — this is an intra-C# surface refactor that doesn't touch storylet authoring, effects, triggers, or save offsets. Standard `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` + `python Tools/Validation/validate_content.py` pass gates. In-game smoke:

- Open enlisted_status fresh after enlisting → DISPATCHES and UPCOMING render with current-week content.
- Open Camp hub → YOU and CAMP ACTIVITIES render with live content; UPCOMING and HEADLINES are gone.
- Exit to settlement and return → DISPATCHES regenerates if ≥7 days passed, else same text.
- Fast-forward 8 in-game days at 16× with enlisted_status showing → DISPATCHES stays frozen; pause → DISPATCHES flips to next edition.
- Sit at 1× for 7+ in-game hours straight → DISPATCHES flips once on the tick covering the boundary.
- No `HEADLINES` option appears in main menu at any point.
- `BuildDailyBriefSection` references produce a compile error (confirming all callers migrated).

No new save-definer offsets. No new validator phases. No new localization required beyond the few new header keys.

---

## Open questions

None locked in. Edition length (7 days) is the one tunable the playtest might want to revisit — flagged in Risk 3. Lord-culture flavor on the masthead label (`Week of` → `Sennight of` / `Fortnight` per culture) is deferred — not in scope unless a playtest surfaces it.
