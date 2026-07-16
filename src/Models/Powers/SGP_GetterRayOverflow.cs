#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 盖塔射线爆发。名字中有“盖塔”的卡牌费用降低；每打出一张盖塔卡获得进化。
/// </summary>
public sealed class SGP_GetterRayOverflow : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => (PowerStackType)1;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    private static bool IsGetterCard(CardModel card) =>
        card.GetType().Name.Contains("Getter", System.StringComparison.Ordinal);

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        if (IsGetterCard(card))
        {
            modifiedCost = System.Math.Max(0m, originalCost - Amount);
            return true;
        }
        modifiedCost = originalCost;
        return false;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel card = cardPlay.Card;
        if (Amount <= 0
            || card.Owner.Creature != Owner
            || !IsGetterCard(card))
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<SGP_Evolution>(
            choiceContext,
            Owner,
            Amount,
            Owner,
            card);
    }
}
