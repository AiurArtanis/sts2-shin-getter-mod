#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_PetalBreakthrough : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(7m, ValueProp.Move),
        new IntVar("Times", 1m),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(new IHoverTip[]
    {
        HoverTipFactory.Static(StaticHoverTip.ReplayDynamic, DynamicVars["Times"]),
    });

    public SGC_PetalBreakthrough()
        : base(1, CardType.Attack, CardRarity.Event, TargetType.AnyEnemy, false)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int vigorToConsume = CaptureVigorForManualAttack(ValueProp.Move);
        await CreatureCmd.Damage(
            choiceContext,
            cardPlay.Target!,
            DynamicVars.Damage,
            Owner.Creature,
            this);
        await ConsumeCapturedVigor(choiceContext, vigorToConsume);
        if (Owner.Creature.GetPower<SGP_HotBlood>() is { } hotBlood)
            await hotBlood.ConsumeForCardDamage(choiceContext, this, ValueProp.Move);

        List<CardModel> candidates = PileType.Draw.GetPile(Owner).Cards
            .Where(card => card.Type == CardType.Attack && card.GetEnchantedReplayCount() < 1)
            .ToList();
        CardModel? selected = Owner.RunState.Rng.CombatCardSelection.NextItem(candidates);
        if (selected != null)
        {
            selected.BaseReplayCount += DynamicVars["Times"].IntValue;
            CardCmd.Preview(selected);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Times"].UpgradeValueBy(1m);
    }
}
