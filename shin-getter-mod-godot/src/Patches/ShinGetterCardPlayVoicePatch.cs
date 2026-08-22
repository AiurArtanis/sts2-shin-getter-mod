using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class ShinGetterCardPlayVoicePatch
{
    private static void Prefix(CardModel __instance)
    {
        ShinGetterVoiceService.TryPlayCardVoiceAtCardPlayStart(__instance);

        if (__instance is ShinGetterCardBase
            || __instance is Maul
            || __instance.Owner?.Character is not ShinGetter)
            return;

        string trigger = __instance.Type switch
        {
            CardType.Attack => "Attack",
            CardType.Skill when __instance.GainsBlock => "Block",
            CardType.Skill or CardType.Power => "Cast",
            _ => "Dash",
        };

        NShinGetterStaticVisuals.TryPlayCreatureActionAnimation(__instance.Owner.Creature, trigger);
    }
}
