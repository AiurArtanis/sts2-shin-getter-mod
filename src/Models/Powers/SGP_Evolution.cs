#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 进化。回合结束时，X=min(E,V,R,P)，获得X力量/最大生命/敏捷，各层数减去X。
/// </summary>
public sealed class SGP_Evolution : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterEnergyReset(Player player)
    {
        int E = base.Amount;
        if (E <= 0) return;

        int V = player.Creature.GetPower<VigorPower>()?.Amount ?? 0;
        int R = player.Creature.GetPower<RegenPower>()?.Amount ?? 0;
        int P = player.Creature.GetPower<PlatingPower>()?.Amount ?? 0;

        int X = Math.Min(Math.Min(E, V), Math.Min(R, P));
        if (X <= 0) return;

        var ctx = new ThrowingPlayerChoiceContext();

        await PowerCmd.ModifyAmount(ctx, this, -X, null, null);
        if (V > 0) await PowerCmd.ModifyAmount(ctx, player.Creature.GetPower<VigorPower>()!, -X, null, null);
        if (R > 0) await PowerCmd.ModifyAmount(ctx, player.Creature.GetPower<RegenPower>()!, -X, null, null);
        if (P > 0) await PowerCmd.ModifyAmount(ctx, player.Creature.GetPower<PlatingPower>()!, -X, null, null);

        await PowerCmd.Apply<StrengthPower>(ctx, player.Creature, X, player.Creature, null);
        await CreatureCmd.GainMaxHp(player.Creature, X);
        await PowerCmd.Apply<DexterityPower>(ctx, player.Creature, X, player.Creature, null);

        // 广播：通知进化引擎下回合获得能量
        var engine = player.Creature.GetPower<SGP_EvolutionEngine>();
        engine?.MarkPendingEnergyGain();
    }
}
