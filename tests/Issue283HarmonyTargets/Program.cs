using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

const BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
var harmony = typeof(Harmony).Assembly;
var native = typeof(EventSynchronizer).Assembly;
static string Hash(Assembly assembly) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)));
static void Require(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
Require(Hash(native) == "C2D3E15310259957BA312F9D2362CBA193512EBE9819456A062366E6AF38B9B0", "Expected the pinned formal109 assembly.");
Require(Hash(harmony) == "EF1898322C9F5C86DC1B0758B272A9C440823B4A41CA9A0B82A3AA6B3D206387", "Expected the actual reviewed 0Harmony assembly.");
Console.WriteLine($"REAL_HARMONY {harmony.GetName().Version} SHA256={Hash(harmony)}");
Console.WriteLine($"FORMAL109 SHA256={Hash(native)}");

// The real class processor merges attributes and defaults an unspecified method type to Normal.
// Read its processed description, then use Harmony's own original-target resolver without Patch().
var engine = new Harmony("shin-getter.issue283.target-resolution-probe");
FieldInfo descriptionField = typeof(PatchClassProcessor)
    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    .Single(field => field.FieldType == typeof(HarmonyMethod));
MethodInfo resolve = harmony.GetType("HarmonyLib.PatchTools", throwOnError: true)?
    .GetMethod("GetOriginalMethod", StaticMethods, new[] { typeof(HarmonyMethod) })
    ?? throw new InvalidOperationException("Harmony's original-target resolver missing.");
HarmonyMethod Description(Type type) => (HarmonyMethod)(descriptionField.GetValue(new PatchClassProcessor(engine, type))
    ?? throw new InvalidOperationException("No real Harmony attribute on " + type.FullName));
MethodBase? Resolve(Type type) => (MethodBase?)resolve.Invoke(null, new object[] { Description(type) });

Require(typeof(EventSynchronizer).GetConstructor(Type.EmptyTypes) == null, "Negative control requires no parameterless native constructor.");
Require(Resolve(typeof(MissingSignature)) == null, "Missing constructor signature must be rejected, not silently accepted.");
Require(Resolve(typeof(WrongSignature)) == null, "Wrong constructor signature must be rejected.");
Console.WriteLine("NEGATIVE_CONTROLS missing-signature=REJECTED wrong-signature=REJECTED");

var expected = new Dictionary<string, (Type Type, string Name)>
{
    ["ShinGetterTrialSyncInitPatch"] = (typeof(EventSynchronizer), ".ctor"),
    ["ShinGetterTrialProceedRoutePatch"] = (typeof(NEventRoom), "OptionButtonClicked"),
    ["ShinGetterTrialSyncBeginPatch"] = (typeof(EventSynchronizer), "BeginEvent"),
    ["ShinGetterTrialSyncChoicePatch"] = (typeof(EventSynchronizer), "ChooseOptionForEvent"),
    ["ShinGetterTrialSyncExitPatch"] = (typeof(EventRoom), "Exit"),
    ["ShinGetterTrialSyncDisposePatch"] = (typeof(EventSynchronizer), "Dispose"),
    ["ShinGetterTrialSyncRunCleanupPatch"] = (typeof(RunManager), "CleanUp"),
};
Type[] patches = Assembly.GetExecutingAssembly().GetTypes()
    .Where(type => type.Namespace == "ShinGetterMod.Patches" && type.Name.StartsWith("ShinGetterTrial", StringComparison.Ordinal))
    .OrderBy(type => type.Name == "ShinGetterTrialSyncInitPatch" ? 0 : 1).ThenBy(type => type.Name).ToArray();
Require(patches.Length == expected.Count, "Unexpected production patch set; update review coverage explicitly.");
int hooks = 0;
foreach (Type patch in patches)
{
    MethodBase original = Resolve(patch) ?? throw new InvalidOperationException("Undefined real Harmony target: " + patch.FullName);
    Require(expected.TryGetValue(patch.Name, out var target) && original.DeclaringType == target.Type && original.Name == target.Name,
        "Unexpected target for " + patch.FullName);
    if (original.IsConstructor)
    {
        Type[] fullSignature = { typeof(RunLocationTargetedMessageBuffer), typeof(INetGameService), typeof(IPlayerCollection), typeof(ulong), typeof(uint) };
        Require(Description(patch).argumentTypes?.SequenceEqual(fullSignature) == true, "Constructor attribute must state the exact five-parameter signature.");
        Require(original.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(fullSignature), "Resolved native constructor differs.");
    }
    foreach (MethodInfo hook in patch.GetMethods(StaticMethods | BindingFlags.DeclaredOnly)
        .Where(method => method.Name is "Prefix" or "Postfix" or "Finalizer"))
    {
        foreach (ParameterInfo parameter in hook.GetParameters())
        {
            if (parameter.Name == "__instance")
                Require(parameter.ParameterType.IsAssignableFrom(original.DeclaringType), "Wrong instance injection: " + hook);
            else if (parameter.Name == "__exception")
                Require(hook.Name == "Finalizer" && parameter.ParameterType == typeof(Exception), "Wrong exception injection: " + hook);
            else
                Require(original.GetParameters().Any(nativeParameter => nativeParameter.Name == parameter.Name
                    && nativeParameter.ParameterType == parameter.ParameterType), "Hook parameter absent/mismatched on real target: " + hook + " / " + parameter.Name);
        }
        hooks++;
    }
    Console.WriteLine($"TARGET_OK {patch.Name} -> {original.DeclaringType?.FullName}::{original}");
}
Require(hooks == 9, "Unexpected number of actual production hooks.");
Console.WriteLine($"issue#283 actual-Harmony target gate PASS: {patches.Length} targets / {hooks} hooks / 2 negative controls; no Patch/PatchAll, Godot engine, callbacks or gameplay executed.");

[HarmonyPatch(typeof(EventSynchronizer), MethodType.Constructor)]
internal static class MissingSignature { }

[HarmonyPatch(typeof(EventSynchronizer), MethodType.Constructor, new Type[]
{
    typeof(RunLocationTargetedMessageBuffer), typeof(INetGameService), typeof(IPlayerCollection), typeof(ulong), typeof(int),
})]
internal static class WrongSignature { }
