#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 大雪山崩落 | 攻击 | 稀有 | 2费 | 三号/防杀终端
/// 造成 15 伤害，消耗全部格挡，每消耗 1 点额外造成 1 伤害
/// 三号机：每有一层覆甲就额外造成 1 伤害
/// </summary>
public sealed class SGC_Avalanche : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(15m),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(GetAvalancheBonus),
    };

    public SGC_Avalanche()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        decimal consumedBlock = Owner.Creature.Block;
        decimal plating = HasForm(Owner, ShinGetterForm.Getter3)
            ? Owner.Creature.GetPower<PlatingPower>()?.Amount ?? 0
            : 0;
        await CreatureCmd.LoseBlock(Owner.Creature, consumedBlock);
        await DamageCmd.Attack(DynamicVars.CalculatedDamage.BaseValue + consumedBlock + plating)
            .FromCard(this).Targeting(cardPlay.Target)
            .BeforeDamage(() => ShinGetterCombatVfx.PlayAvalanche(cardPlay.Target))
            .WithHitFx("vfx/vfx_rock_shatter").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(5m);
    }

    private static decimal GetAvalancheBonus(CardModel card, Creature? _) =>
        card.Owner.Creature.Block + (IsInForm(card.Owner, ShinGetterForm.Getter3)
            ? card.Owner.Creature.GetPower<PlatingPower>()?.Amount ?? 0
            : 0);
}
