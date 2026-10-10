#!/usr/bin/env python3
"""Mechanical repair of the six native-rejected b5 foreground contours only.

Uses the existing ART-004 diagnostic mask/run-rectangle method. A zero raster
XOR is offline evidence, not proof of native GPU pixels or animation quality.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
BASE = "b5e03af385389dadaa7630218040a088b932aafe"
TARGETS = {
    "getter_one_star_slash": {42: 2, 44: 29, 72: 5},
    "shin_getter_dragon_star_slash": {9: 119, 32: 0, 42: 42},
}
BASE_HASHES = {
    "getter_one_star_slash": "14ddd9bd127c4b76489c8bcba1cfa8f990053a29eb38f2037c4ab9c1362f2b7c",
    "shin_getter_dragon_star_slash": "829ca68fe40bbd1d4326817a6e053a73e6dd7b1f21a2533a3a08deb4a93db252",
}
REPORT = ROOT / "tools/star-slash-native-foreground-repairs.json"


def mask_of(polygons):
    mask = Image.new("L", (720, 720))
    draw = ImageDraw.Draw(mask)
    for polygon in polygons:
        draw.polygon([tuple(point) for point in polygon], fill=255)
    return mask


def partition_mask(mask):
    active, rectangles = {}, []
    pixels = mask.load()
    for y in range(721):
        runs, start = [], None
        for x in range(721):
            filled = y < 720 and x < 720 and pixels[x, y] != 0
            if filled and start is None:
                start = x
            elif not filled and start is not None:
                runs.append((start, x))
                start = None
        next_active = {run: active.pop(run, y) for run in runs}
        for (x0, x1), y0 in active.items():
            rectangles.append((x0, y0, x1, y))
        active = next_active
    # Match the retained ART-004 half-open run conversion, including its .001 inset.
    polygons = [[[x0, y0], [x1 - .001, y0], [x1 - .001, y1 - .001], [x0, y1 - .001]]
                for x0, y0, x1, y1 in rectangles]
    assert polygons and mask_of(polygons).tobytes() == mask.tobytes(), "Partition changed diagnostic coverage"
    return polygons


def baseline(action):
    relative = f"shin-getter-mod-godot/images/characters/shin_getter/forms/{action}/animation.json"
    raw = subprocess.check_output(["git", "show", f"{BASE}:{relative}"], cwd=ROOT.parent)
    assert hashlib.sha256(raw).hexdigest() == BASE_HASHES[action], "Native probe baseline changed"
    return json.loads(raw)


def repair_document(document, action):
    original = baseline(action)
    repaired = copy.deepcopy(document)
    details = []
    for frame_index, polygon_index in TARGETS[action].items():
        frame = repaired["frames"][frame_index]
        expected = original["frames"][frame_index]["body_foreground"][polygon_index]
        assert frame["body_foreground"][polygon_index] == expected, "Refusing a different foreground revision"
        replacements = partition_mask(mask_of([expected]))
        before = mask_of(frame["body_foreground"])
        frame["body_foreground"][polygon_index:polygon_index + 1] = replacements
        assert before.tobytes() == mask_of(frame["body_foreground"]).tobytes(), "Full frame matte changed"
        details.append({"frame_index": frame_index, "source_frame": frame["source_frame"],
                        "layer": "body_foreground", "polygon_index": polygon_index,
                        "original_polygon": expected, "replacement_parts": len(replacements),
                        "raster_xor_pixels": 0,
                        "diagnostic_mask_sha256": hashlib.sha256(mask_of([expected]).tobytes()).hexdigest()})
    return repaired, details


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="Generate the six scoped runtime metadata repairs")
    args = parser.parse_args()
    report = {"baseline_ref": BASE,
              "method": "ART-004 720px diagnostic mask, vertically merged pixel runs, .001 inset; no hull",
              "native_baseline": "Godot 4.5.1: 6 rejected body contours, 20/68970 failed calls across raw/centered/H/V/HV",
              "native_repaired_result": "not executed by this generator; independent result is recorded in issue124-star-slash-audit.md",
              "actions": {}}
    for action in TARGETS:
        original = baseline(action)
        expected, details = repair_document(original, action)
        path = ROOT / f"images/characters/shin_getter/forms/{action}/animation.json"
        if args.apply:
            assert json.loads(path.read_text(encoding="utf-8")) in (original, expected), "Refusing to overwrite unrelated metadata changes"
            path.write_text(json.dumps(expected, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
        assert json.loads(path.read_text(encoding="utf-8")) == expected, "Runtime JSON differs outside mechanical repair"
        report["actions"][action] = {"baseline_metadata_sha256": BASE_HASHES[action],
                                     "runtime_metadata_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
                                     "targets": details}
    if args.apply:
        REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    else:
        assert json.loads(REPORT.read_text(encoding="utf-8")) == report, "Repair evidence no longer matches runtime JSON"
    parts = sum(target["replacement_parts"] for action in report["actions"].values() for target in action["targets"])
    print(f"PASS six exact native-failure repairs: {parts} simple parts; per-polygon and full-frame diagnostic XOR=0")
    print("No mother PNG, frame mapping/weight, source annotation, weapon geometry or deployment changed.")


if __name__ == "__main__":
    main()
