#!/usr/bin/env python3
"""Import delivered PNG bytes and timing maps; never decode or re-key the video."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
from pathlib import Path

from PIL import Image, ImageSequence

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT.parent / "art_sources/characters/shin_getter/forms/shin_getter_dragon_shining_spark"
AUDIT = ROOT / "tools/b130-shining-material.json"
SOURCE = Path("D:/Library/Pictures/\u6740\u622e\u5c16\u58542-\u7d20\u6750/anim-sprite/sprites/\u771f\u76d6\u5854\u9f99/\u95ea\u5149\u7206\u88c2")
SOURCE_SHA = "d56ad4479bccb9b576d6eda73899403f36111b69a250f68a1d43041cd1851c88"
MAPS = ("stage_timing_map.json", "sampled_frame_map.json")
STAGES = (("discard", 0, 11, 0.4), ("charge", 12, 35, 1.0),
          ("rush", 36, 44, 0.28), ("impact", 45, 46, 0.08), ("recover", 46, 46, 0.35))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def json_digest(path: Path) -> str:
    canonical = json.dumps(read_json(path), sort_keys=True, ensure_ascii=True, separators=(",", ":"))
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def check(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def validate_maps(timing: dict, sampled: list) -> None:
    check(timing["source_sha256"] == SOURCE_SHA, "Unapproved source video")
    check(timing["source_clip_end_exclusive_seconds"] == 5.4, "Source clip must end at 5.4s")
    check(timing["source_mother_count"] == 130, "Expected 130 unchanged mother frames")
    check(timing["output_frame_count"] == 47 and timing["output_size"] == [720, 720], "Expected 47 720 RGBA frames")
    check(timing["frames"] == sampled and len(sampled) == 47, "Authoritative maps disagree")
    check(len(timing["stages"]) == len(STAGES), "Stage count")
    start = 0.0
    for stage, (name, first, last, budget) in zip(timing["stages"], STAGES):
        check((stage["name"], stage["first_index_0_based"], stage["last_index_0_based"],
               stage["frame_count"], stage["budget_seconds"]) == (name, first, last, last-first+1, budget),
              f"Unexpected {name} stage")
        check(math.isclose(stage["game_start_seconds"], start, abs_tol=1e-9), "Stage start timing")
        start += budget
        if name == "recover":
            continue
        check(stage["source_indices_1_based"] == [f["source_frame_1_based"] for f in sampled[first:last+1]], "Stage source indices")
        check(stage["source_first_frame_1_based"] == sampled[first]["source_frame_1_based"]
              and stage["source_last_frame_1_based"] == sampled[last]["source_frame_1_based"], "Stage source endpoints")
        for index in range(first, last+1):
            frame = sampled[index]
            check(frame["index_0_based"] == index and frame["file"] == f"sprite_{index+1:06d}.png", "Frame order/name")
            check(frame["stage"] == name, "Frame assigned to wrong stage")
            check(math.isclose(frame["duration_seconds"], budget/(last-first+1), abs_tol=1e-9), "Frame duration")
            check(math.isclose(frame["stage_start_seconds"], (index-first)*frame["duration_seconds"], abs_tol=1e-7), "Stage timestamp")
            check(math.isclose(frame["game_start_seconds"], stage["game_start_seconds"]+frame["stage_start_seconds"], abs_tol=1e-7), "Game timestamp")
    pts = [frame["source_pts_seconds"] for frame in sampled]
    source_indices = [frame["source_frame_1_based"] for frame in sampled]
    check(all(0 <= t < 5.4 for t in pts), "Frame outside source clip")
    check(all(a < b for a, b in zip(pts, pts[1:])), "PTS must increase")
    check(all(a < b for a, b in zip(source_indices, source_indices[1:])), "Source indices must increase")
    check(all(math.isclose(t, (n-1)/24, abs_tol=1e-7) for t, n in zip(pts, source_indices)), "Source frame/PTS alignment")
    check(source_indices[0] == 1 and source_indices[-1] == 130, "Source endpoints")
    check(math.isclose(sum(f["duration_seconds"] for f in sampled)+0.35, 2.11), "Minimum timing includes recovery")
    check(timing["minimum_game_seconds"] == 2.11, "Minimum game timing")


def frame_records(directory: Path, sampled: list) -> list:
    check({p.name for p in directory.glob("sprite_*.png")} == {f["file"] for f in sampled}, "PNG inventory mismatch")
    records = []
    for frame in sampled:
        path = directory / frame["file"]
        with Image.open(path) as image:
            check(image.mode == "RGBA" and image.size == (720, 720), f"Invalid PNG: {path}")
            alpha = image.getchannel("A")
            check(alpha.getbbox() is not None and alpha.getextrema() == (0, 255), f"Invalid alpha: {path}")
            records.append({**frame, "path": frame["file"], "sha256": sha256(path),
                            "empty": False, "dimensions": [720, 720], "mode": "RGBA", "alpha_extrema": [0, 255]})
    return records


def verify_import() -> None:
    timing, sampled = (read_json(OUTPUT / name) for name in MAPS)
    validate_maps(timing, sampled)
    audit = read_json(AUDIT)
    check(audit["input_video_sha256"] == SOURCE_SHA and audit["frame_count"] == 47, "Audit source/count")
    check(audit["stages"] == timing["stages"], "Audit stages")
    check(audit["frames"] == frame_records(OUTPUT, sampled), "Imported PNG hashes/metadata mismatch")
    for name in MAPS:
        check(audit["map_semantic_sha256"][name] == json_digest(OUTPUT / name), f"Map content changed: {name}")
    print("VERIFIED 47 delivered PNG hashes, alpha, source mapping and 2.11s stage timing")


def import_delivery(source: Path) -> None:
    timing, sampled = (read_json(source / name) for name in MAPS)
    validate_maps(timing, sampled)
    report = read_json(source / "sprite_pipeline_report.json")
    check(report["sha256"] == SOURCE_SHA and sha256(Path(report["input"])) == SOURCE_SHA, "Source SHA mismatch")
    records = frame_records(source / "sprites_import", sampled)
    preview = source / "\u95ea\u5149\u7206\u88c2_sampled_dark.gif"
    with Image.open(preview) as image:
        delays = [frame.info["duration"] for frame in ImageSequence.Iterator(image)]
        check(len(delays) == 47 and sum(delays) == 2110, "Preview timing/inventory")
    expected = {f["file"] for f in sampled}
    check(not OUTPUT.exists() or {p.name for p in OUTPUT.glob("sprite_*.png")} <= expected, "Refusing unexpected existing PNGs")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for frame in sampled:
        shutil.copyfile(source / "sprites_import" / frame["file"], OUTPUT / frame["file"])
    for name in MAPS:
        shutil.copyfile(source / name, OUTPUT / name)
    audit = {
        "status": "new_clip_integrated_pending_code_review_and_runtime_acceptance",
        "issue": "issue#215", "input_video": report["input"], "input_video_sha256": SOURCE_SHA,
        "source_delivery": str(source), "source_clip_end_exclusive_seconds": 5.4,
        "frame_count": 47, "minimum_game_seconds": 2.11,
        "map_delivery_sha256": {name: sha256(source / name) for name in MAPS},
        "map_semantic_sha256": {name: json_digest(OUTPUT / name) for name in MAPS},
        "stages": timing["stages"], "frames": records,
        "import_method": "byte_exact_copy_of_delivered_sprites_import_no_resize_rekey_or_interpolation",
        "preview": {"path": str(preview), "sha256": sha256(preview), "frames": 47, "duration_ms": 2110},
        "runtime_notes": timing["runtime_notes"],
        "acceptance": {"screen_facing_charge": "explicitly_accepted_by_user",
                       "runtime": "not_run", "impact_green_fade": "source_104_to_130_in_80ms_requires_gameplay_review"},
    }
    AUDIT.write_text(json.dumps(audit, indent=2, ensure_ascii=True)+"\n", encoding="utf-8")
    verify_import()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("import", "verify"))
    parser.add_argument("--source", type=Path, default=SOURCE)
    args = parser.parse_args()
    if args.mode == "import":
        import_delivery(args.source)
    else:
        verify_import()


if __name__ == "__main__":
    main()
