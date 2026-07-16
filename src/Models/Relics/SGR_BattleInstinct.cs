using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_BattleInstinct : ShinGetterRelicBase
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool IsUsedUp => TriggeredThisCombat;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new PowerVar<VigorPower>(5m) };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[] { HoverTipFactory.FromPower<VigorPower>() };

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool TriggeredThisCombat
    {
        get => _triggeredThisCombat;
        set
        {
            AssertMutable();
            _triggeredThisCombat = value;
            Status = _triggeredThisCombat ? RelicStatus.Disabled : RelicStatus.Normal;
        }
    }

    public override Task BeforeCombatStart()
    {
        TriggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        TriggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public async Task OnTransform(Creature creature)
    {
        if (creature != Owner.Creature || TriggeredThisCombat)
            return;

        Flash();
        TriggeredThisCombat = true;
        await PowerCmd.Apply<VigorPower>(
            new ThrowingPlayerChoiceContext(),
            Owner.Creature,
            DynamicVars["VigorPower"].BaseValue,
            Owner.Creature,
            null);
    }
}
