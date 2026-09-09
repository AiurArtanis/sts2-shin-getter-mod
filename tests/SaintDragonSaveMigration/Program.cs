using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod;

static class Program
{
    // Explicit legacy save fixtures, not registered model aliases.
    private const string OldEntry = "S_G_C_HOLY_DRAGON_ROAR";
    private const string NewEntry = "S_G_C_SAINT_DRAGON_ROAR";
    private static readonly (string Table, string Old, string New)[] Keys =
    [
        ("cards", OldEntry + ".title", NewEntry + ".title"),
        ("cards", OldEntry + ".description", NewEntry + ".description"),
        ("events", "S_G_E_GETTER_MANDALA.pages.INITIAL.options.HOLY_DRAGON.title", "S_G_E_GETTER_MANDALA.pages.INITIAL.options.SAINT_DRAGON.title"),
        ("events", "S_G_E_GETTER_MANDALA.pages.INITIAL.options.HOLY_DRAGON.description", "S_G_E_GETTER_MANDALA.pages.INITIAL.options.SAINT_DRAGON.description"),
        ("events", "S_G_E_GETTER_MANDALA.pages.HOLY_DRAGON.description", "S_G_E_GETTER_MANDALA.pages.SAINT_DRAGON.description"),
    ];
    private static int _assertions;

    public static void Main()
    {
        // Warm native readers before patching: startup may have read vanilla saves already.
        _ = ModelId.Deserialize("CARD." + OldEntry);
        _ = Read<SerializableCard>("{\"id\":\"CARD." + OldEntry + "\"}");
        _ = Read<LocString>(JsonSerializer.Serialize(new { table = "events", key = Keys[2].Old }));
        Assembly mod = typeof(Entry).Assembly;
        var harmony = new Harmony("ShinGetterMod.Tests.SaintDragonSaveMigration");
        foreach (Type type in mod.GetTypes().Where(t => t.Name.StartsWith("ShinGetterSaintDragon", StringComparison.Ordinal)
                     && t.GetCustomAttribute<HarmonyPatch>() != null))
            harmony.CreateClassProcessor(type).Patch();
        Run(mod);
        Console.WriteLine($"Saint Dragon save migration: PASS ({_assertions} assertions; real 109 assemblies; no game process)");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(Assembly mod)
    {
        Require(ModelId.Deserialize("CARD." + OldEntry).Entry == NewEntry, "legacy string ID was not migrated");
        Require(new ModelId("CARD", OldEntry).Entry == NewEntry, "constructor ID was not migrated");
        Require(ModelId.Deserialize("RELIC." + OldEntry).Entry == OldEntry, "foreign category changed");
        Require(ModelId.Deserialize("CARD.HOLY_WATER").Entry == "HOLY_WATER", "unrelated model changed");
        Require(ModelId.Deserialize("CARD." + OldEntry + "_OTHER").Entry == OldEntry + "_OTHER", "prefix-only match changed");
        Require(ModelId.Deserialize("CARD." + NewEntry).Entry == NewEntry, "new ID changed");

        // Exercise the game's generated object constructor as well as its normal string converter.
        var objectOptions = new JsonSerializerOptions(JsonSerializationUtility.Options);
        for (int i = objectOptions.Converters.Count - 1; i >= 0; i--)
            if (objectOptions.Converters[i] is ModelIdRunSaveConverter) objectOptions.Converters.RemoveAt(i);
        var objectId = JsonSerializer.Deserialize<ModelId>("{\"Category\":\"CARD\",\"Entry\":\"" + OldEntry + "\"}", objectOptions)!;
        Require(objectId.Entry == NewEntry, "generated object ID was not migrated");

        string cardJson = "{\"id\":\"CARD." + OldEntry + "\",\"current_upgrade_level\":1,\"floor_added_to_deck\":9,"
            + "\"enchantment\":{\"id\":\"ENCHANTMENT.CORRUPTED\",\"amount\":1}}";
        SerializableCard saved = Read<SerializableCard>(cardJson);
        Require(saved.Id!.Entry == NewEntry, "native card JSON ID");
        Type cardType = mod.GetType("ShinGetterMod.Models.Cards.SGC_SaintDragonRoar")
            ?? throw new InvalidOperationException("new card class missing");
        Require(mod.GetTypes().Count(t => t.Name == "SGC_SaintDragonRoar") == 1, "duplicate new model");
        Require(mod.GetType("ShinGetterMod.Models.Cards.SGC_HolyDragonRoar") == null, "legacy model still registered");
        ModelDb.Inject(cardType);
        ModelDb.Inject(typeof(Corrupted));
        var unupgraded = CardModel.FromSerializable(Read<SerializableCard>("{\"id\":\"CARD." + OldEntry + "\"}"));
        Require(unupgraded.CurrentUpgradeLevel == 0 && unupgraded.Enchantment == null, "plain card state changed");
        Require(unupgraded.DynamicVars.Damage.BaseValue == 15 && unupgraded.DynamicVars["BurnDamage"].BaseValue == 5, "base values changed");
        Require(unupgraded.Type == CardType.Attack && unupgraded.Rarity == CardRarity.Ancient
            && unupgraded.TargetType == TargetType.AllEnemies && unupgraded.Keywords.Contains(CardKeyword.Exhaust), "card rules changed");
        CardModel restored = CardModel.FromSerializable(saved);
        Require(restored.GetType() == cardType, "card restored as wrong/deprecated model");
        Require(restored.CurrentUpgradeLevel == 1 && restored.FloorAddedToDeck == 9, "upgrade/floor lost");
        Require(restored.Enchantment is Corrupted && restored.Enchantment.Amount == 1, "enchantment lost");
        Require(restored.DynamicVars.Damage.BaseValue == 20 && restored.DynamicVars["BurnDamage"].BaseValue == 8, "upgraded values changed");
        string savedAgain = Write(restored.ToSerializable());
        Require(!savedAgain.Contains(OldEntry) && savedAgain.Contains(NewEntry), "card writes legacy ID");
        Require(Write(CardModel.FromSerializable(Read<SerializableCard>(savedAgain)).ToSerializable()) == savedAgain, "card round trip not idempotent");

        var progress = Read<SerializableProgress>("{\"unique_id\":\"fixture\",\"discovered_cards\":[\"CARD." + OldEntry + "\"]}");
        Require(progress.DiscoveredCards.Single().Entry == NewEntry, "discovery progress lost");
        Require(!Write(progress).Contains(OldEntry), "progress writes legacy ID");
        var history = Read<RunHistoryPlayer>("{\"id\":42,\"deck\":[" + cardJson + "]}");
        Require(history.Id == 42 && history.Deck.Single().Id!.Entry == NewEntry, "history deck lost");
        Require(!Write(history).Contains(OldEntry), "history writes legacy ID");

        var props = Read<SavedProperties>("{\"ints\":[{\"name\":\"counter\",\"value\":7}],"
            + "\"model_ids\":[{\"name\":\"card\",\"value\":\"CARD." + OldEntry + "\"}],"
            + "\"cards\":[{\"name\":\"stored\",\"value\":" + cardJson + "}]}" );
        Require(props.ints!.Single().value == 7 && props.modelIds!.Single().value.Entry == NewEntry, "saved property data lost");
        Require(props.cards!.Single().value.CurrentUpgradeLevel == 1, "nested saved card lost");
        Require(!Write(props).Contains(OldEntry), "nested properties write legacy ID");

        foreach (var key in Keys)
        {
            var loc = Read<LocString>(JsonSerializer.Serialize(new { table = key.Table, key = key.Old }));
            Require(loc.LocTable == key.Table && loc.LocEntryKey == key.New, "legacy localization key not migrated");
            Require(Read<LocString>(Write(loc)).LocEntryKey == key.New, "localization round trip not idempotent");
            Require(new LocString("foreign", key.Old).LocEntryKey == key.Old, "foreign localization changed");
            Require(new LocString(key.Table, key.Old + "_OTHER").LocEntryKey == key.Old + "_OTHER", "unrelated localization changed");
        }
        var choice = Read<EventOptionHistoryEntry>(JsonSerializer.Serialize(new
        {
            title = new { table = "events", key = Keys[2].Old },
        }));
        Require(choice.Title.LocEntryKey == Keys[2].New, "event history choice lost");
        Require(!Write(choice).Contains("HOLY_DRAGON"), "event history writes legacy key");
        var packet = new PacketWriter();
        packet.WriteString("events");
        packet.WriteString(Keys[2].Old);
        packet.WriteBool(false);
        var reader = new PacketReader();
        reader.Reset(packet.Buffer);
        var packetChoice = new EventOptionHistoryEntry();
        packetChoice.Deserialize(reader);
        Require(packetChoice.Title.LocEntryKey == Keys[2].New, "native event history packet key not migrated");
        Require(reader.BitPosition == packet.BitPosition, "native event history packet length changed");
    }

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonSerializationUtility.Options)!;
    private static string Write<T>(T value) => JsonSerializer.Serialize(value, JsonSerializationUtility.Options);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _assertions++;
    }
}
