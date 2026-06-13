using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 无限进化。战斗结束后随机获得1永久力量/敏捷/最大生命。
/// </summary>
public sealed class SGP_InfiniteEvolution : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterCombatVictory(CombatRoom _)
    {
        if (!base.Owner.IsDead && base.Amount > 0)
        {
            // 随机获得力量/敏捷/最大生命 (基于层数)
            var rng = new System.Random();
            int choice = rng.Next(3);
            var ctx = new ThrowingPlayerChoiceContext();
            if (choice == 0)
                await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, base.Amount, base.Owner, null);
            else if (choice == 1)
                await PowerCmd.Apply<DexterityPower>(ctx, base.Owner, base.Amount, base.Owner, null);
            else
                await CreatureCmd.GainMaxHp(base.Owner, (int)base.Amount);
        }
    }
}
