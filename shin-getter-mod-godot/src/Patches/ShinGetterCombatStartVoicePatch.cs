using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

        CombatState combatState = __instance.CombatState;
        CombatVoiceResetState resetState = CombatVoiceResetStates.GetOrCreateValue(combatState);
        if (resetState.HasPreparedVoiceState)
            return;

        foreach (Player player in runState.Players)
        {
            if (player.Character is not ShinGetter)
                continue;

            resetState.HasPreparedVoiceState = true;
            ShinGetterVoiceService.PrepareCombatStart(player, __instance);
            break;
        }
    }

    private sealed class CombatVoiceResetState
    {
        public bool HasPreparedVoiceState;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Add), new[] { typeof(Creature) })]
internal static class ShinGetterEnemySummonVoicePatch
{
    private static void Prefix(Creature creature)
    {
        ShinGetterVoiceService.OnEnemySummoned(creature);
    }
}
