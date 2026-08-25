#!/usr/bin/env python3
"""Focused static validation for issue#159 Shin Getter animation-library rebuild."""

from __future__ import annotations

import hashlib
from pathlib import Path

import cv2
import numpy as np
from PIL import Image

from clean_character_animation_frames import remove_corner_watermark


PROJECT = Path(__file__).resolve().parents[1]
REPOSITORY = PROJECT.parent
SOURCE_ROOT = REPOSITORY / "art_sources/characters/shin_getter/forms"
OUTPUT_ROOT = PROJECT / "images/characters/shin_getter/forms"
FRAME_SIZE = 720

EXPECTED_SOURCE_DIGESTS = {
    "getter_one_idle": "2d7009352c4c2533f5e5d35c636ee0af80acf06df02e4fd3f93a205083baccc6",
    "getter_one_attack": "8b4cadd1cbf421d0b0cc78d6fb2da7d0857e4db57026880bfdbd0fb9734873a9",
    "getter_one_block": "52531c5cca5a4997f34148056c1b7d187d80ad67617af7a766e544297d265110",
    "getter_one_cast": "68ca596958798bcdce1d6730c8be337671bdfb55941bedd6291bbf073aab89bb",
    "getter_one_dash": "92fe6c8c4f5eb38a5f1afab808b9355c7a2af5f0b02cbfb3a89505a7811b983c",
    "getter_one_death": "a05f17270502067bb85ce953384bee1edcbe9e8c016f1e54fc77d5fcf612e22b",
    "getter_one_fusion": "2f7fe0bc985a4d209468e575c9f933a17d0919b9150cb8e13fb8c9f8ad7af138",
    "getter_one_stoner_sunshine": "78bc0b1f441ead173332a3484e5155bdb117687116dca896a9043a83e35cc7e1",
    "getter_two_idle": "6e0df21b7f0c2c80b746d837b2ffc7fbd26e986619689ac27e79d799ba3bd942",
    "getter_two_attack": "5d50176216c71789adaa4996b79fbfa26c6133efddcaa4d358f91a126b9222fe",
    "getter_two_block": "d9b576fd4e5958c15a82fac41053e2262b7f91cd7216174b38c5ffef979cb4f7",
    "getter_two_cast": "c8296f80ff69ef585c75f52cefab8bfacf0a77d0186a2eec29ac74701bcaa057",
    "getter_two_dash": "f51745748dd115bb3c9566dfbd02436b8a6414d15c3f1ed30ff68733777b61f3",
    "getter_two_death": "776d6499743c392ffd238b48dd93d0620696e9849e96b354a7a0097089456f82",
    "getter_two_fusion": "bba8a9c1008b5bd67f117579f2ff6665800d2e7aa032468e1d1c0671bdc30f44",
    "getter_three_idle": "34ec6fc021884613011365fcf3df142df62475a8bc1a0c2b2fc47376562aa1ad",
    "getter_three_attack": "edbd2a9ac5a519bfe99c1234edb3bc4af0e4555e47df6078adc7d79d2be84494",
    "getter_three_block": "e6cbc01151cf1d1b6fb9289a0360bbe9b19c92bec51ea88563503b5c29daf6a3",
    "getter_three_cast": "385cf66d10affa6658a13d842e9e68180d331f9063fb409e9ca3aabb9e4a723d",
    "getter_three_dash": "6755c5c0032f390cc70f0020809eb1a7e96e9a26d9a63670d4370e0ee434e6ae",
    "getter_three_death": "605676f27ce83226f3e9e9506887b3b70505685b190d913d41d78e2d6b918f0f",
    "getter_three_fusion": "cbd9d0e8f6fd819e23fb1a554805cda2129e7ab46cff53da4290ecd0057b295b",
    "shin_getter_dragon_idle": "f15b59c00aee5953202bfe513eb10f3de05d4aeeacd305299374779320cd3260",
    "shin_getter_dragon_attack": "92971e2f5d7062a7e169e572fad3a87775a8ee1abfe7270761cd7c35e6d9f27e",
    "shin_getter_dragon_block": "afcb546cf449d79638697728fbae095b9c07b30182b1948c79cb95c027705002",
    "shin_getter_dragon_cast": "8d1bf7518cb48a283e8090a1aa167aaad54b7e5e26627cda6f9c22d994d1ad14",
    "shin_getter_dragon_dash": "27f81876b12d4d2e879d55d992f317d79daba5600503ec4efbe41cdb9bf2fab3",
    "shin_getter_dragon_death": "686a50ba8e08b27e51ad9503a181674392bfdf437d327d4a3915512127af3084",
    "shin_getter_dragon_cyclone": "b98b6603cabc197962228ace4c1999aca6e436f509cda4f4bf5cde8e6d88ba33",
    "shin_getter_dragon_dash_v2": "1439ad27e079681fdd3636423cee7587e312ad618c3bf5297d9a2a099bbff443",
    "shin_getter_dragon_drill_attack": "be97edf3f43d5e197173b72f7ae6e70fe014149d47c5dd6102eef64140e1d46a",
    "shin_getter_dragon_stoner_sunshine": "d878cab0b7cff8a91537ef5b9704ce360962f7fa237d9bbff404f7ab924d6842",
}

EXPECTED_CLEAN_RGB_DIGESTS = {
    "getter_one_attack": "523980b3d107ca36182d26415d67281fa74bd934520ecff06d0922f7571969bd",
    "getter_one_block": "209d8299200d46c1571ae5d205eda5aeccd8d3711f448fa8325c84dc32ea5c15",
    "getter_one_cast": "174fc4314f1b77abdce0b298c1beff3ca5cfebe349222ac1e623b7803dc46aaa",
    "getter_one_dash": "e026b19e0cc3b7748783889a00152cc598defd224369342075f98144fc54ff88",
    "getter_one_idle": "69b2bc5bf35d7a975ca427dd15f7b4e38570382e07af77dcc18adb81d7f6886b",
    "getter_three_attack": "a67a200a78bc7cd41cd821e00baeb160e51c5ab179cdee33a54e757cd8f3728f",
    "getter_three_cast": "469d1737c4c3402df2667cbfc58512aa8f9023b4ac922aef86325a9a22e157f8",
    "getter_three_dash": "c1caf734bddc229cdca60dee5223cbc4545652dc5892b607a729774621fff71c",
    "getter_three_idle": "35a533f7b1912b2bd95b17aee80db22aeb122eb8824580cced517903b96fd5fe",
    "getter_two_cast": "c5f44918e3e91da7638f020f9d21600e32d13272af56abe554cec4d90db4a01a",
    "getter_two_attack": "edb169b118bd52c638eef023be4023f7673060bbee2ebcd20afac6ab31c7c510",
    "getter_two_dash": "f266cfbe053c002916d74ce09921cdf2f5ea6d867b1baf27f466f55b1cb5109a",
    "getter_two_death": "7e0e0c95d78286ec368868811f6b6bdb080955f610455b32151761476e94ab12",
    "getter_two_idle": "06088b6896dcef125546631431f0c2873cc6a5237aed7302964cbeaea9da547b",
    "shin_getter_dragon_attack": "b183889f175a29ea9cfc96f859d39e919ec28220021a6c8201abbeeb022fb080",
    "shin_getter_dragon_cast": "c5ef0f7da34483c69e15be09c28a41cc772fd31aa170ec5e961da495d9a1f90e",
    "shin_getter_dragon_idle": "3eaeb2b30c5762ed274786892ed855ebd029aa31d140d83ed6e04ef476524b85",
}

EXPECTED_WATERMARK_ALPHA_DIGESTS = {
    "getter_one_attack": "5455a4048326d8064f120d3aa4ea55e083699dee00542c3d95cbb6ca565d2816",
    "getter_one_block": "4806b4d0f4f58a143a3d9f247fa2d61cf890e5c3165b553e3aa508ae1959fe7f",
    "getter_one_cast": "ed997ed5ecb11ca3312055cd5c6dc0e48b5998087e012812b82adde151f23e22",
    "getter_one_dash": "7b2c8b0d312277a868b9e90194e0f0c125c2041be0f58f6d47e3c3a2896db9ab",
    "getter_two_idle": "a1b2852beec0e0254cfe1c9d5df5f5dea7661e4d753a7871a566b1083108e9b5",
    "getter_two_attack": "59c87c282f4f84b17162dd1773a65685e11033bf00a8b628fea3cc9d176035bc",
    "getter_three_idle": "b0adf68507686ad6f514b37bf798d9e5f86bbb9b8b10a3943bd85bc183a63a14",
    "shin_getter_dragon_attack": "760110700863627117e741cb5199cc38f38813871fc8802187b5ddc3f57b840a",
    "shin_getter_dragon_idle": "53988a193d71e56999999d2a843c0dc97a367ad6e7fb7630c11d8466cb001e8d",
}

EXPECTED_FRAME_COUNTS = {
    "getter_one_idle": 24,
    "getter_one_attack": 40,
    "getter_one_block": 24,
    "getter_one_cast": 32,
    "getter_one_dash": 48,
    "getter_one_death": 48,
    "getter_one_fusion": 30,
    "getter_one_stoner_sunshine": 90,
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
    "shin_getter_dragon_stoner_sunshine": 90,
}

SPECIAL_CARD_GROUPS = {
    "Cyclone": (
        "SGC_Avalanche", "SGC_PoseidonThunder", "SGC_GetterMissile",
        "SGC_FocusFire", "SGC_Annihilation",
    ),
    "DashV2": (
        "SGC_ShiningSpark", "SGC_GetterRush", "SGC_Acceleration",
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
    "SGC_ShiningSpark",
}

WATERMARK_ACTIONS = {
    "getter_one_attack": 121,
    "getter_one_block": 121,
    "getter_one_cast": 121,
    "getter_one_dash": 121,
    "getter_two_idle": 241,
    "getter_two_attack": 121,
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


def aggregate_rgb_digest(frames: list[Path]) -> str:
    digest = hashlib.sha256()
    for frame in frames:
        with Image.open(frame) as image:
            digest.update(np.asarray(image.convert("RGB"), dtype=np.uint8).tobytes())
    return digest.hexdigest()


def aggregate_alpha_digest(frames: list[Path]) -> str:
    digest = hashlib.sha256()
    for frame in frames:
        with Image.open(frame) as image:
            digest.update(np.asarray(image.convert("RGBA"), dtype=np.uint8)[:, :, 3].tobytes())
    return digest.hexdigest()


def visible_rgb_is_unchanged(approved: np.ndarray, cleaned: np.ndarray) -> bool:
    visible = cleaned[:, :, 3] > 0
    return np.array_equal(approved[:, :, :3][visible], cleaned[:, :, :3][visible])


def frame_number(path: Path) -> int:
    return int(path.stem.rsplit("_", 1)[1])


def has_fully_contained_corner_component(
    mask: np.ndarray,
    number: int,
    capture_count: int,
) -> bool:
    height, width = mask.shape
    switch_frame = int(capture_count * 0.8)
    if number <= switch_frame:
        corner = (570, 660, width, height)
    else:
        corner = (0, 0, 160, 80)
    count, _, stats, _ = cv2.connectedComponentsWithStats(
        mask.astype(np.uint8),
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


def has_detached_corner_component(image: Image.Image, number: int, capture_count: int) -> bool:
    alpha = np.asarray(image.getchannel("A"), dtype=np.uint8)
    return has_fully_contained_corner_component(
        alpha >= 4,
        number,
        capture_count,
    )


def has_detached_corner_rgb_component(
    image: Image.Image,
    number: int,
    capture_count: int,
) -> bool:
    rgb = np.asarray(image.convert("RGB"), dtype=np.uint8)
    return has_fully_contained_corner_component(
        np.any(rgb != 0, axis=2),
        number,
        capture_count,
    )


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


def count_visible_magenta_silhouette_edge(image: Image.Image) -> int:
    rgba = np.asarray(image, dtype=np.int16)
    red, green, blue, alpha = (rgba[:, :, index] for index in range(4))
    silhouette_distance = cv2.distanceTransform(
        (alpha >= 4).astype(np.uint8),
        cv2.DIST_L2,
        5,
    )
    visible_magenta_edge = (
        (alpha >= 64)
        & (red >= 70)
        & (blue >= 80)
        & (green * 3 < np.minimum(red, blue) * 2)
        & (silhouette_distance <= 3.0)
    )
    return int(visible_magenta_edge.sum())


def check_authoritative_sources() -> None:
    actual_actions = {path.name for path in SOURCE_ROOT.iterdir() if path.is_dir()}
    require(
        actual_actions == set(EXPECTED_SOURCE_DIGESTS),
        "all 32 character actions must be covered by approved source digests",
    )
    for action, expected_hash in EXPECTED_SOURCE_DIGESTS.items():
        frames = sorted((SOURCE_ROOT / action).glob("sprite_*.png"))
        expected_count = EXPECTED_FRAME_COUNTS[action]
        require(len(frames) == expected_count, f"{action}: expected {expected_count} source frames")
        require(aggregate_digest(frames) == expected_hash, f"{action}: source frames differ from the approved cleanup")
        if action in EXPECTED_CLEAN_RGB_DIGESTS:
            require(
                aggregate_rgb_digest(frames) == EXPECTED_CLEAN_RGB_DIGESTS[action],
                f"{action}: cleaned RGB differs from the approved result",
            )
        if action in EXPECTED_WATERMARK_ALPHA_DIGESTS:
            require(
                aggregate_alpha_digest(frames) == EXPECTED_WATERMARK_ALPHA_DIGESTS[action],
                f"{action}: watermark-only cleanup must not change approved alpha",
            )
        for frame in frames:
            with Image.open(frame) as image:
                require(image.size == (FRAME_SIZE, FRAME_SIZE), f"{frame}: expected 720x720")
                require(image.mode == "RGBA", f"{frame}: background-cleaned source must retain RGBA")


def check_rgb_invariant_guard() -> None:
    approved = np.array(
        [[[20, 40, 60, 255], [80, 100, 120, 128]]],
        dtype=np.uint8,
    )
    alpha_only = approved.copy()
    alpha_only[0, 1, 3] = 64
    require(
        visible_rgb_is_unchanged(approved, alpha_only),
        "alpha-only cleanup must satisfy the visible RGB invariant",
    )
    rgb_changed = alpha_only.copy()
    rgb_changed[0, 1, 0] = 81
    require(
        not visible_rgb_is_unchanged(approved, rgb_changed),
        "visible RGB invariant guard must reject foreground color propagation",
    )


def check_hidden_watermark_guard() -> None:
    fixture = np.zeros((FRAME_SIZE, FRAME_SIZE, 4), dtype=np.uint8)
    fixture[670:680, 580:600, :3] = 220
    image = Image.fromarray(fixture, mode="RGBA")
    require(
        not has_detached_corner_component(image, 1, 121),
        "alpha-only watermark guard fixture must remain invisible",
    )
    require(
        has_detached_corner_rgb_component(image, 1, 121),
        "hidden watermark guard must reject transparent nonzero RGB text",
    )

    cleaned = fixture.copy()
    removed = remove_corner_watermark(cleaned, 1, 121)
    require(
        removed == 200 and not np.any(cleaned),
        "cleaner must clear the full RGBA value of a hidden watermark component",
    )

    fixture[670:680, 580:600] = 0
    require(
        not has_detached_corner_rgb_component(
            Image.fromarray(fixture, mode="RGBA"),
            1,
            121,
        ),
        "fully cleared watermark RGBA must pass the hidden watermark guard",
    )

    fixture[670:680, 560:600, :3] = 220
    untouched = fixture.copy()
    require(
        remove_corner_watermark(untouched, 1, 121) == 0
        and np.array_equal(untouched, fixture),
        "cleaner must leave components extending outside the active corner unchanged",
    )
    require(
        not has_detached_corner_rgb_component(
            Image.fromarray(fixture, mode="RGBA"),
            1,
            121,
        ),
        "components extending outside the active watermark corner must not be auto-classified",
    )

    overlap = np.zeros((FRAME_SIZE, FRAME_SIZE, 4), dtype=np.uint8)
    overlap[670:680, 560:600, :3] = 220
    overlap[670:680, 560:570, 3] = 255
    overlap_before = overlap.copy()
    removed = remove_corner_watermark(
        overlap,
        66,
        121,
        scrub_hidden_overlap=True,
    )
    require(
        removed == 300,
        "overlap cleanup must count only transparent RGB inside the active corner",
    )
    require(
        not np.any(overlap[670:680, 570:600, :3]),
        "overlap cleanup must erase hidden RGB joined to a visible effect",
    )
    require(
        np.array_equal(overlap[:, :, 3], overlap_before[:, :, 3])
        and np.array_equal(
            overlap[670:680, 560:570, :3],
            overlap_before[670:680, 560:570, :3],
        ),
        "overlap cleanup must preserve visible effect alpha and RGB",
    )


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
                require(
                    not has_detached_corner_rgb_component(
                        image.convert("RGBA"),
                        frame_number(frame),
                        capture_count,
                    ),
                    f"{frame}: transparent RGB watermark remains in the known corner",
                )

    for action in CHROMA_CLEAN_ACTIONS:
        for frame in sorted((SOURCE_ROOT / action).glob("sprite_*.png")):
            with Image.open(frame) as image:
                require(
                    count_visible_strong_magenta(image.convert("RGBA")) == 0,
                    f"{frame}: strong magenta pixels remain visible on black",
                )

    idle_magenta_edge_counts = []
    for frame in sorted((SOURCE_ROOT / "getter_one_idle").glob("sprite_*.png")):
        with Image.open(frame) as image:
            edge_count = count_visible_magenta_silhouette_edge(image.convert("RGBA"))
            idle_magenta_edge_counts.append(edge_count)
            require(
                edge_count <= 10,
                f"{frame}: reopened idle rose/magenta silhouette fringe remains visible on black",
            )
    require(
        sum(idle_magenta_edge_counts) <= 100,
        "Getter One idle rose/magenta silhouette fringe regressed across the animation",
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
    require(
        '"getter_one_idle": CaptureSource("一号机", "待机_动态水印重跑", 241, True, True, True)'
        in cleaner,
        "Getter One idle must combine watermark removal with its explicitly reopened edge cleanup",
    )
    for fragment in (
        '"getter_one_attack": CaptureSource("一号机", "攻击", 121, False, True)',
        '"getter_one_dash": CaptureSource("一号机", "突进", 121, False, True)',
        '"getter_two_idle": CaptureSource("二号机", "待机", 241, False, True)',
        '"getter_two_attack": CaptureSource("二号机", "攻击", 121, False, True)',
        '"getter_three_idle": CaptureSource("三号机", "待机", 241, False, True)',
        '"shin_getter_dragon_idle": CaptureSource("真盖塔龙", "待机", 241, False, True)',
    ):
        require(fragment in cleaner,
                f"watermark-only cleanup must not re-key an already-approved matte: {fragment}")
    require("propagate_solid_edge_rgb" not in cleaner,
            "cleanup must not propagate neighboring foreground RGB")
    require("np.dstack((approved_rgba[:, :, :3], alpha))" in cleaner,
            "cleanup must begin with approved RGB and adjusted alpha")
    require("alpha_present | rgb_present" in cleaner,
            "watermark detection must include transparent nonzero RGB components")
    require("rgba[labels == label] = 0" in cleaner,
            "watermark cleanup must clear full RGBA instead of hiding text with alpha")
    require("active_corner[hidden_rgb] = 0" in cleaner,
            "overlapped hidden watermark cleanup must clear transparent RGB")
    require("(keyed_alpha >= ALPHA_FLOOR).astype(np.uint8)" in cleaner,
            "reopened silhouette cleanup must derive its edge from the stable raw-source key")
    require("(silhouette_distance - 8.0) / 4.0" in cleaner
            and "silhouette_distance <= 12.0" in cleaner,
            "Getter One idle must retain the approved stronger rose-edge cleanup")


def check_runtime_wiring() -> None:
    sequence = read(PROJECT / "src/Nodes/Combat/NShinGetterSpriteSequence.cs")
    state_machine = read(PROJECT / "src/Nodes/Combat/NShinGetterSpriteAnimationStateMachine.cs")
    card_base = read(PROJECT / "src/Models/Cards/ShinGetterCardBase.cs")
    static_visuals = read(PROJECT / "src/Nodes/Combat/NShinGetterStaticVisuals.cs")
    annihilation = read(PROJECT / "src/Models/Cards/SGC_Annihilation.cs")
    avalanche = read(PROJECT / "src/Models/Cards/SGC_Avalanche.cs")
    hurricane_strike = read(PROJECT / "src/Models/Cards/SGC_HurricaneStrike.cs")
    shining_spark = read(PROJECT / "src/Models/Cards/SGC_ShiningSpark.cs")
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
    require('["SGC_StarSlash"] = "DashV2"' not in card_base,
            "Star Slash is not one of the authoritative 15 special-animation cards")

    require("ShinDragonSpecialEffectDelaySeconds = 1.4f" in card_base,
            "unmodified Shin Getter Dragon special effects must retain the approved 1.4-second timing")
    for card, delay in (
        ("SGC_FocusFire", "1.2f"),
        ("SGC_TornadoDrill", "1.0f"),
        ("SGC_SpiralDrill", "1.0f"),
        ("SGC_LigerAssault", "1.0f"),
        ("SGC_GetterClaw", "1.0f"),
    ):
        require(f'["{card}"] = {delay}' in card_base,
                f"{card}: shared special-animation effect timing must be {delay}")
    delay_overrides = card_base.split(
        "private static readonly IReadOnlyDictionary<string, float> ShinDragonSpecialEffectDelayOverrides", 1
    )[1].split("private static readonly IReadOnlySet<string> DashAnimationCards", 1)[0]
    require('"SGC_Acceleration"' not in delay_overrides
            and '"SGC_PetalBreakthrough"' not in delay_overrides,
            "already-approved Acceleration and Petal Breakthrough timing must remain at the 1.4-second default")
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
    independent_wait = card_base.split(
        "protected Task WaitForShinDragonSpecialEffect", 1
    )[1].split("protected Func<Task> AccelerateFollowupAnimations", 1)[0]
    require("IsShinDragonSpecialAnimationTrigger(animationTrigger)" in independent_wait
            and ": Task.CompletedTask;" in independent_wait,
            "card-owned timing waits must not delay the same cards outside the current Shin Dragon form")

    for source, delay, card in (
        (avalanche, "1.2f", "Avalanche"),
        (annihilation, "1.2f", "Annihilation"),
        (hurricane_strike, "1.0f", "Hurricane Strike"),
    ):
        require(f"await WaitForShinDragonSpecialEffect({delay});" in source,
                f"{card}: card-owned hit/VFX timing must wait {delay} only for a Shin Dragon special")
    require('bool isShinDragonCyclone = GetActionAnimationTrigger() == "Cyclone";' in annihilation
            and "if (isShinDragonCyclone)" in annihilation
            and "attack.WithNoAttackerAnim();" in annihilation
            and 'attack.WithAttackerAnim("Cast", 0.35f);' in annihilation,
            "Annihilation must not add its ordinary Cast delay after the Shin Dragon 1.2-second gate")

    pause_contract = static_visuals.split(
        "if (waitBeforeSecondHalf != null)", 3
    )[-1].split("await onSecondHalf();", 1)[0]
    require("sprite.SpeedScale = 0f;" in pause_contract
            and "await waitBeforeSecondHalf();" in pause_contract
            and pause_contract.index("sprite.SpeedScale = 0f;")
            < pause_contract.index("await waitBeforeSecondHalf();")
            < pause_contract.rindex("sprite.SpeedScale = Math.Max(0.05f, secondHalfSpeedScale);"),
            "phased animation must pause at its midpoint, await the gate, then resume its second half")
    shining_sequence = shining_spark.split("private async Task PlayShiningSparkSequence", 1)[1]
    require('GetActionAnimationTrigger() != "DashV2"' in shining_sequence,
            "Shining Spark split timing must remain gated to the current Shin Dragon form")
    require("Task intro = ShinGetterVoiceService.PlayShiningSparkIntro(Owner);" in shining_sequence
            and '"DashV2"' in shining_sequence
            and "waitBeforeSecondHalf: () => intro" in shining_sequence,
            "Shining must start with DashV2 first-half playback and gate its midpoint on the intro voice")
    second_half = shining_sequence.split("() => Task.WhenAll(", 1)[1].split(
        "fallbackFirstHalfDuration", 1
    )[0]
    require("PlayRush(Owner.Creature, target, whiteFlash: true)" in second_half
            and "PlayShiningSparkFollowUp(Owner)" in second_half,
            "Spark, the DashV2 second half, and the forward rush must begin together")

    require('GetActionAnimationTrigger() ?? "Attack"' in star_slash,
            "Star Slash manual timing must remain intact after removal from the special-card mapping")
    require('GetActionAnimationTrigger() ?? "Attack"' in getter_flash,
            "Getter Flash manual timing must request DashV2 in Shin Getter Dragon")


def main() -> None:
    check_authoritative_sources()
    check_rgb_invariant_guard()
    check_hidden_watermark_guard()
    check_black_background_cleanup()
    check_builder_and_sheets()
    check_runtime_wiring()
    print("issue#159 validation passed")


if __name__ == "__main__":
    main()
