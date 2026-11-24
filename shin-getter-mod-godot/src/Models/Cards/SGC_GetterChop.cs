using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_GetterChop : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move), new BlockVar(4m, ValueProp.Move) };

    public SGC_GetterChop()
        : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        for (int i = 0; i < 2 && cardPlay.Target.Block > 0m; i++)
            await PlunderShield(choiceContext, cardPlay);

        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (cardPlay.Target.IsAlive)
        {
            await QueueAcceleratedFollowupAnimation();
            await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this, cardPlay).Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        }
    }

    private async Task PlunderShield(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal plunderLimit = Hook.ModifyBlock(
            CombatState,
            Owner.Creature,
            DynamicVars.Block.BaseValue,
            DynamicVars.Block.Props,
            this,
            cardPlay,
            out _);
        decimal stolenBlock = Math.Min(cardPlay.Target.Block, plunderLimit);
        if (stolenBlock > 0m)
        {
            await CreatureCmd.LoseBlock(choiceContext, cardPlay.Target, stolenBlock, Owner.Creature);
            await CreatureCmd.GainBlock(Owner.Creature, stolenBlock, ValueProp.Unpowered, cardPlay);
        }
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
        base.DynamicVars.Block.UpgradeValueBy(2m);
    }
}
