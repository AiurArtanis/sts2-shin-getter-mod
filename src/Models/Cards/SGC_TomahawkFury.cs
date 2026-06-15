using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Powers;
using System.Linq;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 战斧乱舞 | 攻击 | 罕见 | 2费 | 一号/输出终端
/// 获得 3 活力，对所有敌人造成 5 伤害 2 次
/// 一号机加成：斩杀时获得 3 活力
/// </summary>
public sealed class SGC_TomahawkFury : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(5m, ValueProp.Move),
        new PowerVar<VigorPower>(3m),
    };

    public SGC_TomahawkFury()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, DynamicVars["VigorPower"].BaseValue, Owner.Creature, this);
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard(this)
            .TargetingAllOpponents(CombatState).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (HasForm(Owner, ShinGetterForm.Getter1) && attack.Results.SelectMany(results => results).Any(result => result.WasTargetKilled))
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, 3m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["VigorPower"].UpgradeValueBy(2m);
    }
}
