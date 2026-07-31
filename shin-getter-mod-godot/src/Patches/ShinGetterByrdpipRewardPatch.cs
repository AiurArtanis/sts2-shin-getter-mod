#nullable enable
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(Byrdpip), nameof(Byrdpip.AfterObtained))]
internal static class ShinGetterByrdpipAfterObtainedPatch
{
    private static void Prefix(Byrdpip __instance, out bool __state)
    {
        __state = HasByrdonisEgg(__instance.Owner);
    }

    private static void Postfix(Byrdpip __instance, bool __state, ref Task __result)
    {
        if (__state)
            return;

        __result = AddByrdSwoopAfterObtained(__result, __instance.Owner);
    }

    private static bool HasByrdonisEgg(Player player)
    {
        if (PileType.Deck.GetPile(player).Cards.Any(card => card is ByrdonisEgg))
            return true;

        return CombatManager.Instance.IsInProgress
            && player.PlayerCombatState?.AllCards.Any(card => card is ByrdonisEgg) == true;
    }

    private static async Task AddByrdSwoopAfterObtained(Task originalTask, Player owner)
    {
        await originalTask;
        CardModel byrdSwoop = owner.RunState.CreateCard<ByrdSwoop>(owner);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(byrdSwoop, PileType.Deck));
    }
}
