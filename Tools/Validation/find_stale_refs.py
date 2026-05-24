#!/usr/bin/env python3
"""Find (and optionally repair) stale references to moved/deleted doc paths.

Used by Phase F (rename phases) of the docs-restructure to ensure live
references in .cs / .json / .csproj / .xml / .md files get updated in the
same commit as the rename.

Usage:
  python3 Tools/Validation/find_stale_refs.py <old-path> [<new-path>]
  python3 Tools/Validation/find_stale_refs.py <old-path> <new-path> --apply

Without --apply, prints each match (file:line: matched text).
With --apply AND a <new-path>, rewrites <old-path> -> <new-path> in place.

Searches: *.cs, *.json, *.csproj, *.xml, *.md
Skips: .git, bin, obj, Decompile, .worktrees, Archive
"""
from __future__ import annotations
import argparse
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
EXTENSIONS = {".cs", ".json", ".csproj", ".xml", ".md"}
SKIP_DIRS = {".git", "bin", "obj", "Decompile", ".worktrees", "Archive"}


def scan(old_path: str) -> list[tuple[Path, int, str]]:
    hits: list[tuple[Path, int, str]] = []
    for path in REPO_ROOT.rglob("*"):
        if not path.is_file() or path.suffix not in EXTENSIONS:
            continue
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        try:
            for i, line in enumerate(path.open(encoding="utf-8", errors="ignore"), 1):
                if old_path in line:
                    hits.append((path, i, line.rstrip()))
        except OSError:
            continue
    return hits


def apply_rewrite(old_path: str, new_path: str, hits: list[tuple[Path, int, str]]) -> int:
    """Rewrite old_path -> new_path in every file containing a hit."""
    touched: set[Path] = set()
    for path, _, _ in hits:
        if path in touched:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except OSError:
            print(f"  SKIP (unreadable): {path.relative_to(REPO_ROOT)}", file=sys.stderr)
            continue
        new_text = text.replace(old_path, new_path)
        if new_text != text:
            path.write_text(new_text, encoding="utf-8")
            touched.add(path)
    return len(touched)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("old_path", help="Path substring to search for")
    parser.add_argument("new_path", nargs="?", help="Replacement (required with --apply)")
    parser.add_argument(
        "--apply", action="store_true", help="Rewrite in place (requires new_path)"
    )
    args = parser.parse_args()

    if args.apply and not args.new_path:
        parser.error("--apply requires <new_path>")

    hits = scan(args.old_path)
    if not hits:
        print(f"No references to {args.old_path!r} found.")
        return 0

    print(f"Found {len(hits)} reference(s) to {args.old_path!r}:")
    for path, line_no, line in hits:
        print(f"  {path.relative_to(REPO_ROOT)}:{line_no}: {line}")

    if args.apply:
        rewritten = apply_rewrite(args.old_path, args.new_path, hits)
        print(f"\nRewrote {args.old_path!r} -> {args.new_path!r} in {rewritten} file(s).")

    return 0


if __name__ == "__main__":
    sys.exit(main())
