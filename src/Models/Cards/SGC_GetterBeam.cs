#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Nodes.Vfx;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_GetterBeam : ShinGetterCardBase
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[] { HoverTipFactory.FromPower<SGP_Wane>() });
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new CalculationBaseVar(9m),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(GetGetterBeamBonus),
        new DynamicVar("Wane", 1m),
    };

    public SGC_GetterBeam()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this).Targeting(cardPlay.Target)
            .WithAttackerAnim("Cast", 0.5f)
            .BeforeDamage(() => ShinGetterBeamVfx.Play(Owner.Creature, new[] { cardPlay.Target }, ShinGetterBeamStyle.GetterBeam))
            .Execute(choiceContext);

        if (cardPlay.Target.IsAlive)
        {
            await PowerCmd.Apply<SGP_Wane>(
                choiceContext,
                cardPlay.Target,
                DynamicVars["Wane"].BaseValue,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.CalculationBase.UpgradeValueBy(3m);
    }

    private static decimal GetGetterBeamBonus(CardModel card, Creature? _) =>
        IsInForm(card.Owner, ShinGetterForm.Getter1)
            ? CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
                .Where(entry => entry.Actor == card.Owner.Creature && entry.Power is VigorPower && entry.Amount > 0)
                .Sum(entry => entry.Amount)
            : 0m;
}
