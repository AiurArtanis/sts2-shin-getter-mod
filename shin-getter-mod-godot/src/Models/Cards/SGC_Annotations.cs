#nullable enable
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.HoverTips;
using ShinGetterMod.Services;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Annotations : ShinGetterEventCardBase
{
    private bool _canceled;
    private bool _succeeded;
    internal void BeginPlaySequence() { _canceled = false; _succeeded = false; }
    internal void EndPlaySequence() { _canceled = false; _succeeded = false; }
    public SGC_Annotations() : base(1, CardType.Skill, TargetType.Self) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Enchantment", 2m) };
    protected override bool IsPlayable => Owner?.PlayerCombatState?.Hand.Cards.Any(IsLegalTarget) == true;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => WithContextualHoverTips(
        HoverTipFactory.FromEnchantment<Sharp>()
            .Concat(HoverTipFactory.FromEnchantment<Nimble>())
            .Concat(HoverTipFactory.FromEnchantment<Swift>()));

    private static EnchantmentModel? EnchantmentFor(CardModel card) => card.Type switch
    {
        CardType.Attack => ModelDb.Enchantment<Sharp>(),
        CardType.Skill when card.GainsBlock => ModelDb.Enchantment<Nimble>(),
        CardType.Power => ModelDb.Enchantment<Swift>(),
        _ => null,
    };

    private bool IsLegalTarget(CardModel card) => card != this && card.Owner == Owner
        && EnchantmentFor(card)?.CanEnchant(card) == true;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (_canceled) return; // A canceled replay sequence cannot reopen a selection.
        bool firstForm = HasForm(Owner, ShinGetterForm.Getter1);
        CardModel? target = (await CardSelectCmd.FromHand(choiceContext, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1) { Cancelable = true }, IsLegalTarget, this)).FirstOrDefault();
        if (target == null || target.Pile?.Type != PileType.Hand || !IsLegalTarget(target))
        {
            await CancelPlay(cardPlay);
            return;
        }
        EnchantmentModel? previous = target.Enchantment == null ? null
            : ModelDb.GetById<EnchantmentModel>(target.Enchantment.Id).ToMutable();
        decimal previousAmount = target.Enchantment?.Amount ?? 0m;
        ShinGetterCombatCardChanges.RememberEnchantment(target);
        try { CardCmd.Enchant(EnchantmentFor(target)!.ToMutable(), target, DynamicVars["Enchantment"].BaseValue); }
        catch (Exception ex)
        {
            CardCmd.ClearEnchantment(target);
            if (previous != null) CardCmd.Enchant(previous, target, previousAmount);
            Log.Error($"issue#238 annotations enchantment failed: {ex}");
            await CancelPlay(cardPlay);
            return;
        }
        _succeeded = true;
        if (firstForm)
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, 5m, Owner.Creature, this);
    }

    private async Task CancelPlay(CardPlay cardPlay)
    {
        _canceled = true;
        // A later replay cancel cannot undo an earlier successful enchantment or its Exhaust.
        if (_succeeded) return;
        // Move off Play so the wrapper's captured Exhaust result does not consume this card.
        if (Pile?.Type == PileType.Play) await CardPileCmd.Add(this, PileType.Hand);
        if (!cardPlay.IsAutoPlay && cardPlay.PlayIndex == 0)
            await PlayerCmd.GainEnergy(cardPlay.Resources.EnergySpent, Owner);
    }

    protected override void OnUpgrade() => DynamicVars["Enchantment"].UpgradeValueBy(1m);
}
