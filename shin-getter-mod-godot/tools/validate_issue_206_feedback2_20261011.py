#!/usr/bin/env python3
"""Feedback ② contract gate. No game process, player save or visual PASS claim."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def check(ui=None, bridge=None, session=None):
    ui = ui if ui is not None else (ROOT / 'src/Nodes/Events/NShinGetterBondDialogue.cs').read_text(encoding='utf-8-sig')
    bridge = bridge if bridge is not None else (ROOT / 'src/Patches/ShinGetterBondDialoguePatch.cs').read_text(encoding='utf-8-sig')
    session = session if session is not None else (ROOT / 'src/Services/ShinGetterBondSession.cs').read_text(encoding='utf-8-sig')
    checks = {
        'no_finish_button': lambda: 'Finish conversation' not in ui and '结束交谈' not in ui,
        'automatic_saved_completion': lambda: 'CompleteDisplayedFinalLine' in ui and 'CompleteDisplayedFinalLine' in session and '_completionWait' in ui,
        'retained_two_line_tail': lambda: 'RestoreRewardPage' in ui and '.TakeLast(2)' in ui and 'RestoreRewardPage(ancient, state.Session)' in bridge,
        'recenter_reading_and_rewards': lambda: 'RecenterNativeContent(_ancient' in ui and 'RecenterNativeContent(layout)' in ui and 'viewport.OffsetLeft = -width / 2' in ui and '(viewport.Size.X - content.Size.X) / 2' in ui,
        'late_native_sound': lambda: 'ResolveFinalAncientSfx' in bridge and '_finishingDisplayedDialogue' in ui and 'SfxCmd.Play(_finalAncientSfx)' in ui,
        'cold_resume_attach': lambda: 'if (!ShinGetterBondSession.IsEligible(model) || model.IsFinished) return true;' in bridge and 'State state = ShinGetterBondDialogueBridge.Get(model);' in bridge,
    }
    failed = []
    for name, probe in checks.items():
        passed = probe()
        print(name, 'PASS' if passed else 'RED')
        if not passed:
            failed.append(name)
    assert not failed, ', '.join(failed)
    completion = session[session.index('internal bool CompleteDisplayedFinalLine()'):session.index('internal bool ConsumeCue')]
    assert 'WasShown: true' in completion and 'return Advance();' in completion
    assert 'current == lines.Length - 1' in ui and '_retry == null' in ui and 'IsEventActive' in ui
    assert ui.index('_session.CompleteDisplayedFinalLine()') < ui.index('_finishingDisplayedDialogue = true')
    assert 'SfxCmd.Play(_finalAncientSfx)' in ui[ui.index('private void Close()'):ui.index('public override void _ExitTree()')]
    assert 'new MarginContainer' in ui and 'NBackButton' not in ui and 'ui_cancel' not in ui
    assert 'options.SequenceEqual(layout.OptionButtons)' in ui, 'Stale reward layout callback must not overwrite a new event page'
    assert 'line.SetSpeakerIconVisible();' in ui, 'Both retained native lines need their native speaker icons'
    for content_width in (1000, 1064, 1160, 1328, 1600):
        viewport_width = max(1160, content_width + 64)
        x = (viewport_width - content_width) / 2
        assert x >= 32 and viewport_width - x - content_width >= 32
    print('issue#206 feedback2 source contracts and clipping arithmetic PASS (not rendered/input acceptance)')


if __name__ == '__main__':
    check()
