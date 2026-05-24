#!/usr/bin/env python3
"""Phase 21 — Docs structure mirror validator.

Enforces the docs+AGENTS.md restructure design
(docs/superpowers/specs/2026-05-24-docs-and-agents-md-restructure-design.md).

Fail-closed checks (return 1):
  1. Every nested AGENTS.md (anywhere except root) has a sibling CLAUDE.md
     shim that contains @AGENTS.md.
  2. Every src/Features/<X>/AGENTS.md has a corresponding docs/Features/<X>/
     folder.
  3. Every @import path in CLAUDE.md, GEMINI.md, and any nested CLAUDE.md shim
     resolves to an existing file.
  4. Cross-language reference scan: *.cs, *.json, *.csproj, *.xml, *.md files
     anywhere in the repo MUST NOT reference moved/deleted doc paths from the
     STALE_PATHS list below. (List is updated each rename phase.)

Warning checks (do not fail, report count):
  5. Every nested AGENTS.md uses the cross-link template at the top
     (Parent + Handbook links).
  6. Every docs/Features/<X>/ folder has an index.md.
  7. docs/INDEX.md lists every nested AGENTS.md path.
  8. AGENTS.md files >300 lines (signal to split or shed).

Usage:
  python3 Tools/Validation/validate_docs_structure.py        # run all checks
  python3 Tools/Validation/validate_docs_structure.py --json # JSON output for CI
"""
from __future__ import annotations
import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

# Stale paths from prior rename phases. Phase F/G updates this list as renames land.
# Format: each entry is a substring; if found in any tracked file outside Archive/,
# the file's reference is stale and must be repaired.
STALE_PATHS: list[str] = [
    # Populated by Phase F sub-commits. Empty after Phase A; grows during rename
    # phases. Phase G clears entries as their underlying paths are fully repaired.
]

NESTED_AGENTS_PATHS = [
    "src/Features/Content/AGENTS.md",
    "src/Mod.Core/SaveSystem/AGENTS.md",
    "ModuleData/Enlisted/AGENTS.md",
    "Tools/Validation/AGENTS.md",
    "src/Features/Conversations/AGENTS.md",
    "src/Features/Activities/AGENTS.md",
    "docs/superpowers/AGENTS.md",
    "Tools/AGENTS.md",
]


@dataclass
class Result:
    failures: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def fail(self, msg: str) -> None:
        self.failures.append(msg)

    def warn(self, msg: str) -> None:
        self.warnings.append(msg)


def check_shim_siblings(result: Result) -> None:
    """Check 1: every nested AGENTS.md has a sibling CLAUDE.md with @AGENTS.md."""
    for rel in NESTED_AGENTS_PATHS:
        agents = REPO_ROOT / rel
        if not agents.exists():
            result.fail(f"Missing nested AGENTS.md: {rel}")
            continue
        shim = agents.parent / "CLAUDE.md"
        if not shim.exists():
            result.fail(f"Missing CLAUDE.md shim sibling: {shim.relative_to(REPO_ROOT)}")
            continue
        content = shim.read_text(encoding="utf-8")
        if "@AGENTS.md" not in content:
            result.fail(
                f"CLAUDE.md shim missing @AGENTS.md import: {shim.relative_to(REPO_ROOT)}"
            )


def check_features_mirror(result: Result) -> None:
    """Check 2: every src/Features/<X>/AGENTS.md has docs/Features/<X>/ folder."""
    features_src = REPO_ROOT / "src" / "Features"
    if not features_src.exists():
        return
    for agents_file in features_src.glob("*/AGENTS.md"):
        subsystem = agents_file.parent.name
        docs_folder = REPO_ROOT / "docs" / "Features" / subsystem
        if not docs_folder.exists():
            result.fail(
                f"Mirror missing: {agents_file.relative_to(REPO_ROOT)} has no "
                f"matching docs/Features/{subsystem}/ folder"
            )


_IMPORT_RE = re.compile(r"^\s*@([^\s]+)\s*$", re.MULTILINE)


def check_imports_resolve(result: Result) -> None:
    """Check 3: @import paths in CLAUDE.md / GEMINI.md / nested shims resolve."""
    candidates = [
        REPO_ROOT / "CLAUDE.md",
        REPO_ROOT / ".gemini" / "GEMINI.md",
    ]
    candidates.extend(
        (REPO_ROOT / p).parent / "CLAUDE.md" for p in NESTED_AGENTS_PATHS
    )
    for f in candidates:
        if not f.exists():
            continue
        text = f.read_text(encoding="utf-8")
        for m in _IMPORT_RE.finditer(text):
            import_path = m.group(1)
            target = (f.parent / import_path).resolve()
            if not target.exists():
                result.fail(
                    f"Broken @import in {f.relative_to(REPO_ROOT)}: "
                    f"@{import_path} -> {target} does not exist"
                )


def check_stale_refs(result: Result) -> None:
    """Check 4: no live references to moved/deleted doc paths."""
    if not STALE_PATHS:
        return
    extensions = {".cs", ".json", ".csproj", ".xml", ".md"}
    skip_dirs = {".git", "bin", "obj", "Decompile", ".worktrees", "Archive"}
    for path in REPO_ROOT.rglob("*"):
        if not path.is_file() or path.suffix not in extensions:
            continue
        if any(part in skip_dirs for part in path.parts):
            continue
        try:
            text = path.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for stale in STALE_PATHS:
            if stale in text:
                rel = path.relative_to(REPO_ROOT)
                result.fail(f"Stale reference in {rel}: {stale!r}")


_PARENT_LINK_RE = re.compile(r"Parent:\s*\[.*?AGENTS\.md\]")
_HANDBOOK_LINK_RE = re.compile(r"Handbook:\s*\[")


def check_cross_link_templates(result: Result) -> None:
    """Check 5: every nested AGENTS.md has Parent + Handbook lines near top."""
    for rel in NESTED_AGENTS_PATHS:
        f = REPO_ROOT / rel
        if not f.exists():
            continue
        head = "\n".join(f.read_text(encoding="utf-8").splitlines()[:10])
        if not _PARENT_LINK_RE.search(head):
            result.warn(f"{rel}: missing 'Parent: [...AGENTS.md]' link near top")
        if not _HANDBOOK_LINK_RE.search(head):
            result.warn(f"{rel}: missing 'Handbook: [...]' link near top")


def check_docs_features_indexes(result: Result) -> None:
    """Check 6: every docs/Features/<X>/ folder has index.md."""
    features_docs = REPO_ROOT / "docs" / "Features"
    if not features_docs.exists():
        return
    for sub in features_docs.iterdir():
        if not sub.is_dir():
            continue
        if not (sub / "index.md").exists():
            result.warn(f"{sub.relative_to(REPO_ROOT)}: missing index.md")


def check_index_catalogues_nested(result: Result) -> None:
    """Check 7: docs/INDEX.md lists every nested AGENTS.md path."""
    index = REPO_ROOT / "docs" / "INDEX.md"
    if not index.exists():
        result.warn("docs/INDEX.md missing")
        return
    text = index.read_text(encoding="utf-8")
    for rel in NESTED_AGENTS_PATHS:
        if rel not in text:
            result.warn(f"docs/INDEX.md does not catalog: {rel}")


def check_agents_size_budget(result: Result) -> None:
    """Check 8: warn if any AGENTS.md >300 lines."""
    root_paths = [REPO_ROOT / "AGENTS.md"]
    root_paths.extend(REPO_ROOT / p for p in NESTED_AGENTS_PATHS)
    for f in root_paths:
        if not f.exists():
            continue
        n = sum(1 for _ in f.open(encoding="utf-8"))
        if n > 300:
            result.warn(f"{f.relative_to(REPO_ROOT)}: {n} lines (>300, consider splitting)")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", action="store_true", help="Emit JSON output")
    args = parser.parse_args()

    result = Result()
    check_shim_siblings(result)
    check_features_mirror(result)
    check_imports_resolve(result)
    check_stale_refs(result)
    check_cross_link_templates(result)
    check_docs_features_indexes(result)
    check_index_catalogues_nested(result)
    check_agents_size_budget(result)

    if args.json:
        print(json.dumps({"failures": result.failures, "warnings": result.warnings}, indent=2))
    else:
        print(f"docs-structure validator (Phase 21)")
        print(f"  failures: {len(result.failures)}")
        print(f"  warnings: {len(result.warnings)}")
        for f in result.failures:
            print(f"  FAIL: {f}")
        for w in result.warnings:
            print(f"  WARN: {w}")

    return 1 if result.failures else 0


if __name__ == "__main__":
    sys.exit(main())
