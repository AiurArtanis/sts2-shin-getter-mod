#!/usr/bin/env python3
"""Read-only compact-v1 card-art gate. No import, Godot, gameplay, PCK or deployment."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import zlib
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = Path(__file__).with_name("issue237-card-art-manifest.json")
ATLAS_PATH = "images/atlases/card_atlas_shin_getter_01.png"


def require(ok: bool, message: str):
    if not ok:
        raise AssertionError(message)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def png(path: Path, size: tuple[int, int], color: int):
    raw = path.read_bytes()
    require(raw[:8] == b"\x89PNG\r\n\x1a\n", f"not PNG: {path}")
    require(struct.unpack(">II", raw[16:24]) == size, f"size mismatch: {path}")
    require(raw[24] == 8 and raw[25] == color, f"bit depth/color type mismatch: {path}")
    pos = 8
    kinds = []
    while pos < len(raw):
        length = struct.unpack(">I", raw[pos:pos + 4])[0]
        tag = raw[pos + 4:pos + 8]
        require(pos + length + 12 <= len(raw), f"truncated PNG: {path}")
        data = raw[pos + 4:pos + 8 + length]
        crc = struct.unpack(">I", raw[pos + 8 + length:pos + 12 + length])[0]
        require(zlib.crc32(data) & 0xffffffff == crc, f"PNG CRC: {path}")
        kinds.append(tag)
        pos += length + 12
        if tag == b"IEND":
            break
    require(pos == len(raw) and kinds[-1:] == [b"IEND"], f"PNG end: {path}")
    require(b"tRNS" not in kinds and b"acTL" not in kinds, f"transparent/animated PNG: {path}")


def validate(root: Path = ROOT):
    receipt = json.loads(MANIFEST.read_text(encoding="utf-8"))
    require(receipt["schema"] == 1 and receipt["layout"] == "cardface-compact-v1", "receipt schema/layout")
    require({kind: sum(cell["kind"] == kind for cell in receipt["cells"]) for kind in ("ordinary", "ancient-placeholder", "reserved")}
            == {"ordinary": 73, "ancient-placeholder": 4, "reserved": 53}, "slot roles/counts")
    atlas = root / ATLAS_PATH
    png(atlas, (2500, 2470), 2)
    require(sha(atlas.read_bytes()) == receipt["atlas"]["sha256"], "atlas differs from approved delivery")
    with Image.open(atlas) as image:
        rgb = image.convert("RGB")
        require(len(receipt["cells"]) == 130, "fixed-slot inventory")
        placeholders = []
        for n, cell in enumerate(receipt["cells"], 1):
            x, y = ((n - 1) % 10) * 250, ((n - 1) // 10) * 190
            require(cell["sequence"] == n and cell["region"] == [x, y, 250, 190], f"shifted slot: {n}")
            data = rgb.crop((x, y, x + 250, y + 190)).tobytes()
            require(sha(data) == cell["rgb_sha256"], f"wrong cell pixels: {n}")
            if cell["kind"] == "reserved":
                require(not any(data), f"reserved slot is not black: {n}")
            if n in (67, 68, 72, 77):
                require(cell["kind"] == "ancient-placeholder", f"ancient slot moved: {n}")
                placeholders.append(data)
        require(all(data == placeholders[0] for data in placeholders), "ancient placeholders differ")

    expected = {entry["path"]: entry for entry in receipt["textures"]}
    actual = {}
    for path in (root / "images").rglob("*.tres"):
        text = path.read_text(encoding="utf-8-sig")
        if "res://" + ATLAS_PATH in text:
            actual[path.relative_to(root).as_posix()] = text
    require(set(actual) == set(expected) and len(actual) == 76, "incomplete/all-reference AtlasTexture migration")
    require(sum(entry["role"] == "live" for entry in expected.values()) == 73, "live inventory")
    require(sum(entry["role"] == "legacy-unused" for entry in expected.values()) == 3, "legacy inventory")
    require({entry["sequence"] for entry in expected.values() if entry["role"] == "live"}
            == {cell["sequence"] for cell in receipt["cells"] if cell["kind"] == "ordinary"}, "live textures do not match ordinary cells")
    require({entry["sequence"] for entry in expected.values() if entry["role"] == "legacy-unused"} == {67, 68, 77}, "legacy role/sequence drift")
    seen = set()
    for relative, text in actual.items():
        entry = expected[relative]
        require('type="AtlasTexture"' in text, f"resource type: {relative}")
        regions = re.findall(r"(?m)^region = Rect2\(([^)]+)\)$", text)
        require(len(regions) == 1, f"region declaration: {relative}")
        values = [float(value.strip()) for value in regions[0].split(",")]
        n = entry["sequence"]
        region = [((n - 1) % 10) * 250, ((n - 1) // 10) * 190, 250, 190]
        require(values == region == entry["region"], f"old/wrong region: {relative}")
        require(re.findall(r"(?m)^filter_clip = (\w+)$", text) == ["true"], f"sampling clip: {relative}")
        require((region[0], region[1]) not in seen, f"overlap: {relative}")
        seen.add((region[0], region[1]))
        require(0 <= region[0] <= 2250 and 0 <= region[1] <= 2280, f"region bounds: {relative}")
        require(sha((root / relative).read_bytes()) == entry["sha256"], f"unapproved texture changes: {relative}")

    for entry in receipt["portraits"]:
        path = root / entry["path"]
        png(path, (1000, 760), 3)
        require(sha(path.read_bytes()) == entry["sha256"], f"portrait delivery drift: {path}")
    require(len(receipt["portraits"]) == 8, "8-card scope")
    for entry in receipt["preserved_files"]:
        require(sha((root / entry["path"]).read_bytes()) == entry["sha256"], f"unrelated/independent asset changed: {entry['path']}")
    for stem in ("stoner_sunshine", "shin_form", "saint_dragon_roar", "getter_landing"):
        model = (root / f"src/Models/Cards/SGC_{receipt['ancient_models'][stem]}.cs").read_text(encoding="utf-8-sig")
        require("CardRarity.Ancient" in model and f"packed/card_single/shin_getter/s_g_c_{stem}_card.png" in model,
                f"ancient independent portrait consumer changed: {stem}")
    gate = (root / "tools/validate_issue_89.py").read_text(encoding="utf-8-sig")
    require("!= (2500, 2470)" in gate and "!= (2524, 2524)" not in gate, "issue#89 atlas size gate drift")
    print("issue#237 PASS: 9 approved PNG,76 AtlasTextures (73 live+3 legacy),130 RGB cells; not Godot/game acceptance")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    args = parser.parse_args()
    validate(args.root)


if __name__ == "__main__":
    main()
