#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 斗志。被攻击前先对敌人造成等同于层数的伤害。
/// </summary>
public sealed class SGP_FightingSpirit : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // 被攻击前，对攻击者造成反击伤害
        if (target == base.Owner && dealer != null && base.Amount > 0)
        {
            await DamageCmd.Attack(base.Amount).FromCard(null).Targeting(dealer).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        }
    }
}
