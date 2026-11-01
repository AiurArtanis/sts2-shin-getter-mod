#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace ShinGetterMod.Services;

internal static class ShinGetterCombatCardChanges
{
    private sealed class Snapshot
    {
        internal readonly Dictionary<CardModel, int> Upgrades = new();
        internal readonly Dictionary<CardModel, (EnchantmentModel? Enchantment, decimal Amount)> Enchantments = new();
        internal readonly HashSet<CardModel> RemovedExhaust = new();
    }
    private static readonly ConditionalWeakTable<Player, Snapshot> Changes = new();

    internal static void ChangeUpgradeLevels(Player owner, bool upgrade)
    {
        if (owner.PlayerCombatState == null) return;
        Snapshot state = Changes.GetOrCreateValue(owner);
        foreach (CardModel card in owner.PlayerCombatState.AllCards
            .Where(c => c.Owner == owner && c.Pile?.IsCombatPile == true).ToArray())
        {
            state.Upgrades.TryAdd(card, card.CurrentUpgradeLevel);
            if (upgrade && card.IsUpgradable) CardCmd.Upgrade(card, CardPreviewStyle.None);
            else if (!upgrade && card.IsUpgraded) CardCmd.Downgrade(card);
        }
    }

    internal static void RememberEnchantment(CardModel card)
    {
        Changes.GetOrCreateValue(card.Owner).Enchantments.TryAdd(card,
            (card.Enchantment == null ? null : ModelDb.GetById<EnchantmentModel>(card.Enchantment.Id).ToMutable(),
                card.Enchantment?.Amount ?? 0));
    }

    internal static void RememberRemovedExhaust(CardModel card) => Changes.GetOrCreateValue(card.Owner).RemovedExhaust.Add(card);

    internal static void Restore(Player owner)
    {
        if (!Changes.TryGetValue(owner, out Snapshot? state)) return;
        Changes.Remove(owner);
        // Only the captured combat instances are restored. Permanent DeckVersion is untouched.
        foreach (var pair in state.Enchantments)
        {
            if (pair.Key.Owner != owner) continue;
            CardCmd.ClearEnchantment(pair.Key);
            if (pair.Value.Enchantment != null)
                CardCmd.Enchant(pair.Value.Enchantment, pair.Key, pair.Value.Amount);
        }
        foreach (var pair in state.Upgrades)
        {
            if (pair.Key.Owner != owner) continue;
            if (pair.Key.CurrentUpgradeLevel == pair.Value) continue;
            pair.Key.DowngradeInternal();
            for (int i = 0; i < pair.Value; i++) pair.Key.UpgradeInternal();
            pair.Key.FinalizeUpgradeInternal();
        }
        foreach (CardModel card in state.RemovedExhaust)
            if (card.Owner == owner && !card.Keywords.Contains(CardKeyword.Exhaust)) CardCmd.ApplyKeyword(card, CardKeyword.Exhaust);
    }
}
