import glob
import json
import sys
from collections import Counter


def check_fallback_fields(obj: dict, context: str, bad: list) -> None:
    """
    Validates that every localization ID field has a corresponding fallback field.
    Required pairs:
    - titleId -> title
    - setupId -> setup
    - textId -> text
    - resultTextId -> resultText
    - resultFailureTextId -> resultFailureText
    """
    fallback_pairs = [
        ("titleId", "title"),
        ("setupId", "setup"),
        ("textId", "text"),
        ("resultTextId", "resultText"),
        ("resultFailureTextId", "resultFailureText"),
    ]

    for id_field, fallback_field in fallback_pairs:
        if id_field in obj:
            # ID field exists, check for fallback
            if fallback_field not in obj:
                bad.append(
                    ("MISSING_FALLBACK", context, f"'{fallback_field}' missing for '{id_field}'")
                )
            elif not obj[fallback_field] or not str(obj[fallback_field]).strip():
                bad.append(
                    ("EMPTY_FALLBACK", context, f"'{fallback_field}' is empty for '{id_field}'")
                )


def main() -> int:
    files = sorted(glob.glob("ModuleData/Enlisted/Events/events_*.json"))
    if not files:
        print("[validate] No event packs found under ModuleData/Enlisted/Events/")
        return 2

    seen = set()
    bad = []
    total = 0

    for f in files:
        with open(f, encoding="utf-8-sig") as fp:
            obj = json.load(fp)

        events = obj.get("events") or []
        for e in events:
            eid = (e.get("id") or "").strip()
            if not eid:
                bad.append(("MISSING_ID", f))
                continue

            if eid in seen:
                bad.append(("DUP_ID", eid, f))
            seen.add(eid)

            # Check event-level fallback fields
            check_fallback_fields(e, f"event:{eid}", bad)

            # Check option-level fallback fields
            opts = e.get("options") or []
            # Allow 0 options for threshold/info events
            # Allow up to 6 options for onboarding/critical events with abort option
            category = e.get("category", "")
            has_abort = any(opt.get("abortsEnlistment") for opt in opts)
            is_onboarding = category == "onboarding" or e.get("timing", {}).get("oneTime")

            if len(opts) == 1 or len(opts) > 6:
                bad.append(("BAD_OPT_COUNT", eid, len(opts), f))
            elif len(opts) > 4 and not (is_onboarding or has_abort):
                bad.append(
                    (
                        "BAD_OPT_COUNT",
                        eid,
                        len(opts),
                        "(5-6 options only allowed for onboarding/abort events)",
                    )
                )

            for opt in opts:
                opt_id = opt.get("id", "unknown")
                check_fallback_fields(opt, f"event:{eid} option:{opt_id}", bad)

            total += 1

    # Sanity counts
    c = Counter()
    for b in bad:
        c[b[0]] += 1

    print(f"[validate] files={len(files)} events={total} unique={len(seen)}")
    if bad:
        print("[validate] FAIL")
        for b in bad[:100]:
            print("[validate]", b)
        print("[validate] summary:", dict(c))
        return 2

    print("[validate] OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
