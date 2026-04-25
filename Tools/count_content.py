"""Count all content pieces in the mod."""

import glob
import json
from pathlib import Path


def count_content():
    base = Path("ModuleData/Enlisted")

    # Count Decisions (events array)
    decisions = []
    for f in glob.glob(str(base / "Decisions" / "*.json")):
        with open(f, encoding="utf-8-sig") as file:
            data = json.load(file)
            if "events" in data:
                decisions.extend(data["events"])

    # Count Events (narrative events)
    events = []
    for f in glob.glob(str(base / "Events" / "events_*.json")):
        with open(f, encoding="utf-8-sig") as file:
            data = json.load(file)
            if "events" in data:
                events.extend(data["events"])

    # Count Map Incidents
    incidents = []
    for f in glob.glob(str(base / "Events" / "incidents_*.json")):
        with open(f, encoding="utf-8-sig") as file:
            data = json.load(file)
            if "events" in data:
                incidents.extend(data["events"])

    # Count Storylets (storylet backbone — Spec 0)
    storylets = []
    for f in glob.glob(str(base / "Storylets" / "*.json")):
        with open(f, encoding="utf-8-sig") as file:
            data = json.load(file)
            if "storylets" in data:
                storylets.extend(data["storylets"])

    # Retinue-specific content (already in events/incidents)
    retinue_events = []
    retinue_file = base / "Events" / "events_retinue.json"
    if retinue_file.exists():
        with open(retinue_file, encoding="utf-8-sig") as file:
            data = json.load(file)
            if "events" in data:
                retinue_events = data["events"]

    retinue_incidents = []
    retinue_inc_file = base / "Events" / "incidents_retinue.json"
    if retinue_inc_file.exists():
        with open(retinue_inc_file, encoding="utf-8-sig") as file:
            data = json.load(file)
            if "events" in data:
                retinue_incidents = data["events"]

    total_retinue = len(retinue_events) + len(retinue_incidents)

    # Calculate totals
    total_content = len(decisions) + len(events) + len(incidents) + len(storylets)

    print("=" * 60)
    print("ENLISTED MOD - CONTENT COUNT")
    print("=" * 60)
    print(f"Decisions:           {len(decisions):>4}")
    print(f"Context Events:      {len(events):>4}")
    print(f"Map Incidents:       {len(incidents):>4}")
    print(f"Storylets:           {len(storylets):>4}")
    print(f"  (Retinue subset:   {total_retinue:>4})")
    print("-" * 60)
    print(f"TOTAL CONTENT:       {total_content:>4}")
    print("=" * 60)

    return {
        "decisions": len(decisions),
        "events": len(events),
        "incidents": len(incidents),
        "storylets": len(storylets),
        "retinue": total_retinue,
        "total": total_content,
    }


if __name__ == "__main__":
    count_content()
