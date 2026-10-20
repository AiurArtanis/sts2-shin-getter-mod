"""Approved-source parity with explicit Beta API-only transformations."""
from pathlib import Path
import re
import subprocess
import json
import hashlib

FORMAL = '26c8da9a907fd9e12d41bf5bf17d24ca5975bfa8'
SHIM = 'shin-getter-mod-godot/src/Patches/ShinGetterSavedPropertiesPatch.cs'
COMPAT = 'shin-getter-mod-godot/src/Services/ShinGetterBondRunCompatibility.cs'
COMPAT_SHA256 = '0243b5cc748509252cf2f807a1ff5cf4553792c81c61b102b0fdc044e4933c13'
# Explicit issue#248/206 Beta behavior exception, separate from API transforms.
COMPAT_REPLACEMENTS = {
    'shin-getter-mod-godot/src/Services/ShinGetterBondSession.cs': (
        ('ShinGetterBondRunCompatibility.IsStandardModifiers(owner.RunState.Modifiers)', 'owner.RunState.Modifiers.Count == 0'),
        ('!ShinGetterBondRunCompatibility.IsStandardHistoryModifiers(history.Modifiers)', 'history.Modifiers.Count != 0'),
    ),
    'shin-getter-mod-godot/src/Diagnostics/ShinGetterDialogueConsoleCmd.cs': (
        ('!ShinGetterBondRunCompatibility.IsStandardModifiers(state.Modifiers)', 'state.Modifiers.Count != 0'),
    ),
}


def before_compatibility(path, text):
    for current, previous in COMPAT_REPLACEMENTS.get(path, ()):
        assert text.count(current) == 1, f'Unexpected compatibility wiring: {path}: {current}'
        text = text.replace(current, previous)
    return text


def api_calls(text, name, transform):
    cursor = 0
    while (start := text.find(name + '(', cursor)) >= 0:
        end = start + len(name)
        depth = 0
        quote = None
        escaped = False
        args = []
        token = end + 1
        for i in range(end, len(text)):
            char = text[i]
            if quote:
                if escaped: escaped = False
                elif char == '\\': escaped = True
                elif char == quote: quote = None
            elif char in ('"', "'"): quote = char
            elif char in '([{': depth += 1
            elif char in ')]}':
                depth -= 1
                if depth == 0:
                    args.append(text[token:i].strip())
                    replacement = name + '(' + ', '.join(transform(args)) + ')'
                    text = text[:start] + replacement + text[i + 1:]
                    cursor = start + len(replacement)
                    break
            elif char == ',' and depth == 1:
                args.append(text[token:i].strip()); token = i + 1
        else: raise AssertionError('Unbalanced call: ' + name)
    return text


def normalize(text, beta=False):
    text = re.sub(r'^using .*?;\s*', '', text, flags=re.M)
    text = re.sub(r'//[^\n]*|/\*[\s\S]*?\*/', '', text)
    text = text.replace('StartRunLobbyPlayer', 'LobbyPlayer')
    text = text.replace('"CreateRandomPotions"', '"CreateRandomPotion"')
    text = text.replace('ref IEnumerable<PotionModel> __result', 'ref List<PotionModel> __result')
    text = re.sub(r',\s*CardPlay\? cardPlay(?=\s*\))', '', text)
    text = text.replace('GenerateAnimator(MegaSprite controller, Creature creature)', 'GenerateAnimator(MegaSprite controller)')
    text = text.replace('PlunderShield(PlayerChoiceContext choiceContext, CardPlay cardPlay)', 'PlunderShield(CardPlay cardPlay)')
    text = api_calls(text, '.FromCard', lambda a: a[:-1] if a[-1] == 'cardPlay' and len(a) == 2 else a)
    text = api_calls(text, 'PlunderShield', lambda a: a[1:] if len(a) == 2 and a[0] == 'choiceContext' else a)
    if beta:
        text = api_calls(text, 'CreatureCmd.Damage', lambda a: a[:-1] if len(a) == 7 and a[-1] in ('cardPlay', 'null') or len(a) == 6 and a[-1] == 'cardPlay' else a)
    text = api_calls(text, 'CreatureCmd.LoseBlock', lambda a: a[1:3] if len(a) == 4 and a[0] == 'choiceContext' and a[-1] in ('null', 'Owner.Creature') else a)
    text = api_calls(text, 'SignalPlayerChoiceBegun', lambda a: a[1:] if len(a) == 2 and a[0] == 'player' else a)
    for name in ('base.ModifyDamageAdditive', 'base.ModifyDamageMultiplicative'):
        text = api_calls(text, name, lambda a: a[:-1] if len(a) == 6 and a[-1] == 'cardPlay' else a)
    if beta:
        text = text.replace('combatState.Encounter?.CanonicalInstance', 'combatState.Encounter')
        text = text.replace('ModelDb.Encounter<ByrdonisElite>()', 'ModelDb.Encounter<ByrdonisElite>().ToMutable()')
        text = text.replace('ModelDb.Encounter<KnightsElite>()', 'ModelDb.Encounter<KnightsElite>().ToMutable()')
    return re.sub(r'\s+', '', text)


def check(repo):
    paths = subprocess.check_output(['git', 'ls-tree', '-r', '--name-only', FORMAL, 'shin-getter-mod-godot/src'], cwd=repo, text=True).splitlines()
    expected = ({p for p in paths if p.endswith('.cs')} - {SHIM}) | {COMPAT}
    actual = {p.relative_to(repo).as_posix() for p in (repo / 'shin-getter-mod-godot/src').rglob('*.cs')}
    assert actual == expected, f'C# source-set mismatch: missing={expected-actual}, extra={actual-expected}'
    bad = []
    for p in sorted(expected):
        if p == COMPAT:
            assert hashlib.sha256((repo / p).read_text(encoding='utf-8-sig').encode()).hexdigest() == COMPAT_SHA256, 'Unreviewed BS2 compatibility helper change'
            continue
        original = subprocess.check_output(['git', 'show', FORMAL + ':' + p], cwd=repo).decode('utf-8-sig')
        current = (repo / p).read_text(encoding='utf-8-sig')
        if original == current: continue
        if normalize(original) != normalize(before_compatibility(p, current), beta=True): bad.append(p)
    assert not bad, 'Non-API gameplay divergence from approved source: ' + ', '.join(bad)
    allowed = {'shin-getter-mod-godot/ShinGetterMod.json', 'shin-getter-mod-godot/ShinGetterMod/update_history.json'}
    differences = subprocess.check_output(['git', 'diff', '--name-only', FORMAL, '--', 'shin-getter-mod-godot'], cwd=repo, text=True).splitlines()
    non_source = [p for p in differences if '/src/' not in p and '/tools/' not in p and p not in allowed and not re.search(r'/localization/(zhs|eng|jpn)/settings_ui\.json$', p)]
    assert not non_source, 'Resource/localization loss or unrelated changes: ' + ', '.join(non_source)
    manifest_path = 'shin-getter-mod-godot/ShinGetterMod.json'
    formal_manifest = json.loads(subprocess.check_output(['git', 'show', FORMAL + ':' + manifest_path], cwd=repo))
    beta_manifest = json.loads((repo / manifest_path).read_text(encoding='utf-8-sig'))
    assert {k: v for k, v in formal_manifest.items() if k not in ('version', 'min_game_version')} == {k: v for k, v in beta_manifest.items() if k not in ('version', 'min_game_version')}
    for language in ('zhs', 'eng', 'jpn'):
        path = f'shin-getter-mod-godot/ShinGetterMod/localization/{language}/settings_ui.json'
        formal = json.loads(subprocess.check_output(['git', 'show', FORMAL + ':' + path], cwd=repo))
        beta = json.loads((repo / path).read_text(encoding='utf-8-sig'))
        assert {k: v for k, v in formal.items() if '_beta_' not in k} == {k: v for k, v in beta.items() if '_beta_' not in k}, f'Non-channel localization divergence: {language}'
    path = 'shin-getter-mod-godot/ShinGetterMod/update_history.json'
    formal = json.loads(subprocess.check_output(['git', 'show', FORMAL + ':' + path], cwd=repo))
    beta = json.loads((repo / path).read_text(encoding='utf-8-sig'))
    assert [entry for entry in beta if '-beta.' not in entry['version']] == formal, 'Formal update history must remain exact'
    print(f'issue#248 approved source parity PASS: {len(expected)} C# files; explicit BS2 passive-stat compatibility + API differences; resources/texts exact (except channel metadata)')
