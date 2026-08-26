#!/usr/bin/env python3
"""Static release-note and version consistency gate for issue#181."""

from __future__ import annotations

import json
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = PROJECT_ROOT.parent
VERSION = "v1.2.0"
BETA_VERSION = "v1.2.0-beta.111"
TAG = "mod-v1.2.0"
ARCHIVE = "shin-getter-mod-v1.2.0.zip"
UPDATE_KEY = "SHIN_GETTER_CHUNIBYO.UPDATE.v1_2_0"
RELEASE_URL = (
    "https://github.com/AiurArtanis/sts2-shin-getter-mod/releases/tag/mod-v1.2.0"
)

RELEASE_FILES = {
    "zhs": (
        REPO_ROOT / "README.md",
        REPO_ROOT / "workshop/WORKSHOP_DESCRIPTION_BBCODE.txt",
    ),
    "eng": (
        REPO_ROOT / "README_EN.md",
        REPO_ROOT / "workshop/WORKSHOP_DESCRIPTION_EN_BBCODE.txt",
    ),
    "jpn": (
        REPO_ROOT / "README_JP.md",
        REPO_ROOT / "workshop/WORKSHOP_DESCRIPTION_JP_BBCODE.txt",
    ),
}

CONTENT_COUNT_MARKERS = {
    "zhs": ("77", "卡牌", "13", "遗物", "6", "药水", "2", "附魔"),
    "eng": ("77", "cards", "13", "relics", "6", "potions", "2", "enchantments"),
    "jpn": ("77", "カード", "13", "レリック", "6", "ポーション", "2", "エンチャント"),
}

RELEASE_MARKERS = {
    "zhs": ("90", "30fps", "NEW"),
    "eng": ("90", "30 fps", "NEW"),
    "jpn": ("90", "30fps", "NEW"),
}

EVENT_COUNT_MARKERS = {
    "zhs": ("18",),
    "eng": ("18", "Eighteen", "eighteen"),
    "jpn": ("18",),
}


def require(text: str, path: Path, *needles: str) -> None:
    for needle in needles:
        if needle not in text:
            raise AssertionError(f"Missing issue#181 marker in {path}: {needle}")


def validate_manifest_and_history() -> None:
    manifest_path = PROJECT_ROOT / "ShinGetterMod.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    current_version = manifest.get("version")
    if current_version not in (VERSION, BETA_VERSION):
        raise AssertionError(
            f"Manifest version must be {VERSION} or its audited 0.111 Beta variant: {manifest}"
        )

    history_path = PROJECT_ROOT / "ShinGetterMod/update_history.json"
    history = json.loads(history_path.read_text(encoding="utf-8"))
    expected_latest = (
        {
            "version": BETA_VERSION,
            "date": "2026-08-26",
            "localization_key": "SHIN_GETTER_CHUNIBYO.UPDATE.v1_2_0_beta_111",
        }
        if current_version == BETA_VERSION
        else {
            "version": VERSION,
            "date": "2026-08-25",
            "localization_key": UPDATE_KEY,
        }
    )
    if not history or history[0] != expected_latest:
        raise AssertionError(f"Latest update history entry is incorrect: {history[:1]}")
    if sum(entry.get("version") == VERSION for entry in history) != 1:
        raise AssertionError(f"Update history must contain exactly one {VERSION} entry.")

    localization_root = PROJECT_ROOT / "ShinGetterMod/localization"
    for language in RELEASE_FILES:
        path = localization_root / language / "settings_ui.json"
        table = json.loads(path.read_text(encoding="utf-8"))
        body = table.get(UPDATE_KEY, "")
        if len(body.splitlines()) < 18 or body.count("- ") < 12:
            raise AssertionError(
                f"{VERSION} update history is incomplete for {language}: {path}"
            )


def validate_registered_content_counts() -> None:
    models = PROJECT_ROOT / "src/Models"
    actual = {
        "cards": len(list((models / "Cards").glob("SGC_*.cs"))),
        "relics": len(list((models / "Relics").glob("SGR_*.cs"))),
        "potions": len(list((models / "Potions").glob("*.cs"))),
        "enchantments": len(list((models / "Enchantments").glob("*.cs"))),
    }
    expected = {"cards": 77, "relics": 13, "potions": 6, "enchantments": 2}
    if actual != expected:
        raise AssertionError(f"Release content counts drifted: {actual} != {expected}")


def validate_release_files() -> None:
    for language, paths in RELEASE_FILES.items():
        for path in paths:
            text = path.read_text(encoding="utf-8")
            require(text, path, VERSION, TAG, ARCHIVE, RELEASE_URL)
            require(text, path, *CONTENT_COUNT_MARKERS[language])
            require(text, path, *RELEASE_MARKERS[language])
            if not any(marker in text for marker in EVENT_COUNT_MARKERS[language]):
                raise AssertionError(f"Missing eighteen-event marker in {path}")
            for stale in (
                "v1.1.0",
                "mod-v1.1.0",
                "shin-getter-mod-v1.1.0.zip",
                "24fps",
                "24 fps",
                "72 cards",
                "72 张卡牌",
                "カード72枚",
            ):
                if stale in text:
                    raise AssertionError(f"Stale release marker remains in {path}: {stale}")

    for path in (
        REPO_ROOT / "README.md",
        REPO_ROOT / "README_EN.md",
        REPO_ROOT / "README_JP.md",
    ):
        require(path.read_text(encoding="utf-8"), path, "0.107.0")


def main() -> None:
    validate_manifest_and_history()
    validate_registered_content_counts()
    validate_release_files()
    print("issue#181 release-note validation passed")


if __name__ == "__main__":
    main()
