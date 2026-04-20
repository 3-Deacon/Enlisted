#!/usr/bin/env python3
"""
Extract Localization Strings from C# Code

Scans C# files for TextObject("{=string_id}Fallback text") patterns
and extracts the fallback text to generate proper XML localization entries.

Usage:
    python Tools/Validation/extract_localization_from_cs.py

Outputs:
    _extracted_strings.xml - Ready-to-add XML entries with proper text
"""

import re
from collections import defaultdict
from pathlib import Path


def extract_textobject_strings(cs_file_path):
    """
    Extract TextObject string patterns from a C# file.
    Returns list of (string_id, fallback_text) tuples.
    """
    try:
        content = cs_file_path.read_text(encoding="utf-8-sig")
    except Exception as e:
        print(f"  ERROR reading {cs_file_path}: {e}")
        return []

    # Pattern: TextObject("{=string_id}Fallback text here...")
    # Captures string_id and fallback_text
    # Handles multi-line strings and escaped quotes
    pattern = re.compile(
        r'new\s+TextObject\s*\(\s*["\']?\{=([a-zA-Z0-9_]+)\}([^"\']*)["\']', re.MULTILINE
    )

    matches = []
    for match in pattern.finditer(content):
        string_id = match.group(1)
        fallback_text = match.group(2).strip()

        # Clean up fallback text
        fallback_text = fallback_text.replace('\\"', '"')  # Unescape quotes
        fallback_text = fallback_text.strip("\"'")  # Remove quotes

        # Skip empty fallback text (these need manual review)
        if not fallback_text or fallback_text == string_id:
            continue

        matches.append((string_id, fallback_text))

    return matches


def main():
    print("=" * 80)
    print("EXTRACTING LOCALIZATION STRINGS FROM C# CODE")
    print("=" * 80)

    src_path = Path("src")
    if not src_path.exists():
        print("ERROR: src/ directory not found")
        return 1

    # Collect all strings from C# files
    all_strings = defaultdict(set)  # string_id -> set of fallback texts
    file_count = 0
    string_count = 0

    for cs_file in src_path.rglob("*.cs"):
        strings = extract_textobject_strings(cs_file)
        if strings:
            file_count += 1
            for string_id, fallback_text in strings:
                all_strings[string_id].add(fallback_text)
                string_count += 1

    print(f"\nScanned {file_count} C# files")
    print(f"Found {string_count} TextObject references")
    print(f"Unique string IDs: {len(all_strings)}")

    # Detect conflicts (same ID with different text)
    conflicts = {sid: texts for sid, texts in all_strings.items() if len(texts) > 1}
    if conflicts:
        print(f"\nWARNING: {len(conflicts)} string IDs have multiple different texts:")
        for sid, texts in sorted(conflicts.items())[:5]:
            print(f"  {sid}:")
            for text in texts:
                print(f"    - {text[:60]}...")
        if len(conflicts) > 5:
            print(f"  ... and {len(conflicts) - 5} more")

    # Generate XML output
    output_file = Path("_extracted_strings.xml")

    with open(output_file, "w", encoding="utf-8") as f:
        f.write('<?xml version="1.0" encoding="utf-8"?>\n')
        f.write("<!-- Extracted localization strings from C# code -->\n")
        f.write("<!-- Add these to ModuleData/Languages/enlisted_strings.xml -->\n\n")
        f.write("<base>\n")
        f.write("  <strings>\n")

        for string_id in sorted(all_strings.keys()):
            texts = all_strings[string_id]
            if len(texts) == 1:
                text = list(texts)[0]
                # Escape XML special characters
                text = text.replace("&", "&amp;")
                text = text.replace("<", "&lt;")
                text = text.replace(">", "&gt;")
                text = text.replace('"', "&quot;")
                text = text.replace("'", "&apos;")

                f.write(f'    <string id="{string_id}" text="{text}" />\n')
            else:
                # Multiple texts - add comment with all variants
                f.write(f"    <!-- CONFLICT: {string_id} has {len(texts)} variants -->\n")
                for i, text in enumerate(sorted(texts), 1):
                    text_escaped = (
                        text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
                    )
                    f.write(f"    <!-- Variant {i}: {text_escaped[:60]}... -->\n")
                # Use first variant as default
                text = sorted(texts)[0]
                text = (
                    text.replace("&", "&amp;")
                    .replace("<", "&lt;")
                    .replace(">", "&gt;")
                    .replace('"', "&quot;")
                    .replace("'", "&apos;")
                )
                f.write(f'    <string id="{string_id}" text="{text}" />\n')

        f.write("  </strings>\n")
        f.write("</base>\n")

    print(f"\n[OK] Generated {output_file}")
    print(f"  Contains {len(all_strings)} string entries")
    print("\nNext steps:")
    print("  1. Review _extracted_strings.xml")
    print("  2. Merge into ModuleData/Languages/enlisted_strings.xml")
    print("  3. Run validation again to confirm all strings are present")

    return 0


if __name__ == "__main__":
    import sys

    sys.exit(main())
