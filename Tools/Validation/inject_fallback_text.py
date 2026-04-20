#!/usr/bin/env python3
"""
Inject Fallback Text Tool

Reads localized strings from enlisted_strings.xml and injects them as fallback text
into event JSON files. This ensures events always display readable text even when
XML localization fails to load (e.g., during hot-reload development).

Safety features:
- Only ADDS missing fallback fields, never removes or modifies existing ones
- Preserves all existing JSON structure and formatting
- Creates backup before modifying any file
- Reports all changes for review

Usage:
    python tools/events/inject_fallback_text.py [--dry-run] [--no-backup]

Options:
    --dry-run    Show what would be changed without modifying files
    --no-backup  Skip creating backup files (not recommended)
"""

import json
import re
import shutil
import sys
from datetime import datetime
from pathlib import Path

# Paths relative to project root
PROJECT_ROOT = Path(__file__).parent.parent.parent
XML_PATH = PROJECT_ROOT / "ModuleData" / "Languages" / "enlisted_strings.xml"
EVENTS_DIR = PROJECT_ROOT / "ModuleData" / "Enlisted" / "Events"
DECISIONS_DIR = PROJECT_ROOT / "ModuleData" / "Enlisted" / "Decisions"
ORDERS_DIR = PROJECT_ROOT / "ModuleData" / "Enlisted" / "Orders"
BACKUP_DIR = PROJECT_ROOT / "Debugging" / "fallback_backups"

# Field mappings: ID field -> fallback field name
FIELD_MAPPINGS = {
    "titleId": "title",
    "setupId": "setup",
    "textId": "text",
    "resultTextId": "resultText",
    "resultFailureTextId": "resultFailureText",
    "tooltipId": "tooltip",
    "descriptionId": "description",
}


def load_xml_strings(xml_path: Path) -> dict[str, str]:
    """Load all string definitions from the XML localization file using regex for robustness."""
    strings = {}

    if not xml_path.exists():
        print(f"[ERROR] XML file not found: {xml_path}")
        return strings

    try:
        with open(xml_path, encoding="utf-8") as f:
            content = f.read()

        # Use regex to extract string id and text attributes
        # Pattern matches: <string id="..." text="..." />
        pattern = r'<string\s+id="([^"]+)"\s+text="([^"]*)"'
        matches = re.findall(pattern, content)

        for string_id, text in matches:
            if string_id and text:
                # Decode XML entities to plain text for JSON
                text = text.replace("&#xA;", "\n")
                text = text.replace("&apos;", "'")
                text = text.replace("&quot;", '"')
                text = text.replace("&amp;", "&")
                text = text.replace("&lt;", "<")
                text = text.replace("&gt;", ">")
                strings[string_id] = text

        print(f"[OK] Loaded {len(strings)} strings from XML")

    except Exception as e:
        print(f"[ERROR] Failed to read XML: {e}")

    return strings


def process_options(
    options: list, xml_strings: dict[str, str], changes: list, file_name: str
) -> int:
    """Process event/decision options and add missing fallbacks. Returns count of changes."""
    change_count = 0

    for option in options:
        if not isinstance(option, dict):
            continue

        for id_field, fallback_field in FIELD_MAPPINGS.items():
            if id_field in option and fallback_field not in option:
                string_id = option[id_field]
                if string_id in xml_strings:
                    option[fallback_field] = xml_strings[string_id]
                    changes.append(
                        f"  + {file_name}: option '{option.get('id', '?')}' -> added '{fallback_field}'"
                    )
                    change_count += 1

        # Handle nested reward choices if present
        if "rewardChoices" in option and isinstance(option["rewardChoices"], dict):
            for choice_list in option["rewardChoices"].values():
                if isinstance(choice_list, list):
                    for choice in choice_list:
                        if isinstance(choice, dict):
                            for id_field, fallback_field in FIELD_MAPPINGS.items():
                                if id_field in choice and fallback_field not in choice:
                                    string_id = choice[id_field]
                                    if string_id in xml_strings:
                                        choice[fallback_field] = xml_strings[string_id]
                                        changes.append(
                                            f"  + {file_name}: reward choice -> added '{fallback_field}'"
                                        )
                                        change_count += 1

    return change_count


def process_event(event: dict, xml_strings: dict[str, str], changes: list, file_name: str) -> int:
    """Process a single event/decision and add missing fallbacks. Returns count of changes."""
    change_count = 0
    event_id = event.get("id", "unknown")

    # Process top-level ID fields
    for id_field, fallback_field in FIELD_MAPPINGS.items():
        if id_field in event and fallback_field not in event:
            string_id = event[id_field]
            if string_id in xml_strings:
                # Insert fallback immediately after the ID field to maintain proper order
                event[fallback_field] = xml_strings[string_id]
                changes.append(f"  + {file_name}: '{event_id}' -> added '{fallback_field}'")
                change_count += 1

    # Process options
    if "options" in event and isinstance(event["options"], list):
        change_count += process_options(event["options"], xml_strings, changes, file_name)

    return change_count


def reorder_event_fields(event: dict) -> dict:
    """
    Reorder event fields so fallback text immediately follows its ID field.
    This maintains the project convention for readability.
    """
    result = {}
    processed = set()

    # Define the order pairs (ID field, fallback field)
    pairs = [
        ("titleId", "title"),
        ("setupId", "setup"),
        ("textId", "text"),
        ("resultTextId", "resultText"),
        ("resultFailureTextId", "resultFailureText"),
        ("tooltipId", "tooltip"),
        ("descriptionId", "description"),
    ]

    # First pass: add fields in original order, inserting fallbacks after IDs
    for key, value in event.items():
        if key in processed:
            continue

        result[key] = value
        processed.add(key)

        # Check if this is an ID field that has a fallback
        for id_field, fallback_field in pairs:
            if key == id_field and fallback_field in event and fallback_field not in processed:
                result[fallback_field] = event[fallback_field]
                processed.add(fallback_field)

    # Recursively process options
    if "options" in result and isinstance(result["options"], list):
        result["options"] = [
            reorder_event_fields(opt) if isinstance(opt, dict) else opt for opt in result["options"]
        ]

    return result


def process_json_file(
    json_path: Path, xml_strings: dict[str, str], dry_run: bool, no_backup: bool
) -> tuple[int, list]:
    """Process a single JSON file and inject missing fallback text."""
    changes = []
    total_changes = 0
    file_name = json_path.name

    try:
        with open(json_path, encoding="utf-8-sig") as f:  # utf-8-sig handles BOM
            data = json.load(f)
    except json.JSONDecodeError as e:
        print(f"[ERROR] Failed to parse {file_name}: {e}")
        return 0, []
    except Exception as e:
        print(f"[ERROR] Failed to read {file_name}: {e}")
        return 0, []

    # Process events array
    if "events" in data and isinstance(data["events"], list):
        for event in data["events"]:
            if isinstance(event, dict):
                total_changes += process_event(event, xml_strings, changes, file_name)

    # Process decisions array (same structure)
    if "decisions" in data and isinstance(data["decisions"], list):
        for decision in data["decisions"]:
            if isinstance(decision, dict):
                total_changes += process_event(decision, xml_strings, changes, file_name)

    # Process orders array
    if "orders" in data and isinstance(data["orders"], list):
        for order in data["orders"]:
            if isinstance(order, dict):
                total_changes += process_event(order, xml_strings, changes, file_name)

    # If changes were made, reorder fields and save
    if total_changes > 0 and not dry_run:
        # Reorder fields for proper ID/fallback pairing
        if "events" in data:
            data["events"] = [
                reorder_event_fields(e) if isinstance(e, dict) else e for e in data["events"]
            ]
        if "decisions" in data:
            data["decisions"] = [
                reorder_event_fields(d) if isinstance(d, dict) else d for d in data["decisions"]
            ]
        if "orders" in data:
            data["orders"] = [
                reorder_event_fields(o) if isinstance(o, dict) else o for o in data["orders"]
            ]

        # Create backup
        if not no_backup:
            BACKUP_DIR.mkdir(parents=True, exist_ok=True)
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            backup_path = BACKUP_DIR / f"{json_path.stem}_{timestamp}.json"
            shutil.copy2(json_path, backup_path)

        # Write updated file
        with open(json_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
            f.write("\n")  # Trailing newline

    return total_changes, changes


def main():
    dry_run = "--dry-run" in sys.argv
    no_backup = "--no-backup" in sys.argv

    print("=" * 60)
    print("Fallback Text Injection Tool")
    print("=" * 60)

    if dry_run:
        print("[MODE] Dry run - no files will be modified")

    # Load XML strings
    xml_strings = load_xml_strings(XML_PATH)
    if not xml_strings:
        print("[ERROR] No strings loaded from XML. Aborting.")
        return 1

    # Collect all JSON files
    json_files = []
    for directory in [EVENTS_DIR, DECISIONS_DIR, ORDERS_DIR]:
        if directory.exists():
            json_files.extend(directory.glob("*.json"))

    print(f"[INFO] Found {len(json_files)} JSON files to process")
    print()

    # Process each file
    total_changes = 0
    all_changes = []
    modified_files = []

    for json_path in sorted(json_files):
        changes_count, changes = process_json_file(json_path, xml_strings, dry_run, no_backup)
        if changes_count > 0:
            total_changes += changes_count
            all_changes.extend(changes)
            modified_files.append(json_path.name)

    # Report results
    print()
    print("=" * 60)
    print("Results")
    print("=" * 60)

    if total_changes == 0:
        print("[OK] All events already have fallback text. No changes needed.")
    else:
        print(f"[INFO] {total_changes} fallback fields {'would be' if dry_run else 'were'} added")
        print(f"[INFO] {len(modified_files)} files {'would be' if dry_run else 'were'} modified:")
        for name in modified_files:
            print(f"       - {name}")

        if not dry_run and not no_backup:
            print(f"\n[INFO] Backups saved to: {BACKUP_DIR}")

        if dry_run:
            print("\n[INFO] Run without --dry-run to apply changes")

        # Show detailed changes
        print("\nDetailed changes:")
        for change in all_changes[:50]:  # Limit output
            print(change)
        if len(all_changes) > 50:
            print(f"  ... and {len(all_changes) - 50} more")

    return 0


if __name__ == "__main__":
    sys.exit(main())
