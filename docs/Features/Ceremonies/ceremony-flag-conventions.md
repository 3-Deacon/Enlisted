# Ceremony Flag Conventions

**Owning plan:** [Plan 3 — Rank-Ceremony Arc](../../superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc.md).

**Store:** `Enlisted.Features.Flags.FlagStore` (global, bool-only, `CampaignTime` expiry).

This doc records the choice-memory flag conventions ceremony storylets use. All flags are global (not hero-scoped), permanent (`CampaignTime.Never` expiry), and queryable via `FlagStore.Instance.Has(name)`.

**Note on store choice.** `FlagStore` only stores booleans. There is no string-flag store. To remember "which option the player picked at ceremony N," each option sets its own bool flag — the choice identity is encoded in the flag name itself, not in a string value.

---

## 1. Flag-name patterns

| Pattern | Type | When set | Purpose |
| :-- | :-- | :-- | :-- |
| `ceremony_fired_t{N}` | bool | Set by every option's effects when the player picks it. | Dedup. `CeremonyProvider.FireCeremonyForTier(N)` short-circuits if set. |
| `ceremony_choice_t{N}_<choice_id>` | bool | Set by the picked option's effects. | Choice memory. `<choice_id>` is the storylet option's `id` field. Plans 4-7 read these to gate or flavor downstream content. |
| `ceremony_culture_variant_t{N}_<suffix>` | bool | Set by the option's effects, optional. | Records which cultural variant fired (Vlandian / Sturgian / Imperial / base). Replay-analysis only; no game logic reads this for v1. |

All flag names are flat underscore per architecture brief §4 rule 6 — no dotted notation.

`{N}` is always the **destination tier** (e.g. T2 ceremony fires at the T1→T2 transition, sets `ceremony_fired_t2`).

---

## 2. Flag-set sequence per ceremony

A typical ceremony option's effect list ends with these three flag sets:

```json
{ "apply": "set_flag", "name": "ceremony_choice_t2_trust_sergeant" },
{ "apply": "set_flag", "name": "ceremony_fired_t2" }
```

The dedup flag is set last so that a partial failure mid-effect-chain doesn't leave the dedup gate engaged without choice memory.

---

## 3. Example: full T1→T9 career playthrough

Suppose the player picks one option per ceremony across the five retained transitions:

| Ceremony | Picked option | Flags set after pick |
| :-- | :-- | :-- |
| T1→T2 | `trust_sergeant` | `ceremony_choice_t2_trust_sergeant`, `ceremony_fired_t2` |
| T2→T3 | `frugal` | `ceremony_choice_t3_frugal`, `ceremony_fired_t3` |
| T4→T5 | `obey` | `ceremony_choice_t5_obey`, `ceremony_fired_t5` |
| T6→T7 | `humble_accept` | `ceremony_choice_t7_humble_accept`, `ceremony_fired_t7` |
| T7→T8 | `compromise` | `ceremony_choice_t8_compromise`, `ceremony_fired_t8` |

After the full arc, `FlagStore` holds 10 ceremony flags. A consumer (e.g. Plan 5 Endeavor System) can query the player's character history with:

```csharp
var trustsAuthority = FlagStore.Instance.Has("ceremony_choice_t2_trust_sergeant")
                   || FlagStore.Instance.Has("ceremony_choice_t5_obey");
var humble = FlagStore.Instance.Has("ceremony_choice_t7_humble_accept");
```

No ceremony fires at T4 / T6 / T9 — `PathCrossroadsBehavior` already fires Modal storylets at those tiers; ceremonies were intentionally skipped to avoid back-to-back modals (Plan 3 §4.7 lock 1). PathCrossroads writes its own flag set (`committed_path_<id>`, `path_resisted_<id>`) — see `EffectExecutor.DoCommitPath` / `DoResistPath`.

---

## 4. Persistence

`FlagStore` is `[Serializable]` and persists via `FlagStoreBehavior.SyncData`. Ceremony flags survive save/reload natively. No save-definer offset is consumed by ceremonies — they ride on Plan 0's existing `FlagStore` registration.

`FlagStore.EnsureInitialized()` reseats null dictionary fields after load (CLAUDE.md known footgun #4). Older saves that predate ceremony flags simply have no flags set; `Has(name)` returns false; no NRE.

---

## 5. Counter-pattern — what NOT to do

❌ **Do not store choice as a string value.**

```csharp
// WRONG — FlagStore has no string store
FlagStore.Instance.Set("ceremony_t2_choice", "trust_sergeant", expiry);
```

```csharp
// CORRECT — bool per choice
FlagStore.Instance.Set("ceremony_choice_t2_trust_sergeant", CampaignTime.Never);
```

❌ **Do not use dotted notation.**

```json
{ "apply": "set_flag", "name": "ceremony.fired.t2" }   // WRONG
{ "apply": "set_flag", "name": "ceremony_fired_t2" }   // CORRECT
```

❌ **Do not set the dedup flag from `CeremonyProvider` directly before firing the modal.**

```csharp
// WRONG — players who close the modal mid-air without picking are locked out
FlagStore.Instance.Set("ceremony_fired_t" + newTier, CampaignTime.Never);
ModalEventBuilder.FireCeremony(...);
```

```csharp
// CORRECT — let the option's effects set the dedup flag, so close-without-pick retries later
ModalEventBuilder.FireCeremony(...);
```

---

## 6. References

- [Plan 3 — Rank-Ceremony Arc](../../superpowers/plans/2026-04-24-ck3-wanderer-rank-ceremony-arc.md)
- [Architecture brief §4 rule 6](../../architecture/ck3-wanderer-architecture-brief.md) — flat underscore namespace
- [Ceremony storylet schema](ceremony-storylet-schema.md) — sibling doc
- `src/Features/Flags/FlagStore.cs` — bool-only store; `Has(name)`, `Set(name, expiry)`, `Clear(name)`
- `src/Features/Content/EffectExecutor.cs` — `set_flag` primitive (line 192)
