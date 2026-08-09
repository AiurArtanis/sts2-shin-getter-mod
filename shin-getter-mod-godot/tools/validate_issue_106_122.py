#!/usr/bin/env python3
"""Static regression gate for issue#106 and issue#122."""

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
        "src/Audio/ShinGetterVoiceService.cs",
        "DamageResponse",
        "ActiveDamageResponsePlayer",
        "HasActiveDamageResponse",
        "if (!playVoice)",
    )
    require(
        "src/Models/Cards/ShinGetterCardBase.cs",
        "bool playVoice = true",
        "PlayTransform(player, next, playVoice)",
    )
    require(
        "src/Models/Relics/SGR_TripleWoodCarving.cs",
        "playVoice: false",
    )
    print("issue#106 issue#122 static validation passed")


if __name__ == "__main__":
    main()
