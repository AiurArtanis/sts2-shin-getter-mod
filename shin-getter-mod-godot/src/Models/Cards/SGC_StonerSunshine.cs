#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Helpers;
using ShinGetterMod.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Audio;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 烈阳闪光弹 | 攻击 | 先古 | 1费
/// 对所有敌人造成 10 伤害，给予 2 衰退，额外造成本场战斗获得的活力伤害
/// </summary>
public sealed class SGC_StonerSunshine : ShinGetterCardBase
{
    public override bool CanBeGeneratedInCombat => false;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_single/shin_getter/s_g_c_stoner_sunshine_card.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths => new[]
    {
        PortraitPath,
        ImageHelper.GetImagePath("packed/card_portraits/shin_getter/s_g_c_stoner_sunshine.png"),
    };

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(10m),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(GetVigorGainedBonus),
        new DynamicVar("Wane", 2m),
    };

    public SGC_StonerSunshine()
        : base(1, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(CombatState);
        var combatState = CombatState;

        if (HasForm(Owner, ShinGetterForm.Getter1))
        {
            float sequenceDurationSeconds = 4f;
            if (ShinGetterVoiceService.TryPlayCardVoiceAtCustomTiming(this, out float voiceDurationSeconds)
                && voiceDurationSeconds > 0f)
            {
                sequenceDurationSeconds = voiceDurationSeconds;
            }

            NShinGetterStaticVisuals.QueueNextActionSpeed(Owner.Creature, 0.3f);
            NShinGetterStaticVisuals.TryPlayCreatureActionAnimation(Owner.Creature, "Cast");

            await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this)
                .TargetingAllOpponents(combatState)
                .WithNoAttackerAnim()
                .BeforeDamage(() => ShinGetterCombatVfx.PlayStonerSunshine(
                    Owner.Creature,
                    combatState.GetOpponentsOf(Owner.Creature),
                    sequenceDurationSeconds))
                .WithHitFx("vfx/vfx_starry_impact").Execute(choiceContext);
        }
        else
        {
            NShinGetterStaticVisuals.TryPlayCreatureActionAnimation(Owner.Creature, "Cast");

            await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this)
                .TargetingAllOpponents(combatState)
                .WithNoAttackerAnim()
                .BeforeDamage(() => ShinGetterCombatVfx.PlayEnergyBall(
                    Owner.Creature,
                    combatState.GetOpponentsOf(Owner.Creature)))
                .WithHitFx("vfx/vfx_starry_impact").Execute(choiceContext);
        }

        foreach (var enemy in combatState.GetOpponentsOf(Owner.Creature).Where(creature => creature.IsAlive))
            await PowerCmd.Apply<SGP_Wane>(choiceContext, enemy, DynamicVars["Wane"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(5m);
        DynamicVars["Wane"].UpgradeValueBy(1m);
    }

    private static decimal GetVigorGainedBonus(CardModel card, MegaCrit.Sts2.Core.Entities.Creatures.Creature? _) =>
        CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
            .Where(entry => entry.Actor == card.Owner.Creature && entry.Power is VigorPower && entry.Amount > 0)
            .Sum(entry => entry.Amount);
}
