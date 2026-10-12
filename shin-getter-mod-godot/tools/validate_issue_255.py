#!/usr/bin/env python3
"""Bounded render-preparation source contract; native GPU evidence is separate."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SERVICE = ROOT / 'src/Services/ShinGetterCombatRenderPreparation.cs'
PATCH = ROOT / 'src/Patches/ShinGetterCombatRenderPreparationPatch.cs'

def validate(service: str, patch: str) -> None:
    assert 'ConditionalWeakTable<IRunState, Preparation>' in service, 'weak run ownership'
    assert 'TestMode.IsOn' in service and 'DisplayServer.GetName() == "headless"' in service, 'non-rendering gate'
    assert 'p.Character is ShinGetter' in service, 'getter run isolation'
    assert 'RenderTargetUpdateMode = SubViewport.UpdateMode.Always' in service, 'must actually render'
    assert 'UseHdr2D = mainViewport.UseHdr2D' in service and 'Msaa2D = mainViewport.Msaa2D' in service, 'match render format'
    assert 'RenderingServer.FramePostDraw +=' in service and 'RenderingServer.FramePostDraw -=' in service, 'draw subscription cleanup'
    assert 'TreeExiting +=' in service and 'TreeExiting -=' in service, 'cancel on node exit'
    assert 'template.Free();' in service and 'host.QueueFree();' in service, 'node ownership cleanup'
    assert 'source.Duplicate(0)' in service and 'particle.ProcessMaterial = source.ProcessMaterial' in service, 'no native Ready or material replacement'
    assert 'source.Visible' in service, 'unused hidden variants excluded'
    assert 'await original;' in patch and 'await ShinGetterCombatRenderPreparation.PrepareAsync(runState);' in patch, 'await original load then prepare'
    assert 'LoadRoomCombatAssets' in patch and 'ref Task __result' in patch, 'formal109 target'
    assert len(re.findall(r'"res://[^"\n]+\.tscn"', service)) == 3, 'fixed three-scene budget'
    assert not re.search(r'Create\(|PowerCmd|CreatureCmd|SettingsSave|\.Save\(|CacheLimit|CacheCapacity|WindowSetVsync|MaxFps|QueueFreeSafely', service), 'no gameplay/settings/cache/render-mode mutation'

def main() -> None:
    assert SERVICE.is_file() and PATCH.is_file(), 'render preparation production files missing (RED)'
    service, patch = SERVICE.read_text(encoding='utf-8'), PATCH.read_text(encoding='utf-8')
    validate(service, patch)
    mutations = [
        (service.replace('ConditionalWeakTable<IRunState, Preparation>', 'Dictionary<IRunState, Preparation>'), patch),
        (service.replace('TestMode.IsOn', 'false'), patch),
        (service.replace('DisplayServer.GetName() == "headless"', 'false'), patch),
        (service.replace('p.Character is ShinGetter', 'true'), patch),
        (service.replace('RenderingServer.FramePostDraw -=', 'RenderingServer.FramePreDraw -='), patch),
        (service.replace('template.Free();', '// orphan'), patch),
        (service.replace('host.QueueFree();', '// leak'), patch),
        (service, patch.replace('await original;', '// ignored task')),
    ]
    for index, (bad_service, bad_patch) in enumerate(mutations, 1):
        try:
            validate(bad_service, bad_patch)
        except AssertionError:
            continue
        raise AssertionError(f'mutation {index} was accepted')
    print('issue#255 source contract PASS; 8 negative mutations rejected; GPU/runtime acceptance remains separate')

if __name__ == '__main__':
    main()
