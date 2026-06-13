using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 漆黑披风 | 技能 | 罕见 | 2费 | 通用/防杀
/// 获 10 格挡，本回合格挡每次格挡伤害就对所有敌人造成 2 伤害
/// 一号机加成：获得 1 腾空
/// </summary>
public sealed class SGC_DarkCape : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] {
        new BlockVar(10m, ValueProp.Move),
        new DamageVar(2m, ValueProp.Move)
    };

    public SGC_DarkCape()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
        // TODO: 本回合格挡每次格挡伤害就对所有敌人造成 2 伤害
        // TODO: 一号机加成：获得 1 腾空
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(3m);
        // TODO: 2→3 伤害
    }
}
