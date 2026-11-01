"""Offline final-art integration contracts. Does not import Godot or deploy."""
from pathlib import Path
from io import BytesIO
import hashlib
import json
import subprocess
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parent
MANIFEST = REPO / "art_sources/issue238-v140-20261101/manifest.json"


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def checked_image(path, size):
    require(path.is_file(), f"missing asset: {path}")
    require(path.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n", f"not a PNG: {path}")
    image = Image.open(path)
    image.load()
    require(image.size == size, f"wrong image dimensions: {path}")
    return image


def validate():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    require(len(manifest["cards"]) == 5 and len(manifest["icons"]) == 4, "five cards and four item icons")
    require(manifest["human_approval"] == "approved_Artanis_all_nine_20261102"
            and manifest["production_export_ready"], "final approved/exported selection required")
    selected_jobs = {"SGC_TabletOfTruth": "ART-007", "SGC_TripleCyclone": "ART-027",
                     "SGC_DrillMissile": "ART-037", "SGC_BrokenDrill": "ART-001", "SGC_Annotations": "ART-016",
                     "SGR_ActivatedSapphire": "ART-005", "SGR_SymbioticFilter": "ART-009",
                     "SGR_Ember": "ART-017", "SGR_MobilityCatalyst": "ART-008"}
    for item in manifest["cards"] + manifest["icons"]:
        require(item["job"] == selected_jobs[item["model"]] and item["human_approval"] == "approved_Artanis"
                and len(item["approved_source_sha256"]) == 64, "final human selection drifted")
    atlas = manifest["card_atlas"]
    path = REPO / atlas["path"]
    require(digest(path) == atlas["sha256"], "candidate card atlas receipt mismatch")
    current = checked_image(path, (2500, 2470)).convert("RGB")
    baseline = subprocess.check_output(["git", "show", f"{atlas['baseline_commit']}:{atlas['path']}"], cwd=REPO)
    require(hashlib.sha256(baseline).hexdigest() == atlas["baseline_sha256"], "wrong card atlas baseline")
    original = Image.open(BytesIO(baseline)).convert("RGB")
    mask = Image.new("L", current.size)
    expected = {
        "SGC_TabletOfTruth": (78, "s_g_c_tablet_of_truth"),
        "SGC_TripleCyclone": (79, "s_g_c_triple_cyclone"),
        "SGC_DrillMissile": (80, "s_g_c_drill_missile"),
        "SGC_BrokenDrill": (81, "s_g_c_broken_drill"),
        "SGC_Annotations": (82, "s_g_c_annotations"),
    }
    require({c["model"] for c in manifest["cards"]} == set(expected), "wrong candidate card IDs")
    resource_checks = (ROOT / "tools/validate-mod-resources.gd").read_text(encoding="utf-8-sig")
    for card in manifest["cards"]:
        sequence, slug = expected[card["model"]]
        require(card["sequence"] == sequence, "card sequence moved")
        full = ROOT / f"images/packed/card_portraits/shin_getter/{slug}.png"
        require(REPO / card["portrait"] == full, "native full portrait path mismatch")
        image = checked_image(full, (1000, 760))
        require(image.mode in ("RGB", "P") and "transparency" not in image.info,
                "card full portraits require RGB/indexed PNG without alpha")
        require(digest(full) == card["portrait_sha256"], "full portrait SHA mismatch")
        x, y = (sequence - 1) % 10 * 250, (sequence - 1) // 10 * 190
        require(card["region"] == [x, y, 250, 190], "fixed atlas slot moved")
        descriptor = ROOT / f"images/atlases/card_atlas.sprites/shin_getter/{slug}.tres"
        require(REPO / card["descriptor"] == descriptor, "native portrait descriptor path mismatch")
        source = descriptor.read_text(encoding="utf-8")
        require(f"region = Rect2({x}, {y}, 250, 190)" in source and "card_atlas_shin_getter_01.png" in source,
                "wrong native card atlas mapping")
        require("filter_clip = true" in source, "card atlas clipping missing")
        region = current.crop((x, y, x + 250, y + 190))
        require(hashlib.sha256(region.tobytes()).hexdigest() == card["thumbnail_rgb_sha256"],
                "atlas slot differs from the reviewed pipeline thumbnail")
        mask.paste(255, (x, y, x + 250, y + 190))
        for asset in (full, descriptor):
            require("res://" + asset.relative_to(ROOT).as_posix() in resource_checks,
                    f"Godot resource gate missing {asset.name}")
    outside = Image.composite(original, current, mask)
    require(ImageChops.difference(outside, original).getbbox() is None,
            "old/Ancient atlas pixels changed outside the five new slots")
    for item in manifest["icons"]:
        index = item["slot"]
        for role in ("icon", "outline"):
            atlas_record = manifest["item_atlases"][role]
            atlas_path = REPO / atlas_record["path"]
            require(digest(atlas_path) == atlas_record["sha256"], "item atlas receipt mismatch")
            image = checked_image(atlas_path, (512, 128)).convert("RGBA")
            region = image.crop((index * 128, 0, (index + 1) * 128, 128))
            alpha = region.getchannel("A")
            require(alpha.getextrema() == (0, 255), "item icon has no genuine transparent/opaque range")
            require(alpha.crop((0, 0, 128, 1)).getbbox() is None and alpha.crop((0, 127, 128, 128)).getbbox() is None,
                    "item icon touches top/bottom atlas edge")
            require(alpha.crop((0, 0, 1, 128)).getbbox() is None and alpha.crop((127, 0, 128, 128)).getbbox() is None,
                    "item icon touches left/right atlas edge")
            descriptor = REPO / item[role + "_descriptor"]
            text = descriptor.read_text(encoding="utf-8")
            require(f"region = Rect2({index * 128}, 0, 128, 128)" in text and atlas_path.name in text,
                    "wrong item icon atlas mapping")
            require("res://" + descriptor.relative_to(ROOT).as_posix() in resource_checks,
                    "item descriptor missing from Godot resource gate")
        icons = manifest["item_atlases"]
        normal = Image.open(REPO / icons["icon"]["path"]).convert("RGBA")
        outline = Image.open(REPO / icons["outline"]["path"]).convert("RGBA")
        require(ImageChops.difference(normal.getchannel("A"), outline.getchannel("A")).getbbox() is None,
                "item outline silhouette differs from icon")
        if item["kind"] == "relic":
            big = REPO / item["big_icon"]
            checked_image(big, (256, 256))
            require(digest(big) == item["big_icon_sha256"], "big relic icon receipt mismatch")
            require("res://" + big.relative_to(ROOT).as_posix() in resource_checks, "big relic icon gate missing")
    require(all(j["independent_qa"] in ("approved", "approved-selected-item-only") for j in manifest["art_jobs"]),
            "unreviewed candidate art selected")
    require(not manifest["deployed"], "feature receipt must not claim deployment")
    print("issue#238 final approved assets: 5 native card entries / 4 item icons; SHA, dimensions, alpha, fixed-slot preservation PASS (game display not verified)")


if __name__ == "__main__":
    validate()
