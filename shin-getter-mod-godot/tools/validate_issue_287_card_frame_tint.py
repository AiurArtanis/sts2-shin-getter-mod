#!/usr/bin/env python3
"""Check the frame shader's outline mask with relocated atlas sprites (CPU only)."""
import argparse
import json
import re
from pathlib import Path

SHADER = Path(__file__).resolve().parents[1] / "shaders/shin_getter_hsv.gdshader"
TYPES = ("attack", "skill", "power")
DESCRIPTION = ((.076, .68), (.18, .72), (.50, .82), (.50, .928), (.905, .78))
BORDER = ((.025, .70), (.50, .975), (.972, .70))


def normalized_regions(sheet):
    if sheet is None:
        # Draw the same-sized frames near the atlas edges and away from them.
        return dict(zip(TYPES, ((.002, .77, .29, .21), (.32, .20, .29, .21), (.67, .001, .29, .21))))

    regions = {}
    for page in json.loads(sheet.read_text(encoding="utf-8-sig"))["textures"]:
        width, height = (page["size"][k] for k in ("w", "h"))
        for sprite in page["sprites"]:
            for kind in TYPES:
                if sprite["filename"] == f"card/card_frame_{kind}_s.png":
                    r = sprite["region"]
                    regions[kind] = (r["x"] / width, r["y"] / height, r["w"] / width, r["h"] / height)
    if regions.keys() != set(TYPES):
        raise AssertionError("Expected all three native card-frame regions")
    return regions


def test_mask(shader, regions):
    code = shader.read_text(encoding="utf-8-sig")
    if "texture(TEXTURE, UV)" not in code:
        raise AssertionError("Texture sampling must still use atlas UVs")
    local_uv = "vec2 frame_uv = (UV - REGION_RECT.xy) / max(abs(REGION_RECT.zw), vec2(0.00001));"
    distance = "float edge_distance = min(min({0}.x, 1.0 - {0}.x), min({0}.y, 1.0 - {0}.y));"
    if local_uv in code and distance.format("frame_uv") in code:
        use_local_uv = True
    elif distance.format("UV") in code:
        use_local_uv = False
    else:
        raise AssertionError("Unrecognized shader coordinates; update this probe with the shader")
    falloff = re.search(r"float edge_mask = 1\.0 - smoothstep\((\d+\.\d+), (\d+\.\d+), edge_distance\);", code)
    if not falloff or "edge_mask * border_tint_strength" not in code:
        raise AssertionError("Unrecognized frame-border blend")
    start, end = map(float, falloff.groups())

    def mask(point, region):
        x, y, width, height = region
        u, v = x + point[0] * width, y + point[1] * height
        if use_local_uv:
            u, v = (u - x) / width, (v - y) / height
        d = min(u, v, 1 - u, 1 - v)
        t = min(1., max(0., (d - start) / (end - start)))
        return 1 - t * t * (3 - 2 * t)

    failed = []
    for kind, region in regions.items():
        description = max(mask(pt, region) for pt in DESCRIPTION)
        border = min(mask(pt, region) for pt in BORDER)
        print(f"{kind}: description_mask_max={description:.4f} border_mask_min={border:.4f}")
        if description != 0 or border < .95:
            failed.append(kind)
    if failed:
        raise AssertionError(f"Tint leaks into description or misses the frame: {', '.join(failed)}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--shader", type=Path, default=SHADER)
    parser.add_argument("--atlas-sheet", type=Path)
    args = parser.parse_args()
    test_mask(args.shader, normalized_regions(args.atlas_sheet))
