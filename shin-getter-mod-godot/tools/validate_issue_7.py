#!/usr/bin/env python3
"""Static regression gate for issue#7 Chunibyo configuration UI."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SUBMENU_PATH = ROOT / "src/Nodes/Config/NChunibyoConfigSubmenu.cs"
PAGINATOR_PATH = ROOT / "src/Nodes/Config/NShinGetterVoicePaginator.cs"
ACTION_BUTTON_PATH = ROOT / "src/Nodes/Config/NShinGetterConfigActionButton.cs"
RICH_TEXT_PATCH_PATH = ROOT / "src/Patches/RichTextWhitePatch.cs"
LOCALIZATION_ROOT = ROOT / "ShinGetterMod/localization"
LANGUAGES = ("eng", "jpn", "zhs")


def require(text: str, *needles: str) -> None:
    for needle in needles:
        if needle not in text:
            raise AssertionError(f"Missing required issue#7 assertion: {needle}")


def validate_update_history() -> None:
    submenu = SUBMENU_PATH.read_text(encoding="utf-8")
    history_popup = submenu.split(
        "private static void ConfigureUpdateHistoryPopup", 1
    )[1].split("private static string Localize", 1)[0]
    require(
        submenu,
        'private const string ScrollbarScenePath = "res://scenes/ui/scrollbar.tscn";',
        "var scroll = new NScrollableContainer",
        'Name = "UpdateHistoryScroll"',
        'description.Name = "Content"',
        'MegaRichTextLabel description = popup.GetNode<MegaRichTextLabel>',
        "description.AutoSizeEnabled = false",
        "description.MinFontSize = NoteFontSize",
        "description.MaxFontSize = NoteFontSize",
        "description.AddThemeFontSizeOverride(themeKey, NoteFontSize)",
        "Instantiate<NScrollbar>()",
        'scrollbar.Name = "Scrollbar"',
        "scroll.SetContent(description)",
        "description.ResetSize()",
        "scroll.InstantlyScrollToTop()",
    )
    if "var scroll = new ScrollContainer" in history_popup:
        raise AssertionError("Update history must use the original NScrollableContainer stack.")


def validate_hover_tips() -> None:
    paginator = PAGINATOR_PATH.read_text(encoding="utf-8")
    action_button = ACTION_BUTTON_PATH.read_text(encoding="utf-8")
    require(
        paginator,
        "MegaRichTextLabel",
        'Name = "VoiceOptionHoverBounds"',
        "MouseFilter = MouseFilterEnum.Stop",
        "HoverTip.GetHoverTipAlignment(_hoverBounds)",
        "NHoverTipSet.Remove(_hoverBounds)",
    )
    require(
        action_button,
        "HoverTip.GetHoverTipAlignment(this)",
        "NHoverTipSet.Remove(this)",
    )
    if "NSettingsScreen.settingTipsOffset" in paginator + action_button:
        raise AssertionError("Custom controls must not reuse the settings screen fixed offset.")


def validate_voice_markup() -> None:
    submenu = SUBMENU_PATH.read_text(encoding="utf-8")
    paginator = PAGINATOR_PATH.read_text(encoding="utf-8")
    rich_text_patch = RICH_TEXT_PATCH_PATH.read_text(encoding="utf-8")
    require(
        paginator,
        "_richPresentationLabel.MaxFontSize = _currentIndex == 2 ? 30 : 28",
        "_richPresentationLabel.SetTextAutoSize(_options[_currentIndex])",
        'GetNode<Control>("LeftArrow").MoveToFront()',
        'GetNode<Control>("RightArrow").MoveToFront()',
        "BbcodeEnabled = true",
    )
    require(
        submenu,
        "silent,",
        "StripVoicePresentationTags(silent)",
    )
    require(
        rich_text_patch,
        "__instance.CustomEffects.Add(WhiteEffect)",
    )
    if "NormalizeVoiceMarkup" in submenu or '.Replace("[white]", "[color=white]"' in submenu:
        raise AssertionError("The registered [white] tag must not be rewritten to [color=white].")

    for language in LANGUAGES:
        path = LOCALIZATION_ROOT / language / "settings_ui.json"
        table = json.loads(path.read_text(encoding="utf-8"))
        silent = table["SHIN_GETTER_CHUNIBYO.VOICE.SILENT"]
        if not silent.startswith("[white]") or not silent.endswith("[/white]"):
            raise AssertionError(
                f"Silent voice markup must use the registered white tag for {language}: {silent}"
            )
        value = table["SHIN_GETTER_CHUNIBYO.VOICE.ALWAYS"]
        if not (
            value.startswith("[red][sine]")
            and value.endswith("[/sine][/red]")
        ):
            raise AssertionError(
                f"Always voice markup is not nested red/sine for {language}: {value}"
            )


def main() -> None:
    validate_update_history()
    validate_hover_tips()
    validate_voice_markup()
    print("issue#7 static validation passed")


if __name__ == "__main__":
    main()
