#!/usr/bin/env python3
"""Read-only v1.3.0 -> 111 Beta synchronization guard; never launches a game."""
from pathlib import Path
import json
from issue248_source_parity import check

ROOT = Path(__file__).resolve().parents[1]
manifest = json.loads((ROOT / 'ShinGetterMod.json').read_text(encoding='utf-8-sig'))
assert manifest['version'] == 'v1.3.0-beta.111', 'RED: v1.3.0 Beta candidate not synchronized'
assert manifest['min_game_version'] == '0.111.0'
for file in ('Services/ShinGetterBondSession.cs', 'Nodes/Combat/NShinGetterStarSlashSequence.cs',
             'Nodes/Combat/NShinGetterShiningSparkSequence.cs', 'Patches/ShinGetterCardSelectionHoverPatch.cs'):
    assert (ROOT / 'src' / file).is_file(), file
assert not (ROOT / 'src/Patches/ShinGetterSavedPropertiesPatch.cs').exists(), 'Do not import the removed 109 cache target'
assert 'StartRunLobbyPlayer player' in (ROOT / 'src/Patches/ShinGetterVisualsPatch.cs').read_text(encoding='utf-8-sig')
assert 'CreateRandomPotions' in (ROOT / 'src/Patches/ShinGetterPotionFactoryWeightPatch.cs').read_text(encoding='utf-8-sig')
assert 'GenerateAnimator(MegaSprite controller, Creature creature)' in (ROOT / 'src/Models/Characters/ShinGetter.cs').read_text(encoding='utf-8-sig')
print('issue#248 structural synchronization PASS (not gameplay or release acceptance)')
assert 'ReleaseProgressEpoch = "v1.3.0"' in (ROOT / 'src/Services/ShinGetterBondSession.cs').read_text(encoding='utf-8-sig')
assert 'PanelContainer' not in (ROOT / 'src/Nodes/Events/NShinGetterBondDialogue.cs').read_text(encoding='utf-8-sig')
invasion = (ROOT / 'src/Events/ShinGetterEventInvasionService.cs').read_text(encoding='utf-8-sig')
assert invasion.count('ReferenceEquals(combatState.Encounter?.CanonicalInstance, pending.Encounter)') == 2
for name in ('ByrdonisElite', 'KnightsElite'):
    assert f'ModelDb.Encounter<{name}>().ToMutable()' not in invasion
# Beta event-combat synchronizer owns the mutable clone and remains authoritative.
beta_source = Path('E:/Work/SlaytheSpare2-111-beta/src/Core')
assert '_combatSynchronizer.ReadyToEnterCombat(canonicalEncounter' in (beta_source / 'Models/EventModel.cs').read_text(encoding='utf-8-sig')
assert 'CreateCombatState(canonicalEncounter.ToMutable())' in (beta_source / 'Multiplayer/Game/EventCombatSynchronizer.cs').read_text(encoding='utf-8-sig')
check(ROOT.parent)
