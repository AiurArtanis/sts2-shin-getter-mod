using System.Reflection;
using System.Text.Json;
using HarmonyLib;
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

    public static void Main(string[] args)
    {
        string mode = args.FirstOrDefault() ?? "standalone";
        var originalIds = Ids();
        if (mode == "pre-registered")
        {
            // Component-only simulation of BaseLib's existing native cache registration.
            SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(SGR_GoodCitizenCard));
            SavedPropertiesTypeCache.InjectTypeIntoCache(typeof(ForeignSaveHolder));
        }
        var beforeIds = Ids();
        Assembly mod = typeof(Entry).Assembly;
        Type? patch = mod.GetType("ShinGetterMod.Patches.ShinGetterSavedPropertiesPatch");
        if (patch != null)
            new Harmony("ShinGetterMod.Tests.Issue192").CreateClassProcessor(patch).Patch();

        // Invoke the actual patched startup hook, with an empty model database and no game process.
        ModelDb.InitIds();
        Type[] types = mod.GetTypes().Where(t => !t.IsAbstract && !t.ContainsGenericParameters
            && typeof(AbstractModel).IsAssignableFrom(t)).OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        int cached = 0;
        foreach (Type type in types)
        {
            PropertyInfo[] expected = type.GetProperties(Properties)
                .Where(p => p.GetCustomAttribute<SavedPropertyAttribute>() != null)
                .OrderBy(p => p.GetCustomAttribute<SavedPropertyAttribute>()!.order)
                .ThenBy(p => p.Name, StringComparer.Ordinal).ToArray();
            var actual = SavedPropertiesTypeCache.GetJsonPropertiesForType(type);
            if (expected.Length == 0) continue;
            Require(actual != null, $"cache missing: {type.Name}");
            Require(actual!.Select(p => p.Name).SequenceEqual(expected.Select(p => p.Name)), $"properties/order: {type.Name}");
            cached++;
        }
        foreach (var pair in beforeIds)
            Require(Ids()[pair.Key] == pair.Value, $"existing ID changed: {pair.Key}");
        foreach (var pair in originalIds)
            Require(Ids()[pair.Key] == pair.Value, $"native ID changed: {pair.Key}");

        var firstIds = Ids();
        int firstWidth = SavedPropertiesTypeCache.NetIdBitSize;
        ModelDb.InitIds();
        Require(Ids().OrderBy(x => x.Key).SequenceEqual(firstIds.OrderBy(x => x.Key)), "repeat registration changed IDs");
        Require(SavedPropertiesTypeCache.NetIdBitSize == firstWidth, "repeat registration changed bit width");

        if (mode == "post-registered")
        {
            foreach (Type type in types.Reverse()) SavedPropertiesTypeCache.InjectTypeIntoCache(type);
            Require(Ids().OrderBy(x => x.Key).SequenceEqual(firstIds.OrderBy(x => x.Key)), "BaseLib-style repeat changed IDs");
        }

        int nameCount = Ids().Count;
        if (mode == "pre-registered")
            Require(firstWidth > 6, "coexistence fixture must exercise a network width boundary");
        Require((1L << SavedPropertiesTypeCache.NetIdBitSize) >= nameCount, "network bit width too small");
        Require(SavedPropertiesTypeCache.NetIdBitSize == 0 || (1L << (SavedPropertiesTypeCache.NetIdBitSize - 1)) < nameCount,
            "network bit width not minimal");
        foreach (var pair in Ids())
            Require(SavedPropertiesTypeCache.GetPropertyNameForNetId(pair.Value) == pair.Key, "network ID round trip");

        foreach (Type type in new[] { typeof(SGR_GoodCitizenCard), typeof(SGR_GetterFurnace),
                     typeof(SGR_EmperorsFragment), typeof(SGC_InfiniteEvolution), typeof(SGR_AlloyPlate) })
            ModelDb.Inject(type);
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
        Console.WriteLine(JsonSerializer.Serialize(new { mode, assertions = _assertions, models = types.Length,
            cached, names = nameCount, bitWidth = firstWidth, verdict = "PASS", scope = "managed-component-real-109-assemblies" }));
    }

    private static Dictionary<string, int> Ids()
    {
        _ = SavedPropertiesTypeCache.GetJsonPropertiesForType(typeof(SGR_GoodCitizenCard));
        return new((Dictionary<string, int>)AccessTools.Field(typeof(SavedPropertiesTypeCache), "_propertyNameToNetIdMap").GetValue(null)!);
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

sealed class ForeignSaveHolder
{
    [SavedProperty] public int ForeignCounter { get; set; }
    [SavedProperty] public int ForeignA { get; set; }
    [SavedProperty] public int ForeignB { get; set; }
    [SavedProperty] public int ForeignC { get; set; }
    [SavedProperty] public int ForeignD { get; set; }
    [SavedProperty] public int ForeignE { get; set; }
    [SavedProperty] public int ForeignF { get; set; }
    [SavedProperty] public int ForeignG { get; set; }
    [SavedProperty] public int ForeignH { get; set; }
}
