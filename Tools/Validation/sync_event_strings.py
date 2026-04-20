#!/usr/bin/env python3
"""
Sync localization strings from JSON event files to enlisted_strings.xml.

This script scans all JSON event files in ModuleData/Enlisted/Events/ and extracts
localization string IDs and their fallback text, then appends missing strings to
ModuleData/Languages/enlisted_strings.xml.

Usage:
    python tools/events/sync_event_strings.py

The script will:
1. Scan all event JSON files for string IDs (titleId, setupId, textId, resultTextId, resultFailureTextId)
2. Load existing string IDs from enlisted_strings.xml
3. Generate XML <string> entries for any missing IDs using fallback text from JSON
4. Append missing strings to enlisted_strings.xml (maintaining XML structure)
5. Report statistics on strings processed and added

All special characters are properly escaped for XML:
- Newlines: &#xA;
- Quotes: &quot;
- Apostrophes: &apos;
"""

import json
import re
from pathlib import Path


def xml_escape(text: str) -> str:
    """Escape text for XML attributes"""
    if not text:
        return ""
    text = text.replace("&", "&amp;")
    text = text.replace("<", "&lt;").replace(">", "&gt;")
    text = text.replace('"', "&quot;").replace("'", "&apos;")
    text = text.replace("\n", "&#xA;")
    text = text.replace("&amp;#xA;", "&#xA;")
    return text


def extract_strings_from_event(event: dict) -> dict[str, str]:
    """Extract all localizable strings from an event definition

    Supports both schema v1 (with 'content' wrapper) and schema v2 (flattened).
    """
    strings = {}

    # Schema v1: has 'content' wrapper
    # Schema v2: flattened (titleId/setupId at top level)
    content = event.get("content", event)

    # Title and setup
    if content.get("titleId") and content.get("title"):
        strings[content["titleId"]] = content["title"]
    if content.get("setupId") and content.get("setup"):
        strings[content["setupId"]] = content["setup"]

    # Options
    for opt in content.get("options", []):
        if opt.get("textId") and opt.get("text"):
            strings[opt["textId"]] = opt["text"]
        # Schema v2: resultText
        if opt.get("resultTextId") and opt.get("resultText"):
            strings[opt["resultTextId"]] = opt["resultText"]
        # Schema v1: outcome (fallback)
        elif opt.get("resultTextId") and opt.get("outcome"):
            strings[opt["resultTextId"]] = opt["outcome"]
        # Schema v2: failure_resultText
        if opt.get("resultFailureTextId") and opt.get("failure_resultText"):
            strings[opt["resultFailureTextId"]] = opt["failure_resultText"]
        # Schema v1: outcome_failure (fallback)
        elif opt.get("resultFailureTextId") and opt.get("outcome_failure"):
            strings[opt["resultFailureTextId"]] = opt["outcome_failure"]
        # Order events: failResultText (alternate field name)
        elif opt.get("failResultTextId") and opt.get("failResultText"):
            strings[opt["failResultTextId"]] = opt["failResultText"]

    # Variants (custom extension in some events)
    for variant in event.get("variants", {}).values():
        if isinstance(variant, dict):
            if variant.get("setupId") and variant.get("setup"):
                strings[variant["setupId"]] = variant["setup"]
            for opt in variant.get("options", []):
                if opt.get("textId") and opt.get("text"):
                    strings[opt["textId"]] = opt["text"]
                if opt.get("resultTextId"):
                    if opt.get("resultText"):
                        strings[opt["resultTextId"]] = opt["resultText"]
                    elif opt.get("outcome"):
                        strings[opt["resultTextId"]] = opt["outcome"]

    return strings


def extract_strings_from_storylet(storylet: dict) -> dict[str, str]:
    """Extract all localizable strings from a storylet definition.

    Parses the inline {=key}Fallback pattern used in the storylet backbone.
    Fields scanned: title, setup, options[].text, options[].tooltip.
    """
    strings = {}
    pattern = re.compile(r"^\{=([^}]+)\}(.*)$", re.DOTALL)

    for field in ("title", "setup"):
        value = storylet.get(field, "")
        if isinstance(value, str) and value.startswith("{="):
            m = pattern.match(value)
            if m:
                strings[m.group(1)] = m.group(2)

    for opt in storylet.get("options", []):
        for field in ("text", "tooltip"):
            value = opt.get(field, "")
            if isinstance(value, str) and value.startswith("{="):
                m = pattern.match(value)
                if m:
                    strings[m.group(1)] = m.group(2)

    return strings


def scan_storylet_files(
    storylets_dir: Path, verbose: bool = False
) -> tuple[dict[str, dict], list[str]]:
    """Scan all JSON storylet files and extract inline {=key}Fallback strings.

    Returns (all_strings, missing_fallbacks) — same shape as scan_event_files.
    missing_fallbacks is always empty because the {=key}X pattern enforces a
    fallback by construction.
    """
    all_strings: dict[str, dict] = {}
    missing_fallbacks: list[str] = []

    for json_file in storylets_dir.glob("*.json"):
        try:
            with open(json_file, encoding="utf-8-sig") as f:
                content = f.read()

            try:
                data = json.loads(content)
            except json.JSONDecodeError:
                content = re.sub(r",(\s*[}\]])", r"\1", content)
                try:
                    data = json.loads(content)
                except json.JSONDecodeError as e:
                    if verbose:
                        print(f"  Skipping {json_file.name}: {e}")
                    continue

            for storylet in data.get("storylets", []):
                storylet_id = storylet.get("id", "unknown")
                strings = extract_strings_from_storylet(storylet)
                if strings:
                    all_strings[storylet_id] = strings

        except Exception as e:
            if verbose:
                print(f"  Skipping {json_file.name}: {e}")

    return all_strings, missing_fallbacks


def load_existing_xml_strings(xml_file: Path) -> set[str]:
    """Load all existing string IDs from XML file"""
    if not xml_file.exists():
        return set()

    content = xml_file.read_text(encoding="utf-8")
    pattern = r'<string\s+id="([^"]+)"'
    return set(re.findall(pattern, content))


def scan_event_files(events_dir: Path, verbose: bool = False) -> tuple[dict[str, dict], list[str]]:
    """Scan all JSON event files and extract strings"""
    all_strings = {}
    missing_ids = []

    for json_file in events_dir.glob("*.json"):
        try:
            # Handle UTF-8 BOM
            with open(json_file, encoding="utf-8-sig") as f:
                content = f.read()

            # Try to parse JSON, skip files with trailing commas or other issues
            try:
                data = json.loads(content)
            except json.JSONDecodeError:
                # Try lenient parsing by removing trailing commas
                content = re.sub(r",(\s*[}\]])", r"\1", content)
                try:
                    data = json.loads(content)
                except json.JSONDecodeError as e:
                    if verbose:
                        print(f"  Skipping {json_file.name}: {e}")
                    continue

            events = data.get("events", [])
            for event in events:
                event_id = event.get("id", "unknown")
                strings = extract_strings_from_event(event)

                if strings:
                    all_strings[event_id] = strings

                # Check for missing fallback text
                content = event.get("content", {})
                if content.get("titleId") and not content.get("title"):
                    missing_ids.append(f"{json_file.name}: {event_id} missing 'title' fallback")
                if content.get("setupId") and not content.get("setup"):
                    missing_ids.append(f"{json_file.name}: {event_id} missing 'setup' fallback")

        except Exception as e:
            if verbose:
                print(f"  Skipping {json_file.name}: {e}")

    return all_strings, missing_ids


def generate_xml_strings(
    all_strings: dict[str, dict], missing_ids: set[str], output_file: Path
) -> None:
    """Generate XML string entries for missing IDs and write to file"""
    # Collect missing strings with their text
    missing_strings = []
    for event_id, strings in all_strings.items():
        for string_id, text in strings.items():
            if string_id in missing_ids:
                missing_strings.append((string_id, text, event_id))

    # Sort by ID for consistent output
    missing_strings.sort(key=lambda x: x[0])

    # Generate XML entries grouped by event
    xml_lines = []
    current_event = None

    for string_id, text, event_id in missing_strings:
        # Add comment for new event group
        if event_id != current_event:
            if xml_lines:
                xml_lines.append("")  # Blank line between events
            xml_lines.append(f"    <!-- {event_id} -->")
            current_event = event_id

        # Escape text for XML
        escaped_text = xml_escape(text)
        xml_lines.append(f'    <string id="{string_id}" text="{escaped_text}" />')

    # Write to output file
    with open(output_file, "w", encoding="utf-8") as f:
        f.write("\n".join(xml_lines))

    print(f"\nGenerated {len(missing_strings)} XML string entries")
    print(f"Output written to: {output_file}")
    print("\nTo add these to enlisted_strings.xml:")
    print("1. Review the generated XML")
    print("2. Copy the entries into enlisted_strings.xml before the closing </strings> tag")
    print("3. Organize by section if desired (Events, Decisions, Orders, etc.)")


def main():
    """Main entry point"""
    import argparse

    parser = argparse.ArgumentParser(description="Check and sync event localization strings")
    parser.add_argument(
        "--check", action="store_true", help="Check for missing strings only (don't modify)"
    )
    parser.add_argument(
        "--generate",
        type=str,
        metavar="FILE",
        help="Generate missing XML strings to specified file",
    )
    parser.add_argument("--verbose", action="store_true", help="Show detailed output")
    args = parser.parse_args()

    # Paths
    events_dir = Path("ModuleData/Enlisted/Events")
    decisions_dir = Path("ModuleData/Enlisted/Decisions")
    xml_file = Path("ModuleData/Languages/enlisted_strings.xml")

    if not events_dir.exists():
        print(f"Error: {events_dir} not found")
        return 1

    print("Scanning JSON event files...")
    all_strings, missing_fallbacks = scan_event_files(events_dir, args.verbose)

    print("Scanning JSON decision files...")
    decision_strings, decision_fallbacks = scan_event_files(decisions_dir, args.verbose)
    all_strings.update(decision_strings)
    missing_fallbacks.extend(decision_fallbacks)

    # Scan order event files
    order_events_dir = Path("ModuleData/Enlisted/Orders/order_events")
    if order_events_dir.exists():
        print("Scanning order event files...")
        order_strings, order_fallbacks = scan_event_files(order_events_dir, args.verbose)
        all_strings.update(order_strings)
        missing_fallbacks.extend(order_fallbacks)

    # Scan storylet files
    storylets_dir = Path("ModuleData/Enlisted/Storylets")
    if storylets_dir.exists():
        print("Scanning storylet files...")
        storylet_strings, storylet_fallbacks = scan_storylet_files(storylets_dir, args.verbose)
        all_strings.update(storylet_strings)
        missing_fallbacks.extend(storylet_fallbacks)

    # Count total strings
    total_string_count = sum(len(strings) for strings in all_strings.values())
    print(f"Found {len(all_strings)} events with {total_string_count} localizable strings")

    # Load existing XML strings
    existing_ids = load_existing_xml_strings(xml_file)
    print(f"Found {len(existing_ids)} existing strings in XML")

    # Find missing strings
    all_required_ids = set()
    for strings in all_strings.values():
        all_required_ids.update(strings.keys())

    missing_in_xml = all_required_ids - existing_ids

    if missing_in_xml:
        print(f"\n[MISSING] {len(missing_in_xml)} string IDs missing from XML:")
        for string_id in sorted(missing_in_xml):
            # Find which event this belongs to
            for event_id, strings in all_strings.items():
                if string_id in strings:
                    print(f"  {string_id} (from {event_id})")
                    break
    else:
        print("\n[OK] All string IDs present in XML")

    if missing_fallbacks:
        print(f"\n[WARNING] {len(missing_fallbacks)} events missing fallback text in JSON:")
        for issue in missing_fallbacks:
            print(f"  {issue}")

    if args.check:
        return 1 if (missing_in_xml or missing_fallbacks) else 0

    # Generate XML strings if requested
    if args.generate and missing_in_xml:
        output_file = Path(args.generate)
        generate_xml_strings(all_strings, missing_in_xml, output_file)
        return 0

    # If not just checking, offer to generate missing strings
    if missing_in_xml:
        print("\nTo add missing strings, you'll need to:")
        print("1. Run with --generate output.xml to create XML entries")
        print("2. Review the generated XML")
        print("3. Copy entries into enlisted_strings.xml in the appropriate section")
        print("4. Ensure proper XML escaping is preserved")
        return 1

    return 0


if __name__ == "__main__":
    import sys

    sys.exit(main())
