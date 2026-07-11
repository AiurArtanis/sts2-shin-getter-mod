#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_HolyDragonRoar : ShinGetterCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_single/shin_getter/s_g_c_saint_dragon_roar_card.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<string> AllPortraitPaths => new[]
    {
        PortraitPath,
        ImageHelper.GetImagePath("packed/card_portraits/shin_getter/s_g_c_saint_dragon_roar.png"),
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        WithContextualHoverTips(new IHoverTip[] { StunIntent.GetStaticHoverTip() });

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(20m, ValueProp.Move) };

    public SGC_HolyDragonRoar()
        : base(3, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatState
            ?? throw new InvalidOperationException("Holy Dragon Roar requires an active combat state.");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
            .TargetingAllOpponents(combatState)
            .BeforeDamage(() => ShinGetterCombatVfx.PlayHolyDragonRoar(Owner.Creature))
            .WithHitFx("vfx/vfx_starry_impact")
            .Execute(choiceContext);

        foreach (var enemy in combatState.GetOpponentsOf(Owner.Creature).Where(enemy => enemy.IsAlive))
            await CreatureCmd.Stun(enemy);

        List<CardModel> getterCards = PileType.Hand.GetPile(Owner).Cards
            .Where(card => card != this && IsGetterCard(card))
            .ToList();
        foreach (CardModel card in getterCards)
            await CardCmd.Exhaust(choiceContext, card);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(10m);
    }

    private static bool IsGetterCard(CardModel card) =>
        card.Id.Entry.Contains("GETTER", StringComparison.Ordinal);
}
