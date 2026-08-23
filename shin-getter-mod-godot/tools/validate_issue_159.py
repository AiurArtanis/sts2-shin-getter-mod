#!/usr/bin/env python3
"""Focused static validation for issue#159 Shin Getter animation-library rebuild."""

from __future__ import annotations

import hashlib
from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
REPOSITORY = PROJECT.parent
SOURCE_ROOT = REPOSITORY / "art_sources/characters/shin_getter/forms"
OUTPUT_ROOT = PROJECT / "images/characters/shin_getter/forms"
FRAME_SIZE = 720

EXPECTED_SOURCE_DIGESTS = {
    "getter_one_attack": "e8759aefa0c872273ab7fe159cc5c59080ef3810cb3ac577c7036006d4cdcfbd",
    "getter_one_block": "43750ca6928b44cfff565c6bdbf28c572a73273b5a57ffce3b3dc12605e5ccb5",
    "getter_one_cast": "2607616150d7133011703f688d40293f3f871e83e14a3e4634ce5ab145ad40fb",
    "getter_one_dash": "d2a30050ededbb4247eef0d88948088e51f57fe4fece0bbc42cd58dd42ed34d0",
    "getter_one_death": "a05f17270502067bb85ce953384bee1edcbe9e8c016f1e54fc77d5fcf612e22b",
    "getter_one_fusion": "2f7fe0bc985a4d209468e575c9f933a17d0919b9150cb8e13fb8c9f8ad7af138",
    "getter_two_idle": "74a3f4ae29cfa91eb75b98caf2a4b13dfbfc657459a2e4abaab7b03351d71cd2",
    "getter_two_attack": "4b34f35016b32713dc458655db9615a0d86179c493c2ecd0e53c13b15ad4ae98",
    "getter_two_block": "d9b576fd4e5958c15a82fac41053e2262b7f91cd7216174b38c5ffef979cb4f7",
    "getter_two_cast": "a340c1a79b4ab6441a0b77f95fec60b553636982821616eafc917a8202c0eee0",
    "getter_two_dash": "468b7cf7b7e9ea31803c78320e763f896d91dae9545e30ea3540991980bcabfb",
    "getter_two_death": "94b9b534635789851a6de3459bd5ca189dbab35971c724e083d56c9ec7e43262",
    "getter_two_fusion": "bba8a9c1008b5bd67f117579f2ff6665800d2e7aa032468e1d1c0671bdc30f44",
    "getter_three_idle": "1ab39c63b930c357f2eb6ddd82e9a7d47481267465735f152f4b71e9ab6349d1",
    "getter_three_attack": "fd511dc5e1421b705754173bebcce4be6ee0ad7bf0fd8cb9a6a37baf20ad4b92",
    "getter_three_block": "e6cbc01151cf1d1b6fb9289a0360bbe9b19c92bec51ea88563503b5c29daf6a3",
    "getter_three_cast": "9384cb0bbccbd6f925e122ae4a3297efc67e7f4db54e70f436c9e69b1a447371",
    "getter_three_dash": "2da095d5e5bdba0813108b2b9f27f0df096a36a123fc58ea103ba8b833f3835a",
    "getter_three_death": "605676f27ce83226f3e9e9506887b3b70505685b190d913d41d78e2d6b918f0f",
    "getter_three_fusion": "cbd9d0e8f6fd819e23fb1a554805cda2129e7ab46cff53da4290ecd0057b295b",
    "shin_getter_dragon_idle": "d1e264805e0abdc9f8f03c2c0235164e0cd0ca6c7ff71b1dd65496889b1a2f1f",
    "shin_getter_dragon_attack": "e052fef344f58791f2c0d5f10667279ee31c37cd4199d6c05a858a924196ab5c",
    "shin_getter_dragon_block": "afcb546cf449d79638697728fbae095b9c07b30182b1948c79cb95c027705002",
    "shin_getter_dragon_cast": "d115291a58cc02f9b94770d49acf6bb139d20f76281aaf10701b0989ca2f7885",
    "shin_getter_dragon_dash": "27f81876b12d4d2e879d55d992f317d79daba5600503ec4efbe41cdb9bf2fab3",
    "shin_getter_dragon_death": "686a50ba8e08b27e51ad9503a181674392bfdf437d327d4a3915512127af3084",
    "shin_getter_dragon_cyclone": "b98b6603cabc197962228ace4c1999aca6e436f509cda4f4bf5cde8e6d88ba33",
    "shin_getter_dragon_dash_v2": "1439ad27e079681fdd3636423cee7587e312ad618c3bf5297d9a2a099bbff443",
    "shin_getter_dragon_drill_attack": "be97edf3f43d5e197173b72f7ae6e70fe014149d47c5dd6102eef64140e1d46a",
}

EXPECTED_FRAME_COUNTS = {
    "getter_one_attack": 40,
    "getter_one_block": 24,
    "getter_one_cast": 32,
    "getter_one_dash": 48,
    "getter_one_death": 48,
    "getter_one_fusion": 30,
    "getter_two_idle": 24,
    "getter_two_attack": 40,
    "getter_two_block": 24,
    "getter_two_cast": 32,
    "getter_two_dash": 48,
    "getter_two_death": 48,
    "getter_two_fusion": 30,
    "getter_three_idle": 24,
    "getter_three_attack": 40,
    "getter_three_block": 24,
    "getter_three_cast": 32,
    "getter_three_dash": 48,
    "getter_three_death": 48,
    "getter_three_fusion": 30,
    "shin_getter_dragon_idle": 36,
    "shin_getter_dragon_attack": 60,
    "shin_getter_dragon_block": 48,
    "shin_getter_dragon_cast": 32,
    "shin_getter_dragon_dash": 48,
    "shin_getter_dragon_death": 48,
    "shin_getter_dragon_cyclone": 60,
    "shin_getter_dragon_dash_v2": 60,
    "shin_getter_dragon_drill_attack": 60,
}

SPECIAL_CARD_GROUPS = {
    "Cyclone": (
        "SGC_Avalanche", "SGC_PoseidonThunder", "SGC_GetterMissile",
        "SGC_FocusFire", "SGC_Annihilation",
    ),
    "DashV2": (
        "SGC_StarSlash", "SGC_GetterRush", "SGC_Acceleration",
        "SGC_GetterFlash", "SGC_PetalBreakthrough",
    ),
    "DrillAttack": (
        "SGC_TornadoDrill", "SGC_SpiralDrill", "SGC_LigerAssault",
        "SGC_GetterClaw", "SGC_HurricaneStrike",
    ),
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def aggregate_digest(frames: list[Path]) -> str:
    digest = hashlib.sha256()
    for frame in frames:
        digest.update(frame.read_bytes())
    return digest.hexdigest()


def check_authoritative_sources() -> None:
    for action, expected_hash in EXPECTED_SOURCE_DIGESTS.items():
        frames = sorted((SOURCE_ROOT / action).glob("sprite_*.png"))
        expected_count = EXPECTED_FRAME_COUNTS[action]
        require(len(frames) == expected_count, f"{action}: expected {expected_count} source frames")
        require(aggregate_digest(frames) == expected_hash, f"{action}: source frames differ from the approved cleanup")
        for frame in frames:
            with Image.open(frame) as image:
                require(image.size == (FRAME_SIZE, FRAME_SIZE), f"{frame}: expected 720x720")
                require(image.mode == "RGBA", f"{frame}: background-cleaned source must retain RGBA")


def check_builder_and_sheets() -> None:
    builder = read(PROJECT / "tools/build_character_sprite_sheets.py")
    manifest = read(SOURCE_ROOT / "frame_manifest.txt")
    resource_validator = read(PROJECT / "tools/validate-mod-resources.gd")
    require("sequence contiguous_60=" in manifest,
            "60-frame special actions require a declared contiguous source sequence")
    for action in (
        "shin_getter_dragon_cyclone",
        "shin_getter_dragon_dash_v2",
        "shin_getter_dragon_drill_attack",
    ):
        require(f'"{action}": 60' in builder, f"{action}: builder count is missing")
        require(f"action {action}=contiguous_60" in manifest, f"{action}: manifest entry is missing")
        sheet = OUTPUT_ROOT / action / "sprite_sheet.png"
        sidecar = sheet.with_name("sprite_sheet.png.import")
        require(sheet.is_file() and sidecar.is_file(), f"{action}: sheet or import sidecar is missing")
        with Image.open(sheet) as image:
            require(image.size == (7200, 4320), f"{action}: expected a 10x6 720px sheet")
        sidecar_text = read(sidecar)
        require('"vram_texture": false' in sidecar_text and "compress/mode=0" in sidecar_text,
                f"{action}: must use the lossless non-VRAM action policy")
        resource_path = f"res://images/characters/shin_getter/forms/{action}/sprite_sheet.png"
        require(resource_path in resource_validator, f"{action}: PCK resource check is missing")

    require("EXPECTED_CHARACTER_SOURCE_FRAME_COUNT := 1370" in resource_validator,
            "PCK source-frame exclusion count must include the three new 60-frame actions")


def check_runtime_wiring() -> None:
    sequence = read(PROJECT / "src/Nodes/Combat/NShinGetterSpriteSequence.cs")
    state_machine = read(PROJECT / "src/Nodes/Combat/NShinGetterSpriteAnimationStateMachine.cs")
    card_base = read(PROJECT / "src/Models/Cards/ShinGetterCardBase.cs")
    star_slash = read(PROJECT / "src/Models/Cards/SGC_StarSlash.cs")
    getter_flash = read(PROJECT / "src/Models/Cards/SGC_GetterFlash.cs")

    for fragment in (
        'ShinDragonCycloneFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_cyclone"',
        'ShinDragonDashV2FrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_dash_v2"',
        'ShinDragonDrillAttackFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_drill_attack"',
        'CycloneAnimationName = "cyclone"',
        'DashV2AnimationName = "dash_v2"',
        'DrillAttackAnimationName = "drill_attack"',
        "ShinDragonSpecialMaxFrames = 60",
        "ShinDragonSpecialFramesPerSecond = 24d",
    ):
        require(fragment in sequence, f"sprite sequence is missing: {fragment}")

    for trigger, animation in (
        ("Cyclone", "CycloneAnimationName"),
        ("DashV2", "DashV2AnimationName"),
        ("DrillAttack", "DrillAttackAnimationName"),
    ):
        require(f'"{trigger}" => NShinGetterSpriteSequence.{animation}' in state_machine,
                f"state machine is missing {trigger}")

    require("ShouldKeepActiveSpecialAnimation" in state_machine
            and "IsSpecialAnimation" in state_machine,
            "generic engine triggers must not interrupt an active Shin Dragon special animation")
    require("Owner.Creature.GetPower<SGP_ShinForm>() != null" in card_base,
            "special card mapping must be gated to the current Shin Getter Dragon form")
    for trigger, cards in SPECIAL_CARD_GROUPS.items():
        for card in cards:
            require(f'["{card}"] = "{trigger}"' in card_base,
                    f"{card}: missing Shin Getter Dragon {trigger} mapping")

    require('GetActionAnimationTrigger() ?? "Attack"' in star_slash,
            "Star Slash manual timing must request DashV2 in Shin Getter Dragon")
    require('GetActionAnimationTrigger() ?? "Attack"' in getter_flash,
            "Getter Flash manual timing must request DashV2 in Shin Getter Dragon")


def main() -> None:
    check_authoritative_sources()
    check_builder_and_sheets()
    check_runtime_wiring()
    print("issue#159 validation passed")


if __name__ == "__main__":
    main()
