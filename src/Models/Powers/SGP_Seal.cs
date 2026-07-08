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

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 封印。本回合不会增减状态层数。
/// </summary>
public sealed class SGP_Seal : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public void FlashBlockedTransform() => Flash();

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        if (target != Owner)
        {
            modifiedAmount = amount;
            return false;
        }

        // 允许封印自身到期衰减；其他可见状态的层数变化都保持不变。
        if (canonicalPower is SGP_Seal && amount < 0m)
        {
            modifiedAmount = amount;
            return false;
        }

        if (amount > 0m && canonicalPower.IsVisible && target.GetPower(canonicalPower.Id) == null)
        {
            modifiedAmount = amount;
            return false;
        }

        if (amount != 0m && canonicalPower.IsVisible)
        {
            Flash();
            modifiedAmount = 0m;
            return true;
        }
        modifiedAmount = amount;
        return false;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
            await PowerCmd.Decrement(this);
    }
}
