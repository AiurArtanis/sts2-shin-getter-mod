#!/usr/bin/env python3
"""Bounded v1.2.2 -> 111 Beta parity/containment gate. No gameplay execution."""
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / 'shin-getter-mod-godot'
FORMAL = '7286ae7f59e12f266b9d008ac2509f0ade648e93'
BASE = '50aa4e23'
SYNCED = {
    'Audio/ShinGetterBgmCatalog.cs',
    'Audio/ShinGetterBgmPreviewService.cs',
    'Audio/ShinGetterEncounterMusicService.cs',
    'Audio/ShinGetterExecutionMusicService.cs',
    'Config/ShinGetterChunibyoConfigService.cs',
    'Nodes/Config/NChunibyoConfigSubmenu.cs',
}

def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)

def normalized(data):
    return data.replace(b'\r\n', b'\n')

def main():
    prefix = 'shin-getter-mod-godot/src/'
    baseline = set(git('ls-tree', '-r', '--name-only', BASE, '--', prefix).decode().splitlines())
    actual = {p.relative_to(ROOT).as_posix() for p in (PROJECT / 'src').rglob('*') if p.is_file()}
    # Only tracked baseline source files are relevant; generated .uid files are not code.
    assert {p for p in actual if p.endswith('.cs')} == {p for p in baseline if p.endswith('.cs')}
    count = 0
    for path in sorted(p for p in baseline if p.endswith('.cs')):
        ref = FORMAL if path.removeprefix(prefix) in SYNCED else BASE
        assert normalized((ROOT / path).read_bytes()) == normalized(git('show', f'{ref}:{path}')), path
        count += 1
    formal_music = set(git('ls-tree', '-r', '--name-only', FORMAL, '--', 'shin-getter-mod-godot/audio/music').decode().splitlines())
    actual_music = {p.relative_to(ROOT).as_posix() for p in (PROJECT / 'audio/music').rglob('*.mp3')}
    assert actual_music == {p for p in formal_music if p.endswith('.mp3')}
    for path in actual_music:
        assert (ROOT / path).read_bytes() == git('show', f'{FORMAL}:{path}'), path
    for lang in ('zhs', 'eng', 'jpn'):
        path = f'shin-getter-mod-godot/ShinGetterMod/localization/{lang}/settings_ui.json'
        formal = json.loads(git('show', f'{FORMAL}:{path}'))
        beta = json.loads((ROOT / path).read_text(encoding='utf-8-sig'))
        assert all(beta.get(k) == v for k, v in formal.items()), lang
        assert set(beta) - set(formal) == {
            'SHIN_GETTER_CHUNIBYO.UPDATE.v1_2_0_beta_111',
            'SHIN_GETTER_CHUNIBYO.UPDATE.v1_2_1_beta_111',
            'SHIN_GETTER_CHUNIBYO.UPDATE.v1_2_2_beta_111',
        }, lang
    history = json.loads((PROJECT / 'ShinGetterMod/update_history.json').read_text(encoding='utf-8-sig'))
    old_history = json.loads(git('show', f'{BASE}:shin-getter-mod-godot/ShinGetterMod/update_history.json'))
    assert history[2:] == old_history
    assert [e['version'] for e in history[:2]] == ['v1.2.2-beta.111', 'v1.2.2']
    manifest_path = PROJECT / 'tools/issue214-bgm-manifest.json'
    assert json.loads(manifest_path.read_text(encoding='utf-8-sig')) == json.loads(git('show', f'{FORMAL}:shin-getter-mod-godot/tools/issue214-bgm-manifest.json'))
    print(f'issue#234 PASS: {count} production C# files; 6 formal-exact, remaining Beta-baseline exact; 23 audio files and trilingual settings match; history retained')

if __name__ == '__main__':
    main()
