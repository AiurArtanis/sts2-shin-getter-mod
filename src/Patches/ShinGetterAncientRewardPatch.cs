#nullable enable
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Characters;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(DustyTome), nameof(DustyTome.SetupForPlayer))]
internal static class ShinGetterDustyTomePatch
{
    private static void Postfix(DustyTome __instance, Player player)
    {
        if (player.Character is ShinGetter)
            __instance.AncientCard = ModelDb.Card<SGC_ShinForm>().Id;
    }
}

[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.SetupForPlayer))]
internal static class ShinGetterTouchOfOrobasSetupPatch
{
    private static bool Prefix(TouchOfOrobas __instance, Player player, ref bool __result)
    {
        if (player.Character is not ShinGetter)
            return true;

        SGR_GetterFurnace? getterFurnace = ShinGetterTouchOfOrobasPatchHelpers.FindGetterFurnace(player);
        if (getterFurnace == null)
        {
            __result = false;
            return false;
        }

        __instance.StarterRelic = getterFurnace.Id;
        __instance.UpgradedRelic = ModelDb.Relic<SGR_EmperorsFragment>().Id;
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.AfterObtained))]
internal static class ShinGetterTouchOfOrobasAfterObtainedPatch
{
    private static bool Prefix(TouchOfOrobas __instance, ref Task __result)
    {
        Player player = __instance.Owner;
        if (player.Character is not ShinGetter)
            return true;

        __result = ShinGetterTouchOfOrobasPatchHelpers.TransformGetterFurnace(player);
        return false;
    }
}

[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.SetupForPlayer))]
internal static class ShinGetterArchaicToothSetupPatch
{
    private static bool Prefix(ArchaicTooth __instance, Player player, ref bool __result)
    {
        if (player.Character is not ShinGetter)
            return true;

        CardModel? getterBeam = ShinGetterArchaicToothPatchHelpers.FindGetterBeam(player);
        if (getterBeam == null)
        {
            __result = false;
            return false;
        }

        CardModel stonerSunshine = ShinGetterArchaicToothPatchHelpers.CreateStonerSunshineFrom(getterBeam);
        __instance.SetupForTests(getterBeam.ToSerializable(), stonerSunshine.ToSerializable());
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(ArchaicTooth), nameof(ArchaicTooth.AfterObtained))]
internal static class ShinGetterArchaicToothAfterObtainedPatch
{
    private static bool Prefix(ArchaicTooth __instance, ref Task __result)
    {
        Player player = __instance.Owner;
        if (player.Character is not ShinGetter)
            return true;

        __result = ShinGetterArchaicToothPatchHelpers.TransformGetterBeam(player);
        return false;
    }
}

internal static class ShinGetterTouchOfOrobasPatchHelpers
{
    internal static SGR_GetterFurnace? FindGetterFurnace(Player player) =>
        player.GetRelic<SGR_GetterFurnace>();

    internal static async Task TransformGetterFurnace(Player player)
    {
        SGR_GetterFurnace? getterFurnace = FindGetterFurnace(player);
        if (getterFurnace == null)
            return;

        await RelicCmd.Replace(getterFurnace, ModelDb.Relic<SGR_EmperorsFragment>().ToMutable());
    }
}

internal static class ShinGetterArchaicToothPatchHelpers
{
    internal static CardModel? FindGetterBeam(Player player) =>
        player.Deck.Cards.FirstOrDefault(card => card.Id == ModelDb.Card<SGC_GetterBeam>().Id);

    internal static CardModel CreateStonerSunshineFrom(CardModel getterBeam)
    {
        CardModel stonerSunshine = getterBeam.Owner.RunState.CreateCard<SGC_StonerSunshine>(getterBeam.Owner);
        if (getterBeam.IsUpgraded)
            CardCmd.Upgrade(stonerSunshine);

        if (getterBeam.Enchantment != null)
        {
            var enchantment = (EnchantmentModel)getterBeam.Enchantment.MutableClone();
            CardCmd.Enchant(enchantment, stonerSunshine, enchantment.Amount);
        }

        return stonerSunshine;
    }

    internal static async Task TransformGetterBeam(Player player)
    {
        CardModel? getterBeam = FindGetterBeam(player);
        if (getterBeam == null)
            return;

        CardModel stonerSunshine = CreateStonerSunshineFrom(getterBeam);
        await CardCmd.Transform(getterBeam, stonerSunshine);
    }
}
