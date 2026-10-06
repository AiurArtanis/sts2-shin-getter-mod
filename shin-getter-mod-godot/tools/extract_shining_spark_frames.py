#!/usr/bin/env python3
"""Deterministic source-video extraction; no generation or runtime integration."""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
import cv2
import numpy as np
from PIL import Image, ImageDraw

REPO = Path(__file__).resolve().parents[2]
SHARED = Path("D:/Library/Pictures/\u6740\u622e\u5c16\u58542-\u7d20\u6750/anim-sprite")
VIDEO = SHARED / "\u89c6\u9891\u52a8\u753b/\u771f\u76d6\u5854\u9f99/\u771f\u76d6\u5854\u9f99-\u95ea\u5149\u7206\u88c2.mp4"
PIPELINE = SHARED / "tools/video_to_adaptive_sprite_frames.py"
OUTPUT = REPO / "art_sources/characters/shin_getter/forms/shin_getter_dragon_shining_spark"
QA = Path("E:/Work/StS2 Mods/_validation/b130-shining-20261001")
AUDIT = Path(__file__).with_name("b130-shining-material.json")
# Preserve action landmarks, not uniformly spaced duplicates from static holds.
SELECTED = [0, 9, 14, 18, 22, 26, 30, 34, 40, 44, 47, 50, 53, 54, 56,
            58, 59, 60, 61, 64, 70, 80, 88, 94, 100, 106, 112, 120,
            124, 128, 132, 136, 140, 144]
PHASES = {
    "release_visible": {"sequence_index": 12, "source_index": 53,
                        "basis": "Both palms open; baked axe visible behind left silhouette, no added prop"},
    "discard_complete": {"sequence_index": 18, "source_index": 61,
                         "basis": "Source 60 still has left-edge shaft; 61 is first persistently weapon-free frame"},
    "charge_hold": {"sequence_index": 26, "source_index": 112,
                    "basis": "Both fists drawn back, lowered stance; stable crouched hold around 104-119"},
    "dash_peak": {"sequence_index": 31, "source_index": 136,
                  "basis": "Deep forward lean reached; source has no translational dash or impact"},
    "end_hold": {"sequence_index": 33, "source_index": 144,
                 "basis": "Final weapon-free forward-lean pose; does not recover to idle"},
}
HARD_DISTANCE = 60.0
FEATHER_RADIUS = 10


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_pipeline():
    spec = importlib.util.spec_from_file_location("shining_readonly_pipeline", PIPELINE)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def decode():
    capture = cv2.VideoCapture(str(VIDEO))
    if not capture.isOpened():
        raise RuntimeError(f"Cannot open {VIDEO}")
    metadata = {
        "width": int(capture.get(cv2.CAP_PROP_FRAME_WIDTH)),
        "height": int(capture.get(cv2.CAP_PROP_FRAME_HEIGHT)),
        "fps": float(capture.get(cv2.CAP_PROP_FPS)),
        "reported_frame_count": int(capture.get(cv2.CAP_PROP_FRAME_COUNT)),
    }
    frames = []
    while True:
        ok, bgr = capture.read()
        if not ok:
            break
        frames.append(cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB))
    capture.release()
    if len(frames) != metadata["reported_frame_count"]:
        raise RuntimeError("Incomplete sequential decode")
    metadata["decoded_frame_count"] = len(frames)
    metadata["duration_seconds"] = len(frames) / metadata["fps"]
    if metadata["width"] != 960 or metadata["height"] != 960 or metadata["fps"] != 24:
        raise RuntimeError(f"Unexpected source metadata: {metadata}")
    return frames, metadata


def sheet(images, labels, destination, background="checker", tile=240, columns=6):
    rows = math.ceil(len(images) / columns)
    canvas = Image.new("RGB", (columns * tile, rows * (tile + 24)), "white")
    draw = ImageDraw.Draw(canvas)
    for i, (image, label) in enumerate(zip(images, labels)):
        x, y = i % columns * tile, i // columns * (tile + 24)
        if background == "checker":
            base = load_pipeline().checkerboard((tile, tile), 16)
        else:
            base = Image.new("RGB", (tile, tile), background)
        thumb = image.resize((tile, tile), Image.Resampling.LANCZOS)
        if thumb.mode == "RGBA":
            base.paste(thumb, (0, 0), thumb)
        else:
            base.paste(thumb, (0, 0))
        canvas.paste(base, (x, y + 24))
        draw.text((x + 4, y + 5), label, fill="black")
    canvas.save(destination)


def metrics(rgb, sprite, native):
    array = np.asarray(sprite)
    alpha = array[:, :, 3]
    color = array[:, :, :3].astype(np.int32)
    a960 = np.asarray(native)[:, :, 3]
    src = rgb.astype(np.int32)
    protected = ((src[:, :, 0] < 125) & (src[:, :, 1] < 125) & (src[:, :, 2] < 125)) | (
        (src[:, :, 0] > 90) & (src[:, :, 0] > src[:, :, 1] * 1.5)
        & (src[:, :, 0] > src[:, :, 2] * 1.5))
    residual = (alpha > 16) & (color[:, :, 0] > 150) & (color[:, :, 2] > 145) & (
        color[:, :, 1] < 115) & (color[:, :, 0] + color[:, :, 2] > 2 * color[:, :, 1] + 130)
    ys, xs = np.nonzero(alpha > 16)
    return {
        "dimensions": list(sprite.size), "mode": sprite.mode,
        "alpha_extrema": [int(alpha.min()), int(alpha.max())],
        "transparent_pixels": int((alpha == 0).sum()),
        "opaque_pixels": int((alpha == 255).sum()),
        "soft_alpha_pixels": int(((alpha > 0) & (alpha < 255)).sum()),
        "visible_bbox_xyxy_exclusive": [int(xs.min()), int(ys.min()), int(xs.max()+1), int(ys.max()+1)] if len(xs) else None,
        "empty": not bool(len(xs)),
        "strong_magenta_residual_pixels": int(residual.sum()),
        "protected_dark_or_red_pixels": int(protected.sum()),
        "protected_dark_or_red_alpha_zero_pixels": int((protected & (a960 == 0)).sum()),
        "protected_dark_or_red_alpha_below_240_pixels": int((protected & (a960 < 240)).sum()),
        "transparent_nonzero_rgb_pixels": int(((alpha == 0) & np.any(array[:, :, :3] != 0, axis=2)).sum()),
        "canvas_edge_visible_pixels": int((alpha[0] > 16).sum()+(alpha[-1] > 16).sum()+(alpha[:, 0] > 16).sum()+(alpha[:, -1] > 16).sum()),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("inspect", "extract", "verify"))
    args = parser.parse_args()
    pipeline = load_pipeline()
    frames, metadata = decode()
    QA.mkdir(parents=True, exist_ok=True)
    if args.mode == "inspect":
        raw_dir = QA / "raw_source_960"
        matte_dir = QA / "all_candidate_720"
        raw_dir.mkdir(exist_ok=True)
        matte_dir.mkdir(exist_ok=True)
        stats = []
        for n, rgb in enumerate(frames):
            Image.fromarray(rgb).save(raw_dir / f"source_{n:03d}.png")
            native, bg = pipeline.matte_frame(rgb, HARD_DISTANCE, FEATHER_RADIUS)
            sprite = pipeline.resize_rgba_premultiplied(native, 720)
            sprite.save(matte_dir / f"source_{n:03d}.png")
            stats.append({"source_index_0_based": n, "background_rgb": bg, **metrics(rgb, sprite, native)})
        for start in range(0, len(frames), 36):
            indices = list(range(start, min(start+36, len(frames))))
            labels = [f"src {n:03d} | {n/24:.3f}s" for n in indices]
            sheet([Image.fromarray(frames[n]) for n in indices], labels, QA / f"raw_{start:03d}_{indices[-1]:03d}.jpg")
            sheet([Image.open(matte_dir / f"source_{n:03d}.png") for n in indices], labels, QA / f"matte_{start:03d}_{indices[-1]:03d}.jpg")
        (QA / "all-frame-metrics.json").write_text(json.dumps({"video": metadata, "frames": stats}, indent=2), encoding="utf-8")
        print(json.dumps({"mode": args.mode, "video": metadata, "qa": str(QA), "empty_frames": sum(s["empty"] for s in stats), "worst_magenta_pixels": max(s["strong_magenta_residual_pixels"] for s in stats)}))
        return
    if not SELECTED or SELECTED != sorted(set(SELECTED)):
        raise RuntimeError("Source-reviewed selection is required")
    expected_names = {f"sprite_{n+1:06d}.png" for n in range(len(SELECTED))}
    if OUTPUT.exists() and {p.name for p in OUTPUT.iterdir()} - expected_names:
        raise RuntimeError("Refusing to touch unexpected existing output files")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    records, images = [], []
    for index, source_index in enumerate(SELECTED):
        native, bg = pipeline.matte_frame(frames[source_index], HARD_DISTANCE, FEATHER_RADIUS)
        sprite = pipeline.resize_rgba_premultiplied(native, 720)
        path = OUTPUT / f"sprite_{index+1:06d}.png"
        if args.mode == "verify":
            with Image.open(path) as actual:
                if actual.mode != "RGBA" or not np.array_equal(np.asarray(actual), np.asarray(sprite)):
                    raise RuntimeError(f"Deterministic pixel mismatch: {path}")
        else:
            sprite.save(path)
        record = {"sequence_index_0_based": index, "source_index_0_based": source_index,
                  "source_frame_1_based": source_index+1, "source_time_seconds": source_index/24,
                  "path": str(path), "sha256": sha256(path),
                  "raw_qa_path": str(QA / f"raw_source_960/source_{source_index:03d}.png"),
                  "suggested_source_review_duration_ms": round(((SELECTED[index+1] if index+1 < len(SELECTED) else len(frames))-source_index)/24*1000),
                  "decoded_source_rgb_sha256": hashlib.sha256(frames[source_index].tobytes()).hexdigest(),
                  "background_rgb": bg, **metrics(frames[source_index], sprite, native)}
        if record["empty"] or record["protected_dark_or_red_alpha_zero_pixels"]:
            raise RuntimeError(f"Unsafe matte: {record}")
        records.append(record)
        images.append(sprite)
    if args.mode == "verify":
        audit = json.loads(AUDIT.read_text(encoding="utf-8"))
        if (audit["frames"] != records or audit["input_video_sha256"] != sha256(VIDEO)
                or audit["readonly_pipeline_sha256"] != sha256(PIPELINE)
                or audit["script_sha256"] != sha256(Path(__file__))):
            raise RuntimeError("Audit provenance mismatch")
        print(json.dumps({"status": "deterministic_pixels_and_hashes_verified", "frames": len(records)}))
        return
    labels = [f"seq {n:02d} | src {s:03d}" for n, s in enumerate(SELECTED)]
    for background, name in (("checker", "checker"), ((24, 28, 28), "dark"), ((244, 246, 244), "light")):
        sheet(images, labels, QA / f"selected_{name}.jpg", background)
    sheet([Image.fromarray(frames[n]) for n in SELECTED], labels, QA / "selected_raw.jpg")
    for source_index in (0, 53, 60, 61, 112, 136, 144):
        sprite = images[SELECTED.index(source_index)]
        raw = Image.fromarray(frames[source_index]).resize((720, 720), Image.Resampling.LANCZOS)
        light = Image.new("RGB", (720, 720), (244, 246, 244))
        dark = Image.new("RGB", (720, 720), (24, 28, 28))
        light.paste(sprite, (0, 0), sprite)
        dark.paste(sprite, (0, 0), sprite)
        sheet([raw, light, dark, sprite.getchannel("A").convert("RGB")],
              [f"src {source_index:03d} raw", "matte on light", "matte on dark", "alpha"],
              QA / f"detail_{source_index:03d}.png", tile=720, columns=2)
    # Preserve evidence for every chroma-detector hit instead of silently erasing it.
    residual_crops, residual_labels = [], []
    for record, sprite in zip(records, images):
        if not record["strong_magenta_residual_pixels"]:
            continue
        rgba = np.asarray(sprite)
        rgb = rgba[:, :, :3].astype(np.int32)
        hit = (rgba[:, :, 3] > 16) & (rgb[:, :, 0] > 150) & (rgb[:, :, 2] > 145) & (rgb[:, :, 1] < 115) & (rgb[:, :, 0]+rgb[:, :, 2] > 2*rgb[:, :, 1]+130)
        y, x = np.nonzero(hit)
        for xx, yy in sorted(set((int(xx)//48, int(yy)//48) for xx, yy in zip(x, y))):
            box = (max(0, xx*48-8), max(0, yy*48-8), min(720, (xx+1)*48+8), min(720, (yy+1)*48+8))
            crop = sprite.crop(box)
            for name, bg in (("light", (244, 246, 244)), ("dark", (24, 28, 28))):
                base = Image.new("RGB", crop.size, bg)
                base.paste(crop, (0, 0), crop)
                residual_crops.append(base.resize((240, 240), Image.Resampling.NEAREST))
                residual_labels.append(f"s{record['source_index_0_based']:03d} x{box[0]} y{box[1]} {name}")
    if residual_crops:
        sheet(residual_crops, residual_labels, QA / "chroma_detector_hit_crops.png")
    base = pipeline.checkerboard((720, 720), 32)
    preview = []
    durations = []
    for n, sprite in enumerate(images):
        composed = base.copy()
        composed.paste(sprite, (0, 0), sprite)
        preview.append(composed)
        end = SELECTED[n+1] if n+1 < len(SELECTED) else len(frames)
        durations.append(max(10, round((end-SELECTED[n])/24*100)*10))
    preview[0].save(QA / "selected_source_timing.gif", save_all=True, append_images=preview[1:], duration=durations, loop=0, disposal=2)
    payload = {
        "status": "candidate_pending_parent_visual_acceptance", "issue": "B1.3.0/215",
        "input_video": str(VIDEO), "input_video_sha256": sha256(VIDEO), "video": metadata,
        "script": str(Path(__file__).resolve()), "script_sha256": sha256(Path(__file__)),
        "readonly_pipeline": str(PIPELINE), "readonly_pipeline_sha256": sha256(PIPELINE),
        "runtime": {"python": sys.version, "opencv": cv2.__version__, "numpy": np.__version__},
        "output_directory": str(OUTPUT), "qa_directory": str(QA),
        "selection_method": "explicit_action_reviewed_sequential_decode_source_indices",
        "selected_source_indices_0_based": SELECTED, "frame_count": len(records),
        "matting": {"method": "existing_full_canvas_adaptive_difference_key_soft_alpha_despill", "hard_distance": HARD_DISTANCE, "feather_radius": FEATHER_RADIUS,
                    "resize": "premultiplied_RGBA_INTER_AREA_960_to_720", "crop_or_reanchor": False,
                    "watermark_removal": "none_no_visible_watermark_identified", "weapon_removal": "none_baked_source_discard_only", "generated_or_repainted": False},
        "phase_suggestions_0_based": PHASES, "frames": records,
        "validation": {
            "empty_selected_frames": sum(r["empty"] for r in records),
            "protected_dark_or_red_alpha_zero_selected": sum(r["protected_dark_or_red_alpha_zero_pixels"] for r in records),
            "transparent_nonzero_rgb_selected": sum(r["transparent_nonzero_rgb_pixels"] for r in records),
            "strong_magenta_detector_hits_selected_sum": sum(r["strong_magenta_residual_pixels"] for r in records),
            "strong_magenta_detector_hits_selected_max": max(r["strong_magenta_residual_pixels"] for r in records),
            "strong_magenta_detector_hits_weapon_free_sum": sum(r["strong_magenta_residual_pixels"] for r in records[18:]),
            "reproducibility_check_command": f'python -B "{Path(__file__).resolve()}" verify',
            "all_frame_metrics_path": str(QA / "all-frame-metrics.json"),
        },
        "integration_constraints": ["Parent visual approval required before integration", "No second falling axe", "No armed DashV2 replacement", "Godot supplies translation and hold durations", "Source timing GIF is review only, not recommended gameplay timing"],
        "visual_review": {"status": "candidate_reviewed_pending_parent_acceptance",
            "coverage": "All 145 source/matte frames inspected in labelled contact sheets; key source and alpha details inspected at full size; all 34 selected frames checked on light and dark contact sheets",
            "persistently_weapon_free_source_range_0_based": [61, 144],
            "persistently_weapon_free_sequence_range_0_based": [18, 33],
            "body_alpha_zero_check": "No protected dark/red source pixels fully removed across all 145 frames; edge softening is not counted as full removal",
            "watermark": "No visible text/logo/watermark found; no corner erasure applied",
            "edge_review": "No continuous visible bright magenta fringe in reviewed mattes; sparse strong-magenta detector hits remain and are not represented as absolute zero spill",
            "limitations": [
                "Axe briefly occluded in source 47-50, visible again from 51; not a second spawned axe, but source temporal discontinuity remains",
                "Raised axe touches/clips top boundary in part of source 17-23; source character is not clipped",
                "Axe surface shape and hue fluctuate in source video; no generative repair or RGB repaint applied",
                "Open-hand and wing silhouettes vary slightly; rear cape/black shapes receive normal edge soft alpha",
                "Source provides stationary crouch/forward exertion, no actual dash, impact, or idle recovery",
                "No crop, per-frame reanchor, or synthesized missing weapon/body pixels; parent must judge source scale/foot drift in scene",
                "Sparse per-pixel chroma detections are recorded; parent must inspect full-size details before approval"],
            "approval": "Not approved for runtime until parent visual acceptance"},
    }
    AUDIT.write_text(json.dumps(payload, indent=2, ensure_ascii=True), encoding="utf-8")
    print(json.dumps({"status": payload["status"], "frames": len(records), "audit": str(AUDIT)}))


if __name__ == "__main__":
    main()
