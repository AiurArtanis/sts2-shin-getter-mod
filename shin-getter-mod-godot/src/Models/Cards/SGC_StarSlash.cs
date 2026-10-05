using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Powers;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Nodes.Vfx;

namespace ShinGetterMod.Models.Cards;

/// <summary>
/// 斩星斧 | 攻击 | 稀有 | 3费 | 烧牌/输出终端
/// 消耗抽牌堆 1 张卡，将数值叠加在此卡上，造成 22 点基础伤害
/// 一号机：本次攻击获得热血
/// </summary>
public sealed class SGC_StarSlash : ShinGetterCardBase
{
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(22m, ValueProp.Move),
        new CardsVar(1),
    };

    public SGC_StarSlash()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
        var pile = PileType.Draw.GetPile(Owner);
        var prefs = new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, DynamicVars.Cards.IntValue);
        // Same auto-select boundary as the native command; do not add a forced manual prompt.
        bool willChoose = !CombatManager.Instance.IsOverOrEnding && pile.Cards.Count > 0
            && (prefs.RequireManualConfirmation || pile.Cards.Count > prefs.MinSelect)
            && CardSelectCmd.Selector == null && !NonInteractiveMode.IsActive
            && RunManager.Instance.NetService.Type != NetGameType.Replay;
        NShinGetterStarSlashSequence sequence = NShinGetterStarSlashSequence.TryCreate(Owner);
        try
        {
            if (willChoose && sequence != null)
            {
                Task preparationVoice = ShinGetterVoiceService.TryPlayStarSlashPreparation(this);
                await sequence.WaitForSelection(preparationVoice);
            }
            if (Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
            var selected = (await CardSelectCmd.FromCombatPile(choiceContext, pile, Owner, prefs)).ToList();
            ShinGetterVoiceService.FinishStarSlashPreparation(Owner);
            if (Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
            ShinGetterVoiceService.TryPlayCardVoiceAtCustomTiming(this, out float voiceDuration);
            sequence?.Confirm(voiceDuration);

            decimal stackedValue = Math.Min(selected.Sum(SumOriginalCardValues), 50m);
            foreach (var card in selected)
            {
                await CardCmd.Exhaust(choiceContext, card);
            }
            if (Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;

            ShinGetterCombatVfx.FlashHotBloodIcon(Owner.Creature);
            if (HasForm(Owner, ShinGetterForm.Getter1))
                await PowerCmd.Apply<SGP_HotBlood>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

            Task recovery = Task.CompletedTask;
            Task impactVfx = Task.CompletedTask;
            if (sequence != null)
            {
                await sequence.PlayToImpact();
                if (Owner.Creature.IsDead || CombatManager.Instance.IsOverOrEnding) return;
                recovery = sequence.Recover();
                impactVfx = ShinGetterCombatVfx.PlayHeavyCleave(Owner.Creature, new[] { cardPlay.Target });
            }
            else await PlayLegacyAnimationToImpact(cardPlay.Target);
            Task damage = DamageCmd.Attack(DynamicVars.Damage.BaseValue + stackedValue).FromCard(this)
                .WithNoAttackerAnim()
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_giant_horizontal_slash").Execute(choiceContext);
            await Task.WhenAll(damage, impactVfx, recovery);
        }
        finally
        {
            sequence?.Close();
            ShinGetterVoiceService.FinishStarSlashPreparation(Owner);
        }
    }

    private Task PlayLegacyAnimationToImpact(MegaCrit.Sts2.Core.Entities.Creatures.Creature target)
    {
        // Other forms/non-rendered execution retain the previous animation and impact VFX.
        return NShinGetterStaticVisuals.PlayPhasedCreatureActionAnimation(
                Owner.Creature,
                GetActionAnimationTrigger() ?? "Attack",
                1f,
                1f,
                () => ShinGetterCombatVfx.PlayHeavyCleave(Owner.Creature, new[] { target }),
                firstHalfDurationOverride: 0.5f);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
    }

    private static decimal SumOriginalCardValues(CardModel card)
    {
        decimal total = card.DynamicVars.ContainsKey("CalculatedDamage")
            ? card.DynamicVars.CalculationBase.BaseValue
            : 0m;

        foreach (DynamicVar dynamicVar in card.DynamicVars.Values)
        {
            if (dynamicVar.Name is "CalculatedDamage" or "CalculationBase" or "CalculationExtra" or "ExtraDamage")
                continue;
            total += dynamicVar.BaseValue;
        }

        return total;
    }
}
