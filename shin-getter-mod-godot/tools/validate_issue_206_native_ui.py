#!/usr/bin/env python3
"""Read-only native UI wiring gate; does not launch Godot or prove visual/input acceptance."""
from pathlib import Path
import argparse
import json

ROOT = Path(__file__).resolve().parents[1]


def check():
    ui = (ROOT / 'src/Nodes/Events/NShinGetterBondDialogue.cs').read_text(encoding='utf-8-sig')
    patch = (ROOT / 'src/Patches/ShinGetterBondDialoguePatch.cs').read_text(encoding='utf-8-sig')
    for forbidden in ('ConversationPanel', 'PanelContainer', 'StyleBoxFlat', 'NShinGetterBondButton', 'NSettingsButton'):
        assert forbidden not in ui, f'RED: custom dialogue panel remains: {forbidden}'
    for token in ('NAncientEventLayout', '_ancient.SetDialogue(', 'SetDialogueLineAndAnimate',
                  'OnDialogueHitboxClicked', 'NEventOptionButton.Create', 'NBackButton',
                  'TalkCmd.Play', 'VfxDuration.Forever', 'StopSpeechBubble', 'RestoreNativeUi',
                  'FocusModeEnum.All', 'FocusNeighborTop', 'FocusNext', 'TryGrabFocus',
                  '_renderGeneration', '_optionActions.Clear()', '_skipButton.Disable()',
                  'SHIN_GETTER_BOND_UI_TEXT', 'IsUpdatingNativeUi', 'MarkDisplayed',
                  '_readingHeight', 'OnDialogueLineFocused', 'OnDialogueLineUnfocused'):
        assert token in ui or token in patch, token
    assert 'oldContent?.Hide()' not in patch, 'Do not hide the reused native dialogue'
    assert 'ui.TryChooseOption(option)' in patch, 'Temporary options must be intercepted before reward routing'
    choose = ui[ui.index('internal bool TryChooseOption'):ui.index('internal void AdvanceFromNativeHitbox')]
    for token in ('_closed', 'IsEventActive', '!button.IsEnabled', '!button.IsVisibleInTree()', 'button.IsQueuedForDeletion()) return false', '_optionActions.TryGetValue'):
        assert token in choose, token
    for forbidden in ('ChooseLocalOption(', 'EventSynchronizer', '.Chosen()', 'ForceClick('):
        assert forbidden not in choose, f'Local story choice must not enter reward/network flow: {forbidden}'
    assert 'RestoreNativeUi();' in ui[ui.index('private void Close()'):ui.index('public override void _ExitTree()')]
    assert 'RestoreNativeUi();' in ui[ui.index('public override void _ExitTree()'):ui.index('private void ReleaseConsoleGuard()')]
    assert 'RestoreOptionsVisibility' in ui and 'snapshot.Button.GetParent() == _column' in ui
    assert '_session.Advance()' in ui and '_session.ConsumeCue(encounter.Line)' in ui
    for lang in ('zhs', 'eng', 'jpn'):
        loc = json.loads((ROOT / f'ShinGetterMod/localization/{lang}/ancients.json').read_text(encoding='utf-8-sig'))
        assert loc['SHIN_GETTER_BOND_UI_TEXT'] == '{text}'
    print('issue#206 native UI wiring PASS (not native input or visual acceptance)')


def official(root):
    probes = {
        'src/Core/Nodes/Events/NAncientEventLayout.cs': ('void SetDialogue(IReadOnlyList<AncientDialogueLine> lines)', 'void SetDialogueLineAndAnimate(int lineIndex)', 'void OnDialogueHitboxClicked(NClickableControl _)', 'void OnDialogueLineFocused(NClickableControl dialogueLine)', 'void OnDialogueLineUnfocused(NClickableControl dialogueLine)'),
        'src/Core/Nodes/Events/NEventOptionButton.cs': ('NEventOptionButton Create(EventModel eventModel, EventOption option, int index)', 'NEventRoom.Instance.OptionButtonClicked(Option, Index);'),
        'src/Core/Commands/TalkCmd.cs': ('NSpeechBubbleVfx? Play(LocString line, Creature speaker, VfxColor vfxColor',),
        'src/Core/Models/Events/TheArchitect.cs': ('Creature? _architectCreature;', 'ArchitectAttackers',),
    }
    for path, tokens in probes.items():
        source = (root / path).read_text(encoding='utf-8-sig')
        for token in tokens:
            assert token in source, (path, token)
    print('official109 native UI source probes PASS (not Harmony runtime binding)')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path)
    args = parser.parse_args()
    check()
    if args.source_root:
        official(args.source_root)
