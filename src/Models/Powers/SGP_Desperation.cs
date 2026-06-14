#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 绝境。战斗结束回复等同于层数的生命。
/// </summary>
public sealed class SGP_Desperation : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    private bool _spiritCardsAreFree = true;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!_spiritCardsAreFree || card.Owner?.Creature != Owner || card is not ShinGetterMod.Models.Cards.ShinGetterCardBase getterCard || getterCard.SpiritRequirement <= 0)
            return false;

        modifiedCost = 0m;
        return true;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
            _spiritCardsAreFree = false;
        return Task.CompletedTask;
    }

    public override async Task AfterCombatVictory(CombatRoom _)
    {
        if (!base.Owner.IsDead && base.Amount > 0)
        {
            await CreatureCmd.Heal(base.Owner, base.Amount);
        }
    }
}
