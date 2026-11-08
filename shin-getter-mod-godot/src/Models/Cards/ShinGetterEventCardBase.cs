using MegaCrit.Sts2.Core.Entities.Cards;

namespace ShinGetterMod.Models.Cards;

public abstract class ShinGetterEventCardBase : ShinGetterCardBase
{
    protected ShinGetterEventCardBase(int cost, CardType type, TargetType target)
        : base(cost, type, CardRarity.Event, target, false) { }

    // Each event card uses its own native atlas entry and full portrait.
    public override string BetaPortraitPath => PortraitPath;
}
