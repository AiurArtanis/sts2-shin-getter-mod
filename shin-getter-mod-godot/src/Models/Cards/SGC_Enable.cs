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
/// 再动 | 能力 | 稀有 | 4费
/// 【精神 6】变形至一号机；结束当前回合，获得 1 个额外的回合
/// </summary>
public sealed class SGC_Enable : ShinGetterCardBase
{
    public override int SpiritRequirement => IsUpgraded ? 4 : 6;
    public override int UpgradePreviewSpiritRequirement => 4;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new PowerVar<SGP_Enable>(1m) };

    public SGC_Enable()
        : base(4, CardType.Power, CardRarity.Rare, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        for (int i = 0; i < 3 && !HasForm(Owner, ShinGetterForm.Getter1); i++)
            await Transform(choiceContext, Owner, this);
        var enablePower = await PowerCmd.Apply<SGP_Enable>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        enablePower?.FlashOnPlay();
        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override void OnUpgrade()
    {
    }
}
