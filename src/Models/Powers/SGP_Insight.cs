#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 识破。回合开始受到攻击意图时，本回合获得敏捷。
/// </summary>
public sealed class SGP_Insight : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(base.Owner) || combatState == null) return;

        // 检查是否有敌人有攻击意图
        bool intendsToAttack = combatState.HittableEnemies
            .Any(e => e.IsAlive && e.Monster?.IntendsToAttack == true);

        if (intendsToAttack)
        {
            await MegaCrit.Sts2.Core.Commands.PowerCmd.Apply<DexterityPower>(
                new MegaCrit.Sts2.Core.GameActions.Multiplayer.ThrowingPlayerChoiceContext(),
                base.Owner, base.Amount, base.Owner, null);
        }
    }
}
