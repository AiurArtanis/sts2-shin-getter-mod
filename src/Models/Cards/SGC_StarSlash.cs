using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 斩星斧 | 攻击 | 稀有 | 3费 | 烧牌/输出终端
/// 消耗抽牌堆 1 张卡，将数值叠加在此卡上，共造成 20 点伤害
/// 一号机：额外造成本场战斗获得的活力值
/// </summary>
public sealed class SGC_StarSlash : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(20m, ValueProp.Move),
        new CardsVar(1),
    };

    public SGC_StarSlash()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var selected = (await CardSelectCmd.FromCombatPile(choiceContext, PileType.Draw.GetPile(Owner), Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, DynamicVars.Cards.IntValue))).ToList();
        decimal stackedValue = selected.Sum(SumOriginalCardValues);
        foreach (var card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        decimal vigorGained = 0m;
        if (HasForm(Owner, ShinGetterForm.Getter1))
        {
            vigorGained = CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
                .Where(entry => entry.Actor == Owner.Creature && entry.Power is VigorPower && entry.Amount > 0)
                .Sum(entry => entry.Amount);
        }
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + stackedValue + vigorGained).FromCard(this)
            .Targeting(cardPlay.Target)
            .BeforeDamage(() => ShinGetterCombatVfx.PlayHeavyCleave(Owner.Creature, new[] { cardPlay.Target }))
            .WithHitFx("vfx/vfx_giant_horizontal_slash").Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }

    private static decimal SumOriginalCardValues(CardModel card)
    {
        decimal total = card.DynamicVars.ContainsKey("CalculatedDamage")
            ? card.DynamicVars.CalculationBase.BaseValue
            : 0m;

        foreach (DynamicVar dynamicVar in card.DynamicVars.Values)
        {
            if (dynamicVar.Name is "CalculatedDamage" or "CalculationBase" or "CalculationExtra" or "ExtraDamage")
                continue;
            total += dynamicVar.BaseValue;
        }

        return total;
    }
}
