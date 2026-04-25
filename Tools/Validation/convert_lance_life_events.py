import argparse
import html
import json
import re
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs" / "research"
OUT_DIR = ROOT / "ModuleData" / "Enlisted" / "Events"
LANG_XML = ROOT / "ModuleData" / "Languages" / "enlisted_strings.xml"

LEAVING_BATTLE_INCIDENT_EVENT_IDS = {
    # Immediate post-battle moments (incident channel)
    "medic_surgical_assist",
    "arm_battle_repairs",
    "arm_captured_equipment",
    "eng_equipment_repair",
    "commander_onboard_03_first_blood",
}


@dataclass(frozen=True)
class MetaRow:
    event_id: str
    time_tokens: list[str]
    trigger_expr: str
    cooldown_days: int
    category: str
    duty: str | None
    formation: str | None
    priority: str
    tier_min: int
    tier_max: int


def _slugify(s: str) -> str:
    s = s.strip().lower()
    s = re.sub(r"[^a-z0-9]+", "_", s)
    s = re.sub(r"_+", "_", s)
    return s.strip("_") or "opt"


def _make_string_id(*parts: str) -> str:
    p = [x.strip() for x in parts if x and x.strip()]
    return "_".join(p)


def _xml_attr_escape(text: str) -> str:
    # enlisted_strings.xml stores content in a text="..." attribute.
    # Keep it stable and one-line by converting real newlines to \n.
    t = (text or "").replace("\r\n", "\n").replace("\r", "\n")
    t = t.replace("\n", "\\n")
    return html.escape(t, quote=True)


def _upsert_enlisted_strings(string_table: dict[str, str]) -> None:
    """
    Adds missing <string id="..." text="..."/> entries to ModuleData/Languages/enlisted_strings.xml.
    Does not overwrite existing IDs.
    """
    if not string_table:
        return

    if not LANG_XML.exists():
        raise FileNotFoundError(f"Missing language file: {LANG_XML}")

    xml_text = LANG_XML.read_text(encoding="utf-8")
    # Collect existing ids quickly.
    existing = set(re.findall(r'id="([^"]+)"', xml_text, flags=re.IGNORECASE))

    additions: list[str] = []
    for sid in sorted(string_table.keys(), key=lambda s: s.lower()):
        if sid in existing:
            continue
        val = string_table[sid]
        additions.append(
            f'    <string id="{_xml_attr_escape(sid)}" text="{_xml_attr_escape(val)}" />'
        )

    if not additions:
        return

    marker = "</strings>"
    idx = xml_text.rfind(marker)
    if idx < 0:
        raise RuntimeError("Failed to find </strings> in enlisted_strings.xml")

    insert = "\n\n    <!-- Lance Life Events (auto-generated) -->\n" + "\n".join(additions) + "\n\n"
    updated = xml_text[:idx] + insert + xml_text[idx:]
    LANG_XML.write_text(updated, encoding="utf-8")


def _banner(title: str) -> list[str]:
    line = "    <!-- ═══════════════════════════════════════════════════════════════ -->"
    return [line, f"    <!-- {title} -->", line]


def _rewrite_lance_life_events_section_xml(default_texts: dict[str, str]) -> None:
    """
    Rewrites the Lance Life Events section in enlisted_strings.xml to match the project’s
    section-header style (banner comments) and keep a stable order for translators.
    """
    if not LANG_XML.exists():
        raise FileNotFoundError(f"Missing language file: {LANG_XML}")

    xml_text = LANG_XML.read_text(encoding="utf-8")

    begin_marker = "    <!-- BEGIN AUTO-GENERATED: LanceLifeEvents -->"
    end_marker = "    <!-- END AUTO-GENERATED: LanceLifeEvents -->"

    # Build ordered list by reading the current generated event packs (stable grouping).
    pack_files = [
        ("Duty Events — Quartermaster", OUT_DIR / "events_duty_quartermaster.json"),
        ("Duty Events — Scout", OUT_DIR / "events_duty_scout.json"),
        ("Duty Events — Field Medic", OUT_DIR / "events_duty_field_medic.json"),
        ("Duty Events — Messenger", OUT_DIR / "events_duty_messenger.json"),
        ("Duty Events — Armorer", OUT_DIR / "events_duty_armorer.json"),
        ("Duty Events — Runner", OUT_DIR / "events_duty_runner.json"),
        ("Duty Events — Lookout", OUT_DIR / "events_duty_lookout.json"),
        ("Duty Events — Engineer", OUT_DIR / "events_duty_engineer.json"),
        ("Duty Events — Boatswain (Naval)", OUT_DIR / "events_duty_boatswain.json"),
        ("Duty Events — Navigator (Naval)", OUT_DIR / "events_duty_navigator.json"),
        ("Training Events", OUT_DIR / "events_training.json"),
        ("General Events", OUT_DIR / "events_general.json"),
        ("Onboarding Events", OUT_DIR / "events_onboarding.json"),
        ("Escalation Threshold Events", OUT_DIR / "events_escalation_thresholds.json"),
    ]

    def _load_events(path: Path) -> list[dict]:
        if not path.exists():
            return []
        obj = json.loads(path.read_text(encoding="utf-8"))
        return obj.get("events") or []

    section_lines: list[str] = []
    section_lines.extend(_banner("LANCE LIFE EVENTS - JSON Event Packs (auto-generated)"))
    section_lines.append(begin_marker)

    for section_name, path in pack_files:
        events = _load_events(path)
        if not events:
            continue

        section_lines.append("")
        section_lines.extend(_banner(section_name))

        for evt in events:
            evt_id = (evt.get("id") or "").strip()
            content = evt.get("content") or {}
            title = (content.get("title") or "").strip()
            section_lines.append(f"    <!-- Event: {evt_id} — {title} -->")

            ids_in_order: list[str] = []
            if content.get("titleId"):
                ids_in_order.append(content["titleId"])
            if content.get("setupId"):
                ids_in_order.append(content["setupId"])

            for opt in content.get("options") or []:
                if opt.get("textId"):
                    ids_in_order.append(opt["textId"])
                if opt.get("resultTextId"):
                    ids_in_order.append(opt["resultTextId"])
                if opt.get("resultFailureTextId"):
                    ids_in_order.append(opt["resultFailureTextId"])

            variants = evt.get("variants") or {}
            if isinstance(variants, dict) and variants:
                for v_key in sorted(variants.keys(), key=lambda s: (s or "").lower()):
                    v = variants.get(v_key) or {}
                    v_setup_id = v.get("setupId")
                    if v_setup_id:
                        ids_in_order.append(v_setup_id)
                    for opt in v.get("options") or []:
                        if opt.get("textId"):
                            ids_in_order.append(opt["textId"])
                        if opt.get("resultTextId"):
                            ids_in_order.append(opt["resultTextId"])
                        if opt.get("resultFailureTextId"):
                            ids_in_order.append(opt["resultFailureTextId"])

            # Emit strings (stable, no duplicates)
            seen_local = set()
            for sid in ids_in_order:
                if not sid or sid in seen_local:
                    continue
                seen_local.add(sid)
                val = default_texts.get(sid, "")
                section_lines.append(
                    f'    <string id="{_xml_attr_escape(sid)}" text="{_xml_attr_escape(val)}" />'
                )

            section_lines.append("")

    section_lines.append(end_marker)
    new_block = "\n".join(section_lines).rstrip() + "\n"

    # Replace existing block.
    if begin_marker in xml_text and end_marker in xml_text:
        pre, rest = xml_text.split(begin_marker, 1)
        _, post = rest.split(end_marker, 1)
        xml_text = pre + new_block + post
    else:
        # Backward-compat: replace from the old marker comment to </strings>
        old_marker = "    <!-- Lance Life Events (auto-generated) -->"
        if old_marker in xml_text:
            pre, rest = xml_text.split(old_marker, 1)
            end_idx = rest.rfind("</strings>")
            if end_idx < 0:
                raise RuntimeError("Failed to find </strings> in enlisted_strings.xml")
            post = rest[end_idx:]
            xml_text = pre + new_block + post
        else:
            # Fallback: insert before </strings>
            marker = "</strings>"
            idx = xml_text.rfind(marker)
            if idx < 0:
                raise RuntimeError("Failed to find </strings> in enlisted_strings.xml")
            xml_text = xml_text[:idx] + "\n" + new_block + xml_text[idx:]

    LANG_XML.write_text(xml_text, encoding="utf-8")


def _normalize_time_token(t: str) -> str | None:
    t = t.strip().lower()
    if not t or t == "—":
        return None
    if t in {"any"}:
        return "any"
    # Accept schema vocabulary tokens directly.
    if t in {"dawn", "morning", "afternoon", "evening", "dusk", "night", "late_night"}:
        return t
    # Some tables use ranges like "dawn-afternoon" or "morning-evening"
    if "-" in t:
        left, right = [x.strip() for x in t.split("-", 1)]
        mapping = {
            "dawn": ["dawn"],
            "morning": ["morning"],
            "afternoon": ["afternoon"],
            "evening": ["evening"],
            "dusk": ["dusk"],
            "night": ["night"],
            "late_night": ["late_night"],
        }
        return (mapping.get(left) or [left])[0]
    return t


def _normalize_trigger_token(token: str) -> str:
    t = token.strip()
    if not t:
        return ""
    # Unify a few legacy shorthands from markdown tables
    t = re.sub(r"\bweekly\b", "weekly_tick", t, flags=re.IGNORECASE)
    t = re.sub(r"\brandom\b", "random_chance", t, flags=re.IGNORECASE)
    t = re.sub(r"\bchance\b", "chance", t, flags=re.IGNORECASE)
    t = re.sub(r"\bdays_enlisted\b", "days_enlisted", t, flags=re.IGNORECASE)
    return t


def _split_trigger_expr(expr: str) -> tuple[list[str], list[str]]:
    """
    Converts a simple "A AND B OR C" style string into triggers.all / triggers.any.
    Not a full boolean parser; the metadata docs are simple enough for this first pass.
    """
    expr = (expr or "").strip()
    if not expr or expr == "—":
        return [], []

    expr = _normalize_trigger_token(expr)

    # Split OR groups first
    if " OR " in expr:
        parts = [p.strip() for p in expr.split(" OR ") if p.strip()]
        # If parts themselves contain AND, push those into "all" and keep the last term in any.
        any_tokens: list[str] = []
        all_tokens: list[str] = []
        for p in parts:
            if " AND " in p:
                and_parts = [x.strip() for x in p.split(" AND ") if x.strip()]
                all_tokens.extend(and_parts)
            else:
                any_tokens.append(p)
        return [_normalize_trigger_token(x) for x in all_tokens if x], [
            _normalize_trigger_token(x) for x in any_tokens if x
        ]

    # Pure AND
    if " AND " in expr:
        parts = [p.strip() for p in expr.split(" AND ") if p.strip()]
        return [_normalize_trigger_token(x) for x in parts if x], []

    # Single token
    return [_normalize_trigger_token(expr)], []


def _parse_costs(cost: str) -> dict:
    cost = (cost or "").strip()
    out = {"fatigue": 0, "gold": 0, "time_hours": 0}
    if not cost or cost == "—":
        return out

    # Fatigue: "+2 Fatigue"
    m = re.search(r"([+\-−])\s*(\d+)\s*Fatigue", cost, flags=re.IGNORECASE)
    if m:
        sign = m.group(1)
        val = int(m.group(2))
        if sign in {"-", "−"}:
            # Negative fatigue in cost means "rest"; treat as 0 cost.
            out["fatigue"] = 0
        else:
            out["fatigue"] = val

    # Gold: "−25 Gold"
    m = re.search(r"([+\-−])\s*(\d+)\s*Gold", cost, flags=re.IGNORECASE)
    if m:
        sign = m.group(1)
        val = int(m.group(2))
        if sign in {"-", "−"}:
            out["gold"] = val
        else:
            # Rare, but if a "cost" shows +Gold, ignore (it belongs in rewards)
            out["gold"] = 0

    return out


def _parse_reward_xp(reward: str) -> dict[str, int]:
    reward = (reward or "").strip()
    if not reward or reward == "—":
        return {}

    # Example: "+30 Steward, +15 Trade"
    pairs = re.findall(r"\+(\d+)\s*([A-Za-z_]+)", reward)
    xp: dict[str, int] = {}
    for amount_s, skill in pairs:
        amount = int(amount_s)
        key = skill.strip().lower()
        # Keep schema style keys (lowercase) - loader normalizes them.
        xp[key] = xp.get(key, 0) + amount
    return xp


def _parse_escalation_effects_from_text(text: str) -> dict[str, int]:
    text = (text or "").strip()
    effects = {
        "scrutiny": 0,
        "discipline": 0,
        "lance_reputation": 0,
        "medical_risk": 0,
        "fatigue_relief": 0,
    }

    def _add(label: str, key: str) -> None:
        m = re.search(rf"([+\-−])\s*(\d+)\s*{label}", text, flags=re.IGNORECASE)
        if not m:
            return
        sign = m.group(1)
        val = int(m.group(2))
        if sign in {"-", "−"}:
            effects[key] -= val
        else:
            effects[key] += val

    _add("Scrutiny", "scrutiny")
    _add("Discipline", "discipline")
    # Content tables sometimes say "Lance Rep"
    _add("Lance Rep", "lance_reputation")
    _add("Lance Reputation", "lance_reputation")
    _add("Medical Risk", "medical_risk")
    return effects


def _parse_injury_risk(injury: str, default_type: str = "strain") -> dict | None:
    injury = (injury or "").strip()
    if not injury or injury == "—" or injury.lower() in {"none", "n/a"}:
        return None

    m = re.search(r"(\d+)\s*%\s*([A-Za-z\-]+)", injury)
    if not m:
        return None

    chance = int(m.group(1))
    sev = m.group(2).strip().lower()
    # "minor-moderate" -> minor (we can expand later)
    if "minor" in sev:
        severity = "minor"
    elif "moderate" in sev or "mod" in sev:
        severity = "moderate"
    elif "severe" in sev:
        severity = "severe"
    else:
        severity = "minor"

    t = default_type
    if "fall" in injury.lower() or "thrown" in injury.lower() or "strain" in injury.lower():
        t = "strain"
    elif "wound" in injury.lower() or "cut" in injury.lower() or "blade" in injury.lower():
        t = "wound"
    return {"chance": chance, "severity": severity, "type": t}


def _generic_outcome(risk: str) -> str:
    risk = (risk or "").strip().lower()
    if risk == "corrupt":
        return "You do it, knowing it will have consequences."
    if risk == "risky":
        return "You take the risk and see it through."
    return "You carry it out."


def _extract_setup_and_options(block: str, default_injury_type: str) -> tuple[str, list[dict]]:
    # Setup is between "**Setup:**" and "**Options:**"
    setup = ""
    m = re.search(r"\*\*Setup:\*\*\s*(.*?)\n\s*\*\*Options:\*\*", block, flags=re.DOTALL)
    if m:
        setup = m.group(1).strip()

    # Options table: find header row then collect lines starting with "|"
    options: list[dict] = []
    table_m = re.search(
        r"\|\s*Option\s*\|\s*Risk\s*\|\s*Cost\s*\|\s*Reward\s*\|\s*Injury\s*\|.*?\n(.*?)\n\s*(?:---|$)",
        block,
        flags=re.DOTALL,
    )
    if not table_m:
        return setup, options

    rows = []
    for line in table_m.group(1).splitlines():
        line = line.strip()
        if not line.startswith("|"):
            continue
        if re.match(r"^\|\s*-+\s*\|", line):
            continue
        rows.append(line)

    for line in rows:
        cols = [c.strip() for c in line.strip("|").split("|")]
        if len(cols) < 5:
            continue
        opt_text, risk, cost, reward, injury = cols[:5]

        condition = ""
        tag_m = re.match(r"^\[(.+?)\]\s*(.*)$", opt_text)
        if tag_m:
            tag = tag_m.group(1).strip().lower()
            opt_text = tag_m.group(2).strip()
            # Only map the tags we can express as trigger tokens today.
            duty_map = {
                "field medic": "has_duty:field_medic",
                "quartermaster": "has_duty:quartermaster",
                "armorer": "has_duty:armorer",
                "runner": "has_duty:runner",
                "engineer": "has_duty:engineer",
                "scout": "has_duty:scout",
                "lookout": "has_duty:lookout",
                "messenger": "has_duty:messenger",
                "boatswain": "has_duty:boatswain",
                "navigator": "has_duty:navigator",
            }
            condition = duty_map.get(tag, "")

        risk_norm = risk.strip().lower()
        if "corrupt" in risk_norm:
            risk_norm = "corrupt"
        elif "risky" in risk_norm:
            risk_norm = "risky"
        else:
            risk_norm = "safe"

        costs = _parse_costs(cost)
        rewards_xp = _parse_reward_xp(reward)
        eff_from_cost = _parse_escalation_effects_from_text(cost)
        eff_from_reward = _parse_escalation_effects_from_text(reward)
        effects = {
            "scrutiny": eff_from_cost["scrutiny"] + eff_from_reward["scrutiny"],
            "discipline": eff_from_cost["discipline"] + eff_from_reward["discipline"],
            "lance_reputation": eff_from_cost["lance_reputation"]
            + eff_from_reward["lance_reputation"],
            "medical_risk": eff_from_cost["medical_risk"] + eff_from_reward["medical_risk"],
            "fatigue_relief": 0,
        }

        injury_risk = _parse_injury_risk(injury, default_type=default_injury_type)

        opt_id = _slugify(opt_text)[:32]
        option = {
            "id": opt_id,
            "textId": "",
            "text": opt_text,
            "tooltip": None,
            "condition": condition or None,
            "risk": risk_norm,
            "risk_chance": 50 if risk_norm == "risky" else None,
            "costs": costs,
            "rewards": {
                "xp": rewards_xp,
                "gold": 0,
                "relation": {},
                "items": [],
            },
            "effects": effects,
            "flags_set": [],
            "flags_clear": [],
            "resultTextId": "",
            "outcome": _generic_outcome(risk_norm),
            "outcome_failure": None,
            "injury_risk": injury_risk,
            "triggers_event": None,
            "advances_onboarding": False,
        }
        options.append(option)

    return setup, options


def _split_blocks_by_heading(md: str, heading_prefix: str = "### ") -> list[tuple[str, str]]:
    """
    Returns [(heading, blockText)] where blockText includes the heading line.
    """
    lines = md.splitlines()
    blocks: list[tuple[str, list[str]]] = []
    cur_heading = ""
    cur: list[str] = []
    for line in lines:
        if line.startswith(heading_prefix):
            if cur_heading:
                blocks.append((cur_heading, cur))
            cur_heading = line[len(heading_prefix) :].strip()
            cur = [line]
        else:
            if cur_heading:
                cur.append(line)
    if cur_heading:
        blocks.append((cur_heading, cur))
    return [(h, "\n".join(b)) for h, b in blocks]


def _read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def _parse_metadata_index(md: str) -> list[MetaRow]:
    rows: list[MetaRow] = []

    # Duty sections: we use the big tables, parse "ID | Time | Additional Triggers | Cooldown"
    re.split(r"###\s+", md)
    # We'll parse per-duty table rows with a regex that matches "| id | time | triggers | cooldown |"
    duty_row_re = re.compile(
        r"^\|\s*([a-z0-9_]+)\s*\|\s*([^|]+)\|\s*([^|]+)\|\s*(\d+)\s*\|", re.IGNORECASE
    )

    current_duty = None
    for line in md.splitlines():
        if line.startswith("### ") and "(" in line and ")" in line:
            # e.g. "### Quartermaster (5)"
            name = line[4:].split("(")[0].strip().lower()
            duty_map = {
                "quartermaster": "quartermaster",
                "scout": "scout",
                "field medic": "field_medic",
                "messenger": "messenger",
                "armorer": "armorer",
                "runner": "runner",
                "lookout": "lookout",
                "engineer": "engineer",
                "boatswain — naval": "boatswain",
                "navigator — naval": "navigator",
                "boatswain": "boatswain",
                "navigator": "navigator",
            }
            current_duty = duty_map.get(name)
            continue

        m = duty_row_re.match(line.strip())
        if m and current_duty:
            event_id = m.group(1).strip()
            time_txt = m.group(2).strip()
            triggers = m.group(3).strip()
            cooldown = int(m.group(4))
            times = []
            for part in re.split(r"[,\s]+", time_txt.replace("/", " ").replace("—", "").strip()):
                if not part:
                    continue
                tok = _normalize_time_token(part)
                if tok and tok not in times:
                    times.append(tok)
            all_t, any_t = _split_trigger_expr(triggers)
            # We'll store triggers as expr string and re-split later if needed.
            rows.append(
                MetaRow(
                    event_id=event_id,
                    time_tokens=times,
                    trigger_expr=triggers,
                    cooldown_days=cooldown,
                    category="duty",
                    duty=current_duty,
                    formation=None,
                    priority="normal",
                    tier_min=1,
                    tier_max=6,
                )
            )

    # Training table rows appear earlier in metadata index; parse them by matching "| id | formation | time | fatigue | cooldown | injury | skills |"
    training_row_re = re.compile(
        r"^\|\s*([a-z0-9_]+)\s*\|\s*([a-z]+)\s*\|\s*([^|]+)\|\s*(\d+)\s*\|\s*(\d+)\s*\|",
        re.IGNORECASE,
    )
    in_training = False
    for line in md.splitlines():
        if line.strip() == "## Training Events (16)":
            in_training = True
            continue
        if in_training and line.startswith("---"):
            in_training = False
        if not in_training:
            continue

        m = training_row_re.match(line.strip())
        if not m:
            continue
        event_id = m.group(1).strip()
        formation = m.group(2).strip().lower()
        time_txt = m.group(3).strip()
        cooldown = int(m.group(5))

        times = []
        # Keep tokens like "evening," "night", "any"
        time_txt = time_txt.replace("(at_sea)", "").strip()
        for part in re.split(r"[,\s]+", time_txt.replace("/", " ").strip()):
            tok = _normalize_time_token(part)
            if tok and tok not in times:
                times.append(tok)

        rows.append(
            MetaRow(
                event_id=event_id,
                time_tokens=times,
                trigger_expr="",
                cooldown_days=cooldown,
                category="training",
                duty=None,
                formation=formation,
                priority="normal",
                tier_min=1,
                tier_max=6,
            )
        )

    # General events table: parse "gen_*" IDs
    general_row_re = re.compile(
        r"^\|\s*(gen_[a-z0-9_]+)\s*\|\s*([^|]+)\|\s*([^|]+)\|\s*(\d+)\s*\|", re.IGNORECASE
    )
    in_general = False
    for line in md.splitlines():
        if line.strip() == "## General Events (18)":
            in_general = True
            continue
        if in_general and line.startswith("---"):
            in_general = False
        if not in_general:
            continue

        m = general_row_re.match(line.strip())
        if not m:
            continue
        event_id = m.group(1).strip()
        time_txt = m.group(2).strip()
        triggers = m.group(3).strip()
        cooldown = int(m.group(4))
        times = []
        for part in re.split(r"[,\s]+", time_txt.replace("/", " ").strip()):
            tok = _normalize_time_token(part)
            if tok and tok not in times:
                times.append(tok)
        rows.append(
            MetaRow(
                event_id=event_id,
                time_tokens=times,
                trigger_expr=triggers,
                cooldown_days=cooldown,
                category="general",
                duty=None,
                formation=None,
                priority="normal",
                tier_min=1,
                tier_max=6,
            )
        )

    # Escalation + onboarding are authored in their own docs; we don’t rely on the metadata table for those here.
    return rows


def _find_content_blocks(md: str) -> dict[str, str]:
    """
    Key blocks by their visible heading (e.g. "QM-01: Supply Inventory") to block text.
    """
    blocks = _split_blocks_by_heading(md, "### ")
    return {h.strip(): b for h, b in blocks}


def _build_event_from_content(
    meta: MetaRow, content_heading: str, block: str, strings: dict[str, str]
) -> dict:
    # Title is text after colon if present; otherwise use heading
    title = content_heading
    if ":" in content_heading:
        title = content_heading.split(":", 1)[1].strip()

    default_injury_type = "strain" if meta.category == "training" else "wound"
    setup, options = _extract_setup_and_options(block, default_injury_type=default_injury_type)

    all_tokens = ["is_enlisted", "ai_safe"]
    if meta.category == "duty" and meta.duty:
        all_tokens.append(f"has_duty:{meta.duty}")

    # Additional triggers from metadata (best-effort)
    extra_all, extra_any = _split_trigger_expr(meta.trigger_expr)
    for t in extra_all:
        t = _normalize_trigger_token(t)
        if t and t not in all_tokens:
            all_tokens.append(t)

    any_tokens = []
    for t in extra_any:
        t = _normalize_trigger_token(t)
        if t:
            any_tokens.append(t)

    delivery = {
        "method": "automatic",
        "channel": "inquiry",
        "incident_trigger": None,
        "menu": None,
        "menu_section": None,
    }
    if meta.category == "training":
        delivery = {
            "method": "player_initiated",
            "channel": "menu",
            "incident_trigger": None,
            "menu": "enlisted_activities",
            "menu_section": "training",
        }
    elif meta.event_id in LEAVING_BATTLE_INCIDENT_EVENT_IDS:
        delivery = {
            "method": "automatic",
            "channel": "incident",
            "incident_trigger": "LeavingBattle",
            "menu": None,
            "menu_section": None,
        }

    requirements = {
        "duty": meta.duty or "any",
        "formation": meta.formation or "any",
        "tier": {"min": meta.tier_min, "max": meta.tier_max},
    }

    title_id = _make_string_id("ll_evt", meta.event_id, "title")
    setup_id = _make_string_id("ll_evt", meta.event_id, "setup")
    strings[title_id] = title
    strings[setup_id] = setup

    for o in options:
        opt_id = o.get("id") or "opt"
        text_id = _make_string_id("ll_evt", meta.event_id, "opt", opt_id, "text")
        outcome_id = _make_string_id("ll_evt", meta.event_id, "opt", opt_id, "outcome")
        strings[text_id] = o.get("text") or ""
        strings[outcome_id] = o.get("outcome") or ""
        o["textId"] = text_id
        o["resultTextId"] = outcome_id

    evt = {
        "id": meta.event_id,
        "category": meta.category,
        "metadata": {
            "tier_range": {"min": meta.tier_min, "max": meta.tier_max},
            "content_doc": "docs/research",
        },
        "delivery": delivery,
        "triggers": {
            "all": all_tokens,
            "any": any_tokens,
            "time_of_day": meta.time_tokens,
            "escalation_requirements": {},
        },
        "requirements": requirements,
        "timing": {
            "cooldown_days": meta.cooldown_days,
            "priority": meta.priority,
            "one_time": False,
            "rate_limit": {"max_per_week": 0, "category_cooldown_days": 0},
        },
        "content": {
            "titleId": title_id,
            "setupId": setup_id,
            "title": title,
            "setup": setup,
            "options": options,
        },
        "variants": {},
    }
    return evt


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--write", action="store_true", help="Write JSON files under ModuleData/Enlisted/Events/"
    )
    args = parser.parse_args()

    metadata_md = _read_text(DOCS / "event_metadata_index.md")
    content_md = _read_text(DOCS / "lance_life_events_content_library.md")
    onboarding_md = _read_text(DOCS / "onboarding_story_pack.md")
    thresholds_md = _read_text(DOCS / "escalation_threshold_events.md")

    meta_rows = _parse_metadata_index(metadata_md)
    content_blocks = _find_content_blocks(content_md)
    string_table: dict[str, str] = {}

    # Build duty packs by matching heading order within each duty section.
    packs: dict[str, dict] = {}

    def _get_pack(pack_id: str, category: str) -> dict:
        if pack_id not in packs:
            packs[pack_id] = {
                "schemaVersion": "1.0",
                "packId": pack_id,
                "category": category,
                "events": [],
            }
        return packs[pack_id]

    # Map visible headings to meta rows by sequential matching.
    # Duty headings use prefixes like "QM-01", "SC-01", etc. Training uses "INF-TRAIN-01", etc.
    heading_list = list(content_blocks.keys())

    # Build lookup lists per category for stable assignment
    duty_meta = [m for m in meta_rows if m.category == "duty"]
    train_meta = [m for m in meta_rows if m.category == "training"]
    general_meta = [m for m in meta_rows if m.category == "general"]

    # Duty: match by duty sections in the content library: "## Quartermaster Events" etc
    duty_heading_prefixes = {
        "quartermaster": "QM-",
        "scout": "SC-",
        "field_medic": "MED-",
        "messenger": "MSG-",
        "armorer": "ARM-",
        "runner": "RUN-",
        "lookout": "LOOK-",
        "engineer": "ENG-",
        "boatswain": "BOAT-",
        "navigator": "NAV-",
    }

    for duty_id, prefix in duty_heading_prefixes.items():
        duty_rows = [m for m in duty_meta if m.duty == duty_id]
        duty_rows.sort(key=lambda r: r.event_id)
        # Preserve metadata order by reading metadata table order instead of sorting:
        duty_rows = [m for m in duty_meta if m.duty == duty_id]

        duty_heads = [
            h for h in heading_list if h.upper().startswith(prefix) and "-TRAIN-" not in h.upper()
        ]
        # Preserve appearance order in file
        duty_heads.sort(key=lambda h: heading_list.index(h))
        if len(duty_heads) != len(duty_rows):
            print(
                f"[warn] Duty mismatch for {duty_id}: headings={len(duty_heads)} meta={len(duty_rows)}"
            )

        for idx, m in enumerate(duty_rows):
            if idx >= len(duty_heads):
                continue
            h = duty_heads[idx]
            evt = _build_event_from_content(m, h, content_blocks[h], string_table)
            _get_pack(f"duty_{duty_id}", "duty")["events"].append(evt)

    # Training headings: "INF-TRAIN-", "CAV-TRAIN-", "ARCH-TRAIN-", "NAV-TRAIN-"
    training_prefixes = [
        ("infantry", "INF-TRAIN-"),
        ("cavalry", "CAV-TRAIN-"),
        ("archer", "ARCH-TRAIN-"),
        ("naval", "NAV-TRAIN-"),
    ]
    for formation, prefix in training_prefixes:
        rows = [m for m in train_meta if m.formation == formation]
        rows = rows[:]  # keep metadata order
        heads = [h for h in heading_list if h.upper().startswith(prefix)]
        heads.sort(key=lambda h: heading_list.index(h))
        if len(heads) != len(rows):
            print(
                f"[warn] Training mismatch for {formation}: headings={len(heads)} meta={len(rows)}"
            )
        for idx, m in enumerate(rows):
            if idx >= len(heads):
                continue
            h = heads[idx]
            evt = _build_event_from_content(m, h, content_blocks[h], string_table)
            _get_pack("training", "training")["events"].append(evt)

    # General headings: "DAWN-", "DAY-", "EVE-", "DUSK-", "NIGHT-", "LATE-"
    general_prefixes = ["DAWN-", "DAY-", "EVE-", "DUSK-", "NIGHT-", "LATE-"]
    general_heads = [
        h for h in heading_list if any(h.upper().startswith(p) for p in general_prefixes)
    ]
    general_heads.sort(key=lambda h: heading_list.index(h))
    if len(general_heads) != len(general_meta):
        print(f"[warn] General mismatch: headings={len(general_heads)} meta={len(general_meta)}")
    for idx, m in enumerate(general_meta):
        if idx >= len(general_heads):
            continue
        h = general_heads[idx]
        evt = _build_event_from_content(m, h, content_blocks[h], string_table)
        _get_pack("general", "general")["events"].append(evt)

    if args.write:
        OUT_DIR.mkdir(parents=True, exist_ok=True)
        # Write packs to the requested file layout
        file_map = {
            "duty_quartermaster": "events_duty_quartermaster.json",
            "duty_scout": "events_duty_scout.json",
            "duty_field_medic": "events_duty_field_medic.json",
            "duty_messenger": "events_duty_messenger.json",
            "duty_armorer": "events_duty_armorer.json",
            "duty_runner": "events_duty_runner.json",
            "duty_lookout": "events_duty_lookout.json",
            "duty_engineer": "events_duty_engineer.json",
            "duty_boatswain": "events_duty_boatswain.json",
            "duty_navigator": "events_duty_navigator.json",
            "training": "events_training.json",
            "general": "events_general.json",
        }

        for pack_id, filename in file_map.items():
            pack = packs.get(pack_id)
            if not pack:
                continue
            path = OUT_DIR / filename
            path.write_text(json.dumps(pack, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
            print(f"[write] {path} ({len(pack['events'])} events)")

        onboarding_pack = convert_onboarding_pack(onboarding_md)
        (OUT_DIR / "events_onboarding.json").write_text(
            json.dumps(onboarding_pack, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
        )
        print(
            f"[write] {OUT_DIR / 'events_onboarding.json'} ({len(onboarding_pack['events'])} events)"
        )

        thresholds_pack = convert_thresholds_pack(thresholds_md)
        (OUT_DIR / "events_escalation_thresholds.json").write_text(
            json.dumps(thresholds_pack, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
        )
        print(
            f"[write] {OUT_DIR / 'events_escalation_thresholds.json'} ({len(thresholds_pack['events'])} events)"
        )

        # Populate XML localization for all generated strings (titles/setup/options/outcomes).
        string_table.update(collect_strings_from_pack(onboarding_pack))
        string_table.update(collect_strings_from_pack(thresholds_pack))

        # Rewrite the auto-generated section with banner-style organization (no drifting order).
        _rewrite_lance_life_events_section_xml(string_table)
        print(
            f"[write] {LANG_XML} (Lance Life Events section rewritten; {len(string_table)} strings)"
        )
    else:
        print("Packs prepared (dry run):")
        for pack_id, pack in sorted(packs.items()):
            print(f"  - {pack_id}: {len(pack['events'])} events")
        print(f"  - onboarding: {len(convert_onboarding_pack(onboarding_md)['events'])} events")
        print(
            f"  - escalation_thresholds: {len(convert_thresholds_pack(thresholds_md)['events'])} events"
        )


def convert_onboarding_pack(md: str) -> dict:
    """
    Onboarding doc embeds per-variant JSON examples. We treat those as the content source and normalize into schema pack:
    - 1 event per id
    - variants keyed by variant name (first_time/transfer/return/all)
    """
    code_blocks = re.findall(r"```json\s*([\s\S]*?)\s*```", md, flags=re.IGNORECASE)
    by_id: dict[str, dict] = {}

    for raw in code_blocks:
        raw = raw.strip()
        if not raw.startswith("{"):
            continue
        try:
            obj = json.loads(raw)
        except Exception:
            continue

        event_id = (obj.get("id") or "").strip()
        if not event_id:
            continue

        category = (obj.get("category") or "onboarding").strip().lower()
        if category != "onboarding":
            continue

        track = (obj.get("track") or "").strip().lower()
        variant = (obj.get("variant") or "all").strip().lower()
        priority = (obj.get("priority") or "high").strip().lower()
        cooldown_days = int(obj.get("cooldown_days") or 0)
        one_time = bool(obj.get("one_time") or False)
        triggers = obj.get("triggers") or {}
        all_triggers = triggers.get("all") or []
        time_of_day = triggers.get("time_of_day") or []

        setup = (obj.get("setup") or "").strip()
        options = obj.get("options") or []

        # Tier mapping per track per docs
        tier_min, tier_max = (1, 9)
        if track == "enlisted":
            tier_min, tier_max = (1, 4)
        elif track == "officer":
            tier_min, tier_max = (5, 6)
        elif track == "commander":
            tier_min, tier_max = (7, 9)

        title_id = _make_string_id("ll_evt", event_id, "title")
        setup_id_base = _make_string_id("ll_evt", event_id, "setup")

        base = by_id.get(event_id)
        if not base:
            base = {
                "id": event_id,
                "category": "onboarding",
                "track": track,
                "metadata": {
                    "tier_range": {"min": tier_min, "max": tier_max},
                    "content_doc": "docs/research/onboarding_story_pack.md",
                },
                "delivery": {
                    "method": "automatic",
                    "channel": "incident"
                    if event_id in LEAVING_BATTLE_INCIDENT_EVENT_IDS
                    else "inquiry",
                    "incident_trigger": "LeavingBattle"
                    if event_id in LEAVING_BATTLE_INCIDENT_EVENT_IDS
                    else None,
                    "menu": None,
                    "menu_section": None,
                },
                "triggers": {
                    "all": all_triggers,
                    "any": [],
                    "time_of_day": time_of_day,
                    "escalation_requirements": {},
                },
                "requirements": {
                    "duty": "any",
                    "formation": "any",
                    "tier": {"min": tier_min, "max": tier_max},
                },
                "timing": {
                    "cooldown_days": cooldown_days,
                    "priority": priority,
                    "one_time": one_time,
                    "rate_limit": {"max_per_week": 0, "category_cooldown_days": 0},
                },
                "content": {
                    "titleId": title_id,
                    "setupId": setup_id_base,
                    "title": event_id,
                    "setup": setup,
                    "options": [],
                },
                "variants": {},
            }
            by_id[event_id] = base

        # Normalize options to schema option shape
        norm_opts: list[dict] = []
        for o in options:
            if not isinstance(o, dict):
                continue
            opt_id = _slugify(o.get("id") or o.get("text") or "opt")[:32]
            text = (o.get("text") or "").strip()
            risk = (o.get("risk") or "safe").strip().lower()
            outcome = (o.get("outcome") or "").strip()
            effects = o.get("effects") or {}

            # Only keep the escalation-like effects the engine supports today.
            eff = {
                "scrutiny": int(effects.get("scrutiny") or 0),
                "discipline": int(effects.get("discipline") or 0),
                "lance_reputation": int(effects.get("lance_reputation") or 0),
                "medical_risk": int(effects.get("medical_risk") or 0),
                "fatigue_relief": 0,
            }

            text_id = _make_string_id("ll_evt", event_id, "opt", variant, opt_id, "text")
            outcome_id = _make_string_id("ll_evt", event_id, "opt", variant, opt_id, "outcome")

            norm_opts.append(
                {
                    "id": opt_id,
                    "textId": text_id,
                    "text": text,
                    "tooltip": None,
                    "condition": None,
                    "risk": risk,
                    "risk_chance": 50 if risk == "risky" else None,
                    "costs": {"fatigue": 0, "gold": 0, "time_hours": 0},
                    "rewards": {"xp": {}, "gold": 0, "relation": {}, "items": []},
                    "effects": eff,
                    "flags_set": [],
                    "flags_clear": [],
                    "resultTextId": outcome_id,
                    "outcome": outcome or _generic_outcome(risk),
                    "outcome_failure": None,
                    "injury_risk": None,
                    "triggers_event": None,
                    "advances_onboarding": False,
                }
            )

        variant_setup_id = _make_string_id("ll_evt", event_id, "setup", variant)
        base["variants"][variant] = {
            "setupId": variant_setup_id,
            "setup": setup,
            "options": norm_opts,
        }

        # If the base content has no options, use the first encountered variant as a fallback.
        if not base["content"]["options"]:
            base["content"]["options"] = norm_opts

    # Keep stable ordering
    events = list(by_id.values())
    events.sort(key=lambda e: e.get("id", ""))
    return {
        "schemaVersion": "1.0",
        "packId": "onboarding",
        "category": "onboarding",
        "events": events,
    }


def convert_thresholds_pack(md: str) -> dict:
    """
    Converts escalation threshold story pack into schema events.
    These events are emitted as category 'threshold' so the automatic scheduler prioritizes them.
    """
    events: list[dict] = []

    # Split by "### " headings like "SCRUTINY-01: The Warning"
    blocks = _split_blocks_by_heading(md, "### ")
    for heading, block in blocks:
        if ":" not in heading:
            continue

        # Find Event ID
        m_id = re.search(r"\*\*Event ID:\*\*\s*`([^`]+)`", block)
        if not m_id:
            continue
        event_id = m_id.group(1).strip()
        if "{" in event_id or "}" in event_id:
            # Skip templates shown in the "Event ID Convention" section.
            continue

        # Track + threshold
        m_track = re.search(r"\*\*Track:\*\*\s*([A-Za-z ]+)", block)
        track = (m_track.group(1) if m_track else "").strip().lower()
        m_thr = re.search(r"\*\*Threshold:\*\*\s*([+\-]?\d+)", block)
        threshold_val = int(m_thr.group(1)) if m_thr else 0

        trigger_token = ""
        if track == "scrutiny":
            trigger_token = f"scrutiny_{threshold_val}"
        elif track == "discipline":
            trigger_token = f"discipline_{threshold_val}"
        elif "lance reputation" in track:
            trigger_token = f"lance_rep_{threshold_val}"
        elif "medical" in track:
            trigger_token = f"medical_{threshold_val}"

        # Setup between "#### Setup" and "#### Options"
        setup = ""
        m_setup = re.search(
            r"####\s+Setup\s*(.*?)\n####\s+Options", block, flags=re.DOTALL | re.IGNORECASE
        )
        if m_setup:
            setup = m_setup.group(1).strip()

        # Options table: | Option | Text | Risk | Outcome |
        opt_rows: list[tuple[str, str, str, str]] = []
        table_m = re.search(
            r"\|\s*Option\s*\|\s*Text\s*\|\s*Risk\s*\|\s*Outcome\s*\|.*?\n(.*?)\n\s*####\s+Effects",
            block,
            flags=re.DOTALL | re.IGNORECASE,
        )
        if table_m:
            for line in table_m.group(1).splitlines():
                line = line.strip()
                if not line.startswith("|"):
                    continue
                if re.match(r"^\|\s*-+\s*\|", line):
                    continue
                cols = [c.strip() for c in line.strip("|").split("|")]
                if len(cols) < 4:
                    continue
                opt_rows.append((cols[0], cols[1], cols[2], cols[3]))

        # Effects table: | Option | Effects |
        eff_map: dict[str, dict] = {}
        eff_m = re.search(
            r"####\s+Effects\s*(.*?)\n(?:---|\Z)", block, flags=re.DOTALL | re.IGNORECASE
        )
        if eff_m:
            for line in eff_m.group(1).splitlines():
                line = line.strip()
                if not line.startswith("|"):
                    continue
                if re.match(r"^\|\s*-+\s*\|", line):
                    continue
                cols = [c.strip() for c in line.strip("|").split("|")]
                if len(cols) < 2:
                    continue
                key = cols[0].strip().lower()
                eff_text = cols[1].strip()
                eff_map[key] = _parse_escalation_effects_from_text(eff_text)

        schema_options: list[dict] = []
        for opt_key, text, risk, outcome_cell in opt_rows:
            opt_key_norm = opt_key.strip().lower()
            risk_norm = "safe"
            chance = None
            if "corrupt" in risk.lower():
                risk_norm = "corrupt"
            elif "risky" in risk.lower():
                risk_norm = "risky"
                m_ch = re.search(r"\((\d+)%\)", risk)
                chance = int(m_ch.group(1)) if m_ch else 50

            outcome = outcome_cell.strip()
            outcome_failure = None
            if "success:" in outcome.lower() and "failure:" in outcome.lower():
                # Split on Success/Failure markers
                s = re.split(r"\*\*Success:\*\*|\*\*Failure:\*\*", outcome)
                # s[0] is preface
                if len(s) >= 3:
                    outcome = s[1].strip()
                    outcome_failure = s[2].strip()

            base_eff = eff_map.get(
                opt_key_norm,
                {
                    "scrutiny": 0,
                    "discipline": 0,
                    "lance_reputation": 0,
                    "medical_risk": 0,
                    "fatigue_relief": 0,
                },
            )

            # Success/failure effects can be encoded by naming like "comply (success)" in the effects table
            eff_success = eff_map.get(f"{opt_key_norm} (success)")
            eff_failure = eff_map.get(f"{opt_key_norm} (failure)")

            schema_options.append(
                {
                    "id": _slugify(opt_key_norm)[:32],
                    "text": text.strip(),
                    "tooltip": None,
                    "condition": None,
                    "risk": risk_norm,
                    "risk_chance": chance,
                    "costs": {"fatigue": 0, "gold": 0, "time_hours": 0},
                    "rewards": {"xp": {}, "gold": 0, "relation": {}, "items": []},
                    "effects": base_eff,
                    "effects_success": eff_success,
                    "effects_failure": eff_failure,
                    "flags_set": [],
                    "flags_clear": [],
                    "outcome": outcome or _generic_outcome(risk_norm),
                    "outcome_failure": outcome_failure,
                    "injury_risk": None,
                    "triggers_event": None,
                    "advances_onboarding": False,
                }
            )

        # Keep 2-4 options (schema requirement). If the source has more, keep first 4 for now.
        if len(schema_options) > 4:
            schema_options = schema_options[:4]

        title = heading.split(":", 1)[1].strip()
        title_id = _make_string_id("ll_evt", event_id, "title")
        setup_id = _make_string_id("ll_evt", event_id, "setup")

        for o in schema_options:
            opt_id = o.get("id") or "opt"
            t_id = _make_string_id("ll_evt", event_id, "opt", opt_id, "text")
            out_id = _make_string_id("ll_evt", event_id, "opt", opt_id, "outcome")
            o["textId"] = t_id
            o["resultTextId"] = out_id
            if o.get("outcome_failure"):
                fail_id = _make_string_id("ll_evt", event_id, "opt", opt_id, "outcome_failure")
                o["resultFailureTextId"] = fail_id

        events.append(
            {
                "id": event_id,
                "category": "threshold",
                "metadata": {
                    "tier_range": {"min": 1, "max": 6},
                    "content_doc": "docs/research/escalation_threshold_events.md",
                },
                "delivery": {
                    "method": "automatic",
                    "channel": "inquiry",
                    "incident_trigger": None,
                    "menu": None,
                    "menu_section": None,
                },
                "triggers": {
                    "all": ["is_enlisted", "ai_safe"] + ([trigger_token] if trigger_token else []),
                    "any": [],
                    "time_of_day": ["any"],
                    "escalation_requirements": {},
                },
                "requirements": {"duty": "any", "formation": "any", "tier": {"min": 1, "max": 6}},
                "timing": {
                    "cooldown_days": 7,
                    "priority": "high",
                    "one_time": False,
                    "rate_limit": {"max_per_week": 0, "category_cooldown_days": 0},
                },
                "content": {
                    "titleId": title_id,
                    "setupId": setup_id,
                    "title": title,
                    "setup": setup,
                    "options": schema_options,
                },
                "variants": {},
            }
        )

    events.sort(key=lambda e: e.get("id", ""))
    return {
        "schemaVersion": "1.0",
        "packId": "escalation_thresholds",
        "category": "threshold",
        "events": events,
    }


def collect_strings_from_pack(pack: dict) -> dict[str, str]:
    strings: dict[str, str] = {}
    for evt in pack.get("events") or []:
        content = evt.get("content") or {}
        if content.get("titleId"):
            strings[content["titleId"]] = content.get("title") or ""
        if content.get("setupId"):
            strings[content["setupId"]] = content.get("setup") or ""

        for opt in content.get("options") or []:
            if opt.get("textId"):
                strings[opt["textId"]] = opt.get("text") or ""
            if opt.get("resultTextId"):
                strings[opt["resultTextId"]] = opt.get("outcome") or ""
            if opt.get("resultFailureTextId"):
                strings[opt["resultFailureTextId"]] = opt.get("outcome_failure") or ""

        for v in (evt.get("variants") or {}).values():
            if not isinstance(v, dict):
                continue
            if v.get("setupId"):
                strings[v["setupId"]] = v.get("setup") or ""
            for opt in v.get("options") or []:
                if opt.get("textId"):
                    strings[opt["textId"]] = opt.get("text") or ""
                if opt.get("resultTextId"):
                    strings[opt["resultTextId"]] = opt.get("outcome") or ""
                if opt.get("resultFailureTextId"):
                    strings[opt["resultFailureTextId"]] = opt.get("outcome_failure") or ""
    return strings


if __name__ == "__main__":
    main()
