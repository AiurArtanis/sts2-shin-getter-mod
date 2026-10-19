"""File-level stable Tag/Beta semantic whitelist; no game or deployment."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
from issue248_source_parity import FORMAL, SHIM, check, normalize

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '.github/issue248-refresh-semantic-whitelist.json'


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def cosmetic(text):
    text = re.sub(r'^using .*?;\s*', '', text, flags=re.M)
    return re.sub(r'\s+', '', re.sub(r'//[^\n]*|/\*[\s\S]*?\*/', '', text))


check(ROOT)
rows = []
paths = git('ls-tree', '-r', '--name-only', FORMAL, 'shin-getter-mod-godot/src').decode().splitlines()
for path in paths:
    if not path.endswith('.cs'):
        continue
    a = git('show', FORMAL + ':' + path)
    target = ROOT / path
    if path == SHIM:
        assert not target.exists()
        rows.append({'path': path, 'reason': '109-only cache shim omitted; 111 native ModelIdSerializationCache owns registration/IDs/width/hash'})
        continue
    b = target.read_bytes()
    if a == b:
        continue
    ta, tb = a.decode('utf-8-sig'), b.decode('utf-8-sig')
    assert normalize(ta) == normalize(tb, beta=True), path
    if cosmetic(ta) == cosmetic(tb):
        reason = 'format/import/comment only'
    elif '/Models/Cards/' in path:
        reason = '111 CardPlay attack propagation; GetterChop also retains choiceContext/remover for LoseBlock'
    elif '/Models/Powers/' in path or '/Models/Enchantments/' in path:
        reason = '111 damage override/direct-Damage CardPlay context; non-card sources explicitly null'
    elif path.endswith('/Events/ShinGetterEventInvasionService.cs'):
        reason = '111 synchronizer owns mutable clone: canonical encounters in, CanonicalInstance comparisons'
    elif path.endswith('/Models/Characters/ShinGetter.cs'):
        reason = '111 GenerateAnimator(MegaSprite, Creature) override'
    elif path.endswith('/Nodes/Combat/NShinGetterFormChoice.cs'):
        reason = '111 SignalPlayerChoiceBegun includes Player'
    elif path.endswith('/Patches/ShinGetterVisualsPatch.cs'):
        reason = '111 StartRunLobbyPlayer character-select signature'
    elif path.endswith('/Patches/ShinGetterPotionFactoryWeightPatch.cs'):
        reason = '111 plural CreateRandomPotions / IEnumerable result'
    else:
        raise AssertionError('Unclassified C# difference: ' + path)
    rows.append({'path': path, 'reason': reason, 'formalSha256': hashlib.sha256(a).hexdigest(),
                 'betaSha256': hashlib.sha256(b).hexdigest(), 'normalizedSha256': hashlib.sha256(normalize(ta).encode()).hexdigest()})

resources = [p for p in git('diff', '--name-only', FORMAL, '--', 'shin-getter-mod-godot').decode().splitlines()
             if '/src/' not in p and '/tools/' not in p]
expected = ['shin-getter-mod-godot/ShinGetterMod.json', 'shin-getter-mod-godot/ShinGetterMod/update_history.json']
expected += [f'shin-getter-mod-godot/ShinGetterMod/localization/{lang}/settings_ui.json' for lang in ('zhs', 'eng', 'jpn')]
assert set(resources) == set(expected), resources
record = {'formalTag': 'mod-v1.3.0', 'formalCommit': FORMAL,
          'betaBase': 'dce78bf472ca678b8bac042e878489d48014e83c', 'productCSharpFiles': 347,
          'apiOnlyOrNativeReplacementDifferences': rows,
          'resourceChannelWhitelist': sorted(resources),
          'resourceRule': 'All other tracked project resources byte-identical to formal Tag. Common settings/history entries exact; only beta history/localization keys and manifest version/min_game_version differ.',
          'metadataInput': {'kind': 'official111 ExportRelease', 'sha256': '0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9', 'mvid': '73b63ee0-6c0a-47bb-b0d1-b21f6d94222e'},
          'bs2StaticInput': {'version': '2.3.0', 'dllSha256': 'B58AC78FE6CB7C1E771AF483450D7AFB402DB7E9AC7FB5A113970D3844BBC0B2', 'adapterChanged': False, 'runtimeAcceptance': 'NOT_RUN'}}
OUT.write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(f'Whitelist PASS: {len(rows)} source/native-replacement exceptions; {len(resources)} channel resource files; all other resources exact')
