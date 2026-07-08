#nullable enable
using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using ShinGetterMod.Models.Cards;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(ProgressState), nameof(ProgressState.FromSerializable))]
internal static class ShinGetterProgressCompatibilityPatch
{
    private static readonly ModelId OldSunshineTypoId = new("CARD", "S_G_C_" + "STONER_SHINE");
    private static readonly ModelId NewSunshineId = ModelDb.GetId<SGC_StonerSunshine>();

    private static void Prefix(SerializableProgress save)
    {
        ArgumentNullException.ThrowIfNull(save);

        NormalizeCardStats(save.CardStats);
        NormalizeDiscoveredCards(save.DiscoveredCards);
    }

    private static void NormalizeCardStats(List<CardStats> cardStats)
    {
        if (cardStats.Count == 0)
        {
            return;
        }

        int firstIndex = -1;
        long timesPicked = 0;
        long timesSkipped = 0;
        long timesWon = 0;
        long timesLost = 0;

        for (int i = cardStats.Count - 1; i >= 0; i--)
        {
            CardStats stats = cardStats[i];
            if (NormalizeCardId(stats.Id) != NewSunshineId)
            {
                continue;
            }

            firstIndex = firstIndex < 0 ? i : Math.Min(firstIndex, i);
            timesPicked += stats.TimesPicked;
            timesSkipped += stats.TimesSkipped;
            timesWon += stats.TimesWon;
            timesLost += stats.TimesLost;
            cardStats.RemoveAt(i);
        }

        if (firstIndex < 0)
        {
            return;
        }

        cardStats.Insert(Math.Min(firstIndex, cardStats.Count), new CardStats
        {
            Id = NewSunshineId,
            TimesPicked = timesPicked,
            TimesSkipped = timesSkipped,
            TimesWon = timesWon,
            TimesLost = timesLost,
        });
    }

    private static void NormalizeDiscoveredCards(List<ModelId> discoveredCards)
    {
        int firstIndex = -1;
        for (int i = 0; i < discoveredCards.Count; i++)
        {
            if (NormalizeCardId(discoveredCards[i]) == NewSunshineId)
            {
                firstIndex = i;
                break;
            }
        }

        if (firstIndex < 0)
        {
            return;
        }

        for (int i = discoveredCards.Count - 1; i >= 0; i--)
        {
            if (NormalizeCardId(discoveredCards[i]) == NewSunshineId)
            {
                discoveredCards.RemoveAt(i);
            }
        }

        discoveredCards.Insert(Math.Min(firstIndex, discoveredCards.Count), NewSunshineId);
    }

    private static ModelId? NormalizeCardId(ModelId? id) =>
        id == OldSunshineTypoId ? NewSunshineId : id;
}
