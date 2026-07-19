#nullable enable
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 不屈。将下一次实际生命损失转移到延时伤害队列。
/// </summary>
public sealed class SGP_Indomitable : PowerModel
{
    private class Data
    {
        public int pendingDamage;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new Data();

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0m)
            return amount;

        int delayedDamage = (int)amount;
        if (delayedDamage <= 0 || Amount <= 0)
            return amount;

        GetInternalData<Data>().pendingDamage += delayedDamage;
        return 0m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        var data = GetInternalData<Data>();
        int delayedDamage = data.pendingDamage;
        if (delayedDamage <= 0)
            return;

        data.pendingDamage = 0;
        Flash();
        await SGP_DelayDamage.AddPending(Owner, delayedDamage);
        await PowerCmd.Decrement(this);
    }
}
