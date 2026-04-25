#!/usr/bin/env python3
"""
Schema V1 → V2 Migration Script

Converts deprecated schema v1 event files to schema v2 format:
1. Remove "content" wrapper
2. Rename "outcome" → "resultText"
3. Rename "outcome_failure" → "failure_resultText"
4. Change string prefixes: ll_evt_* → evt_*, ll_dec_* → dec_*
5. Update schemaVersion to 2
"""

import json
import sys
from pathlib import Path
from typing import Any


def migrate_string_id(string_id: str) -> str:
    """Convert legacy string IDs to schema v2 format."""
    if not string_id:
        return string_id

    # ll_evt_ → evt_
    if string_id.startswith("ll_evt_"):
        return string_id.replace("ll_evt_", "evt_", 1)

    # ll_dec_ → dec_
    if string_id.startswith("ll_dec_"):
        return string_id.replace("ll_dec_", "dec_", 1)

    return string_id


def migrate_option(option: dict[str, Any]) -> dict[str, Any]:
    """Migrate a single option from v1 to v2."""
    migrated = option.copy()

    # Rename outcome fields
    if "outcome" in migrated:
        migrated["resultText"] = migrated.pop("outcome")

    if "outcome_failure" in migrated:
        migrated["failure_resultText"] = migrated.pop("outcome_failure")

    # Migrate string IDs
    if "textId" in migrated:
        migrated["textId"] = migrate_string_id(migrated["textId"])

    if "resultTextId" in migrated:
        migrated["resultTextId"] = migrate_string_id(migrated["resultTextId"])

    if "resultFailureTextId" in migrated:
        migrated["resultFailureTextId"] = migrate_string_id(migrated["resultFailureTextId"])

    return migrated


def migrate_event(event: dict[str, Any]) -> dict[str, Any]:
    """Migrate a single event from v1 to v2."""
    migrated = event.copy()

    # Flatten content wrapper if present
    if "content" in migrated:
        content = migrated.pop("content")

        # Move content fields to top level
        for key, value in content.items():
            if key == "options":
                # Migrate each option
                migrated["options"] = [migrate_option(opt) for opt in value]
            else:
                migrated[key] = value

    # Migrate top-level string IDs
    if "titleId" in migrated:
        migrated["titleId"] = migrate_string_id(migrated["titleId"])

    if "setupId" in migrated:
        migrated["setupId"] = migrate_string_id(migrated["setupId"])

    return migrated


def migrate_file(input_path: Path, output_path: Path = None, backup: bool = True):
    """Migrate a schema v1 file to schema v2."""
    if output_path is None:
        output_path = input_path

    # Read input file
    with open(input_path, encoding="utf-8") as f:
        data = json.load(f)

    # Check schema version
    if data.get("schemaVersion") != 1:
        print(f"[X] {input_path.name}: Not schema v1 (schemaVersion={data.get('schemaVersion')})")
        return False

    # Create backup if requested
    if backup and output_path == input_path:
        backup_path = input_path.with_suffix(".json.v1.backup")
        with open(backup_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        print(f"[BACKUP] {backup_path.name}")

    # Migrate events
    migrated_data = data.copy()
    migrated_data["schemaVersion"] = 2

    if "events" in migrated_data:
        migrated_data["events"] = [migrate_event(evt) for evt in migrated_data["events"]]

    # Write output
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(migrated_data, f, indent=2, ensure_ascii=False)

    event_count = len(migrated_data.get("events", []))
    print(f"[OK] {input_path.name}: Migrated {event_count} events to schema v2")
    return True


def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(description="Migrate schema v1 event files to v2")
    parser.add_argument("files", nargs="+", help="Files to migrate")
    parser.add_argument("--no-backup", action="store_true", help="Don't create .v1.backup files")
    parser.add_argument("--dry-run", action="store_true", help="Show what would be done")

    args = parser.parse_args()

    print("=" * 80)
    print("SCHEMA V1 -> V2 MIGRATION")
    print("=" * 80)
    print()

    success_count = 0
    fail_count = 0

    for file_path in args.files:
        path = Path(file_path)

        if not path.exists():
            print(f"[X] {path.name}: File not found")
            fail_count += 1
            continue

        if args.dry_run:
            with open(path, encoding="utf-8") as f:
                data = json.load(f)

            if data.get("schemaVersion") == 1:
                event_count = len(data.get("events", []))
                print(f"[DRY-RUN] {path.name}: Would migrate {event_count} events")
                success_count += 1
            else:
                print(f"[SKIP] {path.name}: Already schema v{data.get('schemaVersion', '?')}")
        else:
            if migrate_file(path, backup=not args.no_backup):
                success_count += 1
            else:
                fail_count += 1

        print()

    print("=" * 80)
    print(f"[OK] Migrated: {success_count} files")
    if fail_count > 0:
        print(f"[X] Failed: {fail_count} files")
    print("=" * 80)

    return 0 if fail_count == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
