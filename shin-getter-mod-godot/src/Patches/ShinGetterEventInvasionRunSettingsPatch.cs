using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Config;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(RunState), nameof(RunState.CreateForNewRun))]
internal static class ShinGetterEventInvasionRunSettingsPatch
{
    private static void Postfix(RunState __result)
    {
        bool enabled = ShinGetterChunibyoConfigService.Current.EventInvasionEnabled;

        foreach (var player in __result.Players)
        {
            if (player.Character is not ShinGetter)
                continue;

            if (player.GetRelic<SGR_GetterFurnace>() is { } furnace)
                furnace.EventInvasionEnabled = enabled;

            if (player.GetRelic<SGR_EmperorsFragment>() is { } fragment)
                fragment.EventInvasionEnabled = enabled;
        }
    }
}
