"""Offline final-art integration gate; no Godot, artwork approval or deployment."""
from pathlib import Path
from io import BytesIO
import hashlib
import json
import re
import subprocess
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parent
ART = "art_sources/issue238-v140-20261101"
MANIFEST = REPO / ART / "manifest.json"
PROVENANCE = REPO / ART / "final-art-provenance-20261102.json"
# Independent of the mutable integration manifest. Complete selected records are
# portable; original approval/export receipts remain traceable by their SHA.
PROVENANCE_SHA256 = "60636b8d533a299e82ceb1759d4284605f7ed87416d800873dcb588c68057f5e"
ORIGINAL_RECEIPTS = {
    "approval": "b5d9b2b913e944dda03651bfda8ce0ed8cd408d828083484dff53316f1347359",
    "cards": "4ad369e88c80244e14a16071de3a77c415713c0e5307db7c51d7e5438239d4c3",
    "icons": "50f1385d459aaf2bf7dcaad6a5fb307efcc6688e60cd3a43208aff6c6de1ddea",
}
CARD_ATLAS = "images/atlases/card_atlas_shin_getter_01.png"
BASELINE_COMMIT = "26c8da9a907fd9e12d41bf5bf17d24ca5975bfa8"
BASELINE_SHA256 = "8fa1bbc0f3f168591664a7bc4a6ba4a11029c5d132c8902e9bfa8e1378f009a2"
CARDS = {
    "SGC_TabletOfTruth": (78, "s_g_c_tablet_of_truth", "ART-007"),
    "SGC_TripleCyclone": (79, "s_g_c_triple_cyclone", "ART-027"),
    "SGC_DrillMissile": (80, "s_g_c_drill_missile", "ART-037"),
    "SGC_BrokenDrill": (81, "s_g_c_broken_drill", "ART-001"),
    "SGC_Annotations": (82, "s_g_c_annotations", "ART-016"),
}
ITEMS = {
    "SGR_ActivatedSapphire": (0, "relic", "s_g_r_activated_sapphire", "ART-005"),
    "SGR_SymbioticFilter": (1, "relic", "s_g_r_symbiotic_filter", "ART-009"),
    "SGR_Ember": (2, "relic", "s_g_r_ember", "ART-017"),
    "SGR_MobilityCatalyst": (3, "potion", "s_g_r_mobility_catalyst", "ART-008"),
}
ITEM_ATLASES = {"icon": "images/atlases/issue238_item_atlas.png",
                "outline": "images/atlases/issue238_item_outline_atlas.png"}


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def read_bytes(path):
    return path.read_bytes()


def sha(data):
    return hashlib.sha256(data).hexdigest()


def digest(path):
    return sha(read_bytes(path))


def load_json(path):
    return json.loads(read_bytes(path).decode("utf-8-sig"))


def checked_image(path, size):
    data = read_bytes(path)
    require(data[:8] == b"\x89PNG\r\n\x1a\n", f"not a PNG: {path.name}")
    image = Image.open(BytesIO(data))
    image.load()
    require(image.size == size, f"wrong image dimensions: {path.name}")
    return image


def baseline_bytes():
    return subprocess.check_output(["git", "show", f"{BASELINE_COMMIT}:shin-getter-mod-godot/{CARD_ATLAS}"], cwd=REPO)


def unique_records(rows, expected, label):
    require(len(rows) == len(expected) and {r["model"] for r in rows} == set(expected),
            f"wrong or duplicate {label} model set")
    return {r["model"]: r for r in rows}


def descriptor(path, atlas, region):
    text = read_bytes(path).decode("utf-8-sig").replace("\r\n", "\n")
    refs = re.findall(r'^\[ext_resource type="Texture2D" path="([^"]+)" id="([^"]+)"\]$', text, re.M)
    require(len(refs) == 1 and refs[0][0] == "res://" + atlas, f"wrong native atlas reference: {path.name}")
    require(re.findall(r'^atlas\s*=\s*(.+)$', text, re.M) == [f'ExtResource("{refs[0][1]}")'],
            f"wrong native atlas binding: {path.name}")
    regions = re.findall(r'^region\s*=\s*Rect2\(([^)]+)\)\s*$', text, re.M)
    require(len(regions) == 1 and [int(x.strip()) for x in regions[0].split(",")] == region,
            f"wrong native atlas region: {path.name}")
    require(re.findall(r'^filter_clip\s*=\s*(.+)$', text, re.M) == ["true"],
            f"native atlas clipping missing: {path.name}")


def validate(quiet=False):
    manifest = load_json(MANIFEST)
    provenance_data = read_bytes(PROVENANCE)
    require(sha(provenance_data) == PROVENANCE_SHA256, "fixed provenance receipt SHA mismatch")
    provenance = json.loads(provenance_data)
    require(provenance["schema"] == 1 and provenance["issue"] == 238
            and provenance["original_receipt_sha256"] == ORIGINAL_RECEIPTS
            and provenance["approval"]["human_approved"], "incomplete approval/export authority")
    require(manifest["human_approval"] == "approved_Artanis_all_nine_20261102"
            and manifest["human_approval_receipt_sha256"] == ORIGINAL_RECEIPTS["approval"]
            and manifest["production_export_ready"], "final approved/exported selection required")
    require(manifest["production_exports"] == {"cards_manifest_sha256": ORIGINAL_RECEIPTS["cards"],
                                               "icons_manifest_sha256": ORIGINAL_RECEIPTS["icons"]},
            "original production export receipts differ")
    cards = unique_records(manifest["cards"], CARDS, "card")
    items = unique_records(manifest["icons"], ITEMS, "item")
    approved = unique_records(provenance["approval"]["assets"], CARDS.keys() | ITEMS.keys(), "approved source")
    exported_cards = unique_records(provenance["exports"]["cards"], CARDS, "exported card")
    exported_items = unique_records(provenance["exports"]["icons"], ITEMS, "exported item")

    # Seven approved sources already exist as runtime files. Only the approved
    # Drill RGB and potion 256 master need a source archive; no derivative copies.
    for model, approval in approved.items():
        if model in CARDS:
            _, slug, job = CARDS[model]
            source = f"shin-getter-mod-godot/images/packed/card_portraits/shin_getter/{slug}.png"
            kind, size = "card", (1000, 760)
            if model == "SGC_DrillMissile":
                source = ART + "/approved_sources/s_g_c_drill_missile-rgb.png"
        else:
            _, kind, slug, job = ITEMS[model]
            source, size = f"shin-getter-mod-godot/images/relics/{slug}.png", (256, 256)
            if kind == "potion":
                source = ART + "/approved_sources/s_g_r_mobility_catalyst-256.png"
        item = cards[model] if model in cards else items[model]
        require(approval["source"] == source and approval["job"] == job and approval["kind"] == kind
                and approval["human_approval"].startswith("approved_Artanis_"), "approved source identity drifted")
        require(item["job"] == job and item["human_approval"] == "approved_Artanis"
                and item["approved_source_sha256"] == approval["source_sha256"]
                and digest(REPO / source) == approval["source_sha256"], "actual approved source SHA differs")
        checked_image(REPO / source, size)

    expected_jobs = provenance["art_jobs"]
    require(len(expected_jobs) == 9 and len(manifest["art_jobs"]) == 9
            and len({j["job"] for j in manifest["art_jobs"]}) == 9, "missing/duplicate art job receipts")
    jobs = {j["job"]: j for j in manifest["art_jobs"]}
    require(set(jobs) == {a["job"] for a in approved.values()}, "art job coverage differs from nine approved sources")
    for receipt in expected_jobs:
        require(all(jobs[receipt["job"]].get(k) == v for k, v in receipt.items()), "selected art job receipt drifted")

    resource_checks = read_bytes(ROOT / "tools/validate-mod-resources.gd").decode("utf-8-sig")
    atlas = manifest["card_atlas"]
    require(atlas["path"] == "shin-getter-mod-godot/" + CARD_ATLAS
            and atlas["baseline_commit"] == BASELINE_COMMIT and atlas["baseline_sha256"] == BASELINE_SHA256,
            "fixed card atlas baseline/path drifted")
    path = ROOT / CARD_ATLAS
    require(digest(path) == atlas["sha256"], "card atlas receipt mismatch")
    current = checked_image(path, (2500, 2470)).convert("RGBA")
    baseline = baseline_bytes()
    require(sha(baseline) == BASELINE_SHA256, "wrong immutable card atlas baseline")
    original = Image.open(BytesIO(baseline)).convert("RGBA")
    mask = Image.new("L", current.size)
    for model, card in cards.items():
        sequence, slug, job = CARDS[model]
        exported = exported_cards[model]
        full = ROOT / f"images/packed/card_portraits/shin_getter/{slug}.png"
        require(card["sequence"] == sequence == exported["sequence"] and exported["job"] == job
                and exported["source_sha256"] == approved[model]["source_sha256"] and exported["production_ready"],
                "card export identity drifted")
        require(card["portrait"] == exported["portrait"] == full.relative_to(REPO).as_posix()
                and digest(full) == card["portrait_sha256"] == exported["portrait_sha256"], "actual portrait export differs")
        image = checked_image(full, (1000, 760))
        require(image.mode == "P" and read_bytes(full)[24:26] == bytes((8, 3)) and "transparency" not in image.info,
                "production portraits require opaque 8-bit indexed PNG")
        x, y = (sequence - 1) % 10 * 250, (sequence - 1) // 10 * 190
        require(card["region"] == [x, y, 250, 190], "fixed card atlas slot moved")
        desc = ROOT / f"images/atlases/card_atlas.sprites/shin_getter/{slug}.tres"
        require(card["descriptor"] == desc.relative_to(REPO).as_posix(), "native card descriptor path drifted")
        descriptor(desc, CARD_ATLAS, [x, y, 250, 190])
        region = current.crop((x, y, x + 250, y + 190))
        require(card["thumbnail_sha256"] == exported["thumbnail_export_sha256"]
                and sha(region.tobytes()) == exported["thumbnail_rgba_sha256"]
                and sha(region.convert("RGB").tobytes()) == card["thumbnail_rgb_sha256"], "actual thumbnail differs from fixed export receipt")
        mask.paste(255, (x, y, x + 250, y + 190))
        for asset in (full, desc):
            require("res://" + asset.relative_to(ROOT).as_posix() in resource_checks, "card missing from Godot resource gate")
    # Compare every RGBA byte; bare RGBA getbbox() can miss RGB-only differences.
    outside = Image.composite(original, current, mask)
    require(outside.tobytes() == original.tobytes(), "RGBA atlas pixels changed outside the five new slots")

    require(set(manifest["item_atlases"]) == set(ITEM_ATLASES), "wrong item atlas roles")
    require({r["role"] for r in provenance["exports"]["item_atlases"]} == set(ITEM_ATLASES)
            and len(provenance["exports"]["item_atlases"]) == 2, "missing item atlas export receipts")
    atlas_exports = {r["role"]: r for r in provenance["exports"]["item_atlases"]}
    item_images = {}
    for role, relative in ITEM_ATLASES.items():
        record, exported = manifest["item_atlases"][role], atlas_exports[role]
        require(record["path"] == exported["path"] == "shin-getter-mod-godot/" + relative
                and digest(ROOT / relative) == record["sha256"] == exported["sha256"], "actual item atlas export differs")
        item_images[role] = checked_image(ROOT / relative, (512, 128)).convert("RGBA")
    require(all(type(i["slot"]) is int for i in items.values()) and len({i["slot"] for i in items.values()}) == 4,
            "item slots must be four unique integers")
    for model, item in items.items():
        index, kind, slug, job = ITEMS[model]
        exported = exported_items[model]
        require(item["slot"] == index == exported["slot"] and item["kind"] == kind == exported["kind"]
                and exported["job"] == job and exported["source_sha256"] == approved[model]["source_sha256"], "fixed item identity/slot drifted")
        regions = {}
        for role in ITEM_ATLASES:
            entry = exported["exports"][role]
            region = item_images[role].crop((index * 128, 0, (index + 1) * 128, 128))
            require(item[role + "_source_sha256"] == entry["png_sha256"]
                    and sha(region.tobytes()) == entry["rgba_sha256"], "actual icon/outline differs from fixed export receipt")
            alpha = region.getchannel("A")
            require(alpha.getextrema() == (0, 255), "item requires genuine transparent/opaque pixels")
            require(all(alpha.crop(box).getbbox() is None for box in ((0, 0, 128, 1), (0, 127, 128, 128),
                    (0, 0, 1, 128), (127, 0, 128, 128))), "item touches atlas cell edge")
            folder = f"{kind}_{'outline_' if role == 'outline' else ''}atlas.sprites"
            desc = ROOT / f"images/atlases/{folder}/{slug}.tres"
            require(item[role + "_descriptor"] == desc.relative_to(REPO).as_posix(), "native item descriptor path drifted")
            descriptor(desc, ITEM_ATLASES[role], [index * 128, 0, 128, 128])
            require("res://" + desc.relative_to(ROOT).as_posix() in resource_checks, "item missing from Godot resource gate")
            regions[role] = region
        require(regions["icon"].getchannel("A").tobytes() == regions["outline"].getchannel("A").tobytes(), "outline alpha silhouette differs")
        if kind == "relic":
            big = ROOT / f"images/relics/{slug}.png"
            require(item["big_icon"] == exported["exports"]["big"]["path"] == big.relative_to(REPO).as_posix()
                    and digest(big) == item["big_icon_sha256"] == exported["exports"]["big"]["sha256"], "actual relic big icon export differs")
            checked_image(big, (256, 256))
            require("res://" + big.relative_to(ROOT).as_posix() in resource_checks, "big relic icon missing from resource gate")
    require(not manifest["deployed"], "feature receipt must not claim deployment")
    if not quiet:
        print("issue#238 final assets: 9 actual approved sources / fixed complete export receipts / 5 cards / 4 unique item slots / 8 clipped item entries / RGBA preservation PASS (game display not verified)")


if __name__ == "__main__":
    validate()
