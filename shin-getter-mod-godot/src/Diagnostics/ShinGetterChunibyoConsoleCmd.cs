#nullable enable
using System;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using ShinGetterMod.Audio;
using ShinGetterMod.Config;

namespace ShinGetterMod.Diagnostics;

public sealed class ShinGetterChunibyoConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "chunibyo";
    public override string Args => "<on|off>";
    public override string Description => "Shows or hides the Shin Getter Chunibyo Config main-menu entry after restart.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 1 ||
            (!args[0].Equals("on", StringComparison.OrdinalIgnoreCase) &&
             !args[0].Equals("off", StringComparison.OrdinalIgnoreCase)))
        {
            return new(false, "Usage: chunibyo <on|off>");
        }

        bool enabled = args[0].Equals("on", StringComparison.OrdinalIgnoreCase);
        ShinGetterChunibyoConfigService.Load();
        ShinGetterChunibyoConfigService.Current.ShowInMainMenu = enabled;
        if (!ShinGetterChunibyoConfigService.Save(out string error))
            return new(false, $"Could not save Shin Getter config: {error}");

        return new(true, enabled
            ? "Chunibyo Config will appear on the main menu after restart."
            : "Chunibyo Config will be hidden from the main menu after restart. Use 'chunibyo on' to restore it.");
    }
}

public sealed class ShinGetterSoundConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "sgs";
    public override string Args => "<001-047|049-050|058-065>";
    public override string Description => "Plays a Shin Getter voice or transformation sound by workbook code.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 1)
            return new(false, "Usage: sgs <001-047|049-050|058-065>");

        bool success = ShinGetterVoiceService.TryPlayCode(issuingPlayer, args[0], out string message);
        return new(success, message);
    }
}
