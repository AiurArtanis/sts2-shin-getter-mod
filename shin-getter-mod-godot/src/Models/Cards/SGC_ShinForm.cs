using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Audio;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Patches;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 真化形态 | 技能 | 先古 | 4费
/// 变形为真盖塔龙，同时视作 3 个形态；每获得过1层进化减少1耗能。
/// </summary>
public sealed class SGC_ShinForm : ShinGetterCardBase
{
    protected override float ActionAnimationSpeedScale => 0.33f;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_single/shin_getter/s_g_c_shin_form_card.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths => new[]
    {
        PortraitPath,
        ImageHelper.GetImagePath("packed/card_portraits/shin_getter/s_g_c_shin_form.png"),
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => System.Array.Empty<DynamicVar>();

    public SGC_ShinForm()
        : base(4, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
    }

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || originalCost <= 0m)
            return false;

        int discount = Owner.Creature.GetPower<SGP_EvolutionMemory>()?.Amount ?? 0;
        if (discount <= 0)
            return false;

        modifiedCost = Math.Max(0m, originalCost - discount);
        return modifiedCost != originalCost;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, this) && cardPlay.PlayIndex == 0)
            ShinGetterVoiceService.PlayShinDragonTransform(Owner);

        await base.BeforeCardPlayed(cardPlay);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = base.Owner.Creature;
        if (creature.GetPower<SGP_Seal>() is { } seal)
        {
            seal.FlashBlockedTransform();
            return;
        }

        await NShinGetterStaticVisuals.PlayShinFormTransformVfx(creature);

        ShinGetterCardFramePatch.BeginFormTransitionToShinDragon();
        try
        {
            // 移除当前所有形态
            var one = creature.GetPower<SGP_ShinGetterOne>();
            var two = creature.GetPower<SGP_ShinGetterTwo>();
            var three = creature.GetPower<SGP_ShinGetterThree>();
            var shin = creature.GetPower<SGP_ShinForm>();

            if (one != null) await PowerCmd.Remove(one);
            if (two != null) await PowerCmd.Remove(two);
            if (three != null) await PowerCmd.Remove(three);
            if (shin != null) await PowerCmd.Remove(shin);

            // 变形为真化形态
            await PowerCmd.Apply<SGP_ShinForm>(choiceContext, creature, 1m, creature, this);
        }
        finally
        {
            ShinGetterCardFramePatch.EndFormTransitionAndRefresh();
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
