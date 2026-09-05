#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Audio;
using ShinGetterMod.Events;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Services;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_EmperorsFragment : ShinGetterRelicBase, IInfiniteEvolutionProgressStore
{
    private int _playedVoiceMask;
    private int _playedVoiceMaskHigh;
    private int _openingVoiceMask;
    private int _combatStartVoiceCount;
    private bool _eventInvasionEnabled = true;
    private bool _infiniteEvolutionProgressInitialized;
    private int _infiniteEvolutionStrengthGain;
    private int _infiniteEvolutionDexterityGain;
    private int _infiniteEvolutionMaxHpGain;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<SGP_Ki>(2m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<SGP_Ki>() };

    public int PlayedVoiceMask
    {
        get => _playedVoiceMask;
        set
        {
            AssertMutable();
            _playedVoiceMask = value;
        }
    }

    public int PlayedVoiceMaskHigh
    {
        get => _playedVoiceMaskHigh;
        set
        {
            AssertMutable();
            _playedVoiceMaskHigh = value;
        }
    }

    public int OpeningVoiceMask
    {
        get => _openingVoiceMask;
        set
        {
            AssertMutable();
            _openingVoiceMask = value;
        }
    }

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
        fragment.PlayedVoiceMaskHigh = getterFurnace.PlayedVoiceMaskHigh;
        fragment.OpeningVoiceMask = getterFurnace.OpeningVoiceMask;
        fragment.CombatStartVoiceCount = getterFurnace.CombatStartVoiceCount;
        fragment.EventInvasionEnabled = getterFurnace.EventInvasionEnabled;
        fragment.InfiniteEvolutionProgressInitialized = getterFurnace.InfiniteEvolutionProgressInitialized;
        fragment.InfiniteEvolutionStrengthGain = getterFurnace.InfiniteEvolutionStrengthGain;
        fragment.InfiniteEvolutionDexterityGain = getterFurnace.InfiniteEvolutionDexterityGain;
        fragment.InfiniteEvolutionMaxHpGain = getterFurnace.InfiniteEvolutionMaxHpGain;
        return fragment;
    }

    public override async Task BeforeCombatStart()
    {
        ShinGetterStonerSunshineService.ResetCombat(Owner);
        Flash();
        await SGP_ShinGetterOne.ApplyOpening(Owner.Creature);
        await PowerCmd.Apply<SGP_Ki>(
            new ThrowingPlayerChoiceContext(), Owner.Creature,
            DynamicVars["SGP_Ki"].BaseValue, Owner.Creature, null);
        await ShinGetterEventInvasionService.ApplyPendingPreCombatSetup(Owner);
        Task openingFusion = NShinGetterStaticVisuals.PlayOpeningGetterOneFusion(Owner.Creature);
        ShinGetterVoiceService.PlayPreparedCombatStart(Owner);
        await openingFusion;
    }

    public override Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        ShinGetterVoiceService.OnAfterDamageGiven(Owner, dealer, result, target);
        return Task.CompletedTask;
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        ShinGetterVoiceService.OnAfterCurrentHpChanged(Owner, creature, delta);
        return Task.CompletedTask;
    }

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        ShinGetterVoiceService.OnAfterDamageReceived(Owner, target, result, props, dealer);
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ShinGetterStonerSunshineService.RecordCardPlayed(Owner, cardPlay);
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
            return;

        await ShinGetterEventInvasionService.ApplyPendingTrialAfterHandDraw(choiceContext, Owner);
        await ShinGetterStonerSunshineService.TryGrantAfterHandDraw(Owner);
    }

    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        ShinGetterExecutionMusicService.TryStart(Owner, card);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom room) =>
        ShinGetterExecutionMusicService.StopAndRestore(room.CombatState);

    public override Task AfterCombatVictory(CombatRoom room)
    {
        ShinGetterStonerSunshineService.AddVictoryReward(Owner, room);
        return Task.CompletedTask;
    }
}
