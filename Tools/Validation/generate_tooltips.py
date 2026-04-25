#!/usr/bin/env python3
"""
Generate professional contextual tooltips for event options.
Format: action + side effects + cooldown/restrictions
Example: "20% success chance. Failure adds scrutiny and discipline penalty."
"""

import json
import sys
from pathlib import Path


def analyze_option_intent(option, event_context):
    """Analyze the option text and context to understand player intent."""
    text = option.get("text", "").lower()
    option_id = option.get("id", "").lower()

    # Common patterns
    is_cancel = "cancel" in option_id or "leave" in text or "decline" in text or "walk away" in text
    is_aggressive = any(word in text for word in ["fight", "attack", "threaten", "force", "demand"])
    is_diplomatic = any(
        word in text for word in ["negotiate", "ask", "persuade", "convince", "charm"]
    )
    is_cautious = any(word in text for word in ["careful", "cautious", "wait", "observe", "think"])
    is_bold = any(word in text for word in ["bold", "confident", "assert", "stand up"])

    return {
        "cancel": is_cancel,
        "aggressive": is_aggressive,
        "diplomatic": is_diplomatic,
        "cautious": is_cautious,
        "bold": is_bold,
    }


def generate_tooltip(option, event_context=None):
    """Generate a concise professional tooltip for an event option."""
    # Skip if already has a tooltip
    if option.get("tooltip") and option["tooltip"] not in [None, "null", ""]:
        return option["tooltip"]

    parts = []

    # Get option context
    intent = analyze_option_intent(option, event_context)
    risk = option.get("risk")
    risk_chance = option.get("risk_chance")

    # 1. Success chance (if risky)
    if risk in ["risky", "dangerous"] and risk_chance:
        parts.append(f"{risk_chance}% success chance")
    elif risk == "dangerous" and not risk_chance:
        parts.append("High risk")

    # Get costs, effects, rewards
    costs = option.get("costs", {})
    effects = option.get("effects", {})
    rewards = option.get("rewards", {})

    fatigue_cost = costs.get("fatigue", 0)
    gold_cost = costs.get("gold", 0)
    time_cost = costs.get("time_hours", 0)

    scrutiny = effects.get("scrutiny", 0)
    discipline = effects.get("discipline", 0)
    soldier_rep = effects.get("soldierRep", 0)
    officer_rep = effects.get("officerRep", 0)
    lord_rep = effects.get("lordRep", 0)
    medical_risk = effects.get("medical_risk", 0)
    fatigue_relief = effects.get("fatigue_relief", 0)
    hp_change = effects.get("hpChange", 0)

    gold_reward = rewards.get("gold", 0)
    skill_xp = rewards.get("skillXp", {}) or rewards.get("xp", {})
    fatigue_relief_reward = rewards.get("fatigueRelief", 0)

    injury_risk = option.get("injury_risk")

    # 2. Failure consequences (if risky and has negative effects)
    if risk in ["risky", "dangerous"]:
        failure_parts = []
        if scrutiny > 0:
            failure_parts.append("scrutiny")
        if discipline > 0:
            failure_parts.append("discipline penalty")
        if medical_risk > 0:
            failure_parts.append("injury risk")
        if soldier_rep < 0:
            failure_parts.append("soldier reputation loss")

        if failure_parts:
            parts.append(f"Failure adds {' and '.join(failure_parts)}")

    # 3. Positive outcomes
    if gold_reward > 0:
        parts.append(f"Earn {gold_reward} gold")

    if skill_xp:
        skill_names = list(skill_xp.keys())
        if len(skill_names) == 1:
            parts.append(f"Train {skill_names[0]}")
        else:
            parts.append("Skill training")

    if fatigue_relief > 0 or fatigue_relief_reward > 0:
        parts.append("Reduces fatigue")

    if hp_change > 0:
        parts.append("Restores health")

    # 4. Reputation effects
    if soldier_rep > 0:
        parts.append("Improves soldier reputation")
    elif soldier_rep < 0 and risk == "safe":  # Only mention if guaranteed
        parts.append("Harms soldier reputation")

    if officer_rep > 0:
        parts.append("Improves officer reputation")
    elif officer_rep < 0 and risk == "safe":
        parts.append("Harms officer reputation")

    if lord_rep > 0:
        parts.append("Improves lord reputation")
    elif lord_rep < 0 and risk == "safe":
        parts.append("Harms lord reputation")

    # 5. Costs
    if fatigue_cost > 0:
        parts.append("Causes fatigue")

    if gold_cost > 0:
        parts.append(f"Costs {gold_cost} gold")

    if time_cost > 0:
        if time_cost >= 8:
            parts.append(f"Takes {time_cost}h")
        else:
            parts.append(f"{time_cost}h duration")

    # 6. Risks (if not already mentioned)
    if injury_risk and not any("injury" in p.lower() for p in parts):
        chance = injury_risk.get("chance", 0)
        if chance > 0:
            parts.append("Chance of injury")

    if medical_risk > 0 and not any("injury" in p.lower() for p in parts):
        parts.append("Medical risk")

    if scrutiny > 0 and risk == "safe":  # Guaranteed scrutiny
        parts.append("Attracts scrutiny")

    if discipline > 0 and risk == "safe":  # Guaranteed discipline
        parts.append("Disciplinary risk")

    # 7. Cancel/safe options with no effects
    if not parts:
        if intent["cancel"]:
            return "No action taken"
        if risk == "safe":
            return "Safe choice"
        return None

    # Format: Period-separated sentences
    tooltip = ". ".join(parts)
    if not tooltip.endswith("."):
        tooltip += "."

    return tooltip


def process_options_recursive(data, event_context=None):
    """Recursively process all options in the data structure."""
    modified = False

    if isinstance(data, dict):
        # Check if this dict has an options array
        if "options" in data and isinstance(data["options"], list):
            for option in data["options"]:
                if option.get("tooltip") is None:
                    new_tooltip = generate_tooltip(option, event_context)
                    if new_tooltip:
                        option["tooltip"] = new_tooltip
                        modified = True

        # Recurse into all dict values
        for _key, value in data.items():
            if process_options_recursive(value, event_context):
                modified = True

    elif isinstance(data, list):
        # Recurse into all list items
        for item in data:
            if process_options_recursive(item, event_context):
                modified = True

    return modified


def process_file(filepath):
    """Process a single JSON file and update tooltips."""
    print(f"Processing {filepath.name}...")

    with open(filepath, encoding="utf-8-sig") as f:
        data = json.load(f)

    modified = process_options_recursive(data)

    if modified:
        # Write back with proper formatting
        with open(filepath, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        print(f"[OK] Updated {filepath.name}")
        return True
    else:
        print(f"  No changes needed for {filepath.name}")
        return False


def main():
    if len(sys.argv) > 1:
        target = Path(sys.argv[1])

        if target.is_file():
            process_file(target)
        elif target.is_dir():
            # Process all JSON files in directory
            json_files = list(target.glob("*.json"))
            print(f"Found {len(json_files)} JSON files in {target}")

            updated_count = 0
            for filepath in sorted(json_files):
                if process_file(filepath):
                    updated_count += 1

            print(f"\n[OK] Updated {updated_count} of {len(json_files)} files")
        else:
            print(f"Error: Path not found: {target}")
            sys.exit(1)
    else:
        print("Usage: python generate_tooltips.py <json_file_or_directory>")
        sys.exit(1)


if __name__ == "__main__":
    main()
