using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_PoseidonThunder : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<VulnerablePower>(),
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<FrailPower>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move),
        new PowerVar<VulnerablePower>(1m),
        new PowerVar<WeakPower>(1m),
        new PowerVar<FrailPower>(1m),
    };

    public SGC_PoseidonThunder()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .WithNoAttackerAnim()
            .TargetingAllOpponents(CombatState)
            .BeforeDamage(() => ShinGetterCombatVfx.PlayThunderField(Owner.Creature, CombatState.GetOpponentsOf(Owner.Creature)))
            .WithHitFx("vfx/vfx_attack_lightning").Execute(choiceContext);

        foreach (var enemy in CombatState.GetOpponentsOf(Owner.Creature).Where(creature => creature.IsAlive))
        {
            await PowerCmd.Apply<VulnerablePower>(
                choiceContext, enemy, DynamicVars["VulnerablePower"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<WeakPower>(
                choiceContext, enemy, DynamicVars["WeakPower"].BaseValue, Owner.Creature, this);
            await PowerCmd.Apply<FrailPower>(
                choiceContext, enemy, DynamicVars["FrailPower"].BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["VulnerablePower"].UpgradeValueBy(2m);
        DynamicVars["WeakPower"].UpgradeValueBy(2m);
        DynamicVars["FrailPower"].UpgradeValueBy(2m);
    }
}
