#nullable enable
using System;
using System.Linq;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Nodes.Events;
using ShinGetterMod.Services;

namespace ShinGetterMod.Diagnostics;

/// <summary>sgd = Shin Getter dialogue, following the existing sgs voice-test format.</summary>
public sealed class ShinGetterDialogueConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "sgd";
    public override string Args => "<NPC|ALL> <status|clear|unlock> [...]";
    public override string Description => "Reads/resets Shin Getter dialogue progress or forces the next eligible conversation. Changes the active profile!";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (!ShinGetterBondConsolePlan.TryParse(args, out var plan, out string error)) return new(false, error);
        if (!plan.IsReadOnly)
        {
            if (NShinGetterBondDialogue.IsOpen)
                return new(false, "Finish or skip the open conversation before editing its progress.");
            if (TestMode.IsOn || NonInteractiveMode.IsActive)
                return new(false, "Progress edits are unavailable in replay/non-interactive modes.");
            if (RunManager.Instance.IsInProgress)
            {
                var state = RunManager.Instance.DebugOnlyGetState();
                if (state == null || state.Players.Count != 1 || state.GameMode != GameMode.Standard || state.Modifiers.Count != 0
                    || RunManager.Instance.DailyTime.HasValue || !RunManager.Instance.ShouldSave
                    || issuingPlayer?.Character is not ShinGetter)
                    return new(false, "Use a saved standard solo Shin Getter run (or the main menu). Multiplayer/daily/custom runs cannot edit solo progress.");
            }
        }
        bool success = ShinGetterBondSession.TryConsole(plan, out string message);
        return new(success, message);
    }

    public override CompletionResult GetArgumentCompletions(Player? player, string[] args)
    {
        string[] candidates = args.Length switch
        {
            <= 1 => ShinGetterBondConsolePlan.Npcs.Concat(new[] { "ALL" }).ToArray(),
            2 => ShinGetterBondConsolePlan.Operations,
            3 when args[1].Equals("unlock", StringComparison.OrdinalIgnoreCase) || args[1] == "解锁" => ShinGetterBondConsolePlan.UnlockKinds,
            4 when ShinGetterBondConsolePlan.Canonical(args[1]) == "UNLOCK"
                => ShinGetterBondConsolePlan.NumberCompletions(args[0], args[2]),
            _ => Array.Empty<string>(),
        };
        return CompleteArgument(candidates, args.Take(Math.Max(0, args.Length - 1)).ToArray(), args.LastOrDefault() ?? "");
    }
}
