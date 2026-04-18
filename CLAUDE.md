# CLAUDE.md

Claude Code project memory for the Enlisted Bannerlord mod.

## Primary instructions

Universal rules, commands, patterns, pitfalls, and the docs map live in `AGENTS.md` — shared across all AI coding tools (Claude Code, Codex, Cursor, Copilot, Aider, etc.). Claude Code loads it automatically via this import:

@AGENTS.md

Everything below is **Claude-specific** and layers on top of AGENTS.md. Read AGENTS.md first.

---

## Context7 MCP Library IDs

For third-party library docs, use the Context7 MCP with these IDs:

| Library | Context7 ID |
| :--- | :--- |
| Harmony | `/pardeike/harmony` |
| Newtonsoft.Json | `/jamesnk/newtonsoft.json` |
| C# Language | `/websites/learn_microsoft_en-us_dotnet_csharp` |
| Pydantic AI | `/pydantic/pydantic-ai` |

**TaleWorlds APIs:** NEVER use Context7, web search, or training knowledge. Always use local `Decompile/` — it is the only authoritative reference for v1.3.13.

---

## MCP Server Usage

- **Context7** — Third-party library docs only (Harmony, Newtonsoft). Not for TaleWorlds APIs.
- **Microsoft Learn** — Use for .NET Framework 4.7.2 and C# language questions Context7 doesn't cover.
- **Playwright** — UI testing if/when a browser-facing tool is added; not applicable to the mod itself.
- **Cloudflare / Gmail / Google Drive / Google Calendar** — Not relevant to this project; ignore.

---

## Recommended Skills

Match the task to the right skill:

| Task | Skill |
| :--- | :--- |
| Reviewing a PR | `code-review:code-review` |
| Before proposing a bug fix | `superpowers:systematic-debugging` |
| Before claiming work done | `superpowers:verification-before-completion` |
| New feature implementation | `superpowers:test-driven-development` |
| Writing an implementation plan | `superpowers:writing-plans` |
| Security review of the branch | `security-review` |
| Updating this CLAUDE.md file | `claude-md-management:revise-claude-md` |
| Reducing permission prompts | `fewer-permission-prompts` |

---

## Session-Specific Guidance

- Shell is bash on Windows — use Unix paths (`/dev/null`, forward slashes), not `NUL` or backslashes
- For broad codebase exploration (>3 searches), spawn an `Explore` subagent rather than searching directly
- Parallelize independent Agent / tool calls; serialize only when one result feeds the next
- If the user needs to run an interactive command, suggest the `!` prefix so output lands in-context

---

## Help and feedback

- `/help` — Get help with Claude Code
- Feedback: <https://github.com/anthropics/claude-code/issues>
