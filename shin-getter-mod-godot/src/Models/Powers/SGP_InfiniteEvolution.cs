#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 无限进化。战斗结束后随机获得1永久力量/敏捷/最大生命。
/// </summary>
public sealed class SGP_InfiniteEvolution : PowerModel
{
    private sealed class Data
    {
        public SGC_InfiniteEvolution? SourceCard;
    }

    public enum VictoryGain
    {
        Strength,
        Dexterity,
        MaxHp,
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (cardSource is SGC_InfiniteEvolution source)
        {
            GetInternalData<Data>().SourceCard = source.DeckVersion as SGC_InfiniteEvolution ?? source;
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyPowerAmountReceived(
        PowerModel canonicalPower,
        Creature target,
        decimal amount,
        Creature? applier,
        out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target == Owner && canonicalPower is SGP_InfiniteEvolution && amount > 0m)
        {
            modifiedAmount = 0m;
            return true;
        }

        return false;
    }

    public override async Task AfterCombatEnd(CombatRoom _)
    {
        if (!base.Owner.IsDead && base.Amount > 0 && base.Owner.Player is { } player)
        {
            int roll = player.RunState.Rng.CombatCardSelection.NextInt(100);
            VictoryGain gain = roll switch
            {
                < 18 => VictoryGain.Strength,
                < 88 => VictoryGain.MaxHp,
                _ => VictoryGain.Dexterity,
            };
            SGC_InfiniteEvolution? sourceCard = ResolveSourceCard(player);
            if (sourceCard == null)
                return;

            sourceCard.RecordVictoryGain(gain);
            Flash();
            if (gain == VictoryGain.MaxHp)
                await CreatureCmd.GainMaxHp(base.Owner, 1m);
        }
    }

    private SGC_InfiniteEvolution? ResolveSourceCard(Player player)
    {
        Data data = GetInternalData<Data>();
        if (data.SourceCard != null && player.Deck.Cards.Contains(data.SourceCard))
            return data.SourceCard;

        SGC_InfiniteEvolution? deckSource = player.Deck.Cards
            .OfType<SGC_InfiniteEvolution>()
            .FirstOrDefault();
        if (deckSource != null)
            data.SourceCard = deckSource;

        return deckSource ?? data.SourceCard;
    }
}
