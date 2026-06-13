#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 漆黑披风。回合格挡完全格挡伤害后对所有敌人造成N伤害。
/// </summary>
public sealed class SGP_DarkCape : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        // 格挡完全格挡时触发
        if (dealer == base.Owner && result.WasFullyBlocked && base.Amount > 0)
        {
            var enemies = base.Owner.CombatState?.HittableEnemies;
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != target) // skip the original attacker
                        await DamageCmd.Attack(base.Amount).FromCard(null).Targeting(enemy).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
                }
            }
        }
    }
}
