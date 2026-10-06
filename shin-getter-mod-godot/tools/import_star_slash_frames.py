#!/usr/bin/env python3
"""Byte-exact ART-003 import and explicit ART-004 runtime metadata conversion.

No video decoding, keying, RGB/alpha edits, Godot launch, PCK or deployment.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import math
from pathlib import Path
import shutil
from PIL import Image
from repair_star_slash_foreground import repair_document

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT.parent / "art_sources/characters/shin_getter/forms"
OUTPUT = ROOT / "images/characters/shin_getter/forms"
FORMS = {"一号机": ("getter_one_star_slash", 76, 133),
         "真盖塔龙": ("shin_getter_dragon_star_slash", 71, 130)}

def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")

def sidecar(path, template):
    relative = "res://" + path.relative_to(ROOT).as_posix()
    old_source = next(line for line in template.splitlines() if line.startswith("source_file="))[13:-1]
    old_name = Path(old_source).name
    new_name = path.name
    old_digest = hashlib.md5(old_source.encode()).hexdigest()
    new_digest = hashlib.md5(relative.encode()).hexdigest()
    import re
    text = template.replace(old_source, relative)
    text = re.sub(rf'{re.escape(old_name)}-[0-9a-f]{{32}}', f"{new_name}-{new_digest}", text)
    text = re.sub(r'uid="uid://[a-z0-9]+"', f'uid="uid://{hashlib.sha256(relative.encode()).hexdigest()[:12]}"', text)
    target = path.with_suffix(path.suffix + ".import")
    if target.exists() and target.read_text(encoding="utf-8") != text:
        raise ValueError(f"Refusing to overwrite a different import sidecar: {target}")
    target.write_text(text, encoding="utf-8")

def import_frames(source):
    for form, (action, count, hold_source) in FORMS.items():
        directory = source / form / "斩星斧"
        sampled = read(directory / "sampled_frame_map.json")["frames"]
        timing = read(directory / "stage_timing_map.json")
        assert len(sampled) == count and timing["candidate_hold_source_frame"] == hold_source
        assert math.isclose(sum(f["duration_seconds"] for f in sampled), 2.4)
        records = []
        destination = ART / action
        destination.mkdir(parents=True, exist_ok=True)
        for index, frame in enumerate(sampled):
            assert frame["import_index_0_based"] == index
            source_frame = directory / "sprites_import" / f"sprite_{index + 1:06d}.png"
            assert sha(source_frame) == frame["import_sha256"]
            with Image.open(source_frame) as image:
                assert image.mode == "RGBA" and image.size == (720, 720) and image.getchannel("A").getbbox()
            target = destination / source_frame.name
            if target.exists() and sha(target) != sha(source_frame):
                raise ValueError(f"Refusing changed PNG: {target}")
            shutil.copyfile(source_frame, target)
            records.append({"index": index, "source_frame": frame["source_frame_1_based"],
                "source_pts": frame["source_pts_seconds"], "duration_seconds": frame["duration_seconds"],
                "stage": frame["stage"], "sha256": sha(target)})
        write(destination / "delivery.json", {"job": "ART-003", "hold_source": hold_source,
            "impact_time": 1.4, "total_time": 2.4, "frames": records,
            "method": "byte_exact_720_RGBA_copy_no_rekey_no_resize"})
        print(f"IMPORTED {action}: {count} exact PNGs; 2.4s weighted durations")

def reviewed_metadata(annotations, form):
    # Do not silently import a WIP calibration just because runtime_metadata.json exists.
    control = annotations.parents[2] / "art-control/jobs/ART-004"
    result = read(control / "result.json")
    review = read(control / "review.json")
    assert result["job_id"] == review["job_id"] == "ART-004"
    assert review["review_status"] == "approved", "ART-004 visual review is not approved"
    annotation_path = annotations / form / "runtime_metadata.json"
    expected = [output for output in result["outputs"]
                if Path(output["path"]).resolve() == annotation_path.resolve()]
    assert len(expected) == 1 and expected[0]["sha256"].lower() == sha(annotation_path), \
        "Metadata is missing from the final result or changed after handoff"
    checked = review.get("metadata_sha256", {})
    assert checked.get(form, "").lower() == sha(annotation_path), \
        "The approved review did not check this exact metadata SHA"
    diagnostic = read(annotations / form / "overlay_diagnostic_report.json")
    assert diagnostic["metadata_sha256"].lower() == sha(annotation_path), \
        "The diagnostic images were generated from an older calibration"
    for filename in ("NShinGetterStarSlashData.cs", "NShinGetterStarSlashSequence.cs"):
        assert diagnostic["runtime_source_sha256"][filename].lower() == sha(ROOT / "src/Nodes/Combat" / filename), \
            "The diagnostic geometry predates the actual runtime implementation"
    assert sha(annotations / "reference_weapon_geometry.json") == sha(ROOT / "tools/star-slash-reference-geometry.json"), \
        "The retained reference profile is not the diagnostic's current profile"
    return annotation_path, sha(control / "review.json")

def convert_data(annotations):
    for form, (action, count, hold_source) in FORMS.items():
        # ART-004's explicit final contract: one runtime_metadata.json per named form.
        annotation_path, review_sha = reviewed_metadata(annotations, form)
        annotation = read(annotation_path)
        assert annotation["job_id"] == "ART-004" and annotation["form"] == form
        assert annotation["canvas"] == [720, 720] and annotation["coordinate_origin"] == "top-left"
        anchors = annotation["frames"]
        delivery = read(ART / action / "delivery.json")
        assert len(anchors) == count
        records = []
        for index, (anchor, original) in enumerate(zip(anchors, delivery["frames"])):
            assert anchor["import_index_0_based"] == index
            assert anchor["source_frame_1_based"] == original["source_frame"]
            assert anchor["sprite_sha256"] == original["sha256"]
            assert math.isclose(anchor["duration_seconds"], original["duration_seconds"], abs_tol=1e-9)
            assert anchor["stage"] == original["stage"]
            assert anchor["grip_720"] == anchor["grip"]
            assert anchor["shaft_axis_second_point_720"] == anchor["axis_second_point"], \
                "Runtime aliases and the diagnostic source anchors must be identical"
            handle_cover = anchor.get("handle_cover_polygons_720")
            if handle_cover is None:
                roles = anchor["original_axe_cover_roles"]
                polygons = anchor["original_axe_cover_polygons"]
                assert len(roles) == len(polygons)
                handle_cover = [polygon for role, polygon in zip(roles, polygons) if role == "handle"]
            assert handle_cover, "Every frame needs the source handle contour, not blade/trail/body vertices"
            records.append({**original, "grip": anchor["grip_720"],
                "axis": anchor["shaft_axis_second_point_720"],
                "weapon_cover": anchor["weapon_cover_polygons_720"],
                "blade_cover": anchor["blade_cover_polygons_720"],
                "handle_cover": handle_cover,
                "body_foreground": anchor.get("body_foreground_polygons_720", []),
                "hands": anchor["hand_foreground_polygons_720"]})
        document = {"source": "ART-003 + ART-004",
            "annotation_sha256": sha(annotation_path),
            "annotation_review_sha256": review_sha,
            "hold_frame": next(i for i, f in enumerate(records) if f["source_frame"] == hold_source),
            "impact_time": 1.4, "frames": records}
        # Retain approved source annotations; replay the six proven native-topology fixes.
        document, _ = repair_document(document, action)
        write(OUTPUT / action / "animation.json", document)

def imports():
    texture_template = (OUTPUT / "shin_getter_dragon_shining_spark/sprite_sheet.png.import").read_text(encoding="utf-8")
    wav_root = ROOT / "audio/sfx/characters/shin_getter/voices"
    wav_template = (wav_root / "ryoma_star_slash.wav.import").read_text(encoding="utf-8")
    for action, _, _ in FORMS.values():
        sidecar(OUTPUT / action / "sprite_sheet.png", texture_template)
    for name in ("ryoma_burn_shin_dragon.wav", "ryoma_go_shin_getter.wav"):
        sidecar(wav_root / name, wav_template)
    for name in ("NShinGetterStarSlashData", "NShinGetterStarSlashSequence"):
        path = ROOT / f"src/Nodes/Combat/{name}.cs.uid"
        if not path.exists():
            path.write_text("uid://" + hashlib.sha256(name.encode()).hexdigest()[:12] + "\n", encoding="utf-8")

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("frames", "data", "imports"))
    parser.add_argument("--source", type=Path)
    args = parser.parse_args()
    if args.mode == "frames": import_frames(args.source)
    elif args.mode == "data": convert_data(args.source)
    else: imports()

if __name__ == "__main__": main()
