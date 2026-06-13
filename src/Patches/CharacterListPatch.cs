using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(ModelDb), "get_AllCharacters")]
internal static class ModelDbAllCharactersPatch
{
    private static void Postfix(ref IEnumerable<CharacterModel> __result)
    {
        var shinGetter = ModelDb.GetByIdOrNull<CharacterModel>(ModelDb.GetId(typeof(ShinGetter)));
        if (shinGetter == null) return;

        __result = __result.Append(shinGetter).Distinct().ToArray();
    }
}
