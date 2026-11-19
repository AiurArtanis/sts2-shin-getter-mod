using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Nodes.Vfx;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 膨胀打击 | 攻击 | 罕见 | 2费 | 三号/防御终端
/// 按自身与目标的异常状态种类数之和，每种造成 5 伤害
/// 三号机加成：按同一合计数，每种获得 2 覆甲
/// </summary>
public sealed class SGC_ExpansionStrike : ShinGetterCardBase
{
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(5m, ValueProp.Move) };

    public SGC_ExpansionStrike()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        int debuffTypes = Owner.Creature.Powers.Count(power => power.Type == PowerType.Debuff)
            + cardPlay.Target.Powers.Count(power => power.Type == PowerType.Debuff);
        // The supplied FireNow WAV has a phrase gap ending at approximately 0.86s.
        // Do not hold silent/consumed/wrong-form voices behind an artificial lead-in.
        if (ShinGetterVoiceService.TryPlayCardVoiceAtCustomTiming(this, out float duration) && duration > 0f)
            await Cmd.CustomScaledWait(Math.Min(0.86f, duration), Math.Min(0.86f, duration));
        await PlayMovementVfx(() => ShinGetterCombatVfx.PlayExpansionRush(
            Owner.Creature, cardPlay.Target,
            async () => await DamageCmd.Attack(DynamicVars.Damage.BaseValue * debuffTypes).FromCard(this, cardPlay)
                .Targeting(cardPlay.Target).WithNoAttackerAnim()
                .WithHitFx("vfx/vfx_attack_blunt").Execute(choiceContext)));
        if (debuffTypes > 0 && HasForm(Owner, ShinGetterForm.Getter3))
            await PowerCmd.Apply<PlatingPower>(choiceContext, Owner.Creature, debuffTypes * 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
