#!/usr/bin/env python3
"""Static regression gate for issue#216 NPower initialization safety."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    patch = (ROOT / "src/Patches/ShinGetterPowerUiPatch.cs").read_text(encoding="utf-8-sig")
    section = patch.split("internal static class ShinGetterPowerIconFlashPatch", 1)[1]
    section = section.split("[HarmonyPatch", 1)[0]

    require('FieldRefAccess<NPower, PowerModel?>("_model")' in section,
            "Power icon patch must inspect NPower's nullable backing model")
    require("PowerModel? power = ModelRef(__instance);" in section,
            "Power icon patch must read the nullable model before using it")
    require("if (power == null" in section,
            "Power icon patch must skip Reload calls made before Model assignment")
    require("__instance.Model.GetType()" not in section,
            "Power icon patch must not call the throwing Model getter during initialization")
    require("%PowerFlash\").Texture = power.Icon;" in section,
            "Power icon patch must still apply the Shin Getter power icon after Model assignment")

    print("issue#216 static validation passed")


if __name__ == "__main__":
    main()
