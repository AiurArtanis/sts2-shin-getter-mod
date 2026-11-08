using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class Issue286Compatibility
{
    private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;

    internal static void Run(Assembly game, string productPath)
    {
        Assembly product = AssemblyLoadContext.Default.LoadFromAssemblyPath(productPath);
        Type state = product.GetType("ShinGetterMod.Services.ShinGetterPlayerEventState", true)!;
        Type checkpointType = state.GetNestedType("EnteredRoomCheckpoint", BindingFlags.NonPublic)!;
        Type visitType = state.GetNestedType("Visit", BindingFlags.NonPublic)!;
        Type dataType = state.GetNestedType("Data", BindingFlags.NonPublic)!;
        Type rngType = game.GetType("MegaCrit.Sts2.Core.Random.Rng", true)!;
        Type serialType = game.GetType("MegaCrit.Sts2.Core.Saves.SerializableRng", true)!;
        MethodInfo next = rngType.GetMethod("NextInt", new[] { typeof(int) })!;
        MethodInfo serialize = rngType.GetMethod("ToSerializable")!;
        MethodInfo restore = rngType.GetMethod("LoadFromSerializable")!;
        MethodInfo encode = state.GetMethod("Encode", HiddenStatic)!;
        MethodInfo decode = state.GetMethod("Decode", HiddenStatic)!;
        object New(Type type) => Activator.CreateInstance(type, nonPublic: true)!;
        void Set(object obj, string key, object value) => obj.GetType().GetProperty(key)!.SetValue(obj, value);
        object Get(object obj, string key) => obj.GetType().GetProperty(key)!.GetValue(obj)!;
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        object original = Activator.CreateInstance(rngType, new object[] { 286UL })!;
        foreach (int bound in new[] { 17, 11, 99 }) next.Invoke(original, new object[] { bound });
        object snapshot = serialize.Invoke(original, null)!;
        int counter = (int)serialType.GetField("counter")!.GetValue(snapshot)!;
        ulong[] words = Enumerable.Range(0, 4)
            .Select(i => (ulong)serialType.GetField("state" + i)!.GetValue(snapshot)!).ToArray();
        object checkpoint = New(checkpointType);
        Set(checkpoint, "RngCounter", counter);
        Set(checkpoint, "RngState111", words);
        object visit = New(visitType);
        Set(visit, "RoomCheckpoint", checkpoint);
        object data = New(dataType);
        Set(data, "Initialized", true);
        Set(data, "Enabled", true);
        Set(data, "Seed", "issue286-checkpoint-probe");
        ((IDictionary)Get(data, "Visits")).Add("probe", visit);
        string wire = (string)encode.Invoke(null, new[] { data })!;
        object decoded = decode.Invoke(null, new object[] { wire })!;
        object restoredVisit = ((IDictionary)Get(decoded, "Visits"))["probe"]!;
        object restoredCheckpoint = Get(restoredVisit, "RoomCheckpoint");
        ulong[] restoredWords = (ulong[])Get(restoredCheckpoint, "RngState111");
        Require(words.SequenceEqual(restoredWords), "Production checkpoint changed native RNG state words.");
        object nativeSave = New(serialType);
        serialType.GetField("counter")!.SetValue(nativeSave, Get(restoredCheckpoint, "RngCounter"));
        for (int i = 0; i < 4; i++) serialType.GetField("state" + i)!.SetValue(nativeSave, restoredWords[i]);
        object resumed = Activator.CreateInstance(rngType, new object[] { 1UL })!;
        restore.Invoke(resumed, new[] { nativeSave });
        for (int i = 0; i < 32; i++)
        {
            int bound = i % 2 == 0 ? 3 : 100003;
            Require(Equals(next.Invoke(original, new object[] { bound }), next.Invoke(resumed, new object[] { bound })),
                "Native RNG diverged after checkpoint JSON round trip.");
        }
        foreach (string mode in new[] { "missing", "short", "zero", "counter" })
        {
            JsonNode json = JsonNode.Parse(wire)!;
            JsonObject cp = json["Visits"]!["probe"]!["RoomCheckpoint"]!.AsObject();
            if (mode == "missing") cp.Remove("RngState111");
            if (mode == "short") cp["RngState111"] = JsonSerializer.SerializeToNode(new ulong[] { 1, 2 });
            if (mode == "zero") cp["RngState111"] = JsonSerializer.SerializeToNode(new ulong[] { 0, 0, 0, 0 });
            if (mode == "counter") cp["RngCounter"] = -1;
            bool rejected = false;
            try { decode.Invoke(null, new object[] { json.ToJsonString() }); }
            catch (TargetInvocationException ex) when (ex.InnerException is JsonException) { rejected = true; }
            Require(rejected, "Production decoder accepted invalid checkpoint: " + mode);
        }
        Type manager = game.GetType("MegaCrit.Sts2.Core.Saves.SaveManager", true)!;
        Type reload = product.GetType("ShinGetterMod.Patches.ShinGetterEventReloadSavePatch", true)!;
        MethodInfo wrapper = reload.GetMethod("SaveReloadCheckpoint", HiddenStatic)!;
        Type[] expected = new[] { manager }.Concat(manager.GetMethod("IncrementNumReloads")!
            .GetParameters().Select(p => p.ParameterType)).ToArray();
        Require(wrapper.GetParameters().Take(expected.Length).Select(p => p.ParameterType).SequenceEqual(expected),
            "Scoped reload wrapper does not preserve the complete native call signature.");
        Console.WriteLine("issue#286 native probe PASS: production checkpoint JSON, 32 RNG draws, 4 invalid payloads, exact reload-call prefix.");
        Console.WriteLine("No game initialization, Harmony patch installation, real save write, or gameplay was executed.");
    }
}
