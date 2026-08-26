using System.Reflection;
using System.Runtime.Loader;

string sourceRoot = Environment.GetEnvironmentVariable("SHIN_GETTER_STS2_111_SOURCE")
    ?? @"E:\Work\SlaytheSpare2-111-beta";
string betaBin = Path.GetFullPath(Path.Combine(sourceRoot, ".godot", "mono", "temp", "bin", "Debug"));
string gamePath = Path.Combine(betaBin, "sts2.dll");

if (!File.Exists(gamePath))
    throw new InvalidOperationException($"0.111 Beta sts2.dll was not found: {gamePath}");

AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string dependency = Path.Combine(betaBin, $"{name.Name}.dll");
    return File.Exists(dependency)
        ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency)
        : null;
};

Assembly game = AssemblyLoadContext.Default.LoadFromAssemblyPath(gamePath);

Type RequireType(string fullName) =>
    game.GetType(fullName, throwOnError: true, ignoreCase: false)
    ?? throw new InvalidOperationException($"Missing type {fullName}");

MethodInfo RequireMethod(Type type, string name, Func<MethodInfo, bool> predicate)
{
    MethodInfo[] candidates = type.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        .Where(method => method.Name == name && predicate(method))
        .ToArray();
    return candidates.Length == 1
        ? candidates[0]
        : throw new InvalidOperationException(
            $"Expected one {type.FullName}.{name} target, found {candidates.Length}");
}

FieldInfo RequireField(Type type, string name)
{
    for (Type? current = type; current != null; current = current.BaseType)
    {
        FieldInfo? field = current.GetField(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        if (field != null)
            return field;
    }

    throw new InvalidOperationException($"Missing field {type.FullName}.{name}");
}

bool ParameterIs(MethodInfo method, int index, string fullName) =>
    method.GetParameters()[index].ParameterType.FullName == fullName;

Type characterModel = RequireType("MegaCrit.Sts2.Core.Models.CharacterModel");
RequireMethod(
    characterModel,
    "GenerateAnimator",
    method => method.GetParameters().Length == 2
        && ParameterIs(method, 1, "MegaCrit.Sts2.Core.Entities.Creatures.Creature"));

Type abstractModel = RequireType("MegaCrit.Sts2.Core.Models.AbstractModel");
foreach (string name in new[] { "ModifyDamageAdditive", "ModifyDamageMultiplicative" })
{
    RequireMethod(
        abstractModel,
        name,
        method => method.GetParameters().Length == 6
            && ParameterIs(method, 5, "MegaCrit.Sts2.Core.Entities.Cards.CardPlay"));
}

Type attackCommand = RequireType("MegaCrit.Sts2.Core.Commands.Builders.AttackCommand");
RequireMethod(
    attackCommand,
    "FromCard",
    method => method.GetParameters().Length == 2
        && ParameterIs(method, 1, "MegaCrit.Sts2.Core.Entities.Cards.CardPlay"));

Type creatureCmd = RequireType("MegaCrit.Sts2.Core.Commands.CreatureCmd");
RequireMethod(
    creatureCmd,
    "LoseBlock",
    method => method.GetParameters().Length == 4
        && ParameterIs(method, 0, "MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext")
        && ParameterIs(method, 3, "MegaCrit.Sts2.Core.Entities.Creatures.Creature"));
RequireMethod(
    creatureCmd,
    "Damage",
    method => method.GetParameters().Length == 6
        && ParameterIs(method, 1, "MegaCrit.Sts2.Core.Entities.Creatures.Creature")
        && ParameterIs(method, 2, "System.Decimal")
        && ParameterIs(method, 3, "MegaCrit.Sts2.Core.ValueProps.ValueProp")
        && ParameterIs(method, 4, "MegaCrit.Sts2.Core.Models.CardModel")
        && ParameterIs(method, 5, "MegaCrit.Sts2.Core.Entities.Cards.CardPlay"));

Type choiceContext = RequireType("MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext");
RequireMethod(
    choiceContext,
    "SignalPlayerChoiceBegun",
    method => method.GetParameters().Length == 2
        && ParameterIs(method, 0, "MegaCrit.Sts2.Core.Entities.Players.Player"));

Type characterSelect = RequireType(
    "MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen");
RequireMethod(
    characterSelect,
    "PlayerChanged",
    method => method.GetParameters().Length == 2
        && ParameterIs(method, 0, "MegaCrit.Sts2.Core.Entities.Multiplayer.StartRunLobbyPlayer"));
if (game.GetType("MegaCrit.Sts2.Core.Entities.Multiplayer.LobbyPlayer", false) != null)
    throw new InvalidOperationException("Removed LobbyPlayer unexpectedly exists in the 0.111 Beta assembly");

Type hook = RequireType("MegaCrit.Sts2.Core.Hooks.Hook");
RequireMethod(
    hook,
    "ModifyDamage",
    method => method.GetParameters().Length == 11
        && ParameterIs(method, 7, "MegaCrit.Sts2.Core.Entities.Cards.CardPlay"));

Type potionFactory = RequireType("MegaCrit.Sts2.Core.Factories.PotionFactory");
MethodInfo randomPotions = RequireMethod(
    potionFactory,
    "CreateRandomPotions",
    method => method.IsPrivate && method.IsStatic && method.GetParameters().Length == 3);
if (!randomPotions.ReturnType.IsGenericType
    || randomPotions.ReturnType.GetGenericTypeDefinition() != typeof(IEnumerable<>))
{
    throw new InvalidOperationException(
        $"PotionFactory.CreateRandomPotions has unexpected return type {randomPotions.ReturnType}");
}
if (potionFactory.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
    .Any(method => method.Name == "CreateRandomPotion"))
{
    throw new InvalidOperationException("Removed PotionFactory.CreateRandomPotion unexpectedly exists");
}

RequireType("MegaCrit.Sts2.Core.Modding.ModInitializerAttribute");
Type modManager = RequireType("MegaCrit.Sts2.Core.Modding.ModManager");
RequireMethod(modManager, "CallModInitializer", method => method.IsPrivate);
RequireMethod(modManager, "TryLoadMod", method => method.IsPrivate);

RequireField(RequireType("MegaCrit.Sts2.Core.Models.PowerModel"), "_internalData");
Type vigorData = RequireType("MegaCrit.Sts2.Core.Models.Powers.VigorPower")
    .GetNestedType("Data", BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("Missing VigorPower.Data");
RequireField(vigorData, "commandToModify");
RequireField(vigorData, "amountWhenAttackStarted");

var reflectionFields = new (string TypeName, string[] Fields)[]
{
    ("MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardLibrary", new[] { "_poolFilters", "_cardPoolFilters" }),
    ("MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary.NCardLibraryGrid", new[] { "_allCards" }),
    ("MegaCrit.Sts2.Core.Nodes.Cards.NCard", new[] { "_frame" }),
    ("MegaCrit.Sts2.Core.Models.Events.TheArchitect", new[] { "_architectCreature", "_score", "_speechBubble" }),
    ("MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen.NGameOverScreen", new[] { "_localPlayer", "_history", "_deathQuote" }),
    ("MegaCrit.Sts2.Core.Nodes.Combat.NPowerContainer", new[] { "_powerNodes" }),
    ("MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectScreen", new[] { "_bgContainer" }),
    ("MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NMultiplayerLoadGameScreen", new[] { "_bgContainer" }),
};
foreach ((string typeName, string[] fields) in reflectionFields)
{
    Type type = RequireType(typeName);
    foreach (string field in fields)
        RequireField(type, field);
}

Console.WriteLine("issue#93 0.111 Beta runtime reflection/Harmony target probe passed");
