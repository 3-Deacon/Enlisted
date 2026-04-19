#!/usr/bin/env python3
"""
Convert event JSON files from old reputation system to new system.

Changes:
1. Scrutiny: Scale by 10 (0-10 → 0-100)
2. Discipline: Convert to Scrutiny and scale by 10
3. Officer/Soldier reputation: Convert to lordReputation
"""

import json
import os
import sys
from pathlib import Path
from typing import Any, Dict, List, Tuple


def convert_effects(effects: Dict[str, Any]) -> Tuple[Dict[str, Any], bool]:
    """
    Convert effects object from old system to new system.
    Returns (new_effects, changed).
    """
    if not effects:
        return effects, False
    
    new_effects = {}
    changed = False
    
    # Track reputation values for potential merging
    lord_rep_total = 0
    has_officer = False
    has_soldier = False
    
    for key, value in effects.items():
        if key == "scrutiny":
            # Scale scrutiny by 10
            if isinstance(value, (int, float)) and value != 0:
                new_effects["scrutiny"] = int(value * 10)
                changed = True
            else:
                new_effects["scrutiny"] = value
                
        elif key == "discipline":
            # Convert discipline to scrutiny (scaled by 10)
            if isinstance(value, (int, float)) and value != 0:
                # Add to existing scrutiny if present
                scrutiny_value = new_effects.get("scrutiny", 0)
                new_effects["scrutiny"] = scrutiny_value + int(value * 10)
                changed = True
            # Don't copy discipline key
            
        elif key == "lordRep":
            # lordRep is a short form for lordReputation - just rename it
            if isinstance(value, (int, float)):
                lord_rep_total += value
                has_officer = True
                changed = True
            # Don't copy lordRep key (will be renamed to lordReputation)
            
        elif key in ("officerReputation", "officerRep"):
            # Convert to lordReputation
            if isinstance(value, (int, float)):
                lord_rep_total += value
                has_officer = True
                changed = True
            # Don't copy officerReputation/officerRep key
            
        elif key in ("soldierReputation", "soldierRep"):
            # Convert to lordReputation with half weight
            if isinstance(value, (int, float)):
                lord_rep_total += int(value / 2)
                has_soldier = True
                changed = True
            # Don't copy soldierReputation/soldierRep key
            
        elif key == "escalation" and isinstance(value, dict):
            # Nested escalation effects object - recursively convert it
            new_escalation, escalation_changed = convert_effects(value)
            if escalation_changed:
                new_effects["escalation"] = new_escalation
                changed = True
            else:
                new_effects["escalation"] = value
        
        else:
            # Keep all other effects unchanged
            new_effects[key] = value
    
    # Add lordReputation if we converted any officer/soldier rep
    if has_officer or has_soldier:
        existing_lord_rep = new_effects.get("lordReputation", 0)
        new_effects["lordReputation"] = existing_lord_rep + lord_rep_total
    
    return new_effects, changed


def convert_event(event: Dict[str, Any]) -> Tuple[Dict[str, Any], bool]:
    """
    Convert a single event object. Returns (new_event, changed).
    """
    changed = False
    new_event = event.copy()
    
    # Convert effects
    if "effects" in event:
        new_effects, effects_changed = convert_effects(event["effects"])
        if effects_changed:
            new_event["effects"] = new_effects
            changed = True
    
    # Convert caughtConsequences (used in camp_opportunities.json)
    if "caughtConsequences" in event:
        new_consequences, consequences_changed = convert_effects(event["caughtConsequences"])
        if consequences_changed:
            new_event["caughtConsequences"] = new_consequences
            changed = True
    
    # Convert option effects
    if "options" in event:
        new_options = []
        for option in event["options"]:
            new_option = option.copy()
            if "effects" in option:
                new_effects, effects_changed = convert_effects(option["effects"])
                if effects_changed:
                    new_option["effects"] = new_effects
                    changed = True
            if "failEffects" in option:
                new_fail_effects, fail_effects_changed = convert_effects(option["failEffects"])
                if fail_effects_changed:
                    new_option["failEffects"] = new_fail_effects
                    changed = True
            if "effects_success" in option:
                new_success_effects, success_effects_changed = convert_effects(option["effects_success"])
                if success_effects_changed:
                    new_option["effects_success"] = new_success_effects
                    changed = True
            if "effects_failure" in option:
                new_failure_effects, failure_effects_changed = convert_effects(option["effects_failure"])
                if failure_effects_changed:
                    new_option["effects_failure"] = new_failure_effects
                    changed = True
            # Also check camelCase variants
            if "effectsSuccess" in option:
                new_success_effects, success_effects_changed = convert_effects(option["effectsSuccess"])
                if success_effects_changed:
                    new_option["effectsSuccess"] = new_success_effects
                    changed = True
            if "effectsFailure" in option:
                new_failure_effects, failure_effects_changed = convert_effects(option["effectsFailure"])
                if failure_effects_changed:
                    new_option["effectsFailure"] = new_failure_effects
                    changed = True
            new_options.append(new_option)
        new_event["options"] = new_options
    
    # Convert consequences
    if "consequences" in event:
        new_consequences = []
        for consequence in event["consequences"]:
            new_consequence = consequence.copy()
            if "effects" in consequence:
                new_effects, effects_changed = convert_effects(consequence["effects"])
                if effects_changed:
                    new_consequence["effects"] = new_effects
                    changed = True
            new_consequences.append(new_consequence)
        new_event["consequences"] = new_consequences
    
    return new_event, changed


def convert_file(file_path: Path, dry_run: bool = False) -> Tuple[int, bool]:
    """
    Convert a JSON file. Returns (events_changed, file_changed).
    """
    try:
        with open(file_path, 'r', encoding='utf-8-sig') as f:
            data = json.load(f)
        
        if not isinstance(data, dict):
            return 0, False
        
        # Find events array (could be "events", "decisions", "opportunities", "orders")
        events_key = None
        for key in ["events", "decisions", "opportunities", "orders"]:
            if key in data and isinstance(data[key], list):
                events_key = key
                break
        
        if not events_key:
            return 0, False
        
        events = data[events_key]
        new_events = []
        events_changed = 0
        file_changed = False
        
        for event in events:
            new_event, changed = convert_event(event)
            if changed:
                events_changed += 1
                file_changed = True
            new_events.append(new_event)
        
        if file_changed and not dry_run:
            data[events_key] = new_events
            with open(file_path, 'w', encoding='utf-8-sig') as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
        
        return events_changed, file_changed
    
    except Exception as e:
        print(f"ERROR processing {file_path}: {e}")
        return 0, False


def main():
    script_dir = Path(__file__).parent.parent.parent
    module_data_dir = script_dir / "ModuleData" / "Enlisted"
    
    if not module_data_dir.exists():
        print(f"ERROR: ModuleData directory not found at {module_data_dir}")
        return 1
    
    dry_run = "--dry-run" in sys.argv
    
    # Find all JSON files in Events/, Orders/, and Decisions/
    json_files = []
    for subdir in ["Events", "Orders", "Decisions"]:
        subdir_path = module_data_dir / subdir
        if subdir_path.exists():
            json_files.extend(subdir_path.rglob("*.json"))
    
    # Exclude backup files
    json_files = [f for f in json_files if ".backup" not in f.name]
    
    print(f"Found {len(json_files)} JSON files")
    if dry_run:
        print("DRY RUN MODE - No files will be modified")
    print()
    
    total_files_changed = 0
    total_events_changed = 0
    
    for json_file in sorted(json_files):
        events_changed, file_changed = convert_file(json_file, dry_run)
        
        if file_changed:
            total_files_changed += 1
            total_events_changed += events_changed
            rel_path = json_file.relative_to(module_data_dir)
            print(f"{'[DRY RUN] ' if dry_run else ''}Updated {rel_path}: {events_changed} events changed")
    
    print()
    print(f"Summary: {total_files_changed} files changed, {total_events_changed} events updated")
    
    return 0


if __name__ == "__main__":
    sys.exit(main())
