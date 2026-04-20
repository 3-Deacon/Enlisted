#!/usr/bin/env python3
"""
Merge Localization Strings

Merges extracted strings from _extracted_strings.xml into enlisted_strings.xml,
avoiding duplicates and preserving the existing file structure.

Usage:
    python Tools/Validation/merge_localization.py
"""

import xml.etree.ElementTree as ET
from pathlib import Path


def load_existing_strings(xml_path):
    """Load existing string IDs from enlisted_strings.xml"""
    try:
        tree = ET.parse(xml_path)
        root = tree.getroot()

        existing_ids = set()
        strings_element = root.find("strings")
        if strings_element is not None:
            for string_elem in strings_element.findall("string"):
                string_id = string_elem.get("id")
                if string_id:
                    existing_ids.add(string_id)

        return existing_ids, tree, root
    except Exception as e:
        print(f"ERROR loading {xml_path}: {e}")
        return set(), None, None


def load_new_strings(xml_path):
    """Load new strings from _extracted_strings.xml"""
    try:
        tree = ET.parse(xml_path)
        root = tree.getroot()

        new_strings = {}
        strings_element = root.find("strings")
        if strings_element is not None:
            for string_elem in strings_element.findall("string"):
                string_id = string_elem.get("id")
                text = string_elem.get("text")
                if string_id and text:
                    new_strings[string_id] = text

        return new_strings
    except Exception as e:
        print(f"ERROR loading {xml_path}: {e}")
        return {}


def main():
    print("=" * 80)
    print("MERGING LOCALIZATION STRINGS")
    print("=" * 80)

    enlisted_xml = Path("ModuleData/Languages/enlisted_strings.xml")
    extracted_xml = Path("_extracted_strings.xml")

    if not enlisted_xml.exists():
        print(f"ERROR: {enlisted_xml} not found")
        return 1

    if not extracted_xml.exists():
        print(f"ERROR: {extracted_xml} not found")
        print("Run: python Tools/Validation/extract_localization_from_cs.py first")
        return 1

    # Load existing strings
    print(f"\nLoading {enlisted_xml}...")
    existing_ids, tree, root = load_existing_strings(enlisted_xml)
    if tree is None:
        return 1
    print(f"  Found {len(existing_ids)} existing strings")

    # Load new strings
    print(f"\nLoading {extracted_xml}...")
    new_strings = load_new_strings(extracted_xml)
    print(f"  Found {len(new_strings)} extracted strings")

    # Find strings to add
    to_add = {sid: text for sid, text in new_strings.items() if sid not in existing_ids}
    duplicates = len(new_strings) - len(to_add)

    print("\nAnalysis:")
    print(f"  Strings already present: {duplicates}")
    print(f"  New strings to add: {len(to_add)}")

    if not to_add:
        print("\n[OK] All strings already present in enlisted_strings.xml")
        return 0

    # Add new strings to the XML
    strings_element = root.find("strings")
    if strings_element is None:
        print("ERROR: No <strings> element found in enlisted_strings.xml")
        return 1

    # Add a comment section for new strings
    comment = ET.Comment(" Strings auto-extracted from C# code ")
    strings_element.append(comment)

    # Add new strings in alphabetical order
    for string_id in sorted(to_add.keys()):
        text = to_add[string_id]
        string_elem = ET.Element("string", {"id": string_id, "text": text})
        strings_element.append(string_elem)

    # Write back to file
    backup_path = Path("ModuleData/Languages/enlisted_strings.xml.backup")
    print(f"\nCreating backup: {backup_path}")
    enlisted_xml.rename(backup_path)

    print(f"Writing updated {enlisted_xml}...")
    tree.write(enlisted_xml, encoding="utf-8", xml_declaration=True)

    print(f"\n[OK] Merged {len(to_add)} new strings into {enlisted_xml}")
    print(f"     Backup saved to {backup_path}")
    print("\nNext steps:")
    print("  1. Run validation: python Tools/Validation/validate_content.py")
    print("  2. Verify reduced warning count")
    print("  3. If satisfied, delete backup file")

    return 0


if __name__ == "__main__":
    import sys

    sys.exit(main())
