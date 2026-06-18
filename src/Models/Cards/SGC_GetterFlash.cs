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
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 盖塔闪光 | 攻击 | 罕见 | 1费 | 一号/输出终端
/// 造成 8 伤害，本回合名字中有"盖塔"的卡牌费用减 1，消耗
/// 一号机加成：获得 8 活力
/// </summary>
public sealed class SGC_GetterFlash : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(8m, ValueProp.Move) };

    public SGC_GetterFlash()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        foreach (var card in PileType.Hand.GetPile(Owner).Cards.Where(card => card.GetType().Name.StartsWith("SGC_Getter", StringComparison.Ordinal)))
            card.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
        if (HasForm(Owner, ShinGetterForm.Getter1))
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, 8m, Owner.Creature, this);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
            .BeforeDamage(() => ShinGetterCombatVfx.PlayWhiteFlash(Owner.Creature))
            .WithHitFx("vfx/vfx_starry_impact").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
