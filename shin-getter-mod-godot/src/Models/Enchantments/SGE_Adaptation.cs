using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

#nullable enable

namespace ShinGetterMod.Models.Enchantments;

public sealed class SGE_Adaptation : EnchantmentModel
{
    public override bool HasExtraCardText => true;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<VigorPower>(1m),
    };

    public override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay? cardPlay)
    {
        await CreatureCmd.Damage(
            choiceContext,
            Card.Owner.Creature,
            1m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Card,
            cardPlay);

        await DecrementFirstDebuff();

        await PowerCmd.Apply<VigorPower>(
            choiceContext,
            Card.Owner.Creature,
            DynamicVars["VigorPower"].BaseValue,
            Card.Owner.Creature,
            Card);
    }

    private async Task DecrementFirstDebuff()
    {
        PowerModel? debuff = Card.Owner.Creature.Powers
            .FirstOrDefault(power => power.TypeForCurrentAmount == PowerType.Debuff
                && power.StackType == PowerStackType.Counter
                && power.Amount > 0);

        if (debuff != null)
            await PowerCmd.Decrement(debuff);
    }
}
