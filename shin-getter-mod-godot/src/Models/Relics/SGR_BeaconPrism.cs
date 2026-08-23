#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.CardPools;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_BeaconPrism : ShinGetterRelicBase
{
    private bool _availableThisTurn;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool ShowCounter => CombatManager.Instance.IsInProgress;

    public override int DisplayAmount => AvailableThisTurn ? 1 : 0;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            new HoverTip(
                new LocString("static_hover_tips", "SHIN_GETTER_BEACON_PRISM_COLOR.title"),
                new LocString("static_hover_tips", "SHIN_GETTER_BEACON_PRISM_COLOR.description")),
        };

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool AvailableThisTurn
    {
        get => _availableThisTurn;
        set
        {
            AssertMutable();
            _availableThisTurn = value;
            Status = value ? RelicStatus.Active : RelicStatus.Disabled;
            InvokeDisplayAmountChanged();
        }
    }

    public override Task BeforeCombatStart()
    {
        AvailableThisTurn = true;
        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (participants.Contains(Owner.Creature))
            AvailableThisTurn = true;

        return Task.CompletedTask;
    }

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        if (!AvailableThisTurn
            || Owner.Creature.IsDead
            || card.Owner != Owner
            || Owner.PlayerCombatState?.Phase != PlayerTurnPhase.Play
            || oldPileType != PileType.Draw
            || card.Pile?.Type != PileType.Hand
            || HasGetterLineColor(card))
        {
            return;
        }

        AvailableThisTurn = false;
        Flash();
        await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, Owner);
    }

    public override Task AfterCombatEnd(CombatRoom _)
    {
        AvailableThisTurn = false;
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    private static bool HasGetterLineColor(CardModel card) =>
        card.Pool is ShinGetterCardPool
        && (card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare
            || card is SGC_Strike or SGC_Defend or SGC_GetterBeam or SGC_GetterLaunch);
}
