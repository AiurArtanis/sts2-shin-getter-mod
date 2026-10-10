#!/usr/bin/env python3
"""Validate stable v1.3.0 publication data, without claiming runtime acceptance."""
from pathlib import Path
import datetime
import json
import re

PROJECT = Path(__file__).resolve().parents[1]
REPO = PROJECT.parent
VERSION = "v1.3.0"
KEY = "SHIN_GETTER_CHUNIBYO.UPDATE.v1_3_0"


def require(condition, message):
    if not condition:
        raise AssertionError(message)


manifest = json.loads((PROJECT / "ShinGetterMod.json").read_text(encoding="utf-8"))
require(manifest["version"] == VERSION, "Stable manifest version mismatch")
require(manifest["min_game_version"] == "0.107.0", "Stable minimum game version changed")
history = json.loads((PROJECT / "ShinGetterMod/update_history.json").read_text(encoding="utf-8"))
require(history[0]["version"] == VERSION and history[0]["localization_key"] == KEY, "Newest history entry mismatch")
require(sum(item["version"] == VERSION for item in history) == 1, "Duplicate v1.3.0 history")
datetime.date.fromisoformat(history[0]["date"])

for language, readme, bbcode in (
    ("zhs", "README.md", "WORKSHOP_DESCRIPTION_BBCODE.txt"),
    ("eng", "README_EN.md", "WORKSHOP_DESCRIPTION_EN_BBCODE.txt"),
    ("jpn", "README_JP.md", "WORKSHOP_DESCRIPTION_JP_BBCODE.txt"),
):
    settings = json.loads((PROJECT / f"ShinGetterMod/localization/{language}/settings_ui.json").read_text(encoding="utf-8"))
    entry = settings[KEY]
    require(len(entry.splitlines()) == 6 and "sgd" in entry, f"Incomplete {language} game update entry")
    require("v1.3.0-beta" not in entry, f"Unreleased Beta claim in {language}")
    readme_text = (REPO / readme).read_text(encoding="utf-8")
    bbcode_text = (REPO / "workshop" / bbcode).read_text(encoding="utf-8")
    require(VERSION in readme_text and VERSION in bbcode_text, f"Missing {language} version")
    require("sgd" in readme_text and "sgd" in bbcode_text, f"Missing {language} bond tooling")
    require("v1.2.2-beta.111" in readme_text and "v1.2.2-beta.111" in bbcode_text, f"Published Beta boundary missing: {language}")
    require("releases/latest" in readme_text and "releases/latest" in bbcode_text, f"Live download entry missing: {language}")
    require("blob/main/" in bbcode_text and "blob/patch/" not in bbcode_text, f"Stale branch README link: {language}")
    require("releases/tag/mod-v1.3.0" not in readme_text + bbcode_text, "Do not link an unpublished release as an available download")
    for tag in ("h1", "h2", "b", "i", "list", "url"):
        opened = len(re.findall(rf"\[{tag}(?:=[^\]]*)?\]", bbcode_text))
        closed = bbcode_text.count(f"[/{tag}]")
        require(opened == closed, f"Unbalanced BBCode {tag}: {language}")
    note = (REPO / "workshop" / f"CHANGE_NOTE_v1.3.0_{language}.txt").read_text(encoding="utf-8")
    require(note.startswith(VERSION) and "sgd" in note, f"Invalid {language} changeNote")
    require("发布准备" not in note + entry, "Player-facing preparation wording")

release_notes = (REPO / ".github/release-notes/mod-v1.3.0.md").read_text(encoding="utf-8")
require("mod-v1.3.0" in release_notes and "shin-getter-mod-v1.3.0.zip" in release_notes, "Package contract missing")
require("v1.3.0-beta" not in release_notes, "Unreleased Beta package promoted")
require("強化版の気合" in json.loads((PROJECT / "ShinGetterMod/localization/jpn/settings_ui.json").read_text(encoding="utf-8"))[KEY], "Use the official Japanese Ki card name")
print("issue#246: version, three-language publication entries and truthful download/Beta boundaries PASS (not runtime acceptance)")
