#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_TripleWhirlwind : ShinGetterEventCardBase
{
    public SGC_TripleWhirlwind() : base(3, CardType.Attack, TargetType.AllEnemies) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(3m, ValueProp.Move), new BlockVar(3m, ValueProp.Move), new PowerVar<SGP_Wane>(3m),
    };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (CombatState == null) return;
        bool isDragon = Owner.Creature.GetPower<SGP_ShinForm>() != null;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(3).FromCard(this)
            .TargetingAllOpponents(CombatState).AfterAttackerAnim(AccelerateFollowupAnimations(3))
            .Execute(choiceContext);
        for (int i = 0; i < 3; i++)
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (isDragon)
        {
            foreach (var enemy in CombatState.HittableEnemies.ToArray())
                await PowerCmd.Apply<SGP_Wane>(choiceContext, enemy, 3m, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
