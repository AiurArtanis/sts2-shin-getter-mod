#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 辐射。每层使受到的伤害增加 25%。
/// </summary>
public sealed class SGP_Radiation : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => (PowerStackType)1;
    public override LocString Description
    {
        get
        {
            LocString description = base.Description;
            description.Add("DamageIncreasePercent", Amount * 25m);
            return description;
        }
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override Task BeforeDamageReceived(PlayerChoiceContext choiceContext, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && amount > 0m && Amount > 0 && !IsHpLoss(props))
            Flash();
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (IsHpLoss(props))
            return 1m;
        if (target == base.Owner && target.Player?.GetRelic<SGR_ResearchNotes>() != null)
            return 1m;
        if (target == base.Owner && base.Amount > 0)
            return 1m + base.Amount * 0.25m;
        return 1m;
    }

    private static bool IsHpLoss(ValueProp props)
    {
        return props.HasFlag(ValueProp.Unblockable) && props.HasFlag(ValueProp.Unpowered);
    }
}
