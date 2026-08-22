#!/usr/bin/env python3
"""Static validation for issue#153 update indicators and read-version persistence."""

from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "ShinGetterMod.json"
CONFIG = ROOT / "src/Config/ShinGetterChunibyoConfigService.cs"
BADGE = ROOT / "src/Nodes/Config/NShinGetterUpdateBadge.cs"
MENU_PATCH = ROOT / "src/Patches/ShinGetterChunibyoMenuPatch.cs"
SUBMENU = ROOT / "src/Nodes/Config/NChunibyoConfigSubmenu.cs"
LOCALIZATION_ROOT = ROOT / "ShinGetterMod/localization"
LANGUAGES = ("zhs", "jpn", "eng")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def method_body(source: str, signature: str) -> str:
    start = source.index(signature)
    brace = source.index("{", start)
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[start:index + 1]
    raise AssertionError(f"Unterminated method: {signature}")


def validate_manifest_and_config_contract() -> None:
    manifest = json.loads(read(MANIFEST))
    current_version = manifest.get("version")
    require(isinstance(current_version, str) and current_version.strip() == current_version,
            "ShinGetterMod.json.version must be a normalized non-empty string")

    config = read(CONFIG)
    for needle in (
        "public string LastReadUpdateVersion { get; set; } = string.Empty",
        'private const string ManifestPath = "res://ShinGetterMod.json"',
        "internal static string CurrentManifestVersion => ReadCurrentManifestVersion()",
        'document.RootElement.GetProperty("version").GetString()',
        "version.Trim()",
        "internal static bool IsCurrentUpdateUnread",
        "NormalizeVersion(Current.LastReadUpdateVersion)",
        "StringComparison.Ordinal",
    ):
        require(needle in config, f"Missing manifest/config contract: {needle}")

    submenu = read(SUBMENU)
    require("ManifestPath" not in submenu and 'GetProperty("version")' not in submenu,
            "UI must not duplicate the manifest-version source")
    require("ShinGetterChunibyoConfigService.CurrentManifestVersion" in submenu,
            "version display must use the centralized manifest service")

    mark_body = method_body(config, "internal static bool MarkCurrentUpdateRead")
    required_order = (
        "string previousVersion = Current.LastReadUpdateVersion",
        "Current.LastReadUpdateVersion = currentVersion",
        "if (!Save(out error))",
        "Current.LastReadUpdateVersion = previousVersion",
        "UpdateReadStateChanged?.Invoke()",
    )
    positions = [mark_body.index(needle) for needle in required_order]
    require(positions == sorted(positions),
            "MarkCurrentUpdateRead must save atomically, roll back, then broadcast")
    require("return false" in mark_body[positions[2]:positions[4]],
            "failed read-version save must return failure before broadcasting")
    save_body = method_body(config, "public static bool Save")
    require('string temporaryPath = path + ".tmp"' in save_body
            and "File.Move(temporaryPath, path, overwrite: true)" in save_body,
            "read-version persistence must retain the existing atomic save")
    require("UpdateReadStateChanged?.Invoke()" not in save_body,
            "ordinary config saves must not broadcast a false read-state change")


def validate_badge_and_mounts() -> None:
    badge = read(BADGE)
    for needle in (
        'Text = "NEW"',
        "CustomMinimumSize = BadgeSize",
        "MouseFilter = MouseFilterEnum.Ignore",
        "FocusMode = FocusModeEnum.None",
        "AnchorLeft = 1f",
        "AnchorRight = 1f",
        "UpdateReadStateChanged += RefreshVisibility",
        "UpdateReadStateChanged -= RefreshVisibility",
        "public override void _ExitTree()",
        "if (!_subscribed)",
        "host.GetNodeOrNull<NShinGetterUpdateBadge>(BadgeNodeName)",
        "Visible = ShinGetterChunibyoConfigService.IsCurrentUpdateUnread",
    ):
        require(needle in badge, f"Missing NEW badge boundary: {needle}")
    require(not re.search(
        r"static\s+(?:readonly\s+)?(?:Control|NShinGetterUpdateBadge)\??\s+\w+\s*(?:=|;)",
        badge,
    ),
            "badge helper must not retain Godot nodes through static references")

    menu = read(MENU_PATCH)
    submenu = read(SUBMENU)
    require("Current.ShowInMainMenu" in menu,
            "main-menu NEW marker must retain the ShowInMainMenu boundary")
    require(menu.count("NShinGetterUpdateBadge.AttachTo") >= 4,
            "main-menu and settings entries need creation and duplicate-ready badge paths")
    require("is { } existing" in menu and "is { } existingEntry" in menu,
            "both outer entry patches must prevent duplicate _Ready nodes")
    require("private NShinGetterConfigActionButton? _updateHistoryButton" in submenu
            and "NShinGetterUpdateBadge.AttachTo(_updateHistoryButton)" in submenu,
            "the update-history button must retain and mount the shared badge")
    require(submenu.count("MarkCurrentUpdateRead") == 1,
            "entering the config page must never mark the update as read")


def validate_popup_success_boundary() -> None:
    submenu = read(SUBMENU)
    show_history = method_body(submenu, "private void ShowUpdateHistory()")
    ordered = (
        "TryReadUpdateHistory",
        "ShowUpdateHistoryPopup",
        "MarkCurrentUpdateRead",
    )
    positions = [show_history.index(needle) for needle in ordered]
    require(positions == sorted(positions),
            "history content and popup visibility must succeed before read-version persistence")
    require("if (!ShowUpdateHistoryPopup" in show_history and "return" in show_history,
            "popup failure must leave the update unread")
    require("SAVE_ERROR_TITLE" in show_history and "saveError" in show_history,
            "save failure must use the existing error popup and keep the marker")

    popup = method_body(submenu, "private bool ShowUpdateHistoryPopup")
    for needle in (
        "return false",
        "NChunibyoUpdateHistoryPopup.Create",
        "modalContainer.Add(popup)",
        "GodotObject.IsInstanceValid(popup)",
        "popup.IsInsideTree()",
        "popup.IsVisibleInTree()",
    ):
        require(needle in popup, f"Missing popup success boundary: {needle}")
    require(popup.index("modalContainer.Add(popup)") < popup.index("popup.IsInsideTree()"),
            "popup must be added to the modal tree before it can clear NEW")

    reader = method_body(submenu, "private static bool TryReadUpdateHistory")
    require("return true" in reader and "return false" in reader and "error = ex.Message" in reader,
            "history read failures must remain distinguishable from an empty valid history")


def validate_localization_boundary() -> None:
    tables = {
        language: json.loads(read(LOCALIZATION_ROOT / language / "settings_ui.json"))
        for language in LANGUAGES
    }
    reference_keys = set(tables[LANGUAGES[0]])
    for language in LANGUAGES[1:]:
        require(set(tables[language]) == reference_keys,
                f"settings_ui key set mismatch for {language}")
    for table in tables.values():
        require("NEW" not in table,
                "the universal uppercase NEW badge must not add a localization key")


def main() -> None:
    validate_manifest_and_config_contract()
    validate_badge_and_mounts()
    validate_popup_success_boundary()
    validate_localization_boundary()
    print("issue#153 validation passed")


if __name__ == "__main__":
    main()
