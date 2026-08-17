#!/usr/bin/env python3
"""Focused static validation for issue#10 transform animation resources and wiring."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
REPOSITORY = PROJECT.parent
FRAME_SIZE = 720
FUSION_ACTIONS = ("getter_one_fusion", "getter_two_fusion", "getter_three_fusion")
EXPECTED_FIRST_FRAME_SHA256 = "31ebc1f3e222299e09c7448ee52cf6cd307c8a18ea5f832844a60dffaf9770ee"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def check_fusion_sheets() -> None:
    manifest = read(REPOSITORY / "art_sources/characters/shin_getter/forms/frame_manifest.txt")
    builder = read(PROJECT / "tools/build_character_sprite_sheets.py")
    require("\"getter_one_fusion\": 30" in builder, "getter_one fusion frame count is missing")
    require("\"getter_two_fusion\": 30" in builder, "getter_two fusion frame count is missing")
    require("\"getter_three_fusion\": 30" in builder, "getter_three fusion frame count is missing")
    require("30: 6" in builder, "fusion sheets must use six columns")
    require("sequence contiguous_30=" in manifest, "30-frame source sequence is missing")

    first_hashes: list[str] = []
    for action in FUSION_ACTIONS:
        require(f"action {action}=contiguous_30" in manifest, f"{action} is not in the manifest")
        source = REPOSITORY / "art_sources/characters/shin_getter/forms" / action
        frames = sorted(source.glob("sprite_*.png"))
        require(len(frames) == 30, f"{action} must have exactly 30 source frames")
        for frame in frames:
            with Image.open(frame) as image:
                require(image.size == (FRAME_SIZE, FRAME_SIZE), f"{frame} is not 720x720")
        first_hashes.append(hashlib.sha256(frames[0].read_bytes()).hexdigest())

        sheet = PROJECT / "images/characters/shin_getter/forms" / action / "sprite_sheet.png"
        with Image.open(sheet) as image:
            require(image.size == (FRAME_SIZE * 6, FRAME_SIZE * 5), f"{sheet} must be a 6x5 sheet")

    require(first_hashes == [EXPECTED_FIRST_FRAME_SHA256] * 3, "fusion sources do not share the authoritative fighter frame")


def check_code_wiring() -> None:
    sequence = read(PROJECT / "src/Nodes/Combat/NShinGetterSpriteSequence.cs")
    visuals = read(PROJECT / "src/Nodes/Combat/NShinGetterStaticVisuals.cs")
    open_get = read(PROJECT / "src/Models/Powers/SGP_OpenGet.cs")
    choice = read(PROJECT / "src/Nodes/Combat/NShinGetterFormChoice.cs")
    shade = read(PROJECT / "src/Models/Powers/SGP_Shade.cs")
    landing = read(PROJECT / "src/Models/Cards/SGC_GetterLanding.cs")
    pool = read(PROJECT / "src/Models/CardPools/ShinGetterCardPool.cs")
    voice = read(PROJECT / "src/Audio/ShinGetterVoiceService.cs")

    require("FusionFramesPerSecond = 60d" in sequence, "fusion animation must run at 60fps")
    require("EnsureFusionLoaded" in sequence and "FusionAnimationName" in sequence, "fusion sequence loader is missing")
    require("TryPlayFusionTransition" in visuals and "PlayOpenGetVfx" in visuals, "fusion visual flow is missing")
    require("PlayShadeVfx" in visuals, "shade visual hook is missing")
    require("DisplayAmount => Amount - 1" in open_get, "Open Get must expose a zero starting counter")
    require("dealer?.Side != Owner.Side" in open_get, "Open Get must account for allied damage")
    require("ModifyDamageMultiplicative" in open_get and "AfterDamageGiven" in open_get, "Open Get avoidance/accounting is missing")
    require("AfterEnergyReset" in open_get, "Open Get must expire at turn start")
    require("PlayShadeVfx" in shade, "Shade does not trigger its visual effect")
    require("NShinGetterFormChoice" in landing, "Getter Landing does not show form choices")
    require("PowerCmd.Remove<SGP_OpenGet>" in landing, "Getter Landing must refresh the unique Open Get status")
    require("ModelDb.Card<SGC_GetterLanding>()" in pool, "Getter Landing is not registered")
    require("and not SGC_GetterLanding" in pool, "Getter Landing must stay out of reward epochs")
    require("PlayerChoiceSynchronizer" in choice and "PlayerChoiceResult.FromIndex" in choice, "form choice must synchronize in multiplayer")
    require("ShouldSelectLocalForm" in choice and "CardSelectCmd.ShouldSelectLocalCard" not in choice,
            "form choice must not call CardSelectCmd's private selector")
    for code in ("058", "059", "060"):
        require(f'new("{code}"' in voice, f"Open Get voice {code} is missing")


def check_localization_and_assets() -> None:
    for locale in ("zhs", "eng", "jpn"):
        root = PROJECT / "ShinGetterMod/localization" / locale
        cards = json.loads(read(root / "cards.json"))
        powers = json.loads(read(root / "powers.json"))
        characters = json.loads(read(root / "characters.json"))
        require("S_G_C_GETTER_LANDING.title" in cards, f"{locale} Getter Landing title is missing")
        require("S_G_C_GETTER_LANDING.description" in cards, f"{locale} Getter Landing description is missing")
        require("S_G_P_OPEN_GET.title" in powers, f"{locale} Open Get title is missing")
        require("S_G_P_OPEN_GET.description" in powers, f"{locale} Open Get description is missing")
        for key in ("SHIN_GETTER.voice.openGetOne", "SHIN_GETTER.voice.openGetTwo", "SHIN_GETTER.voice.openGetThree"):
            require(key in characters, f"{locale} {key} is missing")

    icon = read(PROJECT / "images/atlases/power_atlas.sprites/s_g_p_open_get.tres")
    require("region = Rect2(320, 256, 64, 64)" in icon, "Open Get icon region is wrong")
    resource_gate = read(PROJECT / "tools/validate-mod-resources.gd")
    require("s_g_c_getter_landing.tres" in resource_gate and "s_g_p_open_get.tres" in resource_gate,
            "resource gate does not require issue#10 atlas resources")
    for filename in ("ryoma_open_get.wav", "hayato_open_get.wav", "musashi_open_get.wav"):
        path = PROJECT / "audio/sfx/characters/shin_getter/voices" / filename
        require(path.is_file(), f"missing voice file: {filename}")
        require(path.with_name(f"{filename}.import").is_file(), f"missing Godot import sidecar: {filename}")

    for action in FUSION_ACTIONS:
        sheet = PROJECT / "images/characters/shin_getter/forms" / action / "sprite_sheet.png"
        sidecar = sheet.with_name("sprite_sheet.png.import")
        require(sidecar.is_file(), f"missing Godot import sidecar: {sidecar}")
        sidecar_text = read(sidecar)
        require('"vram_texture": false' in sidecar_text, f"{sidecar} must not use VRAM compression")
        require("compress/mode=1" in sidecar_text and "compress/lossy_quality=0.75" in sidecar_text,
                f"{sidecar} has the wrong compression policy")


def main() -> None:
    check_fusion_sheets()
    check_code_wiring()
    check_localization_and_assets()
    print("issue#10 validation passed")


if __name__ == "__main__":
    main()
