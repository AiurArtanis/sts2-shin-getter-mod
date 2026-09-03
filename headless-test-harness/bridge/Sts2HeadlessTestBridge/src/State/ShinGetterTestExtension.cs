using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;

namespace Sts2HeadlessTestBridge.State;

/// <summary>
/// Optional, reflection-only projection of ShinGetterMod state. The bridge has
/// no compile-time reference to the production mod and therefore cannot enter
/// its dependency graph or production package.
/// </summary>
public static class ShinGetterTestExtension
{
    public static object? Capture(IReadOnlyList<Player> players)
    {
        Player[] getterPlayers = players
            .Where(IsGetterPlayer)
            .ToArray();
        if (getterPlayers.Length == 0)
            return null;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schema"] = "shin-getter-test/v1",
            ["dependency"] = "reflection-only; no production reference",
            ["players"] = getterPlayers.Select(CapturePlayer).ToArray(),
            ["comparison_classes"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["form"] = "hard",
                ["powers"] = "hard",
                ["saved_properties"] = "hard",
                ["stoner_sunshine"] = "eventual",
                ["animation"] = "presentation",
                ["voice"] = "presentation",
            },
            ["presentation"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["animation"] = "unavailable in H0 snapshot",
                ["voice"] = "unavailable in H0 snapshot",
            },
        };
    }

    private static bool IsGetterPlayer(Player player) =>
        player.Character.Id.ToString().Contains("SHIN_GETTER", StringComparison.OrdinalIgnoreCase)
        || player.Character.GetType().Assembly.GetName().Name?.Contains("ShinGetter", StringComparison.OrdinalIgnoreCase) == true;

    private static object CapturePlayer(Player player)
    {
        var powers = player.Creature.Powers
            .Where(IsGetterRelevant)
            .OrderBy(power => power.Id.ToString(), StringComparer.Ordinal)
            .Select(power => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["model_id"] = power.Id.ToString(),
                ["type"] = power.GetType().FullName,
                ["amount"] = power.Amount,
                ["saved_properties"] = SavedProperties(power),
            })
            .ToArray();

        string[] powerNames = player.Creature.Powers
            .Select(power => power.GetType().Name)
            .ToArray();
        string form = powerNames.Contains("SGP_ShinGetterTwo", StringComparer.Ordinal)
            ? "GETTER_TWO"
            : powerNames.Contains("SGP_ShinGetterThree", StringComparer.Ordinal)
                ? "GETTER_THREE"
                : powerNames.Contains("SGP_ShinGetterOne", StringComparer.Ordinal)
                    ? "GETTER_ONE"
                    : powerNames.Contains("SGP_ShinForm", StringComparer.Ordinal)
                        ? "SHIN"
                        : "NONE";

        int Amount(string typeName) => player.Creature.Powers
            .Where(power => StringComparer.Ordinal.Equals(power.GetType().Name, typeName))
            .Select(power => power.Amount)
            .FirstOrDefault();

        object[] modelState = player.Deck.Cards.Cast<object>()
            .Concat(player.Relics)
            .Where(item => item.GetType().Assembly.GetName().Name?.Contains("ShinGetter", StringComparison.OrdinalIgnoreCase) == true)
            .Select(item => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = item.GetType().FullName,
                ["model_id"] = item.GetType().GetProperty("Id")?.GetValue(item)?.ToString(),
                ["saved_properties"] = SavedProperties(item),
            })
            .Where(item => ((Dictionary<string, object?>)item["saved_properties"]!).Count > 0)
            .Cast<object>()
            .ToArray();

        bool stonerInDeck = player.Deck.Cards.Any(card => StringComparer.Ordinal.Equals(card.GetType().Name, "SGC_StonerSunshine"));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["character_id"] = player.Character.Id.ToString(),
            ["form"] = form,
            ["form_powers"] = powerNames.Where(name => name.StartsWith("SGP_Shin", StringComparison.Ordinal)).Order().ToArray(),
            ["spirit"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ki"] = Amount("SGP_Ki"),
                ["super_ki"] = Amount("SGP_SuperKi"),
                ["fighting_spirit"] = Amount("SGP_FightingSpirit"),
            },
            ["vigor"] = Amount("VigorPower"),
            ["evolution"] = Amount("SGP_Evolution"),
            ["chain_reaction"] = Amount("SGP_ChainReaction"),
            ["powers"] = powers,
            ["saved_properties"] = modelState,
            ["stoner_sunshine"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["card_in_deck"] = stonerInDeck,
                ["appearance_chance"] = null,
                ["chance_reason"] = "production service exposes no non-mutating progress accessor",
                ["consumed_state"] = stonerInDeck ? "already_owned" : "unknown",
            },
        };
    }

    private static bool IsGetterRelevant(object model)
    {
        string type = model.GetType().Name;
        return type.StartsWith("SGP_", StringComparison.Ordinal)
            || type is "VigorPower" or "StrengthPower" or "DexterityPower" or "PlatingPower" or "RegenerationPower";
    }

    private static Dictionary<string, object?> SavedProperties(object model)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in model.GetType().GetFields(flags).OrderBy(field => field.Name, StringComparer.Ordinal))
        {
            if (!HasSavedPropertyAttribute(field))
                continue;
            TryAdd(result, field.Name, () => field.GetValue(model));
        }
        foreach (PropertyInfo property in model.GetType().GetProperties(flags).OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            if (property.GetIndexParameters().Length != 0 || !HasSavedPropertyAttribute(property))
                continue;
            TryAdd(result, property.Name, () => property.GetValue(model));
        }
        return result;
    }

    private static bool HasSavedPropertyAttribute(MemberInfo member) => member.CustomAttributes.Any(attribute =>
        attribute.AttributeType.Name.Contains("SavedProperty", StringComparison.Ordinal));

    private static void TryAdd(Dictionary<string, object?> target, string name, Func<object?> getter)
    {
        try
        {
            object? value = getter();
            if (value is null || value is string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
                target[name] = value;
            else if (value.GetType().IsEnum || value is Guid)
                target[name] = value.ToString();
        }
        catch
        {
            target[name] = "<unavailable>";
        }
    }
}
