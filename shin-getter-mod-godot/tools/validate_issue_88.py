#!/usr/bin/env python3
"""Static regression gate for issue#88 BGM replacement settings."""

from __future__ import annotations

import json
import re
import struct
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "src/Config/ShinGetterChunibyoConfigService.cs"
CATALOG = ROOT / "src/Audio/ShinGetterBgmCatalog.cs"
PREVIEW = ROOT / "src/Audio/ShinGetterBgmPreviewService.cs"
ENCOUNTER = ROOT / "src/Audio/ShinGetterEncounterMusicService.cs"
EXECUTION = ROOT / "src/Audio/ShinGetterExecutionMusicService.cs"
SUBMENU = ROOT / "src/Nodes/Config/NChunibyoConfigSubmenu.cs"
DROPDOWN = ROOT / "src/Nodes/Config/NShinGetterBgmDropdown.cs"
CONTROLS = ROOT / "src/Nodes/Config/NShinGetterBgmPreviewControls.cs"
PREVIEW_BUTTON = ROOT / "src/Nodes/Config/NShinGetterBgmPreviewButton.cs"
RESOURCE_GATE = ROOT / "tools/validate-mod-resources.gd"
ENERGY_TEXTURE = ROOT / "images/atlases/ui_atlas.sprites/card/energy_shin_getter.tres"
LOCALIZATION_ROOT = ROOT / "ShinGetterMod/localization"
LANGUAGES = ("zhs", "jpn", "eng")

TRACK_TITLES = {
    "DEFAULT": ("（预设）", "（標準）", "(default)"),
    "RELIEF": ("安堵", "安堵", "Relief"),
    "GRIEF": ("悲痛", "悲痛", "Grief"),
    "MORNING_ON_THE_TUNDRA": ("冰原之晨", "氷原の朝", "Morning on the Tundra"),
    "BRUTALITY": ("残虐", "残虐", "Brutality"),
    "REBEL_ARMY": ("反叛军", "反乱軍", "Rebel Army"),
    "PAST": ("过去", "過去", "Past"),
    "MEMORY": ("记忆", "記憶", "Memory"),
    "INTERFERENCE": ("扰乱", "撹乱", "Interference"),
    "TENSION": ("紧迫", "緊迫", "Tension"),
    "COLD_BLOODEDNESS": ("冷酷", "冷酷", "Cold-Bloodedness"),
    "MYSTERY": ("谜", "謎", "Mystery"),
    "MOMENTUM": ("气势", "気勢", "Momentum"),
    "MAJESTY": ("威风", "威風", "Majesty"),
    "UNKNOWN": ("未知", "未知", "Unknown"),
    "ONSLAUGHT": ("来袭", "襲来", "Onslaught"),
    "BOND_OF_BLOOD": ("血之羁绊", "血の絆", "Bond of Blood"),
    "RESOLVE": ("一念", "一念", "Resolve"),
    "HEROIC": ("勇壮", "勇壮", "Heroic"),
    "HYMN": ("赞歌", "賛歌", "Hymn"),
    "REMINISCENCE": ("追忆", "追憶", "Reminiscence"),
    "FINAL_WAR": ("总决战", "総大戦", "Final War"),
    "DRAGON_STS2": ("DRAGON（杀戮尖塔2版）", "DRAGON（slay the spire 2 ver.）", "DRAGON(slay the spire 2 ver.)"),
    "STORM_STS2": ("STORM（杀戮尖塔2版）", "STORM（slay the spire 2 ver.）", "STORM(slay the spire 2 ver.)"),
    "HEATS_STS2": ("HEATS（杀戮尖塔2版）", "HEATS（slay the spire 2 ver.）", "HEATS(slay the spire 2 ver.)"),
    "GETTER_ROBO_STS2": ("GETTER ROBO（杀戮尖塔2版）", "GETTER ROBO（slay the spire 2 ver.）", "GETTER ROBO(slay the spire 2 ver.)"),
    "HEATS_FINAL": ("HEATS（Final版）", "HEATS（Final ver.）", "HEATS(Final ver.)"),
    "RANDOM": ("随机", "ランダム", "Random"),
}

ALBUM_FILES = {
    "relief.mp3",
    "grief.mp3",
    "morning_on_the_tundra.mp3",
    "brutality.mp3",
    "past.mp3",
    "memory.mp3",
    "interference.mp3",
    "cold_bloodedness.mp3",
    "bond_of_blood.mp3",
    "resolve.mp3",
    "heroic.mp3",
    "hymn.mp3",
    "reminiscence.mp3",
    "dragon_sts2.mp3",
    "storm_sts2.mp3",
    "heats_sts2.mp3",
    "getter_robo_sts2.mp3",
    "heats_final.mp3",
}


def require(text: str, *needles: str) -> None:
    for needle in needles:
        if needle not in text:
            raise AssertionError(f"Missing required issue#88 assertion: {needle}")


def read_png_size(path: Path) -> tuple[int, int]:
    header = path.read_bytes()[:24]
    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n":
        raise AssertionError(f"Invalid PNG atlas: {path}")
    return struct.unpack(">II", header[16:24])


def validate_catalog_and_assets() -> None:
    catalog = CATALOG.read_text(encoding="utf-8")
    suffixes = set(re.findall(r'Track\([^\n]+,\s*"([A-Z0-9_]+)"', catalog))
    if suffixes != set(TRACK_TITLES):
        raise AssertionError(f"BGM catalog/localization mismatch: {sorted(suffixes ^ set(TRACK_TITLES))}")
    require(
        catalog,
        'internal const string RandomTrackId = "random"',
        'Track(RandomTrackId, string.Empty, "RANDOM", "Random")',
        "ResolveForPlayback(ShinGetterBgmTrack selectedTrack)",
        "Random.Shared.Next(1, Tracks.Count - 1)",
        "CanPreview(ShinGetterBgmTrack track)",
    )
    if catalog.index("Track(RandomTrackId") < catalog.index('Track("heats_final"'):
        raise AssertionError("Random must remain the final BGM dropdown option.")
    album = ROOT / "audio/music/shin_getter/album"
    actual_files = {path.name for path in album.iterdir() if path.is_file()}
    if actual_files != ALBUM_FILES:
        raise AssertionError(f"Unexpected BGM album files: {sorted(actual_files ^ ALBUM_FILES)}")
    for path in album.iterdir():
        if path.stat().st_size < 100_000:
            raise AssertionError(f"BGM asset is unexpectedly small: {path}")

    atlas = ROOT / "images/atlases/ui_atlas.png"
    if not atlas.is_file() or read_png_size(atlas) != (899, 276):
        raise AssertionError("Compact UI atlas must be exactly 899x276.")

    obsolete_assets = (
        ROOT / "images/ui/chunibyo/bgm_controls_atlas.png",
        ROOT / "images/atlases/ui_atlas_shin_getter_01.png",
        album / "dragon_sts2.ogg",
        album / "storm_sts2.ogg",
    )
    if existing := [str(path) for path in obsolete_assets if path.exists()]:
        raise AssertionError(f"Obsolete issue#88 assets still exist: {existing}")

    energy_texture = ENERGY_TEXTURE.read_text(encoding="utf-8")
    require(
        energy_texture,
        'path="res://images/atlases/ui_atlas.png"',
        "region = Rect2(828, 0, 71, 79)",
    )

    gate = RESOURCE_GATE.read_text(encoding="utf-8")
    for filename in sorted(ALBUM_FILES):
        require(gate, f'res://audio/music/shin_getter/album/{filename}')
    require(gate, "res://images/atlases/ui_atlas.png")


def validate_localization() -> None:
    tables = {
        language: json.loads(
            (LOCALIZATION_ROOT / language / "settings_ui.json").read_text(encoding="utf-8")
        )
        for language in LANGUAGES
    }
    reference_keys = set(tables[LANGUAGES[0]])
    for language in LANGUAGES[1:]:
        if set(tables[language]) != reference_keys:
            raise AssertionError(f"settings_ui localization keys differ for {language}.")

    for suffix, expected in TRACK_TITLES.items():
        key = f"SHIN_GETTER_CHUNIBYO.BGM.TRACK.{suffix}"
        actual = tuple(tables[language][key] for language in LANGUAGES)
        if actual != expected:
            raise AssertionError(f"Incorrect spreadsheet title mapping for {key}: {actual}")

    english_track_titles = (
        value
        for key, value in tables["eng"].items()
        if key.startswith("SHIN_GETTER_CHUNIBYO.BGM.TRACK.")
    )
    if any("（" in value or "）" in value for value in english_track_titles):
        raise AssertionError("English BGM titles must use half-width parentheses.")


def validate_config_and_runtime() -> None:
    config = CONFIG.read_text(encoding="utf-8")
    for field in (
        "ExecutionBgmTrackId",
        "NormalCombatBgmTrackId",
        "EventCombatBgmTrackId",
        "EliteCombatBgmTrackId",
        "BossCombatBgmTrackId",
    ):
        require(config, f"public string {field}", "ShinGetterBgmCatalog.DefaultTrackId")
    require(config, "public bool BgmForOtherCharacters", "SetBgmTrackId", "ResolveOrDefault(trackId).Id")

    encounter = ENCOUNTER.read_text(encoding="utf-8")
    require(
        encounter,
        "category = room.ParentEventId != null",
        "ShinGetterBgmCategory.EventCombat",
        "RoomType.Monster => ShinGetterBgmCategory.NormalCombat",
        "RoomType.Elite => ShinGetterBgmCategory.EliteCombat",
        "RoomType.Boss => ShinGetterBgmCategory.BossCombat",
        "configured.Id != ShinGetterBgmCatalog.DefaultTrackId",
        "ShinGetterBgmCatalog.ResolveForPlayback(configured).ResourcePath",
        "(Overgrowth, ShinGetterBgmCategory.NormalCombat)",
        "ShinGetterBgmCatalog.GetterRoboSts2TrackId",
        "(Underdocks, ShinGetterBgmCategory.NormalCombat)",
        "ShinGetterBgmCatalog.StormSts2TrackId",
        "(Hive, ShinGetterBgmCategory.NormalCombat)",
        "ShinGetterBgmCatalog.DragonSts2TrackId",
        "(Glory, ShinGetterBgmCategory.NormalCombat)",
        "ShinGetterBgmCatalog.HeatsSts2TrackId",
        "ShinGetterChunibyoConfigService.Current.BgmForOtherCharacters",
        "CurrentMapPointHistoryEntry?.MapPointType == MapPointType.Ancient",
    )
    for act in ("Overgrowth", "Underdocks", "Hive", "Glory"):
        if f"({act}, ShinGetterBgmCategory.EventCombat)" in encounter:
            raise AssertionError(f"Event-combat default must not inherit {act} encounter music.")
    execution = EXECUTION.read_text(encoding="utf-8")
    require(
        execution,
        "ShinGetterBgmCategory.Execution",
        "ShinGetterBgmCatalog.DefaultExecutionMusicPath",
        "ShinGetterBgmCatalog.ResolveForPlayback(configured)",
        "ShinGetterBgmPreviewService.EnableLoop(stream)",
    )


def validate_ui_and_preview() -> None:
    submenu = SUBMENU.read_text(encoding="utf-8")
    dropdown = DROPDOWN.read_text(encoding="utf-8")
    controls = CONTROLS.read_text(encoding="utf-8")
    preview_button = PREVIEW_BUTTON.read_text(encoding="utf-8")
    preview = PREVIEW.read_text(encoding="utf-8")
    require(
        submenu,
        'Name = "SettingsScroll"',
        "HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled",
        "options.AddChild(BuildVoiceModeRow())",
        "options.AddChild(BuildBgmSection())",
        'Name = "BgmSettingsToggle"',
        'Name = "BgmSettingsDetails"',
        "details.Visible = button.ButtonPressed",
        "BgmDropdownWidth = 400f",
        "BgmPreviewControlsWidth = 108f",
        "BgmTextColumnMinimumWidth = 620f",
        "BgmTrackControlsWidth =",
        "SettingsDropdownScenePath",
        'Name = "SettingsScrollContent"',
        'Name = "SettingsOptions"',
        'Name = category + "Controls"',
        'Name = category + "DropdownSlot"',
        "controls.AddChild(dropdownSlot)",
        "dropdown.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect)",
        "dropdownSlot.AddChild(dropdown)",
        "BgmTextColumnMinimumWidth);",
        "CustomMinimumSize = new Vector2(minimumTextWidth, 0f)",
        "child.Reparent(control, keepGlobalTransform: false)",
        "BuildBgmOtherCharactersToggle",
        "ShinGetterBgmPreviewService.Stop()",
        "arrow.Rotation = 0f",
        "arrow.Rotation = button.ButtonPressed ? -Mathf.Pi * 0.5f : 0f",
    )
    for obsolete in ("BgmDropdownLayer", "NShinGetterBgmDropdownAnchor", "anchor.Bind(dropdown)"):
        if obsolete in submenu:
            raise AssertionError(f"Obsolete overlaid dropdown-anchor architecture remains: {obsolete}")
    for temporary_marker in ("SHIN_GETTER_ISSUE88_TEST", "LogLayout"):
        for source_path in ROOT.joinpath("src").rglob("*.cs"):
            if temporary_marker in source_path.read_text(encoding="utf-8"):
                raise AssertionError(
                    f"Temporary issue#88 runtime hook remains in {source_path}: {temporary_marker}"
                )
    if submenu.index("options.AddChild(BuildVoiceModeRow())") > submenu.index("options.AddChild(BuildBgmSection())"):
        raise AssertionError("BGM settings must appear below Voice Amount.")
    require(
        dropdown,
        "NSettingsDropdown",
        "res://scenes/ui/dropdown_item.tscn",
        "NDropdownContainer>().RefreshLayout()",
        "CloseDropdown()",
        "_floatingContainer.TopLevel = _floatingContainer.Visible",
        "_dismisser.TopLevel = _floatingContainer.Visible",
        "float belowY = GlobalPosition.Y + Size.Y",
        "GlobalPosition.Y - _floatingContainer.Size.Y",
        "_floatingContainer.GlobalPosition = new Vector2(GlobalPosition.X, popupY)",
    )
    require(
        controls,
        "res://images/atlases/ui_atlas.png",
        "CreateAtlasIcon(0)",
        "CreateAtlasIcon(1)",
        "new Rect2(0f, 0f, 276f, 276f)",
        "new Rect2(276f, 0f, 276f, 276f)",
        "new NShinGetterBgmPreviewButton",
        "CanPreviewSelection(track)",
        "track.Id != ShinGetterBgmCatalog.RandomTrackId",
        "ShinGetterBgmCatalog.CanPreview(track)",
        "SetPreviewEnabled(hasPreview)",
        "SetIcon(isPlaying ? _pauseIcon : _playIcon)",
    )
    if "ResolveForPlayback(track)" in controls:
        raise AssertionError("Random BGM selection must not be resolved by the preview controls.")
    for forbidden_stop_control in ("_stopButton", '"StopButton"', "CreateAtlasIcon(2)"):
        if forbidden_stop_control in controls:
            raise AssertionError(f"Removed stop control remains: {forbidden_stop_control}")
    require(
        preview_button,
        "NShinGetterBgmPreviewButton : Button",
        "Pressed += InvokeAction",
        "MouseFilter = MouseFilterEnum.Stop",
        "Disabled = !enabled",
        "_action?.Invoke()",
        "_mouseHovered = false",
        "!Disabled && _mouseHovered",
        "Vector2.One * 1.2f",
        'TweenProperty(this, "scale", target',
    )
    if "HasFocus()" in preview_button:
        raise AssertionError("BGM preview scaling must be driven only by mouse hover.")
    require(
        preview,
        'Bus = "Master"',
        "SaveManager.Instance.SettingsSave.VolumeBgm",
        "Mathf.Pow(configuredBgmVolume, 2f)",
        "ShinGetterBgmCatalog.GetRelativeVolume(category)",
        "NAudioManager.Instance?.SetBgmVol(0f)",
        "NAudioManager.Instance?.SetBgmVol(SaveManager.Instance.SettingsSave.VolumeBgm)",
        "StreamPaused",
        "ShinGetterBgmCatalog.ResolveForPlayback(track)",
        "ResourceLoader.Exists(playbackTrack.ResourcePath)",
        "ProcessMode = Node.ProcessModeEnum.Always",
        "[ShinGetterBgmPreview] Started",
    )


def main() -> None:
    validate_catalog_and_assets()
    validate_localization()
    validate_config_and_runtime()
    validate_ui_and_preview()
    print("issue#88 static validation passed")


if __name__ == "__main__":
    main()
