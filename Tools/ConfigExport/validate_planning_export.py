#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime
from pathlib import Path
from typing import Dict, List


REQUIRED_FILES = [
    "design_guidelines.json",
    "battle_units.json",
    "skills.json",
    "battle_effects.json",
    "battle_rewards.json",
]


def load_items(path: Path) -> List[dict]:
    data = json.loads(path.read_text(encoding="utf-8"))
    items = data.get("items")
    if not isinstance(items, list):
        return []
    return items


def append(logs: List[str], level: str, code: str, message: str):
    logs.append(f"[{level}] {code}: {message}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate exported planning JSON.")
    parser.add_argument("--output", required=True)
    parser.add_argument("--log", required=True)
    args = parser.parse_args()

    output = Path(args.output)
    log = Path(args.log)
    log.parent.mkdir(parents=True, exist_ok=True)

    logs: List[str] = [f"[INFO] Validate start: {datetime.now().isoformat(timespec='seconds')}", f"[INFO] Output: {output}"]
    errors = 0
    warnings = 0

    if not output.exists():
        append(logs, "ERROR", "output.missing", f"Output folder not found: {output}")
        log.write_text("\n".join(logs) + "\n", encoding="utf-8")
        return 1

    for file_name in REQUIRED_FILES:
        fp = output / file_name
        if not fp.exists():
            errors += 1
            append(logs, "ERROR", "file.required_missing", file_name)

    if errors > 0:
        append(logs, "INFO", "summary", f"errors={errors}, warnings={warnings}, success=False")
        log.write_text("\n".join(logs) + "\n", encoding="utf-8")
        return 2

    guidelines = load_items(output / "design_guidelines.json")
    battle_units = load_items(output / "battle_units.json")
    skills = load_items(output / "skills.json")
    effects = load_items(output / "battle_effects.json")
    rewards = load_items(output / "battle_rewards.json")

    append(logs, "INFO", "count.design_guidelines", str(len(guidelines)))
    append(logs, "INFO", "count.battle_units", str(len(battle_units)))
    append(logs, "INFO", "count.skills", str(len(skills)))
    append(logs, "INFO", "count.battle_effects", str(len(effects)))
    append(logs, "INFO", "count.battle_rewards", str(len(rewards)))

    if len(battle_units) == 0:
        errors += 1
        append(logs, "ERROR", "battle_units.empty", "battle_units.json has no rows")
    if len(skills) == 0:
        errors += 1
        append(logs, "ERROR", "skills.empty", "skills.json has no rows")
    if len(effects) == 0:
        errors += 1
        append(logs, "ERROR", "battle_effects.empty", "battle_effects.json has no rows")
    if len(rewards) == 0:
        errors += 1
        append(logs, "ERROR", "battle_rewards.empty", "battle_rewards.json has no rows")
    if len(guidelines) == 0:
        warnings += 1
        append(logs, "WARNING", "design_guidelines.empty", "design_guidelines.json has no rows")

    def check_duplicate_ids(name: str, rows: List[dict]):
        nonlocal errors
        seen = set()
        for idx, row in enumerate(rows, start=1):
            rid = str(row.get("id", "")).strip()
            if not rid:
                errors += 1
                append(logs, "ERROR", "row.id.missing", f"{name}[{idx}] missing id")
                continue
            if rid in seen:
                errors += 1
                append(logs, "ERROR", "row.id.duplicate", f"{name}[{idx}] duplicate id={rid}")
            seen.add(rid)

    check_duplicate_ids("design_guidelines", guidelines)
    check_duplicate_ids("battle_units", battle_units)
    check_duplicate_ids("skills", skills)
    check_duplicate_ids("battle_effects", effects)
    check_duplicate_ids("battle_rewards", rewards)

    unit_ids = {str(x.get("id", "")).strip() for x in battle_units if str(x.get("id", "")).strip()}
    skill_ids = {str(x.get("id", "")).strip() for x in skills if str(x.get("id", "")).strip()}

    for idx, row in enumerate(skills, start=1):
        owner = str(row.get("ownerUnitId", "")).strip()
        if owner and owner not in unit_ids:
            errors += 1
            append(logs, "ERROR", "skills.owner.invalid", f"skills[{idx}] ownerUnitId={owner} not found in battle_units")
        cost = row.get("manaCost", 0)
        try:
            iv = int(cost)
        except Exception:
            errors += 1
            append(logs, "ERROR", "skills.cost.invalid", f"skills[{idx}] manaCost={cost}")
            continue
        if iv < 0:
            errors += 1
            append(logs, "ERROR", "skills.cost.negative", f"skills[{idx}] manaCost={iv}")

    def normalize_id_list(value):
        if value is None:
            return []
        if isinstance(value, list):
            return [str(x).strip() for x in value if str(x).strip()]
        if isinstance(value, str):
            parts = [p.strip() for p in value.replace("；", ";").replace("，", ",").split(";")]
            ids = []
            for part in parts:
                if not part:
                    continue
                for sub in part.split(","):
                    sub = sub.strip()
                    if sub:
                        ids.append(sub)
            return ids
        return [str(value).strip()] if str(value).strip() else []

    for idx, row in enumerate(battle_units, start=1):
        innates = normalize_id_list(row.get("innateSkillIds"))
        for skill_id in innates:
            if skill_id not in skill_ids:
                errors += 1
                append(logs, "ERROR", "battle_units.innate.invalid",
                       f"battle_units[{idx}] innateSkillIds={skill_id} not found in skills")

    for idx, row in enumerate(effects, start=1):
        max_stack = row.get("maxStackCount", 0)
        try:
            max_stack = int(max_stack)
        except Exception:
            errors += 1
            append(logs, "ERROR", "battle_effects.max_stack.invalid", f"battle_effects[{idx}] maxStackCount={max_stack}")
            continue
        if max_stack < 0:
            errors += 1
            append(logs, "ERROR", "battle_effects.max_stack.negative", f"battle_effects[{idx}] maxStackCount={max_stack}")

    for idx, row in enumerate(rewards, start=1):
        if not str(row.get("applyActions", "")).strip():
            warnings += 1
            append(logs, "WARNING", "battle_rewards.apply_actions.empty", f"battle_rewards[{idx}] applyActions empty")

    success = errors == 0
    append(logs, "INFO", "summary", f"errors={errors}, warnings={warnings}, success={success}")
    log.write_text("\n".join(logs) + "\n", encoding="utf-8")
    return 0 if success else 3


if __name__ == "__main__":
    sys.exit(main())

