#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_EmperorsFragment : ShinGetterRelicBase
{
    private int _playedVoiceMask;
    private int _combatStartVoiceCount;
    private bool _eventInvasionEnabled = true;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<SGP_Ki>(2m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<SGP_Ki>() };

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PlayedVoiceMask
    {
        get => _playedVoiceMask;
        set
        {
            AssertMutable();
            _playedVoiceMask = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int CombatStartVoiceCount
    {
        get => _combatStartVoiceCount;
        set
        {
            AssertMutable();
            _combatStartVoiceCount = value;
        }
    }

    [SavedProperty]
    public bool EventInvasionEnabled
    {
        get => _eventInvasionEnabled;
        set
        {
            AssertMutable();
            _eventInvasionEnabled = value;
        }
    }

    internal static SGR_EmperorsFragment CreateFrom(SGR_GetterFurnace getterFurnace)
    {
        var fragment = (SGR_EmperorsFragment)ModelDb.Relic<SGR_EmperorsFragment>().ToMutable();
        fragment.PlayedVoiceMask = getterFurnace.PlayedVoiceMask;
        fragment.CombatStartVoiceCount = getterFurnace.CombatStartVoiceCount;
        fragment.EventInvasionEnabled = getterFurnace.EventInvasionEnabled;
        return fragment;
    }

    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<SGP_ShinGetterOne>(
            new ThrowingPlayerChoiceContext(), Owner.Creature, 1m, Owner.Creature, null);
        await PowerCmd.Apply<SGP_Ki>(
            new ThrowingPlayerChoiceContext(), Owner.Creature,
            DynamicVars["SGP_Ki"].BaseValue, Owner.Creature, null);
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        ShinGetterExecutionMusicService.TryStart(Owner, card);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room) =>
        ShinGetterExecutionMusicService.StopAndRestore(room.CombatState);
}
