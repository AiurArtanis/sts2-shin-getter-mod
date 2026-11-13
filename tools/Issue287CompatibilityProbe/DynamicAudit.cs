using System.Reflection;
using System.Collections;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;

namespace Issue287CompatibilityProbe;

internal static class DynamicAudit
{
    private sealed record Site(string File, int Start, int End, string Api, string Id);
    internal static void Run(Audit a)
    {
        var paths = Directory.GetFiles(Path.Combine(a.Root, "shin-getter-mod-godot", "src"), "*.cs", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
        var trees = paths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)).ToArray();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(new[] { a.Game.Location, typeof(Harmony).Assembly.Location, Path.Combine(a.Dependencies, "GodotSharp.dll") })
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ShinGetterMod", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        Type? historyType = null;
        object? historyContract = null;
        a.Guard("history-boundary", "bounded-player-history", "PlayerMapPointHistoryEntry", "shin-getter-mod-godot/src/Events/ShinGetterEventTransaction.cs:35", () =>
        {
            var historyTree = trees.Single(t => t.FilePath.EndsWith("ShinGetterEventTransaction.cs", StringComparison.Ordinal));
            var assignments = historyTree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>().Where(x => x.Left.ToString() == "_history").ToArray();
            if (assignments.Length != 1 || assignments[0].Right.WithoutTrivia().NormalizeWhitespace().ToFullString() != "owner.RunState.CurrentMapPointHistoryEntry?.GetEntry(owner.NetId)")
                throw new InvalidOperationException("History field assignment no longer matches the reviewed single-source data flow.");
            var player = a.Game.GetType("MegaCrit.Sts2.Core.Entities.Players.Player", true)!;
            Type runState = AccessTools.Property(player, "RunState").PropertyType;
            var pointProperty = AccessTools.Property(runState, "CurrentMapPointHistoryEntry") ?? throw new MissingMemberException(runState.FullName, "CurrentMapPointHistoryEntry");
            var getEntry = AccessTools.Method(pointProperty.PropertyType, "GetEntry", new[] { typeof(ulong) }) ?? throw new MissingMethodException("MapPointHistoryEntry.GetEntry(ulong)");
            var type = getEntry.ReturnType;
            if (type.Name != "PlayerMapPointHistoryEntry") throw new TypeLoadException("Unexpected history return type " + type);
            var subtypes = a.Game.GetTypes().Concat(a.Mod.GetTypes()).Where(t => t != type && type.IsAssignableFrom(t)).ToArray();
            if (subtypes.Length > 0) throw new InvalidOperationException("History has subclasses in the two audited assemblies; extend bounded property analysis.");
            var properties = type.GetProperties();
            var contracts = new List<object>();
            foreach (var p in properties.OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (p.GetIndexParameters().Length != 0 || p.GetMethod is not { IsPublic: true, IsStatic: false })
                    throw new InvalidOperationException("GetProperties includes unreadable/static/indexed history property: " + p);
                bool list = typeof(IList).IsAssignableFrom(p.PropertyType);
                bool writableInt = p.PropertyType == typeof(int) && p.CanWrite;
                contracts.Add(new { p.Name, declaredType = p.PropertyType.FullName, getter = Audit.CanonicalSignature(p.GetMethod),
                    setter = p.SetMethod == null ? null : Audit.CanonicalSignature(p.SetMethod), indexerParameters = p.GetIndexParameters().Length,
                    rollbackBranch = list ? "IList.Count snapshot / IList.RemoveAt on rollback" : writableInt ? "writable int snapshot / PropertyInfo.SetValue" : "ignored unless actual value implements IList",
                    declaredIList = list, writableInt });
            }
            using var native = ModuleDefinition.ReadModule(a.Game.Location);
            IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> input) => input.SelectMany(t => new[] { t }.Concat(AllTypes(t.NestedTypes)));
            var constructionSites = AllTypes(native.Types).SelectMany(t => t.Methods).Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions.Where(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Newobj && i.Operand is MethodReference ctor && ctor.DeclaringType.FullName.Replace('/', '+') == type.FullName)
                    .Select(i => new { caller = m.FullName, offset = "IL_" + i.Offset.ToString("X4"), constructor = i.Operand.ToString() })).ToArray();
            historyType = type;
            historyContract = new { assignment = assignments[0].Right.ToString(), pointProperty = pointProperty.PropertyType.FullName,
                getEntry = Audit.MethodIdentity(getEntry), declaredReturnType = type.FullName, type.IsSealed,
                knownSubclassesInGameAndMod = subtypes.Select(t => t.FullName).ToArray(), constructionSites, properties = contracts,
                listInterface = new { count = typeof(IList).GetProperty("Count")?.ToString() ?? typeof(ICollection).GetProperty("Count")!.ToString(),
                    removeAt = typeof(IList).GetMethod("RemoveAt")!.ToString(), runtimePending = "Actual values, non-null ownership, IList.IsReadOnly/IsFixedSize, RemoveAt side effects, setter execution and instance rollback are NOT tested." } };
            a.Add("history-boundary", "bounded-player-history", "PASS", type.FullName!, "shin-getter-mod-godot/src/Events/ShinGetterEventTransaction.cs:35",
                "Bounded metadata PASS: single assignment, exact GetEntry return type, no subclasses in game/mod, public non-indexed readable property set, setter/int/IList contracts enumerated. Not instance rollback acceptance.", historyContract);
            a.Add("runtime", "history-instance-rollback", "UNRESOLVED", type.FullName!, "shin-getter-mod-godot/src/Events/ShinGetterEventTransaction.cs:38",
                "Native pending: actual history values/types/nulls, mutable IList semantics, property getter/setter execution, rollback completeness and side effects. Bounded metadata checks do not execute these operations.");
        });
        var sites = new List<Site>();
        var lookupSymbols = new Dictionary<ISymbol, List<MemberInfo>>(SymbolEqualityComparer.Default);
        var lookupCalls = new Dictionary<(string Path, int Start), List<MemberInfo>>();
        int number = 0;
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbol = model.GetSymbolInfo(call).Symbol as IMethodSymbol;
                string syntaxName = call.Expression switch { MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
                    MemberBindingExpressionSyntax b => b.Name.Identifier.ValueText, SimpleNameSyntax n => n.Identifier.ValueText, _ => "" };
                string owner = symbol?.ContainingType.ToDisplayString() ?? "";
                bool access = owner == "HarmonyLib.AccessTools" || call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "AccessTools" } };
                bool reflection = (owner == "System.Type" && (syntaxName.StartsWith("Get", StringComparison.Ordinal) || syntaxName == "InvokeMember"))
                    || owner == "System.Reflection.Assembly" && syntaxName == "GetType"
                    || owner.StartsWith("HarmonyLib.Traverse", StringComparison.Ordinal)
                    || owner == "System.Activator";
                bool engine = syntaxName is "Connect" or "CallDeferred" or "Call" && owner.StartsWith("Godot.", StringComparison.Ordinal);
                bool unresolvedSyntax = symbol == null && syntaxName is "GetField" or "GetMethod" or "GetProperty" or "GetConstructor" or "GetNestedType";
                if (!access && !reflection && !engine && !unresolvedSyntax) continue;
                string id = "D" + (++number).ToString("D3");
                var line = tree.GetLineSpan(call.Span);
                string source = a.Relative(tree.FilePath) + ":" + (line.StartLinePosition.Line + 1);
                SyntaxNode spanNode = call.Ancestors().FirstOrDefault(n => n is FieldDeclarationSyntax or StatementSyntax) ?? call;
                var enclosing = tree.GetLineSpan(spanNode.Span);
                sites.Add(new(a.Relative(tree.FilePath), enclosing.StartLinePosition.Line + 1, enclosing.EndLinePosition.Line + 1, syntaxName, id));
                string code = call.ToString();
                string? containingClass = (model.GetDeclaredSymbol(call.Ancestors().OfType<TypeDeclarationSyntax>().First()) as INamedTypeSymbol)?.ToDisplayString();
                if (engine)
                {
                    a.Add("dynamic", id, "SCOPED_OUT", owner + "." + syntaxName, source, "Godot signal/deferred dispatch, not an sts2 reflection target; actual engine callback execution not tested.", new { code });
                    continue;
                }
                if (source.Contains("ShinGetterBs2MandalaPatch.cs", StringComparison.Ordinal))
                {
                    a.Add("dynamic", id, "SCOPED_OUT", owner + "." + syntaxName, source, "Optional St2bs adapter only; excluded by ShinGetter-only authorization. No St2bs assembly was loaded or installed.", new { code });
                    continue;
                }
                a.Guard("dynamic", id, owner + "." + syntaxName, source, () =>
                {
                    var args = call.ArgumentList.Arguments.Select(arg => arg.Expression).ToArray();
                    Type[] targetTypes;
                    Type? fieldExpected = null;
                    int nameIndex;
                    if (access)
                    {
                        if (symbol?.IsGenericMethod == true && syntaxName.Contains("FieldRef", StringComparison.Ordinal))
                        {
                            targetTypes = new[] { RuntimeType(a, symbol.TypeArguments[0]) ?? throw new TypeLoadException("Unbound FieldRef owner") };
                            fieldExpected = RuntimeType(a, symbol.TypeArguments[1]);
                            nameIndex = 0;
                        }
                        else
                        {
                            targetTypes = args.Length > 0 ? Owners(a, model, args[0], containingClass) : Array.Empty<Type>();
                            nameIndex = 1;
                        }
                    }
                    else
                    {
                        var receiver = call.Expression is MemberAccessExpressionSyntax member ? member.Expression
                            : call.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression;
                        targetTypes = receiver == null ? Array.Empty<Type>() : Owners(a, model, receiver, containingClass);
                        nameIndex = 0;
                    }
                    if (targetTypes.Length == 0 && historyType != null && syntaxName == "GetProperties" && code == "_history.GetType().GetProperties()")
                        targetTypes = new[] { historyType };
                    string[] names = args.Length > nameIndex ? Names(model, args[nameIndex]).Distinct().ToArray() : Array.Empty<string>();
                    bool enumeration = syntaxName.EndsWith("Fields", StringComparison.Ordinal) || syntaxName.EndsWith("Methods", StringComparison.Ordinal)
                        || syntaxName.EndsWith("Properties", StringComparison.Ordinal) || syntaxName.EndsWith("Constructors", StringComparison.Ordinal)
                        || syntaxName.EndsWith("Members", StringComparison.Ordinal);
                    if (targetTypes.Length == 0 || (!enumeration && names.Length == 0))
                    {
                        a.Add("dynamic", id, "UNRESOLVED", owner + "." + syntaxName, source,
                            "Runtime-dependent receiver/name was not statically bounded; no existence/signature claim. This call remains in the inventory.", new { code, names, targetTypes = targetTypes.Select(t => t.FullName).ToArray() });
                        return;
                    }
                    Type[]? parameters = null;
                    if (access && syntaxName is "Method" or "DeclaredMethod" && args.Length > nameIndex + 1 && !args[nameIndex + 1].IsKind(SyntaxKind.NullLiteralExpression))
                    {
                        var types = args[nameIndex + 1].DescendantNodesAndSelf().OfType<TypeOfExpressionSyntax>().Select(t => RuntimeType(a, model.GetTypeInfo(t.Type).Type)).ToArray();
                        if (types.Any(t => t == null)) throw new TypeLoadException("Explicit Type[] could not be resolved.");
                        parameters = types.Cast<Type>().ToArray();
                    }
                    var resolved = new List<object>();
                    var boundMembers = new List<MemberInfo>();
                    foreach (var type in targetTypes)
                    {
                        if (enumeration)
                        {
                            var members = syntaxName.Contains("Fields", StringComparison.Ordinal) ? type.GetFields(Audit.All | BindingFlags.DeclaredOnly).Cast<MemberInfo>()
                                : syntaxName.Contains("Properties", StringComparison.Ordinal) ? type.GetProperties(Audit.All).Cast<MemberInfo>()
                                : type.GetMethods(Audit.All).Cast<MemberInfo>();
                            resolved.Add(new { owner = type.FullName, members = members.Select(m => m.ToString()).ToArray() });
                            continue;
                        }
                        foreach (var name in names)
                        {
                            MemberInfo? member;
                            if (syntaxName.Contains("Field", StringComparison.Ordinal))
                            {
                                var field = AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
                                if (fieldExpected != null)
                                {
                                    if (fieldExpected.IsValueType ? field.FieldType != fieldExpected : !fieldExpected.IsAssignableFrom(field.FieldType))
                                        throw new InvalidOperationException($"FieldRef type mismatch: {field.FieldType} vs {fieldExpected}");
                                    var factory = typeof(AccessTools).GetMethods(Audit.All).Single(m => m.Name == "FieldRefAccess" && m.IsGenericMethodDefinition
                                        && m.GetGenericArguments().Length == 2 && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
                                    _ = factory.MakeGenericMethod(type, fieldExpected).Invoke(null, new object[] { name })
                                        ?? throw new InvalidOperationException("FieldRef factory returned null.");
                                }
                                member = field;
                            }
                            else if (syntaxName.Contains("Property", StringComparison.Ordinal)) member = AccessTools.Property(type, name);
                            else if (syntaxName == "GetNestedType") member = type.GetNestedType(name, Audit.All);
                            else member = AccessTools.Method(type, name, parameters);
                            if (member == null) throw new MissingMemberException(type.FullName, name);
                            boundMembers.Add(member);
                            resolved.Add(new { owner = type.FullName, name, signature = member.ToString(), assembly = member.DeclaringType?.Assembly.GetName().Name,
                                explicitParameterTypes = parameters?.Select(t => t.FullName).ToArray(), fieldRefType = fieldExpected?.FullName });
                        }
                    }
                    lookupCalls[(tree.FilePath, call.SpanStart)] = boundMembers;
                    SyntaxNode? variableNode = call.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
                    ISymbol? variableSymbol = variableNode == null ? null : model.GetDeclaredSymbol(variableNode);
                    if (variableSymbol == null && call.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault() is { } assignment)
                        variableSymbol = model.GetSymbolInfo(assignment.Left).Symbol;
                    if (variableSymbol != null) lookupSymbols[variableSymbol] = boundMembers;
                    a.Add("dynamic", id, "PASS", string.Join("; ", targetTypes.Select(t => t.FullName)) + "." + string.Join("/", names), source,
                        enumeration ? "Bounded reflection enumeration resolved; instance values/callbacks not executed. Exact transpiler field use is separately checked."
                        : fieldExpected != null ? "Actual AccessTools FieldRef delegate constructed and field type checked; delegate not invoked."
                        : parameters != null ? "Actual AccessTools target resolved with explicit parameter types; existence and requested overload checked."
                        : "Actual AccessTools name lookup resolved unambiguously; absence of explicit Type[] is recorded, not an asserted caller signature.", new { code, resolved });
                });
            }
            foreach (var attribute in tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>().Where(x => x.Name.ToString() is "HarmonyPatch" or "HarmonyPatchAttribute"))
            {
                string id = "A" + (++number).ToString("D3");
                string source = a.Relative(tree.FilePath) + ":" + (tree.GetLineSpan(attribute.Span).StartLinePosition.Line + 1);
                var declaration = attribute.Ancestors().OfType<TypeDeclarationSyntax>().First();
                string? className = (model.GetDeclaredSymbol(declaration) as INamedTypeSymbol)?.ToDisplayString();
                var results = className != null ? a.Findings.Where(f => f.Category == "harmony-class" && f.Id == className).ToArray() : Array.Empty<Finding>();
                string status = results.Length == 1 && results[0].Status == "PASS" ? "PASS" : "UNRESOLVED";
                a.Add("dynamic-attribute", id, status, className ?? declaration.Identifier.ValueText, source,
                    "HarmonyPatch declaration linked to actual class-processor and per-hook target records; see harmony-target/harmony-parameters/transpiler gates.", new { code = attribute.ToString(), targets = className == null ? null : a.Bindings.GetValueOrDefault(className)?.Select(b => Audit.Signature(b.Target)).Distinct().ToArray() });
            }
        }
        // A name lookup and its reflection invocation are separate gates. Do not invoke game code.
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbol = model.GetSymbolInfo(call).Symbol as IMethodSymbol;
                string owner = symbol?.ContainingType.ToDisplayString() ?? "";
                if (!owner.StartsWith("System.Reflection.", StringComparison.Ordinal) || symbol?.Name is not ("Invoke" or "GetValue" or "SetValue")) continue;
                string id = "R" + (++number).ToString("D3");
                string source = a.Relative(tree.FilePath) + ":" + (tree.GetLineSpan(call.Span).StartLinePosition.Line + 1);
                var span = tree.GetLineSpan((call.Ancestors().FirstOrDefault(n => n is StatementSyntax) ?? call).Span);
                sites.Add(new(a.Relative(tree.FilePath), span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1, symbol.Name, id));
                var receiver = call.Expression is MemberAccessExpressionSyntax access ? access.Expression
                    : call.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression;
                var members = receiver == null ? new List<MemberInfo>() : Lookup(receiver);
                if (members.Count == 0)
                {
                    if (historyType != null && source.Contains("/ShinGetterEventTransaction.cs:", StringComparison.Ordinal))
                    {
                        a.Add("reflection-signature", id, "PASS", historyType.FullName + "." + symbol.Name, source,
                            "Bounded metadata PASS over the enumerated History PropertyInfo set; public getter/no-indexer and writable-int/IList branches checked. Actual instance calls/rollback remain runtime pending.", new { code = call.ToString(), historyContract });
                        continue;
                    }
                    a.Add("reflection-signature", id, "UNRESOLVED", owner + "." + symbol.Name, source,
                        "Reflection member comes from runtime enumeration/data flow not bounded by a static string lookup; no instance invocation performed.", new { code = call.ToString() });
                    continue;
                }
                a.Guard("reflection-signature", id, owner + "." + symbol.Name, source, () =>
                {
                    var checks = new List<object>();
                    bool unresolved = false;
                    foreach (var member in members)
                    {
                        var args = call.ArgumentList.Arguments.Select(x => x.Expression).ToArray();
                        ParameterInfo[] expected;
                        ExpressionSyntax[]? values;
                        if (symbol.Name == "Invoke" && member is MethodBase method)
                        {
                            expected = method.GetParameters();
                            if (args.Length != 2) { unresolved = true; continue; }
                            values = args[1].IsKind(SyntaxKind.NullLiteralExpression) ? Array.Empty<ExpressionSyntax>()
                                : args[1] switch { ArrayCreationExpressionSyntax array => array.Initializer?.Expressions.ToArray(),
                                    ImplicitArrayCreationExpressionSyntax array => array.Initializer.Expressions.ToArray(), _ => null };
                        }
                        else if (symbol.Name == "SetValue")
                        {
                            Type valueType = member is FieldInfo field ? field.FieldType : member is PropertyInfo property ? property.PropertyType : throw new InvalidOperationException("Unexpected SetValue target");
                            if (member is PropertyInfo propertyInfo && propertyInfo.SetMethod == null) throw new MissingMethodException("Reflected property setter missing: " + propertyInfo);
                            values = args.Length >= 2 ? new[] { args[1] } : null;
                            expected = Array.Empty<ParameterInfo>();
                            if (values != null) CheckValue(values[0], valueType);
                            checks.Add(new { target = member.ToString(), valueType = valueType.ToString(), arguments = values?.Select(v => v.ToString()).ToArray() });
                            continue;
                        }
                        else
                        {
                            if (member is PropertyInfo p && (p.GetMethod == null || p.GetIndexParameters().Length != 0)) throw new MissingMethodException("Property getter/indexer shape mismatch.");
                            checks.Add(new { target = member.ToString(), operation = "read field/property metadata; instance value not read" });
                            continue;
                        }
                        if (values == null) { unresolved = true; continue; }
                        if (values.Length != expected.Length) throw new TargetParameterCountException($"{member}: expected {expected.Length}, source supplies {values.Length}");
                        for (int i = 0; i < values.Length; i++) CheckValue(values[i], expected[i].ParameterType);
                        checks.Add(new { target = member.ToString(), expected = expected.Select(p => p.ParameterType.ToString()).ToArray(), arguments = values.Select(v => v.ToString()).ToArray() });
                    }
                    a.Add("reflection-signature", id, unresolved ? "UNRESOLVED" : "PASS", string.Join("; ", members.Select(m => m.DeclaringType?.FullName + "::" + m)), source,
                        unresolved ? "Target bound but some source argument types/data flow remain runtime-dependent; no invocation performed."
                        : "Source reflection invocation argument count/types or field/property operation checked against actual target metadata; instance state/behavior not executed.", new { code = call.ToString(), checks });
                    void CheckValue(ExpressionSyntax value, Type expected)
                    {
                        expected = expected.IsByRef ? expected.GetElementType()! : expected;
                        if (value.IsKind(SyntaxKind.NullLiteralExpression))
                        {
                            if (expected.IsValueType && Nullable.GetUnderlyingType(expected) == null) throw new InvalidOperationException("null passed to value-type reflected parameter.");
                            return;
                        }
                        var actual = RuntimeType(a, model.GetTypeInfo(value).Type);
                        ExpressionSyntax unwrapped = value;
                        while (unwrapped is PostfixUnaryExpressionSyntax unary) unwrapped = unary.Operand;
                        if (actual == typeof(object) && unwrapped is InvocationExpressionSyntax nested && nested.Expression is MemberAccessExpressionSyntax nestedMember)
                        {
                            var returnMethods = Lookup(nestedMember.Expression).OfType<MethodInfo>().ToArray();
                            if (returnMethods.Length == 1)
                            {
                                actual = returnMethods[0].ReturnType;
                                checks.Add(new { nestedReflectionReturn = Audit.MethodIdentity(returnMethods[0]), inferredReturnType = actual.FullName,
                                    expectedType = expected.FullName, instanceInvocation = "NOT RUN" });
                            }
                        }
                        if (actual == null || actual == typeof(object)) { unresolved = true; return; }
                        if (!expected.IsAssignableFrom(actual)) throw new InvalidOperationException($"Reflected parameter type mismatch: expected={expected}, source={actual}, expression={value}");
                    }
                });
                List<MemberInfo> Lookup(ExpressionSyntax expression)
                {
                    if (expression is PostfixUnaryExpressionSyntax unary) return Lookup(unary.Operand);
                    if (lookupCalls.TryGetValue((tree.FilePath, expression.SpanStart), out var calls)) return calls;
                    var value = model.GetSymbolInfo(expression).Symbol;
                    return value != null && lookupSymbols.TryGetValue(value, out var bindings) ? bindings : new();
                }
            }
            foreach (var creation in tree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var symbol = model.GetSymbolInfo(creation).Symbol as IMethodSymbol;
                if (symbol?.ContainingType.ToDisplayString() != "HarmonyLib.HarmonyMethod") continue;
                string source = a.Relative(tree.FilePath) + ":" + (tree.GetLineSpan(creation.Span).StartLinePosition.Line + 1);
                a.Add("dynamic-manual-patch", "M" + (++number).ToString("D3"), "SCOPED_OUT", "HarmonyMethod callback descriptor", source,
                    "Optional St2bs adapter descriptor only; not part of ShinGetter-only installed registry and not executed.", new { code = creation.ToString() });
            }
        }
        a.Add("runtime", "initial-fourth-options-instance-invoke", "UNRESOLVED", "EventModel.GenerateInitialOptionsWrapper -> SetEventState", "shin-getter-mod-godot/src/Events/ShinGetterEventInvasionService.Issue238.cs:429",
            "Native pending: actual model instance, null/value behavior and invocation side effects. Nested MethodInfo.ReturnType assignability is checked separately without invoking the game method.");
        foreach (var method in a.Types.SelectMany(t => t.Methods).Where(m => m.HasBody))
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference call) continue;
            string owner = call.DeclaringType.FullName;
            bool reflection = owner == "HarmonyLib.AccessTools" || owner == "System.Type" && call.Name.StartsWith("Get", StringComparison.Ordinal)
                || owner == "System.Reflection.Assembly" && call.Name == "GetType" || owner.StartsWith("HarmonyLib.Traverse", StringComparison.Ordinal)
                || owner == "System.Activator" || owner.StartsWith("System.Reflection.", StringComparison.Ordinal) && call.Name is "Invoke" or "GetValue" or "SetValue";
            if (!reflection) continue;
            if (owner == "System.Type" && call.Name == "GetTypeFromHandle")
            {
                a.Add("compiler-type-handle", method.MetadataToken + ":IL_" + instruction.Offset.ToString("X4"), "PASS", call.FullName,
                    a.Location(method, instruction.Offset), "Compiler typeof(RuntimeTypeHandle) bridge, not a string-named lookup. TypeRef binding is audited separately.");
                continue;
            }
            if (owner == "System.Activator" && call is GenericInstanceMethod && call.Name == "CreateInstance")
            {
                a.Add("compiler-generic-construction", method.MetadataToken + ":IL_" + instruction.Offset.ToString("X4"), "SCOPED_OUT", call.FullName,
                    a.Location(method, instruction.Offset), "Compiler new T() bridge with new() constraint; no string target. Actual Godot control construction is outside offline audit.");
                continue;
            }
            string source = a.Location(method, instruction.Offset);
            int colon = source.LastIndexOf(':');
            string file = colon > 0 ? source[..colon] : source;
            int line = colon > 0 && int.TryParse(source[(colon + 1)..], out int n) ? n : -100;
            var matching = sites.Where(s => s.File == file && s.Api == call.Name && line >= s.Start && line <= s.End).Select(s => s.Id).ToArray();
            a.Add("dynamic-il", method.MetadataToken + ":IL_" + instruction.Offset.ToString("X4"), matching.Length > 0 ? "PASS" : "UNRESOLVED", call.FullName, source,
                matching.Length > 0 ? "Compiled reflection/AccessTools IL call linked to source inventory; link does not override target verdict." : "Compiled reflection call lacks a matched source inventory site; explicit coverage gap.",
                new { caller = method.FullName, sourceIds = matching });
        }
    }

    private static Type[] Owners(Audit a, SemanticModel model, ExpressionSyntax expression, string? containingClass)
    {
        if (expression is TypeOfExpressionSyntax t) return new[] { RuntimeType(a, model.GetTypeInfo(t.Type).Type) }.OfType<Type>().ToArray();
        if (expression is InvocationExpressionSyntax invocation && invocation.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "GetType")
        {
            if (member.Expression.ToString() == "internalData" && containingClass?.EndsWith("VigorPowerSetAmountPatch", StringComparison.Ordinal) == true)
            {
                using var native = ModuleDefinition.ReadModule(a.Game.Location);
                var vigor = native.Types.Single(t => t.FullName == "MegaCrit.Sts2.Core.Models.Powers.VigorPower");
                bool dataUsed = vigor.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i =>
                    i.Operand is GenericInstanceMethod { Name: "GetInternalData" } generic
                    && generic.GenericArguments.Single().FullName == vigor.FullName + "/Data");
                if (!dataUsed) return Array.Empty<Type>();
                return a.Game.GetType(vigor.FullName, true)!.GetNestedTypes(Audit.All).Where(n => n.Name == "Data").ToArray();
            }
            var runtime = RuntimeType(a, model.GetTypeInfo(member.Expression).Type);
            if (runtime != null && runtime != typeof(object) && runtime != typeof(Type)) return new[] { runtime };
        }
        if (containingClass != null && a.Bindings.TryGetValue(containingClass, out var bindings)
            && (expression.ToString() is "machine" or "stateMachine" || expression.ToString().Contains("DeclaringType", StringComparison.Ordinal)))
            return bindings.Select(b => b.Target.DeclaringType!).Distinct().ToArray();
        return Array.Empty<Type>();
    }

    private static IEnumerable<string> Names(SemanticModel model, ExpressionSyntax expression, int depth = 0)
    {
        if (depth > 8) yield break;
        var constant = model.GetConstantValue(expression);
        if (constant is { HasValue: true, Value: string text }) { yield return text; yield break; }
        if (expression is ConditionalExpressionSyntax conditional)
        {
            foreach (var value in Names(model, conditional.WhenTrue, depth + 1).Concat(Names(model, conditional.WhenFalse, depth + 1))) yield return value;
        }
        else if (expression is IdentifierNameSyntax)
        {
            var symbol = model.GetSymbolInfo(expression).Symbol;
            if (symbol != null) foreach (var declaration in symbol.DeclaringSyntaxReferences)
            {
                var syntax = declaration.GetSyntax();
                if (syntax is VariableDeclaratorSyntax { Initializer: not null } variable)
                    foreach (var value in Names(model, variable.Initializer.Value, depth + 1)) yield return value;
                if (syntax is ForEachStatementSyntax loop)
                    foreach (var value in Names(model, loop.Expression, depth + 1)) yield return value;
            }
        }
        else if (expression is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax)
            foreach (var item in expression.DescendantNodes().OfType<InitializerExpressionSyntax>().SelectMany(i => i.Expressions))
                foreach (var value in Names(model, item, depth + 1)) yield return value;
    }

    private static Type? RuntimeType(Audit a, ITypeSymbol? symbol)
    {
        if (symbol is IArrayTypeSymbol array) return RuntimeType(a, array.ElementType)?.MakeArrayType();
        if (symbol is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error) return null;
        var parts = new Stack<string>();
        for (var current = named.OriginalDefinition; current != null; current = current.ContainingType) parts.Push(current.MetadataName);
        string name = (named.ContainingNamespace.IsGlobalNamespace ? "" : named.ContainingNamespace.ToDisplayString() + ".") + string.Join("+", parts);
        Type? type = a.Game.GetType(name) ?? a.Mod.GetType(name) ?? Type.GetType(name);
        if (type == null)
        {
            try { type = Assembly.Load(named.ContainingAssembly.Name).GetType(name); }
            catch (Exception ex) when (ex is FileNotFoundException or FileLoadException) { return null; }
        }
        if (type != null && named.IsGenericType && !named.IsUnboundGenericType)
        {
            var args = named.TypeArguments.Select(t => RuntimeType(a, t)).ToArray();
            if (args.Any(t => t == null)) return null;
            type = type.MakeGenericType(args!);
        }
        return type;
    }
}
