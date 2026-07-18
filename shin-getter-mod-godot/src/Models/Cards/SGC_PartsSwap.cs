using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 部件更换 | 技能 | 罕见 | 2费 | 烧牌
/// 最多选择 2 张手牌与消耗堆卡牌交换
/// </summary>
public sealed class SGC_PartsSwap : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar(2) };

    public SGC_PartsSwap()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int max = DynamicVars.Cards.IntValue;
        var exhaust = PileType.Exhaust.GetPile(Owner);
        var hand = PileType.Hand.GetPile(Owner);
        if (exhaust.Cards.Count == 0 || hand.Cards.All(card => card == this))
            return;

        var fromExhaust = (await CardSelectCmd.FromCombatPile(choiceContext, exhaust, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 0, Math.Min(max, exhaust.Cards.Count)))).ToList();
        int handMax = Math.Min(fromExhaust.Count, hand.Cards.Count(card => card != this));
        if (handMax == 0)
            return;
        var fromHand = (await CardSelectCmd.FromHand(choiceContext, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, handMax), card => card != this, this)).ToList();
        await CardPileCmd.Add(fromHand, PileType.Exhaust);
        await CardPileCmd.Add(fromExhaust.Take(fromHand.Count), PileType.Hand);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
