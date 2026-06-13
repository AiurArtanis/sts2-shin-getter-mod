#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 超级气力。每回合开始将1张"气势"加入手牌。
/// </summary>
public sealed class SGP_SuperKi : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
            return;

        var combatState = player.Creature.CombatState;
        if (combatState == null) return;

        var card = combatState.CreateCard<SGC_Vigor>(player);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
    }
}
