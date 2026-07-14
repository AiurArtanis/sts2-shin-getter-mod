#nullable enable
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Models.CardPools;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Patches;

[HarmonyPatch(typeof(NCard))]
internal static class ShinGetterCardFramePatch
{
    private const float FrameTintTweenSeconds = 0.22f;
    private const string DefaultGetterRayKey = "1CC099";
    private const string GetterOneRedKey = "B00A0C";
    private const string GetterTwoSilverKey = "D9E4DE";
    private const string GetterThreeYellowKey = "C4AD59";
    private const string DefaultDisabledKey = "default";
    private const string DynamicFrameTexturePath = "res://images/atlases/ui_atlas.sprites/card/card_frame_attack_s.tres";

    private static readonly ConditionalWeakTable<NCard, FrameTintState> TintStates = new();
    private static readonly AccessTools.FieldRef<NCard, TextureRect> FrameRef =
        AccessTools.FieldRefAccess<NCard, TextureRect>("_frame");
    private static Texture2D? SharedDynamicFrameTexture;
    private static int _formTransitionDepth;
    private static PendingFrameForm? PendingTransitionForm;
    private static int DefaultTintOverrideDepth;

    internal static bool IsFormTransitionActive => _formTransitionDepth > 0;

    private enum PendingFrameForm
    {
        Getter1,
        Getter2,
        Getter3,
        ShinDragon,
    }

    private readonly record struct FrameHsvTarget(string Key, Color BorderColor, float H, float S, float V, bool Enabled = true)
    {
    }

    private sealed class FrameTintState
    {
        public string ColorKey = string.Empty;
        public Color CurrentBorderColor = new(DefaultGetterRayKey);
        public float CurrentH = 0.455f;
        public float CurrentS = 1.05f;
        public float CurrentV = 1.16f;
        public Tween? Tween;
    }

    [HarmonyPatch("Reload")]
    [HarmonyPostfix]
    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady())
            return;

        CardModel? model = __instance.Model;
        if (model?.VisualCardPool is not ShinGetterCardPool)
            return;

        EnsureFrameMaterial(__instance);
        ApplyFrameTint(__instance, animate: false);
    }

    [HarmonyPatch(nameof(NCard.UpdateVisuals))]
    [HarmonyPostfix]
    private static void UpdateVisualsPostfix(NCard __instance, PileType pileType, CardPreviewMode previewMode)
    {
        ApplyFrameTint(__instance, animate: true);
    }

    public static void RefreshVisibleCards()
    {
        if (_formTransitionDepth > 0)
            return;

        RefreshVisibleCardsCore();
    }

    public static void BeginFormTransition()
    {
        _formTransitionDepth++;
    }

    public static void BeginFormTransition(ShinGetterForm targetForm)
    {
        _formTransitionDepth++;
        PendingTransitionForm = targetForm switch
        {
            ShinGetterForm.Getter1 => PendingFrameForm.Getter1,
            ShinGetterForm.Getter2 => PendingFrameForm.Getter2,
            ShinGetterForm.Getter3 => PendingFrameForm.Getter3,
            _ => null,
        };
    }

    public static void BeginFormTransitionToShinDragon()
    {
        _formTransitionDepth++;
        PendingTransitionForm = PendingFrameForm.ShinDragon;
    }

    public static void EndFormTransitionAndRefresh()
    {
        if (_formTransitionDepth > 0)
            _formTransitionDepth--;

        if (_formTransitionDepth == 0)
        {
            RefreshVisibleCardsCore();
            PendingTransitionForm = null;
        }
    }

    public static void BeginDefaultTintOverride()
    {
        DefaultTintOverrideDepth++;
    }

    public static void EndDefaultTintOverride()
    {
        if (DefaultTintOverrideDepth > 0)
            DefaultTintOverrideDepth--;
    }

    private static void RefreshVisibleCardsCore()
    {
        NCombatRoom? combatRoom = NCombatRoom.Instance;
        if (combatRoom == null || !combatRoom.IsNodeReady())
            return;

        RefreshCardNodes(combatRoom);
    }

    private static void EnsureFrameMaterial(NCard card)
    {
        CardModel? model = card.Model;
        if (model?.VisualCardPool is not ShinGetterCardPool)
            return;

        TextureRect? frame = FrameRef(card);
        if (frame == null)
            return;

        frame.Texture = GetFrameTexture(model);
        frame.SelfModulate = Colors.White;

        Material? material = ModelDb.CardPool<ShinGetterCardPool>().FrameMaterial;
        if (material is ShaderMaterial shaderMaterial)
        {
            ShaderMaterial frameMaterial = (ShaderMaterial)shaderMaterial.Duplicate();
            frameMaterial.ResourceLocalToScene = true;
            frame.Material = frameMaterial;
            ResetTintState(card);
        }
        else
        {
            frame.Material = material;
        }
    }

    private static void ApplyFrameTint(NCard card, bool animate)
    {
        if (!card.IsNodeReady())
            return;

        CardModel? model = card.Model;
        if (model?.VisualCardPool is not ShinGetterCardPool)
            return;

        TextureRect? frame = FrameRef(card);
        if (frame?.Material is not ShaderMaterial material)
            return;

        FrameHsvTarget target = GetTargetTint(model);
        FrameTintState state = TintStates.GetOrCreateValue(card);
        if (state.ColorKey == target.Key)
            return;

        state.Tween?.Kill();
        state.Tween = null;
        state.ColorKey = target.Key;

        if (!animate || !target.Enabled)
        {
            ApplyHsvToMaterial(material, target.BorderColor, target.H, target.S, target.V);
            state.CurrentBorderColor = target.BorderColor;
            state.CurrentH = target.H;
            state.CurrentS = target.S;
            state.CurrentV = target.V;
            return;
        }

        Color fromBorderColor = state.CurrentBorderColor;
        float fromH = state.CurrentH;
        float fromS = state.CurrentS;
        float fromV = state.CurrentV;
        Color toBorderColor = target.BorderColor;
        float toH = target.H;
        float toS = target.S;
        float toV = target.V;
        ApplyHsvToMaterial(material, fromBorderColor, fromH, fromS, fromV);
        state.Tween = card.CreateTween();
        state.Tween.TweenMethod(
                Callable.From<float>(t =>
                {
                    Color borderColor = fromBorderColor.Lerp(toBorderColor, t);
                    float h = Mathf.Lerp(fromH, toH, t);
                    float s = Mathf.Lerp(fromS, toS, t);
                    float v = Mathf.Lerp(fromV, toV, t);
                    ApplyHsvToMaterial(material, borderColor, h, s, v);
                    state.CurrentBorderColor = borderColor;
                    state.CurrentH = h;
                    state.CurrentS = s;
                    state.CurrentV = v;
                }),
                0f,
                1f,
                FrameTintTweenSeconds)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Sine);
    }

    private static FrameHsvTarget GetTargetTint(CardModel model)
    {
        if (!IsDynamicTintEligible(model))
            return DisabledTarget();

        if (DefaultTintOverrideDepth > 0)
            return DefaultGetterRayTarget();

        if (PendingTransitionForm is { } pendingForm)
            return TargetForPendingForm(pendingForm);

        if (!TryGetCombatOwnerCreature(model, out Creature creature))
            return DefaultGetterRayTarget();

        if (creature.GetPower<SGP_ShinForm>() != null)
            return DefaultGetterRayTarget();

        if (creature.GetPower<SGP_ShinGetterOne>() != null)
            return new FrameHsvTarget(GetterOneRedKey, new Color(GetterOneRedKey), 0.025f, 0.85f, 1.0f);

        if (creature.GetPower<SGP_ShinGetterTwo>() != null)
            return new FrameHsvTarget(GetterTwoSilverKey, new Color(GetterTwoSilverKey), 0.0f, 0.08f, 1.22f);

        if (creature.GetPower<SGP_ShinGetterThree>() != null)
            return new FrameHsvTarget(GetterThreeYellowKey, new Color(GetterThreeYellowKey), 0.14f, 1.35f, 1.12f);

        return DefaultGetterRayTarget();
    }

    private static bool TryGetCombatOwnerCreature(CardModel model, out Creature creature)
    {
        creature = null!;

        if (model.Owner?.Creature is { } ownerCreature)
        {
            creature = ownerCreature;
            return true;
        }

        if (model.CardScope is ICombatState scopedCombatState && TryGetLocalCombatCreature(scopedCombatState, out creature))
            return true;

        if (CombatManager.Instance.IsInProgress
            && CombatManager.Instance.DebugOnlyGetState() is { } activeCombatState
            && TryGetLocalCombatCreature(activeCombatState, out creature))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetLocalCombatCreature(ICombatState combatState, out Creature creature)
    {
        creature = null!;

        if (LocalContext.GetMe(combatState)?.Creature is { } localCreature)
        {
            creature = localCreature;
            return true;
        }

        if (combatState is CombatState concreteCombatState)
        {
            foreach (var player in concreteCombatState.Players)
            {
                if (player.Creature == null)
                    continue;

                creature = player.Creature;
                return true;
            }
        }

        return false;
    }

    private static FrameHsvTarget DefaultGetterRayTarget() =>
        new(DefaultGetterRayKey, new Color(DefaultGetterRayKey), 0.455f, 1.05f, 1.16f);

    private static FrameHsvTarget TargetForPendingForm(PendingFrameForm form) =>
        form switch
        {
            PendingFrameForm.Getter1 => new FrameHsvTarget(GetterOneRedKey, new Color(GetterOneRedKey), 0.025f, 0.85f, 1.0f),
            PendingFrameForm.Getter2 => new FrameHsvTarget(GetterTwoSilverKey, new Color(GetterTwoSilverKey), 0.0f, 0.08f, 1.22f),
            PendingFrameForm.Getter3 => new FrameHsvTarget(GetterThreeYellowKey, new Color(GetterThreeYellowKey), 0.14f, 1.35f, 1.12f),
            PendingFrameForm.ShinDragon => DefaultGetterRayTarget(),
            _ => DefaultGetterRayTarget(),
        };

    private static FrameHsvTarget DisabledTarget() =>
        new(DefaultDisabledKey, new Color(DefaultGetterRayKey), 0.455f, 1.05f, 1.16f, Enabled: false);

    private static Texture2D GetFrameTexture(CardModel model)
    {
        if (!IsDynamicTintEligible(model))
            return model.Frame;

        SharedDynamicFrameTexture ??= ResourceLoader.Load<Texture2D>(
            DynamicFrameTexturePath,
            null,
            ResourceLoader.CacheMode.Reuse);
        return SharedDynamicFrameTexture ?? model.Frame;
    }

    private static bool IsDynamicTintEligible(CardModel model)
    {
        if (model is not ShinGetterCardBase)
            return false;

        if (model.Rarity == CardRarity.Ancient)
            return false;

        return model.Type is CardType.Attack or CardType.Skill or CardType.Power;
    }

    private static void ApplyHsvToMaterial(ShaderMaterial material, Color borderColor, float h, float s, float v)
    {
        material.SetShaderParameter("h", h);
        material.SetShaderParameter("s", s);
        material.SetShaderParameter("v", v);
        material.SetShaderParameter("border_color", borderColor);
        material.SetShaderParameter("border_tint_strength", 0.78f);
    }

    private static void ResetTintState(NCard card)
    {
        FrameTintState state = TintStates.GetOrCreateValue(card);
        state.Tween?.Kill();
        state.Tween = null;
        state.ColorKey = string.Empty;
        state.CurrentBorderColor = new Color(DefaultGetterRayKey);
        state.CurrentH = 0.455f;
        state.CurrentS = 1.05f;
        state.CurrentV = 1.16f;
    }

    private static void RefreshCardNodes(Node node)
    {
        if (node is NHandCardHolder handHolder)
        {
            handHolder.UpdateCard();
            return;
        }

        if (node is NCard card)
            ApplyFrameTint(card, animate: true);

        foreach (Node child in node.GetChildren())
            RefreshCardNodes(child);
    }
}
