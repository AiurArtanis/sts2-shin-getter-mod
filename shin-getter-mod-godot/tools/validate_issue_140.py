#!/usr/bin/env python3
"""Static regression gate for issue#140."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    source = (ROOT / "src/Patches/ShinGetterCardPlayVoicePatch.cs").read_text(
        encoding="utf-8-sig"
    )
    for fragment in (
        "ShinGetterVoiceService.TryPlayCardVoiceAtCardPlayStart(__instance);",
        "__instance is Maul",
        "NShinGetterStaticVisuals.TryPlayCreatureActionAnimation",
    ):
        if fragment not in source:
            raise AssertionError(f"Missing issue#140 assertion: {fragment}")

    if source.index("TryPlayCardVoiceAtCardPlayStart") > source.index("__instance is Maul"):
        raise AssertionError("Maul must retain its card-play voice before skipping pre-animation")
    if source.index("__instance is Maul") > source.index(
        "TryPlayCreatureActionAnimation"
    ):
        raise AssertionError("Maul must skip the duplicate pre-animation trigger")

    print("issue#140 static validation passed")


if __name__ == "__main__":
    main()
