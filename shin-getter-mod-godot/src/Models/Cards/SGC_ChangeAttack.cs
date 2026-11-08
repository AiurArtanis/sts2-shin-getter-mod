using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 变形攻击 | 攻击 | 罕见 | X费 | 变形流
/// 变形 X 次，每次造成 7 伤害
/// </summary>
public sealed class SGC_ChangeAttack : ShinGetterCardBase
{
    internal const float TransformSpeedScale = 2f;

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
        var voicedForms = new HashSet<ShinGetterForm>();
        for (int i = 0; i < x; i++)
        {
            if (!CanContinueChangeAttack(cardPlay.Target))
                break;

            // Local to this play, including replays: Always voice mode must not repeat
            // the same form's line within a long X-cost transformation chain.
            ShinGetterForm[] forms = GetCurrentForms(Owner);
            ShinGetterForm nextVoiceForm = forms.Length > 1 ? ShinGetterForm.None
                : forms.Length == 1 && forms[0] == ShinGetterForm.Getter1 ? ShinGetterForm.Getter2
                : forms.Length == 1 && forms[0] == ShinGetterForm.Getter2 ? ShinGetterForm.Getter3
                : ShinGetterForm.Getter1;
            await Transform(choiceContext, Owner, this, playVoice: voicedForms.Add(nextVoiceForm));
            if (!CanContinueChangeAttack(cardPlay.Target))
                break;

            var attack = DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
                .Targeting(cardPlay.Target).WithNoAttackerAnim().WithHitFx("vfx/vfx_attack_slash");
            await NShinGetterStaticVisuals.PlayCompleteCreatureAttack(Owner.Creature,
                () => CanContinueChangeAttack(cardPlay.Target)
                    ? attack.Execute(choiceContext) : Task.CompletedTask,
                speedScale: i < x - 1 ? 2f : 1f);
        }
    }

    private bool CanContinueChangeAttack(Creature target) =>
        CombatManager.Instance.IsInProgress
        && !CombatManager.Instance.IsOverOrEnding
        && !Owner.Creature.IsDead
        && target.IsHittable;

    protected override void OnUpgrade()
    {
    }
}
