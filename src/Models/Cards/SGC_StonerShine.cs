using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Powers;
using ShinGetterMod.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 烈阳闪光弹 | 攻击 | 先古 | 1费
/// 对所有敌人造成 10 伤害，给予 2 衰退，额外造成本场战斗获得的正面层数伤害
/// </summary>
public sealed class SGC_StonerShine : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

    public SGC_StonerShine()
        : base(1, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal buffsGained = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
            .Where(entry => entry.Actor == Owner.Creature && entry.Power.Type == PowerType.Buff && entry.Amount > 0)
            .Sum(entry => entry.Amount);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + buffsGained).FromCard(this)
            .TargetingAllOpponents(CombatState).WithHitFx("vfx/vfx_starry_impact").Execute(choiceContext);
        foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(creature => creature.IsAlive))
            await PowerCmd.Apply<SGP_Wane>(choiceContext, enemy, 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
