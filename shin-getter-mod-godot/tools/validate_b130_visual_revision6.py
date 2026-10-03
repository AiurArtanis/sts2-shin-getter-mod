#!/usr/bin/env python3
"""Offline PNG/geometry/shader contracts, not a Godot rendering test."""

import math
import re
from pathlib import Path

from PIL import Image

from validate_b130_core import block, has, ordered, read, require
from validate_b130_subtitle_layout import compact, fits, place

ROOT = Path(__file__).resolve().parents[1]


def shader_contract(source: str) -> None:
    sample = block(source, "float frame_alpha(")
    ordered(sample, ["pixel.x < 0.0", "pixel.x >= frame_size.x", "return 0.0;",
                     "mix(pixel, frame_size - pixel, flipped)",
                     "clamp(local_pixel, vec2(0.5), frame_size - vec2(0.5))",
                     "(frame_region.xy + local_pixel) / atlas_size"],
            "Reject outside-cell samples before mirroring and clamping the bilinear footprint")
    fragment = block(source, "void fragment()")
    for part in ("inner - body", "1.0 - smoothstep(0.1, 0.65, body)",
                 "edge * 0.28 + glow * 0.12", "strength * outside"):
        has(fragment, part, "Only a faint expanded alpha shell, no opaque armor repaint")
    require("TIME" not in source and ".rgb" not in source and "blend_add" not in source,
            "No unsynchronized phase, RGB body duplicate or whole-body additive wash")


def main() -> None:
    clock = read(ROOT / "src/Nodes/Combat/NShinGetterShiningSparkSequence.cs")
    table = clock.split("Vector4[] FrameBounds =", 1)[1].split("};", 1)[0]
    bounds = [tuple(map(int, values)) for values in re.findall(
        r"new\((\d+), (\d+), (\d+), (\d+)\)", table)]
    pngs = sorted((ROOT.parent / "art_sources/characters/shin_getter/forms/shin_getter_dragon_shining_spark").glob("sprite_*.png"))
    require(len(bounds) == len(pngs) == 47, "Every approved frame has offline bounds")
    for expected, png in zip(bounds, pngs):
        with Image.open(png) as image:
            require(image.getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox() == expected,
                    f"High-alpha bounds match the unchanged source: {png.name}")
    geometry = block(clock, "internal static Rect2 GetFrameLocalRect(")
    for part in ("sprite.Animation == NShinGetterSpriteSequence.ShiningSparkAnimationName",
                 "sprite.Frame < FrameBounds.Length", "size.X - rect.End.X", "size.Y - rect.End.Y",
                 "sprite.Offset - (sprite.Centered ? size * 0.5f : Vector2.Zero)"):
        has(geometry, part, "Use current pose, flips, offset and center; idle does not reuse frame46")
    transform = block(clock, "internal static Rect2 TransformRect(")
    require(transform.count("Expand(") == 3, "Project all four corners, including rotation/nonuniform scale")

    # Exercise the complete production-equivalent selector after transforming both contents' AABBs.
    cases = 0
    native_cases = 0
    compact_cases = 0
    unavailable_cases = 0
    for x0, y0, x1, y1 in bounds:
        for flip_x, flip_y in ((False, False), (True, False), (False, True), (True, True)):
            for centered in (False, True):
                xs = (720 - x1, 720 - x0) if flip_x else (x0, x1)
                ys = (720 - y1, 720 - y0) if flip_y else (y0, y1)
                origin = -360 if centered else 0
                for angle in (0.0, 0.35, -0.2):
                    for canvas_x, canvas_y, width, height in ((0.75, 0.75, 960, 540),
                                                             (1, 1, 1280, 720),
                                                             (1.5, 1.5, 1920, 1080),
                                                             (2, 1.2, 2560, 864)):
                        points = [(width * 0.35 + canvas_x * ((x + origin + 17) * 0.6 * math.cos(angle)
                                   - (y + origin - 9) * 0.8 * math.sin(angle)),
                                   height * 0.48 + canvas_y * ((x + origin + 17) * 0.6 * math.sin(angle)
                                   + (y + origin - 9) * 0.8 * math.cos(angle))) for x in xs for y in ys]
                        bx, by = min(x for x, _ in points), min(y for _, y in points)
                        body = (bx, by, max(x for x, _ in points) - bx, max(y for _, y in points) - by)
                        # Rotate a measured union of bubble, text and shadow, not an arbitrary bottom edge.
                        bubble_points = [(canvas_x * (x * math.cos(0.12) - y * math.sin(0.12)),
                                          canvas_y * (x * math.sin(0.12) + y * math.cos(0.12)))
                                         for x in (-214, 426) for y in (-140, 140)]
                        size = (max(x for x, _ in bubble_points) - min(x for x, _ in bubble_points),
                                max(y for _, y in bubble_points) - min(y for _, y in bubble_points))
                        view = (16, 16, width - 32, height - 32)
                        placed = place(size, body, view)
                        if placed is None:
                            # Match runtime's basis-column magnitudes after nonuniform Canvas/root rotation.
                            font_scale = (1.95 * math.hypot(canvas_x * math.cos(0.12), canvas_y * math.sin(0.12)),
                                          1.95 * math.hypot(canvas_x * math.sin(0.12), canvas_y * math.cos(0.12)))
                            placed = compact(list("Spaaaaaark !"), body, view, font_scale)
                            compact_cases += placed is not None
                            unavailable_cases += placed is None
                        else:
                            native_cases += 1
                        if placed is not None:
                            require(fits(placed, body, view), "Full viewport containment and body separation")
                        cases += 1
    require(native_cases > 0 and compact_cases > 0, "Both actual native and fallback decisions are exercised")

    shader = read(ROOT / "shaders/shin_getter_shining_shell.gdshader")
    shader_contract(shader)
    rejected = 0
    for old, new in (("return 0.0;", "return 1.0;"),
                     ("frame_region.xy + local_pixel", "local_pixel"),
                     ("strength * outside", "strength"),
                     ("frame_size - vec2(0.5)", "frame_size")):
        require(old in shader, "Mutation targets the current shader")
        try:
            shader_contract(shader.replace(old, new, 1))
        except AssertionError:
            rejected += 1
        else:
            raise AssertionError(f"Bad shader passed: {old}")

    samples = 0
    for index in range(47):
        ox, oy = index % 8 * 720, index // 8 * 720
        for px, py in ((0, 0), (719.99, 0), (0, 719.99), (719.99, 719.99), (-24, 360), (744, 360)):
            if px < 0 or py < 0 or px >= 720 or py >= 720:
                continue  # The shader must return zero, never another cell's edge.
            for flip_x, flip_y in ((False, False), (True, False), (False, True), (True, True)):
                x = max(0.5, min(719.5, 720 - px if flip_x else px)) + ox
                y = max(0.5, min(719.5, 720 - py if flip_y else py)) + oy
                require(ox <= math.floor(x - 0.5) <= ox + 719, "Atlas X footprint remains in its cell")
                require(oy <= math.floor(y - 0.5) <= oy + 719, "Atlas Y footprint remains in its cell")
                samples += 1
    resources = read(ROOT / "tools/validate-mod-resources.gd")
    has(resources, '"res://shaders/shin_getter_shining_shell.gdshader": false', "Future PCK gate loads the new shader")
    print(f"Visual revision6 offline gate passed: 47 PNG bounds, {cases} full layout cases "
          f"({native_cases} native/{compact_cases} compact/{unavailable_cases} unavailable), "
          f"{samples} atlas-footprint cases, {rejected} bad shader sources rejected")
    print("Godot shader compilation, pixels, font layout and visual acceptance NOT executed.")


if __name__ == "__main__":
    main()
