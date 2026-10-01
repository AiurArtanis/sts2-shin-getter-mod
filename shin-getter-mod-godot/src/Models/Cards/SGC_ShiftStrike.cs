using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 换挡打击 | 攻击 | 普通 | 1费 | 变形流
/// 造成 6 伤害。首击实际扣血不足 6 时变形，并对仍可攻击的原目标追加一次攻击。
/// 升级后首击前额外变形一次。
/// </summary>
public sealed class SGC_ShiftStrike : ShinGetterCardBase
{
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

    public SGC_ShiftStrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var target = cardPlay.Target;
        var combatState = Owner.Creature.CombatState;

        if (IsUpgraded)
            await Transform(choiceContext, Owner, this);

        var firstAttack = await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        var firstResults = firstAttack.Results.Take(1).SelectMany(results => results)
            .Where(result => result.Receiver == target).ToArray();

        // UnblockedDamage is final HP loss, excluding block and overkill. The threshold stays 6.
        if (firstResults.Length == 0 || firstResults.Sum(result => result.UnblockedDamage) >= 6)
            return;

        await Transform(choiceContext, Owner, this);

        if (firstResults.Any(result => result.WasTargetKilled)
            || combatState == null || !combatState.ContainsCreature(target) || !target.IsHittable)
            return;

        await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
    }
}
