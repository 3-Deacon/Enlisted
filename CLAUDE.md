# CLAUDE.md

Claude Code project memory for the Enlisted Bannerlord mod. Authoritative rules for all AI tools live in AGENTS.md and its nested cascade (`src/Features/Content/AGENTS.md`, etc.). This file holds Claude-specific extras only.

@AGENTS.md

---

## Current project status

Lives at [docs/superpowers/STATUS.md](docs/superpowers/STATUS.md). Loaded by both Codex (via AGENTS.md cascade) and Claude Code.

---

## Recommended Skills

| Task | Skill |
| :--- | :--- |
| Reviewing a PR | `code-review:code-review` |
| Before proposing a bug fix | `superpowers:systematic-debugging` |
| Before claiming work done | `superpowers:verification-before-completion` |
| New feature implementation | `superpowers:test-driven-development` |
| Before designing any feature | `superpowers:brainstorming` |
| Writing an implementation plan | `superpowers:writing-plans` |
| Executing a multi-task plan | `superpowers:subagent-driven-development` |
| Security review of the branch | `security-review` |
| Updating this CLAUDE.md file | `claude-md-management:revise-claude-md` (small targeted edits) or `claude-md-management:claude-md-improver` (full audit + improve) |
| Reducing permission prompts | `fewer-permission-prompts` |

---

## MCP Server Usage

- **Context7** — Third-party library docs only. IDs: Harmony `/pardeike/harmony`, Newtonsoft.Json `/jamesnk/newtonsoft.json`, C# `/websites/learn_microsoft_en-us_dotnet_csharp`. NOT for TaleWorlds APIs (use the local Decompile per AGENTS.md Critical Rule #1).
- **Microsoft Learn** — .NET Framework 4.7.2 and C# language questions Context7 doesn't cover.
- **Playwright** — UI testing if/when a browser-facing tool is added; not applicable to the mod itself.
- **Cloudflare / Gmail / Google Drive / Google Calendar / any other ambient MCP servers** — Not relevant; ignore.
