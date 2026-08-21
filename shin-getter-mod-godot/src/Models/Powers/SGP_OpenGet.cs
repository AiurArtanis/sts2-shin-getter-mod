#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Combat;
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
using ShinGetterMod.Patches;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 战机分离。记录本回合此状态生效后的友方伤害，并在一次足额攻击命中时回避。
/// </summary>
public sealed class SGP_OpenGet : PowerModel
{
    private sealed class Data
    {
        public AttackCommand? ActiveAttack;
        public bool WillAvoidCurrentHit;
        public bool WillAvoidActiveAttack;
        public bool AvoidanceTriggered;
        public bool AvoidanceFeedbackPlayed;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // PowerCmd cannot apply a zero-amount power. Keep one hidden sentinel stack so the
    // power exists immediately after the card resolves, while the UI and threshold still
    // start at zero accumulated damage.
    public override int DisplayAmount => Amount - 1;

    protected override IEnumerable<DynamicVar> CanonicalVars => Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    internal bool WouldAvoidIntent(int totalDamage) =>
        totalDamage > 0 && totalDamage <= DisplayAmount && Owner.Player != null;

    internal bool WouldAvoidAttack(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer)
    {
        if (target != Owner || !props.IsPoweredAttack() || Owner.Player == null)
            return false;

        Data data = GetInternalData<Data>();
        if (data.ActiveAttack?.Attacker == dealer && data.WillAvoidActiveAttack)
            return true;

        int finalHitCount = data.ActiveAttack is { } activeAttack && activeAttack.Attacker == dealer
            ? ShinGetterOpenGetAttackHitCountPatch.GetFinalHitCount(activeAttack)
            : 1;
        decimal totalAttackDamage = finalHitCount > 1
            ? amount * finalHitCount
            : amount;
        SGP_Shade? shade = Owner.GetPower<SGP_Shade>();
        return totalAttackDamage > 0m
            && totalAttackDamage <= DisplayAmount
            && shade?.WouldPreventCurrentHit(dealer) != true;
    }

    internal bool IsAvoidingCurrentHit => GetInternalData<Data>().WillAvoidCurrentHit;

    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount)
    {
        if (attack.TargetSide == Owner.Side)
        {
            Data data = GetInternalData<Data>();
            data.ActiveAttack = attack;
            data.WillAvoidCurrentHit = false;
            data.WillAvoidActiveAttack = false;
            data.AvoidanceTriggered = false;
            data.AvoidanceFeedbackPlayed = false;
        }

        return hitCount;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (target.CombatState?.CurrentSide != CombatSide.Player
            || dealer?.Side != Owner.Side
            || target.Side == Owner.Side
            || result.TotalDamage <= 0)
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
        if (ShinGetterOpenGetIntentPatch.IsCalculatingIntentDamage)
            return 1m;

        if (!WouldAvoidAttack(target, amount, props, dealer))
            return 1m;

        data.WillAvoidCurrentHit = true;
        if (data.ActiveAttack?.Attacker == dealer)
            data.WillAvoidActiveAttack = true;
        Owner.GetPower<SGP_Shade>()?.RecordOpenGetAvoidedHit(dealer);
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
        if (Owner.Player is not { } player)
            return;

        data.AvoidanceTriggered = true;
        if (!data.AvoidanceFeedbackPlayed)
        {
            data.AvoidanceFeedbackPlayed = true;
            Flash();
            Task vfxTask = NShinGetterStaticVisuals.PlayOpenGetVfx(Owner);
            Task voiceTask = ShinGetterVoiceService.PlayOpenGet(player);
            await Task.WhenAll(vfxTask, voiceTask);
        }

        // Keep the power alive until the entire AttackCommand is complete so every hit of an
        // eligible multi-attack is avoided. Direct powered damage retains the immediate removal.
        if (data.ActiveAttack?.Attacker != dealer)
            await PowerCmd.Remove(this);
    }

    public override async Task AfterAttack(PlayerChoiceContext choiceContext, AttackCommand command)
    {
        Data data = GetInternalData<Data>();
        if (data.ActiveAttack != command)
            return;

        bool shouldRemove = data.AvoidanceTriggered;
        data.ActiveAttack = null;
        data.WillAvoidCurrentHit = false;
        data.WillAvoidActiveAttack = false;
        data.AvoidanceTriggered = false;
        data.AvoidanceFeedbackPlayed = false;
        if (shouldRemove)
            await PowerCmd.Remove(this);
    }

    public override async Task AfterEnergyReset(Player player)
    {
        if (player.Creature == Owner)
            await PowerCmd.Remove(this);
    }
}
