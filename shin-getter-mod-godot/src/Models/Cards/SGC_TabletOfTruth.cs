#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Services;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_TabletOfTruth : ShinGetterEventCardBase
{
    public SGC_TabletOfTruth() : base(2, CardType.Power, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new HealVar(6m) };

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal heal = DynamicVars["Heal"].BaseValue; // This power itself is in the affected Play pile.
        ShinGetterCombatCardChanges.ChangeUpgradeLevels(Owner, upgrade: false);
        await CreatureCmd.Heal(Owner.Creature, heal);
    }

    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (ReferenceEquals(card, this))
            ShinGetterCombatCardChanges.ChangeUpgradeLevels(Owner, upgrade: true);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Heal"].UpgradeValueBy(3m);
}
