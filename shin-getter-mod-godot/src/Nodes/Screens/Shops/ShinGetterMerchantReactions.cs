#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rooms;
using ShinGetterMod.Models.Relics;

namespace ShinGetterMod.Nodes.Screens.Shops;

internal static class ShinGetterMerchantReactions
{
    private const string AvailableKey = "SHIN_GETTER_GOOD_CITIZEN.open_available";
    private const string UsedKey = "SHIN_GETTER_GOOD_CITIZEN.open_used";
    private const string PurchaseKey = "SHIN_GETTER_GOOD_CITIZEN.purchase_used";
    private static readonly ConditionalWeakTable<MerchantInventory, GreetingState> Greetings = new();
    private static readonly ConditionalWeakTable<MerchantEntry, FreePurchaseState> FreePurchases = new();
    private static MethodInfo? _showRandom;
    private static bool _failureLogged;

    public static void MarkFreePurchase(Player player, MerchantEntry entry)
    {
        // Called only after this relic has actually appended the current act's saved usage.
        // Presentation errors must never escape back into the purchase hook.
        try
        {
            if (player.RunState.CurrentRoom is not MerchantRoom ||
                !ReferenceEquals(LocalContext.GetMe(player.RunState), player))
                return;
            FreePurchases.Remove(entry);
            FreePurchases.Add(entry, new FreePurchaseState(player));
        }
        catch (Exception exception)
        {
            WarnOnce(exception.Message);
        }
    }

    public static void ClearPurchase(MerchantEntry entry)
    {
        if (entry != null)
            FreePurchases.Remove(entry);
    }

    public static void PlayWelcome(string nativeSfx, float volume, NMerchantInventory inventory)
    {
        string sound = nativeSfx;
        try
        {
            MerchantInventory? model = inventory.Inventory;
            Player? player = GetLocalPlayer(inventory);
            SGR_GoodCitizenCard? relic = player?.GetRelic<SGR_GoodCitizenCard>();
            if (relic != null && model != null)
            {
                GreetingState state = Greetings.GetValue(model, _ => new GreetingState());
                state.PendingKey = null;
                if (!state.Greeted)
                {
                    state.Greeted = true;
                    state.PendingKey = relic.IsUsedUp ? UsedKey : AvailableKey;
                    if (!relic.IsUsedUp)
                        sound = FmodSfx.merchantDisappointment;
                    else
                        volume *= 0.65f;
                }
            }
        }
        catch (Exception exception)
        {
            WarnOnce(exception.Message);
        }
        TryPlay(sound, volume);
    }

    public static void ShowWelcome(NMerchantDialogue dialogue, NMerchantInventory inventory)
    {
        string? key = null;
        try
        {
            MerchantInventory? model = inventory.Inventory;
            if (model != null && GetLocalPlayer(inventory) != null &&
                Greetings.TryGetValue(model, out GreetingState? state))
            {
                key = state.PendingKey;
                state.PendingKey = null;
            }
        }
        catch (Exception exception)
        {
            WarnOnce(exception.Message);
        }
        if (key == null || !TryShow(dialogue, key))
            dialogue.ShowOnInventoryOpen();
    }

    public static void PlayPurchase(string nativeSfx, float volume, NMerchantInventory inventory, MerchantEntry entry)
    {
        string sound = nativeSfx;
        try
        {
            if (TryGetFreePurchase(inventory, entry, out FreePurchaseState? state))
            {
                if (state.SoundPlayed)
                    return;
                state.SoundPlayed = true;
                sound = FmodSfx.merchantDisappointment;
            }
        }
        catch (Exception exception)
        {
            WarnOnce(exception.Message);
        }
        TryPlay(sound, volume);
    }

    public static void ShowPurchase(NMerchantDialogue dialogue, PurchaseStatus status,
        NMerchantInventory inventory, MerchantEntry entry)
    {
        bool usedCertificate = false;
        try
        {
            usedCertificate = status == PurchaseStatus.Success && TryGetFreePurchase(inventory, entry, out _);
            if (usedCertificate)
                FreePurchases.Remove(entry);
        }
        catch (Exception exception)
        {
            WarnOnce(exception.Message);
        }
        if (!usedCertificate || !TryShow(dialogue, PurchaseKey))
            dialogue.ShowForPurchaseAttempt(status);
    }

    private static bool TryGetFreePurchase(NMerchantInventory inventory, MerchantEntry entry,
        [NotNullWhen(true)] out FreePurchaseState? state)
    {
        if (FreePurchases.TryGetValue(entry, out state) &&
            ReferenceEquals(state.Player, GetLocalPlayer(inventory)))
            return true;
        state = null;
        return false;
    }

    private static Player? GetLocalPlayer(NMerchantInventory inventory)
    {
        Player? player = inventory.Inventory?.Player;
        if (inventory.GetType() != typeof(NMerchantInventory) || player == null ||
            player.RunState.CurrentRoom is not MerchantRoom || player.Creature.IsDead ||
            NMerchantRoom.Instance?.Inventory != inventory ||
            !ReferenceEquals(LocalContext.GetMe(player.RunState), player))
            return null;
        return player;
    }

    private static bool TryShow(NMerchantDialogue dialogue, string key)
    {
        try
        {
            // These keys are outside MERCHANT.talk.*, so they never enter the native pools.
            LocString? line = LocString.GetIfExists("merchant_room", key);
            _showRandom ??= AccessTools.Method(typeof(NMerchantDialogue), "ShowRandom");
            if (line == null || _showRandom == null || !GodotObject.IsInstanceValid(dialogue))
            {
                WarnOnce("Native dialogue method or localized line is unavailable.");
                return false;
            }
            _showRandom.Invoke(dialogue, new object[] { new[] { line } });
            return true;
        }
        catch (Exception exception)
        {
            WarnOnce(exception.Message);
            return false;
        }
    }

    private static void TryPlay(string sound, float volume)
    {
        try
        {
            SfxCmd.Play(sound, volume);
        }
        catch (Exception exception)
        {
            // Keep the new text even if the optional existing sound cannot be played.
            WarnOnce(exception.Message);
        }
    }

    internal static void WarnOnce(string detail)
    {
        if (_failureLogged)
            return;
        _failureLogged = true;
        Log.Warn($"ShinGetterMod: merchant reaction unavailable; preserving native shop flow. {detail}");
    }

    private sealed class GreetingState
    {
        public bool Greeted;
        public string? PendingKey;
    }

    private sealed class FreePurchaseState
    {
        public readonly Player Player;
        public bool SoundPlayed;
        public FreePurchaseState(Player player) => Player = player;
    }
}
