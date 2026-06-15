#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using System.Linq;
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
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(base.Owner) || base.Amount <= 0) return;
        var player = base.Owner.Player;
        if (player == null || combatState == null) return;

        Flash();
        // 创建并自动打出盖塔飞斧
        for (int i = 0; i < base.Amount; i++)
        {
            var card = combatState.CreateCard<Models.Cards.SGC_GetterTomahawk>(player);
            await MegaCrit.Sts2.Core.Commands.CardCmd.AutoPlay(
                new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
                card, null);
        }
    }
}
