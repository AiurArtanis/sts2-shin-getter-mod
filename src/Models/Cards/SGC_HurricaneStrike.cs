using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 飓风打击 | 攻击 | 普通 | 1费 | 二号/过牌
/// 对所有敌人造成 6 伤害，每命中 1 目标抽 1 张
/// 二号机：获得 1 敏捷
/// </summary>
public sealed class SGC_HurricaneStrike : ShinGetterCardBase
{
    protected override IEnumerable<string> ExtraRunAssetPaths => new[]
    {
        NDaggerSprayFlurryVfx.scenePath,
        NDaggerSprayImpactVfx.scenePath,
    };

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

    public SGC_HurricaneStrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int targetCount = CombatState.HittableEnemies.Count;
        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue)
            .FromCard(this)
            .TargetingAllOpponents(CombatState)
            .BeforeDamage(() => ShinGetterCombatVfx.PlayDaggerSpray(Owner.Creature, CombatState.HittableEnemies))
            .Execute(choiceContext);

        if (targetCount > 0)
            await CardPileCmd.Draw(choiceContext, targetCount, Owner);

        if (HasForm(Owner, ShinGetterForm.Getter2))
            await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
