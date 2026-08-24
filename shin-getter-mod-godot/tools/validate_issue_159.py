#!/usr/bin/env python3
"""Focused static validation for issue#159 Shin Getter animation-library rebuild."""

from __future__ import annotations

import hashlib
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
REPOSITORY = PROJECT.parent
SOURCE_ROOT = REPOSITORY / "art_sources/characters/shin_getter/forms"
OUTPUT_ROOT = PROJECT / "images/characters/shin_getter/forms"
FRAME_SIZE = 720

EXPECTED_SOURCE_DIGESTS = {
    "getter_one_idle": "feb006ab746ecd75ffb645781b81fcb3af50fa3ab589a45032576281a275d708",
    "getter_one_attack": "8b4cadd1cbf421d0b0cc78d6fb2da7d0857e4db57026880bfdbd0fb9734873a9",
    "getter_one_block": "953d44b211a619820dede1a9e3d04bace4b763595ab7c344ec3fc266ebc1240b",
    "getter_one_cast": "a2d00c6ec18c0ee9b62931c69626afea3b3efc34872c3b435f2dbc66dc3cbbab",
    "getter_one_dash": "92fe6c8c4f5eb38a5f1afab808b9355c7a2af5f0b02cbfb3a89505a7811b983c",
    "getter_one_death": "a05f17270502067bb85ce953384bee1edcbe9e8c016f1e54fc77d5fcf612e22b",
    "getter_one_fusion": "2f7fe0bc985a4d209468e575c9f933a17d0919b9150cb8e13fb8c9f8ad7af138",
    "getter_two_idle": "6e0df21b7f0c2c80b746d837b2ffc7fbd26e986619689ac27e79d799ba3bd942",
    "getter_two_attack": "4b34f35016b32713dc458655db9615a0d86179c493c2ecd0e53c13b15ad4ae98",
    "getter_two_block": "d9b576fd4e5958c15a82fac41053e2262b7f91cd7216174b38c5ffef979cb4f7",
    "getter_two_cast": "7b01b262cd532fac07cfc913be21af237c65449e79e45296edc373e8e5c2046b",
    "getter_two_dash": "49eaf905ad00566bc843658d2e142cc7d7ca7ccb111d43deeaeccdd9bd3a2bfd",
    "getter_two_death": "0c397fb4d7aa9abd9a0974038f18fab3d3a8a9a081b1aaba54bc242e1ca7cba5",
    "getter_two_fusion": "bba8a9c1008b5bd67f117579f2ff6665800d2e7aa032468e1d1c0671bdc30f44",
    "getter_three_idle": "34ec6fc021884613011365fcf3df142df62475a8bc1a0c2b2fc47376562aa1ad",
    "getter_three_attack": "4a4a43a0af0c42e5cf9c2710aec21412d3700e27ef6a5b255bfea3d21377f272",
    "getter_three_block": "e6cbc01151cf1d1b6fb9289a0360bbe9b19c92bec51ea88563503b5c29daf6a3",
    "getter_three_cast": "5c4e5a3c2975380e3b68baa8f207874e3839a773f1134590e8438115a81b4b2b",
    "getter_three_dash": "692565a8be7d71af28f145900d73c90ce296909098df6bcd7ec12f36f87fe393",
    "getter_three_death": "605676f27ce83226f3e9e9506887b3b70505685b190d913d41d78e2d6b918f0f",
    "getter_three_fusion": "cbd9d0e8f6fd819e23fb1a554805cda2129e7ab46cff53da4290ecd0057b295b",
    "shin_getter_dragon_idle": "f15b59c00aee5953202bfe513eb10f3de05d4aeeacd305299374779320cd3260",
    "shin_getter_dragon_attack": "958b9278b7416d7faf4307f7db2f2d17358e9596ffad8c0d133aeba87aec7f27",
    "shin_getter_dragon_block": "afcb546cf449d79638697728fbae095b9c07b30182b1948c79cb95c027705002",
    "shin_getter_dragon_cast": "e9e74dc605bb617aab2d7e1875f2e6b8b5592cc84db3f5993bda5079d522ac37",
    "shin_getter_dragon_dash": "27f81876b12d4d2e879d55d992f317d79daba5600503ec4efbe41cdb9bf2fab3",
    "shin_getter_dragon_death": "686a50ba8e08b27e51ad9503a181674392bfdf437d327d4a3915512127af3084",
    "shin_getter_dragon_cyclone": "b98b6603cabc197962228ace4c1999aca6e436f509cda4f4bf5cde8e6d88ba33",
    "shin_getter_dragon_dash_v2": "1439ad27e079681fdd3636423cee7587e312ad618c3bf5297d9a2a099bbff443",
    "shin_getter_dragon_drill_attack": "be97edf3f43d5e197173b72f7ae6e70fe014149d47c5dd6102eef64140e1d46a",
}

EXPECTED_FRAME_COUNTS = {
    "getter_one_idle": 24,
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

SPECIAL_TIMING_EXEMPT_CARDS = {
    "SGC_Annihilation",
    "SGC_Avalanche",
    "SGC_GetterFlash",
    "SGC_GetterMissile",
    "SGC_GetterRush",
    "SGC_HurricaneStrike",
    "SGC_PoseidonThunder",
    "SGC_StarSlash",
}

WATERMARK_ACTIONS = {
    "getter_one_attack": 121,
    "getter_one_block": 121,
    "getter_one_cast": 121,
    "getter_one_dash": 121,
    "getter_two_idle": 241,
    "getter_three_idle": 241,
    "shin_getter_dragon_attack": 121,
    "shin_getter_dragon_idle": 241,
}

CHROMA_CLEAN_ACTIONS = {
    "getter_one_idle",
    "getter_one_block",
    "getter_one_cast",
    "getter_two_cast",
    "getter_two_dash",
    "getter_two_death",
    "getter_three_attack",
    "getter_three_cast",
    "getter_three_dash",
    "shin_getter_dragon_attack",
    "shin_getter_dragon_cast",
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


def frame_number(path: Path) -> int:
    return int(path.stem.rsplit("_", 1)[1])


def has_detached_corner_component(image: Image.Image, number: int, capture_count: int) -> bool:
    alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    height, width = alpha.shape
    switch_frame = int(capture_count * 0.8)
    if number <= switch_frame:
        corner = (570, 660, width, height)
    else:
        corner = (0, 0, 160, 80)
    count, _, stats, _ = cv2.connectedComponentsWithStats(
        (alpha >= 4).astype(np.uint8),
        connectivity=8,
    )
    for label in range(1, count):
        x, y, component_width, component_height, area = stats[label]
        fully_inside = (
            x >= corner[0]
            and y >= corner[1]
            and x + component_width <= corner[2]
            and y + component_height <= corner[3]
        )
        if fully_inside and area <= 9000:
            return True
    return False


def count_visible_strong_magenta(image: Image.Image) -> int:
    rgba = np.asarray(image, dtype=np.int16)
    red, green, blue, alpha = (rgba[:, :, index] for index in range(4))
    visible_magenta = (
        (alpha >= 64)
        & (red >= 160)
        & (blue >= 160)
        & (green * 2 < np.minimum(red, blue))
    )
    return int(visible_magenta.sum())


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


def check_black_background_cleanup() -> None:
    for action, capture_count in WATERMARK_ACTIONS.items():
        for frame in sorted((SOURCE_ROOT / action).glob("sprite_*.png")):
            with Image.open(frame) as image:
                require(
                    not has_detached_corner_component(
                        image.convert("RGBA"),
                        frame_number(frame),
                        capture_count,
                    ),
                    f"{frame}: detached dynamic watermark remains in the known corner",
                )

    for action in CHROMA_CLEAN_ACTIONS:
        for frame in sorted((SOURCE_ROOT / action).glob("sprite_*.png")):
            with Image.open(frame) as image:
                require(
                    count_visible_strong_magenta(image.convert("RGBA")) == 0,
                    f"{frame}: strong magenta pixels remain visible on black",
                )


def check_builder_and_sheets() -> None:
    builder = read(PROJECT / "tools/build_character_sprite_sheets.py")
    cleaner = read(PROJECT / "tools/clean_character_animation_frames.py")
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
    for fragment in (
        '"getter_one_attack": CaptureSource("一号机", "攻击", 121, False, True)',
        '"getter_one_dash": CaptureSource("一号机", "突进", 121, False, True)',
        '"getter_two_idle": CaptureSource("二号机", "待机", 241, False, True)',
        '"getter_three_idle": CaptureSource("三号机", "待机", 241, False, True)',
        '"shin_getter_dragon_idle": CaptureSource("真盖塔龙", "待机", 241, False, True)',
    ):
        require(fragment in cleaner,
                f"watermark-only cleanup must not re-key an already-approved matte: {fragment}")


def check_runtime_wiring() -> None:
    sequence = read(PROJECT / "src/Nodes/Combat/NShinGetterSpriteSequence.cs")
    state_machine = read(PROJECT / "src/Nodes/Combat/NShinGetterSpriteAnimationStateMachine.cs")
    card_base = read(PROJECT / "src/Models/Cards/ShinGetterCardBase.cs")
    star_slash = read(PROJECT / "src/Models/Cards/SGC_StarSlash.cs")
    getter_flash = read(PROJECT / "src/Models/Cards/SGC_GetterFlash.cs")
    getter_missile = read(PROJECT / "src/Models/Cards/SGC_GetterMissile.cs")

    for fragment in (
        'ShinDragonCycloneFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_cyclone"',
        'ShinDragonDashV2FrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_dash_v2"',
        'ShinDragonDrillAttackFrameDirectory = "res://images/characters/shin_getter/forms/shin_getter_dragon_drill_attack"',
        'CycloneAnimationName = "cyclone"',
        'DashV2AnimationName = "dash_v2"',
        'DrillAttackAnimationName = "drill_attack"',
        "ShinDragonSpecialMaxFrames = 60",
        "ShinDragonSpecialFramesPerSecond = 30d",
    ):
        require(fragment in sequence, f"sprite sequence is missing: {fragment}")

    for trigger, animation in (
        ("Cyclone", "CycloneAnimationName"),
        ("DashV2", "DashV2AnimationName"),
        ("DrillAttack", "DrillAttackAnimationName"),
    ):
        require(f'"{trigger}" => NShinGetterSpriteSequence.{animation}' in state_machine,
                f"state machine is missing {trigger}")

    suppression_path = state_machine.split(
        "if (ShouldKeepActiveSpecialAnimation(sprite, state, trigger))", 1
    )[1].split("ensureLoaded(sprite, animationName);", 1)[0]
    require("state.NextActionSpeedScale = 1f;" in suppression_path,
            "suppressed follow-up triggers must consume their queued speed multiplier")
    protection_contract = state_machine.split(
        "private static bool ShouldKeepActiveSpecialAnimation", 1
    )[1].split("private static bool IsSpecialAnimation", 1)[0]
    for protected_trigger in ("Attack", "HeavyAttack", "Cast", "Dash", "Hit"):
        require(f'"{protected_trigger}"' in protection_contract,
                f"active special animations must ignore {protected_trigger}")
    for priority_trigger in ("Dead", "Death"):
        require(f'"{priority_trigger}"' not in protection_contract,
                f"active special animations must still allow {priority_trigger}")
    require("CombatState.Creatures.Where(creature => creature.IsHittable)" in getter_missile
            and "target == Owner.Creature ? null : Owner.Creature" in getter_missile,
            "Getter Missile self-hit regression fixture is missing")
    require("Owner.Creature.GetPower<SGP_ShinForm>() != null" in card_base,
            "special card mapping must be gated to the current Shin Getter Dragon form")
    for trigger, cards in SPECIAL_CARD_GROUPS.items():
        for card in cards:
            require(f'["{card}"] = "{trigger}"' in card_base,
                    f"{card}: missing Shin Getter Dragon {trigger} mapping")

    require("ShinDragonSpecialEffectDelaySeconds = 1.4f" in card_base,
            "Shin Getter Dragon special effects must begin at 1.4 seconds")
    timing_set = card_base.split(
        "private static readonly IReadOnlySet<string> ShinDragonSpecialTimingHandledByCard", 1
    )[1].split("private static readonly IReadOnlySet<string> MovementVfxTimingCards", 1)[0]
    for cards in SPECIAL_CARD_GROUPS.values():
        for card in cards:
            if card in SPECIAL_TIMING_EXEMPT_CARDS:
                require(f'"{card}"' in timing_set,
                        f"{card}: independent effect timing must remain exempt")
            else:
                require(f'"{card}"' not in timing_set,
                        f"{card}: must use the shared 1.4-second effect timing")
    before_card_played = card_base.split("public override async Task BeforeCardPlayed", 1)[1].split(
        "private void PlayCardVoiceAndAnimation", 1
    )[0]
    require("IsShinDragonSpecialAnimationTrigger(animationTrigger)" in before_card_played
            and "ShinDragonSpecialTimingHandledByCard.Contains(cardTypeName)" in before_card_played
            and "await Cmd.CustomScaledWait(" in before_card_played,
            "shared special timing must wait before card effects begin")
    dash_charge = card_base.split("protected Task WaitForDashCharge()", 1)[1].split(
        "protected Func<Task> AccelerateFollowupAnimations", 1
    )[0]
    require('"DashV2" => ShinDragonSpecialEffectDelaySeconds' in dash_charge,
            "Getter Rush DashV2 must preserve its movement-VFX delay without restarting the animation")

    require('GetActionAnimationTrigger() ?? "Attack"' in star_slash,
            "Star Slash manual timing must request DashV2 in Shin Getter Dragon")
    require('GetActionAnimationTrigger() ?? "Attack"' in getter_flash,
            "Getter Flash manual timing must request DashV2 in Shin Getter Dragon")


def main() -> None:
    check_authoritative_sources()
    check_black_background_cleanup()
    check_builder_and_sheets()
    check_runtime_wiring()
    print("issue#159 validation passed")


if __name__ == "__main__":
    main()
