#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(AttackIntent), nameof(AttackIntent.GetSingleDamage))]
internal static class ShinGetterOpenGetIntentPatch
{
    internal const string HoverMeta = "shin_getter_open_get";
    private const string SingleLabelKey = "SHIN_GETTER_OPEN_GET_INTENT_SINGLE";
    private const string MultiLabelKey = "SHIN_GETTER_OPEN_GET_INTENT_MULTI";
    private const string HoverTitleKey = "SHIN_GETTER_OPEN_GET_INTENT.title";
    private const string HoverDescriptionKey = "SHIN_GETTER_OPEN_GET_INTENT.description";

    [ThreadStatic]
    private static int _intentDamageCalculationDepth;

    internal static bool IsCalculatingIntentDamage => _intentDamageCalculationDepth > 0;

    [HarmonyPrefix]
    private static void Prefix()
    {
        _intentDamageCalculationDepth++;
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        _intentDamageCalculationDepth = Math.Max(0, _intentDamageCalculationDepth - 1);
        return __exception;
    }

    private static T CalculateIntentDamage<T>(Func<T> calculate)
    {
        _intentDamageCalculationDepth++;
        try
        {
            return calculate();
        }
        finally
        {
            _intentDamageCalculationDepth = Math.Max(0, _intentDamageCalculationDepth - 1);
        }
    }

    internal static void ApplyAvoidanceLabel(
        AttackIntent intent,
        IEnumerable<Creature> targets,
        Creature owner,
        ref LocString result)
    {
        Creature[] targetArray = targets as Creature[] ?? targets.ToArray();
        if (!WouldShowAvoidance(intent, targetArray, owner))
            return;

        var replacement = new LocString(
            "static_hover_tips",
            intent is MultiAttackIntent ? MultiLabelKey : SingleLabelKey);
        replacement.Add(
            "Damage",
            CalculateIntentDamage(() => intent.GetSingleDamage(targetArray, owner)));
        replacement.Add(
            "Repeat",
            intent is MultiAttackIntent multiAttack
                ? ShinGetterMultiAttackIntentPatch.GetAdjustedRepeats(multiAttack, owner)
                : 1);
        result = replacement;
    }

    internal static bool WouldShowAvoidance(
        AttackIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        Creature[] targetArray = targets as Creature[] ?? targets.ToArray();
        if (!IsEligibleForAvoidance(intent, targetArray, owner))
            return false;

        // Intents are rendered in CombatState.Enemies order and then in each monster move's
        // intent order. Mark only the first eligible attack so multiple enemies do not imply
        // that one Open Get counter will avoid every attack this turn.
        if (owner.CombatState is not { } combatState)
            return true;

        foreach (Creature enemy in combatState.Enemies)
        {
            if (!enemy.IsAlive || enemy.Monster == null)
                continue;

            foreach (AbstractIntent candidate in enemy.Monster.NextMove.Intents)
            {
                if (candidate is not AttackIntent attackIntent
                    || !IsEligibleForAvoidance(attackIntent, targetArray, enemy))
                {
                    continue;
                }

                return ReferenceEquals(enemy, owner) && ReferenceEquals(attackIntent, intent);
            }
        }

        // Keep isolated previews and transient move updates useful if the current intent is not
        // present in the combat state's move list yet.
        return true;
    }

    private static bool IsEligibleForAvoidance(
        AttackIntent intent,
        Creature[] targets,
        Creature owner)
    {
        int totalDamage = CalculateIntentDamage(() => intent.GetTotalDamage(targets, owner));
        return targets.Any(target =>
            target.GetPower<SGP_OpenGet>()?.WouldAvoidIntent(totalDamage) == true);
    }

    internal static HoverTip CreateAvoidanceHoverTip() => new(
        new LocString("static_hover_tips", HoverTitleKey),
        new LocString("static_hover_tips", HoverDescriptionKey));
}

[HarmonyPatch(typeof(SingleAttackIntent), nameof(SingleAttackIntent.GetIntentLabel))]
internal static class ShinGetterOpenGetSingleIntentLabelPatch
{
    private static void Postfix(
        SingleAttackIntent __instance,
        IEnumerable<Creature> targets,
        Creature owner,
        ref LocString __result)
    {
        ShinGetterOpenGetIntentPatch.ApplyAvoidanceLabel(__instance, targets, owner, ref __result);
    }
}

[HarmonyPatch(typeof(NIntent), nameof(NIntent.UpdateIntent))]
internal static class ShinGetterOpenGetIntentStatePatch
{
    private static readonly ConditionalWeakTable<NIntent, IntentState> States = new();

    private static void Postfix(
        NIntent __instance,
        AbstractIntent intent,
        IEnumerable<Creature> targets,
        Creature owner)
    {
        States.Remove(__instance);
        States.Add(__instance, new IntentState(intent, targets.ToArray(), owner));
    }

    internal static bool TryGet(NIntent intentNode, out IntentState state) =>
        States.TryGetValue(intentNode, out state!);

    internal sealed record IntentState(AbstractIntent Intent, Creature[] Targets, Creature Owner);
}

[HarmonyPatch(typeof(NIntent), "_Ready")]
internal static class ShinGetterOpenGetIntentHoverPatch
{
    private static readonly ConditionalWeakTable<NIntent, object> ConnectedNodes = new();

    private static void Postfix(NIntent __instance)
    {
        if (ConnectedNodes.TryGetValue(__instance, out _))
            return;

        ConnectedNodes.Add(__instance, new object());
        RichTextLabel label = __instance.GetNode<RichTextLabel>("%Value");
        label.MouseFilter = Control.MouseFilterEnum.Pass;
        label.MetaHoverStarted += meta => OnMetaHoverStarted(__instance, meta);
        label.MetaHoverEnded += meta => OnMetaHoverEnded(__instance, meta);
    }

    private static void OnMetaHoverStarted(NIntent intentNode, Variant meta)
    {
        if (meta.AsString() != ShinGetterOpenGetIntentPatch.HoverMeta
            || !ShinGetterOpenGetIntentStatePatch.TryGet(intentNode, out var state)
            || state.Intent is not AttackIntent attackIntent
            || !ShinGetterOpenGetIntentPatch.WouldShowAvoidance(attackIntent, state.Targets, state.Owner))
        {
            return;
        }

        NCombatRoom.Instance?.GetCreatureNode(state.Owner)?.ShowHoverTips(
            new IHoverTip[] { ShinGetterOpenGetIntentPatch.CreateAvoidanceHoverTip() });
    }

    private static void OnMetaHoverEnded(NIntent intentNode, Variant meta)
    {
        if (meta.AsString() != ShinGetterOpenGetIntentPatch.HoverMeta
            || !ShinGetterOpenGetIntentStatePatch.TryGet(intentNode, out var state))
        {
            return;
        }

        NCombatRoom.Instance?.GetCreatureNode(state.Owner)?.HideHoverTips();
    }
}
