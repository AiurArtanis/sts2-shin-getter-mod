#nullable enable
using System.Globalization;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using ShinGetterMod.Services;

namespace ShinGetterMod.Diagnostics;

public sealed class ShinGetterStonerSunshineRateConsoleCmd : AbstractConsoleCmd
{
    public override string CmdName => "stoner_sunshine_rate";
    public override string Args => string.Empty;
    public override string Description => "Shows the current Stoner Sunshine special-arrival probability.";
    public override bool IsNetworked => false;

    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (args.Length != 0)
            return new(false, "Usage: stoner_sunshine_rate");

        if (!ShinGetterStonerSunshineService.TryGetCurrentAppearanceChance(
                issuingPlayer,
                out decimal chance,
                out string error))
        {
            return new(false, error);
        }

        return new(
            true,
            $"Current Stoner Sunshine appearance rate: {chance.ToString("P2", CultureInfo.InvariantCulture)}");
    }
}
