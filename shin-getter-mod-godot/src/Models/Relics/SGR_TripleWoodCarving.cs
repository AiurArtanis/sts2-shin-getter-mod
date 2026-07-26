using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_TripleWoodCarving : ShinGetterRelicBase
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task BeforeCombatStart()
    {
        Flash();
        int transformCount = Owner.RunState.Rng.Niche.NextInt(1, 4);
        var choiceContext = new ThrowingPlayerChoiceContext();

        for (int i = 0; i < transformCount; i++)
            await ShinGetterCardBase.Transform(choiceContext, Owner, null);
    }
}
