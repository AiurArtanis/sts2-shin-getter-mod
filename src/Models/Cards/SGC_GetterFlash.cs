using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.Powers;
using System.Linq;
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

        var attack = await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
            .WithNoAttackerAnim()
            .Targeting(cardPlay.Target)
            .BeforeDamage(async () =>
            {
                ShinGetterVoiceService.TryPlayCardVoice(this);
                await ShinGetterCombatVfx.PlayFlashRush(Owner.Creature, cardPlay.Target);
                NShinGetterStaticVisuals.QueueNextActionSpeed(Owner.Creature, 1.75f);
                NShinGetterStaticVisuals.TryPlayCreatureActionAnimation(Owner.Creature, "Attack");
                await Cmd.CustomScaledWait(0.18f, 0.22f);
            })
            .Execute(choiceContext);

        decimal damageDealt = attack.Results.SelectMany(results => results).Sum(result => result.UnblockedDamage);
        if (damageDealt > 0m)
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, damageDealt, Owner.Creature, this);
        if (HasForm(Owner, ShinGetterForm.Getter1))
        {
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, 2m, Owner.Creature, this);
            await PowerCmd.Apply<SGP_Airborne>(choiceContext, Owner.Creature, 2m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
