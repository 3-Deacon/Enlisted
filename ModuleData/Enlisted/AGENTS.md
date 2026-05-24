# AGENTS.md — ModuleData/Enlisted

> Parent: [../../AGENTS.md](../../AGENTS.md)
> Handbook: [../../docs/Features/Content/](../../docs/Features/Content/) (content authoring lives in Content handbook)

This file owns content-authoring rules: JSON field order, tooltip rules, inline localization, and `Enlisted.csproj` AfterBuild parity. Source-of-truth for any file under `ModuleData/Enlisted/`.

---

## JSON Field Order — fallback immediately after ID

```json
{ "titleId": "key", "title": "Fallback", "setupId": "key2", "setup": "Text" }
```

---

## Event Tooltips Required

All options need tooltips (<80 chars). Format: action + effects + cooldown.

---

## Localization — inline `{=key}Fallback`

Storylet/event JSON authors loc-keys inline as `{=key_id}Fallback Text` in `title` / `setup` / `options[].text` / `options[].tooltip` fields, NOT as the legacy Event schema's separate `titleId`+`title` pairs. The game falls back to inline text when a key is missing from `ModuleData/Languages/enlisted_strings.xml`, so missing keys only affect translators (zero runtime impact).

After authoring, run `python3 Tools/Validation/sync_event_strings.py` and integrate the generated XML.

---

## `Enlisted.csproj` AfterBuild parity — authoring discipline

Adding a new content directory under `ModuleData/Enlisted/` requires three additions to `Enlisted.csproj`:

1. An `<XxxData Include="ModuleData\Enlisted\Xxx\*.json"/>` ItemGroup
2. A matching `<MakeDir Directories="$(OutputPath)..\..\ModuleData\Enlisted\Xxx"/>` inside `AfterBuild`
3. A `<Copy SourceFiles="@(XxxData)" DestinationFolder="...\Xxx\"/>` step

Missing any of the three = content silently not deployed to the game install. Runtime loaders log `Expected("XXX", "no_xxx_dir", "directory not found: ...")` at info level, so the failure is easy to miss.

Pattern at `Enlisted.csproj:614-671` (ItemGroups) and `:728-745` (AfterBuild).

**Tooling note:** `validate_content.py` does NOT currently enforce csproj↔ModuleData parity — `content_dirs_to_check` at `Tools/Validation/validate_content.py:1475` is empty. Adding enforcement is a follow-up TODO. Until then, this is authoring discipline.

---

## `Enlisted.csproj` wildcards are NON-RECURSIVE

**`Enlisted.csproj` wildcards are NON-RECURSIVE.** `<Compile Include="src\Features\Activities\*.cs"/>` (line 390) and similar patterns only match files directly in the named directory — they do NOT match subfolders. A file at `src\Features\Activities\Home\Foo.cs` needs an explicit `<Compile Include>` line even though `Activities\*.cs` exists. Before creating a new `.cs` file, grep `Enlisted.csproj` for a wildcard that covers your exact directory (not a parent).

---

## See also

- [../../src/Features/Content/AGENTS.md](../../src/Features/Content/AGENTS.md) — runtime rules for storylets/events
- [../../docs/Features/Content/storylet-backbone.md](../../docs/Features/Content/storylet-backbone.md) — living reference
- [../../docs/Features/Content/writing-style-guide.md](../../docs/Features/Content/writing-style-guide.md) — voice and tone
- [Effects/scripted_effects.json](Effects/scripted_effects.json) — seed catalog for scripted-effect ids
