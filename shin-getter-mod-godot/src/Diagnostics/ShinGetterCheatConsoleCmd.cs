#nullable enable
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using ShinGetterMod.Services;

namespace ShinGetterMod.Diagnostics;

public sealed class ShinGetterCheatConsoleCmd : AbstractConsoleCmd
{
    internal const string CommandName = "shin_getter_cheat";
    public override string CmdName => CommandName;
    public override string Args => "";
    public override string Description => "Defy the laws of physics. Multiplayer: once for the entire team per run.";
    public override bool IsNetworked => true;
    public override bool DebugOnly => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args) =>
        args.Length == 0 ? ShinGetterCheatService.TryActivate(issuingPlayer)
            : new(false, "This command takes no arguments.");
}
