#nullable enable
using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), new[] { typeof(string), typeof(float) })]
internal static class ShinGetterCharacterSelectAudioPatch
{
    private const string CharacterSelectSfxPath =
        "res://audio/sfx/characters/shin_getter/shin_getter_select.wav";

    private static bool Prefix(string sfx, float volume)
    {
        if (!string.Equals(sfx, CharacterSelectSfxPath, StringComparison.Ordinal))
            return true;

        if (NonInteractiveMode.IsActive)
            return false;

        AudioStream? stream = ResourceLoader.Load<AudioStream>(sfx);
        if (stream is null)
            return false;

        var player = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = "SFX",
            VolumeDb = Mathf.LinearToDb(volume),
        };
        player.Finished += player.QueueFree;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(player);
        player.Play();
        return false;
    }
}
