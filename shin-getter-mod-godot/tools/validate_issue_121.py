#!/usr/bin/env python3
"""Static regression gate for issue#121."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    obsolete = (
        "images/atlases/card_atlas.sprites/shin_getter/strike_shin_getter.tres",
        "images/atlases/card_atlas.sprites/shin_getter/defend_shin_getter.tres",
    )
    for relative_path in obsolete:
        if (ROOT / relative_path).exists():
            raise AssertionError(f"obsolete resource still exists: {relative_path}")
    for relative_path in (
        "images/atlases/card_atlas.sprites/shin_getter/s_g_c_strike.tres",
        "images/atlases/card_atlas.sprites/shin_getter/s_g_c_defend.tres",
    ):
        if not (ROOT / relative_path).is_file():
            raise AssertionError(f"active base-card resource is missing: {relative_path}")
    print("issue#121 static validation passed")


if __name__ == "__main__":
    main()
