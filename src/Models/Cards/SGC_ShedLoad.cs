using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 减负 | 技能 | 普通 | 2费 | 二号/防杀
/// 失去所有气力，每失去 1 点，获得 10 格挡
/// 二号机：每失去 1 点，额外获得 1 敏捷和 1 再生
/// </summary>
public sealed class SGC_ShedLoad : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(10m, ValueProp.Move),
        new PowerVar<DexterityPower>(1m),
        new PowerVar<RegenPower>(1m),
    };

    public SGC_ShedLoad()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var ki = Owner.Creature.GetPower<SGP_Ki>();
        if (ki is null || ki.Amount <= 0)
            return;

        int amount = ki.Amount;
        await PowerCmd.Remove(ki);
        await CreatureCmd.GainBlock(
            Owner.Creature,
            amount * DynamicVars.Block.BaseValue,
            ValueProp.Move,
            cardPlay);

        if (HasForm(Owner, ShinGetterForm.Getter2))
        {
            await PowerCmd.Apply<DexterityPower>(
                choiceContext, Owner.Creature,
                amount * DynamicVars.Dexterity.BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<RegenPower>(
                choiceContext, Owner.Creature,
                amount * DynamicVars["RegenPower"].BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
