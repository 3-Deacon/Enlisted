#!/usr/bin/env python3
"""
Scan C# source for ModLogger.Surfaced(...) calls and regenerate
docs/error-codes.md deterministically.

Usage:
    python Tools/Validation/generate_error_codes.py [--check]

Exits with non-zero on:
  - Two different (category, summary) pairs hashing to the same suffix
    within the same category (collision).
  - Surfaced call with empty category or summary.
  - Surfaced call with non-string-literal category or summary (we can't
    compute a stable code from a variable).

--check mode: validates that the on-disk docs/error-codes.md matches what
would be generated, without writing. Used by CI.
"""
import argparse
import hashlib
import re
import sys
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SRC_DIR = REPO_ROOT / "src"
REGISTRY_PATH = REPO_ROOT / "docs" / "error-codes.md"

# Match: ModLogger.Surfaced("CATEGORY", "summary"...
# We only accept two string literals as the first two args. Interpolated
# strings, concatenations, or variables make the code unstable and are rejected.
SURFACED_RE = re.compile(
    r'ModLogger\.Surfaced\s*\(\s*'
    r'"([A-Z][A-Z0-9\-]*)"\s*,\s*'   # group 1: category (uppercase alnum + dash)
    r'"([^"\\]*(?:\\.[^"\\]*)*)"'    # group 2: summary (handles simple escapes)
    r'',
    re.MULTILINE,
)

# Detect suspicious (non-literal) Surfaced calls so we can fail loudly.
SUSPICIOUS_RE = re.compile(r'ModLogger\.Surfaced\s*\(')

# Block and line comments. Block is non-greedy, multi-line via DOTALL.
# Line comment matches // (including ///) up to but not including the newline.
_BLOCK_COMMENT_RE = re.compile(r'/\*.*?\*/', re.DOTALL)
_LINE_COMMENT_RE = re.compile(r'//[^\n]*')


def _blank_preserving_newlines(match: re.Match) -> str:
    """Replace a regex match with whitespace, preserving newlines so that
    downstream line-number calculations (text[:pos].count("\\n")) remain
    accurate."""
    return "".join("\n" if ch == "\n" else " " for ch in match.group(0))


def strip_comments(text: str) -> str:
    """Strip C# // line comments and /* ... */ block comments from source
    text, replacing them with whitespace so line numbers are preserved.

    Block comments are stripped first because a block comment could contain
    '//', and a preceding '//' on the same line would otherwise swallow a
    block-comment opener. Naive (not string-aware): acceptable here because
    we only care about locating 'ModLogger.Surfaced(' which is not plausibly
    embedded in a string literal.
    """
    text = _BLOCK_COMMENT_RE.sub(_blank_preserving_newlines, text)
    text = _LINE_COMMENT_RE.sub(_blank_preserving_newlines, text)
    return text


def compute_suffix(summary: str) -> str:
    h = hashlib.sha256(summary.encode("utf-8")).digest()
    return f"{h[0]:02x}{h[1]:02x}"


def scan() -> tuple[list[dict], list[str]]:
    entries = []
    errors = []
    for cs_file in SRC_DIR.rglob("*.cs"):
        try:
            text = cs_file.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            text = cs_file.read_text(encoding="utf-8-sig")

        # Strip C# comments before scanning so commented-out Surfaced calls
        # (ghosts) and XML doc crefs like <see cref="ModLogger.Surfaced(...)"/>
        # don't produce false positives. Whitespace replacement preserves
        # line numbers for accurate error/registry reporting.
        text = strip_comments(text)

        # Collect all Surfaced matches with their line numbers.
        matched_spans = []
        for m in SURFACED_RE.finditer(text):
            line = text[: m.start()].count("\n") + 1
            entries.append({
                "category": m.group(1),
                "summary": m.group(2),
                "file": cs_file.relative_to(REPO_ROOT).as_posix(),
                "line": line,
            })
            matched_spans.append((m.start(), m.end()))

        # Any call to Surfaced(...) NOT caught by SURFACED_RE is non-literal
        # and should be a hard error.
        for m in SUSPICIOUS_RE.finditer(text):
            if any(s <= m.start() < e for s, e in matched_spans):
                continue
            line = text[: m.start()].count("\n") + 1
            errors.append(
                f"{cs_file.relative_to(REPO_ROOT).as_posix()}:{line}: "
                f"ModLogger.Surfaced(...) with non-literal arguments — "
                f"category and summary must be string literals for stable code assignment."
            )
    return entries, errors


def validate(entries: list[dict]) -> list[str]:
    errors = []
    # Empty args
    for e in entries:
        if not e["category"]:
            errors.append(f"{e['file']}:{e['line']}: empty category")
        if not e["summary"]:
            errors.append(f"{e['file']}:{e['line']}: empty summary")
    # Collisions per category
    by_cat_suffix = defaultdict(dict)
    for e in entries:
        suffix = compute_suffix(e["summary"])
        key = (e["category"], suffix)
        prior = by_cat_suffix[e["category"]].get(suffix)
        if prior is not None and prior["summary"] != e["summary"]:
            errors.append(
                f"Hash collision in category {e['category']} (suffix {suffix}):\n"
                f"  {prior['file']}:{prior['line']}: {prior['summary']!r}\n"
                f"  {e['file']}:{e['line']}: {e['summary']!r}\n"
                f"  Rephrase one summary string to break the tie."
            )
        else:
            by_cat_suffix[e["category"]][suffix] = e
    return errors


def render(entries: list[dict]) -> str:
    by_cat: dict[str, list[dict]] = defaultdict(list)
    for e in entries:
        by_cat[e["category"]].append(e)

    lines = [
        "# Error Codes",
        "",
        "This registry is **auto-generated** from `ModLogger.Surfaced(...)` calls",
        "by `Tools/Validation/generate_error_codes.py`. Do not hand-edit — your",
        "changes will be overwritten on the next run.",
        "",
        "For pre-redesign codes (format `E-SUBSYSTEM-NNN`), see",
        "[error-codes-archive.md](error-codes-archive.md).",
        "",
    ]
    for cat in sorted(by_cat):
        lines.append(f"## {cat}")
        lines.append("")
        lines.append("| Code | Summary | Source |")
        lines.append("|---|---|---|")
        # Dedup + aggregate: multiple call sites with same summary share one row.
        seen: dict[str, dict] = {}
        for e in by_cat[cat]:
            suffix = compute_suffix(e["summary"])
            code = f"E-{cat}-{suffix}"
            if code in seen:
                # Multiple call sites; keep the first file:line.
                continue
            seen[code] = {**e, "code": code}
        for code in sorted(seen):
            e = seen[code]
            lines.append(f"| {code} | {e['summary']} | {e['file']}:{e['line']} |")
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true",
                        help="Validate registry is in sync; do not write.")
    args = parser.parse_args()

    entries, scan_errors = scan()
    errors = scan_errors + validate(entries)
    if errors:
        print("[generate_error_codes] ERRORS:", file=sys.stderr)
        for e in errors:
            print(f"  {e}", file=sys.stderr)
        return 1

    rendered = render(entries)
    if args.check:
        current = REGISTRY_PATH.read_text(encoding="utf-8") if REGISTRY_PATH.exists() else ""
        if current != rendered:
            print("[generate_error_codes] docs/error-codes.md is out of sync. "
                  "Run without --check to regenerate.", file=sys.stderr)
            return 1
        print(f"[generate_error_codes] OK — {len(entries)} Surfaced call(s), registry in sync.")
        return 0

    REGISTRY_PATH.write_text(rendered, encoding="utf-8")
    print(f"[generate_error_codes] Wrote {REGISTRY_PATH} "
          f"({len(entries)} Surfaced call(s) across {len({e['category'] for e in entries})} categories).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
