#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.CardPools;

namespace ShinGetterMod.Diagnostics;

public sealed class ShinGetterAddAllCardsConsoleCmd : AbstractConsoleCmd
{
    private const string Usage = "shin_getter_add_cards \"-\" 0";
    private const string AllCharactersFilter = "-";
    private const string ShinGetterCharacterFilter = "SHIN_GETTER";

    public override string CmdName => "shin_getter_add_cards";
    public override string Args => "\"character\" <upgraded:0|1>";
    public override string Description => "Adds all Shin Getter mod cards to the current player's deck.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer == null)
            return new(false, "No issuing player is available.");

        if (!RunManager.Instance.IsInProgress)
            return new(false, "A run is currently not in progress!");

        if (!TryParseRequest(args, out var characterFilter, out var shouldUpgrade, out var error))
            return new(false, error);

        var canonicalCards = SelectCards(characterFilter).ToList();
        if (canonicalCards.Count == 0)
            return new(false, $"No Shin Getter cards matched character filter '{characterFilter}'.");

        Task task = AddCardsToDeck(issuingPlayer, canonicalCards, shouldUpgrade);
        var upgradeText = shouldUpgrade ? "upgraded " : string.Empty;
        return new(
            task,
            true,
            $"Adding {canonicalCards.Count} {upgradeText}Shin Getter card(s) to {issuingPlayer.Character.Id.Entry}'s deck.");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(
                new[] { "\"-\"", "\"SHIN_GETTER\"" },
                Array.Empty<string>(),
                args.Length == 0 ? string.Empty : args[0]);
        }

        if (args.Length == 2)
            return CompleteArgument(new[] { "0", "1" }, new[] { args[0] }, args[1]);

        return base.GetArgumentCompletions(player, args);
    }

    private static IEnumerable<CardModel> SelectCards(string characterFilter)
    {
        if (characterFilter != AllCharactersFilter &&
            !characterFilter.Equals(ShinGetterCharacterFilter, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<CardModel>();
        }

        return ModelDb.CardPool<ShinGetterCardPool>().AllCards;
    }

    private static async Task AddCardsToDeck(
        Player issuingPlayer,
        IReadOnlyList<CardModel> canonicalCards,
        bool shouldUpgrade)
    {
        var cards = new List<CardModel>(canonicalCards.Count);
        foreach (CardModel canonicalCard in canonicalCards)
        {
            CardModel card = issuingPlayer.RunState.CreateCard(canonicalCard, issuingPlayer);
            if (shouldUpgrade)
            {
                while (card.IsUpgradable)
                    CardCmd.Upgrade(card, CardPreviewStyle.None);
            }

            cards.Add(card);
        }

        await CardPileCmd.Add(cards, PileType.Deck, CardPilePosition.Bottom, clonedBy: null, skipVisuals: true);
    }

    private static bool TryParseRequest(
        string[] args,
        out string characterFilter,
        out bool shouldUpgrade,
        out string error)
    {
        characterFilter = AllCharactersFilter;
        shouldUpgrade = false;
        error = string.Empty;

        if (!TryParseQuotedCommandArgs(args, out var tokens, out error))
            return false;

        if (tokens.Count > 2)
        {
            error = "Usage: " + Usage;
            return false;
        }

        if (tokens.Count >= 1)
        {
            if (!tokens[0].WasQuoted)
            {
                error = "The character argument must be wrapped in English double quotes. Usage: " + Usage;
                return false;
            }

            characterFilter = NormalizeCharacterFilter(tokens[0].Value);
        }

        if (tokens.Count == 2)
        {
            if (!int.TryParse(tokens[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var upgraded) ||
                upgraded is not (0 or 1))
            {
                error = $"Invalid upgraded value '{tokens[1].Value}'. Use 0 or 1.";
                return false;
            }

            shouldUpgrade = upgraded == 1;
        }

        return true;
    }

    private static string NormalizeCharacterFilter(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrEmpty(trimmed)
            ? AllCharactersFilter
            : trimmed.ToUpperInvariant();
    }

    private static bool TryParseQuotedCommandArgs(
        string[] rawArgs,
        out List<ConsoleToken> tokens,
        out string error)
    {
        tokens = new();
        error = string.Empty;

        var text = string.Join(" ", rawArgs);
        var index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            if (index >= text.Length)
                break;

            if (text[index] == '"')
            {
                index++;
                var quoted = new StringBuilder();
                while (index < text.Length && text[index] != '"')
                {
                    quoted.Append(text[index]);
                    index++;
                }

                if (index >= text.Length)
                {
                    error = "Unclosed double quote in shin_getter_add_cards arguments.";
                    return false;
                }

                index++;
                if (index < text.Length && !char.IsWhiteSpace(text[index]))
                {
                    error = "Unexpected text after a quoted argument. Add a space between arguments.";
                    return false;
                }

                tokens.Add(new(quoted.ToString(), wasQuoted: true));
                continue;
            }

            var start = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
                index++;
            tokens.Add(new(text[start..index], wasQuoted: false));
        }

        return true;
    }

    private readonly struct ConsoleToken
    {
        public ConsoleToken(string value, bool wasQuoted)
        {
            Value = value;
            WasQuoted = wasQuoted;
        }

        public string Value { get; }
        public bool WasQuoted { get; }
    }
}
