#nullable enable
using System;
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
    // The stable Beta minimum (0.113.0) does not expose this virtual getter.
    private static readonly Func<EnchantmentModel, bool>? EnchantmentGainsBlock =
        typeof(EnchantmentModel).GetProperty("GainsBlock")?.GetMethod?.CreateDelegate<Func<EnchantmentModel, bool>>();

    private static void Prefix(CardModel __instance)
    {
        ShinGetterVoiceService.TryPlayCardVoiceAtCardPlayStart(__instance);

        if (__instance is ShinGetterCardBase
            || __instance is Maul
            || __instance.Owner?.Character is not ShinGetter)
            return;

        NShinGetterStaticVisuals.TryPlayCreatureActionAnimation(__instance.Owner.Creature, AnimationTriggerFor(__instance));
    }

    private static string AnimationTriggerFor(CardModel card) => card.Type switch
    {
        CardType.Attack => "Attack",
        CardType.Skill when card.GainsBlock
            || (card.Enchantment is { } enchantment && EnchantmentGainsBlock?.Invoke(enchantment) == true) => "Block",
        CardType.Skill or CardType.Power => "Cast",
        _ => "Dash",
    };
}
