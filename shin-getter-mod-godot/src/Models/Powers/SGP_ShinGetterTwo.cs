#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Patches;

namespace ShinGetterMod.Models.Powers;

/// <summary>
/// 二号机形态。变形时获得1再生，+1能量+1抽牌，格挡减半。
/// </summary>
public sealed class SGP_ShinGetterTwo : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        System.Array.Empty<DynamicVar>();

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner != null && Amount > 0)
        {
            await PowerCmd.Apply<RegenPower>(new ThrowingPlayerChoiceContext(), Owner, 1m, Owner, null);
            float speedScale = cardSource is SGC_ChangeAttack ? SGC_ChangeAttack.TransformSpeedScale : 1f;
            await NShinGetterStaticVisuals.ShowForm(Owner, ShinGetterForm.Getter2, speedScale: speedScale);
            ShinGetterCardFramePatch.RefreshVisibleCards();
        }
    }

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner.Player)
            return amount;

        return amount + 1m;
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        return player.Creature == Owner ? count + 1m : count;
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        // 只削减来自卡牌的格挡；覆甲、遗物等非卡牌格挡不受影响。
        if (target == base.Owner && (cardSource != null || cardPlay != null))
            return 0.5m;
        return 1m;
    }
}
