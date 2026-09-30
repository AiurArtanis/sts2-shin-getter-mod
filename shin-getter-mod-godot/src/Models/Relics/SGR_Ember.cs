using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Services;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_Ember : ShinGetterPlaceholderEventRelic
{
    private bool _used;
    public override Task BeforeCombatStart()
    {
        _used = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterCardDrawnEarly(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (_used || card.Owner != Owner || card.Pile?.Type != PileType.Hand
            || card.Type is not (CardType.Attack or CardType.Skill)
            || !card.Keywords.Contains(CardKeyword.Exhaust)) return Task.CompletedTask;
        _used = true;
        Status = RelicStatus.Disabled;
        Flash();
        ShinGetterCombatCardChanges.RememberRemovedExhaust(card);
        CardCmd.RemoveKeyword(card, CardKeyword.Exhaust);
        return Task.CompletedTask;
    }
}
