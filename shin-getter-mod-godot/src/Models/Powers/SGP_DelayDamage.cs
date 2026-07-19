#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 延时伤害。用两个隐藏 Power 的 Amount 保存本回合到期和下回合到期的独立批次。
/// </summary>
public sealed class SGP_DelayDamage : PowerModel
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override int DisplayAmount => IsMutable ? GetQueuedAmount(Owner) : 0;
    public override Color AmountLabelColor => _debuffAmountLabelColor;

    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("Amount", DisplayAmount);
            return description;
        }
    }

    internal static async Task AddPending(Creature owner, int amount)
    {
        if (amount <= 0)
            return;

        int pending = owner.GetPower<SGP_DelayDamagePending>()?.Amount ?? 0;
        await SetSlotAmount<SGP_DelayDamagePending>(owner, pending + amount);
        await EnsureFacade(owner);
        owner.GetPower<SGP_DelayDamage>()?.InvokeQueuedAmountChanged();
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner))
            return;

        int pending = Owner.GetPower<SGP_DelayDamagePending>()?.Amount ?? 0;
        if (pending <= 0)
            return;

        int due = Owner.GetPower<SGP_DelayDamageDue>()?.Amount ?? 0;
        await SetSlotAmount<SGP_DelayDamageDue>(Owner, due + pending);
        await SetSlotAmount<SGP_DelayDamagePending>(Owner, 0);
        InvokeQueuedAmountChanged();
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
            return;

        int due = Owner.GetPower<SGP_DelayDamageDue>()?.Amount ?? 0;
        if (due <= 0)
        {
            await CleanupIfEmpty();
            return;
        }

        Flash();
        await SetSlotAmount<SGP_DelayDamageDue>(Owner, 0);
        InvokeQueuedAmountChanged();

        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            due,
            ValueProp.Move | ValueProp.Unblockable,
            null,
            null);

        await CleanupIfEmpty();
    }

    private static int GetQueuedAmount(Creature owner) =>
        (owner.GetPower<SGP_DelayDamageDue>()?.Amount ?? 0)
        + (owner.GetPower<SGP_DelayDamagePending>()?.Amount ?? 0);

    private static async Task EnsureFacade(Creature owner)
    {
        var facade = owner.GetPower<SGP_DelayDamage>();
        if (facade == null)
        {
            facade = await PowerCmd.Apply<SGP_DelayDamage>(
                new ThrowingPlayerChoiceContext(),
                owner,
                1m,
                owner,
                null,
                silent: true);
        }

        if (facade != null && facade.Amount != 1)
            facade.SetAmount(1, silent: true);
    }

    private static async Task SetSlotAmount<T>(Creature owner, int amount)
        where T : PowerModel
    {
        var slot = owner.GetPower<T>();
        if (amount <= 0)
        {
            if (slot != null)
                await PowerCmd.Remove(slot);
            return;
        }

        if (slot == null)
        {
            slot = await PowerCmd.Apply<T>(
                new ThrowingPlayerChoiceContext(),
                owner,
                amount,
                owner,
                null,
                silent: true);
        }

        if (slot != null && slot.Amount != amount)
            slot.SetAmount(amount, silent: true);
    }

    private async Task CleanupIfEmpty()
    {
        if (GetQueuedAmount(Owner) <= 0)
            await PowerCmd.Remove(this);
        else
            InvokeQueuedAmountChanged();
    }

    private void InvokeQueuedAmountChanged() => InvokeDisplayAmountChanged();
}

public sealed class SGP_DelayDamageDue : PowerModel
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
}

public sealed class SGP_DelayDamagePending : PowerModel
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => false;
}
