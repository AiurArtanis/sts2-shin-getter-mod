#!/usr/bin/env python3
"""2026-10-11 UI source/mutation gate; no Godot, game or physical input test."""
from pathlib import Path
import argparse
import json
import re

ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / 'src/Nodes/Events/NShinGetterBondDialogue.cs'


def no_normal_skip(ui):
    for token in ('NBackButton', '_skipButton', '_skipReleased', '_UnhandledInput', 'ui_cancel'):
        assert token not in ui, f'Normal skip input remains: {token}'
    # Artanis 2026-10-19 replaces retry/skip pages with automatic legacy fallback.
    attempt = ui[ui.index('    private void Attempt('):ui.index('    private void Render()')]
    fatal = ui[ui.index('    private void ShowFatalError('):ui.index('    private void Next()')]
    assert 'if (!operation())' in attempt and 'ShowFatalError(' in attempt, 'Save failure must automatically recover'
    assert '_useLegacyDialogue = true;' in fatal and 'Close();' in fatal, 'Fatal failure must return to the original event'
    assert '_session.Skip();' not in ui and 'RestoreNativeUi();' in ui, 'Recovery must not write skipped/completed progress'


def name_only_color(ui):
    assert 'Select(selected)), dialogueOption: true)' in ui, 'Personal choices must use description formatting'
    add = ui[ui.index('    private void AddButton('):ui.index('    private void ConfigureFocus()')]
    assert 'bool dialogueOption = false' in add
    assert 'NativeText(dialogueOption ? "" : text)' in add, 'Personal choice must not inherit native gold title'
    assert 'NativeText(dialogueOption ? text : "")' in add, 'Personal choice text must use native default body color'
    for forbidden in ('AddThemeColorOverride', '[gold]', '[b]'):
        assert forbidden not in add, 'Do not override native body typography'


def hover_bounds(ui):
    add = ui[ui.index('    private void AddButton('):ui.index('    private void ConfigureFocus()')]
    assert 'new MarginContainer' in add, 'Native outline must have an unclipped layout reserve'
    for side in ('left', 'right'):
        assert f'AddThemeConstantOverride("margin_{side}", 32)' in add
    for side in ('top', 'bottom'):
        assert f'AddThemeConstantOverride("margin_{side}", 24)' in add
    assert add.index('_column.AddChild(inset)') < add.index('inset.AddChild(button)')
    clear = ui[ui.index('    private void ClearTemporaryOptions()'):ui.index('    private void ClearNativeDialogue()')]
    for token in ('button.Disable()', 'inset.GetParent()?.RemoveChild(inset)', 'inset.QueueFree()', '_buttonInsets.Clear()'):
        assert token in clear, f'Inset lifetime guard missing: {token}'


def official(root):
    source = (root / 'src/Core/Nodes/Events/NEventOptionButton.cs').read_text(encoding='utf-8-sig')
    assert '_hoverScale = Vector2.One * 1.01f' in source
    assert '_pressScale = Vector2.One * 0.99f' in source
    assert '_label.Text = formattedText2;' in source
    assert '[{value}][b]{formattedText}[/b][/{value}]' in source
    scene = (root / 'scenes/events/ancient_event_option_button.tscn').read_text(encoding='utf-8-sig')
    for token in ('Vector2(1000, 100)', 'pivot_offset = Vector2(450, 50)',
                  'offset_left = -12.0', 'offset_right = 12.0',
                  'offset_top = -63.0', 'offset_bottom = 61.0',
                  'scale = Vector2(1.005, 1.05)', 'pivot_offset = Vector2(400, 50)'):
        assert token in scene, f'Re-audit native hover geometry: {token}'
    # Native outline, then its parent button scale, with the actual scene pivots.
    # Reserve contains the whole outline at idle/press/hover, not a clipped label.
    for scale in (0.99, 1.0, 1.01):
        left = (400 + (-12 - 400) * 1.005 - 450) * scale + 450
        right = (400 + (1012 - 400) * 1.005 - 450) * scale + 450
        top = (50 + (-13 - 50) * 1.05 - 50) * scale + 50
        bottom = (50 + (111 - 50) * 1.05 - 50) * scale + 50
        assert left >= -32 and right <= 1032 and top >= -24 and bottom <= 124
    print('official109 native title/outline probes PASS (not rendered acceptance)')


def catalogue():
    colors = {'RYOMA': 'red', 'HAYATO': 'white', 'BENKEI': 'yellow'}
    base = json.loads((ROOT / 'data/dialogues/zhs.json').read_text(encoding='utf-8-sig'))
    drivers = {row['Id']: row['Driver'] for row in base if row.get('Option')}
    for lang in ('zhs', 'eng', 'jpn'):
        rows = json.loads((ROOT / f'data/dialogues/{lang}.json').read_text(encoding='utf-8-sig'))
        choices = [row for row in rows if row.get('Option')]
        assert len(choices) == 63, (lang, 'Expected all personal dialogue choices')
        for row in choices:
            color = colors[drivers[row['Id']]]
            assert re.fullmatch(rf'\[{color}\][^\[\]\r\n]+\[/{color}\][:：].+', row['Option']), (lang, row['Id'])
            assert not re.search(r'\[/?(?:red|white|yellow|gold|color)(?:\]|=)', row['Option'].split(f'[/{color}]', 1)[1]), (lang, row['Id'], 'Body color override')
    print('All 189 three-language personal choice name-only color probes PASS')


def check(ui=None):
    ui = UI.read_text(encoding='utf-8-sig') if ui is None else ui
    failed = []
    for probe in (no_normal_skip, name_only_color, hover_bounds):
        try:
            probe(ui)
            print(probe.__name__, 'PASS')
        except AssertionError as error:
            failed.append(str(error))
            print(probe.__name__, 'RED:', error)
    assert not failed, '; '.join(failed)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path)
    args = parser.parse_args()
    check()
    catalogue()
    if args.source_root:
        official(args.source_root)
