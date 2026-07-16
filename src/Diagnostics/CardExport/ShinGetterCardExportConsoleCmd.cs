#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;

namespace ShinGetterMod.Diagnostics.CardExport;

public sealed class ShinGetterCardExportConsoleCmd : AbstractConsoleCmd
{
    private const string Usage =
        "export_cards \"SHIN_GETTER\" \"-\" \"-\" true 1 0 -";

    public override string CmdName => "export_cards";
    public override string Args =>
        "\"character\" \"outputDir\" \"idFilter\" <includeUpgrades:true|false> <scale> <maxBaseCards> <nameFormat:-|zhs|jpn|eng>";
    public override string Description => "Exports card PNG files for a character or non-character card pool.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!ShinGetterCardPngExporter.TryValidateExportEnvironment(out var environmentError))
            return new(false, environmentError);

        if (!TryParseRequest(args, out var request, out var error))
            return new(false, error);

        var selectedCards = ShinGetterCardPngExporter.SelectCards(request);
        var baseCardsToExport = request.MaxBaseCards > 0
            ? Math.Min(selectedCards.Count, request.MaxBaseCards)
            : selectedCards.Count;
        if (baseCardsToExport == 0)
            return new(false, "No cards matched the requested character and idFilter.");

        ShinGetterCardPngExporter.BeginExport(request, message => Log.Info($"[ShinGetterCardExport] {message}"));

        var upgradeMode = request.IncludeUpgradedVariants ? "base and upgraded variants" : "base variants only";
        return new(
            true,
            $"Started card PNG export: {baseCardsToExport} base card(s), {upgradeMode}. Output: '{request.OutputDirectory}'. Progress is written to the Godot log as [ShinGetterCardExport].");
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        if (args.Length <= 1)
        {
            return CompleteArgument(
                new[] { "\"SHIN_GETTER\"", "\"IRONCLAD\"", "\"SILENT\"", "\"REGENT\"", "\"NECROBINDER\"", "\"DEFECT\"", "\"-\"" },
                Array.Empty<string>(),
                args.Length == 0 ? string.Empty : args[0]);
        }

        if (args.Length == 4)
        {
            return CompleteArgument(new[] { "true", "false" }, args.Take(args.Length - 1).ToArray(), args[^1]);
        }

        if (args.Length == 7)
        {
            return CompleteArgument(new[] { "-", "zhs", "jpn", "eng" }, args.Take(args.Length - 1).ToArray(), args[^1]);
        }

        return base.GetArgumentCompletions(player, args);
    }

    private static bool TryParseRequest(
        string[] args,
        out ShinGetterCardPngExportRequest request,
        out string error)
    {
        request = default;
        error = string.Empty;

        if (!TryParseQuotedCommandArgs(args, out var tokens, out error))
            return false;

        if (tokens.Count != 7)
        {
            error = "Usage: " + Usage;
            return false;
        }

        for (var i = 0; i < 3; i++)
        {
            if (!tokens[i].WasQuoted)
            {
                error = "The character, outputDir, and idFilter arguments must be wrapped in English double quotes. Usage: " + Usage;
                return false;
            }
        }

        if (!ShinGetterCardPngExporter.TryNormalizeCharacterFilter(tokens[0].Value, out var characterFilter, out error))
            return false;

        if (!ShinGetterCardPngExporter.TryNormalizeOutputDirectory(tokens[1].Value, out var outputDirectory, out error))
            return false;

        var idFilter = tokens[2].Value.Trim();
        if (string.IsNullOrWhiteSpace(idFilter))
        {
            error = "idFilter cannot be empty. Use \"-\" when no filter is needed.";
            return false;
        }

        if (!bool.TryParse(tokens[3].Value, out var includeUpgrades))
        {
            error = $"Invalid includeUpgrades value '{tokens[3].Value}'. Use true or false.";
            return false;
        }

        if (!float.TryParse(tokens[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) ||
            scale <= 0f)
        {
            error = $"Invalid scale value '{tokens[4].Value}'. Use a positive number such as 1.";
            return false;
        }

        if (!int.TryParse(tokens[5].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxBaseCards) ||
            maxBaseCards < 0)
        {
            error = $"Invalid maxBaseCards value '{tokens[5].Value}'. Use 0 or a positive integer.";
            return false;
        }

        if (!TryParseNameFormat(tokens[6].Value, out var nameFormat))
        {
            error = $"Invalid nameFormat value '{tokens[6].Value}'. Use -, zhs, jpn, or eng.";
            return false;
        }

        request = new()
        {
            CharacterFilter = characterFilter,
            OutputDirectory = outputDirectory,
            IdFilterPattern = idFilter == "-" ? null : idFilter,
            IncludeUpgradedVariants = includeUpgrades,
            IncludeCardsHiddenFromLibrary = false,
            Scale = scale,
            MaxBaseCards = maxBaseCards,
            NameFormat = nameFormat,
        };
        return true;
    }

    private static bool TryParseNameFormat(
        string raw,
        out ShinGetterCardExportNameFormat nameFormat)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "-":
                nameFormat = ShinGetterCardExportNameFormat.Default;
                return true;
            case "zhs":
                nameFormat = ShinGetterCardExportNameFormat.Zhs;
                return true;
            case "jpn":
                nameFormat = ShinGetterCardExportNameFormat.Jpn;
                return true;
            case "eng":
                nameFormat = ShinGetterCardExportNameFormat.Eng;
                return true;
            default:
                nameFormat = default;
                return false;
        }
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
                    error = "Unclosed double quote in export_cards arguments.";
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
