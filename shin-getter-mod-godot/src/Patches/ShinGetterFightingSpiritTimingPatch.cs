#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

[HarmonyPatch]
internal static class ShinGetterFightingSpiritTimingPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo damage = AccessTools.DeclaredMethod(typeof(CreatureCmd), nameof(CreatureCmd.Damage),
            new[] { typeof(PlayerChoiceContext), typeof(IEnumerable<DamageTargetData>), typeof(decimal),
                typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay), typeof(ModifyDamageHookType) });
        Type stateMachine = damage?.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new MissingMemberException("CreatureCmd.Damage async state machine");
        return AccessTools.DeclaredMethod(stateMachine, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        MethodInfo afterModifiers = AccessTools.DeclaredMethod(typeof(Hook), nameof(Hook.AfterModifyingDamageAmount));
        MethodInfo calculate = AccessTools.DeclaredMethod(typeof(CreatureCmd), "GetDamageResults");
        var code = instructions.ToList();
        int after = code.FindIndex(i => i.Calls(afterModifiers));
        int calculation = code.FindIndex(i => i.Calls(calculate));
        if (after < 0 || calculation <= after || code.Count(i => i.Calls(afterModifiers)) != 1
            || code.Count(i => i.Calls(calculate)) != 1)
            throw new InvalidOperationException("Fighting Spirit requires the per-target modifier await before damage calculation.");

        var fields = original.DeclaringType!.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo context = fields.Single(f => f.Name == "choiceContext" && f.FieldType == typeof(PlayerChoiceContext));
        FieldInfo target = fields.Single(f => f.Name.StartsWith("<originalTarget>", StringComparison.Ordinal)
            && f.FieldType == typeof(Creature));
        FieldInfo dealer = fields.Single(f => f.Name == "dealer" && f.FieldType == typeof(Creature));
        var (closure, props) = fields
            .Where(f => f.FieldType.DeclaringType == typeof(CreatureCmd))
            .Select(f => (Field: f, Props: AccessTools.DeclaredField(f.FieldType, "props")))
            .Where(pair => pair.Props?.FieldType == typeof(ValueProp)).Single();

        var loadContext = new CodeInstruction(OpCodes.Ldarg_0);
        loadContext.labels.AddRange(code[after].labels);
        loadContext.blocks.AddRange(code[after].blocks);
        code.RemoveAt(after);
        code.InsertRange(after, new[]
        {
            loadContext, new CodeInstruction(OpCodes.Ldfld, context),
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, target),
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, closure),
            new CodeInstruction(OpCodes.Ldfld, props),
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, dealer),
            new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(ShinGetterFightingSpiritTimingPatch),
                nameof(AfterModifiersThenCounter))),
        });
        return code;
    }

    private static async Task AfterModifiersThenCounter(IRunState runState, ICombatState? combatState,
        CardModel? cardSource, IEnumerable<AbstractModel> modifiers, PlayerChoiceContext choiceContext,
        Creature target, ValueProp props, Creature? dealer)
    {
        await Hook.AfterModifyingDamageAmount(runState, combatState, cardSource, modifiers);
        if (combatState == null || !combatState.ContainsCreature(target)
            || target.GetPower<SGP_FightingSpirit>() is not { } power)
            return;

        // 114 calculates HP loss before the normal BeforeDamageReceived hook. Only the counter moves.
        choiceContext.PushModel(power);
        try
        {
            await power.CounterBeforeDamageCalculation(choiceContext, target, props, dealer, cardSource);
        }
        finally
        {
            choiceContext.PopModel(power);
        }
    }
}
