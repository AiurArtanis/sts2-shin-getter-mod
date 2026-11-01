#nullable enable
using System.Text.Json.Serialization.Metadata;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShinGetterMod.Models.Relics;
using ShinGetterMod.Services;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(JsonSerializationUtility), nameof(JsonSerializationUtility.AlphabetizeProperties))]
internal static class ShinGetterEventStateJsonPatch
{
    private static void Postfix(JsonTypeInfo info) => ShinGetterPlayerEventState.AddJsonField(info);
}

[HarmonyPatch(typeof(Player), nameof(Player.ToSerializable))]
internal static class ShinGetterEventStateCapturePatch
{
    private static void Postfix(Player __instance, SerializablePlayer __result) => ShinGetterPlayerEventState.Capture(__instance, __result);
}
[HarmonyPatch(typeof(Player), nameof(Player.FromSerializable))]
internal static class ShinGetterEventStateLoadPatch
{
    private static void Postfix(SerializablePlayer save, Player __result) => ShinGetterPlayerEventState.Restore(__result, save);
}
[HarmonyPatch(typeof(Player), nameof(Player.SyncWithSerializedPlayer))]
internal static class ShinGetterEventStateSyncPatch
{
    private static void Postfix(Player __instance, SerializablePlayer player) => ShinGetterPlayerEventState.Restore(__instance, player);
}
[HarmonyPatch(typeof(SerializablePlayer), nameof(SerializablePlayer.Anonymized))]
internal static class ShinGetterEventStateAnonymizePatch
{
    private static void Postfix(SerializablePlayer __instance, SerializablePlayer __result) =>
        ShinGetterPlayerEventState.SetWire(__result, ShinGetterPlayerEventState.GetWire(__instance));
}
[HarmonyPatch(typeof(SerializablePlayer), nameof(SerializablePlayer.Serialize))]
internal static class ShinGetterEventStatePacketWritePatch
{
    private static void Postfix(SerializablePlayer __instance, PacketWriter writer)
    {
        if (ShinGetterPlayerEventState.IsShinGetter(__instance.CharacterId))
            writer.WriteString(ShinGetterPlayerEventState.GetWire(__instance)
                ?? ShinGetterPlayerEventState.Encode(new ShinGetterPlayerEventState.Data()));
    }
}
[HarmonyPatch(typeof(SerializablePlayer), nameof(SerializablePlayer.Deserialize))]
internal static class ShinGetterEventStatePacketReadPatch
{
    private static void Postfix(SerializablePlayer __instance, PacketReader reader)
    {
        if (ShinGetterPlayerEventState.IsShinGetter(__instance.CharacterId))
            ShinGetterPlayerEventState.SetWire(__instance, reader.ReadString());
    }
}
[HarmonyPatch(typeof(RelicCmd), nameof(RelicCmd.Remove))]
internal static class ShinGetterEventStateFreezePatch
{
    private static void Prefix(RelicModel relic)
    {
        if (relic is SGR_GetterFurnace or SGR_EmperorsFragment)
            _ = ShinGetterPlayerEventState.Get(relic.Owner);
    }
}
