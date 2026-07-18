using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(AncientEventModel), "RelicOption", new[] { typeof(RelicModel), typeof(string), typeof(string) })]
internal static class ShinGetterTezcataraPatch
{
    private static void Prefix(AncientEventModel __instance, ref RelicModel relic)
    {
        if (__instance is Tezcatara
            && __instance.Owner?.Character is ShinGetter
            && relic is YummyCookie)
        {
            relic = ModelDb.Relic<SGR_YummyCookie>().ToMutable();
        }
    }
}
