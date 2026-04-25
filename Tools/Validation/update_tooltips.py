#!/usr/bin/env python3
"""
Generate professional tooltips for event options based on their effects, costs, and risks.
Follows the style: action + side effects + cooldown/restrictions
Example: "Increase equipped weapon skill. Causes fatigue. Chance of injury. 3 day cooldown."
"""

import json
import sys
from pathlib import Path


def generate_tooltip(option, event_context=None):
    """Generate a concise professional tooltip for an event option."""
    if option.get("tooltip") and option["tooltip"] != "null" and option["tooltip"]:
        # Already has a non-null tooltip, skip
        return option["tooltip"]

    parts = []

    # Analyze the option text to understand the action
    text = option.get("text", "")

    # Check for risk
    risk = option.get("risk")
    risk_chance = option.get("risk_chance")

    # Check for costs
    costs = option.get("costs", {})
    fatigue_cost = costs.get("fatigue", 0)
    gold_cost = costs.get("gold", 0)
    time_cost = costs.get("time_hours", 0)

    # Check for effects
    effects = option.get("effects", {})
    scrutiny = effects.get("scrutiny", 0)
    discipline = effects.get("discipline", 0)
    soldier_rep = effects.get("soldierRep", 0)
    officer_rep = effects.get("officerRep", 0)
    lord_rep = effects.get("lordRep", 0)
    medical_risk = effects.get("medical_risk", 0)
    fatigue_relief = effects.get("fatigue_relief", 0)
    hp_change = effects.get("hpChange", 0)

    # Check for rewards
    rewards = option.get("rewards", {})
    gold_reward = rewards.get("gold", 0)
    skill_xp = rewards.get("skillXp", {}) or rewards.get("xp", {})
    fatigue_relief_reward = rewards.get("fatigueRelief", 0)

    # Check for injury risk
    injury_risk = option.get("injury_risk")

    # Build tooltip components

    # Risk-based descriptions
    if risk == "risky" and risk_chance:
        parts.append(f"{risk_chance}% success chance")
    elif risk == "dangerous":
        parts.append("High risk")

    # Positive effects first
    if fatigue_relief > 0 or fatigue_relief_reward > 0:
        max(fatigue_relief, fatigue_relief_reward)
        parts.append("Relieves fatigue")

    if hp_change > 0:
        parts.append("Restores health")
    elif hp_change < 0:
        parts.append("Health risk")

    if gold_reward > 0:
        parts.append(f"+{gold_reward} gold")

    if skill_xp:
        parts.append("Skill training")

    # Reputation effects
    if soldier_rep > 0:
        parts.append("+Soldier reputation")
    elif soldier_rep < 0:
        parts.append("−Soldier reputation")

    if officer_rep > 0:
        parts.append("+Officer reputation")
    elif officer_rep < 0:
        parts.append("−Officer reputation")

    if lord_rep > 0:
        parts.append("+Lord reputation")
    elif lord_rep < 0:
        parts.append("−Lord reputation")

    # Negative effects
    if fatigue_cost > 0:
        parts.append("Causes fatigue")

    if gold_cost > 0:
        parts.append(f"Costs {gold_cost} gold")

    if time_cost > 0:
        parts.append(f"Takes {time_cost}h")

    if scrutiny > 0:
        parts.append("Attracts scrutiny")
    elif scrutiny < 0:
        parts.append("Reduces suspicion")

    if discipline > 0:
        parts.append("Disciplinary risk")
    elif discipline < 0:
        parts.append("Improves record")

    if medical_risk > 0:
        parts.append("Medical risk")

    if injury_risk and injury_risk.get("chance", 0) > 0:
        parts.append("Injury chance")

    # If we couldn't generate anything meaningful, provide a generic tooltip
    if not parts:
        # For cancel/decline options
        if (
            "cancel" in option.get("id", "").lower()
            or "decline" in text.lower()
            or "leave" in text.lower()
        ):
            return "No effect"
        # For safe dialogue options with no mechanical effects
        if risk == "safe" and not any([fatigue_cost, gold_cost, time_cost]):
            return "Safe choice"
        return None

    # Join with period separators and capitalize
    tooltip = ". ".join(parts)
    if tooltip and not tooltip.endswith("."):
        tooltip += "."

    return tooltip


def process_file(filepath):
    """Process a single JSON file and update tooltips."""
    print(f"Processing {filepath}...")

    with open(filepath, encoding="utf-8") as f:
        data = json.load(f)

    modified = False
    events = data.get("events", [])

    for event in events:
        # Process main options
        options = event.get("content", {}).get("options", [])
        for option in options:
            if option.get("tooltip") is None:
                new_tooltip = generate_tooltip(option, event)
                if new_tooltip:
                    option["tooltip"] = new_tooltip
                    modified = True

        # Process variant options
        variants = event.get("variants", {})
        for _variant_name, variant_data in variants.items():
            variant_options = variant_data.get("options", [])
            for option in variant_options:
                if option.get("tooltip") is None:
                    new_tooltip = generate_tooltip(option, event)
                    if new_tooltip:
                        option["tooltip"] = new_tooltip
                        modified = True

        # Process reward choice options (nested within options)
        options = event.get("content", {}).get("options", [])
        for option in options:
            reward_choices = option.get("reward_choices", {})
            if reward_choices:
                for choice_option in reward_choices.get("options", []):
                    if choice_option.get("tooltip") is None:
                        new_tooltip = generate_tooltip(choice_option, event)
                        if new_tooltip:
                            choice_option["tooltip"] = new_tooltip
                            modified = True

    if modified:
        # Write back with proper formatting
        with open(filepath, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        print(f"✓ Updated {filepath}")
        return True
    else:
        print(f"  No changes needed for {filepath}")
        return False


def main():
    if len(sys.argv) > 1:
        filepath = Path(sys.argv[1])
        if filepath.exists():
            process_file(filepath)
        else:
            print(f"Error: File not found: {filepath}")
            sys.exit(1)
    else:
        print("Usage: python update_tooltips.py <json_file>")
        sys.exit(1)


if __name__ == "__main__":
    main()
