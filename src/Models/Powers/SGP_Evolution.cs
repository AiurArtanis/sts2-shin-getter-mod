#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.IsDead || Amount <= 0)
            return;

        var vigor = Owner.GetPower<VigorPower>();
        var regen = Owner.GetPower<RegenPower>();
        var plating = Owner.GetPower<PlatingPower>();

        int vigorAmount = vigor?.Amount ?? 0;
        int regenAmount = regen?.Amount ?? 0;
        int platingAmount = plating?.Amount ?? 0;
        int evolutionAmount = Math.Min(Amount, Math.Min(vigorAmount, Math.Min(regenAmount, platingAmount)));

        if (evolutionAmount <= 0)
            return;

        Flash();
        await PowerCmd.ModifyAmount(choiceContext, vigor!, -evolutionAmount, null, null);
        await PowerCmd.ModifyAmount(choiceContext, regen!, -evolutionAmount, null, null);
        await PowerCmd.ModifyAmount(choiceContext, plating!, -evolutionAmount, null, null);

        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, evolutionAmount, Owner, null);
        await CreatureCmd.GainMaxHp(Owner, evolutionAmount);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, evolutionAmount, Owner, null);

        var engine = Owner.GetPower<SGP_EvolutionEngine>();
        engine?.MarkPendingEnergyGain();
    }
}
