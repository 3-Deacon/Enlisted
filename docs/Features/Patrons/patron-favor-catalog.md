# Patron Favor Catalog

Living reference for the six favors a former lord (a "patron") may grant. Plan 6 populates the catalog; this doc is the canonical contract for behaviour, conditions, cooldowns, and the storylet IDs each favor fires.

**Source spec:** [Spec v6 §3.3 Roll of Patrons](../../superpowers/specs/2026-04-24-ck3-wanderer-systems-analysis.md).
**Plan:** [Plan 6](../../superpowers/plans/2026-04-24-ck3-wanderer-roll-of-patrons.md).
**Code:** `src/Features/Patrons/FavorKind.cs`, `PatronEntry.cs`, `PatronRoll.cs`, `PatronFavorResolver.cs`.

---

## Lifecycle context

A patron is a Lord the player has served and discharged from (mid-career — fired, captured, lord lost, faction-switch, grace-period). One `PatronEntry` is added per discharge; re-enlisting and re-discharging the same lord updates the existing entry (cumulative `DaysServed`, preserved cooldowns).

Patrons stay on the Roll until full retirement, at which point `PatronRoll.Clear()` empties the list — Enlisted explicitly trades CK3-style post-retirement persistence for a cleaner mod-silence boundary.

When a patron dies (`OnHeroKilled`), their entry's `IsDead` flag flips. Dead patrons remain on the Roll (greyed in audience flow) but cannot grant favors.

---

## How a favor is granted

`PatronFavorResolver.TryGrantFavor(entry, kind, out refusalReason)` is the single grant point. Flow:

1. Resolve the `Hero` from `entry.HeroId` via `MBObjectManager`. If null or `!IsAlive`, refuse with `{=patron_dead}`.
2. Check per-favor cooldown via `entry.IsFavorAvailable(kind, CampaignTime.Now)`. If active, refuse with `{=patron_cooldown}`.
3. Check kind-specific conditions (relation threshold, patron gold, party space, runtime availability of dependent systems).
4. If granted: stamp the cooldown on the entry, fire the matching outcome storylet via `ModalEventBuilder.FireSimpleModal(...)`, return `true`.
5. If refused: return `false` with a localized reason; the caller (`AddPatronDialogs`) shows the reason inline.

Per-favor cooldowns are stored as a CSV string on `PatronEntry.PerFavorCooldownsCsv` (`"<kindId>:<ticks>;<kindId>:<ticks>"`) — `Dictionary<FavorKind, CampaignTime>` is not a saveable container shape (CLAUDE.md known issue #14), so the entry serialises a flat string and the helper methods parse it on access.

---

## Catalog

### LetterOfIntroduction (`FavorKind = 1`)

A flattering letter to a peer in the patron's faction or court. Opens minor renown gain and a weak hook for a third-party introduction.

| Field | Value |
| :-- | :-- |
| Cooldown | 30 days |
| Conditions | Patron alive; relation ≥ 0 |
| Storylet | `patron.letter_of_introduction` |
| Outcome (typical) | +5 renown; flag `patron.letter.<lord_id>` set; storylet flavour describes the introduction target |
| Refusal text | `{=patron_relation_low}` if relation < 0 |

### GoldLoan (`FavorKind = 2`)

A sizeable loan (5,000 denars) at the cost of relation drift and a debt flag tracked for future repayment storylets.

| Field | Value |
| :-- | :-- |
| Cooldown | 60 days |
| Conditions | Patron alive; relation ≥ 10; patron `Hero.Gold ≥ 5000` |
| Storylet | `patron.gold_loan` |
| Outcome (typical) | `give_gold 5000` (routes through `GiveGoldAction` per AGENTS Rule #3); relation -3 (`patron_relation_drift_minor_negative` scripted effect); debt flag `patron.debt.<lord_id>` set with amount |
| Refusal text | `{=patron_relation_low}` if relation < 10; `{=patron_no_gold}` if patron lacks gold |

### TroopLoan (`FavorKind = 3`)

The patron lends 1-2 high-tier knights to the player's MainParty for a limited tour. Knights auto-depart after the tour expires via a scheduled `RemoveCompanionAction.ApplyByFire`.

| Field | Value |
| :-- | :-- |
| Cooldown | 90 days |
| Conditions | Patron alive; relation ≥ 20; player `MainParty` has at least 2 free hero slots |
| Storylet | `patron.troop_loan` |
| Outcome (typical) | 1-2 knight troops added to MainParty; auto-removal scheduled at +7 days; relation -2 |
| Refusal text | `{=patron_relation_low}` if relation < 20; `{=patron_party_full}` if no slots free |

### AudienceArrangement (`FavorKind = 4`)

The patron arranges a meeting with a third lord in their orbit (clan member, peer, vassal). Sets a one-shot flag the audience flow consumes on next encounter.

| Field | Value |
| :-- | :-- |
| Cooldown | 45 days |
| Conditions | Patron alive; relation ≥ 5 |
| Storylet | `patron.audience` |
| Outcome (typical) | Flag `patron.audience.pending.<target_lord_id>` set with expiry; player meets the named lord with +5 relation seed on next encounter |
| Refusal text | `{=patron_relation_low}` if relation < 5 |

### MarriageFacilitation (`FavorKind = 5`)

The patron introduces the player to an eligible marriage candidate from their clan or court. Outcome is a storylet branch — actual marriage flow remains the player's later choice.

| Field | Value |
| :-- | :-- |
| Cooldown | 180 days |
| Conditions | Patron alive; relation ≥ 30; player is unmarried |
| Storylet | `patron.marriage` |
| Outcome (typical) | Flag `patron.marriage.candidate.<lord_id>` set; named candidate appears as a follow-up audience or visit option |
| Refusal text | `{=patron_relation_low}` if relation < 30; `{=patron_already_married}` if player has a spouse |

### AnotherContract (`FavorKind = 6`)

The patron offers a high-priority contract — typically a bounty, escort, or scout duty against a named foe. Spawns a `ContractActivity` (Plan 5) with patron-flavoured origin.

| Field | Value |
| :-- | :-- |
| Cooldown | 30 days |
| Conditions | Patron alive; relation ≥ 0; **`EndeavorRunner.Instance != null`** (Plan 5 runtime loaded) |
| Storylet | `patron.another_contract` |
| Outcome (typical) | `ContractActivity` spawned with `patron_referral` origin metadata; payout adjusted by patron generosity |
| Refusal text | Hidden from the favor menu entirely if Plan 5 isn't loaded (graceful degradation per Plan 6 Lock 1) |

---

## Cooldown encoding

`PatronEntry.PerFavorCooldownsCsv` example after Letter and Gold favors granted on day 0:

```text
1:62208000000;2:124416000000
```

- `1` = `(int)FavorKind.LetterOfIntroduction`; `62208000000` = `CampaignTime.DaysFromNow(30).NumTicks`
- `2` = `(int)FavorKind.GoldLoan`; `124416000000` = `CampaignTime.DaysFromNow(60).NumTicks`

`PatronEntry.GetCooldown(FavorKind)` parses; `SetCooldown(kind, expiry)` updates the matching segment in-place. Cooldowns expire by simple `<= CampaignTime.Now` comparison.

---

## Refusal reasons (loc keys)

| Loc key | Trigger |
| :-- | :-- |
| `{=patron_dead}` | `MBObjectManager.GetObject<Hero>(HeroId)` returns null or `!IsAlive` |
| `{=patron_cooldown}` | `IsFavorAvailable` returns false |
| `{=patron_relation_low}` | Per-kind relation threshold not met |
| `{=patron_no_gold}` | Patron `Hero.Gold` below floor (GoldLoan only) |
| `{=patron_party_full}` | Player MainParty hero slots full (TroopLoan only) |
| `{=patron_already_married}` | Player has a spouse (MarriageFacilitation only) |

These map to short Inquiry popups when a favor refuses; tooltips in the audience flow surface the same text.
