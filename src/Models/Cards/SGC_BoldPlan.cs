using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 大胆计划 | 技能 | 稀有 | X费 | 过牌/加费
/// 失去 X 气力，获得 X 能量，抽 X 张
/// </summary>
public sealed class SGC_BoldPlan : ShinGetterCardBase
{
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_BoldPlan()
        : base(-1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int x = ResolveEnergyXValue() + (IsUpgraded ? 1 : 0);
        var ki = Owner.Creature.GetPower<SGP_Ki>();
        if (ki != null && x > 0)
            await PowerCmd.ModifyAmount(choiceContext, ki, -x, Owner.Creature, this);
        if (x > 0)
        {
            await PlayerCmd.GainEnergy(x, Owner);
            await CardPileCmd.Draw(choiceContext, x, Owner);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
