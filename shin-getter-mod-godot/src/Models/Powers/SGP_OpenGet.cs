#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 战机分离。记录本回合此状态生效后的友方伤害，并在一次足额攻击命中时回避。
/// </summary>
public sealed class SGP_OpenGet : PowerModel
{
    private sealed class Data
    {
        public bool WillAvoidCurrentHit;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // PowerCmd cannot apply a zero-amount power. Keep one hidden sentinel stack so the
    // power exists immediately after the card resolves, while the UI and threshold still
    // start at zero accumulated damage.
    public override int DisplayAmount => Amount - 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer?.Side != Owner.Side || target.Side == Owner.Side || result.TotalDamage <= 0)
            return;

        int gainedDamage = result.TotalDamage;
        if (gainedDamage > 0)
            await PowerCmd.ModifyAmount(choiceContext, this, gainedDamage, Owner, cardSource);
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        Data data = GetInternalData<Data>();
        data.WillAvoidCurrentHit = false;
        if (target != Owner
            || !props.IsPoweredAttack()
            || amount <= 0m
            || amount > DisplayAmount
            || Owner.Player is not { } player
            || player.Creature.GetPower<SGP_ShinForm>() != null)
        {
            return 1m;
        }

        data.WillAvoidCurrentHit = true;
        return 0m;
    }

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        Data data = GetInternalData<Data>();
        if (target != Owner || !data.WillAvoidCurrentHit)
            return;

        data.WillAvoidCurrentHit = false;
        if (Owner.Player is not { } player || player.Creature.GetPower<SGP_ShinForm>() != null)
            return;

        Flash();
        Task vfxTask = NShinGetterStaticVisuals.PlayOpenGetVfx(Owner);
        Task voiceTask = ShinGetterVoiceService.PlayOpenGet(player);
        await Task.WhenAll(vfxTask, voiceTask);
        await PowerCmd.Remove(this);
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}
