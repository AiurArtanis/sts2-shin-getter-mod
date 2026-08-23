#!/usr/bin/env python3
"""Static regression gate for issue#166."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    source = (ROOT / "src/Patches/ShinGetterCardFramePatch.cs").read_text(
        encoding="utf-8-sig"
    )

    if "frame.Texture = model.Frame;" not in source:
        raise AssertionError("Dynamic tint must preserve CardModel's type-specific frame")

    for forbidden in (
        "DynamicFrameTexturePath",
        "SharedDynamicFrameTexture",
        "card_frame_attack_s.tres",
        "GetFrameTexture(model)",
    ):
        if forbidden in source:
            raise AssertionError(f"Forbidden attack-frame override remains: {forbidden}")

    print("issue#166 static validation passed")


if __name__ == "__main__":
    main()
