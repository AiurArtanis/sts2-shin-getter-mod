#!/usr/bin/env python3
"""Static regression gate for issue#105 and issue#107."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def read(relative_path: str) -> str:
    return (ROOT / relative_path).read_text(encoding="utf-8-sig")


def require(relative_path: str, *fragments: str) -> None:
    text = read(relative_path)
    for fragment in fragments:
        if fragment not in text:
            raise AssertionError(f"{relative_path}: missing {fragment!r}")


def main() -> None:
    require(
        "src/Models/Cards/SGC_Radiated.cs",
        "var self = Owner.Creature;",
        "creature != self",
        "null,",
        "public override int MaxUpgradeLevel => 0;",
    )
    require(
        "src/Models/CardPools/ShinGetterCardPool.cs",
        "not SGC_Radiated",
    )
    print("issue#105 issue#107 static validation passed")


if __name__ == "__main__":
    main()
