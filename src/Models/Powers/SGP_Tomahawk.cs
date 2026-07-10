#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 盖塔飞斧。回合开始时额外打出「盖塔飞斧」。
/// </summary>
public sealed class SGP_Tomahawk : PowerModel
{
    private sealed class Data
    {
        public List<CardModel> Cards { get; } = new();
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public void QueueReplay(CardModel card)
    {
        GetInternalData<Data>().Cards.Add(card);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(base.Owner) || base.Amount <= 0) return;
        var player = base.Owner.Player;
        if (player == null || combatState == null) return;

        Data data = GetInternalData<Data>();
        List<CardModel> cards = data.Cards.ToList();
        data.Cards.Clear();

        Flash();
        foreach (CardModel card in cards)
        {
            await CardCmd.AutoPlay(
                new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
                card,
                null);
        }

        await PowerCmd.Remove(this);
    }
}
