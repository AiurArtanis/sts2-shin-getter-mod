#!/usr/bin/env python3
"""Positive fixture + negative mutations of the actual issue#237 gate, temp files only."""
import contextlib
import io
import json
from pathlib import Path
import shutil
import tempfile
from PIL import Image
import validate_issue_237 as gate

receipt = json.loads(gate.MANIFEST.read_text(encoding="utf-8"))
required = {entry["path"] for entry in receipt["textures"] + receipt["portraits"] + receipt["preserved_files"]}
required.add(gate.ATLAS_PATH)
required.add("tools/validate_issue_89.py")
required.update(f"src/Models/Cards/SGC_{model}.cs" for model in receipt["ancient_models"].values())

with tempfile.TemporaryDirectory(prefix="shin-getter-issue237-gate-") as temp:
    root = Path(temp)
    for relative in required:
        target = root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(gate.ROOT / relative, target)
    def validate():
        with contextlib.redirect_stdout(io.StringIO()):
            gate.validate(root)
    validate()
    rejected = []
    def negative(name, relative, mutate):
        path = root / relative
        try:
            mutate(path)
            try:
                validate()
            except AssertionError:
                rejected.append(name)
            else:
                raise RuntimeError("Invalid fixture accepted: " + name)
        finally:
            if relative in required:
                shutil.copy2(gate.ROOT / relative, path)
            elif path.exists():
                path.unlink()
    def replace(old, new):
        return lambda path: path.write_text(path.read_text(encoding="utf-8").replace(old, new), encoding="utf-8")
    strike = "images/atlases/card_atlas.sprites/shin_getter/s_g_c_strike.tres"
    landing = "images/atlases/card_atlas.sprites/shin_getter/s_g_c_getter_landing.tres"
    negative("old first-cell padding", strike, replace("Rect2(0, 0, 250, 190)", "Rect2(2, 2, 250, 190)"))
    negative("unmigrated unused ancient texture", landing, replace("Rect2(1500, 1330, 250, 190)", "Rect2(2774, 962, 250, 190)"))
    negative("missing active texture", strike, lambda path: path.unlink())
    negative("extra untracked atlas reference", strike.replace("s_g_c_strike", "unexpected_resource"),
             lambda path: shutil.copy2(root / strike, path))
    negative("wrong atlas resource path", strike, replace("card_atlas_shin_getter_01.png", "ui_atlas.png"))
    negative("clipping disabled", strike, replace("filter_clip = true", "filter_clip = false"))
    negative("wrong frame size", strike, replace("250, 190", "249, 190"))
    negative("old atlas dimensions", gate.ATLAS_PATH,
             lambda path: Image.new("RGB", (2524, 2524)).save(path))
    negative("opaque RGBA is still forbidden", gate.ATLAS_PATH,
             lambda path: Image.new("RGBA", (2500, 2470), (0, 0, 0, 255)).save(path))
    negative("wrong compact atlas pixels", gate.ATLAS_PATH,
             lambda path: Image.new("RGB", (2500, 2470)).save(path))
    negative("portrait swap", receipt["portraits"][0]["path"],
             lambda path: shutil.copy2(root / receipt["portraits"][1]["path"], path))
    negative("corrupt PNG CRC", receipt["portraits"][0]["path"],
             lambda path: path.write_bytes(path.read_bytes()[:-1] + bytes([path.read_bytes()[-1] ^ 1])))
    negative("mipmap import modified", gate.ATLAS_PATH + ".import",
             replace("mipmaps/generate=false", "mipmaps/generate=true"))
    negative("independent ancient PNG modified", "images/packed/card_single/shin_getter/s_g_c_getter_landing_card.png",
             lambda path: path.write_bytes(b"not the original independent texture"))
    negative("ancient consumer switched to atlas", "src/Models/Cards/SGC_GetterLanding.cs",
             replace("packed/card_single/shin_getter/s_g_c_getter_landing_card.png", "atlases/card_atlas.sprites/shin_getter/s_g_c_getter_landing.tres"))
    negative("obsolete issue#89 expectations", "tools/validate_issue_89.py", replace("!= (2500, 2470)", "!= (2524, 2524)"))
    validate()
    print(f"issue#237 gate tests PASS: positive fixture + {len(rejected)} rejected mutations (no Godot/game)")

