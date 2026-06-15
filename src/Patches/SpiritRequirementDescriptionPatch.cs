using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), new[] { typeof(PileType), typeof(Creature) })]
internal static class SpiritRequirementDescriptionPatch
{
    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance is ShinGetterCardBase card && card.SpiritRequirement > 0)
        {
            __result = $"[gold]【气力 {card.SpiritRequirement}】[/gold]\n{__result}";
        }
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForUpgradePreview))]
internal static class SpiritRequirementUpgradeDescriptionPatch
{
    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance is ShinGetterCardBase card && card.UpgradePreviewSpiritRequirement > 0)
        {
            __result = $"[gold]【气力 {card.UpgradePreviewSpiritRequirement}】[/gold]\n{__result}";
        }
    }
}
