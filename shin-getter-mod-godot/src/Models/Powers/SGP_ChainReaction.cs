#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 连锁反应。失去活力时获得再生+覆甲。
/// （通过 VigorPowerSetAmountPatch 监听内置 VigorPower 的层数变化实现）
/// </summary>
public sealed class SGP_ChainReaction : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public void FlashTrigger() => Flash();
}
