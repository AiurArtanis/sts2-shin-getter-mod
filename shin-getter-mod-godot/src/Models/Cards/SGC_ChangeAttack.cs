using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 变形攻击 | 攻击 | 罕见 | X费 | 变形流
/// 变形 X 次，每次造成 7 伤害
/// </summary>
public sealed class SGC_ChangeAttack : ShinGetterCardBase
{
    internal const float TransformSpeedScale = 1.5f;

    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(7m, ValueProp.Move) };

    public SGC_ChangeAttack()
        : base(-1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        int x = ResolveEnergyXValue() + (IsUpgraded ? 1 : 0);
        for (int i = 0; i < x; i++)
        {
            await Transform(choiceContext, Owner, this);
            await PlayAcceleratedFollowupAnimation();
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
                .Targeting(cardPlay.Target).WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
