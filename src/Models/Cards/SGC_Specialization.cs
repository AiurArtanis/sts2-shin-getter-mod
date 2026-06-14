using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 专精 | 技能 | 稀有 | 0费 | 变形流
/// 将随机 1 张专属形态卡加入手牌。这张卡牌在本回合可以免费打出。消耗
/// 二号机：获得 1 能量，抽 1 张
/// </summary>
public sealed class SGC_Specialization : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_Specialization()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel[] candidates = GetCurrentForms(Owner).SelectMany(form => form switch
        {
            ShinGetterForm.Getter1 => new CardModel[] { ModelDb.Card<SGC_TomahawkFury>(), ModelDb.Card<SGC_GetterRayOverflow>(), ModelDb.Card<SGC_StarSlash>() },
            ShinGetterForm.Getter2 => new CardModel[] { ModelDb.Card<SGC_TornadoDrill>(), ModelDb.Card<SGC_SpiralDrill>(), ModelDb.Card<SGC_LigerAssault>() },
            ShinGetterForm.Getter3 => new CardModel[] { ModelDb.Card<SGC_ExpansionStrike>(), ModelDb.Card<SGC_Avalanche>(), ModelDb.Card<SGC_IronWall>() },
            _ => System.Array.Empty<CardModel>(),
        }).DistinctBy(card => card.GetType()).ToArray();
        if (candidates.Length > 0)
        {
            var card = CombatState.CreateCard(Owner.RunState.Rng.CombatCardSelection.NextItem(candidates), Owner);
            card.SetToFreeThisTurn();
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
        }
        if (HasForm(Owner, ShinGetterForm.Getter2))
        {
            await PlayerCmd.GainEnergy(1, Owner);
            await CardPileCmd.Draw(choiceContext, 1, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
