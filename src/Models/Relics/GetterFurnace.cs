using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Relics;

/// <summary>
/// 盖塔熔炉 — 起始遗物。在每场战斗开始时，获得 1 气力。
/// </summary>
public sealed class GetterFurnace : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Creature.Side && combatState.RoundNumber == 1)
        {
            Flash();
            // TODO: 获得 1 气力 (FightingSpiritPower)
            await Task.CompletedTask;
        }
    }
}
