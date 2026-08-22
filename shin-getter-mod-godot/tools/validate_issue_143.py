#!/usr/bin/env python3
"""Static regression gate for issue#143."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    patch = ROOT / "src/Patches/ShinGetterSpiritCommandRetainPatch.cs"
    if not patch.is_file():
        raise AssertionError("Missing ShinGetterSpiritCommandRetainPatch.cs")

    text = patch.read_text(encoding="utf-8-sig")
    for fragment in (
        'HarmonyPatch(typeof(CardModel), "get_ShouldRetainThisTurn")',
        "__instance is not ShinGetterCardBase { SpiritRequirement: > 0 }",
        "spiritCommand.CombatState == null",
        "spiritCommand.Owner.Creature.GetPower<SGP_Ki>()?.Amount > 0",
    ):
        if fragment not in text:
            raise AssertionError(f"Missing issue#143 assertion: {fragment}")

    print("issue#143 static validation passed")


if __name__ == "__main__":
    main()
