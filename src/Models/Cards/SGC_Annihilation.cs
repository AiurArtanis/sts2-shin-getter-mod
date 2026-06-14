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
/// 湮灭 | 攻击 | 罕见 | 2费 | 进化流
/// 对所有敌人造成 10 伤害，每造成 1 次伤害就将 1 张「放射能」加入手牌
/// </summary>
public sealed class SGC_Annihilation : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => Array.Empty<CardKeyword>();

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

    public SGC_Annihilation()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
            .TargetingAllOpponents(CombatState)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
        int targetCount = attack.Results.SelectMany(results => results).Count(result => result.TotalDamage > 0);

        for (int i = 0; i < targetCount; i++)
        {
            var radiated = CombatState.CreateCard<SGC_Radiated>(Owner);
            await CardPileCmd.AddGeneratedCardToCombat(radiated, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        AddKeyword(CardKeyword.Retain);
    }
}
