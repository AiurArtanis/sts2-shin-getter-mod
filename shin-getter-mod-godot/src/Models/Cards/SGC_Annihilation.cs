using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 湮灭 | 攻击 | 罕见 | 2费 | 进化流
/// 对所有敌人造成 8 伤害，给予 1 衰退，每造成 1 次伤害就将 1 张「放射能」加入手牌
/// </summary>
public sealed class SGC_Annihilation : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => Array.Empty<CardKeyword>();

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("Wane", 1m),
    };

    public SGC_Annihilation()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool isShinDragonCyclone = GetActionAnimationTrigger() == "Cyclone";
        await WaitForShinDragonSpecialEffect(1.2f);
        var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
            .TargetingAllOpponents(CombatState);
        if (isShinDragonCyclone)
            attack.WithNoAttackerAnim();
        else
            attack.WithAttackerAnim("Cast", 0.35f);

        await attack
            .BeforeDamage(() => ShinGetterCombatVfx.PlayAnnihilation(Owner.Creature, CombatState.GetOpponentsOf(Owner.Creature)))
            .Execute(choiceContext);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature dealer,
        MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result,
        ValueProp props,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
        CardModel cardSource)
    {
        if (!ReferenceEquals(cardSource, this) || dealer != Owner.Creature)
            return;

        if (result.TotalDamage > 0)
        {
            var radiated = CombatState.CreateCard<SGC_Radiated>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(radiated, PileType.Hand, Owner);
        }

        if (target.IsAlive)
        {
            await PowerCmd.Apply<SGP_Wane>(
                choiceContext,
                target,
                DynamicVars["Wane"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Wane"].UpgradeValueBy(2m);
    }
}
