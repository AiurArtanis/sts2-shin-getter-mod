using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Spirit : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public override int SpiritRequirement => 3;
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<VigorPower>(5m),
        new CardsVar(1),
    };

    public SGC_Spirit()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await ShinGetterCombatVfx.PlaySpiritAura(Owner.Creature);
        await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, DynamicVars["VigorPower"].BaseValue, Owner.Creature, this);
        int max = System.Math.Min(DynamicVars.Cards.IntValue, PileType.Hand.GetPile(Owner).Cards.Count(card => card != this));
        if (max > 0)
        {
            var selected = await CardSelectCmd.FromHand(choiceContext, Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 0, max), card => card != this, this);
            foreach (var card in selected.ToList())
                await CardCmd.Transform(card, CreateKiCard());
        }
    }

    protected override void OnUpgrade()
    {
    }

    private CardModel CreateKiCard()
    {
        CardModel ki = CardScope.CreateCard<SGC_Ki>(Owner);
        if (IsUpgraded)
            CardCmd.Upgrade(ki);
        return ki;
    }
}
