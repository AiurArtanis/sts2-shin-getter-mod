#nullable enable
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(PotionFactory), "CreateRandomPotion")]
internal static class ShinGetterPotionFactoryWeightPatch
{
    private static bool Prefix(
        IEnumerable<PotionModel> options,
        int count,
        Rng rng,
        ref List<PotionModel> __result)
    {
        var available = options.ToList();
        var selected = new List<PotionModel>();

        for (int i = 0; i < count; i++)
        {
            float roll = rng.NextFloat();
            PotionRarity rarity = roll <= 0.1f
                ? PotionRarity.Rare
                : roll <= 0.35f
                    ? PotionRarity.Uncommon
                    : PotionRarity.Common;

            PotionModel? item = rng.NextItem(available.Where(potion => potion.Rarity == rarity))
                ?? rng.NextItem(available);
            if (item == null)
                break;

            selected.Add(item);
            available.RemoveAll(potion => potion.Id == item.Id);
        }

        __result = selected;
        return false;
    }
}
