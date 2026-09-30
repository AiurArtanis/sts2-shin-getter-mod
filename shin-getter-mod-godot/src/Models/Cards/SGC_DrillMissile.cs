using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.HoverTips;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_DrillMissile : ShinGetterEventCardBase
{
    private sealed class PlayState { internal bool Played; }
    private static readonly ConditionalWeakTable<SGC_DrillMissile, PlayState> Plays = new();
    public SGC_DrillMissile() : base(2, CardType.Attack, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DamageVar(25m, ValueProp.Move) };
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(
        HoverTipFactory.FromCardWithCardHoverTips<SGC_BrokenDrill>());

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool secondForm = HasForm(Owner, ShinGetterForm.Getter2);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(cardPlay.Target)
            .Execute(choiceContext);
        if (secondForm)
            await Transform(choiceContext, Owner, this);
        Plays.GetOrCreateValue(this).Played = true;
    }

    internal async Task ReplacePlayedCombatInstance()
    {
        if (!Plays.TryGetValue(this, out var state) || !state.Played) return;
        Plays.Remove(this);
        // The wrapper reads Owner and repeats OnPlay. Replace only after it returns, so replay,
        // enchantment, result-pile and AfterCardPlayed hooks all retain a valid original owner.
        if (Owner == null || CombatState == null || Pile?.IsCombatPile != true) return;
        await CardCmd.TransformTo<SGC_BrokenDrill>(this, CardPreviewStyle.None);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5m);
}
