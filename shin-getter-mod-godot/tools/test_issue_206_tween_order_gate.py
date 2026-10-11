#!/usr/bin/env python3
import contextlib
import io
import validate_issue_206_tween_order as gate

ui = (gate.ROOT / 'src/Nodes/Events/NShinGetterBondDialogue.cs').read_text(encoding='utf-8-sig')
guard = (gate.ROOT / 'src/Nodes/Events/ShinGetterContentTweenGuard.cs').read_text(encoding='utf-8-sig')
gate.check(ui, guard)
stop = '        ShinGetterContentTweenGuard.Stop(ref ContentTween(layout));\n'
late = ui.replace(stop, '').replace('        float width = Math.Max(1160, content.Size.X + 64);', stop + '        float width = Math.Max(1160, content.Size.X + 64);')
cases = {
    'guard omitted': (ui.replace(stop, ''), guard),
    'guard after measurement': (late, guard),
    'old field not cleared': (ui, guard.replace('tween = null;', '')),
    'old tween not killed': (ui, guard.replace('tween.Kill();', 'tween.Pause();')),
    'stale callback fast-forward': (ui, guard.replace('tween.Kill();', 'tween.CustomStep(1.0); tween.Kill();')),
    'reading starts next tween before recenter': (ui.replace('RecenterNativeContent(_ancient!);\n                AnimateLine.Invoke(_ancient, new object[] { current });', 'AnimateLine.Invoke(_ancient, new object[] { current });\n                RecenterNativeContent(_ancient!);'), guard),
}
for name, args in cases.items():
    assert args != (ui, guard), name + ': fixture did not mutate'
    with contextlib.redirect_stdout(io.StringIO()):
        try:
            gate.check(*args)
        except (AssertionError, ValueError):
            continue
    raise AssertionError('Regression accepted: ' + name)
print(f'issue#206 tween order gate PASS: positive + {len(cases)} rejected regressions')
