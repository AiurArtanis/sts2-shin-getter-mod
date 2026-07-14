using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CombatRoom), "StartCombat", new[] { typeof(IRunState) })]
internal static class ShinGetterCombatStartVoicePatch
{
    private static readonly ConditionalWeakTable<CombatState, CombatVoiceResetState> CombatVoiceResetStates = new();

    private static void Prefix(CombatRoom __instance, IRunState runState)
    {
        if (runState is null)
            return;

        foreach (Player player in runState.Players)
        {
            if (player.Character is not ShinGetter)
                continue;

            CombatState combatState = __instance.CombatState;
            CombatVoiceResetState resetState = CombatVoiceResetStates.GetOrCreateValue(combatState);
            if (combatState.RoundNumber == 1 && !resetState.HasResetVoiceHistory)
            {
                resetState.HasResetVoiceHistory = true;
                ShinGetterVoiceService.ResetCombatVoiceHistory(player);
            }

            ShinGetterVoiceService.PlayCombatStart(player);
            break;
        }
    }

    private sealed class CombatVoiceResetState
    {
        public bool HasResetVoiceHistory;
    }
}
