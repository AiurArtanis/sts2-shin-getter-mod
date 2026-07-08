using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 专精 | 技能 | 稀有 | 0费 | 变形流
/// 将随机 1 张专属形态卡加入手牌。这张卡牌在本回合可以免费打出。消耗
/// 二号机：获得 4 再生
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
        foreach (ShinGetterForm form in GetCurrentForms(Owner))
        {
            CardModel[] candidates = GetFormCards(form);
            if (candidates.Length > 0)
            {
                var card = CombatState.CreateCard(Owner.RunState.Rng.CombatCardSelection.NextItem(candidates), Owner);
                card.SetToFreeThisTurn();
                await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
            }
        }

        if (HasForm(Owner, ShinGetterForm.Getter2))
        {
            await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, 4m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }

    private static CardModel[] GetFormCards(ShinGetterForm form) => form switch
    {
        ShinGetterForm.Getter1 => new CardModel[]
        {
            ModelDb.Card<SGC_BlackArmor>(),
            ModelDb.Card<SGC_DarkCape>(),
            ModelDb.Card<SGC_Desperation>(),
            ModelDb.Card<SGC_DiveStrike>(),
            ModelDb.Card<SGC_GetterBeam>(),
            ModelDb.Card<SGC_GetterFlash>(),
            ModelDb.Card<SGC_GetterTomahawk>(),
            ModelDb.Card<SGC_GetterWill>(),
            ModelDb.Card<SGC_Insight>(),
            ModelDb.Card<SGC_StarSlash>(),
            ModelDb.Card<SGC_TomahawkFury>(),
        },
        ShinGetterForm.Getter2 => new CardModel[]
        {
            ModelDb.Card<SGC_BackupPlan>(),
            ModelDb.Card<SGC_Desperation>(),
            ModelDb.Card<SGC_GetterClaw>(),
            ModelDb.Card<SGC_HurricaneStrike>(),
            ModelDb.Card<SGC_Insight>(),
            ModelDb.Card<SGC_Jammer>(),
            ModelDb.Card<SGC_LigerAssault>(),
            ModelDb.Card<SGC_ShedLoad>(),
            ModelDb.Card<SGC_SpiralDrill>(),
            ModelDb.Card<SGC_TornadoDrill>(),
        },
        ShinGetterForm.Getter3 => new CardModel[]
        {
            ModelDb.Card<SGC_Avalanche>(),
            ModelDb.Card<SGC_Desperation>(),
            ModelDb.Card<SGC_ExpansionStrike>(),
            ModelDb.Card<SGC_GetterElbow>(),
            ModelDb.Card<SGC_GetterMissile>(),
            ModelDb.Card<SGC_GetterRush>(),
            ModelDb.Card<SGC_Grapple>(),
            ModelDb.Card<SGC_HedgehogTactic>(),
            ModelDb.Card<SGC_Insight>(),
            ModelDb.Card<SGC_IronWall>(),
        },
        _ => System.Array.Empty<CardModel>(),
    };
}
