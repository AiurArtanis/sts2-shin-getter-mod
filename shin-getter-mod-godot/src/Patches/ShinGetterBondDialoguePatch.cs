#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves;
using ShinGetterMod.Nodes.Events;
using ShinGetterMod.Services;

namespace ShinGetterMod.Patches;

internal static class ShinGetterBondDialogueBridge
{
    internal sealed class State
    {
        private readonly EventModel _model;
        private ShinGetterBondSession? _session;
        // LoadDialogue runs before the room/UI is ready. Resolve identity only
        // when attaching after native SetOptions/OnSetupComplete.
        internal ShinGetterBondSession Session => _session ??= new(_model);
        internal WeakReference<NShinGetterBondDialogue>? Ui;
        internal bool Returned;
        internal bool LegacyFallback;
        internal AncientDialogueLine[] LegacyLines = Array.Empty<AncientDialogueLine>();
        internal string? FinalAncientSfx;
        internal State(EventModel model) { _model = model; }
    }
    private static readonly ConditionalWeakTable<EventModel, State> States = new();
    internal static readonly AccessTools.FieldRef<NEventLayout, EventModel> LayoutEvent = AccessTools.FieldRefAccess<NEventLayout, EventModel>("_event");
    internal static readonly AccessTools.FieldRef<NEventRoom, EventModel> RoomEvent = AccessTools.FieldRefAccess<NEventRoom, EventModel>("_event");
    internal static State Get(EventModel model) => States.GetValue(model, m => new State(m));
    internal static bool TryGet(EventModel model, out State state) => States.TryGetValue(model, out state!);
    internal static bool IsBlocking(EventModel model) => TryGet(model, out var state) && !state.Returned;
    internal static bool IsEligible(EventModel model)
    {
        try { return ShinGetterBondSession.IsEligible(model); }
        catch (Exception ex)
        {
            // A broken catalogue/config must leave the existing dialogue patches
            // and native flow available, including before a session can be made.
            GD.PushWarning("Shin Getter bond eligibility unavailable; using legacy dialogue: " + ex);
            return false;
        }
    }

    internal static string? ResolveFinalAncientSfx(EventModel model)
    {
        // Reuse declared native sounds, without a new NPC path table or RNG draw.
        return model is AncientEventModel ancient ? ancient.DialogueSet.GetAllDialogues()
            .SelectMany(dialogue => dialogue.Lines).Select(line => line.GetSfxOrFallbackPath())
            .FirstOrDefault(path => path.StartsWith("event:/sfx/npcs/", StringComparison.Ordinal)) : null;
    }

    internal static void Attach(EventModel model, NEventLayout layout, Action resume)
    {
        State state = Get(model);
        if (state.Returned) { resume(); return; }
        if (state.Ui != null && state.Ui.TryGetTarget(out var existing) && GodotObject.IsInstanceValid(existing) && existing.IsInsideTree()) return;
        // Own only the enabled states suspended by this dialogue. EnableButton in
        // official109 restores MouseFilter only, not NClickableControl.IsEnabled.
        var enabledOptions = layout.OptionButtons.Where(button => button.IsEnabled).ToArray();
        layout.DisableEventOptions();
        void Return()
        {
            if (state.Returned || !GodotObject.IsInstanceValid(layout) || !layout.IsInsideTree()) return;
            if (layout.IsQueuedForDeletion()) return;
            state.Returned = true;
            state.Ui = null;
            foreach (var button in enabledOptions)
            {
                if (GodotObject.IsInstanceValid(button) && button.IsInsideTree() && !button.IsQueuedForDeletion()
                    && layout.OptionButtons.Contains(button))
                    button.Enable();
            }
            // Restore before native continuation: its lock/vote/choice state wins.
            if (state.LegacyFallback && layout is NAncientEventLayout legacy)
            {
                try
                {
                    var lines = state.LegacyLines;
                    if (lines.Length == 0 && model is AncientEventModel original)
                    {
                        var owner = model.Owner ?? throw new InvalidOperationException("Legacy dialogue owner unavailable.");
                        var stats = SaveManager.Instance.Progress.GetStatsForAncient(model.Id);
                        var dialogue = Rng.Chaotic.NextItem(original.DialogueSet.GetValidDialogues(owner.Character.Id,
                            stats?.GetVisitsAs(owner.Character.Id) ?? 0, stats?.TotalVisits ?? 0,
                            !original.AnyCharacterDialogueBlacklist.Contains(owner.Character)))
                            ?? throw new InvalidOperationException("Legacy dialogue pool is empty.");
                        lines = dialogue.Lines.ToArray();
                    }
                    legacy.ClearDialogue();
                    legacy.SetDialogue(lines);
                }
                catch (Exception ex)
                {
                    GD.PushWarning("Legacy ancient dialogue unavailable; continuing original event: " + ex);
                    legacy.ClearDialogue();
                }
            }
            resume();
            if (!state.LegacyFallback && layout is NAncientEventLayout ancient) NShinGetterBondDialogue.RestoreRewardPage(ancient, state.Session);
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(layout) && layout.IsInsideTree()) layout.DefaultFocusedControl?.TryGrabFocus();
            }).CallDeferred();
        }
        void Fallback()
        {
            state.LegacyFallback = true;
            Return();
        }
        try
        {
            state.FinalAncientSfx ??= ResolveFinalAncientSfx(model);
            var ui = NShinGetterBondDialogue.Create(state.Session, model, layout, Return, state.FinalAncientSfx, Fallback);
            state.Ui = new(ui);
            layout.AddChild(ui);
        }
        catch (Exception ex)
        {
            GD.PushWarning("Shin Getter bond attachment failed; restoring legacy dialogue: " + ex);
            Fallback();
        }
    }
}

// The old localization/dialogue patches remain strictly the non-bond fallback (multiplayer,
// daily/custom and non-interactive runs). New story text never enters their random pools.
[HarmonyPatch(typeof(NAncientEventLayout), nameof(NAncientEventLayout.SetDialogue))]
internal static class ShinGetterBondReplaceAncientLinesPatch
{
    private static bool Prefix(NAncientEventLayout __instance, IReadOnlyList<AncientDialogueLine> lines)
    {
        EventModel model = ShinGetterBondDialogueBridge.LayoutEvent(__instance);
        if (!ShinGetterBondDialogueBridge.IsEligible(model)) return true;
        if (ShinGetterBondDialogueBridge.TryGet(model, out var returned) && returned.Returned) return true;
        if (ShinGetterBondDialogueBridge.TryGet(model, out var state) && state.Ui != null
            && state.Ui.TryGetTarget(out var ui) && ui.IsUpdatingNativeUi) return true;
        ShinGetterBondDialogueBridge.Get(model).LegacyLines = lines.ToArray();
        __instance.ClearDialogue();
        return false;
    }
}

[HarmonyPatch(typeof(NAncientEventLayout), nameof(NAncientEventLayout.OnSetupComplete))]
internal static class ShinGetterBondAncientSetupPatch
{
    private static bool Prefix(NAncientEventLayout __instance)
    {
        EventModel model = ShinGetterBondDialogueBridge.LayoutEvent(__instance);
        // SetupLayout omits SetDialogue on resumed/pre-finished rooms. Do not use
        // that optional call as the only feature initialization/attachment gate.
        if (!ShinGetterBondDialogueBridge.IsEligible(model) || model.IsFinished) return true;
        ShinGetterBondDialogueBridge.State state = ShinGetterBondDialogueBridge.Get(model);
        if (state.Returned) return true;
        ShinGetterBondDialogueBridge.Attach(model, __instance, () => __instance.OnSetupComplete());
        return false;
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.OptionButtonClicked))]
internal static class ShinGetterBondProtectRewardsPatch
{
    private static bool Prefix(NEventRoom __instance, EventOption option)
    {
        EventModel model = ShinGetterBondDialogueBridge.RoomEvent(__instance);
        if (!ShinGetterBondDialogueBridge.IsBlocking(model)) return true;
        if (ShinGetterBondDialogueBridge.TryGet(model, out var state) && state.Ui != null
            && state.Ui.TryGetTarget(out var ui) && GodotObject.IsInstanceValid(ui))
            ui.TryChooseOption(option);
        // Always block native reward/network routing while the story owns input,
        // including when a local choice finishes the conversation in this call.
        return false;
    }
}

[HarmonyPatch(typeof(NAncientEventLayout), "OnDialogueHitboxClicked")]
internal static class ShinGetterBondNativeAdvancePatch
{
    private static bool Prefix(NAncientEventLayout __instance)
    {
        EventModel model = ShinGetterBondDialogueBridge.LayoutEvent(__instance);
        if (!ShinGetterBondDialogueBridge.IsBlocking(model)) return true;
        if (ShinGetterBondDialogueBridge.TryGet(model, out var state) && state.Ui != null
            && state.Ui.TryGetTarget(out var ui) && GodotObject.IsInstanceValid(ui))
            ui.AdvanceFromNativeHitbox();
        // Only the session can advance after its transactional save succeeds.
        return false;
    }
}

[HarmonyPatch(typeof(NAncientEventLayout), nameof(NAncientEventLayout.DefaultFocusedControl), MethodType.Getter)]
internal static class ShinGetterBondNativeFocusPatch
{
    private static bool Prefix(NAncientEventLayout __instance, ref Control? __result)
    {
        EventModel model = ShinGetterBondDialogueBridge.LayoutEvent(__instance);
        if (!ShinGetterBondDialogueBridge.TryGet(model, out var state) || state.Returned) return true;
        __result = state.Ui != null && state.Ui.TryGetTarget(out var ui) && GodotObject.IsInstanceValid(ui)
            ? ui.DefaultFocusedControl : null;
        return false;
    }
}

// Native hover callbacks index the current dialogue child. During replacement or
// cleanup the container is intentionally empty; do not inspect removed children.
[HarmonyPatch]
internal static class ShinGetterBondNativeLineFocusPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NAncientEventLayout), "OnDialogueLineFocused");
        yield return AccessTools.Method(typeof(NAncientEventLayout), "OnDialogueLineUnfocused");
    }
    private static bool Prefix(NAncientEventLayout __instance)
    {
        EventModel model = ShinGetterBondDialogueBridge.LayoutEvent(__instance);
        if (!ShinGetterBondDialogueBridge.TryGet(model, out var state) || state.Returned) return true;
        return state.Ui == null || !state.Ui.TryGetTarget(out var ui) || !ui.IsUpdatingNativeUi;
    }
}

[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.DefaultFocusedControl), MethodType.Getter)]
internal static class ShinGetterBondFocusPatch
{
    private static bool Prefix(NEventRoom __instance, ref Control? __result)
    {
        EventModel model = ShinGetterBondDialogueBridge.RoomEvent(__instance);
        if (!ShinGetterBondDialogueBridge.TryGet(model, out var state) || state.Returned) return true;
        __result = state.Ui != null && state.Ui.TryGetTarget(out var ui) && GodotObject.IsInstanceValid(ui)
            ? ui.DefaultFocusedControl : null;
        return false;
    }
}

[HarmonyPatch(typeof(TheArchitect), "LoadDialogue")]
internal static class ShinGetterBondArchitectLoadPatch
{
    private static readonly AccessTools.FieldRef<TheArchitect, AncientDialogue?> Dialogue = AccessTools.FieldRefAccess<TheArchitect, AncientDialogue?>("_dialogue");
    private static bool Prefix(TheArchitect __instance)
    {
        if (!ShinGetterBondDialogueBridge.IsEligible(__instance)) return true;
        if (ShinGetterBondDialogueBridge.TryGet(__instance, out var state) && state.Returned) return true;
        ShinGetterBondDialogueBridge.Get(__instance);
        // The native ending consumes EndAttackers once. No new damage/score consumer.
        Dialogue(__instance) = new AncientDialogue("") { EndAttackers = ArchitectAttackers.Player };
        return false;
    }
}

[HarmonyPatch(typeof(TheArchitect), "PlayCurrentLine")]
internal static class ShinGetterBondArchitectPlayPatch
{
    private static bool Prefix(TheArchitect __instance, ref Task __result)
    {
        if (!ShinGetterBondDialogueBridge.TryGet(__instance, out var state) || state.LegacyFallback) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

// SetupLayout calls SetOptions after OnRoomEnter/PlayCurrentLine. Attaching in
// PlayCurrentLine lets that later SetOptions delete every temporary Next button.
[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.OnSetupComplete))]
internal static class ShinGetterBondArchitectSetupPatch
{
    private static readonly System.Reflection.MethodInfo Proceed = AccessTools.Method(typeof(TheArchitect), "CreateProceedOption");
    private static readonly System.Reflection.MethodInfo SetState = AccessTools.Method(typeof(EventModel), "SetEventState", new[] { typeof(LocString), typeof(IEnumerable<EventOption>) });
    private static readonly System.Reflection.MethodInfo LoadDialogue = AccessTools.Method(typeof(TheArchitect), "LoadDialogue");
    private static readonly System.Reflection.MethodInfo PlayCurrentLine = AccessTools.Method(typeof(TheArchitect), "PlayCurrentLine");
    private static readonly AccessTools.FieldRef<TheArchitect, int> CurrentLine = AccessTools.FieldRefAccess<TheArchitect, int>("_currentLineIndex");
    private static readonly AccessTools.FieldRef<TheArchitect, AncientDialogue?> Dialogue = AccessTools.FieldRefAccess<TheArchitect, AncientDialogue?>("_dialogue");
    private static void RestoreProceed(TheArchitect model)
    {
        var option = (EventOption)Proceed.Invoke(model, null)!;
        SetState.Invoke(model, new object[] { new LocString("ancients", "PROCEED.description"), new[] { option } });
    }

    private static async Task RestoreLegacy(TheArchitect model)
    {
        try
        {
            // Match native OnRoomEnter: no clickable old Proceed while the first
            // legacy line is waiting for its intro animation to finish.
            SetState.Invoke(model, new object[] { new LocString("ancients", "PROCEED.description"), Array.Empty<EventOption>() });
            LoadDialogue.Invoke(model, null); // Existing pre-bond character pool.
            CurrentLine(model) = 0;
            if (Dialogue(model) is not { Lines.Count: > 0 })
            {
                RestoreProceed(model);
                return;
            }
            await (Task)PlayCurrentLine.Invoke(model, null)!;
            // Native PlayCurrentLine can return successfully without setting an
            // option when its text or speaker is unavailable. Do not leave the
            // empty intro state behind in that case.
            if (model.CurrentOptions.Count == 0) RestoreProceed(model);
        }
        catch (Exception ex)
        {
            GD.PushWarning("Legacy Architect dialogue unavailable; restoring native proceed: " + ex);
            Dialogue(model) = new AncientDialogue("") { EndAttackers = ArchitectAttackers.Player };
            RestoreProceed(model);
        }
    }
    private static void Postfix(NEventLayout __instance)
    {
        if (ShinGetterBondDialogueBridge.LayoutEvent(__instance) is not TheArchitect model
            || !ShinGetterBondDialogueBridge.TryGet(model, out var state) || state.Returned) return;
        ShinGetterBondDialogueBridge.Attach(model, __instance, () =>
        {
            if (state.LegacyFallback)
            {
                TaskHelper.RunSafely(RestoreLegacy(model));
                return;
            }
            RestoreProceed(model);
        });
    }
}
