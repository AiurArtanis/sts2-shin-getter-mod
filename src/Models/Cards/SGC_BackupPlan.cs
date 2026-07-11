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
/// 备用方案 | 技能 | 罕见 | 2费 | 二号/过牌/烧牌
/// 获得 5 格挡；消耗任意张手牌，每消耗一种类型的卡牌抽 1 张牌
/// 二号机加成：获得 1 能量
/// </summary>
public sealed class SGC_BackupPlan : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(5m, ValueProp.Move),
        new EnergyVar(1),
    };

    public SGC_BackupPlan()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

        var hand = PileType.Hand.GetPile(Owner).Cards.Where(card => card != this).ToList();
        if (hand.Count > 0)
        {
            var selected = (await CardSelectCmd.FromHand(choiceContext, Owner,
                new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, hand.Count),
                card => card != this, this)).ToList();
            int distinctTypes = selected.Select(card => card.Type).Distinct().Count();
            foreach (var card in selected)
                await CardCmd.Exhaust(choiceContext, card);
            if (distinctTypes > 0)
                await CardPileCmd.Draw(choiceContext, distinctTypes, Owner);
        }
        if (HasForm(Owner, ShinGetterForm.Getter2))
            await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
