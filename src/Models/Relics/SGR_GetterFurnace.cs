using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Relics;

/// <summary>
/// 盖塔熔炉 — 起始遗物。在每场战斗开始时获得气力，并初始化一号机形态。
/// </summary>
public sealed class SGR_GetterFurnace : ShinGetterRelicBase
{
	private int _playedVoiceMask;

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<SGP_Ki>(1m) };

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
