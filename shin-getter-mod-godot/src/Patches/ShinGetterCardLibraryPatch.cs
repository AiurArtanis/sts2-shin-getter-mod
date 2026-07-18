#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Saves;
using ShinGetterMod.Models.CardPools;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
internal static class ShinGetterCardLibraryPatch
{
    private const string PoolToggleScenePath = "res://scenes/screens/card_library/library_pool_toggle.tscn";
    private const string ShinGetterIconPath = "res://images/ui/top_panel/character_icon_shin_getter.png";

    private static readonly AccessTools.FieldRef<NCardLibrary, Dictionary<NCardPoolFilter, Func<CardModel, bool>>> PoolFiltersRef =
        AccessTools.FieldRefAccess<NCardLibrary, Dictionary<NCardPoolFilter, Func<CardModel, bool>>>("_poolFilters");

    private static readonly AccessTools.FieldRef<NCardLibrary, Dictionary<CharacterModel, NCardPoolFilter>> CardPoolFiltersRef =
        AccessTools.FieldRefAccess<NCardLibrary, Dictionary<CharacterModel, NCardPoolFilter>>("_cardPoolFilters");

    private static readonly MethodInfo UpdateCardPoolFilterMethod =
        AccessTools.Method(typeof(NCardLibrary), "UpdateCardPoolFilter");

    private static void Postfix(NCardLibrary __instance)
    {
        GridContainer? container = __instance.GetNodeOrNull<GridContainer>("Sidebar/MarginContainer/TopVBox/PoolFilters");
        if (container == null || container.GetNodeOrNull<NCardPoolFilter>("ShinGetterPool") != null)
            return;

        PackedScene? scene = ResourceLoader.Load<PackedScene>(PoolToggleScenePath);
        Texture2D? icon = ResourceLoader.Load<Texture2D>(ShinGetterIconPath);
        if (scene == null || icon == null)
            return;

        NCardPoolFilter filter = scene.Instantiate<NCardPoolFilter>(PackedScene.GenEditState.Disabled);
        filter.Name = "ShinGetterPool";
        filter.Loc = new LocString("card_library", "POOL_SHIN_GETTER_TIP");

        TextureRect? image = filter.GetNodeOrNull<TextureRect>("Image");
        TextureRect? shadow = filter.GetNodeOrNull<TextureRect>("Image/Shadow");
        if (image != null)
            image.Texture = icon;
        if (shadow != null)
            shadow.Texture = icon;

        filter.Connect(NCardPoolFilter.SignalName.Toggled, Callable.From<NCardPoolFilter>(selected =>
        {
            UpdateCardPoolFilterMethod.Invoke(__instance, new object[] { selected });
        }));

        container.AddChild(filter);
        PoolFiltersRef(__instance)[filter] = card => card.Pool is ShinGetterCardPool;
        CardPoolFiltersRef(__instance)[ModelDb.Character<ShinGetter>()] = filter;
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid._Ready))]
internal static class ShinGetterCardLibraryGridPatch
{
    private static readonly AccessTools.FieldRef<NCardLibraryGrid, List<CardModel>> AllCardsRef =
        AccessTools.FieldRefAccess<NCardLibraryGrid, List<CardModel>>("_allCards");

    private static void Postfix(NCardLibraryGrid __instance)
    {
        List<CardModel> allCards = AllCardsRef(__instance);
        foreach (CardModel card in ModelDb.CardPool<ShinGetterCardPool>().AllCards)
        {
            if (card.ShouldShowInCardLibrary && !allCards.Contains(card))
                allCards.Add(card);
        }

        __instance.RefreshVisibility();
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), "GetCardVisibility")]
internal static class ShinGetterCardLibraryVisibilityPatch
{
    private static void Postfix(CardModel card, ref ModelVisibility __result)
    {
        if (card.Pool is not ShinGetterCardPool)
            return;

        __result = SaveManager.Instance.Progress.DiscoveredCards.Contains(card.Id)
            ? ModelVisibility.Visible
            : ModelVisibility.NotSeen;
    }
}
