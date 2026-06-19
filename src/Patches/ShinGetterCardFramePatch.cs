#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using ShinGetterMod.Models.CardPools;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class ShinGetterCardFramePatch
{
    private static readonly AccessTools.FieldRef<NCard, TextureRect> FrameRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_frame");

    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady())
            return;

        CardModel? model = __instance.Model;
        if (model?.VisualCardPool is not ShinGetterCardPool)
            return;

        TextureRect? frame = FrameRef(__instance);
        if (frame == null)
            return;

        frame.Texture = model.Frame;
        frame.SelfModulate = Colors.White;

        Material? material = ModelDb.CardPool<ShinGetterCardPool>().FrameMaterial;
        if (material is ShaderMaterial shaderMaterial)
        {
            ShaderMaterial frameMaterial = (ShaderMaterial)shaderMaterial.Duplicate();
            frameMaterial.ResourceLocalToScene = true;
            frameMaterial.SetShaderParameter("frame_height", Mathf.Max(frame.Size.Y, 1f));
            frame.Material = frameMaterial;
        }
        else
        {
            frame.Material = material;
        }
    }
}
