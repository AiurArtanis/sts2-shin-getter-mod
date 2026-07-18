using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 干扰器 | 技能 | 罕见 | 2费 | 过渡
/// 虚无，获得 1 层分身，变形直到变成二号机
/// </summary>
public sealed class SGC_Jammer : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(10m, ValueProp.Move) };

    public SGC_Jammer()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<SGP_Shade>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        for (int i = 0; i < 3 && !HasForm(Owner, ShinGetterForm.Getter2); i++)
            await Transform(choiceContext, Owner, this);
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Ethereal);
    }
}
