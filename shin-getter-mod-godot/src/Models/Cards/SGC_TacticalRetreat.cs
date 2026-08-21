using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_TacticalRetreat : ShinGetterCardBase
{
    internal const float TransformSpeedScale = 0.75f;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_Shade>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new BlockVar(5m, ValueProp.Move),
        new PowerVar<SGP_Shade>(1m),
    };

    public SGC_TacticalRetreat()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool wasGetter2 = HasForm(Owner, ShinGetterForm.Getter2);

        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
        if (wasGetter2)
        {
            await PowerCmd.Apply<SGP_Shade>(
                choiceContext,
                Owner.Creature,
                DynamicVars["SGP_Shade"].BaseValue,
                Owner.Creature,
                this);
        }

        await PlayMovementVfx(() => ShinGetterCombatVfx.PlayTacticalRetreat(
            Owner.Creature,
            () => Transform(choiceContext, Owner, this)));
        await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Block.UpgradeValueBy(2m);
    }
}
