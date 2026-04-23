# News + Status Unification — Design (v2)

**Status:** Draft v2 (2026-04-23). Revised after spec-review feedback flagged v1's incorrect feed-collapsing, missing `SINCE LAST MUSTER` section, regressed `UPCOMING` contract, and wrong `CampaignTimeControlMode` enum surface.

**Problem statement.** The Enlisted UI surfaces news across multiple touchpoints with inconsistent cadence and overlapping scope:

- `enlisted_status` body → `KINGDOM REPORTS` (tick-rebuild, top 2 kingdom-feed items + live war-stance prose), `COMPANY REPORTS`, `PLAYER STATUS`.
- `enlisted_status` option → `HEADLINES [NEW]` → `enlisted_headlines` submenu (renders unread headline-tier **personal-feed** items via `GetPersonalFeedSince(today - 7)`).
- `enlisted_camp_hub` body → `COMPANY STATUS`, `SINCE LAST MUSTER` (12-day period recap), `UPCOMING`, `RECENT ACTIVITY` (personal-feed items), `STATUS`.
- `EnlistedNewsBehavior.BuildDailyBriefSection()` / `EnsureDailyBriefGenerated()` + persisted `_dailyBriefCompany/_dailyBriefUnit/_dailyBriefKingdom` state — defined but **not wired** to any menu. Documented in `docs/Features/UI/news-reporting-system.md` as shipped functionality; in practice it's dead code.

The UI cost: (a) a menu option slot for `HEADLINES` whose content is scoped differently from the `KINGDOM REPORTS` paragraph next to it; (b) `COMPANY REPORTS` and `PLAYER STATUS` duplicated between main body and camp body; (c) the `UPCOMING` forecast surface sitting in Camp but describing imminent-duty stuff that belongs with the main Kingdom news. The gameplay cost: at fast-forward (16×) the main body text re-renders faster than the player can read.

**North star.** A clean scope split that honors the two feeds:

- **Main menu (`enlisted_status`)** = realm-scope view: Kingdom news + what's coming up for the player.
- **Camp hub (`enlisted_camp_hub`)** = player-scope view: what's happening to YOU + how the period is going + what the camp is up to.
- **No `HEADLINES` top-level option.** Personal headline-tier [NEW] markers live inline in the camp `YOU` section.
- **Stable body text under fast-forward.** No churn while the player is reading.
- **Preserve shipped contracts.** `SINCE LAST MUSTER` keeps its content; `UPCOMING` keeps its full forecast contract. This is a unification + regen-cadence fix, not a regression.

---

## The two feeds — stay separate

`EnlistedNewsBehavior` hosts two independent feeds:

| Feed | Scope | Accessor | Producer |
| :--- | :--- | :--- | :--- |
| **Kingdom feed** | Realm-level events — sieges, wars, notable kingdom news | `GetVisibleKingdomFeedItems(maxItems)` | `KingdomHeadlineFactProducer` + direct calls |
| **Personal feed** | Player/company-level events — battles, promotions, condition changes, routine outcomes | `GetVisiblePersonalFeedItems(maxItems)` + `GetPersonalFeedSince(dayNumber)` | `StoryDirector.AddPersonalDispatch(...)` (`StoryDirector.cs:323`) for Headline/Pertinent-tier, `CampRoutineProcessor.AddRoutineOutcome` for routine-scope |

These are NOT a single feed with filters — they're two lists with different population paths. The v1 spec incorrectly conflated them. v2 keeps them routed to different body sections: **kingdom feed → main `DISPATCHES`, personal feed → camp `YOU`.**

---

## Final surface layout

### `enlisted_status` body — 2 sections

```
DISPATCHES · Week of <month> <day>                         [Header]
  <4–5 sentence weekly kingdom digest>                     [body text, FROZEN between triggers]

UPCOMING                                                   [Header]
  <commitment + order + world-state hint + schedule>       [body text, live on each re-entry]
```

**`DISPATCHES`** — 7-day rolling digest of kingdom-feed items + war-stance snapshot. Body is frozen between regeneration triggers (see "Regeneration triggers"). Sentence budget 4–5. Content mix:

1. Lead with the most impactful kingdom-feed item from the last 7 days (top of `GetVisibleKingdomFeedItems(...)` filtered to the 7-day window).
2. Secondary kingdom item if one exists.
3. War-stance line (peace / active war / desperate / multi-front) from `WorldStateAnalyzer.AnalyzeSituation()` — snapshot at regen time, not live.
4. Siege summary (ours / theirs / active counts) — same snapshot.
5. Closing outlook sentence keyed to the stance.

The masthead `DISPATCHES · Week of <month> <day>` teaches the weekly cadence — a stable date in the header explains why the body matches across multiple views of the menu.

**`UPCOMING`** — live-rebuild on `OnEnlistedStatusInit` (cheap). **Preserves the full existing forecast contract from `BuildUpcomingSection()` at `EnlistedMenuBehavior.cs:2319-2420`:**

- Next player commitment from `CampOpportunityGenerator.GetNextCommitment()` with hours-until ("starting soon" if <2h, otherwise scheduled phase + hours).
- Active order with ~24h-baseline remaining-hours forecast (uses `OrderDisplayHelper`).
- World-state activity-level hint when no order is active (via `WorldStateAnalyzer.AnalyzeSituation().ExpectedActivity`: `Intense` → "Expect orders soon", `Active` → "Likely to receive orders within the day", else "No orders expected").
- Camp schedule next-phase forecast from `CampScheduleManager.GetScheduleForPhase(nextPhase)` with deviation / variety handling preserved.
- Muster countdown + pending-muster warning from `enlistment.LastMusterDay` / `IsPayMusterPending`.

This section moves unchanged from its current Camp Hub home at `EnlistedMenuBehavior.cs:1601-1609` — its helper stays as-is, the call site relocates.

### `enlisted_camp_hub` body — 3 sections

```
YOU                                                        [Header]
  <duty state + condition + personal feed + [NEW] markers> [body text, live]

SINCE LAST MUSTER                                          [Header]
  <12-day period recap>                                    [body text, live]

CAMP ACTIVITIES                                            [Header]
  <company state + routine outcomes>                       [body text, live]
```

**`YOU`** — player-scope narrative. Live-rebuild on `OnCampHubInit`. Absorbs the current main-menu `PLAYER STATUS` prose (`BuildPlayerNarrativeParagraph`) plus the camp hub's current `STATUS` (`BuildPlayerPersonalStatus`) plus `RECENT ACTIVITY` (`BuildRecentActivitiesNarrative` — the personal-feed pull). Content:

- Duty state opening: on-duty with order title + elapsed hours, or off-duty with culture-aware NCO/officer titles.
- Physical condition: injury / illness / wound via `PlayerConditionBehavior.State`.
- Up to 3 recent personal-feed events rendered in condensed prose — NOT bulleted (preserves narrative flow).
- **`<span style="Alert">NEW:</span>` prefix on any personal-feed item whose `Tier` is Headline or whose legacy `Severity >= 2`, AND whose `StoryKey` is not yet in `_viewedHeadlineStoryKeys`.** On camp-hub-open, all unread headline-tier personal items visible in this section are marked read (same bookkeeping the current `MarkHeadlinesViewed` uses — the code moves, the semantics stay).
- Closing outlook per the existing `BuildPlayerNarrativeParagraph` style.

The [NEW] prefix is the replacement for the `HEADLINES [NEW]` top-level option's functionality — same unread/mark-read semantics, same `StoryKey`-keyed tracking, just rendered inline in the camp body instead of behind a drilldown.

**`SINCE LAST MUSTER`** — **preserved verbatim.** The existing `BuildPeriodRecapSection(enlistment)` helper at `EnlistedMenuBehavior.cs:2213-2312` is called from here with its full contract intact:

- Orders completed / failed counts + last-order narrative from `news.GetRecentOrderOutcomes(12)`.
- Company losses (lost / sick) from `news.LostSinceLastMuster` / `news.SickSinceLastMuster`.
- Event choices handled from `news.GetRecentEventOutcomes(12)`.
- Muster countdown (`12 - daysSinceMuster`) with `<span style="Warning">` when ≤2 days, else default style.
- Muster-pending fallback when the countdown exhausted.

Stays in Camp Hub. Not moved, not cut.

**`CAMP ACTIVITIES`** — camp-scope state. Live-rebuild on camp-hub-open. Absorbs the current main-menu `COMPANY REPORTS` (`BuildCampNarrativeParagraph`) plus the camp hub's current `COMPANY STATUS` (`BuildCompanyStatusSummary`). Content:

- Company mood / supplies / readiness / logistics color-coded keywords from `EnlistmentBehavior.CompanyNeeds` + `CampLifeBehavior`.
- Recent routine outcomes where the personal feed has `Source == Routine` (filter in the builder, not a new feed).
- Muster-pending alert if `IsPayMusterPending` AND not already surfaced under `SINCE LAST MUSTER` (avoid double-warn).

---

## Regeneration triggers (kingdom digest only)

The main-menu `DISPATCHES` body has its own cadence; every other section rebuilds live on menu-init. The kingdom digest regenerates ONLY when one of three triggers fires AND `currentDay - _lastEditionDay >= 7`:

### Trigger 1 — Menu re-entry (`OnEnlistedStatusInit`)

Every return from a submenu / battle / settlement visit / dialogue / muster ceremony calls `OnEnlistedStatusInit`. Primary "player is about to read" signal at all time-control speeds.

### Trigger 2 — Fast-family → play-family transition (`OnEnlistedStatusTick`)

Hook in `OnEnlistedStatusTick`: compare current `Campaign.Current.TimeControlMode` (a `CampaignTimeControlMode`) to the cached `_lastTimeControlMode`. On transition **from fast-family to play-family**, fire the regen check.

- **Fast-family:** `UnstoppableFastForward`, `StoppableFastForward`, `UnstoppableFastForwardForPartyWaitTime`.
- **Play-family:** `Stop`, `UnstoppablePlay`, `StoppablePlay`, `FastForwardStop` (decompile-verified: `Campaign.TickMapTime` treats `FastForwardStop` identically to `Stop` with `num = 0`; `GetSimplifiedTimeControlMode` returns `Stop` via default arm).

Interpretation: the player slowed or paused — they're about to read.

### Trigger 3 — Play-family resident tick

Hook in `OnEnlistedStatusTick`: if current `TimeControlMode` is play-family (per the taxonomy above) AND `currentDay - _lastEditionDay >= 7`, fire the regen. Handles the case where the player sits at 1× for many in-game hours with the main menu resident and no submenu hops.

At 1× a visible text change occurs at most once per ~7 real hours — RP-acceptable as a weekly newspaper handover. The masthead `Week of <date>` changing is the explanation.

### Never regenerate

- On tick while `TimeControlMode` is fast-family. Covered by Trigger 2's step-down.
- On any campaign state change (war declared, settlement captured). Those fire the Modal event channel; they don't perturb the digest.
- On kingdom-feed additions. New Headline-tier **kingdom** items wait for the next 7-day edition.
- On personal-feed additions. Those go to the `YOU` section which rebuilds live on camp-hub-open — no digest regen needed.

### Implementation surface

Three new fields on `EnlistedMenuBehavior`:

```csharp
private string _cachedKingdomDigest = string.Empty;
private int _lastEditionDay = int.MinValue / 2;                     // sentinel per AGENTS.md pitfall #19
private CampaignTimeControlMode _lastTimeControlMode = CampaignTimeControlMode.Stop;
```

One refactored helper on `EnlistedNewsBehavior`:

```csharp
// Was: BuildDailyBriefSection() — rendered daily, only kingdom path was ever consumed conceptually
// Now: BuildKingdomDigestSection — pure-kingdom slice
public string BuildKingdomDigestSection(int windowDays = 7);
```

One new private method on `EnlistedMenuBehavior`:

```csharp
private void TryRegenerateKingdomDigest(TriggerReason reason);
```

`TriggerReason` is an internal enum (`MenuReentry`, `SpeedDown`, `PlayResidentBoundary`) used in `ModLogger.Info("INTERFACE", ...)` lines to make smoke debugging tractable.

The existing `_dailyBriefCompany` / `_dailyBriefUnit` + their SyncData lines + `_lastDailyBriefDayNumber` are deleted. `_dailyBriefKingdom`'s persisted form is also removed — the kingdom digest is an ephemeral cache, not save state. Save-load of an older save with the three `en_news_dailyBrief*` keys present is harmless (`SyncData` skips missing fields on the read side; unreferenced keys linger in the save payload until the next overwrite).

---

## Removals (stale-code cleanup) + moves

| Item | Current location | Disposition |
| :--- | :--- | :--- |
| `enlisted_headlines_entry` option | `EnlistedMenuBehavior.cs:928-945` | **Delete** — replaced by inline [NEW] in Camp `YOU` |
| `enlisted_headlines` submenu + `enlisted_headlines_back` | `:1203-1232` | **Delete** |
| `HEADLINES_HEADER_TEXT` / `HEADLINES_TEXT` text variables | inline | **Delete** |
| `FormatHeadlines` helper | `:1542-1555` | **Delete** — body text rendered inline |
| `CountUnreadHeadlines` helper | `:1537-1540` | **Delete** — no badge to count for |
| `MarkHeadlinesViewed` helper | `:1557-1567` | **Relocate** to a camp-hub-scoped mark-read call site; semantics unchanged |
| `GetUnreadHighSeverity` helper | `:1516-1535` | **Keep** — still needed to decide which items in `YOU` get [NEW] prefix |
| `_viewedHeadlineStoryKeys` field | `EnlistedMenuBehavior.cs` | **Keep** — still tracks mark-read across sessions |
| `BuildDailyBriefSection()` | `EnlistedNewsBehavior.cs:477-650` | **Refactor** to `BuildKingdomDigestSection(int)` — delete company/unit sub-builders that were never consumed |
| `EnsureDailyBriefGenerated()` | `EnlistedNewsBehavior.cs` (nearby) | **Refactor** to `EnsureKingdomDigestGenerated(int windowDays)` — daily → N-day boundary gate |
| `_dailyBriefCompany`, `_dailyBriefUnit` fields + SyncData lines | `:72-74, 269-275` | **Delete** — never rendered |
| `_lastDailyBriefDayNumber` + SyncData | `:66, 273` | **Delete** — ephemeral cache doesn't need persistence |
| `_dailyBriefKingdom` field + SyncData | `:74, 276` | **Delete** — renamed to `_cachedKingdomDigest` on `EnlistedMenuBehavior` (ephemeral) |
| Main body `KINGDOM REPORTS` section + `BuildKingdomNarrativeParagraph` | `EnlistedMenuBehavior.cs:3323-3330, :3361-3491` | **Replace** — section still exists under new `DISPATCHES` header; helper replaced by `BuildKingdomDigestSection` call |
| Main body `COMPANY REPORTS` section + `BuildCampNarrativeParagraph` | `:3332-3338` + helper | **Move** to Camp `CAMP ACTIVITIES` |
| Main body `PLAYER STATUS` section + `BuildPlayerNarrativeParagraph` | `:3341-3347` + helper | **Move** to Camp `YOU` |
| Camp body `UPCOMING` section + `BuildUpcomingSection` helper | `:1601-1609` + `:2319-2420` | **Move** to Main `UPCOMING` — helper body preserved intact |
| Camp body `SINCE LAST MUSTER` + `BuildPeriodRecapSection` | `:1591-1599` + `:2213-2312` | **Keep** — stays on Camp Hub |
| Camp body `RECENT ACTIVITY` + `BuildRecentActivitiesNarrative` | `:1611-1619` + helper | **Merge** into Camp `YOU` — helper content folded into the new YOU builder |
| Camp body `STATUS` + `BuildPlayerPersonalStatus` | `:1621-1628` + helper | **Merge** into Camp `YOU` — helper content folded |
| Camp body `COMPANY STATUS` + `BuildCompanyStatusSummary` | (nearby in `BuildCampHubText`) + `:1642-...` | **Merge** into Camp `CAMP ACTIVITIES` |

Helpers that survive with their call site moving: `BuildBriefPlayerRecap`, `BuildBriefPlayerForecast`, `BuildKingdomForecastLine`, `BuildCompanyForecastLine`, `BuildSupplyContextLine`, `BuildCasualtyReportLine`, `BuildRecentEventLine`, `BuildPendingEventsLine`, `BuildFlagContextLine`, `BuildRetinueContextLine`, `GetNCOTitle`, `GetOfficerTitle`, `BuildUpcomingSection`, `BuildPeriodRecapSection`. Their signatures stay; their callers update.

---

## Docs-sync scope

This refactor changes shipped surfaces documented in multiple places. All must be updated in the same change:

| Doc | What needs to change |
| :--- | :--- |
| `docs/Features/Content/home-surface.md` | References to `enlisted_status` body composition (if any touch Kingdom/Company/Player sections) |
| `docs/INDEX.md` | Feature-lookup rows referencing HEADLINES or the camp-body section list |
| `docs/Features/Core/order-progression-system.md` | Lines 838 + 856 + 962 describe `SINCE LAST MUSTER` + `UPCOMING` as shipped — confirm the spec preserves these contracts verbatim or update the doc if content shifts |
| `docs/Features/UI/news-reporting-system.md` | Extensively describes "Daily Brief" as shipped. Must be rewritten to describe the `DISPATCHES` weekly-edition flow, remove references to the orphaned `BuildDailyBriefSection`, and clarify the two-feed separation |
| `docs/Features/Core/muster-system.md` | If it cross-references news surfaces used by the ceremony |
| `docs/Features/Campaign/camp-life-simulation.md` | If it references camp-hub body sections by name |

Doc-update commit lands alongside the implementation commit(s), not as a separate pass.

---

## Scope boundaries

**In scope.**

- `enlisted_status` body rewrite (2 sections: weekly `DISPATCHES` + live `UPCOMING` preserving full contract).
- `enlisted_camp_hub` body rewrite (3 sections: live `YOU` with inline [NEW] markers + preserved `SINCE LAST MUSTER` + live `CAMP ACTIVITIES`).
- Delete HEADLINES option + submenu + helpers as enumerated above.
- Refactor `BuildDailyBriefSection` → `BuildKingdomDigestSection(int)` + delete the company/unit branches that were never consumed.
- Purge `_dailyBriefCompany/_dailyBriefUnit/_lastDailyBriefDayNumber/_dailyBriefKingdom` persisted state.
- Three-trigger kingdom-digest regen wired with correct `CampaignTimeControlMode` values.
- Docs sync across the six listed files.

**Out of scope.**

- `MusterMenuHandler` period-digest rendering — uses the same personal feed via `GetPersonalFeedSince(lastMusterDay)` but serves the pay-day ceremony as a distinct ritual. Unchanged.
- `CampRoutineProcessor.AddToNewsFeed` and other producer-side code. Producers keep feeding the same `AddRoutineOutcome` / `AddPersonalDispatch` APIs.
- Combat log widget (`EnlistedCombatLog`). Orthogonal surface, different lifecycle.
- Conversation/dialogue news hooks (QM gossip, lord chatter). Separate content problem.
- The producer-pattern infrastructure (`KingdomHeadlineFactProducer` + templates) documented in news-reporting-system.md — stays in place; the digest builder just consumes its output.
- Localization keys for new headers (`{=dispatches_header}`, `{=camp_you_header}`, `{=camp_activities_header}`). Authored during implementation, not designed here.

---

## Risks + mitigations

**Risk 1 — Player plays for 7+ real hours at 1× without opening any submenu.** The body text visibly changes once per week-of-play in this case. RP-acceptable (newspaper-handover metaphor); masthead shows the week-of date so an observant player sees why.

**Risk 2 — `CampaignTimeControlMode` value set.** The taxonomy above (fast-family vs play-family) is derived from the live decompile at `../Decompile/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignTimeControlMode.cs` plus the tick math at `Campaign.cs:843-875` (which bucket each value by the `num` assignment). Implementation must re-verify against the decompile at authoring time per AGENTS.md Critical Rule #1 — a Bannerlord patch could add/rename members. The bucketing rule is: values producing `num = 0` in `TickMapTime` are play-family (include `FastForwardStop` under this); values multiplying by `SpeedUpMultiplier` are fast-family.

**Risk 3 — Compiled-in 7-day edition length.** If playtest feedback prefers a 12-day cadence (muster-cycle aligned), it's a one-constant edit in `BuildKingdomDigestSection`'s window-days param + the masthead label. Not a re-design.

**Risk 4 — `int.MinValue` sentinel on `_lastEditionDay`.** AGENTS.md pitfall #19 documents the wrap-around hazard. Use `int.MinValue / 2` — arithmetic `currentDay - _lastEditionDay` on a fresh state yields ~1.07B, safely > 7.

**Risk 5 — Save-compat for removed `_dailyBrief*` SyncData keys.** Old saves carry the keys; the new code doesn't read them. Tale Worlds `IDataStore.SyncData` is tolerant of missing fields on read (sets to default), and unreferenced keys remain in the save blob until next overwrite (benign). No migration code needed.

**Risk 6 — Inline [NEW] marker visibility under dense prose.** If the YOU section has 3 recent personal items and the newest is headline-tier, the `NEW:` prefix renders inline mid-paragraph. At the designed ≤3-item limit this reads cleanly; at 5+ items it'd get noisy. Mitigation: cap at 3 items (already the spec) and render `NEW:` only on the most recent unread headline-tier item, not all of them. Older unread headline items still count for mark-read but don't get a prefix.

**Risk 7 — Mark-read timing.** Mark-read fires on camp-hub-open. If the player opens camp, glances, and backs out without scrolling to YOU, headline-tier items are already marked read. Acceptable — mirrors how the current HEADLINES submenu marks read on submenu-open, not on read-through.

---

## Validation

No validator rails changes — intra-C# surface refactor, no storylet authoring / effects / triggers / save offsets touched. Standard gates:

- `dotnet build -c "Enlisted RETAIL" /p:Platform=x64` passes.
- `python Tools/Validation/validate_content.py` passes.

In-game smoke checklist:

- Open `enlisted_status` fresh after enlisting → `DISPATCHES · Week of <date>` + `UPCOMING` render; no `HEADLINES` option visible.
- Open Camp hub → `YOU` (with any unread headline-tier personal dispatches prefixed `NEW:`) + `SINCE LAST MUSTER` + `CAMP ACTIVITIES` render; `UPCOMING` is gone (moved to main).
- Exit camp, return to main menu → if ≥7 in-game days crossed during the Camp visit, `DISPATCHES` body shows fresh content; else identical to before.
- Fast-forward 8 in-game days at 16× with `enlisted_status` resident → `DISPATCHES` stays frozen; press pause → digest regens to current edition.
- Sit at 1× with `enlisted_status` resident for 7+ in-game hours → `DISPATCHES` flips once on the tick covering the 7-day boundary.
- Trigger a headline-tier **personal** dispatch (e.g. via debug hotkey or combat outcome) → open Camp → `YOU` shows it with `NEW:` prefix; close camp and re-open → prefix is gone (marked read).
- Trigger a headline-tier **kingdom** dispatch mid-week → it does NOT appear in `DISPATCHES` until the next edition fires.
- `BuildDailyBriefSection` references produce a compile error (confirms all callers migrated).

No new save-definer offsets. No new validator phases. No new localization required beyond the few new header keys.

---

## Open questions

None locked in. Edition length (7 days) is the one tunable playtest might want to revisit — flagged in Risk 3. Lord-culture flavor on the masthead label (`Week of` → `Sennight` / `Fortnight` per culture) is deferred.

---

## Changelog vs v1

- Kingdom feed and personal feed are documented as independent and kept routed to separate sections.
- `HEADLINES` option functionality preserved as inline `NEW:` prefix in Camp `YOU`, not dropped.
- `SINCE LAST MUSTER` explicitly preserved as a Camp Hub section with its full 12-day recap contract.
- `UPCOMING` full forecast contract documented and preserved (commitment + order + world-state hint + schedule).
- Camp Hub body expanded from 2 sections to 3 (`YOU` / `SINCE LAST MUSTER` / `CAMP ACTIVITIES`).
- `CampaignTimeControlMode` enum used with real value names from the decompile (fast-family + play-family taxonomy explicit).
- Docs-sync scope widened to six files including `order-progression-system.md` and `news-reporting-system.md`.
- Explicit disposition table entries for every helper that moves, stays, or disappears.

## Changelog vs initial v2

- `FastForwardStop` reclassified from fast-family to play-family. Decompile at `Campaign.cs:872-874` puts it in the `num = 0` bucket alongside `Stop`; `GetSimplifiedTimeControlMode` also collapses it to `Stop` via its default arm. Fast-family is now specifically the three values that multiply `SpeedUpMultiplier` in `TickMapTime`.
- Risk 2 text expanded to include the bucketing rule (watch `num` in tick math, not enum name) so future verification doesn't trip on the same naming-versus-behavior mismatch.
