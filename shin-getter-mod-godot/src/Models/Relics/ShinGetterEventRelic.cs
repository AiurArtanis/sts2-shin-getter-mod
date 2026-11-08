using MegaCrit.Sts2.Core.Entities.Relics;

namespace ShinGetterMod.Models.Relics;

public abstract class ShinGetterEventRelic : ShinGetterRelicBase
{
    public override RelicRarity Rarity => RelicRarity.Event;
}
