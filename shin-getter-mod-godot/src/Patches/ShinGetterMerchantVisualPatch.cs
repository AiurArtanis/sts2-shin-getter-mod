using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Nodes.Screens.Shops;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
internal static class ShinGetterMerchantVisualPatch
{
    private static void Postfix(NMerchantRoom __instance)
    {
        ShinGetterMerchantVisuals.Refresh(__instance);
    }
}
