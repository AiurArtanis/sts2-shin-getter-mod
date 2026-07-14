using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CombatRoom), "StartCombat", new[] { typeof(IRunState) })]
internal static class ShinGetterCombatStartVoicePatch
{
    private static void Prefix(IRunState runState)
    {
        if (runState is null)
            return;

        foreach (Player player in runState.Players)
        {
            if (player.Character is not ShinGetter)
                continue;

            ShinGetterVoiceService.PlayCombatStart(player);
            break;
        }
    }
}
