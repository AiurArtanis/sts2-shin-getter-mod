using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 夺取未来 | 技能 | 罕见 | 1费 | 加费
/// 获 6 格挡，将 1 张手牌本回合耗费减 1
/// </summary>
public sealed class SGC_SeizeFuture : ShinGetterCardBase
{
    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(6m, ValueProp.Move) };

    public SGC_SeizeFuture()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
        List<CardModel> candidates = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card != this && !card.EnergyCost.CostsX)
            .ToList();
        if (candidates.Count == 0)
            return;

        IEnumerable<CardModel> selected = await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            card => candidates.Contains(card),
            this);

        foreach (CardModel card in selected)
            card.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
