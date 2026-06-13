#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
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
    private sealed class Data
    {
        public int TemporaryDexterity;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override System.Collections.Generic.IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    protected override object InitInternalData() => new Data();

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(base.Owner) || combatState == null) return;

        // 检查是否有敌人有攻击意图
        bool intendsToAttack = combatState.HittableEnemies
            .Any(e => e.IsAlive && e.Monster?.IntendsToAttack == true);

        if (intendsToAttack)
        {
            var data = GetInternalData<Data>();
            data.TemporaryDexterity += Amount;
            await PowerCmd.Apply<DexterityPower>(
                new ThrowingPlayerChoiceContext(),
                Owner,
                Amount,
                Owner,
                null);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
            return;

        await RemoveTemporaryDexterity(choiceContext, Owner);
    }

    public override async Task AfterRemoved(Creature owner)
    {
        await RemoveTemporaryDexterity(new ThrowingPlayerChoiceContext(), owner);
    }

    private async Task RemoveTemporaryDexterity(PlayerChoiceContext choiceContext, Creature owner)
    {
        var data = GetInternalData<Data>();
        if (data.TemporaryDexterity <= 0)
            return;

        int amount = data.TemporaryDexterity;
        data.TemporaryDexterity = 0;
        await PowerCmd.Apply<DexterityPower>(choiceContext, owner, -amount, owner, null);
    }
}
