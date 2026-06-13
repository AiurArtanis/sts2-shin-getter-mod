using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_BlackArmor : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(11m, ValueProp.Move) };

    public SGC_BlackArmor()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);

        if (!HasForm(Owner, ShinGetterForm.Getter1))
            return;

        await DecrementIfPresent<VulnerablePower>();
        await DecrementIfPresent<WeakPower>();
        await DecrementIfPresent<FrailPower>();
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(3m);
    }

    private async Task DecrementIfPresent<T>() where T : PowerModel
    {
        var power = Owner.Creature.GetPower<T>();
        if (power != null)
            await PowerCmd.Decrement(power);
    }
}
