#!/usr/bin/env python3
"""Static regression gate for issue#7 Chunibyo configuration UI."""

from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SUBMENU_PATH = ROOT / "src/Nodes/Config/NChunibyoConfigSubmenu.cs"
UPDATE_HISTORY_POPUP_PATH = ROOT / "src/Nodes/Config/NChunibyoUpdateHistoryPopup.cs"
PAGINATOR_PATH = ROOT / "src/Nodes/Config/NShinGetterVoicePaginator.cs"
ACTION_BUTTON_PATH = ROOT / "src/Nodes/Config/NShinGetterConfigActionButton.cs"
RICH_TEXT_PATCH_PATH = ROOT / "src/Patches/RichTextWhitePatch.cs"
UPDATE_HISTORY_PATH = ROOT / "ShinGetterMod/update_history.json"
LOCALIZATION_ROOT = ROOT / "ShinGetterMod/localization"
LANGUAGES = ("eng", "jpn", "zhs")


def require(text: str, *needles: str) -> None:
    for needle in needles:
        if needle not in text:
            raise AssertionError(f"Missing required issue#7 assertion: {needle}")


def validate_update_history() -> None:
    submenu = SUBMENU_PATH.read_text(encoding="utf-8")
    popup = UPDATE_HISTORY_POPUP_PATH.read_text(encoding="utf-8")
    entries = json.loads(UPDATE_HISTORY_PATH.read_text(encoding="utf-8"))
    history_popup = submenu.split(
        "private bool ShowUpdateHistoryPopup", 1
    )[1].split("private static string Localize", 1)[0]
    require(
        submenu,
        "private bool ShowUpdateHistoryPopup(string title, string body)",
        "Control? returnFocus = GetViewport().GuiGetFocusOwner()",
        "NChunibyoUpdateHistoryPopup popup = NChunibyoUpdateHistoryPopup.Create(title, body, returnFocus)",
        "modalContainer.Add(popup)",
        "popup.IsInsideTree() && popup.IsVisibleInTree()",
    )
    require(
        popup,
        "public partial class NChunibyoUpdateHistoryPopup : Control, IScreenContext",
        "private const float PopupWidth = 1120f",
        "private const float PopupHeight = 820f",
        "private const float BodyMinimumWidth = 956f",
        'Name = "HistoryScroll"',
        "HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled",
        "VerticalScrollMode = ScrollContainer.ScrollMode.ShowAlways",
        'Name = "HistoryTextMargin"',
        "CustomMinimumSize = new Vector2(BodyMinimumWidth, 0f)",
        'Name = "HistoryText"',
        "BbcodeEnabled = true",
        "FitContent = true",
        "ScrollActive = false",
        "AutowrapMode = TextServer.AutowrapMode.WordSmart",
        "SizeFlagsHorizontal = SizeFlags.ExpandFill",
        "VScrollBar scrollBar = _scrollContainer.GetVScrollBar()",
        'scrollBar.Name = "HistoryScrollbar"',
        "scrollBar.CustomMinimumSize = new Vector2(26f, 0f)",
        "scrollBar.MouseFilter = MouseFilterEnum.Stop",
        'inputEvent.IsActionPressed("ui_page_down")',
        'inputEvent.IsActionPressed("ui_page_up")',
        'inputEvent.IsActionPressed("ui_down")',
        'inputEvent.IsActionPressed("ui_up")',
        "NModalContainer.Instance?.Clear()",
        "returnFocus.CallDeferred(Control.MethodName.GrabFocus)",
    )
    for obsolete_contract in (
        "PatchScreenContentsScenePath",
        "patch_screen_contents.tscn",
        "ConfigureUpdateHistoryPopup",
        "NScrollableContainer",
        "NScrollbar",
        "description.Reparent",
        "content.ResetSize()",
        "scroll.SetContent(content)",
    ):
        if obsolete_contract in submenu + popup:
            raise AssertionError(
                f"Obsolete update-history layout contract remains: {obsolete_contract}"
            )
    if "NErrorPopup.Create" in history_popup or "CallDeferred" in history_popup:
        raise AssertionError(
            "Update history must open its dedicated modal without deferred popup surgery."
        )

    versions = {entry["version"]: entry for entry in entries}
    if len(versions) != len(entries):
        raise AssertionError("Update history versions must be unique.")
    expected_entries = {
        "v1.1.0": ("2026-08-02", "SHIN_GETTER_CHUNIBYO.UPDATE.v1_1_0"),
        "v1.0.7": ("2026-07-21", "SHIN_GETTER_CHUNIBYO.UPDATE.v1_0_7"),
    }
    for version, (date, key) in expected_entries.items():
        entry = versions.get(version)
        if entry is None or entry["date"] != date or entry["localization_key"] != key:
            raise AssertionError(f"Incorrect update history metadata for {version}: {entry}")

    history_keys = {entry["localization_key"] for entry in entries}
    localized_tables: dict[str, dict[str, str]] = {}
    for language in LANGUAGES:
        path = LOCALIZATION_ROOT / language / "settings_ui.json"
        table = json.loads(path.read_text(encoding="utf-8"))
        localized_tables[language] = table
        missing = history_keys - table.keys()
        if missing:
            raise AssertionError(f"Missing update history localization for {language}: {sorted(missing)}")

        latest = table["SHIN_GETTER_CHUNIBYO.UPDATE.v1_1_0"]
        if len(latest.splitlines()) < 15 or latest.count("- ") < 10:
            raise AssertionError(
                f"v1.1.0 history must remain long enough to exercise scrolling for {language}."
            )

        sorted_entries = sorted(
            entries,
            key=lambda entry: (entry["date"], entry["version"]),
            reverse=True,
        )
        rendered = "\n\n".join(
            f'{entry["version"]}  {entry["date"]}\n{table[entry["localization_key"]]}'
            for entry in sorted_entries
        )
        if len(rendered.splitlines()) < 24:
            raise AssertionError(
                f"Rendered update history must overflow the popup and exercise scrolling for {language}."
            )

    reference_keys = set(localized_tables[LANGUAGES[0]])
    for language in LANGUAGES[1:]:
        if set(localized_tables[language]) != reference_keys:
            raise AssertionError(f"settings_ui localization keys differ for {language}.")


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
        "SilentMaxFontSize = 25",
        "DefaultMaxFontSize = 28",
        "AlwaysMaxFontSize = 35",
        "_richPresentationLabel.MaxFontSize = _currentIndex switch",
        "0 => SilentMaxFontSize",
        "2 => AlwaysMaxFontSize",
        "_ => DefaultMaxFontSize",
        "_richPresentationLabel.SetTextAutoSize(_options[_currentIndex])",
        "IsHorizontallyBound = true",
        "IsVerticallyBound = true",
        "AutowrapMode = TextServer.AutowrapMode.Off",
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
