#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyHpLost))]
internal static class ShinGetterActivatedSapphirePatch
{
    // Unlike a relic hook, this observes the result after *all* other HP-loss listeners.
    private static void Postfix(ICombatState? combatState, Creature target, HpLossHookPhase phases, ref decimal __result)
    {
        if (combatState == null || !phases.HasFlag(HpLossHookPhase.AfterOsty)) return;
        SGR_ActivatedSapphire? sapphire = target.Player?.GetRelic<SGR_ActivatedSapphire>();
        if (sapphire != null) __result = sapphire.InterceptFinalHpLoss(target, __result);
    }
}
