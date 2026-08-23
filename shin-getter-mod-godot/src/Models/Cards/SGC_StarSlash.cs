using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Audio;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 斩星斧 | 攻击 | 稀有 | 3费 | 烧牌/输出终端
/// 消耗抽牌堆 1 张卡，将数值叠加在此卡上，造成 22 点基础伤害
/// 一号机：每消耗 1 张牌获得 4 活力
/// </summary>
public sealed class SGC_StarSlash : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(22m, ValueProp.Move),
        new CardsVar(1),
        new DynamicVar("Vigor", 4m),
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
        ShinGetterVoiceService.TryPlayCardVoice(this);

        decimal stackedValue = Math.Min(selected.Sum(SumOriginalCardValues), 50m);
        foreach (var card in selected)
        {
            await CardCmd.Exhaust(choiceContext, card);
            if (HasForm(Owner, ShinGetterForm.Getter1))
            {
                await PowerCmd.Apply<VigorPower>(
                    choiceContext,
                    Owner.Creature,
                    DynamicVars["Vigor"].BaseValue,
                    Owner.Creature,
                    this);
            }
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue + stackedValue).FromCard(this)
            .WithNoAttackerAnim()
            .Targeting(cardPlay.Target)
            .BeforeDamage(() => NShinGetterStaticVisuals.PlayPhasedCreatureActionAnimation(
                Owner.Creature,
                GetActionAnimationTrigger() ?? "Attack",
                1f,
                1f,
                () => ShinGetterCombatVfx.PlayHeavyCleave(Owner.Creature, new[] { cardPlay.Target }),
                firstHalfDurationOverride: 0.5f))
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
