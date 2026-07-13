using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Powers;

public sealed class SGP_FinalGetterBeamStrengthDown : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Card<SGC_FinalGetterBeam>();

    protected override bool IsPositive => false;
}
