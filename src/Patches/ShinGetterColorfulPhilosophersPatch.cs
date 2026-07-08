#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Events;
using ShinGetterMod.Models.CardPools;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(ColorfulPhilosophers), "GenerateInitialOptions")]
internal static class ShinGetterColorfulPhilosophersPatch
{
    private static readonly MethodInfo OfferRewardsMethod =
        AccessTools.Method(typeof(ColorfulPhilosophers), "OfferRewards");

    private static bool Prefix(ColorfulPhilosophers __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (__instance.Owner == null)
            return true;

        List<EventOption> options = new();
        CardPoolModel characterPool = __instance.Owner.Character.CardPool;
        List<CardPoolModel> unlockedPools = __instance.Owner.UnlockState.CharacterCardPools.ToList();

        foreach (CardPoolModel cardPool in CardPoolColorOrder())
        {
            if (characterPool != cardPool && unlockedPools.Contains(cardPool))
            {
                CardPoolModel selectedPool = cardPool;
                options.Add(new EventOption(
                    __instance,
                    () => OfferRewards(__instance, selectedPool),
                    "COLORFUL_PHILOSOPHERS.pages.INITIAL.options." + selectedPool.EnergyColorName.ToUpperInvariant()));
            }
        }

        int optionCount = Mathf.Min(3, options.Count);
        while (options.Count > optionCount)
            options.RemoveAt(__instance.Rng.NextInt(options.Count));

        __result = options;
        return false;
    }

    private static IEnumerable<CardPoolModel> CardPoolColorOrder()
    {
        yield return ModelDb.CardPool<NecrobinderCardPool>();
        yield return ModelDb.CardPool<IroncladCardPool>();
        yield return ModelDb.CardPool<RegentCardPool>();
        yield return ModelDb.CardPool<SilentCardPool>();
        yield return ModelDb.CardPool<DefectCardPool>();
        yield return ModelDb.CardPool<ShinGetterCardPool>();
    }

    private static Task OfferRewards(ColorfulPhilosophers eventModel, CardPoolModel pool)
    {
        object? result = OfferRewardsMethod.Invoke(eventModel, new object[] { pool });
        return (Task)result!;
    }
}
