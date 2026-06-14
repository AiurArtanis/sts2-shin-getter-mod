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
/// 钢之魂 | 技能 | 稀有 | 1费 | 钢之魂流
/// 随机获 1 张精神指令卡。虚无
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
        var candidates = Pool.AllCards.OfType<ShinGetterCardBase>().Where(card => card.SpiritRequirement > 0).ToList();
        if (candidates.Count > 0)
        {
            var card = CombatState.CreateCard(Owner.RunState.Rng.CombatCardSelection.NextItem(candidates), Owner);
            card.SetToFreeThisTurn();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }
}
