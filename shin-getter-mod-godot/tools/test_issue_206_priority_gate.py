#!/usr/bin/env python3
"""Static string fixtures for the actual encounter-priority gate; no game/runtime."""
from pathlib import Path
import validate_issue_206 as gate

source = (Path(__file__).resolve().parents[1] / "src/Services/ShinGetterBondSession.cs").read_text(encoding="utf-8-sig")
loaded = "ShinGetterBondSave save = _save!;"
first = "if (!IsAcquainted(save, _npc)) return WithDialogue(encounter, first);"
choices = "if (Choices.Count != 0) return encounter;"
alias = "using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;"
assert all(token in source for token in (loaded, first, choices, alias))
gate.encounter_priority(source)
swapped = source.replace(first, "__PRIORITY_FIXTURE__").replace(choices, first).replace("__PRIORITY_FIXTURE__", choices)
fixtures = {
    "missing loaded-save local": source.replace(loaded, ""),
    "late loaded-save local": source.replace(loaded, "").replace(first, first + "\n" + loaded),
    "choices before initial meeting": swapped,
    "wrong RNG alias": source.replace(alias, "using RandomNumberGenerator = Godot.RandomNumberGenerator;"),
}
for name, broken in fixtures.items():
    try:
        gate.encounter_priority(broken)
    except AssertionError:
        continue
    raise AssertionError("Invalid static fixture accepted: " + name)
print(f"issue#206 priority gate PASS: positive + {len(fixtures)} rejected static fixtures (not native/runtime)")
