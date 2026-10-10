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
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Nodes.Events;
using ShinGetterMod.Services;

namespace ShinGetterMod.Patches;

internal static class ShinGetterBondDialogueBridge
{
    internal sealed class State
    {
        internal readonly ShinGetterBondSession Session;
        internal WeakReference<NShinGetterBondDialogue>? Ui;
        internal bool Returned;
        internal State(EventModel model) { Session = new(model); }
    }
    private static readonly ConditionalWeakTable<EventModel, State> States = new();
    internal static readonly AccessTools.FieldRef<NEventLayout, EventModel> LayoutEvent = AccessTools.FieldRefAccess<NEventLayout, EventModel>("_event");
    internal static readonly AccessTools.FieldRef<NEventRoom, EventModel> RoomEvent = AccessTools.FieldRefAccess<NEventRoom, EventModel>("_event");
    internal static State Get(EventModel model) => States.GetValue(model, m => new State(m));
    internal static bool TryGet(EventModel model, out State state) => States.TryGetValue(model, out state!);
    internal static bool IsBlocking(EventModel model) => TryGet(model, out var state) && !state.Returned;

    internal static void Attach(EventModel model, NEventLayout layout, Action resume)
    {
        State state = Get(model);
        if (state.Returned) { resume(); return; }
        if (state.Ui != null && state.Ui.TryGetTarget(out var existing) && GodotObject.IsInstanceValid(existing) && existing.IsInsideTree()) return;
        // Own only the enabled states suspended by this dialogue. EnableButton in
        // official109 restores MouseFilter only, not NClickableControl.IsEnabled.
        var enabledOptions = layout.OptionButtons.Where(button => button.IsEnabled).ToArray();
        layout.DisableEventOptions();
        var ui = NShinGetterBondDialogue.Create(state.Session, model, layout, () =>
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
            resume();
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(layout) && layout.IsInsideTree()) layout.DefaultFocusedControl?.TryGrabFocus();
            }).CallDeferred();
        });
        state.Ui = new(ui);
        layout.AddChild(ui);
    }
}

// The old localization/dialogue patches remain strictly the non-bond fallback (multiplayer,
// daily/custom and non-interactive runs). New story text never enters their random pools.
[HarmonyPatch(typeof(NAncientEventLayout), nameof(NAncientEventLayout.SetDialogue))]
internal static class ShinGetterBondReplaceAncientLinesPatch
{
    private static bool Prefix(NAncientEventLayout __instance)
    {
        EventModel model = ShinGetterBondDialogueBridge.LayoutEvent(__instance);
        if (!ShinGetterBondSession.IsEligible(model)) return true;
        if (ShinGetterBondDialogueBridge.TryGet(model, out var state) && state.Ui != null
            && state.Ui.TryGetTarget(out var ui) && ui.IsUpdatingNativeUi) return true;
        ShinGetterBondDialogueBridge.Get(model);
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
        if (!ShinGetterBondDialogueBridge.TryGet(model, out var state) || state.Returned) return true;
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
        if (!ShinGetterBondSession.IsEligible(__instance)) return true;
        ShinGetterBondDialogueBridge.Get(__instance);
        // The native ending consumes EndAttackers once. No new damage/score consumer.
        Dialogue(__instance) = new AncientDialogue("") { EndAttackers = ArchitectAttackers.Player };
        return false;
    }
}

[HarmonyPatch(typeof(TheArchitect), "PlayCurrentLine")]
internal static class ShinGetterBondArchitectPlayPatch
{
    private static readonly System.Reflection.MethodInfo Proceed = AccessTools.Method(typeof(TheArchitect), "CreateProceedOption");
    private static readonly System.Reflection.MethodInfo SetState = AccessTools.Method(typeof(EventModel), "SetEventState", new[] { typeof(LocString), typeof(IEnumerable<EventOption>) });
    private static bool Prefix(TheArchitect __instance, ref Task __result)
    {
        if (!ShinGetterBondDialogueBridge.TryGet(__instance, out var state)) return true;
        __result = Task.CompletedTask;
        if (state.Returned) return false;
        var model = __instance;
        if (model.Node is NEventLayout layout)
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(layout) || !layout.IsInsideTree()) return;
                ShinGetterBondDialogueBridge.Attach(model, layout, () =>
                {
                    var option = (EventOption)Proceed.Invoke(model, null)!;
                    SetState.Invoke(model, new object[] { new LocString("ancients", "PROCEED.description"), new[] { option } });
                });
            }).CallDeferred();
        return false;
    }
}
