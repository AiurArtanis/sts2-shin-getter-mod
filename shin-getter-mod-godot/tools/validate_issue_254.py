#!/usr/bin/env python3
"""Input compatibility contracts; not GPU or physical controller acceptance."""
from pathlib import Path
import argparse

ROOT = Path(__file__).resolve().parents[1]


def check():
    policy = (ROOT / 'src/Services/ShinGetterControllerMappingPolicy.cs').read_text(encoding='utf-8-sig')
    patch = (ROOT / 'src/Patches/ShinGetterControllerMappingCompatibilityPatch.cs').read_text(encoding='utf-8-sig')
    for token in ('new Dictionary<T, T>(source)', 'keyValid && hasAction(pair.Value)', 'defaults.TryGetValue', 'hasAction(fallback)', 'result.Remove', 'return result ?? source'):
        assert token in policy, token
    for token in ('_controllerInputMap', 'ConditionalWeakTable', 'ReferenceEquals(map, state.Normalized)', 'InputMap.HasAction', '_UnhandledInput', 'GetHotkeyIcon', 'ModifyControllerButton', 'Invalidate(__instance)'):
        assert token in patch, token
    for forbidden in ('SaveSettings', 'SaveControllerInputMapping', 'SettingsSave', 'File.', 'ResetToDefaults', 'InputMap.AddAction', 'InputMap.EraseAction'):
        assert forbidden not in patch + policy, 'No saved-input reset or fake alias: ' + forbidden
    assert 'RuntimeMap(manager) = normalized' in patch and 'state.Normalized = normalized' in patch
    print('issue#254 input compatibility contracts PASS (no GPU/physical input claim)')


def official(root):
    source = (root / 'src/Core/Nodes/CommonUi/NInputManager.cs').read_text(encoding='utf-8-sig')
    for token in ('private Dictionary<StringName, StringName> _controllerInputMap', 'public override void _UnhandledInput(InputEvent inputEvent)', 'public Texture2D? GetHotkeyIcon(string hotkey)', 'public void ModifyControllerButton(StringName input, StringName controllerInput)'):
        assert token in source, token
    print('NInputManager Harmony/FieldRef signatures present')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path)
    args = parser.parse_args()
    check()
    if args.source_root:
        official(args.source_root)
