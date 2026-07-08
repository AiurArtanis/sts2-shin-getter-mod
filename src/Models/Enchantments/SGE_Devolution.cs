using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace ShinGetterMod.Models.Enchantments;

public sealed class SGE_Devolution : EnchantmentModel
{
    public override bool HasExtraCardText => true;

    public override bool CanEnchantCardType(CardType cardType)
    {
        return cardType == CardType.Attack;
    }

    public override bool CanEnchant(CardModel card)
    {
        return base.CanEnchant(card) && !card.EnergyCost.CostsX;
    }

    protected override void OnEnchant()
    {
        Card.EnergyCost.UpgradeBy(-1);
    }

    public override decimal EnchantDamageMultiplicative(decimal originalDamage, ValueProp props)
    {
        if (!props.IsPoweredAttack())
            return 1m;

        return 0.5m;
    }
}
