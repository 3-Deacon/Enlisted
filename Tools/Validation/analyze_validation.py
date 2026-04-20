#!/usr/bin/env python3
"""
Validation Report Analyzer

Parses validation report and groups issues into actionable categories.
Provides fix recommendations and examples for each issue type.

Usage:
    python Tools/Validation/analyze_validation.py [path/to/report.txt]
    (defaults to Tools/Debugging/validation_report.txt)
"""

import re
import sys
from collections import defaultdict
from pathlib import Path


def parse_validation_report(report_path: str):
    """Parse validation report and categorize issues."""

    # Try multiple encodings to handle PowerShell output
    for encoding in ["utf-8", "utf-8-sig", "utf-16", "cp1252"]:
        try:
            with open(report_path, encoding=encoding) as f:
                content = f.read()
            break
        except UnicodeDecodeError:
            continue
    else:
        raise ValueError(f"Could not decode {report_path} with any known encoding")

    # Extract summary stats
    total_events = re.search(r"Total Events: (\d+)", content)
    total_errors = re.search(r"Errors: (\d+)", content)
    total_warnings = re.search(r"Warnings: (\d+)", content)

    # Extract category breakdown from summary
    warning_reference = re.search(r"warning_reference: (\d+)", content)
    warning_code_quality = re.search(r"warning_code_quality: (\d+)", content)
    warning_structure = re.search(r"warning_structure: (\d+)", content)
    warning_consistency = re.search(r"warning_consistency: (\d+)", content)

    # Detect truncation
    truncation_match = re.search(r"\.\.\. and (\d+) more warnings", content)
    is_truncated = truncation_match is not None

    # Categorize issues
    issues = {
        "structure": defaultdict(list),
        "reference": defaultdict(list),
        "logic": defaultdict(list),
        "consistency": defaultdict(list),
        "style": defaultdict(list),
        "completeness": defaultdict(list),
        "project": defaultdict(list),
        "config": defaultdict(list),
        "code_quality": defaultdict(list),
    }

    # Parse issue lines
    for line in content.split("\n"):
        if not line.strip() or not line.startswith("  ["):
            continue

        # Extract components: [ERROR/WARNING] file:event [category] message
        match = re.match(
            r"\s*\[(ERROR|WARNING|INFO)\]\s+([^:]+):([^\s]+)\s+\[([^\]]+)\]\s+(.+)", line
        )
        if not match:
            continue

        severity, file, event_id, category, message = match.groups()

        # Categorize by type
        if category == "structure":
            if "Invalid option count: 1" in message:
                issues["structure"]["single_option"].append((file, event_id, message))
            elif "option count" in message:
                issues["structure"]["invalid_option_count"].append((file, event_id, message))
            elif "tooltip" in message.lower():
                issues["structure"]["tooltip_issues"].append((file, event_id, message))
            else:
                issues["structure"]["other"].append((file, event_id, message))

        elif category == "reference":
            if "not found in enlisted_strings.xml" in message:
                # For C# files, event_id is the string_id; for JSON, extract from message
                if file.endswith(".cs"):
                    string_id = event_id
                else:
                    string_match = re.search(r"'([^']+)' not found", message)
                    string_id = string_match.group(1) if string_match else "unknown"
                issues["reference"]["missing_strings"].append((file, event_id, string_id))

        elif category == "logic":
            # SAFETY: Use flexible pattern matching instead of exact strings
            message_lower = message.lower()
            if "skillxp" in message_lower or "xp" in message_lower and "grant" in message_lower:
                issues["logic"]["missing_order_xp"].append((file, event_id, message))
            elif "tier" in message_lower and "role" in message_lower:
                issues["logic"]["tier_role_mismatch"].append((file, event_id, message))
            elif "cooldown" in message_lower:
                issues["logic"]["cooldown_issues"].append((file, event_id, message))
            else:
                # SAFETY: Capture unrecognized logic issues for review
                issues["logic"]["other"].append((file, event_id, message))

        elif category == "consistency":
            issues["consistency"]["flags"].append((file, event_id, message))

        elif category == "style":
            message_lower = message.lower()
            if "hint" in message_lower and "long" in message_lower:
                issues["style"]["long_hints"].append((file, event_id, message))
            elif "hint" in message_lower and "placeholder" in message_lower:
                issues["style"]["hints_no_placeholders"].append((file, event_id, message))
            elif "hint" in message_lower and "ui text" in message_lower:
                issues["style"]["hints_ui_style"].append((file, event_id, message))
            else:
                issues["style"]["other"].append((file, event_id, message))

        elif category == "completeness":
            if "missing hints" in message.lower():
                issues["completeness"]["missing_hints"].append((file, event_id, message))
            else:
                issues["completeness"]["other"].append((file, event_id, message))

        elif category == "project":
            message_lower = message.lower()
            if "not in .csproj" in message_lower or "not in csproj" in message_lower:
                issues["project"]["missing_from_csproj"].append((file, event_id, message))
            elif "does not exist" in message_lower:
                issues["project"]["orphaned_in_csproj"].append((file, event_id, message))
            elif (
                "content directory" in message_lower
                and "no itemgroup" in message_lower
                or "itemgroup" in message_lower
                and "no copy command" in message_lower
            ):
                issues["project"]["content_not_deployed"].append((file, event_id, message))
            elif "rogue file" in message_lower:
                issues["project"]["rogue_files"].append((file, event_id, message))
            elif "unexpected directory" in message_lower:
                issues["project"]["rogue_dirs"].append((file, event_id, message))
            elif "gui asset" in message_lower:
                issues["project"]["gui_missing"].append((file, event_id, message))
            else:
                issues["project"]["other"].append((file, event_id, message))

        elif category == "config":
            issues["config"]["errors"].append((file, event_id, message))

        elif category == "code_quality":
            if "IsCurrentlyAtSea" in message:
                issues["code_quality"]["sea_context"].append((file, event_id, message))
            else:
                issues["code_quality"]["other"].append((file, event_id, message))

    return {
        "stats": {
            "events": int(total_events.group(1)) if total_events else 0,
            "errors": int(total_errors.group(1)) if total_errors else 0,
            "warnings": int(total_warnings.group(1)) if total_warnings else 0,
            "warning_reference": int(warning_reference.group(1)) if warning_reference else 0,
            "warning_code_quality": int(warning_code_quality.group(1))
            if warning_code_quality
            else 0,
            "warning_structure": int(warning_structure.group(1)) if warning_structure else 0,
            "warning_consistency": int(warning_consistency.group(1)) if warning_consistency else 0,
            "is_truncated": is_truncated,
        },
        "issues": issues,
    }


def print_analysis(data):
    """Print analysis report."""

    print("=" * 80)
    print("VALIDATION ANALYSIS - ACTIONABLE SUMMARY")
    print("=" * 80)
    print(f"\nTotal Events: {data['stats']['events']}")
    print(f"Errors: {data['stats']['errors']}")
    print(f"Warnings: {data['stats']['warnings']}")
    print()

    # Structure Issues
    print("=" * 80)
    print("STRUCTURE ISSUES")
    print("=" * 80)

    single_opt = data["issues"]["structure"]["single_option"]
    if single_opt:
        print(f"\n[CRITICAL] Single Option Events ({len(single_opt)}):")
        print("Events must have 0 options (dynamic) or 2-6 options (player choice)")
        for file, event_id, _msg in single_opt[:5]:
            print(f"  - {file}:{event_id}")
        print("\nFIX: Add a second option OR set options: [] if dynamically generated")

    tooltip = data["issues"]["structure"]["tooltip_issues"]
    if tooltip:
        print(f"\n[HIGH] Tooltip Issues ({len(tooltip)}):")
        long_tooltips = [t for t in tooltip if "long" in t[2].lower()]
        missing_tooltips = [t for t in tooltip if "missing" in t[2].lower()]

        if long_tooltips:
            print(f"  Long Tooltips ({len(long_tooltips)}) - Should be <80 chars:")
            for file, event_id, msg in long_tooltips[:5]:
                length = re.search(r"(\d+) chars", msg)
                length_str = f" ({length.group(1)} chars)" if length else ""
                print(f"    - {file}:{event_id}{length_str}")

        if missing_tooltips:
            print(f"  Missing Tooltips ({len(missing_tooltips)}):")
            for file, event_id, _msg in missing_tooltips[:5]:
                print(f"    - {file}:{event_id}")

    # Logic Issues
    print("\n" + "=" * 80)
    print("LOGIC ISSUES")
    print("=" * 80)

    missing_xp = data["issues"]["logic"]["missing_order_xp"]
    if missing_xp:
        print(f"\n[HIGH] Missing Order XP ({len(missing_xp)}):")
        print("Order events MUST grant skillXp - players expect XP for completing orders")
        print("\nAffected files:")
        files = defaultdict(list)
        for file, event_id, msg in missing_xp:
            opt_match = re.search(r"option '([^']+)'", msg)
            opt_id = opt_match.group(1) if opt_match else "unknown"
            files[file].append(f"{event_id}:{opt_id}")

        for file, events in sorted(files.items())[:10]:
            print(f"  {file}:")
            for evt in events[:3]:
                print(f"    - {evt}")
            if len(events) > 3:
                print(f"    ... and {len(events) - 3} more")

        print("\nFIX: Add skillXp to effects:")
        print('  "effects": {')
        print('    "skillXp": { "Tactics": 12 }  // Adjust skill and amount based on order type')
        print("  }")

    tier_role = data["issues"]["logic"]["tier_role_mismatch"]
    if tier_role:
        print(f"\n[MEDIUM] Tier×Role Mismatches ({len(tier_role)}):")
        for file, event_id, msg in tier_role[:5]:
            print(f"  - {file}:{event_id}")
            print(f"    {msg}")

    # SAFETY: Report unrecognized logic issues
    other_logic = data["issues"]["logic"]["other"]
    if other_logic:
        print(f"\n[INFO] Other Logic Issues ({len(other_logic)}):")
        print("These issues don't match known patterns - review manually:")
        for file, event_id, msg in other_logic[:5]:
            print(f"  - {file}:{event_id}")
            print(f"    {msg}")
        if len(other_logic) > 5:
            print(f"  ... and {len(other_logic) - 5} more")

    # Reference Issues
    print("\n" + "=" * 80)
    print("REFERENCE ISSUES (Missing Localization)")
    print("=" * 80)

    missing_strings = data["issues"]["reference"]["missing_strings"]
    total_reference_warnings = data["stats"].get("warning_reference", len(missing_strings))
    is_truncated = data["stats"].get("is_truncated", False)

    if missing_strings or total_reference_warnings > 0:
        if is_truncated:
            print(
                f"\n[HIGH PRIORITY] Missing XML Strings (showing {len(missing_strings)} of {total_reference_warnings} total):"
            )
            print("  WARNING: Report truncated - run with --fix-refs to see all missing strings")
        else:
            print(f"\n[HIGH PRIORITY] Missing XML Strings ({len(missing_strings)}):")

        # Separate C# vs JSON files
        csharp_missing = [(f, e, s) for f, e, s in missing_strings if f.endswith(".cs")]
        json_missing = [(f, e, s) for f, e, s in missing_strings if not f.endswith(".cs")]

        # Estimate split if truncated (C# warnings appear first in validator output)
        if is_truncated and len(csharp_missing) == len(missing_strings):
            print(
                f"\n  C# TextObject References: ~{total_reference_warnings} (exact count requires --fix-refs)"
            )
            print("  JSON Event References: (unknown - report truncated)")
        else:
            print(f"\n  C# TextObject References: {len(csharp_missing)}")
            print(f"  JSON Event References: {len(json_missing)}")

        # Group by file
        by_file = defaultdict(lambda: defaultdict(list))
        for file, event_id, string_id in missing_strings:
            by_file[file][event_id].append(string_id)

        # Show C# files (most impactful - user-facing fallback text)
        csharp_files = [
            (f, sum(len(strings) for strings in events.values()))
            for f, events in by_file.items()
            if f.endswith(".cs")
        ]
        csharp_files.sort(key=lambda x: -x[1])

        if csharp_files:
            print("\n[USER-FACING] C# Files with Missing Localization:")
            print("  These show fallback text in game - high priority to fix")
            for file, count in csharp_files[:10]:
                print(f"    {file.ljust(40)} {count:3d} strings")
            if len(csharp_files) > 10:
                remaining = sum(c for _, c in csharp_files[10:])
                print(f"    ... and {len(csharp_files) - 10} more files ({remaining} strings)")

        # Show JSON files
        json_files = [
            (f, sum(len(strings) for strings in events.values()))
            for f, events in by_file.items()
            if not f.endswith(".cs")
        ]
        json_files.sort(key=lambda x: -x[1])

        if json_files:
            print("\n[CONTENT] JSON Events with Missing Strings:")
            for file, count in json_files[:5]:
                print(f"    {file}: {count} missing strings")

        # Check for schema v1 files
        escalation = by_file.get("events_escalation_thresholds.json")
        if escalation:
            sample_ids = []
            for _event_id, strings in list(escalation.items())[:2]:
                sample_ids.extend(strings[:2])

            print("\n[ACTION REQUIRED] events_escalation_thresholds.json:")
            print("  This file uses SCHEMA V1 (deprecated) with 'll_evt_*' string IDs")
            print(f"  Missing strings: {sum(len(s) for s in escalation.values())}")
            print(f"  Sample IDs: {sample_ids[:3]}")
            print("\n  RECOMMENDATION: Migrate to schema v2")
            print("    1. Remove 'content' wrapper object")
            print("    2. Move titleId/setupId to top level")
            print("    3. Rename 'outcome' -> 'resultText'")
            print("    4. Change string prefix: ll_evt_* -> evt_*")
            print("    5. Run: python Tools/Validation/sync_event_strings.py")

        print("\nFIX OPTIONS:")
        print("  1. Generate stub file: python Tools/Validation/validate_content.py --fix-refs")
        print("     Creates _missing_strings.txt with all missing string entries")
        print("  2. Review stubs and add proper localized text")
        print("  3. Add to ModuleData/Languages/enlisted_strings.xml")
        print("\nSee: Tools/Validation/VALIDATION_BASELINE.md for more details")

    # Code Quality Issues
    code_quality = data["issues"]["code_quality"]
    if any(code_quality.values()):
        print("\n" + "=" * 80)
        print("CODE QUALITY ISSUES")
        print("=" * 80)

        sea_context = code_quality.get("sea_context", [])
        if sea_context:
            print(f"\n[ACCEPTABLE] IsCurrentlyAtSea Pattern ({len(sea_context)}):")
            print("  These are low-priority usages per VALIDATION_BASELINE.md")
            print("  Critical paths (ContentOrchestrator, WorldStateAnalyzer) already fixed")

            # Group by file
            by_file = defaultdict(list)
            for file, line_num, _msg in sea_context:
                by_file[file].append(line_num)

            print("\n  Files affected:")
            for file, lines in sorted(by_file.items(), key=lambda x: -len(x[1]))[:5]:
                print(f"    {file}: {len(lines)} instances")

            if len(by_file) > 5:
                total_remaining = sum(len(lines) for f, lines in list(by_file.items())[5:])
                print(f"    ... and {len(by_file) - 5} more files ({total_remaining} instances)")

            print("\n  Status: Acceptable technical debt (non-critical paths)")

    # Consistency Issues
    consistency = data["issues"]["consistency"]["flags"]
    if consistency:
        print("\n" + "=" * 80)
        print("CONSISTENCY ISSUES")
        print("=" * 80)
        print(f"\n[INFO] Flag Usage ({len(consistency)}):")
        print("Terminal flags (set but never checked) are expected for end-of-chain events")
        print(f"Review if needed: {len(consistency)} flag-related messages")

    # Style Issues (Hints)
    style_issues = data["issues"]["style"]
    if any(style_issues.values()):
        print("\n" + "=" * 80)
        print("STYLE ISSUES (Opportunity Hints)")
        print("=" * 80)

        long_hints = style_issues.get("long_hints", [])
        if long_hints:
            print(f"\n[MEDIUM] Long Hints ({len(long_hints)}):")
            print("Hints should be under 10 words for readability in Daily Brief")
            for file, event_id, _msg in long_hints[:5]:
                print(f"  - {file}:{event_id}")
            print("\nFIX: Shorten to camp gossip style:")
            print('  "hint": "{SOLDIER_NAME} mentioned drill tonight."')

        no_placeholders = style_issues.get("hints_no_placeholders", [])
        if no_placeholders:
            print(f"\n[LOW] Hints Without Placeholders ({len(no_placeholders)}):")
            print("Camp rumors should use placeholders for personalization")
            for file, event_id, _msg in no_placeholders[:5]:
                print(f"  - {file}:{event_id}")
            print("\nFIX: Add dynamic tokens:")
            print('  "hint": "{VETERAN_1_NAME} mentioned a card game tonight."')
            print("  Available: {SOLDIER_NAME}, {COMRADE_NAME}, {SERGEANT}, {SETTLEMENT_NAME}")

        ui_style = style_issues.get("hints_ui_style", [])
        if ui_style:
            print(f"\n[HIGH] UI-Style Hints ({len(ui_style)}):")
            print("Hints should be narrative, not UI descriptions")
            for file, event_id, _msg in ui_style[:5]:
                print(f"  - {file}:{event_id}")
            print("\nFIX: Write as camp gossip, not system text:")
            print('  BAD:  "Card game opportunity available at dusk"')
            print('  GOOD: "{SOLDIER_NAME} is running cards tonight."')

    # Completeness Issues
    completeness = data["issues"]["completeness"]
    if completeness.get("missing_hints"):
        print("\n" + "=" * 80)
        print("COMPLETENESS ISSUES")
        print("=" * 80)
        missing = completeness["missing_hints"]
        print(f"\n[INFO] Opportunities Missing Hints ({len(missing)}):")
        print("Opportunities without hints won't show foreshadowing in Daily Brief")
        for _file, _event_id, msg in missing[:3]:
            print(f"  - {msg}")

    # Project Structure Issues
    project_issues = data["issues"]["project"]
    if any(project_issues.values()):
        print("\n" + "=" * 80)
        print("PROJECT STRUCTURE ISSUES")
        print("=" * 80)

        missing_csproj = project_issues.get("missing_from_csproj", [])
        if missing_csproj:
            print(f"\n[CRITICAL] C# Files Missing from .csproj ({len(missing_csproj)}):")
            print("These files exist in src/ but won't compile until added to .csproj")
            for _file, _event_id, msg in missing_csproj[:10]:
                # Extract file path from message
                match = re.search(r"C# file not in .csproj: ([^\s]+)", msg)
                if match:
                    cs_file = match.group(1)
                    cs_file.replace("/", "\\\\")
                    print(f"  - {cs_file}")
                else:
                    print(f"  - {msg}")
            if len(missing_csproj) > 10:
                print(f"  ... and {len(missing_csproj) - 10} more")
            print("\nFIX: Add to Enlisted.csproj:")
            print('  <Compile Include="src\\\\Features\\\\YourFile.cs"/>')

        orphaned = project_issues.get("orphaned_in_csproj", [])
        if orphaned:
            print(f"\n[CRITICAL] Files in .csproj That Don't Exist ({len(orphaned)}):")
            print("These entries reference deleted/moved files - remove from .csproj")
            for _file, _event_id, msg in orphaned[:10]:
                print(f"  - {msg}")
            print('\nFIX: Remove the <Compile Include="..."/> entry from .csproj')

        rogue_files = project_issues.get("rogue_files", [])
        if rogue_files:
            print(f"\n[MEDIUM] Rogue Files in Root Directory ({len(rogue_files)}):")
            print("These files clutter the root and should be organized per BLUEPRINT.md")
            for _file, _event_id, msg in rogue_files[:10]:
                print(f"  - {msg}")
            if len(rogue_files) > 10:
                print(f"  ... and {len(rogue_files) - 10} more")
            print("\nFIX: Move files to appropriate folders:")
            print("  *.py → Tools/Research/ or Tools/Validation/")
            print("  *.ps1 → Tools/Debugging/ or Tools/Steam/")
            print("  *.md → docs/ or Tools/Debugging/")
            print("  *.txt → Tools/Debugging/ or delete (if temporary)")

        rogue_dirs = project_issues.get("rogue_dirs", [])
        if rogue_dirs:
            print(f"\n[MEDIUM] Unexpected Directories in Root ({len(rogue_dirs)}):")
            for _file, _event_id, msg in rogue_dirs[:5]:
                print(f"  - {msg}")
            print("\nFIX: Review and relocate or delete these directories")

        content_not_deployed = project_issues.get("content_not_deployed", [])
        if content_not_deployed:
            print(f"\n[CRITICAL] Content Directories Not Deployed ({len(content_not_deployed)}):")
            print("These content folders exist in source but WON'T be copied to the game folder!")
            print("Players will experience missing content (events won't fire, etc.)")
            for _file, _event_id, msg in content_not_deployed[:10]:
                print(f"  - {msg}")
            print("\nFIX: Add three things to Enlisted.csproj:")
            print('  1. ItemGroup: <YourDataName Include="path\\\\to\\\\*.json"/>')
            print(
                '  2. MakeDir:   <MakeDir Directories="$(OutputPath)..\\\\..\\\\path\\\\to\\\\"/>'
            )
            print('  3. Copy:      <Copy SourceFiles="@(YourDataName)" DestinationFolder="..."/>')
            print("\nExample for order_events:")
            print(
                '  <OrderEventsData Include="ModuleData\\\\Enlisted\\\\Orders\\\\order_events\\\\*.json"/>'
            )

        gui_missing = project_issues.get("gui_missing", [])
        if gui_missing:
            print(f"\n[LOW] GUI Assets Not in .csproj ({len(gui_missing)}):")
            print("These GUI files won't be copied during build")
            for _file, _event_id, msg in gui_missing[:5]:
                print(f"  - {msg}")
            print("\nFIX: Add to .csproj <ItemGroup> for GUI assets:")
            print('  <Content Include="GUI\\\\Prefabs\\\\YourFile.xml"/>')

    # Summary
    print("\n" + "=" * 80)
    print("PRIORITY FIX ORDER")
    print("=" * 80)
    print("\n1. [CRITICAL] Fix content directories not deployed (players get missing content!)")
    print("2. [CRITICAL] Fix C# files missing from .csproj (won't compile)")
    print("3. [CRITICAL] Remove orphaned .csproj entries (build errors)")
    print("4. [CRITICAL] Fix single-option events (blocks validation)")
    print("5. [HIGH] Add C# TextObject localization strings (user-facing fallback text)")
    print("6. [HIGH] Add missing order XP (player-facing issue)")
    print("7. [HIGH] Fix long tooltips (UX issue)")
    print("8. [HIGH] Fix UI-style hints (immersion issue)")
    print("9. [MEDIUM] Clean up rogue root files (organization)")
    print("10. [MEDIUM] Migrate schema v1 files to v2 (maintenance)")
    print("11. [MEDIUM] Shorten long hints (readability)")
    print("12. [LOW] Add JSON event localization strings (as content is completed)")
    print("13. [LOW] Add placeholders to hints (personalization)")
    print("\n" + "=" * 80)
    print("DISCOVERED ISSUES")
    print("=" * 80)
    print("\nPhase 9 (C# TextObject validation) revealed:")

    total_ref = data["stats"].get("warning_reference", 0)
    visible_ref = len(
        [m for m in data["issues"]["reference"]["missing_strings"] if m[0].endswith(".cs")]
    )

    if data["stats"].get("is_truncated"):
        print(f"  * ~{total_ref} missing C# localization strings (report truncated)")
    else:
        print(f"  * {visible_ref} missing C# localization strings")

    print("  * These cause fallback text to display instead of proper localized strings")
    print("  * Previously invisible until Phase 9 was added to validator")
    print("  * See: Tools/Validation/VALIDATION_BASELINE.md section 5")
    print("\nRecommendation:")
    print("  Run: python Tools/Validation/validate_content.py --fix-refs")
    print("  This generates _missing_strings.txt with all 293 unique missing strings")
    print("  Review: _missing_strings.txt (contains stub XML entries)")
    print("  Update: ModuleData/Languages/enlisted_strings.xml (add proper text)")
    print()


def main():
    report_path = sys.argv[1] if len(sys.argv) > 1 else "Tools/Debugging/validation_report.txt"

    if not Path(report_path).exists():
        print(f"Error: {report_path} not found")
        print("\nRun validation first:")
        print(
            "  python Tools/Validation/validate_content.py > Tools/Debugging/validation_report.txt"
        )
        return 1

    data = parse_validation_report(report_path)
    print_analysis(data)

    return 0


if __name__ == "__main__":
    sys.exit(main())
