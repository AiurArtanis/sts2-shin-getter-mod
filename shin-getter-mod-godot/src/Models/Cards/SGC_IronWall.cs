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
/// 铁壁 | 技能 | 稀有 | 2费 | 钢之魂流
/// 【精神 2】变形至三号机；下回合开始前，受到的所有伤害减 7。消耗
/// 三号机：受到伤害获得 1 覆甲
/// </summary>
public sealed class SGC_IronWall : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override int SpiritRequirement => 2;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_IronWall>(7m) };

    public SGC_IronWall()
        : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        for (int i = 0; i < 3 && !HasForm(Owner, ShinGetterForm.Getter3); i++)
            await Transform(choiceContext, Owner, this);
        await PowerCmd.Apply<SGP_IronWall>(choiceContext, Owner.Creature, DynamicVars["SGP_IronWall"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
