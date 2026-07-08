using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_KenIshikawaManuscript : ShinGetterRelicBase
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override bool HasUponPickupEffect => true;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        HoverTipFactory.FromCardWithCardHoverTips<SGC_InfiniteEvolution>();

    public override async Task AfterObtained()
    {
        CardModel card = Owner.RunState.CreateCard<SGC_InfiniteEvolution>(Owner);
        CardCmd.PreviewCardPileAdd(
            new CardPileAddResult[] { await CardPileCmd.Add(card, PileType.Deck) },
            2f);
    }
}
