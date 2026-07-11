#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 钢之魂 | 技能 | 稀有 | 1费 | 钢之魂流
/// 随机打出 1 张精神指令卡。升级后随机获得并打出。
/// </summary>
public sealed class SGC_SteelSpirit : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_SteelSpirit()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? card = IsUpgraded
            ? CreateRandomSpiritCommand()
            : FindSpiritCommandInCombatPiles();

        if (card == null)
            return;

        card.SetToFreeThisTurn();
        if (IsUpgraded)
        {
            CardPileAddResult addResult = await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
            if (!addResult.success)
                return;
        }

        await CardCmd.AutoPlay(choiceContext, card, null);
    }

    protected override void OnUpgrade()
    {
    }

    private CardModel? FindSpiritCommandInCombatPiles()
    {
        foreach (PileType pileType in new[] { PileType.Hand, PileType.Draw, PileType.Discard })
        {
            var candidates = pileType.GetPile(Owner).Cards
                .OfType<ShinGetterCardBase>()
                .Where(card => card.SpiritRequirement > 0)
                .Cast<CardModel>()
                .ToList();

            if (candidates.Count > 0)
                return Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
        }

        return null;
    }

    private CardModel? CreateRandomSpiritCommand()
    {
        var candidates = Pool.AllCards
            .OfType<ShinGetterCardBase>()
            .Where(card => card.SpiritRequirement > 0)
            .ToList();

        ShinGetterCardBase? candidate = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
        if (candidate == null || CombatState == null)
            return null;

        return CombatState.CreateCard(candidate, Owner);
    }
}
