#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 三体同心。打出的下N张牌后变形。
/// </summary>
public sealed class SGP_TripleUnity : PowerModel
{
    private class Data
    {
        public int cardsPlayed;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int DisplayAmount => System.Math.Max(0, base.Amount - GetInternalData<Data>().cardsPlayed);

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(base.Owner))
        {
            GetInternalData<Data>().cardsPlayed = 0;
            InvokeDisplayAmountChanged();
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.Owner.Creature != base.Owner) return;
        var data = GetInternalData<Data>();
        data.cardsPlayed++;
        InvokeDisplayAmountChanged();

        // 变形
        var cardBase = card as ShinGetterCardBase;
        if (cardBase != null)
            await cardBase.Transform(choiceContext, base.Owner.Player, cardBase);
    }
}
