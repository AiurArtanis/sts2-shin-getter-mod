#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Models.Relics;

public sealed class SGR_MusashiClone : ShinGetterRelicBase
{
    private bool _triggeredThisCombat;
    private ShinGetterForm[] _pendingForms = Array.Empty<ShinGetterForm>();

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsUsedUp => TriggeredThisCombat;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<SGP_HotBlood>(1m),
            new PowerVar<SGP_Acceleration>(1m),
            new PowerVar<SGP_Indomitable>(1m),
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<SGP_HotBlood>(),
            HoverTipFactory.FromPower<SGP_Acceleration>(),
            HoverTipFactory.FromPower<SGP_Indomitable>(),
        };

    [SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
    public bool TriggeredThisCombat
    {
        get => _triggeredThisCombat;
        set
        {
            AssertMutable();
            _triggeredThisCombat = value;
            Status = _triggeredThisCombat ? RelicStatus.Disabled : RelicStatus.Normal;
        }
    }

    public override Task BeforeCombatStart()
    {
        TriggeredThisCombat = false;
        _pendingForms = Array.Empty<ShinGetterForm>();
        return Task.CompletedTask;
    }

    internal decimal PreventFatalHpLoss(Creature target, decimal amount)
    {
        if (target != Owner.Creature
            || TriggeredThisCombat
            || IsMelted
            || !Owner.IsActiveForHooks
            || target.CurrentHp <= 0
            || amount < target.CurrentHp
            || !CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsOverOrEnding)
            return amount;

        _pendingForms = new[] { ShinGetterForm.Getter1, ShinGetterForm.Getter2, ShinGetterForm.Getter3 }
            .Where(form => ShinGetterCardBase.IsInForm(Owner, form))
            .ToArray();
        // Reserve the saved charge before any awaited reward can cause nested damage.
        TriggeredThisCombat = true;
        return 0m;
    }

    public override async Task AfterModifyingHpLostAfterOsty()
    {
        ShinGetterForm[] forms = _pendingForms;
        _pendingForms = Array.Empty<ShinGetterForm>();
        Flash();
        var choiceContext = new ThrowingPlayerChoiceContext();
        foreach (ShinGetterForm form in forms)
        {
            switch (form)
            {
                case ShinGetterForm.Getter1:
                    await PowerCmd.Apply<SGP_HotBlood>(choiceContext, Owner.Creature,
                        DynamicVars["SGP_HotBlood"].BaseValue, Owner.Creature, null);
                    break;
                case ShinGetterForm.Getter2:
                    await PowerCmd.Apply<SGP_Acceleration>(choiceContext, Owner.Creature,
                        DynamicVars["SGP_Acceleration"].BaseValue, Owner.Creature, null);
                    break;
                case ShinGetterForm.Getter3:
                    await PowerCmd.Apply<SGP_Indomitable>(choiceContext, Owner.Creature,
                        DynamicVars["SGP_Indomitable"].BaseValue, Owner.Creature, null);
                    break;
            }
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        TriggeredThisCombat = false;
        _pendingForms = Array.Empty<ShinGetterForm>();
        return Task.CompletedTask;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class MusashiHatFatalDamagePatch
{
    [HarmonyPostfix]
    // -1 is Harmony's unspecified-priority sentinel; -2 follows the Last-priority counter cancellation.
    [HarmonyPriority(Priority.Last - 2)]
    private static void PreventFatalDamage(
        Creature target,
        HpLossHookPhase phases,
        ref IEnumerable<AbstractModel> modifiers,
        ref decimal __result)
    {
        if (!phases.HasFlag(HpLossHookPhase.AfterOsty)
            || target.Player?.GetRelic<SGR_MusashiClone>() is not { } hat)
            return;

        decimal amount = hat.PreventFatalHpLoss(target, __result);
        if (amount == __result)
            return;

        __result = amount;
        List<AbstractModel> updatedModifiers = modifiers.ToList();
        if (!updatedModifiers.Contains(hat))
            updatedModifiers.Add(hat);
        modifiers = updatedModifiers;
    }
}
