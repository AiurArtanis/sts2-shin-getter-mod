#!/usr/bin/env python3
"""Positive and rejected-regression fixtures for the 2026-10-11 UI policy."""
import contextlib
import io
import validate_issue_206_feedback_20261011 as gate

ui = gate.UI.read_text(encoding='utf-8-sig')
gate.check(ui)
mutations = {
    'normal back returns': ui + '\nNBackButton',
    'hidden cancel skip returns': ui + '\nui_cancel',
    'save-failure escape removed': ui.replace(', Skip);', ', Next);'),
    'fatal escape removed': ui.replace('skip.Pressed += Skip;', 'skip.Pressed += Next;'),
    'personal choice uses gold title': ui.replace('NativeText(dialogueOption ? "" : text)', 'NativeText(text)'),
    'personal choice body lost': ui.replace('NativeText(dialogueOption ? text : "")', 'NativeText("")'),
    'personal choice flag lost': ui.replace('dialogueOption: true', 'dialogueOption: false'),
    'horizontal hover space lost': ui.replace('"margin_left", 32', '"margin_left", 0'),
    'vertical hover space lost': ui.replace('"margin_bottom", 24', '"margin_bottom", 0'),
    'inset not freed': ui.replace('inset.QueueFree()', 'inset.Hide()'),
    'inset list leaks': ui.replace('_buttonInsets.Clear()', ''),
}
for name, broken in mutations.items():
    assert broken != ui, 'Fixture did not mutate: ' + name
    with contextlib.redirect_stdout(io.StringIO()):
        try:
            gate.check(broken)
        except AssertionError:
            continue
    raise AssertionError('Regression accepted: ' + name)
print(f'issue#206 feedback gate PASS: positive + {len(mutations)} rejected mutations (not runtime)')
