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

        CardModel? getterLaunch = ShinGetterArchaicToothPatchHelpers.FindGetterLaunch(player);
        if (getterLaunch == null)
        {
            __result = false;
            return false;
        }

        CardModel getterLanding = ShinGetterArchaicToothPatchHelpers.CreateGetterLandingFrom(getterLaunch);
        __instance.SetupForTests(getterLaunch.ToSerializable(), getterLanding.ToSerializable());
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

        __result = ShinGetterArchaicToothPatchHelpers.TransformGetterLaunch(player);
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

        await RelicCmd.Replace(getterFurnace, SGR_EmperorsFragment.CreateFrom(getterFurnace));
    }
}

internal static class ShinGetterArchaicToothPatchHelpers
{
    internal static CardModel? FindGetterLaunch(Player player) =>
        player.Deck.Cards.FirstOrDefault(card => card.Id == ModelDb.Card<SGC_GetterLaunch>().Id);

    internal static CardModel CreateGetterLandingFrom(CardModel getterLaunch)
    {
        CardModel getterLanding = getterLaunch.Owner.RunState.CreateCard<SGC_GetterLanding>(getterLaunch.Owner);
        if (getterLaunch.IsUpgraded)
            CardCmd.Upgrade(getterLanding);

        if (getterLaunch.Enchantment != null)
        {
            var enchantment = (EnchantmentModel)getterLaunch.Enchantment.MutableClone();
            CardCmd.Enchant(enchantment, getterLanding, enchantment.Amount);
        }

        return getterLanding;
    }

    internal static async Task TransformGetterLaunch(Player player)
    {
        CardModel? getterLaunch = FindGetterLaunch(player);
        if (getterLaunch == null)
            return;

        CardModel getterLanding = CreateGetterLandingFrom(getterLaunch);
        await CardCmd.Transform(getterLaunch, getterLanding);
    }
}
