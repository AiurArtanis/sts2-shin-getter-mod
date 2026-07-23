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
using System.Linq;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_GetterMissile : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(4m, ValueProp.Move) };

    public SGC_GetterMissile()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShinGetterCombatVfx.PlayBurningGrowl(Owner.Creature);
        for (int i = 0; i < 4; i++)
        {
            if (CombatManager.Instance.IsOverOrEnding || !HasHittableEnemyTargets())
                break;

            var candidates = GetHittableMissileTargets();
            if (candidates.Count == 0)
                break;

            var target = Owner.RunState.Rng.CombatTargets.NextItem(candidates);
            var dealer = target == Owner.Creature ? null : Owner.Creature;
            var results = await CreatureCmd.Damage(
                choiceContext,
                target,
                DynamicVars.Damage.BaseValue,
                ValueProp.Move,
                dealer,
                this);
            if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead)
                break;

            if (target == Owner.Creature && HasForm(Owner, ShinGetterForm.Getter3))
            {
                decimal damageTaken = results.Sum(result => result.TotalDamage);
                if (damageTaken > 0)
                    await CreatureCmd.GainBlock(Owner.Creature, damageTaken, ValueProp.Unpowered, cardPlay);
            }

            if (i < 3 && HasHittableEnemyTargets())
                await PlayAcceleratedFollowupAnimation();
        }
    }

    private bool HasHittableEnemyTargets() =>
        CombatState.GetOpponentsOf(Owner.Creature).Any(creature => creature.IsHittable);

    private List<Creature> GetHittableMissileTargets() =>
        CombatState.Creatures.Where(creature => creature.IsHittable).ToList();

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeValueBy(2m);
    }
}
