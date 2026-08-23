using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 盖塔闪光 | 攻击 | 罕见 | 1费 | 一号/输出终端
/// 造成 5 伤害，获得等同于造成伤害的活力，消耗，固有
/// 一号机加成：获得 2 活力和 2 腾空
/// </summary>
public sealed class SGC_GetterFlash : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Innate, CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(5m, ValueProp.Move) };

    public SGC_GetterFlash()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
            .WithNoAttackerAnim()
            .Targeting(cardPlay.Target)
            .BeforeDamage(async () =>
            {
                ShinGetterVoiceService.TryPlayCardVoice(this);
                await NShinGetterStaticVisuals.PlayPhasedCreatureActionAnimation(
                    Owner.Creature,
                    GetActionAnimationTrigger() ?? "Attack",
                    0.75f,
                    2f,
                    () => ShinGetterCombatVfx.PlayFlashRush(Owner.Creature, cardPlay.Target),
                    0.74f);
            })
            .Execute(choiceContext);
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature dealer,
        MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result,
        ValueProp props,
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target,
        CardModel cardSource)
    {
        if (!ReferenceEquals(cardSource, this) || dealer != Owner.Creature)
            return;

        bool hasGetterOneBonus = HasForm(Owner, ShinGetterForm.Getter1);
        decimal vigorGain = result.UnblockedDamage + (hasGetterOneBonus ? 2m : 0m);
        if (vigorGain > 0m)
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, vigorGain, Owner.Creature, this);

        if (hasGetterOneBonus)
        {
            await PowerCmd.Apply<SGP_Airborne>(choiceContext, Owner.Creature, 2m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
