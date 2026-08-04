using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 盖塔意志 | 技能 | 稀有 | 0费
/// 选择抽牌堆 1 张能力卡加入手牌；升级后选择 2 张
/// 一号机：获得 2 进化
/// </summary>
public sealed class SGC_GetterWill : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Evolution>(2m),
        new CardsVar(1),
    };

    public SGC_GetterWill()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        IEnumerable<CardModel> selected = await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Draw.GetPile(Owner),
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, DynamicVars.Cards.IntValue),
            card => card.Type == CardType.Power);
        await CardPileCmd.Add(selected, PileType.Hand);

        ShinGetterVoiceService.TryPlayCardVoiceAtCustomTiming(this, out _);

        if (HasForm(Owner, ShinGetterForm.Getter1))
        {
            await PowerCmd.Apply<SGP_Evolution>(
                choiceContext,
                Owner.Creature,
                DynamicVars["SGP_Evolution"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
