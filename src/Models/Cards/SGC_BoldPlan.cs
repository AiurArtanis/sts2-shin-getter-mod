#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using ShinGetterMod.Models.Enchantments;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 大胆计划 | 技能 | 稀有 | X费 | 过牌/附魔
/// 获得 X 辐射、X 气力，抽 X 张；二号机可附魔一张手牌
/// </summary>
public sealed class SGC_BoldPlan : ShinGetterCardBase
{
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        WithContextualHoverTips(HoverTipFactory.FromEnchantment<SGE_Adaptation>());

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Radiation>(1m),
        new PowerVar<SGP_Ki>(1m),
        new CardsVar(1),
    };

    public SGC_BoldPlan()
        : base(-1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int x = ResolveEnergyXValue() + (IsUpgraded ? 1 : 0);
        if (x > 0)
        {
            await PowerCmd.Apply<SGP_Radiation>(choiceContext, Owner.Creature, x, Owner.Creature, this);
            await PowerCmd.Apply<SGP_Ki>(choiceContext, Owner.Creature, x, Owner.Creature, this);
            await CardPileCmd.Draw(choiceContext, x, Owner);
        }

        if (HasForm(Owner, ShinGetterForm.Getter2))
        {
            EnchantmentModel adaptation = ModelDb.Enchantment<SGE_Adaptation>();
            CardModel? card = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner,
                new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1),
                candidate => candidate != this && adaptation.CanEnchant(candidate),
                this)).FirstOrDefault();
            if (card != null)
                CardCmd.Enchant<SGE_Adaptation>(card, 1m);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
