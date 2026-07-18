#nullable enable
using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using ShinGetterMod.Audio;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
internal static class ShinGetterCharacterSelectAudioPatch
{
    private const string CharacterSelectSfxPath =
        "res://audio/sfx/characters/shin_getter/voices/transform.wav";

    private static bool Prefix(string sfx, float volume)
    {
        if (!string.Equals(sfx, CharacterSelectSfxPath, StringComparison.Ordinal))
            return true;

        ShinGetterVoiceService.PlayAudio(sfx, volume);
        return false;
    }
}
