#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Models.Events;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(EventConsoleCmd), nameof(EventConsoleCmd.Process))]
internal static class EventConsoleAliasPatch
{
    private const string EventClassPrefix = "SGE_";
    private const string EventModelPrefix = "s_g_e_";

    private static readonly IReadOnlyDictionary<string, string> SpecialAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["s_g_c_getter_mandala"] = "S_G_E_GETTER_MANDALA",
            ["s_g_e_getter_mandala"] = "S_G_E_GETTER_MANDALA",
            ["SGE_GetterMandala"] = "S_G_E_GETTER_MANDALA",
        };

    private static bool Prefix(Player? issuingPlayer, string[] args, ref CmdResult __result)
    {
        if (args.Length == 0)
            return true;

        if (SpecialAliases.TryGetValue(args[0], out string? modelEntry) && modelEntry != null)
        {
            args[0] = modelEntry;

            if (!RunManager.Instance.IsInProgress)
            {
                __result = new CmdResult(success: false, "A run is currently not in progress!");
                return false;
            }

            if (issuingPlayer == null)
            {
                __result = new CmdResult(success: false, "No issuing player is available.");
                return false;
            }

            EventModel eventModel = ModelDb.Event<SGE_GetterMandala>();
            issuingPlayer.RunState.AppendToMapPointHistory(
                MapPointType.Unknown,
                RoomType.Event,
                eventModel.Id);
            var task = RunManager.Instance.EnterRoom(new EventRoom(eventModel));
            __result = new CmdResult(task, success: true, $"Jumped to event: '{eventModel.Id.Entry}'");
            return false;
        }

        if (args[0].StartsWith(EventModelPrefix, StringComparison.OrdinalIgnoreCase))
        {
            args[0] = args[0].ToUpperInvariant();
            return true;
        }

        if (args[0].StartsWith(EventClassPrefix, StringComparison.OrdinalIgnoreCase))
            args[0] = ToModelEntry(args[0][EventClassPrefix.Length..]);

        return true;
    }

    private static string ToModelEntry(string classNameSuffix)
    {
        StringBuilder entry = new("S_G_E_");
        for (int i = 0; i < classNameSuffix.Length; i++)
        {
            char current = classNameSuffix[i];
            if (current == '_')
            {
                if (entry[^1] != '_')
                    entry.Append('_');
                continue;
            }

            if (i > 0 && char.IsUpper(current) && char.IsLower(classNameSuffix[i - 1]))
                entry.Append('_');

            entry.Append(char.ToUpperInvariant(current));
        }

        return entry.ToString();
    }
}
