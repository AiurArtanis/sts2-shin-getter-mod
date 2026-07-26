#!/usr/bin/env python3
"""Build and verify the Shin Getter character animation sprite sheets."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageChops


FRAME_SIZE = 720
MAX_TEXTURE_SIZE = 8192
SHEET_NAME = "sprite_sheet.png"
FRAME_COUNTS = {
    "getter_one_attack": 40,
    "getter_one_block": 24,
    "getter_one_cast": 32,
    "getter_one_dash": 48,
    "getter_one_death": 48,
    "getter_one_idle": 24,
    "getter_two_attack": 40,
    "getter_two_block": 24,
    "getter_two_cast": 32,
    "getter_two_dash": 48,
    "getter_two_death": 48,
    "getter_two_idle": 24,
    "getter_three_attack": 40,
    "getter_three_block": 24,
    "getter_three_cast": 32,
    "getter_three_dash": 48,
    "getter_three_death": 48,
    "getter_three_idle": 24,
    "shin_getter_dragon_attack": 60,
    "shin_getter_dragon_block": 48,
    "shin_getter_dragon_cast": 32,
    "shin_getter_dragon_dash": 48,
    "shin_getter_dragon_death": 48,
    "shin_getter_dragon_idle": 36,
}
COLUMNS_BY_FRAME_COUNT = {24: 6, 32: 8, 36: 6, 40: 8, 48: 8, 60: 10}
IDLE_RESOURCES = {
    "getter_one_idle": "shin_getter_one_idle_frames.tres",
    "getter_two_idle": "shin_getter_two_idle_frames.tres",
    "getter_three_idle": "shin_getter_three_idle_frames.tres",
    "shin_getter_dragon_idle": "shin_getter_dragon_idle_frames.tres",
}


def frame_paths(source_dir: Path, expected_count: int) -> list[Path]:
    paths = sorted(source_dir.glob("sprite_*.png"))
    if len(paths) != expected_count:
        raise ValueError(f"{source_dir}: expected {expected_count} frames, found {len(paths)}")
    return paths


def sheet_geometry(frame_count: int) -> tuple[int, int]:
    columns = COLUMNS_BY_FRAME_COUNT[frame_count]
    rows = (frame_count + columns - 1) // columns
    width, height = columns * FRAME_SIZE, rows * FRAME_SIZE
    if width > MAX_TEXTURE_SIZE or height > MAX_TEXTURE_SIZE:
        raise ValueError(f"sheet {width}x{height} exceeds {MAX_TEXTURE_SIZE}x{MAX_TEXTURE_SIZE}")
    if columns * rows != frame_count:
        raise ValueError(f"frame count {frame_count} does not exactly fill {columns}x{rows}")
    return columns, rows


def build_sheet(source_dir: Path, output_dir: Path, frame_count: int) -> None:
    paths = frame_paths(source_dir, frame_count)
    columns, rows = sheet_geometry(frame_count)
    sheet = Image.new("RGBA", (columns * FRAME_SIZE, rows * FRAME_SIZE))
    for index, path in enumerate(paths):
        with Image.open(path) as frame:
            frame = frame.convert("RGBA")
            if frame.size != (FRAME_SIZE, FRAME_SIZE):
                raise ValueError(f"{path}: expected {FRAME_SIZE}x{FRAME_SIZE}, found {frame.size}")
            sheet.paste(frame, ((index % columns) * FRAME_SIZE, (index // columns) * FRAME_SIZE))

    output_dir.mkdir(parents=True, exist_ok=True)
    output_path = output_dir / SHEET_NAME
    temporary_path = output_path.with_suffix(".tmp.png")
    sheet.save(temporary_path, format="PNG", compress_level=6)
    temporary_path.replace(output_path)
    print(f"BUILT {output_dir.name}: {frame_count} frames, {columns}x{rows}, {sheet.width}x{sheet.height}")


def verify_sheet(source_dir: Path, output_dir: Path, frame_count: int) -> None:
    paths = frame_paths(source_dir, frame_count)
    columns, rows = sheet_geometry(frame_count)
    output_path = output_dir / SHEET_NAME
    with Image.open(output_path) as sheet:
        sheet = sheet.convert("RGBA")
        expected_size = (columns * FRAME_SIZE, rows * FRAME_SIZE)
        if sheet.size != expected_size:
            raise ValueError(f"{output_path}: expected {expected_size}, found {sheet.size}")
        for index, path in enumerate(paths):
            box = (
                (index % columns) * FRAME_SIZE,
                (index // columns) * FRAME_SIZE,
                (index % columns + 1) * FRAME_SIZE,
                (index // columns + 1) * FRAME_SIZE,
            )
            with Image.open(path) as frame:
                if ImageChops.difference(sheet.crop(box), frame.convert("RGBA")).getbbox() is not None:
                    raise ValueError(f"{output_path}: frame {index + 1} differs from {path}")
    print(f"VERIFIED {output_dir.name}: {frame_count} frames")


def write_idle_resource(project_root: Path, action: str, frame_count: int) -> None:
    columns, _ = sheet_geometry(frame_count)
    sheet_path = f"res://images/characters/shin_getter/forms/{action}/{SHEET_NAME}"
    lines = [
        f'[gd_resource type="SpriteFrames" load_steps={frame_count + 2} format=3]',
        "",
        f'[ext_resource type="Texture2D" path="{sheet_path}" id="1_sheet"]',
        "",
    ]
    for index in range(frame_count):
        x = (index % columns) * FRAME_SIZE
        y = (index // columns) * FRAME_SIZE
        lines.extend(
            [
                f'[sub_resource type="AtlasTexture" id="AtlasTexture_{index + 1:03d}"]',
                'atlas = ExtResource("1_sheet")',
                f"region = Rect2({x}, {y}, {FRAME_SIZE}, {FRAME_SIZE})",
                "filter_clip = true",
                "",
            ]
        )

    ping_pong = list(range(frame_count)) + list(range(frame_count - 1, -1, -1))
    lines.extend(["[resource]", 'animations = [{', '"frames": ['])
    for position, index in enumerate(ping_pong):
        suffix = "," if position < len(ping_pong) - 1 else ""
        lines.extend(
            [
                "{",
                '"duration": 1.0,',
                f'"texture": SubResource("AtlasTexture_{index + 1:03d}")',
                f"}}{suffix}",
            ]
        )
    lines.extend(["],", '"loop": true,', '"name": &"idle",', '"speed": 24.0', "}]", ""])
    target = project_root / "scenes" / "creature_visuals" / IDLE_RESOURCES[action]
    target.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"WROTE {target.relative_to(project_root)}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--check", action="store_true", help="verify existing sheets without modifying files")
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    source_root = project_root.parent / "art_sources" / "characters" / "shin_getter" / "forms"
    output_root = project_root / "images" / "characters" / "shin_getter" / "forms"

    for action, frame_count in FRAME_COUNTS.items():
        source_dir = source_root / action
        output_dir = output_root / action
        if args.check:
            verify_sheet(source_dir, output_dir, frame_count)
        else:
            build_sheet(source_dir, output_dir, frame_count)

    if not args.check:
        for action in IDLE_RESOURCES:
            write_idle_resource(project_root, action, FRAME_COUNTS[action])


if __name__ == "__main__":
    main()
