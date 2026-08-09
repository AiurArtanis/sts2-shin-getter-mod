#!/usr/bin/env python3
"""Static regression gate for issue#120."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    text = (ROOT / "src/Models/Powers/SGP_Airborne.cs").read_text(encoding="utf-8-sig")
    for fragment in (
        "VulnerablePower? vulnerable = await PowerCmd.Apply<VulnerablePower>",
        "vulnerable.SkipNextDurationTick = false;",
    ):
        if fragment not in text:
            raise AssertionError(f"SGP_Airborne.cs: missing {fragment!r}")
    print("issue#120 static validation passed")


if __name__ == "__main__":
    main()
