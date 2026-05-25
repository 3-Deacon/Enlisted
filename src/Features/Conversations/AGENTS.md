# AGENTS.md — Features/Conversations

> Parent: [../../../AGENTS.md](../../../AGENTS.md)
> Handbook: [../../../docs/Features/Conversations/](../../../docs/Features/Conversations/) (folder stubbed by Task A18; populate as design content emerges)

This file owns rules for conversation/dialog wiring: the token-interpolation discipline, the six required tokens, and the wiring vs authored-content gap that bites if you do one without the other. Source-of-truth for any file under `src/Features/Conversations/` and for any dialog/conversation-firing flow elsewhere.

---

## Token interpolation discipline

Authoring conversation / dialog content WITHOUT the standard token
interpolation tokens (`{PLAYER_NAME}`, `{PLAYER_RANK}`, `{LORD_NAME}`)
silently strips the mod's per-kingdom rank work. `RankHelper.GetCurrentRank`
reads `progression_config.json` for culture-specific rank titles
(Vlandian "Sergeant" vs Sturgian / Khuzait / Aserai / Battanian / Imperial
native rank names); a static "soldier" or "sergeant" written into JSON
catalogs ignores all of it. The QM precedent (`qm_gates.json`,
`qm_intro.json`) uses `{PLAYER_NAME}` and `{PLAYER_RANK}` on every line.
Token resolution requires `MBTextManager.SetTextVariable` calls before
the conversation opens; `EnlistedDialogManager.SetCommonDialogueVariables`
(private) handles QM, `EnlistedMenuBehavior.SetCompanionConversationTokens`
handles Plan 2 companions, and `EnlistedDialogManager.SetPatronConversationVariables(Hero patron)`
handles Plan 6 patron dialogs (sets `PATRON_NAME`). New dialog-firing flows must populate the
same six tokens (`PLAYER_NAME`, `PLAYER_RANK`, `LORD_NAME`, `PLAYER_TIER`,
plus speaker-scoped tokens like `COMPANION_NAME` /
`COMPANION_FIRST_NAME`) before opening the conversation, and the
authored content must reference them — only doing one or the other
(wiring without token usage, or token usage without wiring) leaves the
dialog flat or with literal `{PLAYER_RANK}` strings displayed. Plan 2
Phase 5++ (commit `4dfe719`) shipped the wiring + content rewrite for
the six companion catalogs after this gap was caught in code review.

Plan 6 owns the `PATRON_NAME` token. It is set in two places: (1) by
`SetPatronConversationVariables` in `EnlistedDialogManager.cs`, called
from `PatronAcknowledgeCondition` and `PatronFavorOptionCondition` for the
dialog-layer flow; and (2) by `PatronFavorResolver.TryGrantFavor` before
`FireDecisionOutcome` for the storylet-pipeline flow. Both call sites must
stay in sync — a patron dialog that fires without `SetPatronConversationVariables`
will display a literal `{PATRON_NAME}` token.

---

## See also

- [../../../docs/Features/Companions/companion-archetype-catalog.md](../../../docs/Features/Companions/companion-archetype-catalog.md) — companion dialog catalogs (Plan 2 reference)
- [../../../ModuleData/Enlisted/Dialogue/](../../../ModuleData/Enlisted/Dialogue/) — authored dialog content
