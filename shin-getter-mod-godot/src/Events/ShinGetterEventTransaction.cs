#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Services;

namespace ShinGetterMod.Events;

// Event-only rollback. No combat instances, global player replacement, or re-running pickup hooks.
internal sealed class ShinGetterEventTransaction
{
    private static readonly PropertyInfo RemovedFlag = AccessTools.Property(typeof(RelicModel), nameof(RelicModel.HasBeenRemovedFromState))
        ?? throw new MissingMemberException(nameof(RelicModel.HasBeenRemovedFromState));
    private readonly Player _owner;
    private readonly SerializablePlayer _saved;
    private readonly RelicModel[] _relics;
    private readonly PotionModel?[] _potions;
    private readonly object? _history;
    private readonly Dictionary<PropertyInfo, object> _historyValues = new();
    private readonly SerializableRelicGrabBag _sharedRelics;

    internal ShinGetterEventTransaction(Player owner)
    {
        _owner = owner;
        _saved = owner.ToSerializable();
        _relics = owner.Relics.ToArray();
        _potions = owner.PotionSlots.ToArray();
        _sharedRelics = owner.RunState.SharedRelicGrabBag.ToSerializable();
        _history = owner.RunState.CurrentMapPointHistoryEntry?.GetEntry(owner.NetId);
        if (_history != null)
            foreach (PropertyInfo property in _history.GetType().GetProperties())
            {
                object? value = property.GetValue(_history);
                if (value is IList list) _historyValues[property] = list.Count;
                else if (property.CanWrite && value is int) _historyValues[property] = value;
            }
    }

    internal void Rollback()
    {
        // Rebuild only the permanent deck using the native card loader. Clear previews/selection
        // before re-entering INITIAL so no callback retains a replaced target instance.
        foreach (CardModel card in _owner.Deck.Cards.ToArray())
        {
            card.RemoveFromCurrentPile();
            card.RemoveFromState();
        }
        foreach (SerializableCard card in _saved.Deck)
            _owner.Deck.AddInternal(_owner.RunState.LoadCard(card, _owner));
        foreach (RelicModel relic in _owner.Relics.ToArray())
            if (!_relics.Contains(relic)) _owner.RemoveRelicInternal(relic);
        for (int i = 0; i < _relics.Length; i++)
        {
            _saved.Relics[i].Props?.Fill(_relics[i]);
            if (!_owner.Relics.Contains(_relics[i]))
            {
                RestoreRemovedRelicFlag(_relics[i]);
                _owner.AddRelicInternal(_relics[i], i);
            }
        }
        for (int i = 0; i < _potions.Length; i++)
        {
            PotionModel? current = _owner.GetPotionAtSlotIndex(i);
            if (ReferenceEquals(current, _potions[i])) continue;
            if (current != null) _owner.DiscardPotionInternal(current);
            if (_potions[i] != null && !_owner.AddPotionInternal(_potions[i]!, i).success)
                throw new InvalidOperationException("Failed to restore original potion slot.");
        }
        _owner.Creature.SetMaxHpInternal(_saved.MaxHp);
        _owner.Creature.SetCurrentHpInternal(_saved.CurrentHp);
        _owner.Gold = _saved.Gold;
        _owner.PlayerRng.LoadFromSerializable(_saved.Rng);
        _owner.PlayerOdds.LoadFromSerializable(_saved.Odds);
        _owner.RelicGrabBag.LoadFromSerializable(_saved.RelicGrabBag);
        _owner.RunState.SharedRelicGrabBag.LoadFromSerializable(_sharedRelics);
        _owner.DiscoveredCards.Clear(); _owner.DiscoveredCards.AddRange(_saved.DiscoveredCards);
        _owner.DiscoveredPotions.Clear(); _owner.DiscoveredPotions.AddRange(_saved.DiscoveredPotions);
        _owner.DiscoveredRelics.Clear(); _owner.DiscoveredRelics.AddRange(_saved.DiscoveredRelics);
        ShinGetterPlayerEventState.Restore(_owner, _saved);
        if (_history != null)
            foreach (var pair in _historyValues)
            {
                if (pair.Key.GetValue(_history) is IList list)
                    while (list.Count > (int)pair.Value) list.RemoveAt(list.Count - 1);
                else pair.Key.SetValue(_history, pair.Value);
            }
    }

    internal static void RestoreRemovedRelicFlag(RelicModel relic) => RemovedFlag.SetValue(relic, false);
}
