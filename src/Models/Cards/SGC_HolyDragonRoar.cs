#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_HolyDragonRoar : ShinGetterCardBase
{
    public override int MaxUpgradeLevel => 0;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/shin_getter/s_g_c_shin_form.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths => new[] { PortraitPath };

    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_HolyDragonRoar()
        : base(0, CardType.Attack, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // Placeholder card for Getter Mandala until the final Holy Dragon design lands.
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
    }
}
