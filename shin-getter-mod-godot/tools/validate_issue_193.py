#!/usr/bin/env python3
"""Read-only source contracts for issue#193. Does not launch the game.

Written but NOT RUN for the 2026-09-20 delivery: automated testing is on hold.
These textual contracts are not runtime/audio/focus acceptance evidence.
"""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8-sig")


def require(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError(message)


def main() -> None:
    config = read("src/Config/ShinGetterChunibyoConfigService.cs")
    ui = read("src/Nodes/Config/NChunibyoConfigSubmenu.cs")
    encounter = read("src/Audio/ShinGetterEncounterMusicService.cs")
    execution = read("src/Audio/ShinGetterExecutionMusicService.cs")
    preview = read("src/Audio/ShinGetterBgmPreviewService.cs")
    require("public bool BgmEnabled { get; set; } = true;" in config,
            "Missing enabled-by-default field for old JSON configs")
    setter = config.split("internal static bool TrySetBgmEnabled", 1)[1].split(
        "internal static bool MarkCurrentUpdateRead", 1)[0]
    require(setter.index("if (!Save(out error))") < setter.index("Current.BgmEnabled = previous;")
            < setter.index("ShinGetterBgmPreviewService.Stop();")
            < setter.index("BgmEnabledChanged?.Invoke();"), "Save/rollback/apply/broadcast order")
    for stop in ("ShinGetterBgmPreviewService.Stop();",
                 "ShinGetterEncounterMusicService.StopActiveAndRestore();",
                 "ShinGetterExecutionMusicService.StopImmediatelyAndRestore();"):
        require(stop in setter, f"All BGM consumers must stop on disable: {stop}")
    for field in ("ExecutionBgmTrackId", "NormalCombatBgmTrackId", "EventCombatBgmTrackId",
                  "EliteCombatBgmTrackId", "BossCombatBgmTrackId", "BgmForOtherCharacters", "VoiceMode"):
        require(f"Current.{field} =" not in setter, f"Master switch must preserve {field}")
    require(encounter.index("!ShinGetterChunibyoConfigService.IsBgmEnabled")
            < encounter.index("!ShouldReplaceForLocalPlayer") < encounter.index("!TryResolveTrack"),
            "Off must gate every character/category before track selection")
    trigger = execution.split("private static void TryStart(", 1)[1].split(
        "internal static async Task StopAndRestore", 1)[0]
    require(trigger.index("!ShinGetterChunibyoConfigService.IsBgmEnabled")
            < trigger.index("state.HasTriggered = true"), "Off must not consume finisher one-shot")
    playback = execution.split("private static void StartPlayback", 1)[1]
    require(playback.index("!ShinGetterChunibyoConfigService.IsBgmEnabled")
            < playback.index("ResolveForPlayback"), "Off must not draw/load execution tracks")
    toggle = preview.split("internal static void Toggle", 1)[1].split("internal static void Stop", 1)[0]
    require(toggle.index("!ShinGetterChunibyoConfigService.IsBgmEnabled")
            < toggle.index("_player.StreamPaused"), "Paused previews must not bypass the switch")
    start = preview.split("private static void Start(", 1)[1]
    require(start.index("!ShinGetterChunibyoConfigService.IsBgmEnabled")
            < start.index("ResolveForPlayback"), "Off must not draw/load preview tracks")
    for audio in (encounter, execution, preview):
        require(".Stop();" in audio and "QueueFree();" in audio, "Stop before freeing audio")
        require("SettingsSave.VolumeBgm =" not in audio and "SetBusMute" not in audio,
                "Do not rewrite native settings or mute shared audio buses")
    require("state.FadeTween?.Kill();" in execution and "state.StopCompletion?.TrySetResult(true)" in execution,
            "Disabling during execution fade must release pending waiters")
    for token in ('Name = "BgmMasterRow"', 'Name = "BgmEnabledTickbox"',
                  "CreateOriginalTickbox(", "_bgmHeaderButton.Disabled = !enabled",
                  "_bgmHeaderButton.SetPressedNoSignal(false)", "_bgmDetails.Hide()",
                  "enabled ? FocusModeEnum.All : FocusModeEnum.None",
                  "_bgmEnabledTickbox.CallDeferred(Control.MethodName.GrabFocus)",
                  "BgmEnabledChanged += RefreshBgmEnabledState", "BgmEnabledChanged -= RefreshBgmEnabledState",
                  "if (_interfaceBuilt) return;", "_bgmEnabledTickbox.IsTicked = enabled"):
        require(token in ui, f"Missing master UI/input/lifecycle boundary: {token}")
    header = ui.split("private Control CreateBgmSectionHeader", 1)[1].split("private void OnBgmEnabledToggled", 1)[0]
    require(header.index("row.AddChild(_bgmEnabledTickbox)") < header.index("row.AddChild(button)"),
            "Checkbox must precede existing expandable header")
    require(header.index("!ShinGetterChunibyoConfigService.IsBgmEnabled")
            < header.index("details.Visible = button.ButtonPressed"), "Expansion must have a logical guard")
    tables = [json.loads(read(f"ShinGetterMod/localization/{lang}/settings_ui.json"))
              for lang in ("zhs", "eng", "jpn")]
    for table in tables:
        require(set(table) == set(tables[0]), "Three-language settings keys differ")
        for suffix in ("ENABLED_TOOLTIP", "DISABLED_TOOLTIP"):
            require(bool(table.get("SHIN_GETTER_CHUNIBYO.BGM." + suffix)), "Missing master-switch help")
    print("issue#193 source contracts PASS (not runtime acceptance)")


if __name__ == "__main__":
    main()
