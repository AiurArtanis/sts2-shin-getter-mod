#nullable enable
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using ShinGetterMod.Services;

namespace ShinGetterMod.Models.Cards;

public sealed class SGC_Annotations : ShinGetterEventCardBase
{
    private bool _canceled;
    private bool _manualCanceled;
    private CardModel? _preparedTarget;
    internal void BeginPlaySequence() { _canceled = false; }
    internal void EndPlaySequence() { _canceled = false; _manualCanceled = false; _preparedTarget = null; }
    internal bool HasPreparedTarget => _preparedTarget is CardModel target && target.Pile?.Type == PileType.Hand && IsLegalTarget(target);
    internal void CancelManualPreparation() { _manualCanceled = true; _preparedTarget = null; }
    internal bool ConsumeManualCancellation()
    {
        bool canceled = _manualCanceled;
        if (canceled) EndPlaySequence();
        return canceled;
    }

    internal async Task<bool> PrepareManualPlay(PlayerChoiceContext choiceContext)
    {
        EndPlaySequence();
        choiceContext.PushModel(this);
        try
        {
            _preparedTarget = await ShinGetterAnnotationsCardChoice.Select(choiceContext, Owner, SelectionScreenPrompt, IsLegalTarget);
            if (HasPreparedTarget) return true;
            CancelManualPreparation();
            return false;
        }
        finally { choiceContext.PopModel(this); }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        EndPlaySequence();
    }
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
        if (_canceled) return; // Only the remaining effects are skipped; the native sequence stays committed.
        bool firstForm = HasForm(Owner, ShinGetterForm.Getter1);
        CardModel? target = _preparedTarget;
        _preparedTarget = null;
        if (target == null || target.Pile?.Type != PileType.Hand || !IsLegalTarget(target))
        {
            // Auto-play, replay, or a target invalidated by native pre-play hooks is already
            // committed. Its cancellation cannot refund resources or remove the play's hooks.
            var prompt = new LocString("cards", Id.Entry + ".committedSelectionScreenPrompt");
            target = await ShinGetterAnnotationsCardChoice.Select(choiceContext, Owner, prompt, IsLegalTarget);
        }
        if (target == null || target.Pile?.Type != PileType.Hand || !IsLegalTarget(target))
        {
            _canceled = true;
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
            _canceled = true;
            return;
        }
        if (firstForm)
            await PowerCmd.Apply<VigorPower>(choiceContext, Owner.Creature, 5m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["Enchantment"].UpgradeValueBy(1m);
}
