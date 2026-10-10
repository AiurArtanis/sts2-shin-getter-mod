#!/usr/bin/env python3
"""Mutation fixtures for native UI wiring checks; no files changed, no game launched."""
import contextlib
import io
from pathlib import Path
from unittest.mock import patch
import validate_issue_206_native_ui as gate

ui_path = gate.ROOT / 'src/Nodes/Events/NShinGetterBondDialogue.cs'
bridge_path = gate.ROOT / 'src/Patches/ShinGetterBondDialoguePatch.cs'
original_read = Path.read_text
ui = original_read(ui_path, encoding='utf-8-sig')
bridge = original_read(bridge_path, encoding='utf-8-sig')
gate.check()
fixtures = {
    'custom panel returns': (ui + '\nPanelContainer', bridge),
    'no queued-button protection': (ui.replace('button.IsQueuedForDeletion()', 'true'), bridge),
    'no enabled protection': (ui.replace('!button.IsEnabled', 'false'), bridge),
    'no scene active protection': (ui.replace('!IsEventActive', 'false'), bridge),
    'reward selection instead of local routing': (ui, bridge.replace('ui.TryChooseOption(option)', 'ui.ChooseLocalOption(option)')),
    'native content hidden': (ui, bridge + '\noldContent?.Hide()'),
    'completion does not release native UI': (ui.replace('        RestoreNativeUi();\n        Hide();', '        Hide();'), bridge),
    'skip hotkeys not disabled': (ui.replace('_skipButton.Disable()', '_skipButton.Hide()'), bridge),
}
for name, (bad_ui, bad_bridge) in fixtures.items():
    def read(path, *args, **kwargs):
        if path == ui_path:
            return bad_ui
        if path == bridge_path:
            return bad_bridge
        return original_read(path, *args, **kwargs)
    with patch.object(Path, 'read_text', read), contextlib.redirect_stdout(io.StringIO()):
        try:
            gate.check()
        except AssertionError:
            continue
    raise AssertionError('Invalid mutation accepted: ' + name)
print(f'issue#206 native UI gate fixtures PASS: positive + {len(fixtures)} rejected mutations (not runtime)')
