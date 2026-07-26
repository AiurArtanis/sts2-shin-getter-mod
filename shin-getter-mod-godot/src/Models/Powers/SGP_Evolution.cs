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
/// 进化。回合开始时在气力之后，分别将不超过进化层数的活力/再生/覆甲转化为永久成长。
/// </summary>
public sealed class SGP_Evolution : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power != this || amount <= 0m || Owner.IsDead)
            return;

        await PowerCmd.Apply<SGP_EvolutionMemory>(
            choiceContext,
            Owner,
            amount,
            Owner,
            cardSource,
            silent: true);
    }

    public override async Task AfterPlayerTurnStartLate(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player.Creature != Owner || Owner.IsDead || Amount <= 0)
            return;

        var vigor = Owner.GetPower<VigorPower>();
        var regen = Owner.GetPower<RegenPower>();
        var plating = Owner.GetPower<PlatingPower>();

        int vigorAmount = vigor?.Amount ?? 0;
        int regenAmount = regen?.Amount ?? 0;
        int platingAmount = plating?.Amount ?? 0;
        int evolutionAmount = Amount;
        int strengthGain = Math.Min(vigorAmount, evolutionAmount);
        int maxHpGain = Math.Min(regenAmount, evolutionAmount);
        int dexterityGain = Math.Min(platingAmount, evolutionAmount);

        Flash();
        await Cmd.CustomScaledWait(0.08f, 0.14f);
        await ConsumePower(choiceContext, vigor, strengthGain);
        await ConsumePower(choiceContext, regen, maxHpGain);
        await ConsumePower(choiceContext, plating, dexterityGain);

        if (strengthGain > 0)
            await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, strengthGain, Owner, null);
        if (maxHpGain > 0)
            await CreatureCmd.GainMaxHp(Owner, maxHpGain);
        if (dexterityGain > 0)
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, dexterityGain, Owner, null);

        if (strengthGain > 0 || maxHpGain > 0 || dexterityGain > 0)
        {
            var engine = Owner.GetPower<SGP_EvolutionEngine>();
            engine?.MarkPendingEnergyGain();
            await PowerCmd.ModifyAmount(choiceContext, this, -1m, null, null);
        }
    }

    private static async Task ConsumePower(PlayerChoiceContext choiceContext, PowerModel? power, int amount)
    {
        if (power != null && amount > 0)
            await PowerCmd.ModifyAmount(choiceContext, power, -amount, null, null);
    }
}
