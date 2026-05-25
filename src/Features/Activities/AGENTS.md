# AGENTS.md — Features/Activities

> Parent: [../../../AGENTS.md](../../../AGENTS.md)
> Handbook: [../../../docs/Features/Activities/](../../../docs/Features/Activities/) (folder stubbed by Task A18)

This file owns Activities runtime rules. Activities are stateful ticking C# objects with intent-biased phase pools (Banner-Kings Feast pattern). Subdirs covered:
- `Home/` — Home surface activities
- `Orders/` — Orders-surface activities (legacy `src/Features/Orders/` was retired 2026-04-21; this is the current Orders owner)

---

## Pitfall: Writing to a read-only quality from a storylet effect

Writing to a read-only quality (`rank`, `days_in_rank`, `days_enlisted`)
from a storylet effect. `validate_content.py` Phase 12 blocks this at
build time. Rank advancement routes through `EnlistmentBehavior.SetTier`
as the outcome of a promotion-ceremony chain, not a raw quality write.

---

## Pitfall: `int.MinValue` throttle sentinel overflow

`int.MinValue` as a "never fired" sentinel for throttle fields overflows
when subtracted: `nowHour - int.MinValue` goes negative and trips
`diff >= interval` checks backwards, so either the throttle never gates
OR it always gates, depending on comparison direction. Use
`int.MinValue / 2` as the sentinel — gives plenty of headroom and keeps
arithmetic well-defined. Known-bitten fields: `_lastHeartbeatHourTick`,
`_lastTransitionBeatHourTick`, `_lastHourlyHeartbeatTick` in the
Orders-surface tick behaviors.

---

## Orders news-feed throttle (cross-ref to Content)

`OrdersNewsFeedThrottle.TryClaim()` rejects at extreme fast-forward speeds. Intentional silence at extreme fast-forward. Full pitfall in [../Content/AGENTS.md](../Content/AGENTS.md#pitfall-news-feed-throttle-silent-at-4x-speed). Tick-driven `ModLogger` entries (DRIFT, DUTYPROFILE heartbeats, PATH heartbeats) still log at any speed.

API note: `Campaign.Current.SpeedUpMultiplier` is a `float` property (default `4f`). The implementation tests only this property: `Campaign.Current?.SpeedUpMultiplier > 4f`. Do NOT add a `TimeControlMode == StoppableFastForward` guard — that would miss the `UnstoppableFastForward` case and contradict the implementation. Full pitfall and corrected example in [../Content/AGENTS.md](../Content/AGENTS.md#pitfall-news-feed-throttle-silent-at-4x-speed).

---

## Legacy Orders folder retired

The directory `src/Features/Orders/` was deleted on 2026-04-21 (commit `a8719bb`). Orders code now lives under `src/Features/Activities/Orders/` as an Activity subtype. Old plan archived at [docs/superpowers/plans/archive/2026-04-20-orders-surface.md](../../../docs/superpowers/plans/archive/2026-04-20-orders-surface.md).

---

## See also

- [../Content/AGENTS.md](../Content/AGENTS.md) — storylet backbone these activities consume
- [../../../docs/Features/Content/career-loop.md](../../../docs/Features/Content/career-loop.md) — career-loop integration spec
- [../../../docs/superpowers/specs/archive/2026-04-21-plans-integration-design.md](../../../docs/superpowers/specs/archive/2026-04-21-plans-integration-design.md) — five-plan integration roadmap
