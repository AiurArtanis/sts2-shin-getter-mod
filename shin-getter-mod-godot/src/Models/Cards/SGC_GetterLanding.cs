#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 盖塔登场 | 技能 | 先古 | 1费
/// 选择另一个原子形态后变形，获得气力和本回合的战机分离。
/// </summary>
public sealed class SGC_GetterLanding : ShinGetterCardBase
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_single/shin_getter/s_g_c_getter_landing_card.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths => new[]
    {
        PortraitPath,
        ImageHelper.GetImagePath("packed/card_portraits/shin_getter/s_g_c_getter_landing.png"),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.FromPower<SGP_Ki>(),
        HoverTipFactory.FromPower<SGP_OpenGet>(),
    });

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<SGP_Ki>(1m),
    };

    public SGC_GetterLanding()
        : base(1, CardType.Skill, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.Creature.GetPower<SGP_ShinForm>() != null)
        {
            await Transform(choiceContext, Owner, this);
        }
        else
        {
            ShinGetterForm[] candidates = new[]
                {
                    ShinGetterForm.Getter1,
                    ShinGetterForm.Getter2,
                    ShinGetterForm.Getter3,
                }
                .Where(form => !HasForm(Owner, form))
                .ToArray();
            ShinGetterForm selected = await NShinGetterFormChoice.Select(choiceContext, Owner, candidates);
            await TransformTo(choiceContext, Owner, selected, this);
        }

        await PowerCmd.Apply<SGP_Ki>(
            choiceContext,
            Owner.Creature,
            DynamicVars["SGP_Ki"].BaseValue,
            Owner.Creature,
            this);
        // The status is unique. Replaying Landing must retain an already-earned counter.
        if (Owner.Creature.GetPower<SGP_OpenGet>() == null)
        {
            // SGP_OpenGet keeps its initial zero counter behind a hidden sentinel stack because
            // PowerCmd intentionally treats zero-amount applications as a no-op.
            await PowerCmd.Apply<SGP_OpenGet>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["SGP_Ki"].UpgradeValueBy(1m);
    }
}
