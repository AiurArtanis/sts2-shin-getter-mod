using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Relics;

public abstract class ShinGetterPlaceholderEventRelic : ShinGetterRelicBase
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public override string PackedIconPath => ModelDb.Relic<SGR_ResearchNotes>().PackedIconPath;
    protected override string PackedIconOutlinePath => PackedIconPath.Replace("relic_atlas.sprites", "relic_outline_atlas.sprites");
    protected override string BigIconPath => "res://images/relics/s_g_r_research_notes.png";
}
