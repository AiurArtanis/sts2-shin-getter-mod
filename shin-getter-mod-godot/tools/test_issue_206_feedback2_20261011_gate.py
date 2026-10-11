#!/usr/bin/env python3
"""Reject source regressions; not a rendered UI or physical input fixture."""
import contextlib
import io
import validate_issue_206_feedback2_20261011 as gate

ui = (gate.ROOT / 'src/Nodes/Events/NShinGetterBondDialogue.cs').read_text(encoding='utf-8-sig')
bridge = (gate.ROOT / 'src/Patches/ShinGetterBondDialoguePatch.cs').read_text(encoding='utf-8-sig')
session = (gate.ROOT / 'src/Services/ShinGetterBondSession.cs').read_text(encoding='utf-8-sig')
gate.check(ui, bridge, session)
cases = {
    'finish option returns': (ui + 'Finish conversation', bridge, session),
    'completion before display': (ui, bridge, session.replace('WasShown: true', 'WasShown: false')),
    'tail includes full history': (ui.replace('.TakeLast(2)', '.ToArray()'), bridge, session),
    'reward layout not restored': (ui, bridge.replace('RestoreRewardPage(ancient, state.Session)', 'RestoreOldPage(ancient, state.Session)'), session),
    'viewport not recentered': (ui.replace('viewport.OffsetLeft = -width / 2', 'viewport.OffsetLeft = 0'), bridge, session),
    'content not recentered': (ui.replace('(viewport.Size.X - content.Size.X) / 2', 'content.Position.X'), bridge, session),
    'NPC sound gate lost': (ui.replace('_finishingDisplayedDialogue', '_unconditionalSound'), bridge, session),
    'cold attach depends on SetDialogue': (ui, bridge.replace('State state = ShinGetterBondDialogueBridge.Get(model);', 'var state = new object();'), session),
}
for name, values in cases.items():
    with contextlib.redirect_stdout(io.StringIO()):
        try:
            gate.check(*values)
        except AssertionError:
            continue
    raise AssertionError('Regression accepted: ' + name)
print(f'issue#206 feedback2 gate PASS: positive + {len(cases)} rejected regressions (not runtime)')
