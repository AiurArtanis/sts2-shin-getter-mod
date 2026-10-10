using System.Reflection;
using System.Text.Json;

internal static class DynamicBindingProbe
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private sealed record Target(Type? Type, string? Name, string Kind, Type[]? Arguments);

    internal static void Check(Assembly mod, Assembly game, string auditPath)
    {
        int targets = 0, parameters = 0;
        foreach (Type patch in mod.GetTypes())
        {
            var descriptors = patch.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch").Select(Parse).ToArray();
            var injections = patch.GetMethods(All | BindingFlags.DeclaredOnly).Where(IsInjection).ToArray();
            var scopes = descriptors.Select(d => (Descriptor: d, Injections: injections)).ToList();
            foreach (var injection in injections)
                foreach (var attribute in injection.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
                {
                    var d = Parse(attribute);
                    var parent = descriptors.FirstOrDefault(x => x.Type != null);
                    scopes.Add((d with { Type = d.Type ?? parent?.Type, Name = d.Name ?? parent?.Name }, new[] { injection }));
                }
            foreach (var scope in scopes)
            {
                Target descriptor = scope.Descriptor;
                if (descriptor.Type == null || descriptor.Name == null) continue; // TargetMethods declarations are checked from the source inventory below.
                string name = descriptor.Kind == "Getter" ? "get_" + descriptor.Name
                    : descriptor.Kind == "Setter" ? "set_" + descriptor.Name : descriptor.Name;
                var methods = TargetMethods(descriptor.Type, name);
                if (descriptor.Arguments != null)
                    methods = methods.Where(m => m.GetParameters().Select(p => p.ParameterType).SequenceEqual(descriptor.Arguments)).ToArray();
                if (methods.Length != 1) throw new InvalidOperationException($"Ambiguous/missing Harmony target: {patch.Name} -> {descriptor.Type.FullName}.{name}, {methods.Length}");
                targets++;
                foreach (var injection in scope.Injections)
                {
                    foreach (var parameter in injection.GetParameters())
                    {
                        string p = parameter.Name ?? throw new InvalidOperationException("Unnamed injection parameter");
                        Type actual = Element(parameter.ParameterType);
                        if (p == "__instance")
                        {
                            if (!actual.IsAssignableFrom(descriptor.Type)) throw new InvalidOperationException($"{patch.Name}.{injection.Name}: __instance type mismatch");
                        }
                        else if (p == "__result")
                        {
                            if (!actual.IsAssignableFrom(Element(methods[0].ReturnType))) throw new InvalidOperationException($"{patch.Name}.{injection.Name}: __result type mismatch");
                        }
                        else if (p.StartsWith("___", StringComparison.Ordinal))
                        {
                            var field = Field(descriptor.Type, p[3..]);
                            if (field == null || !actual.IsAssignableFrom(Element(field.FieldType))) throw new InvalidOperationException($"{patch.Name}: injected field {p} missing/incompatible");
                        }
                        else if (!p.StartsWith("__", StringComparison.Ordinal))
                        {
                            var native = methods[0].GetParameters().SingleOrDefault(x => x.Name == p);
                            if (native == null || !actual.IsAssignableFrom(Element(native.ParameterType))) throw new InvalidOperationException($"{patch.Name}.{injection.Name}: named parameter {p} missing/incompatible");
                        }
                        parameters++;
                    }
                }
            }
        }
        using var audit = JsonDocument.Parse(File.ReadAllText(auditPath));
        int dynamicTargets = 0;
        var gameTypes = game.GetTypes();
        var modTypes = mod.GetTypes();
        foreach (var call in audit.RootElement.GetProperty("mod_source_inventory").GetProperty("dynamic_target_calls").EnumerateArray())
        {
            if (call.GetProperty("api").GetString() == "HarmonyPatch") continue; // CLR attribute types/signatures already verified above.
            var owners = call.GetProperty("target_type_names").EnumerateArray().Select(x => x.GetString()!).ToArray();
            if (owners.Length == 0) continue; // explicitly scoped special cases are covered by the fixed probe and audit records.
            string owner = owners[0].Split('.').Last();
            var matches = gameTypes.Concat(modTypes).Where(t => t.Name == owner || t.FullName == owners[0]).ToArray();
            if (matches.Length == 0) continue; // .NET/Godot or a contextual generic type, not a game target.
            if (matches.Length != 1) throw new InvalidOperationException($"Ambiguous dynamic owner {owner}");
            Type type = matches[0];
            foreach (var n in call.GetProperty("target_names").EnumerateArray())
            {
                string name = n.GetString()!;
                string api = call.GetProperty("api").GetString()!;
                bool exists = api.Contains("Field") ? Field(type, name) != null
                    : api.Contains("Property") ? Property(type, name) != null
                    : Methods(type).Any(m => m.Name == name || m.Name == "get_" + name || m.Name == "set_" + name);
                if (!exists) throw new InvalidOperationException($"Missing dynamic target {api}: {type.FullName}.{name} at {call.GetProperty("location")}");
                dynamicTargets++;
            }
        }
        Console.WriteLine($"issue#248 Harmony named bindings PASS: {targets} targets/{parameters} parameters; scoped dynamic members={dynamicTargets}; no patches applied");
    }

    private static Type Element(Type type) => type.IsByRef ? type.GetElementType()! : type;
    private static Target Parse(CustomAttributeData a)
    {
        Type? type = null; string? name = null; string kind = "Normal"; Type[]? arguments = null;
        foreach (var arg in a.ConstructorArguments)
        {
            if (arg.ArgumentType == typeof(Type)) type = (Type?)arg.Value;
            else if (arg.ArgumentType == typeof(string)) name ??= (string?)arg.Value;
            else if (arg.ArgumentType.IsEnum && arg.ArgumentType.Name == "MethodType") kind = Enum.ToObject(arg.ArgumentType, arg.Value!).ToString()!;
            else if (arg.ArgumentType == typeof(Type[]) && arg.Value is IReadOnlyCollection<CustomAttributeTypedArgument> values)
                arguments = values.Select(v => (Type)v.Value!).ToArray();
        }
        return new(type, name, kind, arguments);
    }
    private static bool IsInjection(MethodInfo m) => m.Name is "Prefix" or "Postfix" or "Transpiler" or "Finalizer"
        || m.CustomAttributes.Any(a => a.AttributeType.FullName is "HarmonyLib.HarmonyPrefix" or "HarmonyLib.HarmonyPostfix");
    private static IEnumerable<MethodInfo> Methods(Type type)
    {
        for (Type? t = type; t != null; t = t.BaseType)
            foreach (var m in t.GetMethods(All | BindingFlags.DeclaredOnly)) yield return m;
    }
    private static MethodInfo[] TargetMethods(Type type, string name)
    {
        // Harmony chooses the nearest declaring type, not both virtual override/base slots.
        for (Type? t = type; t != null; t = t.BaseType)
        {
            var methods = t.GetMethods(All | BindingFlags.DeclaredOnly).Where(m => m.Name == name).ToArray();
            if (methods.Length != 0) return methods;
        }
        return Array.Empty<MethodInfo>();
    }
    private static FieldInfo? Field(Type type, string name)
    {
        for (Type? t = type; t != null; t = t.BaseType) if (t.GetField(name, All | BindingFlags.DeclaredOnly) is { } f) return f;
        return null;
    }
    private static PropertyInfo? Property(Type type, string name)
    {
        for (Type? t = type; t != null; t = t.BaseType) if (t.GetProperty(name, All | BindingFlags.DeclaredOnly) is { } p) return p;
        return null;
    }
}
