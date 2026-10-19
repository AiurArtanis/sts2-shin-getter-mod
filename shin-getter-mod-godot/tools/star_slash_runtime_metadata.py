"""Lossless, reversible runtime polygon interning; ART source/provenance stays in delivery.

No coordinate rounding, contour simplification, frame removal or texture changes.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess

LAYERS = ("weapon_cover", "blade_cover", "handle_cover", "body_foreground", "hands")
BASE = "02d97982c90d9741f7a98ec5ee16fdf9bb0c2bc9"
ROOT = Path(__file__).resolve().parents[1]
BASE_HASHES = {"getter_one_star_slash": "5b1686b863fe2b866f484ad7cc1192fd327c24d056210dd604b8ef6c0d07ce20",
               "shin_getter_dragon_star_slash": "070807f44da52489991b67f8f974ff659fbd734de7f8c6871c44a495cee4b1e8"}


def encoded(document):
    return json.dumps(document, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n"


def delivery_digest(delivery):
    return hashlib.sha256(encoded(delivery).encode("utf-8")).hexdigest()


def pack_document(document, delivery):
    assert "schema_version" not in document, "Pack only the expanded editing contract"
    assert len(document["frames"]) == len(delivery["frames"])
    pool, indices, frames = [], {}, []
    for frame, original in zip(document["frames"], delivery["frames"]):
        assert all(frame[key] == value for key, value in original.items()), "Delivery provenance mismatch"
        assert set(frame) == set(original) | {"grip", "axis", *LAYERS}, "Unaccounted editing field"
        runtime = {key: frame[key] for key in ("duration_seconds", "grip", "axis")}
        for layer in LAYERS:
            references = []
            for polygon in frame[layer]:
                key = encoded(polygon)
                if key not in indices:
                    indices[key] = len(pool)
                    pool.append(polygon)
                references.append(indices[key])
            runtime[layer] = references
        frames.append(runtime)
    return {**{key: value for key, value in document.items() if key != "frames"},
            "schema_version": 2, "delivery_sha256": delivery_digest(delivery),
            "polygon_pool": pool, "frames": frames}


def expand_document(document, delivery):
    if "schema_version" not in document:
        return document
    assert document["schema_version"] == 2, "Unknown runtime schema"
    assert document["delivery_sha256"] == delivery_digest(delivery), "Delivery changed"
    assert len(document["frames"]) == len(delivery["frames"]), "Frame count changed"
    pool = document["polygon_pool"]
    frames = []
    for runtime, original in zip(document["frames"], delivery["frames"]):
        assert set(runtime) == {"duration_seconds", "grip", "axis", *LAYERS}, "Unexpected runtime fields"
        assert runtime["duration_seconds"] == original["duration_seconds"], "Frame weight changed"
        frame = {**original, "grip": runtime["grip"], "axis": runtime["axis"]}
        for layer in LAYERS:
            assert all(type(index) is int and 0 <= index < len(pool) for index in runtime[layer]), "Bad polygon index"
            frame[layer] = [pool[index] for index in runtime[layer]]
        frames.append(frame)
    return {**{key: value for key, value in document.items()
               if key not in ("frames", "schema_version", "delivery_sha256", "polygon_pool")}, "frames": frames}


def legacy_bytes(action):
    relative = f"shin-getter-mod-godot/images/characters/shin_getter/forms/{action}/animation.json"
    raw = subprocess.check_output(["git", "show", f"{BASE}:{relative}"], cwd=ROOT.parent)
    assert hashlib.sha256(raw).hexdigest() == BASE_HASHES[action], "Optimization baseline changed"
    return raw


def main():
    import argparse
    from PIL import Image, ImageDraw
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    report = {"baseline_ref": BASE, "schema_version": 2,
              "method": "exact coordinate/order interning; editing fields reversible from unchanged delivery",
              "native_gpu_acceptance": False, "actions": {}}
    for action in BASE_HASHES:
        raw = legacy_bytes(action)
        original = json.loads(raw)
        delivery = json.loads((ROOT.parent / f"art_sources/characters/shin_getter/forms/{action}/delivery.json").read_text(encoding="utf-8"))
        expected = pack_document(original, delivery)
        assert expand_document(expected, delivery) == original, "Optimization is not fully reversible"
        path = ROOT / f"images/characters/shin_getter/forms/{action}/animation.json"
        current = json.loads(path.read_text(encoding="utf-8"))
        assert expand_document(current, delivery) == original, "Refusing unrelated resource changes"
        if args.apply:
            path.write_text(encoded(expected), encoding="utf-8", newline="\n")
        assert path.read_text(encoding="utf-8") == encoded(expected), "Runtime output is not reproducible"
        decoded = expand_document(json.loads(path.read_text(encoding="utf-8")), delivery)
        masks, xor_pixels = 0, 0
        for before, after in zip(original["frames"], decoded["frames"]):
            for layer in LAYERS:
                # Independent rasterization of legacy and decoded masks, including fingers.
                images = []
                for frame in (before, after):
                    image = Image.new("1", (720, 720))
                    draw = ImageDraw.Draw(image)
                    for polygon in frame[layer]:
                        draw.polygon([tuple(point) for point in polygon], fill=1)
                    images.append(image.tobytes())
                assert images[0] == images[1], "720px diagnostic coverage changed"
                masks += 1
        references = [polygon for frame in original["frames"] for layer in LAYERS for polygon in frame[layer]]
        unique_by_role = {layer: len({encoded(polygon) for frame in original["frames"] for polygon in frame[layer]}) for layer in LAYERS}
        frames = original["frames"]
        report["actions"][action] = {
            "legacy_bytes": len(raw), "format_only_bytes": len(encoded(original).encode()),
            "runtime_bytes": path.stat().st_size, "runtime_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            "frame_count": len(frames), "polygon_references": len(references),
            "point_references": sum(map(len, references)), "unique_polygons": len(expected["polygon_pool"]),
            "stored_points": sum(map(len, expected["polygon_pool"])), "unique_by_role": unique_by_role,
            "adjacent_identical_pose_frames": sum(all(a[key] == b[key] for key in ("grip", "axis", *LAYERS)) for a, b in zip(frames, frames[1:])),
            "adjacent_identical_png_frames": sum(a["sha256"] == b["sha256"] for a, b in zip(frames, frames[1:])),
            "polygon_points_removed": 0, "frames_removed": 0, "exact_expanded_document_equal": True,
            "diagnostic_masks_checked": masks, "raster_xor_pixels": xor_pixels, "contour_error_px": 0,
            "total_time": sum(frame["duration_seconds"] for frame in frames)}
    report_path = ROOT / "tools/issue255-star-slash-metadata.json"
    if args.apply:
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    else:
        assert json.loads(report_path.read_text(encoding="utf-8")) == report, "Metadata audit changed"
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
