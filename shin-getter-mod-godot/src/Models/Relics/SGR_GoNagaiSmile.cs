using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_GoNagaiSmile : ShinGetterRelicBase
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeCombatStart()
    {
        _triggeredThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player != Owner || _triggeredThisCombat)
            return;

        List<ShinGetterCardBase> options = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .OfType<ShinGetterCardBase>()
            .Where(card => card.SpiritRequirement > 0 && card.CanBeGeneratedInCombat)
            .ToList();

        if (options.Count == 0)
            return;

        Flash();
        _triggeredThisCombat = true;
        CardModel card = combatState.CreateCard(
            Owner.RunState.Rng.CombatCardGeneration.NextItem(options),
            Owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
    }
}
