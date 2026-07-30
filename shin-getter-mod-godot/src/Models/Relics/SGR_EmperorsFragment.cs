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

public sealed class SGR_EmperorsFragment : ShinGetterRelicBase, IInfiniteEvolutionProgressStore
{
    private int _playedVoiceMask;
    private int _combatStartVoiceCount;
    private bool _infiniteEvolutionProgressInitialized;
    private int _infiniteEvolutionStrengthGain;
    private int _infiniteEvolutionDexterityGain;
    private int _infiniteEvolutionMaxHpGain;

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

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool InfiniteEvolutionProgressInitialized
    {
        get => _infiniteEvolutionProgressInitialized;
        set
        {
            AssertMutable();
            _infiniteEvolutionProgressInitialized = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int InfiniteEvolutionStrengthGain
    {
        get => _infiniteEvolutionStrengthGain;
        set
        {
            AssertMutable();
            _infiniteEvolutionStrengthGain = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int InfiniteEvolutionDexterityGain
    {
        get => _infiniteEvolutionDexterityGain;
        set
        {
            AssertMutable();
            _infiniteEvolutionDexterityGain = value;
        }
    }

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int InfiniteEvolutionMaxHpGain
    {
        get => _infiniteEvolutionMaxHpGain;
        set
        {
            AssertMutable();
            _infiniteEvolutionMaxHpGain = value;
        }
    }

    internal static SGR_EmperorsFragment CreateFrom(SGR_GetterFurnace getterFurnace)
    {
        var fragment = (SGR_EmperorsFragment)ModelDb.Relic<SGR_EmperorsFragment>().ToMutable();
        fragment.PlayedVoiceMask = getterFurnace.PlayedVoiceMask;
        fragment.CombatStartVoiceCount = getterFurnace.CombatStartVoiceCount;
        fragment.InfiniteEvolutionProgressInitialized = getterFurnace.InfiniteEvolutionProgressInitialized;
        fragment.InfiniteEvolutionStrengthGain = getterFurnace.InfiniteEvolutionStrengthGain;
        fragment.InfiniteEvolutionDexterityGain = getterFurnace.InfiniteEvolutionDexterityGain;
        fragment.InfiniteEvolutionMaxHpGain = getterFurnace.InfiniteEvolutionMaxHpGain;
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
