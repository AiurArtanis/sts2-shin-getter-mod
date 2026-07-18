#nullable enable
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardModel), "get_ShouldRetainThisTurn")]
internal static class ShinGetterSpiritCommandRetainPatch
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (__result
            || __instance is not ShinGetterCardBase { SpiritRequirement: > 0 } spiritCommand
            || spiritCommand.CombatState == null)
        {
            return;
        }

        __result = spiritCommand.Owner.Creature.GetPower<SGP_Ki>()?.Amount > 0;
    }
}
