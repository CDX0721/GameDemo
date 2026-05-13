#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime
from pathlib import Path
from typing import Dict, List


REQUIRED_FILES = [
    "characters.json",
    "skills.json",
    "enemies.json",
    "battle_rewards.json",
    "states.json",
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

    characters = load_items(output / "characters.json")
    skills = load_items(output / "skills.json")
    enemies = load_items(output / "enemies.json")
    rewards = load_items(output / "battle_rewards.json")
    states = load_items(output / "states.json")

    append(logs, "INFO", "count.characters", str(len(characters)))
    append(logs, "INFO", "count.skills", str(len(skills)))
    append(logs, "INFO", "count.enemies", str(len(enemies)))
    append(logs, "INFO", "count.rewards", str(len(rewards)))
    append(logs, "INFO", "count.states", str(len(states)))

    if len(characters) == 0:
        errors += 1
        append(logs, "ERROR", "characters.empty", "characters.json has no rows")
    if len(skills) == 0:
        errors += 1
        append(logs, "ERROR", "skills.empty", "skills.json has no rows")

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

    check_duplicate_ids("characters", characters)
    check_duplicate_ids("skills", skills)
    check_duplicate_ids("enemies", enemies)
    check_duplicate_ids("battle_rewards", rewards)
    check_duplicate_ids("states", states)

    char_ids = {str(x.get("id", "")).strip() for x in characters if str(x.get("id", "")).strip()}
    reward_ids = {str(x.get("id", "")).strip() for x in rewards if str(x.get("id", "")).strip()}

    for idx, row in enumerate(skills, start=1):
        owner = str(row.get("ownerRoleId", "")).strip()
        if owner and owner != "通用" and owner not in char_ids:
            errors += 1
            append(logs, "ERROR", "skills.owner.invalid", f"skills[{idx}] ownerRoleId={owner} not found in characters")
        cost = row.get("costValue", 0)
        try:
            iv = int(cost)
        except Exception:
            errors += 1
            append(logs, "ERROR", "skills.cost.invalid", f"skills[{idx}] costValue={cost}")
            continue
        if iv < 0:
            errors += 1
            append(logs, "ERROR", "skills.cost.negative", f"skills[{idx}] costValue={iv}")

    for idx, row in enumerate(enemies, start=1):
        reward_id = str(row.get("rewardId", "")).strip()
        if reward_id and reward_id not in reward_ids:
            errors += 1
            append(logs, "ERROR", "enemies.reward.invalid", f"enemies[{idx}] rewardId={reward_id} not found")

    if len(states) > 0:
        missing_state_name = 0
        for row in states:
            if not str(row.get("stateName", "")).strip():
                missing_state_name += 1
        if missing_state_name > 0:
            warnings += 1
            append(logs, "WARNING", "states.name.missing", f"{missing_state_name} state rows missing stateName")

    success = errors == 0
    append(logs, "INFO", "summary", f"errors={errors}, warnings={warnings}, success={success}")
    log.write_text("\n".join(logs) + "\n", encoding="utf-8")
    return 0 if success else 3


if __name__ == "__main__":
    sys.exit(main())

