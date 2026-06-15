#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
    private sealed class Data
    {
        public CardModel? IgnoredCard;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public void IgnoreNextTriggerFrom(CardModel card)
    {
        GetInternalData<Data>().IgnoredCard = card;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var card = cardPlay.Card;
        if (card.Owner.Creature != base.Owner || Amount <= 0)
            return;

        var data = GetInternalData<Data>();
        if (ReferenceEquals(data.IgnoredCard, card))
        {
            data.IgnoredCard = null;
            return;
        }

        if (Owner.Player is { } player)
        {
            Flash();
            await PowerCmd.Decrement(this);
            await ShinGetterCardBase.Transform(choiceContext, player, card);
        }
    }
}
