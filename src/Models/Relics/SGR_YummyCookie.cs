using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_YummyCookie : ShinGetterRelicBase
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CardsVar(4),
    };

    public override async Task AfterObtained()
    {
        List<CardModel> selected = (await CardSelectCmd.FromDeckForUpgrade(
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, DynamicVars.Cards.IntValue))).ToList();

        foreach (CardModel card in selected)
            CardCmd.Upgrade(card);
    }
}
