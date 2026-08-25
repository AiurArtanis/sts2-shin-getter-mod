#!/usr/bin/env python3
"""Rebuild selected Shin Getter RGBA frames from the authoritative captures.

The captures use a bright magenta background and, on some runs, a detached
dynamic watermark.  This tool keeps the approved frame selection, tightens
background-colored chroma pixels by changing alpha only, preserves every RGB
pixel from the approved matte, and removes only detached components fully
contained inside the known watermark corner.  It deliberately does not use
generative image editing so animation identity stays stable frame to frame.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw


PROJECT = Path(__file__).resolve().parents[1]
REPOSITORY = PROJECT.parent
SOURCE_ROOT = REPOSITORY / "art_sources/characters/shin_getter/forms"
DEFAULT_CAPTURE_ROOT = Path(r"D:\Library\Pictures\杀戮尖塔2-素材\anim-sprite\帧截取")
FRAME_SIZE = 720
BACKGROUND_DISTANCE_ZERO = 120.0
BACKGROUND_DISTANCE_SOLID = 200.0
ALPHA_FLOOR = 4


@dataclass(frozen=True)
class CaptureSource:
    form: str
    action: str
    capture_count: int
    clean_chroma: bool = True
    remove_watermark: bool = False
    tighten_magenta_edge: bool = False


# Only actions explicitly reopened by issue#159 feedback are rebuilt here.
CAPTURE_SOURCES = {
    "getter_one_idle": CaptureSource("一号机", "待机_动态水印重跑", 241, True, True, True),
    # The approved attack/dash mattes are already clean: remove only their
    # detached watermark components so their existing edge treatment stays
    # byte-for-byte unchanged elsewhere.
    "getter_one_attack": CaptureSource("一号机", "攻击", 121, False, True),
    "getter_one_block": CaptureSource("一号机", "受击防御", 121, True, True),
    "getter_one_cast": CaptureSource("一号机", "施法", 121, True, True),
    "getter_one_dash": CaptureSource("一号机", "突进", 121, False, True),
    "getter_two_idle": CaptureSource("二号机", "待机", 241, False, True),
    "getter_two_attack": CaptureSource("二号机", "攻击", 121, False, True),
    "getter_two_cast": CaptureSource("二号机", "施法", 121),
    "getter_two_dash": CaptureSource("二号机", "突进", 121),
    "getter_two_death": CaptureSource("二号机", "死亡", 121),
    "getter_three_idle": CaptureSource("三号机", "待机", 241, False, True),
    "getter_three_attack": CaptureSource("三号机", "攻击", 121),
    "getter_three_cast": CaptureSource("三号机", "施法", 121),
    "getter_three_dash": CaptureSource("三号机", "突进", 121),
    "shin_getter_dragon_idle": CaptureSource("真盖塔龙", "待机", 241, False, True),
    "shin_getter_dragon_attack": CaptureSource("真盖塔龙", "攻击", 121, True, True),
    "shin_getter_dragon_cast": CaptureSource("真盖塔龙", "施法", 121),
}


def parse_manifest(path: Path) -> dict[str, tuple[int, ...]]:
    sequences: dict[str, tuple[int, ...]] = {}
    actions: dict[str, str] = {}
    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        declaration, value = line.split("=", 1)
        kind, name = declaration.split(maxsplit=1)
        if kind == "sequence":
            sequences[name] = tuple(int(token) for token in value.split(","))
        elif kind == "action":
            actions[name] = value
    return {action: sequences[sequence] for action, sequence in actions.items()}


def estimate_background(rgb: np.ndarray) -> np.ndarray:
    border = np.concatenate(
        (
            rgb[:24].reshape(-1, 3),
            rgb[-24:].reshape(-1, 3),
            rgb[:, :24].reshape(-1, 3),
            rgb[:, -24:].reshape(-1, 3),
        ),
        axis=0,
    )
    # The median ignores isolated foreground and gray watermark pixels.
    return np.median(border, axis=0).astype(np.float32)


def smooth_alpha(distance: np.ndarray) -> np.ndarray:
    fraction = np.clip(
        (distance - BACKGROUND_DISTANCE_ZERO)
        / (BACKGROUND_DISTANCE_SOLID - BACKGROUND_DISTANCE_ZERO),
        0.0,
        1.0,
    )
    fraction = fraction * fraction * (3.0 - 2.0 * fraction)
    return np.rint(fraction * 255.0).astype(np.uint8)


def remove_corner_watermark(alpha: np.ndarray, frame_number: int, capture_count: int) -> int:
    mask = (alpha >= ALPHA_FLOOR).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, connectivity=8)
    switch_frame = int(capture_count * 0.8)
    removed = 0
    for label in range(1, count):
        x, y, width, height, area = stats[label]
        x2 = x + width
        y2 = y + height
        in_right_bottom = frame_number <= switch_frame and x >= 570 and y >= 660
        in_left_top = frame_number > switch_frame and x2 <= 160 and y2 <= 80
        if (in_right_bottom or in_left_top) and area <= 9000:
            alpha[labels == label] = 0
            removed += area
    return removed


def clean_frame(raw_path: Path, approved_path: Path, clean_chroma: bool,
                remove_watermark: bool, frame_number: int,
                capture_count: int,
                tighten_magenta_edge: bool = False) -> tuple[Image.Image, int]:
    with Image.open(raw_path) as raw_image:
        raw_rgb = np.asarray(raw_image.convert("RGB"), dtype=np.uint8)
    with Image.open(approved_path) as approved_image:
        approved_rgba = np.asarray(approved_image.convert("RGBA"), dtype=np.uint8)
    if raw_rgb.shape[:2] != (FRAME_SIZE, FRAME_SIZE):
        raise ValueError(f"{raw_path}: expected {FRAME_SIZE}x{FRAME_SIZE}")

    alpha = approved_rgba[:, :, 3].copy()
    keyed_alpha: np.ndarray | None = None
    if clean_chroma:
        background = estimate_background(raw_rgb)
        distance = np.linalg.norm(raw_rgb.astype(np.float32) - background, axis=2)
        keyed_alpha = smooth_alpha(distance)
        # Key background-colored pixels even when they are enclosed by wings,
        # shields, or other effects.  Those enclosed pockets caused several of
        # the solid magenta blocks reported on black backgrounds.
        alpha = np.minimum(alpha, keyed_alpha)
        red = raw_rgb[:, :, 0].astype(np.int16)
        green = raw_rgb[:, :, 1].astype(np.int16)
        blue = raw_rgb[:, :, 2].astype(np.int16)
        strong_magenta = (
            (red >= 160)
            & (blue >= 160)
            & (green * 2 < np.minimum(red, blue))
        )
        alpha[strong_magenta] = 0
        approved_red = approved_rgba[:, :, 0].astype(np.int16)
        approved_green = approved_rgba[:, :, 1].astype(np.int16)
        approved_blue = approved_rgba[:, :, 2].astype(np.int16)
        strong_visible_magenta = (
            (approved_red >= 160)
            & (approved_blue >= 160)
            & (approved_green * 2 < np.minimum(approved_red, approved_blue))
        )
        alpha[strong_visible_magenta] = 0
        visible_magenta = (
            (red >= 90)
            & (blue >= 100)
            & (green * 3 < np.minimum(red, blue) * 2)
        )
        edge_distance = cv2.distanceTransform(
            (keyed_alpha > 0).astype(np.uint8),
            cv2.DIST_L2,
            5,
        )
        magenta_edge = visible_magenta & (edge_distance <= 5.0)
        feathered_edge_alpha = np.clip(
            (edge_distance - 1.0) / 4.0 * 255.0,
            0.0,
            255.0,
        ).astype(np.uint8)
        alpha[magenta_edge] = np.minimum(
            alpha[magenta_edge],
            feathered_edge_alpha[magenta_edge],
        )

    if tighten_magenta_edge:
        # The reopened Getter One idle matte has a thin, fully opaque rose
        # fringe that survives the source-color key.  Restrict the stronger
        # cleanup to purple/magenta pixels at the alpha silhouette edge so
        # the intentional dark-purple wing interior remains untouched.
        approved_red = approved_rgba[:, :, 0].astype(np.int16)
        approved_green = approved_rgba[:, :, 1].astype(np.int16)
        approved_blue = approved_rgba[:, :, 2].astype(np.int16)
        purple_edge_color = (
            (approved_red >= 70)
            & (approved_blue >= 80)
            & (approved_green * 3 < np.minimum(approved_red, approved_blue) * 2)
        )
        if keyed_alpha is None:
            raise ValueError("tighten_magenta_edge requires clean_chroma")
        silhouette_distance = cv2.distanceTransform(
            (keyed_alpha >= ALPHA_FLOOR).astype(np.uint8),
            cv2.DIST_L2,
            5,
        )
        tightened_edge_alpha = np.clip(
            (silhouette_distance - 3.0) / 5.0 * 255.0,
            0.0,
            255.0,
        ).astype(np.uint8)
        purple_silhouette_edge = purple_edge_color & (silhouette_distance <= 8.0)
        alpha[purple_silhouette_edge] = np.minimum(
            alpha[purple_silhouette_edge],
            tightened_edge_alpha[purple_silhouette_edge],
        )
    alpha[alpha < ALPHA_FLOOR] = 0

    removed = 0
    if remove_watermark:
        removed = remove_corner_watermark(alpha, frame_number, capture_count)

    # The approved matte owns RGB.  Chroma cleanup and watermark removal may
    # only lower alpha; even fully transparent pixels retain the approved RGB
    # so the invariant can be verified without reconstructing a foreground.
    rgba = np.dstack((approved_rgba[:, :, :3], alpha))
    return Image.fromarray(rgba, mode="RGBA"), removed


def composite_black(image: Image.Image) -> Image.Image:
    black = Image.new("RGBA", image.size, (0, 0, 0, 255))
    black.alpha_composite(image.convert("RGBA"))
    return black.convert("RGB")


def save_qa_montages(
    qa_dir: Path,
    comparisons: dict[str, list[tuple[int, Image.Image, Image.Image]]],
) -> None:
    qa_dir.mkdir(parents=True, exist_ok=True)
    thumb_size = 360
    row_height = thumb_size + 34
    for action, frames in comparisons.items():
        canvas = Image.new("RGB", (thumb_size * 2, row_height * len(frames)), "black")
        draw = ImageDraw.Draw(canvas)
        for row, (frame_number, before, after) in enumerate(frames):
            y = row * row_height
            canvas.paste(composite_black(before).resize((thumb_size, thumb_size)), (0, y + 34))
            canvas.paste(composite_black(after).resize((thumb_size, thumb_size)), (thumb_size, y + 34))
            draw.text((8, y + 8), f"{action} frame {frame_number}: BEFORE", fill="white")
            draw.text((thumb_size + 8, y + 8), "AFTER", fill="white")
        canvas.save(qa_dir / f"{action}_black_before_after.png", compress_level=6)


def select_qa_frames(frame_numbers: tuple[int, ...]) -> set[int]:
    positions = {0, len(frame_numbers) // 2, len(frame_numbers) - 1}
    for candidate in (7, 39, 52, 53, 59, 63, 96, 103, 121):
        if candidate in frame_numbers:
            positions.add(frame_numbers.index(candidate))
    return {frame_numbers[position] for position in sorted(positions)}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--capture-root", type=Path, default=DEFAULT_CAPTURE_ROOT)
    parser.add_argument("--approved-root", type=Path, default=SOURCE_ROOT)
    parser.add_argument("--output-root", type=Path, default=SOURCE_ROOT)
    parser.add_argument("--qa-dir", type=Path)
    parser.add_argument("--actions", nargs="+", choices=sorted(CAPTURE_SOURCES))
    args = parser.parse_args()

    manifest = parse_manifest(SOURCE_ROOT / "frame_manifest.txt")
    comparisons: dict[str, list[tuple[int, Image.Image, Image.Image]]] = {}
    total_frames = 0
    total_removed_pixels = 0
    selected_actions = args.actions or list(CAPTURE_SOURCES)
    for action in selected_actions:
        capture = CAPTURE_SOURCES[action]
        output_dir = args.output_root / action
        output_dir.mkdir(parents=True, exist_ok=True)
        qa_frames = select_qa_frames(manifest[action])
        comparisons[action] = []
        for frame_number in manifest[action]:
            raw_path = (
                args.capture_root / capture.form / capture.action
                / f"frame_{frame_number:06d}.png"
            )
            approved_path = args.approved_root / action / f"sprite_{frame_number:06d}.png"
            if not raw_path.is_file() or not approved_path.is_file():
                raise FileNotFoundError(raw_path if not raw_path.is_file() else approved_path)
            with Image.open(approved_path) as approved:
                before = approved.convert("RGBA")
            after, removed = clean_frame(
                raw_path,
                approved_path,
                capture.clean_chroma,
                capture.remove_watermark,
                frame_number,
                capture.capture_count,
                capture.tighten_magenta_edge,
            )
            after.save(output_dir / approved_path.name, compress_level=6)
            if frame_number in qa_frames:
                comparisons[action].append((frame_number, before, after.copy()))
            total_frames += 1
            total_removed_pixels += removed

    if args.qa_dir:
        save_qa_montages(args.qa_dir, comparisons)
    print(
        f"CLEANED {total_frames} frames across {len(selected_actions)} actions; "
        f"removed {total_removed_pixels} watermark pixels"
    )


if __name__ == "__main__":
    main()
