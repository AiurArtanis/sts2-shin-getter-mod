"""Read-only, in-memory regression cases for the three R5 asset-gate findings."""
from copy import deepcopy
from io import BytesIO
from pathlib import Path
from unittest.mock import patch
import json
from PIL import Image
import validate_issue_238_assets as gate


def main():
    original_read = gate.read_bytes
    manifest = gate.load_json(gate.MANIFEST)
    baseline = gate.baseline_bytes()
    gate.validate(quiet=True)
    cases = []

    def negative(name, change=None, files=None, expected=""):
        candidate = deepcopy(manifest)
        if change:
            change(candidate)
        overlay = {Path(p).resolve(): data for p, data in (files or {}).items()}
        overlay[gate.MANIFEST.resolve()] = json.dumps(candidate).encode("utf-8")

        def read(path):
            key = path.resolve()
            if key in overlay:
                if overlay[key] is None:
                    raise FileNotFoundError("injected absent approved source")
                return overlay[key]
            return original_read(path)

        try:
            with patch.object(gate, "read_bytes", side_effect=read), patch.object(gate, "baseline_bytes", return_value=baseline):
                gate.validate(quiet=True)
        except (AssertionError, FileNotFoundError) as exc:
            if expected and expected not in str(exc):
                raise AssertionError(f"{name}: rejected at unexpected boundary: {exc}") from exc
            cases.append({"case": name, "rejected": True, "boundary": str(exc)})
        else:
            raise AssertionError(f"unsafe asset mutation accepted: {name}")

    negative("four copies of sapphire", lambda m: m.update(icons=[deepcopy(m["icons"][0]) for _ in range(4)]), expected="item model set")
    negative("duplicate item slot", lambda m: m["icons"][1].update(slot=0), expected="unique integers")
    negative("wrong unique slot mapping", lambda m: (m["icons"][0].update(slot=1), m["icons"][1].update(slot=0)), expected="identity/slot")
    negative("wrong native descriptor path", lambda m: m["icons"][1].update(icon_descriptor=m["icons"][0]["icon_descriptor"]), expected="descriptor path")
    negative("relic disguised as potion", lambda m: m["icons"][1].update(kind="potion"), expected="identity/slot")
    negative("zero source hashes and empty jobs", lambda m: ([a.update(approved_source_sha256="0" * 64) for a in m["cards"] + m["icons"]], m.update(art_jobs=[])), expected="approved source SHA")
    negative("empty job receipts", lambda m: m.update(art_jobs=[]), expected="art job receipts")
    negative("duplicate job receipts", lambda m: m.update(art_jobs=[deepcopy(m["art_jobs"][0]) for _ in range(9)]), expected="art job receipts")
    negative("missing selected job item", lambda m: m["art_jobs"][0].update(selected_items=[]), expected="job receipt drifted")
    negative("altered review receipt hash", lambda m: m["art_jobs"][0].update(review_sha256="0" * 64), expected="job receipt drifted")
    negative("zero original approval receipt", lambda m: m.update(human_approval_receipt_sha256="0" * 64), expected="approved/exported")
    negative("zero original export receipts", lambda m: m.update(production_exports={k: "0" * 64 for k in m["production_exports"]}), expected="export receipts differ")
    provenance = gate.load_json(gate.PROVENANCE)
    forged = deepcopy(provenance)
    forged["approval"]["assets"][0]["source_sha256"] = "0" * 64
    negative("self-consistent forged portable provenance", files={gate.PROVENANCE: json.dumps(forged).encode()}, expected="provenance receipt SHA")
    drill_source = gate.REPO / gate.ART / "approved_sources/s_g_c_drill_missile-rgb.png"
    negative("changed actual approved RGB source", files={drill_source: original_read(drill_source) + b"fixture"}, expected="approved source SHA")
    negative("missing actual approved RGB source", files={drill_source: None}, expected="absent approved source")
    negative("wrong fixed baseline reference", lambda m: m["card_atlas"].update(baseline_commit="HEAD"), expected="baseline/path")
    drill = next(c for c in manifest["cards"] if c["model"] == "SGC_DrillMissile")
    drill_path = gate.REPO / drill["portrait"]
    changed_portrait = original_read(drill_path) + b"fixture"
    negative("changed actual portrait export with refreshed manifest SHA",
             lambda m: next(c for c in m["cards"] if c["model"] == "SGC_DrillMissile").update(portrait_sha256=gate.sha(changed_portrait)),
             files={drill_path: changed_portrait}, expected="portrait export differs")
    item_atlas_path = gate.ROOT / gate.ITEM_ATLASES["icon"]
    changed_item_atlas = original_read(item_atlas_path) + b"fixture"
    negative("changed actual item atlas export with refreshed manifest SHA",
             lambda m: m["item_atlases"]["icon"].update(sha256=gate.sha(changed_item_atlas)),
             files={item_atlas_path: changed_item_atlas}, expected="item atlas export differs")

    atlas_path = gate.ROOT / gate.CARD_ATLAS
    original_atlas = Image.open(BytesIO(original_read(atlas_path))).convert("RGBA")
    for component in ("alpha", "rgb"):
        image = original_atlas.copy()
        r, g, b, a = image.getpixel((0, 0))
        image.putpixel((0, 0), (r, g, b, a ^ 1) if component == "alpha" else (r ^ 1, g, b, a))
        stream = BytesIO()
        image.save(stream, format="PNG")
        changed = stream.getvalue()
        negative(f"outside-slot {component}-only atlas change with refreshed receipt",
                 lambda m, data=changed: m["card_atlas"].update(sha256=gate.sha(data)),
                 files={atlas_path: changed}, expected="RGBA atlas pixels changed")

    # Each of the eight fixed native item entrances must independently enforce clipping.
    descriptors = []
    for item in manifest["icons"]:
        for role in ("icon", "outline"):
            path = gate.REPO / item[role + "_descriptor"]
            data = original_read(path)
            assert b"filter_clip = true" in data
            disabled = data.replace(b"filter_clip = true", b"filter_clip = false")
            descriptors.append((path, disabled))
            negative(f"clipping disabled: {item['model']}/{role}", files={path: disabled}, expected="clipping missing")
    negative("all eight item clips disabled", files=dict(descriptors), expected="clipping missing")
    first, disabled = descriptors[0]
    negative("comment cannot fake enabled clipping", files={first: disabled + b"\n; filter_clip = true\n"}, expected="clipping missing")
    native_path = original_read(first)
    negative("comment cannot fake canonical atlas reference", files={first: native_path.replace(
        b'path="res://images/atlases/issue238_item_atlas.png"', b'path="res://images/atlases/other.png"')
        + b"\n; issue238_item_atlas.png\n"}, expected="native atlas reference")

    # The filesystem and the normal gate are unchanged after all overlays.
    assert gate.read_bytes is original_read
    gate.validate(quiet=True)
    print(json.dumps({"positive_before_after": "PASS", "negative_cases": len(cases), "all_rejected": True,
                      "mutation_scope": "memory_only", "product_modified": False, "cases": cases}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
