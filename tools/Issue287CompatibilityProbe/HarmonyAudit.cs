using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Issue287CompatibilityProbe;

internal static class HarmonyAudit
{
    private static readonly HashSet<string> ReviewedHelpers = new(StringComparer.Ordinal)
    {
        "ShinGetterAnnotationsPreparationPatch", "ShinGetterBondNativeLineFocusPatch", "ShinGetterCardMutationPowerPatch",
        "ShinGetterFourthSelectionPresentationPatch", "ShinGetterEventReloadSavePatch", "ShinGetterMerchantWelcomePatch"
    };

    internal static void Run(Audit a)
    {
        var engine = new Harmony("issue287.offline-resolver-only");
        var harmony = typeof(Harmony).Assembly;
        var processorType = typeof(PatchClassProcessor);
        var descriptionField = processorType.GetFields(Audit.All).Single(f => f.FieldType == typeof(HarmonyMethod));
        var methodsField = processorType.GetField("patchMethods", Audit.All)!;
        var getOriginal = harmony.GetType("HarmonyLib.PatchTools", true)!.GetMethod("GetOriginalMethod", Audit.All)!;
        var before = Harmony.GetAllPatchedMethods().ToArray();
        foreach (Type patch in a.Mod.GetTypes().Where(t => t.CustomAttributes.Any(c => c.AttributeType == typeof(HarmonyPatch))).OrderBy(t => t.FullName))
        {
            string source = a.Location(patch.GetMethods(Audit.All | BindingFlags.DeclaredOnly).FirstOrDefault() ?? throw new InvalidOperationException("Empty patch class: " + patch));
            a.Guard("harmony-class", patch.FullName!, patch.FullName!, source, () =>
            {
                var processor = new PatchClassProcessor(engine, patch);
                var description = (HarmonyMethod)descriptionField.GetValue(processor)!;
                var helpers = patch.GetMethods(Audit.All | BindingFlags.DeclaredOnly).Where(m => m.Name is "TargetMethod" or "TargetMethods"
                    || m.CustomAttributes.Any(c => c.AttributeType == typeof(HarmonyTargetMethod) || c.AttributeType == typeof(HarmonyTargetMethods))).ToArray();
                MethodBase[]? dynamicTargets = null;
                if (helpers.Length > 0)
                {
                    if (helpers.Length != 1 || !ReviewedHelpers.Contains(patch.Name))
                    {
                        a.Add("harmony-class", patch.FullName!, "UNRESOLVED", patch.FullName!, source, "Unreviewed TargetMethod(s) helper; not executed automatically.");
                        return;
                    }
                    MethodInfo helper = helpers[0];
                    CheckSafeHelper(a, patch, helper);
                    object? value = helper.Invoke(null, Array.Empty<object>());
                    dynamicTargets = value is MethodBase one ? new[] { one } : value is IEnumerable many ? many.Cast<MethodBase>().ToArray() : Array.Empty<MethodBase>();
                    if (dynamicTargets.Length == 0 || dynamicTargets.Any(m => m == null)) throw new MissingMethodException("Target helper produced no target/null.");
                    a.Add("harmony-helper", patch.FullName!, "PASS", helper.Name, a.Location(helper),
                        "Actual production helper executed/enumerated after reflection-only IL safety review. No callback or patch application.", dynamicTargets.Select(Audit.Signature).ToArray());
                }
                int hooks = 0;
                foreach (object attributePatch in (IEnumerable)methodsField.GetValue(processor)!)
                {
                    var info = (HarmonyMethod)attributePatch.GetType().GetField("info", Audit.All)!.GetValue(attributePatch)!;
                    string kind = attributePatch.GetType().GetField("type", Audit.All)!.GetValue(attributePatch)!.ToString()!;
                    var hook = info.method ?? throw new InvalidOperationException("Harmony hook has no MethodInfo.");
                    var merged = description.Merge(info);
                    MethodBase[] targets = dynamicTargets ?? new[] { (MethodBase?)getOriginal.Invoke(null, new object[] { merged })
                        ?? throw new MissingMethodException("Undefined target for " + hook) };
                    foreach (var target in targets)
                    {
                        string id = patch.FullName + "::" + hook.Name + "->" + target.MetadataToken;
                        a.Add("harmony-target", id, "PASS", Audit.Signature(target), a.Location(hook),
                            dynamicTargets == null ? "Actual 0Harmony PatchClassProcessor merged attributes + PatchTools.GetOriginalMethod resolved target." : "Actual production TargetMethod(s) returned this method.",
                            new { patch = patch.FullName, hook = hook.ToString(), kind, nativeAssembly = target.DeclaringType?.Assembly.GetName().Name });
                        a.Guard("harmony-parameters", id, Audit.Signature(target), a.Location(hook), () => CheckParameters(a, hook, target, kind));
                        if (!a.Bindings.TryGetValue(patch.FullName!, out var list)) a.Bindings[patch.FullName!] = list = new();
                        list.Add((target, hook));
                        a.ExpectedPatches.Add((target, hook, kind));
                        if (kind == "Transpiler") a.Guard("transpiler", id, Audit.Signature(target), a.Location(hook), () => Transpile(a, hook, target));
                    }
                    hooks++;
                }
                if (hooks == 0) throw new InvalidOperationException("No Harmony hooks recognized by actual processor.");
                a.Add("harmony-class", patch.FullName!, "PASS", patch.FullName!, source, $"Actual class processor recognized {hooks} hooks; .Patch() NOT called.");
            });
        }
        var card = a.Game.GetType("MegaCrit.Sts2.Core.Models.CardModel", true)!;
        foreach (var (name, arguments, id) in new[]
        {
            ("__Issue287DefinitelyMissing", Type.EmptyTypes, "harmony-missing-method"),
            ("SpendResources", new[] { typeof(int) }, "harmony-wrong-signature")
        })
        {
            var info = new HarmonyMethod { declaringType = card, methodName = name, methodType = MethodType.Normal, argumentTypes = arguments };
            bool rejected;
            try { rejected = getOriginal.Invoke(null, new object[] { info }) == null; }
            catch (TargetInvocationException ex) when (Audit.Unwrap(ex) is MissingMethodException or AmbiguousMatchException) { rejected = true; }
            a.Add("negative-control", id, rejected ? "PASS" : "FAIL", card.FullName + "." + name, "<probe>", rejected ? "Actual Harmony resolver rejects deliberately invalid target." : "FALSE POSITIVE.");
        }
        var after = Harmony.GetAllPatchedMethods().ToArray();
        bool noPatches = before.OrderBy(Audit.Signature).SequenceEqual(after.OrderBy(Audit.Signature));
        a.Add("isolation", "no-patches-applied", noPatches ? "PASS" : "FAIL", "Harmony.GetAllPatchedMethods", "<probe>", "Patched-method inventory before/after offline audit is identical.", new { before = before.Length, after = after.Length });
        bool registryValid = a.ExpectedPatches.All(p => p.Kind is "Prefix" or "Postfix" or "Transpiler" or "Finalizer")
            && a.ExpectedPatches.Distinct().Count() == a.ExpectedPatches.Count;
        a.Add("registry-contract", "callback-kinds-and-duplicates", registryValid ? "PASS" : "FAIL", "expectedHarmonyRegistry", "<probe>",
            "Every extracted callback is represented in one of the four registry lists and no duplicate original/hook/kind edge was emitted.");
    }

    private static void CheckSafeHelper(Audit a, Type patch, MethodInfo helper)
    {
        if (!helper.IsStatic || helper.GetParameters().Length != 0) throw new InvalidOperationException("Helper must be static and parameterless.");
        var definition = a.Types.Single(t => t.FullName.Replace('/', '+') == patch.FullName);
        var bodies = definition.Methods.Where(m => m.Name == helper.Name || m.IsConstructor && m.IsStatic)
            .Concat(definition.NestedTypes.Where(t => t.Name.Contains("<" + helper.Name + ">", StringComparison.Ordinal)).SelectMany(t => t.Methods));
        foreach (var body in bodies.Where(m => m.HasBody))
        foreach (var instruction in body.Body.Instructions)
        {
            if (instruction.Operand is not Mono.Cecil.MethodReference call) continue;
            string owner = call.DeclaringType.FullName;
            bool safe = owner.StartsWith("System.", StringComparison.Ordinal) || owner == "HarmonyLib.AccessTools"
                || owner.StartsWith(definition.FullName + "/<" + helper.Name + ">", StringComparison.Ordinal);
            if (!safe || owner.StartsWith("System.IO.", StringComparison.Ordinal) || owner.StartsWith("System.Diagnostics.Process", StringComparison.Ordinal))
                throw new InvalidOperationException("Unreviewed helper side effect: " + call.FullName);
        }
    }

    private static Type Element(Type t) => t.IsByRef ? t.GetElementType()! : t;
    private static void CheckParameters(Audit a, MethodInfo hook, MethodBase target, string kind)
    {
        var checks = new List<object>();
        foreach (var p in hook.GetParameters())
        {
            string name = p.Name!;
            Type type = Element(p.ParameterType);
            Type? native = null;
            string rule;
            if (kind == "Transpiler")
            {
                bool accepted = type == typeof(IEnumerable<CodeInstruction>) || type == typeof(ILGenerator) || type.IsAssignableFrom(target.GetType()) || type == typeof(MethodBase);
                if (!accepted) throw new InvalidOperationException("Unsupported transpiler parameter: " + p);
                rule = "Harmony transpiler type-based injection";
            }
            else if (name == "__instance") { if (target.IsStatic) throw new InvalidOperationException("Static original has no __instance."); native = target.DeclaringType; rule = "instance"; }
            else if (name == "__result") { native = target is MethodInfo m ? Element(m.ReturnType) : typeof(void); rule = "result"; }
            else if (name == "__state")
            {
                var producer = hook.DeclaringType!.GetMethods(Audit.All).Where(m => m.Name == "Prefix" || m.CustomAttributes.Any(c => c.AttributeType == typeof(HarmonyPrefix)))
                    .SelectMany(m => m.GetParameters()).FirstOrDefault(x => x.Name == "__state" && x.ParameterType.IsByRef);
                native = producer == null ? null : Element(producer.ParameterType);
                if (native == null) throw new InvalidOperationException("__state has no matching prefix producer.");
                rule = "shared prefix state";
            }
            else if (name == "__exception") { native = typeof(Exception); rule = "exception"; }
            else if (name == "__originalMethod") { native = target.GetType(); rule = "original method"; }
            else if (name == "__args") { native = typeof(object[]); rule = "argument array"; }
            else if (name == "__runOriginal") { native = typeof(bool); rule = "run original"; }
            else if (name.StartsWith("___", StringComparison.Ordinal))
            {
                var field = AccessTools.Field(target.DeclaringType, name[3..]) ?? throw new MissingFieldException(name);
                native = field.FieldType; rule = "private field";
            }
            else
            {
                var originalParameters = target.GetParameters();
                ParameterInfo? parameter = name.StartsWith("__", StringComparison.Ordinal) && int.TryParse(name[2..], out int index)
                    ? originalParameters.ElementAtOrDefault(index) : originalParameters.SingleOrDefault(x => x.Name == name);
                if (parameter == null) throw new MissingMemberException("Named/indexed original parameter missing: " + name);
                native = Element(parameter.ParameterType); rule = "original parameter name/index";
            }
            if (native != null && (native == typeof(void) || !type.IsAssignableFrom(native)
                || p.ParameterType.IsByRef && type != native && type != typeof(object))) throw new InvalidOperationException($"Injection mismatch: {p}; native={native}");
            checks.Add(new { name, type = p.ParameterType.ToString(), native = native?.ToString(), rule });
        }
        a.Add("harmony-parameters", hook.DeclaringType!.FullName + "." + hook.Name + "->" + target.MetadataToken,
            "PASS", Audit.Signature(target), a.Location(hook), $"{checks.Count} injection parameters checked; this is not Harmony wrapper emission/PatchAll.", checks);
    }

    private sealed record Snapshot(CodeInstruction Instruction, OpCode Opcode, object? Operand, Label[] Labels, ExceptionBlock[] Blocks);
    private static void Transpile(Audit a, MethodInfo hook, MethodBase target)
    {
        string patch = hook.DeclaringType!.Name;
        var expected = patch switch
        {
            "ShinGetterMerchantWelcomePatch" => new[] { ("Play", "PlayWelcome"), ("ShowOnInventoryOpen", "ShowWelcome") },
            "ShinGetterMerchantReactionPatch" => new[] { ("Play", "PlayPurchase"), ("ShowForPurchaseAttempt", "ShowPurchase") },
            "ShinGetterAnnotationsPreparationPatch" => new[] { ("SpendResources", "PrepareResources"), ("OnPlayWrapper", "PlayPreparedCard") },
            "ShinGetterEventReloadSavePatch" => new[] { ("IncrementNumReloads", "SaveReloadCheckpoint") },
            "ShinGetterFourthSelectionPresentationPatch" => target.DeclaringType!.Name.Contains("FromChooseACardScreen", StringComparison.Ordinal)
                ? new[] { ("CardsSelected", "ChooseAfterPresentation") } : new[] { ("CardsSelected", "GridAfterPresentation") },
            _ => throw new InvalidOperationException("New transpiler requires an explicit layout contract: " + hook)
        };
        var input = PatchProcessor.GetOriginalInstructions(target, out ILGenerator generator);
        using var nativeModule = Mono.Cecil.ModuleDefinition.ReadModule(a.Game.Location);
        var nativeMethod = (Mono.Cecil.MethodDefinition)nativeModule.LookupToken(target.MetadataToken);
        a.Add("transpiler-native-layout", hook.DeclaringType.FullName + "." + hook.Name + "->" + target.MetadataToken, "PASS", Audit.Signature(target), a.Location(hook),
            "Official original IL decoded by actual Harmony; this record alone does not mean transformation passed.",
            new { nativeIlCalls = nativeMethod.Body.Instructions.Where(i => i.Operand is Mono.Cecil.MethodReference)
                .Select(i => new { offset = "IL_" + i.Offset.ToString("X4"), opcode = i.OpCode.ToString(), signature = i.Operand.ToString() }).ToArray(),
                nativeFields = nativeMethod.DeclaringType.Fields.Select(f => new { f.Name, type = f.FieldType.FullName }).ToArray(),
                calls = input.Select((i, index) => new { i, index }).Where(x => x.i.operand is MethodBase)
                .Select(x => new { x.index, opcode = x.i.opcode.ToString(), target = Audit.Signature((MethodBase)x.i.operand) }).ToArray(),
                cachedMethodTargets = hook.DeclaringType.GetFields(Audit.All).Where(f => f.FieldType == typeof(MethodInfo)).Select(f =>
                {
                    var cached = (MethodInfo?)f.GetValue(null);
                    return new { f.Name, target = cached?.ToString(), declaringType = cached?.DeclaringType?.FullName,
                        reflectedType = cached?.ReflectedType?.FullName, metadataToken = cached?.MetadataToken,
                        callsMatch = cached == null ? 0 : input.Count(i => i.Calls(cached)),
                        sameTokenNativeCalls = cached == null ? null : input.Where(i => i.operand is MethodInfo native && native.Module == cached.Module && native.MetadataToken == cached.MetadataToken)
                            .Select(i => new { declaringType = ((MethodInfo)i.operand).DeclaringType?.FullName, reflectedType = ((MethodInfo)i.operand).ReflectedType?.FullName, methodInfoEquals = Equals(i.operand, cached) }).ToArray() };
                }).ToArray() });
        var snapshots = input.Select(i => new Snapshot(i, i.opcode, i.operand, i.labels.ToArray(), i.blocks.ToArray())).ToArray();
        foreach (var (oldName, _) in expected)
            if (input.Count(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) && i.operand is MethodInfo m && m.Name == oldName) != 1)
                throw new InvalidOperationException("Native call layout not exactly one " + oldName + "; production transpiler NOT called to avoid invoking its skip/log path.");
        if (patch.StartsWith("ShinGetterMerchant", StringComparison.Ordinal))
        {
            string sound = patch == "ShinGetterMerchantWelcomePatch" ? "event:/sfx/npcs/merchant/merchant_welcome" : "event:/sfx/npcs/merchant/merchant_thank_yous";
            int call = input.FindIndex(i => i.operand is MethodInfo m && m.Name == "Play" && m.DeclaringType?.Name == "SfxCmd");
            if (call < 2 || input[call - 2].opcode != OpCodes.Ldstr || !Equals(input[call - 2].operand, sound))
                throw new InvalidOperationException("Native sound literal/relative layout differs; skip path not executed.");
        }
        object?[] args = hook.GetParameters().Select(p => p.ParameterType == typeof(IEnumerable<CodeInstruction>) ? (object)input
            : p.ParameterType == typeof(ILGenerator) ? generator : (object)target).ToArray();
        var output = ((IEnumerable<CodeInstruction>)hook.Invoke(null, args)!).ToList();
        int j = 0;
        var replacements = new List<object>();
        foreach (var snap in snapshots)
        {
            if (j >= output.Count) throw new InvalidOperationException("Native instruction deleted.");
            if (ReferenceEquals(snap.Instruction, output[j]))
            {
                var current = output[j++];
                if (current.opcode != snap.Opcode || !Equals(current.operand, snap.Operand)
                    || !current.labels.SequenceEqual(snap.Labels) || !current.blocks.SequenceEqual(snap.Blocks))
                    throw new InvalidOperationException("Unrelated native instruction/label/exception block changed.");
                continue;
            }
            if (snap.Operand is not MethodInfo old) throw new InvalidOperationException("Non-call native instruction replaced.");
            var contract = expected.SingleOrDefault(p => p.Item1 == old.Name);
            if (contract == default) throw new InvalidOperationException("Unexpected native call replaced: " + old);
            int start = j;
            while (j < output.Count && !(output[j].opcode == OpCodes.Call && output[j].operand is MethodInfo replacement && replacement.Name == contract.Item2)) j++;
            if (j == output.Count) throw new InvalidOperationException("Required adapter not emitted: " + contract.Item2);
            var inserted = output.Skip(start).Take(j - start + 1).ToArray();
            var adapter = (MethodInfo)output[j].operand;
            if (adapter.DeclaringType?.Assembly != a.Mod || !adapter.IsStatic || adapter.ReturnType != old.ReturnType)
                throw new InvalidOperationException("Adapter assembly/static/return mismatch.");
            if (!inserted[0].labels.SequenceEqual(snap.Labels) || !inserted[0].blocks.SequenceEqual(snap.Blocks)
                || inserted.Skip(1).Any(i => i.labels.Count > 0 || i.blocks.Count > 0)) throw new InvalidOperationException("Replacement lost/duplicated branch labels or exception blocks.");
            var loaded = new List<Type>();
            for (int k = 0; k < inserted.Length - 1; k++)
            {
                var i = inserted[k];
                if (i.opcode == OpCodes.Ldarg_0) loaded.Add(target.IsStatic ? target.GetParameters()[0].ParameterType : target.DeclaringType!);
                else if (i.opcode == OpCodes.Ldarg_2) loaded.Add(target.GetParameters()[target.IsStatic ? 2 : 1].ParameterType);
                else if (i.opcode == OpCodes.Ldfld && i.operand is FieldInfo field)
                {
                    Type receiver = loaded.Last();
                    if (!field.DeclaringType!.IsAssignableFrom(receiver)) throw new InvalidOperationException("Invalid field receiver in inserted IL.");
                    loaded[^1] = field.FieldType;
                }
                else throw new InvalidOperationException("Unexpected inserted opcode: " + i);
            }
            var stack = (old.IsStatic ? Array.Empty<Type>() : new[] { old.DeclaringType! }).Concat(old.GetParameters().Select(p => p.ParameterType)).Concat(loaded).ToArray();
            if (!stack.SequenceEqual(adapter.GetParameters().Select(p => p.ParameterType))) throw new InvalidOperationException("Adapter stack signature mismatch: " + adapter);
            replacements.Add(new { original = Audit.Signature(old), replacement = Audit.Signature(adapter), inserted = inserted.Select(i => i.ToString()).ToArray(), labels = snap.Labels.Length, exceptionBlocks = snap.Blocks.Length });
            j++;
        }
        if (j != output.Count || replacements.Count != expected.Length) throw new InvalidOperationException("Silent skip, unexpected insertions or replacement count mismatch.");
        a.Add("transpiler", hook.DeclaringType.FullName + "." + hook.Name + "->" + target.MetadataToken, "PASS", Audit.Signature(target), a.Location(hook),
            $"Actual production transpiler enumerated: {replacements.Count} calls replaced; no silent skip; every other native instruction, branch operand, label and exception-block marker preserved in order. No emitted wrapper or gameplay executed.",
            new { originalInstructions = input.Count, outputInstructions = output.Count, nativeExceptionClauses = target.GetMethodBody()?.ExceptionHandlingClauses.Count,
                labels = snapshots.Sum(s => s.Labels.Length), exceptionBlockMarkers = snapshots.Sum(s => s.Blocks.Length), replacements });
    }
}
