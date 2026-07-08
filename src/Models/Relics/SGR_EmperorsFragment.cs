using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_EmperorsFragment : ShinGetterRelicBase
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<SGP_Ki>(2m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<SGP_Ki>() };

    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<SGP_Ki>(
            new ThrowingPlayerChoiceContext(), Owner.Creature,
            DynamicVars["SGP_Ki"].BaseValue, Owner.Creature, null);
    }
}
