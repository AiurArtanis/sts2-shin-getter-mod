using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 膨胀打击 | 攻击 | 罕见 | 2费 | 三号/防御终端
/// 该名敌人和自身每有 1 种异常状态就造成 5 伤害
/// 三号机加成：每有 1 种异常状态就获得 2 覆甲
/// </summary>
public sealed class SGC_ExpansionStrike : ShinGetterCardBase
{
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(5m, ValueProp.Move) };

    public SGC_ExpansionStrike()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        int debuffTypes = Owner.Creature.Powers.Concat(cardPlay.Target.Powers)
            .Where(power => power.Type == PowerType.Debuff)
            .Select(power => power.GetType())
            .Distinct()
            .Count();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue * debuffTypes).FromCard(this)
            .Targeting(cardPlay.Target)
            .BeforeDamage(() => ShinGetterCombatVfx.PlayExpansionRush(Owner.Creature, cardPlay.Target))
            .WithHitFx("vfx/vfx_attack_blunt").Execute(choiceContext);
        if (debuffTypes > 0 && HasForm(Owner, ShinGetterForm.Getter3))
            await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature, debuffTypes * 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
