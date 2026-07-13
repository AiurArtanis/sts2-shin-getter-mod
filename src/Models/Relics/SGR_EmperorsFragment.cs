using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_EmperorsFragment : ShinGetterRelicBase
{
    private int _playedVoiceMask;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<SGP_Ki>(2m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<SGP_Ki>() };

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public int PlayedVoiceMask
    {
        get => _playedVoiceMask;
        set
        {
            AssertMutable();
            _playedVoiceMask = value;
        }
    }

    internal static SGR_EmperorsFragment CreateFrom(SGR_GetterFurnace getterFurnace)
    {
        var fragment = (SGR_EmperorsFragment)ModelDb.Relic<SGR_EmperorsFragment>().ToMutable();
        fragment.PlayedVoiceMask = getterFurnace.PlayedVoiceMask;
        return fragment;
    }

    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<SGP_ShinGetterOne>(
            new ThrowingPlayerChoiceContext(), Owner.Creature, 1m, Owner.Creature, null);
        await PowerCmd.Apply<SGP_Ki>(
            new ThrowingPlayerChoiceContext(), Owner.Creature,
            DynamicVars["SGP_Ki"].BaseValue, Owner.Creature, null);
    }
}
