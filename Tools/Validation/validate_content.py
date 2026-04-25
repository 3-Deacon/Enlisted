#!/usr/bin/env python3
"""
Enhanced Content Validation Tool for Enlisted Mod

Validates events, configs, and project structure against structural rules,
logical constraints, and integration requirements from docs/Features/Content/event-system-schemas.md

Usage:
    python Tools/Validation/validate_content.py [--strict] [--fix-refs] [--check-orphans]

Validation Phases:
    Phase 1: Structure validation (JSON schema, required fields, enum values)
    Phase 2: Reference validation (localization strings, skills, traits)
    Phase 3: Logical validation (impossible combinations, reasonable values)
    Phase 4: Consistency checks (flags, multi-stage events, priorities)
    Phase 5: Orphan detection (unused XML strings)
    Phase 6: Config validation (baggage_config.json, etc.)
    Phase 7: Project structure validation (.csproj, file organization)
    Phase 8: Code quality validation (hardcoded paths, sea context detection)
    Phase 9: C# TextObject localization (string IDs in code → XML)
    Phase 9.5: Camp schedule descriptions (meaningful phase text)
    Phase 10: Error-code registry sync (generate_error_codes.py --check)
    Phase 11: ModLogger.Error retirement gate (no Error calls in src/)
    Phase 12: Storylet reference validation (effect ids, quality ids, read-only guard)
"""

import argparse
import glob
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

# ============================================================================
# Constants and Reference Data (aligned with event-system-schemas.md)
# ============================================================================

# Valid Bannerlord skills (from schema)
# NOTE: If you add new skills to the game, add them here to avoid false positives
VALID_SKILLS = {
    "OneHanded",
    "TwoHanded",
    "Polearm",
    "Bow",
    "Crossbow",
    "Throwing",
    "Riding",
    "Athletics",
    "Crafting",
    "Scouting",
    "Tactics",
    "Roguery",
    "Charm",
    "Leadership",
    "Trade",
    "Stewardship",
    "Medicine",
    "Engineering",
    "Perception",  # Used in order events for awareness checks
}


# EXTENSION MECHANISM: Load custom skills from config if present
def load_custom_skills():
    """Load additional valid skills from custom config."""
    config_path = Path("ModuleData/Enlisted/Config/validation_extensions.json")
    if config_path.exists():
        try:
            with open(config_path, encoding="utf-8-sig") as f:
                extensions = json.load(f)
                custom_skills = extensions.get("valid_skills", [])
                if custom_skills:
                    print(
                        f"[INFO] Loaded {len(custom_skills)} custom skills from validation_extensions.json"
                    )
                    return set(custom_skills)
        except Exception as e:
            print(f"[WARNING] Failed to load validation_extensions.json: {e}")
    return set()


CUSTOM_SKILLS = load_custom_skills()
ALL_VALID_SKILLS = VALID_SKILLS | CUSTOM_SKILLS

# Valid roles as defined by the Identity System (from schema)
VALID_ROLES = {"Any", "Scout", "Medic", "Engineer", "Officer", "Operative", "NCO", "Soldier"}

# Valid contexts for narrative events (from schema)
VALID_CONTEXTS = {"Any", "War", "Peace", "Siege", "Battle", "Town", "Village", "Camp", "March"}

# Valid categories for events/decisions (from schema)
VALID_CATEGORIES = {
    "decision",
    "escalation",
    "role",
    "universal",
    "muster",
    "crisis",
    "general",
    "onboarding",
    "pay",
    "promotion",
    "retinue",
    "training",
    "threshold",
    "medical",
    "map_incident",  # Medical system events and map incident events
    "quiet_stretch",  # Low-intensity narrative filler events for the Director fallback pool
}

# Valid severities for order events (from schema)
VALID_SEVERITIES = {"normal", "attention", "critical", "urgent", "positive"}

# Valid severities for news priority (from schema)
VALID_NEWS_SEVERITIES = {"normal", "positive", "attention", "urgent", "critical"}

# Valid world states for order events (from schema - requirements.world_state)
VALID_WORLD_STATES = {
    "peacetime_garrison",
    "peacetime_recruiting",
    "peacetime_patrol",
    "war_marching",
    "war_active_campaign",
    "war_raiding",
    "siege_attacking",
    "siege_defending",
    "retreat",
    "recovery",
}

# Valid time of day (from schema)
VALID_TIME_OF_DAY = {
    "dawn",
    "morning",
    "midday",
    "afternoon",
    "evening",
    "night",
    "Dawn",
    "Midday",
    "Dusk",
    "Night",
}

# Role tier requirements (minimum tier for each role)
ROLE_MIN_TIERS = {
    "Officer": 5,
    "NCO": 4,
    "Operative": 3,
    "Scout": 1,
    "Medic": 1,
    "Engineer": 1,
    "Soldier": 1,
    "Any": 1,
}

# Escalation tracks and their ranges (from schema)
# Parser accepts multiple naming variants
ESCALATION_TRACKS = {
    # Primary tracks
    "scrutiny": (0, 10),
    "discipline": (0, 10),
    "medical_risk": (0, 5),
    "medicalrisk": (0, 5),
    "MedicalRisk": (0, 5),
    "pay_tension": (0, 100),
    "pay_tension_min": (0, 100),
    "paytension": (0, 100),
    # Reputation tracks
    "soldierreputation": (-50, 50),
    "soldier_reputation": (-50, 50),
    "SoldierReputation": (-50, 50),
    "officerreputation": (0, 100),
    "officer_reputation": (0, 100),
    "OfficerReputation": (0, 100),
    "lordreputation": (0, 100),
    "lord_reputation": (0, 100),
    "LordReputation": (0, 100),
}

# Known system string prefixes that shouldn't be flagged as orphans
SYSTEM_STRING_PREFIXES = {
    "Enlisted_",  # System messages
    "enlisted_",  # System messages (lowercase)
    "esc_",  # Escalation status words
    "rank_",  # Rank names
    "culture_",  # Culture names
    "dm_",  # Decision menu options
    "qm_",  # Quartermaster dialogue
    "camp_",  # Camp menu options
    "muster_",  # Muster menu options
    "discharge_",  # Discharge strings
    "news_",  # News system
    "brief_",  # Daily brief
    "tooltip_",  # UI tooltips
    "menu_",  # Menu section strings
    "order_",  # Order strings
    "opp_",  # Opportunity strings
    "prog_",  # Progression strings
}

# ============================================================================
# Data Structures
# ============================================================================


class ValidationIssue:
    """Represents a validation issue with severity and context."""

    def __init__(
        self, severity: str, category: str, message: str, file_path: str, event_id: str = None
    ):
        self.severity = severity  # "error", "warning", "info"
        self.category = category  # e.g., "structure", "reference", "logic", "consistency"
        self.message = message
        self.file_path = file_path
        self.event_id = event_id

    def __str__(self):
        prefix = f"[{self.severity.upper()}]"
        location = f"{Path(self.file_path).name}"
        if self.event_id:
            location += f":{self.event_id}"
        return f"{prefix} {location} [{self.category}] {self.message}"


class ValidationContext:
    """Accumulates validation issues and provides reporting."""

    def __init__(self, strict: bool = False):
        self.strict = strict
        self.issues: list[ValidationIssue] = []
        self.stats = Counter()
        self.event_ids: set[str] = set()
        self.flag_references: dict[str, list[str]] = defaultdict(list)
        self.flag_setters: dict[str, list[str]] = defaultdict(list)
        self.referenced_string_ids: set[str] = set()

    def add_issue(
        self, severity: str, category: str, message: str, file_path: str, event_id: str = None
    ):
        """Add a validation issue."""
        self.issues.append(ValidationIssue(severity, category, message, file_path, event_id))
        self.stats[f"{severity}_{category}"] += 1

    def track_string_reference(self, string_id: str):
        """Track a string ID that is referenced by JSON content."""
        if string_id:
            self.referenced_string_ids.add(string_id)

    def has_errors(self) -> bool:
        return any(issue.severity == "error" for issue in self.issues)

    def has_critical_issues(self) -> bool:
        return self.has_errors() or (self.strict and self.has_warnings())

    def has_warnings(self) -> bool:
        return any(issue.severity == "warning" for issue in self.issues)

    def print_report(self):
        """Print validation report."""
        errors = [i for i in self.issues if i.severity == "error"]
        warnings = [i for i in self.issues if i.severity == "warning"]
        infos = [i for i in self.issues if i.severity == "info"]

        print("\n" + "=" * 80)
        print("VALIDATION REPORT")
        print("=" * 80)

        if errors:
            print(f"\n[X] ERRORS ({len(errors)}):")
            for issue in errors[:50]:
                print(f"  {issue}")
            if len(errors) > 50:
                print(f"  ... and {len(errors) - 50} more errors")

        if warnings:
            print(f"\n[!] WARNINGS ({len(warnings)}):")
            for issue in warnings[:50]:
                print(f"  {issue}")
            if len(warnings) > 50:
                print(f"  ... and {len(warnings) - 50} more warnings")

        if infos:
            print(f"\n[i] INFO ({len(infos)}):")
            for issue in infos[:20]:
                print(f"  {issue}")
            if len(infos) > 20:
                print(f"  ... and {len(infos) - 20} more info messages")

        print("\n" + "-" * 80)
        print("SUMMARY:")
        print(f"  Total Events: {len(self.event_ids)}")
        print(f"  Errors: {len(errors)}")
        print(f"  Warnings: {len(warnings)}")
        print(f"  Info: {len(infos)}")

        if self.stats:
            print("\nBy Category:")
            for category, count in sorted(self.stats.items()):
                print(f"  {category}: {count}")

        print("=" * 80 + "\n")


# ============================================================================
# Localization String Loader
# ============================================================================


def load_localization_strings() -> set[str]:
    """Load all string IDs from enlisted_strings.xml."""
    xml_path = Path("ModuleData/Languages/enlisted_strings.xml")
    if not xml_path.exists():
        print(f"Warning: Localization file not found at {xml_path}")
        return set()

    try:
        tree = ET.parse(xml_path)
        root = tree.getroot()
        string_ids = set()

        for string_elem in root.findall(".//string"):
            string_id = string_elem.get("id")
            if string_id:
                string_ids.add(string_id)

        print(f"[INFO] Loaded {len(string_ids)} localization strings from enlisted_strings.xml")
        return string_ids
    except Exception as e:
        print(f"Error loading localization strings: {e}")
        return set()


# ============================================================================
# Phase 1: Structure Validation
# ============================================================================


def validate_structure(event: dict, file_path: str, ctx: ValidationContext) -> bool:
    """Validate event structure and required fields."""
    event_id = event.get("id", "UNKNOWN")

    # Required: ID field
    if not event_id or event_id == "UNKNOWN":
        ctx.add_issue("error", "structure", "Missing or empty 'id' field", file_path)
        return False

    # SAFETY: Detect unknown fields that might be typos or deprecated
    known_top_level_fields = {
        "id",
        "category",
        "order_type",
        "severity",
        "titleId",
        "title",
        "setupId",
        "setup",
        "requirements",
        "triggers",
        "timing",
        "options",
        "content",
        "metadata",
        "delivery",
        "packId",
        "schemaVersion",
        "skill_check",
        "sets_flag",
        "requires_flag",
    }
    unknown_fields = set(event.keys()) - known_top_level_fields
    if unknown_fields:
        ctx.add_issue(
            "info",
            "structure",
            f"Unknown top-level fields (new feature or typo?): {sorted(unknown_fields)}",
            file_path,
            event_id,
        )

    # Track event ID for duplicate detection
    if event_id in ctx.event_ids:
        ctx.add_issue("error", "structure", f"Duplicate event ID: {event_id}", file_path, event_id)
    else:
        ctx.event_ids.add(event_id)

    # Determine event type: Order Event vs Narrative Event
    order_type = event.get("order_type", "")
    category = event.get("category", "")
    severity = event.get("severity", "")

    if order_type:
        # ORDER EVENT: Uses order_type + severity
        if severity and severity.lower() not in VALID_SEVERITIES:
            ctx.add_issue(
                "warning", "structure", f"Unknown order severity: '{severity}'", file_path, event_id
            )
    else:
        # NARRATIVE EVENT: Uses category
        if not category:
            ctx.add_issue("error", "structure", "Missing 'category' field", file_path, event_id)
        elif category not in VALID_CATEGORIES:
            # Invalid category is an error - content won't behave correctly
            ctx.add_issue(
                "error",
                "structure",
                f"Invalid category: '{category}'. Valid: {sorted(VALID_CATEGORIES)}",
                file_path,
                event_id,
            )

        # Check news severity if present - invalid severity is an error
        if severity and severity.lower() not in VALID_NEWS_SEVERITIES:
            ctx.add_issue(
                "error",
                "structure",
                f"Invalid severity: '{severity}'. Valid: {sorted(VALID_NEWS_SEVERITIES)}",
                file_path,
                event_id,
            )

    # Title and setup (check both schema v1 and v2 locations)
    content = event.get("content") or {}
    title_id = event.get("titleId") or content.get("titleId")
    setup_id = event.get("setupId") or content.get("setupId")
    title = event.get("title") or content.get("title")
    setup = event.get("setup") or content.get("setup")

    # Either titleId or title must be present
    if not title_id and not title:
        ctx.add_issue(
            "error", "structure", "Missing 'titleId' or 'title' field", file_path, event_id
        )
    if not setup_id and not setup:
        ctx.add_issue(
            "error", "structure", "Missing 'setupId' or 'setup' field", file_path, event_id
        )

    # Options validation
    options = (event.get("options") or []) or (content.get("options") or [])
    if not options:
        # Allow empty options for dec_baggage_access (dynamically generated)
        if event_id != "dec_baggage_access":
            ctx.add_issue(
                "error", "structure", "Missing or empty 'options' array", file_path, event_id
            )
    else:
        # Option count validation
        has_abort = any(opt.get("abortsEnlistment") for opt in options)
        is_onboarding = category == "onboarding" or event.get("timing", {}).get("oneTime")

        if len(options) == 1:
            ctx.add_issue(
                "error",
                "structure",
                f"Invalid option count: {len(options)} (must be 0 or 2-6)",
                file_path,
                event_id,
            )
        elif len(options) > 6:
            ctx.add_issue(
                "error",
                "structure",
                f"Invalid option count: {len(options)} (must be 2-6)",
                file_path,
                event_id,
            )
        elif len(options) > 4 and not (is_onboarding or has_abort):
            ctx.add_issue(
                "warning",
                "structure",
                "5-6 options only recommended for onboarding/abort events",
                file_path,
                event_id,
            )

    # Validate each option's structure
    for i, option in enumerate(options):
        opt_id = option.get("id", f"option_{i}")

        # Text requirement
        if not option.get("textId") and not option.get("text"):
            ctx.add_issue(
                "error",
                "structure",
                f"Option '{opt_id}' missing textId and fallback text",
                file_path,
                event_id,
            )

        # Tooltip requirement (order events auto-generate from effects, so they can skip)
        if not order_type:
            has_tooltip = option.get("tooltip") or option.get("tooltipTemplate")
            if not has_tooltip:
                ctx.add_issue(
                    "error",
                    "structure",
                    f"Option '{opt_id}' missing tooltip or tooltipTemplate",
                    file_path,
                    event_id,
                )
            elif option.get("tooltip") and len(option.get("tooltip", "")) > 100:
                ctx.add_issue(
                    "warning",
                    "structure",
                    f"Option '{opt_id}' tooltip is long ({len(option['tooltip'])} chars)",
                    file_path,
                    event_id,
                )

    return True


# ============================================================================
# Phase 2: Reference Validation
# ============================================================================


def validate_references(
    event: dict, file_path: str, ctx: ValidationContext, localization_ids: set[str]
):
    """Validate references to localization strings, skills, traits."""
    event_id = event.get("id", "UNKNOWN")
    event.get("order_type", "")

    # Check localization references
    content = event.get("content") or {}
    title_id = event.get("titleId") or content.get("titleId")
    setup_id = event.get("setupId") or content.get("setupId")

    # Track references for orphan detection
    ctx.track_string_reference(title_id)
    ctx.track_string_reference(setup_id)

    if title_id and title_id not in localization_ids:
        ctx.add_issue(
            "warning",
            "reference",
            f"titleId '{title_id}' not found in enlisted_strings.xml",
            file_path,
            event_id,
        )
    if setup_id and setup_id not in localization_ids:
        ctx.add_issue(
            "warning",
            "reference",
            f"setupId '{setup_id}' not found in enlisted_strings.xml",
            file_path,
            event_id,
        )

    # Check option references
    options = (event.get("options") or []) or (content.get("options") or [])
    for option in options:
        text_id = option.get("textId")
        result_id = option.get("resultTextId")
        fail_result_id = option.get("failResultTextId") or option.get("resultTextFailureId")

        ctx.track_string_reference(text_id)
        ctx.track_string_reference(result_id)
        ctx.track_string_reference(fail_result_id)

        if text_id and text_id not in localization_ids:
            ctx.add_issue(
                "warning",
                "reference",
                f"textId '{text_id}' not found in enlisted_strings.xml",
                file_path,
                event_id,
            )
        if result_id and result_id not in localization_ids:
            ctx.add_issue(
                "warning",
                "reference",
                f"resultTextId '{result_id}' not found in enlisted_strings.xml",
                file_path,
                event_id,
            )
        if fail_result_id and fail_result_id not in localization_ids:
            ctx.add_issue(
                "warning",
                "reference",
                f"failResultTextId '{fail_result_id}' not found in enlisted_strings.xml",
                file_path,
                event_id,
            )

    # Check skill references in requirements
    requirements = event.get("requirements") or {}
    min_skills = requirements.get("minSkills") or {}
    for skill_name in min_skills:
        if skill_name not in ALL_VALID_SKILLS:
            # SAFETY: Suggest close matches before flagging as error
            from difflib import get_close_matches

            suggestions = get_close_matches(skill_name, ALL_VALID_SKILLS, n=1, cutoff=0.6)
            if suggestions:
                ctx.add_issue(
                    "error",
                    "reference",
                    f"Invalid skill in minSkills: '{skill_name}' (did you mean '{suggestions[0]}'?)",
                    file_path,
                    event_id,
                )
            else:
                ctx.add_issue(
                    "warning",
                    "reference",
                    f"Unknown skill in minSkills: '{skill_name}' (add to VALID_SKILLS if this is a custom skill)",
                    file_path,
                    event_id,
                )

    # Check world_state values (for order events)
    world_states = requirements.get("world_state") or []
    if isinstance(world_states, list):
        for ws in world_states:
            if ws not in VALID_WORLD_STATES:
                ctx.add_issue(
                    "warning", "reference", f"Unknown world_state: '{ws}'", file_path, event_id
                )

    # Check skill XP in effects, rewards, failEffects
    for option in options:
        _validate_skill_xp(option.get("effects") or {}, "effects", event_id, file_path, ctx)
        _validate_skill_xp(option.get("failEffects") or {}, "failEffects", event_id, file_path, ctx)
        _validate_skill_xp(option.get("rewards") or {}, "rewards", event_id, file_path, ctx)
        _validate_skill_xp(
            option.get("effects_success") or {}, "effects_success", event_id, file_path, ctx
        )
        _validate_skill_xp(
            option.get("effects_failure") or {}, "effects_failure", event_id, file_path, ctx
        )

        # Check skillCheck skill reference
        skill_check = option.get("skillCheck")
        if skill_check:
            if isinstance(skill_check, dict):
                check_skill = skill_check.get("skill")
            else:
                check_skill = str(skill_check)
            if check_skill and check_skill not in VALID_SKILLS:
                ctx.add_issue(
                    "error",
                    "reference",
                    f"Invalid skill in skillCheck: '{check_skill}'",
                    file_path,
                    event_id,
                )

        # LOGIC CHECK: Skill-gated options should use dynamic skill checks
        option_requirements = option.get("requirements") or {}
        min_skills = option_requirements.get("minSkills") or {}
        has_risk_chance = option.get("risk_chance") is not None
        has_skill_check = option.get("skillCheck") is not None

        if min_skills and has_risk_chance and not has_skill_check:
            skill_names = ", ".join(min_skills.keys())
            ctx.add_issue(
                "warning",
                "logic",
                f"Option '{option.get('id')}' has minSkills ({skill_names}) + fixed risk_chance ({option.get('risk_chance')}%). Should use skillCheck for dynamic probability calculation instead.",
                file_path,
                event_id,
            )


def _validate_skill_xp(
    obj: dict, location: str, event_id: str, file_path: str, ctx: ValidationContext
):
    """Helper to validate skillXp references in any object."""
    if not obj:
        return
    skill_xp = obj.get("skillXp") or {}
    for skill_name in skill_xp:
        if skill_name not in ALL_VALID_SKILLS:
            # Suggest proper casing
            skill_lower = skill_name.lower()
            suggestion = next((s for s in ALL_VALID_SKILLS if s.lower() == skill_lower), None)
            if suggestion:
                ctx.add_issue(
                    "error",
                    "reference",
                    f"Invalid skill in {location}.skillXp: '{skill_name}' (did you mean '{suggestion}'?)",
                    file_path,
                    event_id,
                )
            else:
                # SAFETY: Warn instead of error if might be custom skill
                ctx.add_issue(
                    "warning",
                    "reference",
                    f"Unknown skill in {location}.skillXp: '{skill_name}' (add to validation_extensions.json if custom)",
                    file_path,
                    event_id,
                )


# ============================================================================
# Phase 3: Logical Validation
# ============================================================================


def validate_logic(event: dict, file_path: str, ctx: ValidationContext):
    """Validate logical consistency and impossible combinations."""
    event_id = event.get("id", "UNKNOWN")
    order_type = event.get("order_type", "")
    event.get("category", "")
    requirements = event.get("requirements") or {}

    # Get tier requirements
    tier_req = requirements.get("tier") or {}
    min_tier = tier_req.get("min") or requirements.get("minTier")
    max_tier = tier_req.get("max") or requirements.get("maxTier")
    role = requirements.get("role", "Any")

    # Rule 1: Check tier × role combinations
    if role in ROLE_MIN_TIERS:
        role_min = ROLE_MIN_TIERS[role]
        if min_tier and min_tier < role_min:
            ctx.add_issue(
                "error",
                "logic",
                f"Impossible tier×role: role '{role}' requires tier {role_min}+, but minTier={min_tier}",
                file_path,
                event_id,
            )
        if max_tier and max_tier < role_min:
            ctx.add_issue(
                "error",
                "logic",
                f"Impossible tier×role: role '{role}' requires tier {role_min}+, but maxTier={max_tier}",
                file_path,
                event_id,
            )

    # Rule 2: Camp Hub decisions can't require Battle context
    context = requirements.get("context", "Any")
    if event_id.startswith("dec_") and context == "Battle":
        ctx.add_issue(
            "error",
            "logic",
            "Camp Hub decisions (dec_*) cannot require 'Battle' context",
            file_path,
            event_id,
        )

    # Rule 3: Role-skill alignment check
    min_skills = requirements.get("minSkills") or {}
    if role == "Medic" and min_skills:
        if "Medicine" not in min_skills:
            ctx.add_issue(
                "warning",
                "logic",
                f"Role 'Medic' usually requires Medicine skill, but minSkills={list(min_skills.keys())}",
                file_path,
                event_id,
            )
    elif role == "Engineer" and min_skills and "Engineering" not in min_skills:
        ctx.add_issue(
            "warning",
            "logic",
            f"Role 'Engineer' usually requires Engineering skill, but minSkills={list(min_skills.keys())}",
            file_path,
            event_id,
        )

    # Rule 4: Escalation requirements range check
    triggers = event.get("triggers") or {}
    escalation_reqs = (triggers.get("escalation_requirements") or {}) or (
        requirements.get("minEscalation") or {}
    )
    for track, value in escalation_reqs.items():
        if track in ESCALATION_TRACKS:
            min_val, max_val = ESCALATION_TRACKS[track]
            if not (min_val <= value <= max_val):
                ctx.add_issue(
                    "error",
                    "logic",
                    f"Escalation track '{track}' value {value} out of range ({min_val}-{max_val})",
                    file_path,
                    event_id,
                )

    # Rule 5: Cooldown reasonableness
    timing = event.get("timing") or {}
    cooldown = timing.get("cooldown_days") or timing.get("cooldownDays") or 0
    if cooldown < 0:
        ctx.add_issue("error", "logic", f"Negative cooldown: {cooldown}", file_path, event_id)
    elif event_id.startswith("dec_rest") and cooldown > 7:
        ctx.add_issue(
            "warning",
            "logic",
            f"Rest decisions should have short cooldowns (1-2 days), but cooldown={cooldown}",
            file_path,
            event_id,
        )

    # Rule 6: One-time events should have high priority
    priority = timing.get("priority", "normal")
    one_time = timing.get("one_time") or timing.get("oneTime") or False
    if one_time and priority in ["low", "rare"]:
        ctx.add_issue(
            "warning",
            "logic",
            f"One-time event with low priority ({priority}) - should use 'high' or 'critical'",
            file_path,
            event_id,
        )

    # Rule 7: Order events MUST grant XP (from schema)
    if order_type:
        options = event.get("options") or []
        for option in options:
            opt_id = option.get("id", "unknown")
            effects = option.get("effects") or {}
            fail_effects = option.get("failEffects") or {}

            has_xp = "skillXp" in effects or "skillXp" in fail_effects
            if not has_xp:
                ctx.add_issue(
                    "warning",
                    "logic",
                    f"Order event option '{opt_id}' grants no skillXp - players expect XP for completing orders",
                    file_path,
                    event_id,
                )


# ============================================================================
# Phase 4: Consistency Checks
# ============================================================================


def validate_consistency(event: dict, file_path: str, ctx: ValidationContext):
    """Validate flag usage and multi-stage event consistency."""
    event_id = event.get("id", "UNKNOWN")

    # Track flag references
    triggers = event.get("triggers") or {}
    for trigger_list in [
        triggers.get("all") or [],
        triggers.get("any") or [],
        triggers.get("none") or [],
    ]:
        for trigger in trigger_list:
            if trigger.startswith("has_flag:") or trigger.startswith("flag:"):
                flag_name = trigger.replace("has_flag:", "").replace("flag:", "")
                ctx.flag_references[flag_name].append(event_id)

    # Track flag setters
    content = event.get("content") or {}
    options = (event.get("options") or []) or (content.get("options") or [])
    for option in options:
        effects = option.get("effects") or {}
        flags_set = (
            (effects.get("setFlags") or [])
            or (effects.get("set_flags") or [])
            or (option.get("flags_set") or [])
        )
        for flag in flags_set:
            ctx.flag_setters[flag].append(event_id)


def validate_flag_consistency(ctx: ValidationContext):
    """Check for flags that are referenced but never set, or set but never referenced."""
    for flag, references in ctx.flag_references.items():
        if flag not in ctx.flag_setters:
            ctx.add_issue(
                "warning",
                "consistency",
                f"Flag '{flag}' referenced by {len(references)} event(s) but never set: {references[:3]}",
                "flag_analysis",
                None,
            )

    for flag, setters in ctx.flag_setters.items():
        if flag not in ctx.flag_references:
            ctx.add_issue(
                "info",
                "consistency",
                f"Flag '{flag}' set by event(s) but never referenced (terminal flag?): {setters[:3]}",
                "flag_analysis",
                None,
            )


# ============================================================================
# Phase 5: Orphan String Detection
# ============================================================================


def detect_orphan_strings(localization_ids: set[str], ctx: ValidationContext):
    """Detect XML strings that are never referenced by any JSON content."""
    orphan_count = 0
    orphan_samples = []

    for string_id in localization_ids:
        if string_id not in ctx.referenced_string_ids:
            is_system = any(string_id.startswith(prefix) for prefix in SYSTEM_STRING_PREFIXES)
            if not is_system:
                orphan_count += 1
                if len(orphan_samples) < 30:
                    orphan_samples.append(string_id)

    if orphan_count > 0:
        ctx.add_issue(
            "info",
            "orphan",
            f"Found {orphan_count} potentially orphaned strings in XML (not referenced by any JSON)",
            "orphan_analysis",
            None,
        )

        # Group orphans by prefix
        prefixes = defaultdict(list)
        for s in orphan_samples:
            prefix = s.split("_")[0] + "_" if "_" in s else "other"
            prefixes[prefix].append(s)

        for prefix, samples in sorted(prefixes.items()):
            sample_list = samples[:5]
            suffix = "..." if len(samples) > 5 else ""
            ctx.add_issue(
                "info",
                "orphan",
                f"  Orphan prefix '{prefix}': {sample_list}{suffix}",
                "orphan_analysis",
                None,
            )


# ============================================================================
# Phase 5.5: Opportunity Validation (including hints)
# ============================================================================

# Valid opportunity types (from event-system-schemas.md)
VALID_OPPORTUNITY_TYPES = {"training", "social", "economic", "recovery", "special"}

# Valid day phases for opportunities
VALID_DAY_PHASES = {"Dawn", "Midday", "Dusk", "Night"}

# Placeholder tokens that should be used in hints for personalization
RECOMMENDED_HINT_PLACEHOLDERS = {
    "{SOLDIER_NAME}",
    "{COMRADE_NAME}",
    "{VETERAN_1_NAME}",
    "{VETERAN_2_NAME}",
    "{RECRUIT_NAME}",
    "{SERGEANT}",
    "{SERGEANT_NAME}",
    "{OFFICER_NAME}",
    "{SETTLEMENT_NAME}",
    "{LORD_NAME}",
}


def validate_opportunities(file_path: str, ctx: ValidationContext, localization_ids: set[str]):
    """Validate camp_opportunities.json structure and hint fields."""
    try:
        with open(file_path, encoding="utf-8-sig") as f:
            data = json.load(f)
    except json.JSONDecodeError as e:
        ctx.add_issue("error", "structure", f"Invalid JSON: {e}", file_path)
        return
    except Exception as e:
        ctx.add_issue("error", "structure", f"Failed to read file: {e}", file_path)
        return

    # Get opportunities array
    if isinstance(data, list):
        opportunities = data
    elif isinstance(data, dict):
        opportunities = data.get("opportunities") or data.get("events") or []
    else:
        ctx.add_issue("error", "structure", "Invalid root format", file_path)
        return

    if not opportunities:
        ctx.add_issue("warning", "structure", "No opportunities found in file", file_path)
        return

    hints_with_placeholders = 0
    hints_without_placeholders = 0
    personal_hints = 0
    camp_rumors = 0

    for opp in opportunities:
        opp_id = opp.get("id", "UNKNOWN")

        # Check required fields
        if not opp_id or opp_id == "UNKNOWN":
            ctx.add_issue("error", "structure", "Opportunity missing 'id' field", file_path)
            continue

        # Check type
        opp_type = opp.get("type")
        if opp_type and opp_type not in VALID_OPPORTUNITY_TYPES:
            ctx.add_issue(
                "warning",
                "structure",
                f"Unknown opportunity type: '{opp_type}' (expected: {VALID_OPPORTUNITY_TYPES})",
                file_path,
                opp_id,
            )

        # Check valid phases
        valid_phases = opp.get("validPhases") or []
        for phase in valid_phases:
            if phase not in VALID_DAY_PHASES:
                ctx.add_issue(
                    "warning",
                    "structure",
                    f"Unknown day phase: '{phase}' (expected: {VALID_DAY_PHASES})",
                    file_path,
                    opp_id,
                )

        # Check for deprecated 'immediate' field (removed 2026-01-04)
        if "immediate" in opp:
            ctx.add_issue(
                "error",
                "deprecated",
                "Field 'immediate' is deprecated (removed 2026-01-04). All opportunities now compete on fitness through orchestrator. Remove this field.",
                file_path,
                opp_id,
            )

        # ==================== HINT VALIDATION ====================

        hint = opp.get("hint", "")
        hint_id = opp.get("hintId", "")

        # Check if hint localization exists
        if hint_id and hint_id not in localization_ids:
            ctx.add_issue(
                "info",
                "reference",
                f"hintId '{hint_id}' not found in enlisted_strings.xml (using fallback)",
                file_path,
                opp_id,
            )

        # Validate hint text if present
        if hint:
            # Check hint length (should be brief - under 10 words)
            word_count = len(hint.split())
            if word_count > 12:
                ctx.add_issue(
                    "warning",
                    "style",
                    f"Hint too long ({word_count} words, max 10): '{hint[:50]}...'",
                    file_path,
                    opp_id,
                )

            # Check for placeholder usage (recommended for personalization)
            has_placeholder = any(token in hint for token in RECOMMENDED_HINT_PLACEHOLDERS)
            if has_placeholder:
                hints_with_placeholders += 1
            else:
                hints_without_placeholders += 1
                # Only warn for camp rumors (social activities), not personal hints
                if not _is_personal_hint(hint):
                    ctx.add_issue(
                        "info",
                        "style",
                        f"Camp rumor hint could use placeholders for personalization: '{hint}'",
                        file_path,
                        opp_id,
                    )

            # Categorize hint type
            if _is_personal_hint(hint):
                personal_hints += 1
            else:
                camp_rumors += 1

            # Check for UI-style text (should be narrative)
            if any(
                phrase in hint.lower()
                for phrase in ["available", "opportunity", "option", "click", "select"]
            ):
                ctx.add_issue(
                    "warning",
                    "style",
                    f"Hint sounds like UI text, should be narrative: '{hint}'",
                    file_path,
                    opp_id,
                )

        # Check localization field order (hint should follow hintId)
        keys = list(opp.keys())
        if "hintId" in keys and "hint" in keys:
            hint_id_idx = keys.index("hintId")
            hint_idx = keys.index("hint")
            if hint_idx != hint_id_idx + 1:
                ctx.add_issue(
                    "info",
                    "structure",
                    "Fallback 'hint' should immediately follow 'hintId' (field ordering)",
                    file_path,
                    opp_id,
                )

    # Summary stats
    total_opps = len(opportunities)
    opps_with_hints = sum(1 for o in opportunities if o.get("hint"))

    if opps_with_hints < total_opps:
        missing = total_opps - opps_with_hints
        ctx.add_issue(
            "info",
            "completeness",
            f"{missing}/{total_opps} opportunities missing hints (foreshadowing won't appear in Daily Brief)",
            file_path,
        )

    print(f"    Opportunities: {total_opps} total, {opps_with_hints} with hints")
    print(f"    Hint categories: {camp_rumors} camp rumors, {personal_hints} personal hints")
    print(
        f"    Placeholder usage: {hints_with_placeholders} with, {hints_without_placeholders} without"
    )


def _is_personal_hint(hint: str) -> bool:
    """Determine if a hint is personal (player-specific) vs camp rumor."""
    if not hint:
        return False
    lower = hint.lower()

    # Personal hints start with "Your" or "You"
    if lower.startswith("your ") or lower.startswith("you ") or lower.startswith("you'"):
        return True

    # Medical/health terms are personal
    return bool(
        any(
            term in lower
            for term in ["condition", "wound", "injury", "worsening", "pushing yourself"]
        )
    )


# ============================================================================
# Phase 6: Config File Validation
# ============================================================================


def validate_config_files(ctx: ValidationContext):
    """Validate configuration JSON files."""
    config_path = Path("ModuleData/Enlisted/Config")
    if not config_path.exists():
        return

    print("[Phase 6] Validating config files...")

    # Validate baggage_config.json
    baggage_config = config_path / "baggage_config.json"
    if baggage_config.exists():
        _validate_baggage_config(baggage_config, ctx)

    # Validate other config files
    for config_file in config_path.glob("*.json"):
        _validate_generic_config(config_file, ctx)


def _validate_baggage_config(file_path: Path, ctx: ValidationContext):
    """Validate baggage_config.json structure and values."""
    try:
        with open(file_path, encoding="utf-8-sig") as f:
            config = json.load(f)
    except Exception as e:
        ctx.add_issue("error", "config", f"Failed to parse: {e}", str(file_path))
        return

    required_sections = [
        "access_windows",
        "timing",
        "emergency_access",
        "rank_gates",
        "lockdown",
        "events",
    ]
    for section in required_sections:
        if section not in config:
            ctx.add_issue("warning", "config", f"Missing section: '{section}'", str(file_path))

    if "timing" in config:
        timing = config["timing"]
        if timing.get("caught_up_chance_percent", 0) > 100:
            ctx.add_issue(
                "error", "config", "caught_up_chance_percent cannot exceed 100", str(file_path)
            )

    if "lockdown" in config:
        lockdown = config["lockdown"]
        threshold = lockdown.get("supply_threshold_percent", 0)
        if threshold < 0 or threshold > 100:
            ctx.add_issue(
                "error",
                "config",
                f"supply_threshold_percent out of range: {threshold}",
                str(file_path),
            )

    if "rank_gates" in config:
        gates = config["rank_gates"]
        for key, value in gates.items():
            if isinstance(value, int) and (value < 1 or value > 9):
                ctx.add_issue(
                    "error",
                    "config",
                    f"Tier value out of range (1-9): {key}={value}",
                    str(file_path),
                )


def _validate_generic_config(file_path: Path, ctx: ValidationContext):
    """Generic JSON config validation (syntax only)."""
    try:
        with open(file_path, encoding="utf-8-sig") as f:
            json.load(f)
    except json.JSONDecodeError as e:
        ctx.add_issue("error", "config", f"Invalid JSON: {e}", str(file_path))


# ============================================================================
# Phase 7: Project Structure Validation (.csproj)
# ============================================================================

# Files that are allowed in root directory (not considered rogue)
ALLOWED_ROOT_FILES = {
    # Project files
    "Enlisted.csproj",
    "Enlisted.sln",
    "Enlisted.sln.DotSettings",
    "SubModule.xml",
    "packages.config",
    # Documentation
    "README.md",
    "LICENSE",
    "WARP.md",
    "AGENTS.md",
    "CLAUDE.md",
    "Directory.Build.props",
    # Git/Editor config
    ".gitignore",
    ".gitattributes",
    ".editorconfig",
    ".cursorignore",
    ".cursorrules",
    # Lint config
    "ruff.toml",
    "PSScriptAnalyzerSettings.psd1",
}

# Allowed root directories (includes IDE/editor config folders)
ALLOWED_ROOT_DIRS = {
    # Project folders
    "src",
    "docs",
    "Tools",
    "ModuleData",
    "GUI",
    "Properties",
    # Build output
    "obj",
    "bin",
    "packages",
    # Version control
    ".git",
    ".github",
    # IDE/Editor config folders
    ".vs",
    ".idea",
    ".vscode",
    ".cursor",
    ".codex",
    ".claude",
    ".superpowers",
    ".ai",
    ".gemini",
    ".worktrees",
    ".pytest_cache",
    ".ruff_cache",
    ".qodo",
    ".rider",
}


def validate_csproj(ctx: ValidationContext):
    """
    Validate project structure and .csproj file completeness.

    Checks:
    1. All .cs files in src/ are included in .csproj
    2. All files in .csproj actually exist
    3. GUI assets are properly included
    4. No rogue files in root directory
    """
    csproj_path = Path("Enlisted.csproj")
    if not csproj_path.exists():
        ctx.add_issue("error", "project", "Enlisted.csproj not found", "Enlisted.csproj")
        return

    print("[Phase 7] Validating project structure...")

    try:
        # Parse .csproj XML (handle MSBuild namespace)
        tree = ET.parse(csproj_path)
        root = tree.getroot()

        # Handle MSBuild namespace
        ns = {"msbuild": "http://schemas.microsoft.com/developer/msbuild/2003"}

        # Extract all <Compile Include="..."/> entries
        compile_includes = set()
        for compile_elem in root.findall(".//msbuild:Compile", ns):
            include = compile_elem.get("Include")
            if include:
                # Normalize path separators and handle wildcards
                normalized = include.replace("\\", "/")
                if "*" in normalized:
                    # Wildcard - expand it
                    pattern = normalized.replace("/", os.sep)
                    for match in glob.glob(pattern, recursive=True):
                        compile_includes.add(Path(match).as_posix())
                else:
                    compile_includes.add(normalized)

        # Extract all <None Include="..."/> entries (documentation, tools)
        none_includes = set()
        for none_elem in root.findall(".//msbuild:None", ns):
            include = none_elem.get("Include")
            if include:
                normalized = include.replace("\\", "/")
                if "*" in normalized:
                    pattern = normalized.replace("/", os.sep)
                    for match in glob.glob(pattern, recursive=True):
                        none_includes.add(Path(match).as_posix())
                else:
                    none_includes.add(normalized)

        # Extract all <Content Include="..."/> entries (GUI, configs)
        content_includes = set()
        for content_elem in root.findall(".//msbuild:Content", ns):
            include = content_elem.get("Include")
            if include:
                normalized = include.replace("\\", "/")
                content_includes.add(normalized)

        # --- Check 1: All .cs files in src/ are in .csproj ---
        src_path = Path("src")
        if src_path.exists():
            actual_cs_files = set()
            for cs_file in src_path.rglob("*.cs"):
                actual_cs_files.add(cs_file.as_posix())

            # Find .cs files not in .csproj
            missing_from_csproj = actual_cs_files - compile_includes
            for missing in sorted(missing_from_csproj):
                ctx.add_issue(
                    "error",
                    "project",
                    f'C# file not in .csproj: {missing} (add <Compile Include="{missing.replace("/", chr(92))}"/>)',
                    "Enlisted.csproj",
                )

            if not missing_from_csproj:
                ctx.add_issue(
                    "info",
                    "project",
                    f"All {len(actual_cs_files)} C# files in src/ are in .csproj",
                    "Enlisted.csproj",
                )

        # --- Check 2: All compiled files in .csproj exist ---
        for include in compile_includes:
            if "*" not in include:  # Skip wildcards
                file_path = Path(include.replace("/", os.sep))
                if not file_path.exists():
                    ctx.add_issue(
                        "error",
                        "project",
                        f"File in .csproj does not exist: {include} (remove from .csproj or restore file)",
                        "Enlisted.csproj",
                    )

        # --- Check 3: GUI assets are properly included ---
        gui_path = Path("GUI")
        if gui_path.exists():
            actual_gui_files = set()
            for xml_file in gui_path.rglob("*.xml"):
                actual_gui_files.add(xml_file.as_posix())

            missing_gui = actual_gui_files - content_includes
            for missing in sorted(missing_gui):
                ctx.add_issue(
                    "warning", "project", f"GUI asset not in .csproj: {missing}", "Enlisted.csproj"
                )

        # --- Check 4: No rogue files in root directory ---
        root_path = Path(".")
        rogue_files = []
        rogue_dirs = []

        for item in root_path.iterdir():
            if item.is_file():
                if item.name not in ALLOWED_ROOT_FILES:
                    # Check if it's a generated/temporary/user-specific file
                    if item.suffix in {".log", ".tmp", ".bak", ".user"}:
                        continue  # Skip temp/user files
                    if item.name.startswith("."):
                        continue  # Skip hidden files
                    if item.name.endswith(".user"):
                        continue  # Skip user-specific files (e.g., .sln.DotSettings.user)
                    rogue_files.append(item.name)
            elif item.is_dir() and item.name not in ALLOWED_ROOT_DIRS:
                rogue_dirs.append(item.name)

        for rogue in sorted(rogue_files):
            # Determine suggestion based on file type
            if rogue.endswith(".py"):
                suggestion = "move to Tools/Research/ or Tools/Validation/"
            elif rogue.endswith(".ps1"):
                suggestion = "move to Tools/Debugging/ or Tools/Steam/"
            elif rogue.endswith(".md"):
                suggestion = "move to docs/ or Tools/Debugging/"
            elif rogue.endswith(".txt"):
                suggestion = "move to Tools/Debugging/ or delete"
            else:
                suggestion = "review and relocate or delete"

            ctx.add_issue(
                "warning",
                "project",
                f"Rogue file in root: {rogue} ({suggestion})",
                "Enlisted.csproj",
            )

        for rogue in sorted(rogue_dirs):
            ctx.add_issue(
                "warning",
                "project",
                f"Unexpected directory in root: {rogue}/ (should this be in Tools/ or docs/?)",
                "Enlisted.csproj",
            )

        # --- Check 5: Tools documentation coverage ---
        tools_path = Path("Tools")
        if tools_path.exists():
            tools_md_files = set()
            for md_file in tools_path.rglob("*.md"):
                tools_md_files.add(md_file.as_posix())

            # Check if Tools README exists
            if not (tools_path / "README.md").exists():
                ctx.add_issue(
                    "warning",
                    "project",
                    "Tools/README.md missing - tools folder should have documentation",
                    "Enlisted.csproj",
                )

        # --- Check 6: Content subdirectories are deployed ---
        # Verify that all content subdirectories in ModuleData have corresponding
        # ItemGroup entries and AfterBuild copy commands in .csproj
        content_dirs_to_check = [
            # Add content subdirectories here as needed.
        ]

        csproj_content = csproj_path.read_text(encoding="utf-8")

        for source_dir, item_group_name, pattern in content_dirs_to_check:
            source_path = Path(source_dir.replace("/", os.sep))
            if source_path.exists():
                # Directory exists in source - check if .csproj will deploy it
                has_itemgroup = item_group_name in csproj_content
                has_copy = f"@({item_group_name})" in csproj_content

                if not has_itemgroup:
                    ctx.add_issue(
                        "error",
                        "project",
                        f"Content directory '{source_dir}' exists but no ItemGroup '{item_group_name}' in .csproj. "
                        f'Add: <{item_group_name} Include="{pattern}"/>',
                        "Enlisted.csproj",
                    )
                elif not has_copy:
                    ctx.add_issue(
                        "error",
                        "project",
                        f"ItemGroup '{item_group_name}' exists but no Copy command. "
                        f'Add: <Copy SourceFiles="@({item_group_name})" DestinationFolder="..."/>',
                        "Enlisted.csproj",
                    )
                else:
                    # Count files that will be deployed
                    json_files = list(source_path.glob("*.json"))
                    ctx.add_issue(
                        "info",
                        "project",
                        f"Content directory '{source_dir}' ({len(json_files)} files) configured for deployment",
                        "Enlisted.csproj",
                    )

        # --- Summary ---
        total_cs = len(compile_includes)
        total_content = len(content_includes)
        total_none = len(none_includes)
        ctx.add_issue(
            "info",
            "project",
            f"Project includes: {total_cs} compiled files, {total_content} content files, {total_none} documentation files",
            "Enlisted.csproj",
        )

    except ET.ParseError as e:
        ctx.add_issue("error", "project", f"Failed to parse .csproj XML: {e}", "Enlisted.csproj")
    except Exception as e:
        ctx.add_issue("error", "project", f"Error validating .csproj: {e}", "Enlisted.csproj")


# ============================================================================
# Phase 8: Code Quality Validation (Sea Context Detection)
# ============================================================================


def validate_code_quality(ctx: ValidationContext):
    """
    Validate C# code for common anti-patterns and bugs.

    Currently checks:
    1. IsCurrentlyAtSea usage without proper settlement/siege guards
       (prevents "Rigging Check" appearing when on land in settlements)
    2. Hardcoded module paths that break Steam Workshop installs
       (must use ModulePaths utility instead of hardcoded "Modules", "Enlisted" paths)

    Guard patterns recognized:
    - Direct: party.CurrentSettlement == null && party.BesiegedSettlement == null && party.IsCurrentlyAtSea
    - Early-return: if (settlement != null) return; ... if (IsCurrentlyAtSea) (within 20 lines)
    - Alternative siege: BesiegerCamp, SiegeEvent, Party?.SiegeEvent

    Whitelisted patterns:
    - Diagnostic/logging only (ModLogger.Debug/Info, AtSea= in log strings)
    - Harmony patches that report raw values for debugging
    """
    print("[Phase 8] Validating code quality patterns...")

    # Check 1: Hardcoded module paths (breaks Steam Workshop)
    _validate_no_hardcoded_paths(ctx)

    src_path = Path("src")
    if not src_path.exists():
        ctx.add_issue(
            "warning", "project", "Source directory not found, skipping code quality checks", "src/"
        )
        return

    # Whitelisted files (diagnostic logging only, not gameplay logic)
    WHITELISTED_FILES = {
        "NavalNavigationCapabilityPatch.cs",  # Harmony patch - diagnostic logging only
    }

    cs_files = list(src_path.rglob("*.cs"))
    issues_found = 0
    files_with_issues = set()

    for cs_file in cs_files:
        # Skip whitelisted files entirely
        if cs_file.name in WHITELISTED_FILES:
            continue

        try:
            content = cs_file.read_text(encoding="utf-8")
            lines = content.split("\n")
        except Exception:
            continue  # Skip unreadable files

        for i, line in enumerate(lines):
            # Skip comments
            if line.strip().startswith("//") or line.strip().startswith("*"):
                continue

            # Look for IsCurrentlyAtSea usage
            if "IsCurrentlyAtSea" not in line:
                continue

            # Skip if this line is purely diagnostic logging
            if _is_diagnostic_logging(line):
                continue

            # Extract extended context for early-return pattern detection
            # Look 20 lines back for guards (early-return patterns)
            # Look 10 lines forward for context
            guard_start = max(0, i - 20)
            context_end = min(len(lines), i + 11)

            guard_context = "\n".join(
                lines[guard_start:i]
            )  # Lines before (for early-return guards)
            full_context = "\n".join(lines[guard_start:context_end])  # Full context

            # Check for proper safety patterns
            has_settlement = _has_settlement_guard(guard_context, full_context)
            has_siege = _has_siege_guard(guard_context, full_context)

            # If both guards present (directly or via early-return), it's OK
            if has_settlement and has_siege:
                continue

            # Bannerlord itself uses raw sea-state checks for scene/tag/captivity flows.
            # Only content-routing style checks should require settlement/siege guards.
            is_ui_only = any(
                keyword in full_context
                for keyword in [
                    "conversation_scene_sea",
                    "seaConversationScene",
                    "OpenConversationMission",
                    "CampaignMission.Open",
                    "CampaignMapConversation",  # Scene selection for conversations
                    "Conversation",
                    "Dialog",
                    "dialog",
                    "PlayerIsAtSeaTag",
                    "NPCIsInSeaTag",
                ]
            )

            # Check if this is game state sync (intentional, not content filtering)
            # Patterns:
            # - Assignment: main.IsCurrentlyAtSea = lordParty.IsCurrentlyAtSea (syncing sea state)
            # - Comparison for sync: main.IsCurrentlyAtSea != lordParty.IsCurrentlyAtSea (checking mismatch to fix)
            # - Naval battle context: IsNavalMapEvent && ... IsCurrentlyAtSea (battle state handling)
            is_state_sync = bool(re.search(r"IsCurrentlyAtSea\s*=\s*\w+\.IsCurrentlyAtSea", line))
            is_sync_check = bool(re.search(r"IsCurrentlyAtSea\s*!=\s*\w+\.IsCurrentlyAtSea", line))
            is_naval_battle = "IsNavalMapEvent" in full_context and "IsCurrentlyAtSea" in line
            is_recovery_or_captivity = any(
                keyword in full_context
                for keyword in [
                    "EndCaptivity",
                    "captor",
                    "taken_prisoner",
                    "prisoner",
                    "disembark",
                    "Disembark",
                    "wasAtSea",
                    "captorAtSea",
                    "teleport",
                    "Teleport",
                    "raft state",
                    "SetMoveModeHold",
                    "Warsails DLC compatibility",
                    "HasNavalNavigationCapability",
                    "army_wait menu",
                    "state issues",
                ]
            )
            if (
                is_state_sync
                or is_sync_check
                or is_naval_battle
                or is_ui_only
                or is_recovery_or_captivity
            ):
                continue  # State sync/battle handling is intentional, skip validation

            # Check if this is content filtering (high risk)
            is_content_filter = any(
                keyword in full_context
                for keyword in [
                    "isAtSea",
                    "atSea",
                    "NotAtSea",
                    "variant",
                    "filter",
                    "eligible",
                    "available",
                    "requirement",
                    "DetectTravelContext",
                ]
            )

            # Determine severity
            if is_content_filter and not has_settlement and not has_siege:
                # Content filtering without guards - critical
                severity = "error"
                issues_found += 1
                files_with_issues.add(str(cs_file))
            elif is_ui_only and (has_settlement or has_siege):
                continue  # UI with at least one guard is acceptable
            elif has_settlement or has_siege:
                severity = "warning"
                issues_found += 1
            else:
                severity = "warning"
                issues_found += 1

            guard_status = f"Settlement={has_settlement}, Siege={has_siege}"
            ctx.add_issue(
                severity,
                "code_quality",
                f"Line {i + 1}: IsCurrentlyAtSea without full settlement/siege guards ({guard_status}). "
                f"Add: party.CurrentSettlement == null && party.BesiegedSettlement == null",
                str(cs_file),
                None,
            )

    if issues_found == 0:
        ctx.add_issue(
            "info",
            "code_quality",
            f"All {len(cs_files)} C# files pass sea context detection checks",
            "src/",
        )
    else:
        ctx.add_issue(
            "info",
            "code_quality",
            f"Sea context detection: {issues_found} issue(s) in {len(files_with_issues)} file(s). "
            f"See: docs/Features/Content/event-system-schemas.md for the canonical pattern.",
            "src/",
        )


def _validate_no_hardcoded_paths(ctx: ValidationContext):
    """
    Check for hardcoded module paths that break Steam Workshop installs.

    CRITICAL BUG PREVENTION:
    Steam Workshop installs to: steamapps/workshop/content/261550/3621116083/
    Manual/Nexus installs to:   steamapps/common/Mount & Blade II Bannerlord/Modules/Enlisted/

    Code using hardcoded "Modules", "Enlisted" paths will ONLY work for manual installs!
    Must use ModulePaths utility (which calls ModuleHelper.GetModuleFullPath).

    Whitelisted:
    - ModulePaths.cs itself (the utility that provides correct paths)
    - Comments and string literals in documentation/logging
    - Test files
    """
    src_path = Path("src")
    if not src_path.exists():
        return

    # Files that are allowed to have the hardcoded patterns (they ARE the fix)
    WHITELISTED_FILES = {
        "ModulePaths.cs",  # The utility itself uses these as fallbacks
    }

    # Patterns that indicate hardcoded module paths (BREAKS WORKSHOP!)
    HARDCODED_PATH_PATTERNS = [
        # Path.Combine with "Modules" and "Enlisted" strings
        (
            r'Path\.Combine\s*\([^)]*"Modules"[^)]*"Enlisted"',
            'Path.Combine with hardcoded "Modules", "Enlisted" - use ModulePaths.GetContentPath() or ModulePaths.ModuleRoot',
        ),
        # BasePath.Name combined with Modules/Enlisted
        (
            r'BasePath\.Name[^;]*"Modules"[^;]*"Enlisted"',
            'BasePath.Name with "Modules/Enlisted" - use ModulePaths utility instead',
        ),
        # Direct string paths with Modules/Enlisted
        (
            r'"[^"]*\\\\Modules\\\\Enlisted[^"]*"',
            "Hardcoded path string with Modules\\Enlisted - use ModulePaths utility",
        ),
        (
            r'"[^"]*/Modules/Enlisted[^"]*"',
            "Hardcoded path string with Modules/Enlisted - use ModulePaths utility",
        ),
    ]

    cs_files = list(src_path.rglob("*.cs"))
    issues_found = 0

    for cs_file in cs_files:
        # Skip whitelisted files
        if cs_file.name in WHITELISTED_FILES:
            continue

        try:
            content = cs_file.read_text(encoding="utf-8")
            lines = content.split("\n")
        except Exception:
            continue

        for i, line in enumerate(lines):
            # Skip pure comments
            stripped = line.strip()
            if stripped.startswith("//") or stripped.startswith("*") or stripped.startswith("///"):
                continue

            # Check each pattern
            for pattern, message in HARDCODED_PATH_PATTERNS:
                if re.search(pattern, line, re.IGNORECASE):
                    issues_found += 1
                    ctx.add_issue(
                        "error",
                        "code_quality",
                        f"Line {i + 1}: HARDCODED MODULE PATH - {message}. "
                        f"This breaks Steam Workshop installs! "
                        f"Workshop users get files in steamapps/workshop/content/, not Modules/.",
                        str(cs_file),
                        None,
                    )
                    break  # Only report first match per line

    if issues_found == 0:
        ctx.add_issue(
            "info",
            "code_quality",
            f"All {len(cs_files)} C# files use ModulePaths utility correctly (no hardcoded paths)",
            "src/",
        )
    else:
        ctx.add_issue(
            "error",
            "code_quality",
            f"CRITICAL: {issues_found} hardcoded module path(s) found! "
            f"These BREAK Steam Workshop installs. Use ModulePaths.GetContentPath() or ModulePaths.ModuleRoot instead.",
            "src/",
        )


def _is_diagnostic_logging(line: str) -> bool:
    """Check if this line is purely diagnostic logging (not gameplay logic)."""
    logging_patterns = [
        "ModLogger.Debug",
        "ModLogger.Info",
        "ModLogger.Warn",
        "ModLogger.Error",
        "Debug.Log",
        "Console.Write",
        "AtSea=",  # Log string pattern like "[AtSea={value}]"
        '$"',
        '+ "',  # String interpolation/concatenation in logging
    ]
    # Only skip if it's JUST logging (not also assigning to a variable)
    line_stripped = line.strip()
    return any(pattern in line for pattern in logging_patterns) and (
        "=" not in line_stripped or line_stripped.startswith("ModLogger") or "AtSea=" in line
    )


def _has_settlement_guard(guard_context: str, full_context: str) -> bool:
    """Check for CurrentSettlement guard in context."""
    return bool(re.search(r"CurrentSettlement\s*[!=]=\s*null", full_context, re.IGNORECASE))


def _has_siege_guard(guard_context: str, full_context: str) -> bool:
    """
    Check for siege guard in context.
    Recognizes multiple equivalent patterns:
    - BesiegedSettlement != null / == null
    - BesiegerCamp != null
    - SiegeEvent != null / Party?.SiegeEvent != null
    """
    siege_patterns = [
        r"BesiegedSettlement\s*[!=]=\s*null",
        r"BesiegerCamp\s*[!=]=\s*null",
        r"SiegeEvent\s*[!=]=\s*null",
        r"Party\??\s*\.\s*SiegeEvent\s*[!=]=\s*null",
    ]
    return any(re.search(pattern, full_context, re.IGNORECASE) for pattern in siege_patterns)


# ============================================================================
# Phase 9: C# TextObject Localization Validation
# ============================================================================


def validate_csharp_textobjects(ctx: ValidationContext, localization_ids: set[str]):
    """
    Scan C# files for TextObject("{=string_id}...") patterns and verify
    that string_ids exist in enlisted_strings.xml.

    This catches missing XML strings that are referenced from code rather than JSON.
    Also tracks references for complete orphan detection.

    Whitelisted prefixes (debug/internal strings that don't need localization):
    - dbg_ (debug tool messages)
    - test_ (test messages)
    - internal_ (internal system messages)
    """
    print("[Phase 9] Validating C# TextObject string references...")

    src_path = Path("src")
    if not src_path.exists():
        ctx.add_issue(
            "info", "project", "Source directory not found, skipping C# TextObject checks", "src/"
        )
        return

    # Prefixes that don't require localization (debug/internal strings)
    WHITELIST_PREFIXES = ("dbg_", "test_", "internal_", "debug_")

    # Files that are excluded from validation (debug tools, test files)
    WHITELIST_FILES = ("DebugToolsBehavior.cs", "TestBehavior.cs")

    # Pattern to match TextObject("{=string_id}...") where string_id is captured
    # Matches: new TextObject("{=my_string_id}Some fallback text")
    # Also matches: TextObject("{=my_string_id}...")
    textobject_pattern = re.compile(r'TextObject\s*\(\s*["\']?\{=([a-zA-Z0-9_]+)\}', re.MULTILINE)

    total_refs = 0
    missing_refs = 0
    skipped_debug = 0
    files_scanned = 0
    missing_by_file = defaultdict(list)

    for cs_file in src_path.rglob("*.cs"):
        # Skip whitelisted files (debug tools)
        if cs_file.name in WHITELIST_FILES:
            continue

        try:
            content = cs_file.read_text(encoding="utf-8-sig")
            files_scanned += 1

            # Find all TextObject string references
            matches = textobject_pattern.findall(content)

            for string_id in matches:
                total_refs += 1
                ctx.track_string_reference(string_id)

                # Skip whitelisted prefixes (debug strings)
                if string_id.startswith(WHITELIST_PREFIXES):
                    skipped_debug += 1
                    continue

                if string_id not in localization_ids:
                    missing_refs += 1
                    relative_path = str(cs_file.relative_to(Path(".")))
                    missing_by_file[relative_path].append(string_id)

        except Exception as e:
            ctx.add_issue(
                "warning",
                "code_quality",
                f"Failed to read file for TextObject scan: {e}",
                str(cs_file),
            )

    # Report missing string references
    for file_path, missing_ids in sorted(missing_by_file.items()):
        for string_id in missing_ids:
            ctx.add_issue(
                "warning",
                "reference",
                f"TextObject string '{string_id}' not found in enlisted_strings.xml",
                file_path,
                string_id,
            )

    # Summary info
    if total_refs > 0:
        ctx.add_issue(
            "info",
            "code_quality",
            f"C# TextObject scan: {total_refs} refs in {files_scanned} files, {missing_refs} missing, {skipped_debug} debug strings skipped",
            "csharp_textobjects",
        )


def validate_camp_schedule_descriptions(ctx: ValidationContext):
    """
    Validate that camp_schedule.json has meaningful descriptions for all phases.
    These descriptions are displayed to the player in status forecasts.
    """
    schedule_path = Path("ModuleData/Enlisted/Config/camp_schedule.json")
    if not schedule_path.exists():
        return

    try:
        with open(schedule_path, encoding="utf-8-sig") as f:
            data = json.load(f)
    except Exception as e:
        ctx.add_issue(
            "warning", "config", f"Failed to read camp_schedule.json: {e}", str(schedule_path)
        )
        return

    phases = data.get("phases", {})
    for phase_name, phase_data in phases.items():
        # Check slot1 description
        slot1 = phase_data.get("slot1", {})
        slot1_desc = slot1.get("description", "")
        if not slot1_desc or len(slot1_desc) < 5:
            ctx.add_issue(
                "warning",
                "config",
                f"Phase '{phase_name}' slot1 has empty or too short description",
                str(schedule_path),
                phase_name,
            )

        # Check slot2 description
        slot2 = phase_data.get("slot2", {})
        slot2_desc = slot2.get("description", "")
        if not slot2_desc or len(slot2_desc) < 5:
            ctx.add_issue(
                "warning",
                "config",
                f"Phase '{phase_name}' slot2 has empty or too short description",
                str(schedule_path),
                phase_name,
            )

        # Check flavor text (optional but recommended)
        flavor = phase_data.get("flavor", "")
        if not flavor:
            ctx.add_issue(
                "info",
                "config",
                f"Phase '{phase_name}' has no flavor text (optional)",
                str(schedule_path),
                phase_name,
            )


_MODLOGGER_ERROR_RE = re.compile(r"\bModLogger\.Error\s*\(")


def _strip_csharp_comments(text: str) -> str:
    """Strip C# // line comments and /* ... */ block comments from source
    text, replacing them with whitespace so line numbers are preserved.
    Mirrors generate_error_codes.py:strip_comments — kept inline here to
    avoid a sys.path import dance."""
    block = re.compile(r"/\*.*?\*/", re.DOTALL)
    line = re.compile(r"//[^\n]*")

    def blank(m):
        return "".join("\n" if ch == "\n" else " " for ch in m.group(0))

    text = block.sub(blank, text)
    text = line.sub(blank, text)
    return text


def validate_no_modlogger_error_calls(ctx: ValidationContext):
    """Phase 11: confirm no ModLogger.Error(...) calls remain in src/.

    The Error API was retired on 2026-04-19. Every error-tier log must
    use ModLogger.Surfaced / Caught / Expected. See
    docs/superpowers/specs/2026-04-19-error-warn-cleanup-design.md."""
    print("[Phase 11] Validating no ModLogger.Error(...) calls remain...")
    src_root = Path(__file__).parent.parent.parent / "src"
    if not src_root.is_dir():
        ctx.add_issue(
            "error",
            "logging-api",
            f"src/ directory not found at {src_root}",
            str(src_root),
        )
        return

    found = 0
    for cs_file in src_root.rglob("*.cs"):
        try:
            text = cs_file.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            text = cs_file.read_text(encoding="utf-8-sig")
        stripped = _strip_csharp_comments(text)
        for lineno, line in enumerate(stripped.splitlines(), 1):
            if _MODLOGGER_ERROR_RE.search(line):
                ctx.add_issue(
                    "error",
                    "logging-api",
                    "ModLogger.Error is retired. Use Surfaced / Caught / Expected. "
                    "See docs/superpowers/specs/2026-04-19-error-warn-cleanup-design.md.",
                    str(cs_file.relative_to(src_root.parent)),
                    str(lineno),
                )
                found += 1
    if found == 0:
        print("  OK: zero ModLogger.Error(...) call sites in src/.")


def validate_error_code_registry(ctx: ValidationContext):
    """Phase 10: Invoke generate_error_codes.py --check to confirm the
    on-disk docs/error-codes.md is in sync with ModLogger.Surfaced(...)
    call sites. Any drift or scan error fails validation."""
    print("[Phase 10] Validating error-code registry sync...")
    script = Path(__file__).parent / "generate_error_codes.py"
    try:
        result = subprocess.run(
            [sys.executable, str(script), "--check"],
            capture_output=True,
            text=True,
            timeout=60,
        )
    except Exception as ex:
        ctx.add_issue(
            "error",
            "error-codes",
            f"Failed to invoke generate_error_codes.py: {ex}",
            str(script),
        )
        return

    if result.returncode != 0:
        stderr = (result.stderr or "").strip() or "(no stderr)"
        ctx.add_issue(
            "error",
            "error-codes",
            f"Registry out of sync or contains issues. "
            f"Fix: run `python Tools/Validation/generate_error_codes.py`. "
            f"Details: {stderr}",
            str(script),
        )
    else:
        stdout = (result.stdout or "").strip()
        if stdout:
            print(f"  {stdout}")


# ============================================================================
# Phase 13 configuration — duty-pool skill coverage enforcement.
# ============================================================================

_DUTY_PROFILES = [
    "garrisoned", "marching", "besieging", "raiding",
    "escorting", "wandering", "imprisoned"
]

_BANNERLORD_SKILLS = [
    "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing",
    "Riding", "Athletics", "Scouting", "Tactics", "Trade", "Leadership",
    "Charm", "Roguery", "Steward", "Medicine", "Engineering", "Smithing"
]


# ============================================================================
# Phase 12: Storylet Reference Validation
# ============================================================================

# Read-through quality ids registered in QualityBehavior.RegisterDefinitions
# that are NOT stored in quality_defs.json (they proxy live game state).
_READ_THROUGH_QUALITY_IDS: set[str] = {
    "rank_xp",
    "days_in_rank",
    "battles_survived",
    "rank",
    "days_enlisted",
    "lord_relation",
}

# Subset of read-throughs that storylets MUST NOT write (they have no setter).
_READ_ONLY_QUALITY_IDS: set[str] = {"rank", "days_in_rank", "days_enlisted", "committed_path"}

# Primitive effect ids understood by EffectExecutor.
_PRIMITIVE_EFFECT_IDS: set[str] = {
    "quality_add",
    "quality_add_for_hero",
    "quality_set",
    "set_flag",
    "clear_flag",
    "set_flag_on_hero",
    "trait_xp",
    "skill_xp",
    "give_gold",
    "give_item",
    "grant_attribute_level",
    "grant_focus_point",
    "grant_renown",
    "grant_skill_level",
    "grant_unspent_attribute",
    "grant_unspent_focus",
    "relation_change",
    "set_trait_level",
    "grant_item",
    "grant_random_item_from_pool",
    "start_arc",
    "clear_active_named_order",
    "commit_path",
    "resist_path",
}

# Primitives that take a 'quality' parameter.
_QUALITY_WRITING_PRIMITIVES: set[str] = {
    "quality_add",
    "quality_add_for_hero",
    "quality_set",
}


def _load_quality_ids(repo_root: Path, ctx: ValidationContext) -> set[str]:
    """Load valid quality ids from quality_defs.json union read-through ids."""
    defs_path = repo_root / "ModuleData" / "Enlisted" / "Qualities" / "quality_defs.json"
    quality_ids: set[str] = set(_READ_THROUGH_QUALITY_IDS)
    if not defs_path.exists():
        ctx.add_issue(
            "warning",
            "storylet-refs",
            "quality_defs.json not found; quality id cross-check skipped.",
            str(defs_path),
        )
        return quality_ids
    try:
        with open(defs_path, encoding="utf-8-sig") as f:
            data = json.load(f)
        for entry in data.get("qualities", []):
            qid = entry.get("id", "")
            if qid:
                quality_ids.add(qid)
        print(
            f"  Loaded {len(data.get('qualities', []))} quality definitions "
            f"from quality_defs.json (+{len(_READ_THROUGH_QUALITY_IDS)} read-throughs)."
        )
    except Exception as ex:
        ctx.add_issue(
            "error",
            "storylet-refs",
            f"Failed to parse quality_defs.json: {ex}",
            str(defs_path),
        )
    return quality_ids


def _load_scripted_effect_ids(
    repo_root: Path, ctx: ValidationContext, quality_ids: set[str]
) -> set[str]:
    """Load the set of top-level effect keys from scripted_effects.json.

    Also validates that every quality_add / quality_add_for_hero / quality_set
    inside an effect body references a known quality id.
    """
    effects_path = repo_root / "ModuleData" / "Enlisted" / "Effects" / "scripted_effects.json"
    scripted_ids: set[str] = set()
    if not effects_path.exists():
        ctx.add_issue(
            "warning",
            "storylet-refs",
            "scripted_effects.json not found; scripted-effect id cross-check skipped.",
            str(effects_path),
        )
        return scripted_ids
    try:
        with open(effects_path, encoding="utf-8-sig") as f:
            data = json.load(f)
        effects_map = data.get("effects", {})
        for effect_key, body in effects_map.items():
            scripted_ids.add(effect_key)
            if not isinstance(body, list):
                continue
            for step in body:
                if not isinstance(step, dict):
                    continue
                apply_id = step.get("apply", "")
                if apply_id in _QUALITY_WRITING_PRIMITIVES:
                    qid = step.get("quality", "")
                    if not qid:
                        ctx.add_issue(
                            "error",
                            "storylet-refs",
                            f"scripted_effects.json: effect '{effect_key}' step '{apply_id}' "
                            f"missing 'quality' parameter.",
                            str(effects_path),
                        )
                    elif qid not in quality_ids:
                        ctx.add_issue(
                            "error",
                            "storylet-refs",
                            f"scripted_effects.json: effect '{effect_key}' step '{apply_id}' "
                            f"references unknown quality id '{qid}'.",
                            str(effects_path),
                        )
                    elif (
                        qid in _READ_ONLY_QUALITY_IDS
                        and apply_id != "quality_set"
                        and apply_id in ("quality_add", "quality_add_for_hero")
                    ):
                        # quality_set is fine for read-only; add/for_hero are not.
                        ctx.add_issue(
                            "error",
                            "storylet-refs",
                            f"scripted_effects.json: effect '{effect_key}' step '{apply_id}' "
                            f"writes to read-only quality '{qid}'.",
                            str(effects_path),
                        )
        print(f"  Loaded {len(scripted_ids)} scripted effect ids from scripted_effects.json.")
    except Exception as ex:
        ctx.add_issue(
            "error",
            "storylet-refs",
            f"Failed to parse scripted_effects.json: {ex}",
            str(effects_path),
        )
    return scripted_ids


def _check_effect_apply(
    apply_id: str,
    step: dict,
    storylet_id: str,
    file_path: str,
    field_path: str,
    valid_effect_ids: set[str],
    quality_ids: set[str],
    ctx: ValidationContext,
):
    """Validate a single .apply value within a storylet effect step."""
    if apply_id not in valid_effect_ids:
        ctx.add_issue(
            "error",
            "storylet-refs",
            f"[{field_path}] unknown effect id '{apply_id}'. "
            f"Must be a scripted-effect key or a primitive id.",
            file_path,
            storylet_id,
        )
        return

    if apply_id in _QUALITY_WRITING_PRIMITIVES:
        qid = step.get("quality", "")
        if not qid:
            ctx.add_issue(
                "error",
                "storylet-refs",
                f"[{field_path}] '{apply_id}' missing 'quality' parameter.",
                file_path,
                storylet_id,
            )
            return
        if qid not in quality_ids:
            ctx.add_issue(
                "error",
                "storylet-refs",
                f"[{field_path}] '{apply_id}' references unknown quality id '{qid}'.",
                file_path,
                storylet_id,
            )
        if apply_id in ("quality_add", "quality_add_for_hero") and qid in _READ_ONLY_QUALITY_IDS:
            read_only_list = ", ".join(sorted(_READ_ONLY_QUALITY_IDS))
            ctx.add_issue(
                "error",
                "storylet-refs",
                f"[{field_path}] '{apply_id}' writes to read-only quality '{qid}' "
                f"({read_only_list} cannot be written by storylets).",
                file_path,
                storylet_id,
            )


def _walk_effect_list(
    effects: Any,
    storylet_id: str,
    file_path: str,
    field_path: str,
    valid_effect_ids: set[str],
    quality_ids: set[str],
    ctx: ValidationContext,
):
    """Walk a list of effect dicts and validate each .apply value."""
    if not isinstance(effects, list):
        return
    for i, step in enumerate(effects):
        if not isinstance(step, dict):
            continue
        apply_id = step.get("apply", "")
        if not apply_id:
            ctx.add_issue(
                "warning",
                "storylet-refs",
                f"[{field_path}[{i}]] effect step missing 'apply' key.",
                file_path,
                storylet_id,
            )
            continue
        _check_effect_apply(
            apply_id,
            step,
            storylet_id,
            file_path,
            f"{field_path}[{i}]",
            valid_effect_ids,
            quality_ids,
            ctx,
        )


def _collect_storylet_ids(storylet_files: list[Path], ctx: ValidationContext) -> set[str]:
    """Pre-pass: gather the set of all declared storylet ids across Storylets/*.json.

    Pool entries in ActivityTypeDefinition JSON must reference a storylet id from
    this set (Spec 1 Phase E, Task 20).
    """
    storylet_ids: set[str] = set()
    for sf in storylet_files:
        try:
            with open(sf, encoding="utf-8-sig") as f:
                data = json.load(f)
        except Exception:
            # Parse failures are reported by _validate_storylet_file on the main walk.
            continue
        storylets = (
            data
            if isinstance(data, list)
            else data.get("storylets", [data] if isinstance(data, dict) else [])
        )
        if not isinstance(storylets, list):
            storylets = [storylets]
        for storylet in storylets:
            if not isinstance(storylet, dict):
                continue
            sid = storylet.get("id", "")
            if sid:
                storylet_ids.add(sid)
    return storylet_ids


def _load_activity_types(
    repo_root: Path, all_storylet_ids: set[str], ctx: ValidationContext
) -> dict[str, set[str]]:
    """Load ModuleData/Enlisted/Activities/*.json — validate phase shapes + pool refs.

    Returns a Dict[activity_type_id, Set[phase_id]] used by the chain.when check.
    (Spec 1 Phase E, Task 20.)
    """
    activity_type_phases: dict[str, set[str]] = {}
    activities_dir = repo_root / "ModuleData" / "Enlisted" / "Activities"
    if not activities_dir.is_dir():
        print("  OK: Activities/ directory absent — no activity types to validate.")
        return activity_type_phases

    activity_files = sorted(p for p in activities_dir.iterdir() if p.suffix == ".json")
    if not activity_files:
        print("  OK: no activity files found.")
        return activity_type_phases

    total_phases = 0
    for path in activity_files:
        try:
            with open(path, encoding="utf-8-sig") as f:
                doc = json.load(f)
        except Exception as ex:
            ctx.add_issue(
                "error",
                "storylet-refs",
                f"Failed to parse activity file: {ex}",
                str(path),
            )
            continue

        type_id = doc.get("id", path.stem) if isinstance(doc, dict) else path.stem
        phases = doc.get("phases", []) if isinstance(doc, dict) else []
        if not isinstance(phases, list):
            ctx.add_issue(
                "error",
                "storylet-refs",
                f"Activity '{type_id}': 'phases' must be an array.",
                str(path),
                type_id,
            )
            continue

        phase_ids: set[str] = set()
        for phase in phases:
            if not isinstance(phase, dict):
                continue
            phase_id = phase.get("id", "")
            if not phase_id:
                ctx.add_issue(
                    "error",
                    "storylet-refs",
                    f"Activity '{type_id}': phase missing 'id'.",
                    str(path),
                    type_id,
                )
                continue
            phase_ids.add(phase_id)

            delivery = phase.get("delivery", "auto")
            if delivery not in ("auto", "player_choice"):
                ctx.add_issue(
                    "error",
                    "storylet-refs",
                    f"Activity '{type_id}' phase '{phase_id}': invalid delivery "
                    f"'{delivery}' (must be 'auto' or 'player_choice').",
                    str(path),
                    type_id,
                )

            pool = phase.get("pool", [])
            if not isinstance(pool, list):
                ctx.add_issue(
                    "error",
                    "storylet-refs",
                    f"Activity '{type_id}' phase '{phase_id}': 'pool' must be an array.",
                    str(path),
                    type_id,
                )
                continue
            for storylet_id in pool:
                if not isinstance(storylet_id, str):
                    ctx.add_issue(
                        "error",
                        "storylet-refs",
                        f"Activity '{type_id}' phase '{phase_id}': pool entries must be strings.",
                        str(path),
                        type_id,
                    )
                    continue
                if storylet_id not in all_storylet_ids:
                    ctx.add_issue(
                        "error",
                        "storylet-refs",
                        f"Activity '{type_id}' phase '{phase_id}': pool references "
                        f"unknown storylet '{storylet_id}'.",
                        str(path),
                        type_id,
                    )

        if type_id in activity_type_phases:
            ctx.add_issue(
                "error",
                "storylet-refs",
                f"Duplicate activity type id '{type_id}' across Activities/*.json.",
                str(path),
                type_id,
            )
        activity_type_phases[type_id] = phase_ids
        total_phases += len(phase_ids)

    plural_types = "type" if len(activity_type_phases) == 1 else "types"
    plural_phases = "phase" if total_phases == 1 else "phases"
    print(
        f"  Loaded {len(activity_type_phases)} activity {plural_types} "
        f"with {total_phases} {plural_phases}."
    )
    return activity_type_phases


def _check_chain_when(
    when: str,
    storylet_id: str,
    opt_id: str,
    file_path: str,
    activity_type_phases: dict[str, set[str]],
    ctx: ValidationContext,
):
    """Validate a single `chain.when` string is a known `{typeId}.{phaseId}` pair."""
    parts = when.split(".")
    if len(parts) != 2 or not parts[0] or not parts[1]:
        ctx.add_issue(
            "error",
            "storylet-refs",
            f"options[{opt_id}].chain.when malformed '{when}' "
            f"(expected '<activity_type>.<phase_id>').",
            file_path,
            storylet_id,
        )
        return
    type_id, phase_id = parts
    if type_id not in activity_type_phases:
        ctx.add_issue(
            "error",
            "storylet-refs",
            f"options[{opt_id}].chain.when references unknown activity type '{type_id}' "
            f"(in '{when}').",
            file_path,
            storylet_id,
        )
        return
    if phase_id not in activity_type_phases[type_id]:
        ctx.add_issue(
            "error",
            "storylet-refs",
            f"options[{opt_id}].chain.when references unknown phase '{type_id}.{phase_id}'.",
            file_path,
            storylet_id,
        )


def _validate_storylet_file(
    file_path: str,
    valid_effect_ids: set[str],
    quality_ids: set[str],
    activity_type_phases: dict[str, set[str]],
    ctx: ValidationContext,
):
    """Validate effect references + chain.when shapes in a single storylet JSON file."""
    try:
        with open(file_path, encoding="utf-8-sig") as f:
            data = json.load(f)
    except Exception as ex:
        ctx.add_issue("error", "storylet-refs", f"Failed to parse storylet file: {ex}", file_path)
        return

    storylets = (
        data
        if isinstance(data, list)
        else data.get("storylets", [data] if isinstance(data, dict) else [])
    )
    if not isinstance(storylets, list):
        storylets = [storylets]

    for storylet in storylets:
        if not isinstance(storylet, dict):
            continue
        sid = storylet.get("id", Path(file_path).stem)

        # immediate[*].apply
        _walk_effect_list(
            storylet.get("immediate", []),
            sid,
            file_path,
            "immediate",
            valid_effect_ids,
            quality_ids,
            ctx,
        )

        # options[*].effects / effectsSuccess / effectsFailure + chain.when
        for opt_idx, option in enumerate(storylet.get("options", [])):
            if not isinstance(option, dict):
                continue
            opt_id = option.get("id", f"option_{opt_idx}")
            for field in ("effects", "effectsSuccess", "effectsFailure"):
                _walk_effect_list(
                    option.get(field, []),
                    sid,
                    file_path,
                    f"options[{opt_id}].{field}",
                    valid_effect_ids,
                    quality_ids,
                    ctx,
                )
            chain = option.get("chain")
            if isinstance(chain, dict):
                when = chain.get("when", "")
                if when:
                    _check_chain_when(
                        when,
                        sid,
                        opt_id,
                        file_path,
                        activity_type_phases,
                        ctx,
                    )


def validate_storylet_references(ctx: ValidationContext):
    """Phase 12: Cross-check storylet effect references against quality and
    scripted-effect registries, and validate ActivityTypeDefinition pools +
    chain.when references.

    Checks:
    - Every .apply value must be a known scripted-effect id or primitive id.
    - quality_add / quality_add_for_hero / quality_set must target a registered quality.
    - quality_add / quality_add_for_hero must not target a read-only quality
      (rank, days_in_rank, days_enlisted).
    - scripted_effects.json bodies are also cross-checked against quality ids.
    - ActivityTypeDefinition JSON: every phase.pool entry must resolve to a real
      storylet id; phase.delivery must be 'auto' or 'player_choice'.
    - options[*].chain.when must parse to '<activity_type>.<phase_id>' referencing
      a known activity type and phase.
    """
    print("[Phase 12] Validating storylet effect references...")
    repo_root = Path(__file__).parent.parent.parent

    quality_ids = _load_quality_ids(repo_root, ctx)
    scripted_ids = _load_scripted_effect_ids(repo_root, ctx, quality_ids)

    valid_effect_ids = scripted_ids | _PRIMITIVE_EFFECT_IDS

    storylet_dir = repo_root / "ModuleData" / "Enlisted" / "Storylets"
    if not storylet_dir.exists():
        ctx.add_issue(
            "warning",
            "storylet-refs",
            "ModuleData/Enlisted/Storylets/ directory not found; no storylets to check.",
            str(storylet_dir),
        )
        print("  OK: Storylets/ directory absent — no storylets to validate.")
        return

    storylet_files = sorted(storylet_dir.rglob("*.json"))
    if not storylet_files:
        print("  OK: no storylet files found (Spec 0 — directory is empty).")
        return

    # Pre-pass: gather storylet ids for activity pool validation.
    all_storylet_ids = _collect_storylet_ids(storylet_files, ctx)
    print(f"  Indexed {len(all_storylet_ids)} storylet id(s) across {len(storylet_files)} file(s).")

    # Activities must load before the storylet walk — chain.when depends on
    # the activity_type_phases map.
    activity_type_phases = _load_activity_types(repo_root, all_storylet_ids, ctx)

    print(f"  Checking {len(storylet_files)} storylet file(s) (effect refs + chain.when)...")
    for sf in storylet_files:
        _validate_storylet_file(
            str(sf),
            valid_effect_ids,
            quality_ids,
            activity_type_phases,
            ctx,
        )

    errors_before = sum(
        1 for i in ctx.issues if i.severity == "error" and i.category == "storylet-refs"
    )
    if errors_before == 0:
        print(f"  OK: all {len(storylet_files)} storylet file(s) passed reference checks.")


# ============================================================================
# Main Validation Pipeline
# ============================================================================


def validate_event_file(file_path: str, ctx: ValidationContext, localization_ids: set[str]):
    """Validate a single event JSON file."""
    try:
        with open(file_path, encoding="utf-8-sig") as f:
            data = json.load(f)
    except json.JSONDecodeError as e:
        ctx.add_issue("error", "structure", f"Invalid JSON: {e}", file_path)
        return
    except Exception as e:
        ctx.add_issue("error", "structure", f"Failed to read file: {e}", file_path)
        return

    # SAFETY: Check schema version
    if isinstance(data, dict):
        schema_version = data.get("schemaVersion", 0)
        if schema_version == 1:
            ctx.add_issue(
                "info",
                "structure",
                "Using schema v1 (deprecated). Consider migrating to schema v2.",
                file_path,
                None,
            )
        elif schema_version > 2:
            ctx.add_issue(
                "warning",
                "structure",
                f"Unknown schema version: {schema_version}. Validator may not understand all fields.",
                file_path,
                None,
            )

    # Handle both array format and object format
    if isinstance(data, list):
        events = data
    elif isinstance(data, dict):
        events = data.get("events") or []
    else:
        ctx.add_issue(
            "error",
            "structure",
            "Invalid root format (expected array or object with 'events' key)",
            file_path,
        )
        return

    if not events:
        ctx.add_issue("warning", "structure", "No events found in file", file_path)
        return

    for event in events:
        if not validate_structure(event, file_path, ctx):
            continue
        validate_references(event, file_path, ctx, localization_ids)
        validate_logic(event, file_path, ctx)
        validate_consistency(event, file_path, ctx)


def _load_all_storylets(ctx: ValidationContext) -> list[tuple[Path, dict]]:
    """Load every storylet JSON and return (source_path, storylet_dict) pairs."""
    repo_root = Path(__file__).parent.parent.parent
    storylet_dir = repo_root / "ModuleData" / "Enlisted" / "Storylets"
    if not storylet_dir.exists():
        return []
    entries: list[tuple[Path, dict]] = []
    for sf in sorted(storylet_dir.rglob("*.json")):
        try:
            with open(sf, encoding="utf-8-sig") as f:
                data = json.load(f)
        except Exception as e:
            ctx.add_issue(
                "error", "storylet-load", f"Failed to parse {sf.name}: {e}", str(sf)
            )
            continue
        if isinstance(data, dict) and isinstance(data.get("storylets"), list):
            for s in data["storylets"]:
                if isinstance(s, dict):
                    entries.append((sf, s))
        elif isinstance(data, list):
            for s in data:
                if isinstance(s, dict):
                    entries.append((sf, s))
        elif isinstance(data, dict):
            entries.append((sf, data))
    return entries


# ============================================================================
# Phase 13: Duty-profile pool coverage
# ============================================================================


def phase13_pool_coverage(
    storylet_entries: list[tuple[Path, dict]], ctx: ValidationContext
) -> None:
    """Plan 4 §T23. Each duty-profile pool must cover all 18 Bannerlord skills
    at least once. Gaps emit warnings; no build-failure threshold."""
    print("[Phase 13] Validating duty-profile pool skill coverage...")
    repo_root = Path(__file__).parent.parent.parent
    storylet_dir = repo_root / "ModuleData" / "Enlisted" / "Storylets"

    # Initialize coverage matrix: coverage[profile][skill] = int
    coverage: dict[str, dict[str, int]] = {
        profile: {skill: 0 for skill in _BANNERLORD_SKILLS}
        for profile in _DUTY_PROFILES
    }

    for _src, storylet in storylet_entries:
        sid = storylet.get("id", "")
        # Skip transition storylets — they fire from the interrupt hook, not the
        # ambient emitter, and don't have profile_requires.
        if sid.startswith("transition_"):
            continue

        # Skip culture-overlay storylets — their id contains "__<culture>" and the
        # emitter pre-empts the base sibling when the overlay is eligible. Counting
        # them toward coverage would triple-book the same skill grant across cultures.
        if "__" in sid:
            continue

        profiles = storylet.get("profile_requires") or []
        if not profiles:
            continue  # Untargeted storylets don't count toward per-profile coverage.

        # Collect every skill this storylet grants XP for.
        granted: set[str] = set()
        for option in storylet.get("options") or []:
            for effect in option.get("effects") or []:
                if effect.get("apply") == "skill_xp":
                    skill = effect.get("skill")
                    if skill:
                        granted.add(skill)

        for profile in profiles:
            if profile not in coverage:
                continue  # storylet targets an unknown profile — separate check
            for skill in granted:
                if skill in coverage[profile]:
                    coverage[profile][skill] += 1

    # Report density summary and flag empty cells.
    summary_parts = []
    for profile in _DUTY_PROFILES:
        filled = sum(1 for skill in _BANNERLORD_SKILLS if coverage[profile][skill] > 0)
        total = len(_BANNERLORD_SKILLS)
        summary_parts.append(f"{profile}={filled}/{total}")

        pool_path = storylet_dir / f"duty_{profile}.json"
        for skill in _BANNERLORD_SKILLS:
            if coverage[profile][skill] == 0:
                ctx.add_issue(
                    "warning",
                    "duty-pool-coverage",
                    f"duty profile '{profile}' has no storylet awarding {skill} XP",
                    str(pool_path),
                )

    print(f"  Pool coverage (cells with >=1 covering storylet): {', '.join(summary_parts)}")


def phase14_arc_integrity(
    storylet_entries: list[tuple[Path, dict]], ctx: ValidationContext
) -> None:
    """Spec 2 §14. Arc-tagged storylets must have valid duration, phases, and pool refs."""
    print("[Phase 14] Validating named-order arc integrity...")
    storylet_ids = {s.get("id") for _, s in storylet_entries if s.get("id")}
    errors_before = sum(1 for i in ctx.issues if i.category == "arc-integrity")

    for src, s in storylet_entries:
        sid = s.get("id", "<unknown>")
        arc = s.get("arc")
        if not arc:
            continue
        if not isinstance(arc, dict):
            ctx.add_issue(
                "error", "arc-integrity",
                f"storylet '{sid}' arc must be an object, got {type(arc).__name__}",
                str(src),
            )
            continue
        if arc.get("duration_hours", 0) <= 0:
            ctx.add_issue(
                "error", "arc-integrity",
                f"storylet '{sid}' arc.duration_hours must be > 0", str(src),
            )
        phases = arc.get("phases", [])
        if not phases:
            ctx.add_issue(
                "error", "arc-integrity",
                f"storylet '{sid}' arc.phases is empty", str(src),
            )
        for ph in phases:
            prefix = ph.get("pool_prefix", "")
            if not prefix:
                ctx.add_issue(
                    "error", "arc-integrity",
                    f"storylet '{sid}' phase '{ph.get('id')}' missing pool_prefix", str(src),
                )
                continue
            if not any(other_id.startswith(prefix) for other_id in storylet_ids):
                ctx.add_issue(
                    "error", "arc-integrity",
                    f"storylet '{sid}' phase pool_prefix '{prefix}' has no matching storylets",
                    str(src),
                )
        interrupt_id = arc.get("on_transition_interrupt_storylet", "")
        if interrupt_id and interrupt_id not in storylet_ids:
            ctx.add_issue(
                "error", "arc-integrity",
                f"storylet '{sid}' references unknown interrupt storylet '{interrupt_id}'",
                str(src),
            )

    # start_arc effect must only appear on arc-tagged storylets.
    for src, s in storylet_entries:
        if s.get("arc"):
            continue
        sid = s.get("id", "<unknown>")
        for opt in s.get("options", []):
            for eff in opt.get("effects", []):
                if not isinstance(eff, dict):
                    continue
                if "start_arc" in eff or eff.get("apply") == "start_arc":
                    ctx.add_issue(
                        "error", "arc-integrity",
                        f"storylet '{sid}' uses start_arc but has no arc block", str(src),
                    )

    errors_after = sum(1 for i in ctx.issues if i.category == "arc-integrity")
    if errors_after == errors_before:
        print(
            f"  OK: {sum(1 for _, s in storylet_entries if s.get('arc'))} "
            f"arc-tagged storylet(s) passed."
        )


def phase15_path_crossroads(storylets: list[dict], ctx: ValidationContext) -> None:
    """Plan 5 T9. Asserts career-path crossroads completeness + per-path T7+ variant coverage.

    Two checks, gated on the presence of any path_crossroads_* id so pre-Plan-5 repos
    don't fail the phase:

    (a) 15 base crossroads storylets: 5 paths x 3 milestones (T4/T6/T9). Missing ids
        fail with an error referencing the expected id.

    (b) Each of the 5 paths has at least one T7+ variant — identified as any storylet
        whose trigger list contains the literal 'flag:committed_path_<path>'. Minimum
        viable check (one variant per path), not per-archetype enforcement — Half B
        polish expands the archetype matrix later.
    """
    print("[Phase 15] Validating path crossroads completeness...")
    PATHS = ["ranger", "enforcer", "support", "diplomat", "rogue"]
    MILESTONES = [4, 6, 9]
    storylet_ids = {s.get("id") for s in storylets if s.get("id")}

    have_any_crossroads = any(sid.startswith("path_crossroads_") for sid in storylet_ids)
    if not have_any_crossroads:
        print("  OK: no path_crossroads_* storylets yet; skipping completeness check.")
        return

    # (a) Base milestone storylets.
    expected = {f"path_crossroads_{p}_t{t}" for p in PATHS for t in MILESTONES}
    missing = expected - storylet_ids
    for m in sorted(missing):
        ctx.add_issue("error", "crossroads", f"missing crossroads storylet '{m}'", m)

    # (b) Per-path T7+ variant coverage: count storylets whose trigger array contains
    # 'flag:committed_path_<path>'. Any matching storylet counts as a variant for that
    # path — we don't care which archetype (scout / escort / future) it covers.
    variants_per_path: dict[str, int] = {p: 0 for p in PATHS}
    for s in storylets:
        triggers = s.get("trigger") or []
        if not isinstance(triggers, list):
            continue
        for p in PATHS:
            if f"flag:committed_path_{p}" in triggers:
                variants_per_path[p] += 1
                break  # one storylet counts for at most one path

    for p in PATHS:
        if variants_per_path[p] == 0:
            ctx.add_issue(
                "error", "crossroads",
                f"path '{p}' has no T7+ variant storylet with trigger 'flag:committed_path_{p}'",
                f"ModuleData/Enlisted/Storylets/path_{p}_t7_variants.json (expected)",
            )

    if not missing and all(v > 0 for v in variants_per_path.values()):
        variant_summary = ", ".join(f"{p}={variants_per_path[p]}" for p in PATHS)
        print(
            f"  OK: all {len(expected)} milestone storylets present; "
            f"T7+ variants per path — {variant_summary}."
        )


def _load_loot_pools(ctx: ValidationContext) -> list[dict]:
    """Load loot pool definitions from ModuleData/Enlisted/Loot/loot_pools.json."""
    repo_root = Path(__file__).parent.parent.parent
    pool_file = repo_root / "ModuleData" / "Enlisted" / "Loot" / "loot_pools.json"
    if not pool_file.exists():
        return []
    try:
        with open(pool_file, encoding="utf-8-sig") as f:
            data = json.load(f)
    except Exception as e:
        ctx.add_issue(
            "error", "loot-pools",
            f"Failed to parse loot_pools.json: {e}", str(pool_file),
        )
        return []
    pools = data.get("pools") if isinstance(data, dict) else data
    if not isinstance(pools, list):
        ctx.add_issue(
            "error", "loot-pools",
            "loot_pools.json: expected a 'pools' array or top-level list",
            str(pool_file),
        )
        return []
    return pools


def phase16_loot_pool_sanity(loot_pools: list[dict], ctx: ValidationContext) -> None:
    """Spec 2 §14. Pool source + filter required; per-source required keys enforced."""
    print("[Phase 16] Validating loot pool sanity...")
    repo_root = Path(__file__).parent.parent.parent
    pool_file = str(repo_root / "ModuleData" / "Enlisted" / "Loot" / "loot_pools.json")
    errors_before = sum(1 for i in ctx.issues if i.category == "loot-pools")
    for pool in loot_pools:
        if not isinstance(pool, dict):
            ctx.add_issue(
                "error", "loot-pools",
                f"loot_pools.json entry is not an object: {pool!r}", pool_file,
            )
            continue
        pid = pool.get("id", "<unknown>")
        src = pool.get("source")
        if src not in ("faction_troop_tree", "global_catalog"):
            ctx.add_issue(
                "error", "loot-pools",
                f"pool '{pid}' invalid source '{src}' "
                f"(must be 'faction_troop_tree' or 'global_catalog')",
                pool_file,
            )
            continue
        flt = pool.get("filter", {}) or {}
        if not flt.get("categories"):
            ctx.add_issue(
                "error", "loot-pools",
                f"pool '{pid}' missing filter.categories", pool_file,
            )
        if src == "global_catalog" and not flt.get("tier_range"):
            ctx.add_issue(
                "error", "loot-pools",
                f"pool '{pid}' (global_catalog) missing filter.tier_range", pool_file,
            )
        if src == "faction_troop_tree" and not flt.get("max_troop_tier"):
            ctx.add_issue(
                "error", "loot-pools",
                f"pool '{pid}' (faction_troop_tree) missing filter.max_troop_tier",
                pool_file,
            )
    errors_after = sum(1 for i in ctx.issues if i.category == "loot-pools")
    if errors_after == errors_before:
        print(f"  OK: {len(loot_pools)} loot pool(s) passed sanity checks.")


def phase17_rate_caps(
    storylet_entries: list[tuple[Path, dict]], ctx: ValidationContext
) -> None:
    """Spec 2 §7. Per-option caps on permanent reward currencies."""
    print("[Phase 17] Validating per-option rate caps...")
    CAPS = {
        "grant_attribute_level": 1,
        "grant_focus_point": 1,
        "grant_skill_level": 3,
        "grant_renown": 3,
        "grant_item": 1,
        "grant_random_item_from_pool": 1,
    }
    errors_before = sum(1 for i in ctx.issues if i.category == "rate-caps")
    for src, s in storylet_entries:
        sid = s.get("id", "<unknown>")
        for opt in s.get("options", []):
            oid = opt.get("id", "<unknown>")
            counts = {k: 0 for k in CAPS}
            for eff in opt.get("effects", []):
                if not isinstance(eff, dict):
                    continue
                # Effects may be short-form {"grant_renown": {...}} or long-form {"apply": "grant_renown", ...}.
                apply_id = eff.get("apply")
                for key in CAPS:
                    if key in eff or apply_id == key:
                        counts[key] += 1
                # relation_change delta cap (±5). Covers both short-form
                # {"relation_change": {"delta": N}} and long-form
                # {"apply": "relation_change", "delta": N, ...}.
                delta = None
                rc = eff.get("relation_change")
                if isinstance(rc, dict):
                    delta = rc.get("delta", 0)
                elif apply_id == "relation_change":
                    delta = eff.get("delta", 0)
                if delta is not None and abs(delta) > 5:
                    ctx.add_issue(
                        "error", "rate-caps",
                        f"storylet '{sid}' option '{oid}' relation_change |delta|={abs(delta)} > 5",
                        str(src),
                    )
            for key, cap in CAPS.items():
                if counts[key] > cap:
                    ctx.add_issue(
                        "error", "rate-caps",
                        f"storylet '{sid}' option '{oid}' has {counts[key]} "
                        f"{key} effects (cap {cap})",
                        str(src),
                    )
    errors_after = sum(1 for i in ctx.issues if i.category == "rate-caps")
    if errors_after == errors_before:
        print("  OK: all storylet options within rate caps.")


def phase18_companion_dialogue(ctx: ValidationContext) -> None:
    """Plan 2 §6 T18. Schema-validate the six companion archetype dialog catalogs.

    Checks:
      - schemaVersion == 1
      - dialogueType == "companion"
      - Every node has a non-empty id
      - context.archetype values are listed in archetype_catalog.json
      - context.companion_type values are listed in archetype_catalog.json
      - All option.next_node references resolve to a known node id

    Companion catalogs share ModuleData/Enlisted/Dialogue/ with the QM catalog;
    we filter by filename (companion_*.json) and dialogueType discriminator.
    """
    print("[Phase 18] Validating companion dialog catalogs...")

    catalog_path = Path("ModuleData/Enlisted/Companions/archetype_catalog.json")
    known_companion_types: set[str] = set()
    known_archetypes: set[str] = set()
    if catalog_path.is_file():
        try:
            with open(catalog_path, encoding="utf-8") as fh:
                cat = json.load(fh)
            for ct in cat.get("companionTypes", []):
                if isinstance(ct, dict):
                    cid = ct.get("id")
                    if cid:
                        known_companion_types.add(cid)
                    for arche in ct.get("archetypes", []):
                        if isinstance(arche, dict):
                            aid = arche.get("id")
                            if aid:
                                known_archetypes.add(aid)
        except Exception as exc:
            ctx.add_issue(
                "warning", "companion-dialog",
                f"could not read archetype catalog ({exc}); archetype "
                f"validation will be skipped",
                str(catalog_path),
            )

    files = sorted(glob.glob("ModuleData/Enlisted/Dialogue/companion_*.json"))
    if not files:
        ctx.add_issue(
            "warning", "companion-dialog",
            "no companion_*.json catalogs found; six expected",
            "ModuleData/Enlisted/Dialogue/",
        )
        return

    errors_before = sum(1 for i in ctx.issues if i.category == "companion-dialog")
    total_nodes = 0

    for path_str in files:
        path = Path(path_str)
        try:
            with open(path, encoding="utf-8") as fh:
                doc = json.load(fh)
        except Exception as exc:
            ctx.add_issue(
                "error", "companion-dialog",
                f"{path.name}: failed to parse JSON ({exc})",
                str(path),
            )
            continue

        sv = doc.get("schemaVersion")
        if sv != 1:
            ctx.add_issue(
                "error", "companion-dialog",
                f"{path.name}: schemaVersion must be 1, got {sv!r}",
                str(path),
            )

        dtype = doc.get("dialogueType")
        if dtype != "companion":
            ctx.add_issue(
                "error", "companion-dialog",
                f"{path.name}: dialogueType must be \"companion\", got {dtype!r}",
                str(path),
            )

        nodes = doc.get("nodes", [])
        if not isinstance(nodes, list):
            ctx.add_issue(
                "error", "companion-dialog",
                f"{path.name}: 'nodes' must be an array",
                str(path),
            )
            continue

        node_ids: set[str] = set()
        next_node_refs: list[tuple[str, str]] = []
        for idx, node in enumerate(nodes):
            if not isinstance(node, dict):
                ctx.add_issue(
                    "error", "companion-dialog",
                    f"{path.name}: nodes[{idx}] is not an object",
                    str(path),
                )
                continue
            nid = node.get("id")
            if not nid:
                ctx.add_issue(
                    "error", "companion-dialog",
                    f"{path.name}: nodes[{idx}] missing required 'id'",
                    str(path),
                )
                continue
            node_ids.add(nid)
            total_nodes += 1

            node_ctx = node.get("context") or {}
            if isinstance(node_ctx, dict):
                ct = node_ctx.get("companion_type")
                if (
                    ct is not None
                    and known_companion_types
                    and ct not in known_companion_types
                ):
                    ctx.add_issue(
                        "error", "companion-dialog",
                        f"{path.name}: node '{nid}' context.companion_type "
                        f"'{ct}' not in archetype_catalog.json",
                        str(path),
                    )
                arche = node_ctx.get("archetype")
                if (
                    arche is not None
                    and known_archetypes
                    and arche not in known_archetypes
                ):
                    ctx.add_issue(
                        "error", "companion-dialog",
                        f"{path.name}: node '{nid}' context.archetype "
                        f"'{arche}' not in archetype_catalog.json",
                        str(path),
                    )

            for opt_idx, opt in enumerate(node.get("options", [])):
                if not isinstance(opt, dict):
                    continue
                nxt = opt.get("next_node")
                if nxt:
                    next_node_refs.append((nid, nxt))

        for src_id, target in next_node_refs:
            if target not in node_ids:
                ctx.add_issue(
                    "error", "companion-dialog",
                    f"{path.name}: option in node '{src_id}' references "
                    f"unknown next_node '{target}'",
                    str(path),
                )

    errors_after = sum(1 for i in ctx.issues if i.category == "companion-dialog")
    if errors_after == errors_before:
        print(
            f"  OK: {len(files)} companion dialog catalog(s) validated, "
            f"{total_nodes} nodes total."
        )


def main():
    """Main validation entry point."""
    parser = argparse.ArgumentParser(description="Validate Enlisted mod content files")
    parser.add_argument(
        "--strict", action="store_true", help="Treat warnings as errors (blocks merge)"
    )
    parser.add_argument(
        "--fix-refs",
        action="store_true",
        help="Generate stub entries for missing localization strings",
    )
    parser.add_argument(
        "--check-orphans",
        action="store_true",
        help="Detect orphaned XML strings not referenced by any JSON",
    )
    args = parser.parse_args()

    print("=" * 80)
    print("ENLISTED MOD - CONTENT VALIDATION TOOL")
    print("=" * 80)
    print()

    ctx = ValidationContext(strict=args.strict)

    print("[Phase 0] Loading localization strings...")
    localization_ids = load_localization_strings()

    # Collect all content files (Events). Decisions retired.
    event_files = sorted(glob.glob("ModuleData/Enlisted/Events/**/*.json", recursive=True))
    # schema_version.json is metadata, not an event file.
    event_files = [f for f in event_files if not f.endswith("schema_version.json")]
    all_files = event_files

    if not all_files:
        print("[ERROR] No content files found!")
        return 2

    print(f"[Phase 0] Found {len(all_files)} event files")
    print()

    print("[Phase 1-4] Validating structure, references, logic, and consistency...")
    for file_path in all_files:
        validate_event_file(file_path, ctx, localization_ids)

    print("[Phase 4] Running cross-file consistency checks...")
    validate_flag_consistency(ctx)

    if args.check_orphans:
        print("[Phase 5] Detecting orphaned strings...")
        detect_orphan_strings(localization_ids, ctx)

    validate_config_files(ctx)

    # Phase 7: Project structure validation
    validate_csproj(ctx)

    # Phase 8: Code quality validation
    validate_code_quality(ctx)

    # Phase 9: C# TextObject localization validation
    validate_csharp_textobjects(ctx, localization_ids)

    # Phase 9.5: Camp schedule description validation
    validate_camp_schedule_descriptions(ctx)

    # Phase 10: Error-code registry sync
    validate_error_code_registry(ctx)

    # Phase 11: ModLogger.Error retirement gate
    validate_no_modlogger_error_calls(ctx)

    # Phase 12: Storylet reference validation
    validate_storylet_references(ctx)

    # Phases 13-17: Storylet-catalog validators (load storylets once, reuse).
    storylet_entries = _load_all_storylets(ctx)
    storylets_only = [s for _, s in storylet_entries]  # Phase 15 needs ids only.
    phase13_pool_coverage(storylet_entries, ctx)
    phase14_arc_integrity(storylet_entries, ctx)
    phase15_path_crossroads(storylets_only, ctx)
    loot_pools = _load_loot_pools(ctx)
    phase16_loot_pool_sanity(loot_pools, ctx)
    phase17_rate_caps(storylet_entries, ctx)

    # Phase 18: Companion dialog catalog schema (Plan 2)
    phase18_companion_dialogue(ctx)

    # Generate missing strings file if requested
    if args.fix_refs:
        missing_strings = []
        for issue in ctx.issues:
            if (
                issue.category == "reference"
                and "not found in enlisted_strings.xml" in issue.message
            ):
                match = re.search(r"'([^']+)' not found", issue.message)
                if match:
                    missing_strings.append(match.group(1))

        if missing_strings:
            output_file = Path("_missing_strings.txt")
            with open(output_file, "w", encoding="utf-8") as f:
                f.write("# Missing localization strings\n")
                f.write("# Add these to ModuleData/Languages/enlisted_strings.xml\n\n")
                for string_id in sorted(set(missing_strings)):
                    f.write(f'    <string id="{string_id}" text="TODO: {string_id}" />\n')
            print(
                f"\n[FIX-REFS] Generated {output_file} with {len(set(missing_strings))} missing string stubs"
            )

    ctx.print_report()

    if ctx.has_critical_issues():
        print("[X] VALIDATION FAILED - Critical issues found")
        return 1
    elif ctx.has_warnings():
        print("[!] VALIDATION PASSED WITH WARNINGS")
        return 0
    else:
        print("[OK] VALIDATION PASSED")
        return 0


if __name__ == "__main__":
    sys.exit(main())
