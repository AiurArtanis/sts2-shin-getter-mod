#nullable enable
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 终极盖塔射线。将衰退在受伤后的增长量改为该状态的层数。
/// </summary>
public sealed class SGP_FinalGetterBeam : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}
