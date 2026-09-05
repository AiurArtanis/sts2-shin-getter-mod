#!/usr/bin/env python3
"""Static regression gate for issue#216 NPower initialization safety."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    patch = (ROOT / "src/Patches/ShinGetterPowerUiPatch.cs").read_text(encoding="utf-8-sig")
    flash_section = patch.split("internal static class ShinGetterPowerIconFlashPatch", 1)[1]
    flash_section = flash_section.split("[HarmonyPatch", 1)[0]
    transition_section = patch.split("internal static class ShinGetterPowerIconTransitionPatch", 1)[1]
    transition_section = transition_section.split("[HarmonyPatch", 1)[0]

    require("internal static class ShinGetterPowerModelAccess" in patch,
            "Reload postfixes must share one nullable NPower model accessor")
    require('FieldRefAccess<NPower, PowerModel?>("_model")' in patch,
            "Power model accessor must inspect NPower's nullable backing field")

    for name, section in (("flash", flash_section), ("transition", transition_section)):
        require("PowerModel? power = ShinGetterPowerModelAccess.Get(__instance);" in section,
                f"{name} Reload postfix must read the nullable model before using it")
        require("power == null" in section,
                f"{name} Reload postfix must skip calls made before Model assignment")
        require("__instance.Model" not in section,
                f"{name} Reload postfix must not call the throwing Model getter during initialization")

    require("%PowerFlash\").Texture = power.Icon;" in flash_section,
            "Power icon patch must still apply the Shin Getter power icon after Model assignment")
    require("IsShinGetterFormPower(power)" in transition_section,
            "Power transition patch must preserve the form-power filter")

    print("issue#216 static validation passed")


if __name__ == "__main__":
    main()
