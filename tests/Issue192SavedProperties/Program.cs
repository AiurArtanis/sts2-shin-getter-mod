using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Relics;

static class Program
{
    private const BindingFlags Properties = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int _assertions;
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };

    public static void Main()
    {
        Assembly mod = typeof(Entry).Assembly;
        Type[] types = mod.GetTypes().Where(t => !t.IsAbstract && !t.ContainsGenericParameters
            && typeof(AbstractModel).IsAssignableFrom(t)).OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        Require(mod.GetType("ShinGetterMod.Patches.ShinGetterSavedPropertiesPatch") == null, "obsolete cache hook remains");
        // The standalone process has no Godot platform or print bridge.
        var harmony = new Harmony("ShinGetterMod.Tests.Issue192");
        harmony.Patch(AccessTools.Method(typeof(OS), nameof(OS.GetCmdlineArgs)), prefix: new HarmonyMethod(typeof(Program), nameof(NoEngineArgs)));
        harmony.Patch(AccessTools.Method(typeof(OS), nameof(OS.HasFeature)), prefix: new HarmonyMethod(typeof(Program), nameof(NoEngineFeature)));
        harmony.Patch(AccessTools.Method(typeof(ConsoleLogPrinter), nameof(ConsoleLogPrinter.Print)),
            prefix: new HarmonyMethod(typeof(Program), nameof(PrintToConsole)));
        AssemblyInfo.Init();
        AssemblyInfo.ModMap!.Add(mod, new Mod { path = "fixture", state = ModLoadState.Loaded,
            manifest = new ModManifest { id = "ShinGetterMod" }, assemblies = [mod] });
        InitializeModels(types);
        int cached = 0;
        foreach (Type type in types)
        {
            PropertyInfo[] expected = type.GetProperties(Properties)
                .Where(p => p.GetCustomAttribute<SavedPropertyAttribute>() != null)
                .OrderBy(p => p.GetCustomAttribute<SavedPropertyAttribute>()!.order)
                .ThenBy(p => p.Name, StringComparer.Ordinal).ToArray();
            var actual = ModelIdSerializationCache.GetJsonPropertiesForType(type);
            if (expected.Length == 0) continue;
            Require(actual != null, $"cache missing: {type.Name}");
            Require(actual!.Select(p => p.Name).SequenceEqual(expected.Select(p => p.Name)), $"properties/order: {type.Name}");
            cached++;
        }
        var firstIds = Ids();
        int firstWidth = ModelIdSerializationCache.PropertyIdBitSize;
        uint firstHash = ModelIdSerializationCache.Hash;
        InitializeModels(types.Reverse().ToArray());
        Require(Ids().SequenceEqual(firstIds), "reverse registration changed property IDs");
        Require(ModelIdSerializationCache.PropertyIdBitSize == firstWidth, "reverse registration changed bit width");
        Require(ModelIdSerializationCache.Hash == firstHash, "reverse registration changed the native registry hash");

        int nameCount = firstIds.Length;
        Require((1L << firstWidth) >= nameCount, "network bit width too small");
        Require(firstWidth == 0 || (1L << (firstWidth - 1)) < nameCount, "network bit width not minimal");
        for (int i = 0; i < firstIds.Length; i++)
            Require(ModelIdSerializationCache.GetNetIdForPropertyName(firstIds[i]) == i, "network ID round trip");

        var citizen = (SGR_GoodCitizenCard)ModelDb.Relic<SGR_GoodCitizenCard>().ToMutable();
        citizen.LastFreeFloor = 7;
        citizen.FreePurchaseActIndices.AddRange([0, 2]);
        citizen.IsWax = true;
        citizen.IsMelted = true;
        var saved = SaveRoundTrip(citizen);
        Require(saved.GetProperty("ints").EnumerateArray().Any(x => x.GetProperty("name").GetString() == "LastFreeFloor"
            && x.GetProperty("value").GetInt32() == 7), "non-default purchase floor absent");
        var restored = (SGR_GoodCitizenCard)ModelDb.Relic<SGR_GoodCitizenCard>().ToMutable();
        JsonSerializer.Deserialize<SavedProperties>(saved.GetRawText(), JsonOptions)!.Fill(restored);
        Require(restored.LastFreeFloor == 7 && restored.FreePurchaseActIndices.SequenceEqual([0, 2]), "purchase state round trip");
        Require(restored.IsWax && restored.IsMelted, "inherited state round trip");
        var writer = new PacketWriter();
        SavedProperties.From(citizen)!.Serialize(writer);
        var reader = new PacketReader();
        reader.Reset(writer.Buffer);
        var packetProps = new SavedProperties();
        packetProps.Deserialize(reader);
        Require(reader.BitPosition == writer.BitPosition, "packet bit length mismatch");
        Require(JsonSerializer.Serialize(packetProps, JsonOptions) == saved.GetRawText(), "native packet round trip mismatch");
        restored.FreePurchaseActIndices.Add(9);
        Require(citizen.FreePurchaseActIndices.SequenceEqual([0, 2]), "restored history shares mutable list");

        var empty = (SGR_GoodCitizenCard)ModelDb.Relic<SGR_GoodCitizenCard>().ToMutable();
        Require(SaveRoundTrip(empty).GetProperty("int_arrays").EnumerateArray()
            .Any(x => x.GetProperty("name").GetString() == "SavedFreePurchaseActIndices" && x.GetProperty("value").GetArrayLength() == 0),
            "AlwaysSave empty array absent");

        foreach (Type type in new[] { typeof(SGR_GetterFurnace), typeof(SGR_EmperorsFragment) })
        {
            var relic = ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable();
            type.GetProperty("PlayedVoiceMask")!.SetValue(relic, 2);
            type.GetProperty("PlayedVoiceMaskHigh")!.SetValue(relic, 4);
            type.GetProperty("OpeningVoiceMask")!.SetValue(relic, 1);
            type.GetProperty("CombatStartVoiceCount")!.SetValue(relic, 3);
            type.GetProperty("InfiniteEvolutionStrengthGain")!.SetValue(relic, 9);
            string json = SaveRoundTrip(relic).GetRawText();
            Require(json.Contains("EventInvasionEnabled") && json.Contains("InfiniteEvolutionStrengthGain"), "non-voice positive save missing");
            Require(!json.Contains("Voice"), "local voice state leaked into native save");
            type.GetProperty("EventInvasionEnabled")!.SetValue(relic, false);
            var copy = ModelDb.GetById<RelicModel>(ModelDb.GetId(type)).ToMutable();
            JsonSerializer.Deserialize<SavedProperties>(SaveRoundTrip(relic).GetRawText(), JsonOptions)!.Fill(copy);
            Require(!(bool)type.GetProperty("EventInvasionEnabled")!.GetValue(copy)!, "disabled invasion lost on restore");
            Require((int)type.GetProperty("InfiniteEvolutionStrengthGain")!.GetValue(copy)! == 9, "shared evolution lost on restore");
        }
        var evolution = (SGC_InfiniteEvolution)ModelDb.Card<SGC_InfiniteEvolution>().ToMutable();
        evolution.PermanentStrengthGain = 5;
        Require(SaveRoundTrip(evolution).GetRawText().Contains("PermanentStrengthGain"), "card progress absent");
        // A relic with no own SavedProperty must still inherit native wax/melt persistence.
        var alloy = (SGR_AlloyPlate)ModelDb.Relic<SGR_AlloyPlate>().ToMutable();
        alloy.IsWax = true;
        Require(SaveRoundTrip(alloy).GetRawText().Contains("IsWax"), "unlisted relic inherited property absent");
        Console.WriteLine(JsonSerializer.Serialize(new { assertions = _assertions, models = types.Length,
            cached, names = nameCount, bitWidth = firstWidth, verdict = "PASS", scope = "managed-component-real-0.111.1-assemblies" }));
    }

    private static void InitializeModels(Type[] types)
    {
        ModelDb.ResetForTest();
        ModelIdSerializationCache.ResetForTest();
        ModelDb.Init(types);
        ModelIdSerializationCache.Init();
        ModelDb.InitIds();
    }

    private static string[] Ids() => Enumerable.Range(0, ModelIdSerializationCache.MaxPropertyId + 1)
        .Select(ModelIdSerializationCache.GetPropertyNameForNetId).ToArray();

    private static bool NoEngineArgs(ref string[] __result) { __result = []; return false; }
    private static bool NoEngineFeature(ref bool __result) { __result = false; return false; }
    private static bool PrintToConsole(LogLevel logLevel, string text)
    {
        Console.WriteLine($"[{logLevel}] {text}");
        return false;
    }

    private static JsonElement SaveRoundTrip(AbstractModel model)
    {
        SavedProperties? props = SavedProperties.From(model);
        Require(props != null, $"native save returned null: {model.GetType().Name}");
        return JsonSerializer.SerializeToElement(props, JsonOptions);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _assertions++;
    }
}
