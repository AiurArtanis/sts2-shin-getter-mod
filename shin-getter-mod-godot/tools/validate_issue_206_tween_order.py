#!/usr/bin/env python3
"""Production order contract paired with the real Godot linked-source API probe."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def check(ui=None, guard=None):
    ui = ui if ui is not None else (ROOT / 'src/Nodes/Events/NShinGetterBondDialogue.cs').read_text(encoding='utf-8-sig')
    guard = guard if guard is not None else (ROOT / 'src/Nodes/Events/ShinGetterContentTweenGuard.cs').read_text(encoding='utf-8-sig')
    recenter = ui[ui.index('private static void RecenterNativeContent('):ui.index('private void AddButton(')]
    stop = 'ShinGetterContentTweenGuard.Stop(ref ContentTween(layout));'
    assert stop in recenter, 'RED: native full-position tween must be stopped AND field cleared before recenter'
    assert recenter.index(stop) < recenter.index('var viewport') < recenter.index('content.Position ='), 'Stop must precede measurement and new X'
    reading = ui[ui.index('private void RenderAncient('):ui.index('private void RenderArchitect(')]
    rewards = ui[ui.index('internal static void RestoreRewardPage('):ui.index('private static void RecenterNativeContent(')]
    assert reading.index('RecenterNativeContent(_ancient!);') < reading.index('AnimateLine.Invoke(')
    assert rewards.index('RecenterNativeContent(layout);') < rewards.index('AnimateLine.Invoke(')
    assert reading.count('AnimateLine.Invoke(') == 1 and rewards.count('AnimateLine.Invoke(') == 1
    assert 'internal static void Stop(ref Tween? tween)' in guard
    assert guard.index('tween.Kill();') < guard.index('tween = null;')
    assert 'GodotObject.IsInstanceValid(tween)' in guard
    assert 'CustomStep(' not in guard, 'Never fast-forward stale focus callbacks'
    fixture = (ROOT.parent / 'tests/Issue206NativeTween/Issue206NativeTween.csproj').read_text(encoding='utf-8-sig')
    assert 'Compile Include="../../shin-getter-mod-godot/src/Nodes/Events/ShinGetterContentTweenGuard.cs"' in fixture
    assert 'sts2' not in fixture and 'Harmony' not in fixture
    print('issue#206 production Stop -> recenter -> native animation contracts PASS (API behavior checked separately)')


if __name__ == '__main__':
    check()
