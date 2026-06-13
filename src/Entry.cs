using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace ShinGetterMod;

[ModInitializer("Init")]
public static class Entry
{
    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // Register all Harmony patches (CharacterListPatch)
        Harmony harmony = new Harmony("Artanis.ShinGetterMod");
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
            {
                new PatchClassProcessor(harmony, type).Patch();
            }
        }

        Log.Info("ShinGetterMod - loading success! (68 cards + 3 derived cards)");
    }
}
