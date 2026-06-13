using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 减负 | 技能 | 普通 | 2费 | 二号/防杀
/// 失去所有气力，每失去 1 点，获得 1 敏捷
/// 二号机：每失去 1 点，获得 1 再生
/// </summary>
public sealed class SGC_ShedLoad : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_ShedLoad()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var ki = Owner.Creature.GetPower<SGP_Ki>();
        if (ki is null || ki.Amount <= 0)
            return;

        int amount = ki.Amount;
        await PowerCmd.Remove(ki);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner.Creature, amount, Owner.Creature, this);

        if (HasForm(Owner, ShinGetterForm.Getter2))
            await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, amount, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
