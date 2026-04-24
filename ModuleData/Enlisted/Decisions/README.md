# Decisions/

Decision JSON backs player-invoked camp activity choices. These are loaded by
`EventCatalog`, indexed by `DecisionCatalog`, and surfaced through the native
Camp menu.

## Current Flow

1. Player opens `Enlisted Status`.
2. Player opens `Camp`.
3. Player opens `Available Activities`.
4. A scheduled/logistics activity opens a native activity detail menu.
5. The selected option resolves inline through `EventDeliveryManager` so effects,
   cooldowns, and news outcomes stay shared with event delivery.

Routine camp decisions should not queue modal popups. Reserve modal delivery for
true blocking story events routed through `StoryDirector`.

## Files

| File | Purpose |
| --- | --- |
| `camp_opportunities.json` | Scheduled camp opportunities and their target decision IDs. |
| `camp_decisions.json` | Training, social, rest, logistics, and camp-life decisions. |
| `decisions.json` | General player-initiated decisions still used by camp/opportunity routing. |
| `medical_decisions.json` | Medical and recovery choices shown as camp activities when eligible. |

## Authoring Rules

- Use `dec_` IDs for player-initiated camp decisions.
- Keep fallback text immediately after each localization ID.
- Add tooltips to every option.
- Use cancel-like option IDs (`cancel`, `not_now`, `back`, `decline`, etc.) for
  choices that should not commit a decision cooldown.
