import glob
import json

files = glob.glob("ModuleData/Enlisted/Events/events_*.json")
for f in sorted(files):
    with open(f, encoding="utf-8-sig") as file:
        data = json.load(file)
        if "events" in data:
            print(f"\n=== {f.split('\\')[-1].split('/')[-1]} ===")
            for event in data["events"]:
                print(f"  {event['id']}: {event.get('title', 'NO TITLE')}")
