using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Cards;

// Artanis will supply final art. Reuse a known shipped portrait without adding generated art.
public abstract class ShinGetterEventCardBase : ShinGetterCardBase
{
    protected ShinGetterEventCardBase(int cost, CardType type, TargetType target)
        : base(cost, type, CardRarity.Event, target, false) { }

    public override string PortraitPath => ModelDb.Card<SGC_RescheduleTicket>().PortraitPath;
    public override string BetaPortraitPath => PortraitPath;
    public override IEnumerable<string> AllPortraitPaths => new[] { PortraitPath };
}
