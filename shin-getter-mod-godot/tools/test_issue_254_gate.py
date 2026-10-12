#!/usr/bin/env python3
"""Source mutation fixtures, no rendering/physical-input claim."""
from pathlib import Path
from unittest.mock import patch
import contextlib
import io
import validate_issue_254 as gate

policy_path = gate.ROOT / 'src/Services/ShinGetterControllerMappingPolicy.cs'
patch_path = gate.ROOT / 'src/Patches/ShinGetterControllerMappingCompatibilityPatch.cs'
read = Path.read_text
policy = read(policy_path, encoding='utf-8-sig')
source = read(patch_path, encoding='utf-8-sig')
gate.check()
cases = {
    'invalid value check removed': (policy.replace('keyValid && hasAction(pair.Value)', 'keyValid'), source),
    'invalid fallback consumed': (policy.replace('hasAction(fallback)', 'true'), source),
    'source rewritten in place': (policy.replace('new Dictionary<T, T>(source)', 'source'), source),
    'saved settings changed': (policy, source + '\nSaveSettings'),
    'fake actions added': (policy, source + '\nInputMap.AddAction'),
    'warm cache removed': (policy, source.replace('ReferenceEquals(map, state.Normalized)', 'false')),
    'rebind invalidation removed': (policy, source.replace('Invalidate(__instance)', 'EnsureValid(__instance)')),
}
for name, (bad_policy, bad_patch) in cases.items():
    def fake(p, *args, **kwargs):
        if p == policy_path: return bad_policy
        if p == patch_path: return bad_patch
        return read(p, *args, **kwargs)
    with patch.object(Path, 'read_text', fake), contextlib.redirect_stdout(io.StringIO()):
        try: gate.check()
        except AssertionError: continue
    raise AssertionError('Regression accepted: ' + name)
print(f'issue#254 gate fixtures PASS: positive + {len(cases)} rejected regressions')
