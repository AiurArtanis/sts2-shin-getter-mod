#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShinGetterMod.Config;
using ShinGetterMod.Services;

namespace ShinGetterMod.Nodes.Events;

/// <summary>Scene-owned transaction/input adapter. All normal visuals are native game UI.</summary>
internal sealed partial class NShinGetterBondDialogue : Control
{
    public NShinGetterBondDialogue() { }
    private static readonly MethodInfo AnimateLine = AccessTools.Method(typeof(NAncientEventLayout), "SetDialogueLineAndAnimate", new[] { typeof(int) });
    private static readonly AccessTools.FieldRef<NAncientEventLayout, Tween?> ContentTween = AccessTools.FieldRefAccess<NAncientEventLayout, Tween?>("_contentTween");
    private static readonly AccessTools.FieldRef<TheArchitect, Creature?> ArchitectCreature = AccessTools.FieldRefAccess<TheArchitect, Creature?>("_architectCreature");
    private static int _openCount;
    private bool _consoleGuard;
    internal static bool IsOpen => _openCount > 0;
    private ShinGetterBondSession _session = null!;
    private EventModel _model = null!;
    private NEventLayout _layout = null!;
    private NAncientEventLayout? _ancient;
    private Action? _returnToEvent;
    private Action? _fallbackToLegacy;
    private bool _useLegacyDialogue;
    private VBoxContainer _column = null!;
    private readonly List<NEventOptionButton> _buttons = new();
    private readonly List<MarginContainer> _buttonInsets = new();
    private readonly Dictionary<EventOption, Action> _optionActions = new();
    private readonly List<(NEventOptionButton Button, bool Visible, FocusModeEnum Focus)> _originalOptions = new();
    private NAncientDialogueHitbox? _hitbox;
    private Control? _nativeContent;
    private float _readingHeight;
    private NSpeechBubbleVfx? _speechBubble;
    private Control? _eventDescription;
    private bool _descriptionWasVisible;
    private string _dialogueLayoutKey = "";
    private int _renderGeneration;
    private bool _nativeRestored;
    private double _completionWait;
    private bool _completionPending;
    private bool _finishingDisplayedDialogue;
    private string? _finalAncientSfx;
    private AudioStreamPlayer? _voice;
    private double _voiceTimeout;
    private bool _closed;
    private bool _ready;
    private bool _fatal;
    private string _language = "";
    private int _lastCueLine = -1;
    internal bool IsUpdatingNativeUi { get; private set; }
    internal Control? DefaultFocusedControl => (Control?)_buttons.FirstOrDefault(b => b.IsEnabled && b.IsVisibleInTree())
        ?? (_hitbox is { IsEnabled: true } && _hitbox.IsVisibleInTree() ? _hitbox : null);
    private static bool IsEventActive => NEventRoom.Instance != null
        && ActiveScreenContext.Instance.IsCurrent(NEventRoom.Instance);

    internal static NShinGetterBondDialogue Create(ShinGetterBondSession session, EventModel model, NEventLayout layout, Action returnToEvent, string? finalAncientSfx, Action fallbackToLegacy) => new()
    {
        Name = "ShinGetterBondDialogue", _session = session, _model = model,
        _layout = layout, _returnToEvent = returnToEvent, _finalAncientSfx = finalAncientSfx,
        _fallbackToLegacy = fallbackToLegacy,
    };

    public override void _Ready()
    {
        if (_ready) return;
        _ready = true;
        _consoleGuard = true;
        _openCount++;
        try { BuildUi(); }
        catch (Exception ex) { ShowFatalError(ex); }
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _column = _layout.GetNode<VBoxContainer>("%OptionsContainer");
        // Keep rewards in-tree: no cancelled lifetime, recreated relic, or changed event selection.
        foreach (var button in _layout.OptionButtons)
        {
            _originalOptions.Add((button, button.Visible, button.FocusMode));
            button.Hide();
        }
        _ancient = _layout as NAncientEventLayout;
        if (_ancient != null)
        {
            _hitbox = _ancient.GetNode<NAncientDialogueHitbox>("%DialogueHitbox");
            _nativeContent = _ancient.GetNode<Control>("%ContentContainer");
            _readingHeight = _nativeContent.Size.Y;
        }
        else
        {
            _eventDescription = _layout.GetNodeOrNull<Control>("%EventDescription");
            _descriptionWasVisible = _eventDescription?.Visible == true;
            _eventDescription?.Hide();
        }
        Attempt(() => _session.Begin());
    }

    public override void _Process(double delta)
    {
        try { ProcessDialogue(delta); }
        catch (Exception ex) { ShowFatalError(ex); }
    }

    private void ProcessDialogue(double delta)
    {
        if (_closed || _fatal) return;
        if (_voice != null)
        {
            _voiceTimeout -= delta;
            if (_voiceTimeout <= 0 || !_voice.Playing || ShinGetterChunibyoConfigService.Current.VoiceMode == ShinGetterVoiceMode.Silent)
                StopVoice();
        }
        if (_language != LocManager.Instance.Language) Render();
        if (_completionPending && IsEventActive)
        {
            _completionWait -= delta;
            if (_completionWait <= 0 && _voice is not { Playing: true })
            {
                _completionPending = false;
                Attempt(CompleteFinalLine);
            }
        }
    }

    // Native OnRelease owns input; intercept only our exact scene-local options.
    internal bool TryChooseOption(EventOption option)
    {
        if (_closed || _fatal || !IsEventActive || !_optionActions.TryGetValue(option, out var action)) return false;
        var button = _buttons.FirstOrDefault(b => ReferenceEquals(b.Option, option));
        if (button == null || !GodotObject.IsInstanceValid(button) || !button.IsEnabled
            || !button.IsVisibleInTree() || !button.IsInsideTree() || button.IsQueuedForDeletion()) return false;
        try { action(); }
        catch (Exception ex) { ShowFatalError(ex); }
        return true;
    }

    internal void AdvanceFromNativeHitbox() { if (!_fatal) Next(); }

    private void Attempt(Func<bool> operation)
    {
        if (_closed) return;
        try
        {
            if (!operation())
            {
                ShowFatalError(new InvalidOperationException(_session.Error.Length == 0
                    ? "Conversation transaction could not advance." : _session.Error));
                return;
            }
            if (_session.Closed) { Close(); return; }
            Render();
        }
        catch (Exception ex) { ShowFatalError(ex); }
    }

    private void Render()
    {
        if (_fatal) return;
        try { RenderCore(); }
        catch (Exception ex) { ShowFatalError(ex); }
    }

    private void RenderCore()
    {
        if (!_ready || _closed) return;
        int generation = ++_renderGeneration;
        _completionPending = false;
        _language = LocManager.Instance.Language;
        ClearTemporaryOptions();
        var encounter = _session.Encounter;
        string[] lines;
        int current;
        if (encounter == null || encounter.DialogueId.Length == 0)
        {
            lines = new[] { ShinGetterDialogueCatalog.Ui("这一次，由谁来开口？", "Who will speak this time?", "今度は、誰が話す？") };
            current = 0;
            foreach (string id in _session.Choices)
            {
                string selected = id;
                AddButton(ShinGetterDialogueCatalog.Localize(id).Option, () => Attempt(() => _session.Select(selected)), dialogueOption: true);
            }
        }
        else
        {
            lines = _session.CurrentLines(_language);
            current = encounter.Line;
            if (_ancient == null && current < lines.Length - 1)
                AddButton(ShinGetterDialogueCatalog.Ui("继续", "Continue", "続ける"), Next);
        }
        if (_ancient != null) RenderAncient(lines, current, generation);
        else RenderArchitect(lines[current]);
        Callable.From(() =>
        {
            if (_closed || _fatal || generation != _renderGeneration || !IsInsideTree() || !IsVisibleInTree()) return;
            try
            {
                if (!_session.MarkDisplayed())
                {
                    ShowFatalError(new InvalidOperationException(_session.Error));
                    return;
                }
                ConfigureFocus();
                if (encounter is { DialogueId.Length: > 0 }) TryCue(encounter);
                if (_closed) return;
                if (encounter is { DialogueId.Length: > 0 } && current == lines.Length - 1)
                {
                    // Wait for the native line/scroll reveal and approved story cue,
                    // not another confirmation button. The final lines remain readable.
                    _completionWait = 1.0;
                    _completionPending = true;
                }
                if (IsEventActive) DefaultFocusedControl?.TryGrabFocus();
            }
            catch (Exception ex) { ShowFatalError(ex); }
        }).CallDeferred();
    }

    private void RenderAncient(string[] lines, int current, int generation)
    {
        string key = _language + "\n" + string.Join("\n", lines);
        IsUpdatingNativeUi = true;
        try
        {
            if (_dialogueLayoutKey != key)
            {
                _ancient!.ClearDialogue();
                _ancient.SetDialogue(lines.Select(text => new AncientDialogueLine("")
                {
                    LineText = NativeText(text), Speaker = Speaker(text),
                    NextButtonText = NativeText(ShinGetterDialogueCatalog.Ui("继续", "Continue", "続ける")),
                }).ToArray());
                _ancient.GetNode<VBoxContainer>("%DialogueContainer").ResetSize();
                _column.ResetSize();
                _ancient.GetNode<VBoxContainer>("%Content").ResetSize();
                _dialogueLayoutKey = key;
            }
        }
        finally { IsUpdatingNativeUi = false; }
        // Measure after container layout; SL reveals only the saved line, not line zero.
        Callable.From(() =>
        {
            if (_closed || _fatal || generation != _renderGeneration || !IsInsideTree() || !Valid(_ancient)) return;
            try
            {
                // Native last-line mode expands this viewport. A choice/error page
                // must not leave it expanded over the next story's Continue arrow.
                if (current < lines.Length - 1 && _nativeContent != null)
                    _nativeContent.Size = new Vector2(_nativeContent.Size.X, _readingHeight);
                RecenterNativeContent(_ancient!);
                AnimateLine.Invoke(_ancient, new object[] { current });
                ConfigureFocus();
            }
            catch (Exception ex) { ShowFatalError(ex); }
        }).CallDeferred();
    }

    private void RenderArchitect(string text)
    {
        StopSpeechBubble();
        var owner = _model.Owner ?? throw new InvalidOperationException("Native dialogue owner is unavailable.");
        Creature? speaker = Speaker(text) == AncientDialogueSpeaker.Ancient && _model is TheArchitect architect
            ? ArchitectCreature(architect) : owner.Creature;
        if (speaker == null) throw new InvalidOperationException("Native Architect speaker is unavailable.");
        _speechBubble = TalkCmd.Play(NativeText(text), speaker,
            Speaker(text) == AncientDialogueSpeaker.Ancient ? VfxColor.DarkGray : owner.Character.SpeechBubbleColor,
            VfxDuration.Forever);
        if (_speechBubble == null) throw new InvalidOperationException("Native speech bubble could not be displayed.");
    }

    private static AncientDialogueSpeaker Speaker(string text) =>
        text.StartsWith("[red]", StringComparison.Ordinal) || text.StartsWith("[white]", StringComparison.Ordinal)
        || text.StartsWith("[yellow]", StringComparison.Ordinal) ? AncientDialogueSpeaker.Character : AncientDialogueSpeaker.Ancient;

    private static LocString NativeText(string text)
    {
        var result = new LocString("ancients", "SHIN_GETTER_BOND_UI_TEXT");
        result.Add("text", text);
        return result;
    }

    private bool CompleteFinalLine()
    {
        if (!_session.CompleteDisplayedFinalLine()) return false;
        _finishingDisplayedDialogue = true;
        return true;
    }

    internal static void RestoreRewardPage(NAncientEventLayout layout, ShinGetterBondSession session)
    {
        // Only a completed dialogue has a retained tail. Skips/errors never grant
        // completion or replay a voice. Keep the original reward button instances.
        if (session.Encounter is { Completed: true, DialogueId.Length: > 0 })
        {
            try
            {
                string[] tail = session.CurrentLines(LocManager.Instance.Language).TakeLast(2).ToArray();
                layout.ClearDialogue();
                layout.SetDialogue(tail.Select(text => new AncientDialogueLine("")
                {
                    LineText = NativeText(text), Speaker = Speaker(text),
                }).ToArray());
                foreach (var line in layout.GetNode<VBoxContainer>("%DialogueContainer").GetChildren().OfType<NAncientDialogueLine>())
                    line.SetSpeakerIconVisible();
                layout.GetNode<VBoxContainer>("%DialogueContainer").ResetSize();
            }
            catch (Exception ex)
            {
                GD.PushWarning("Completed dialogue tail unavailable: " + ex.Message);
                layout.ClearDialogue(); // A damaged old tail must never block legal rewards.
            }
        }
        layout.GetNode<VBoxContainer>("%OptionsContainer").ResetSize();
        layout.GetNode<VBoxContainer>("%Content").ResetSize();
        var options = layout.OptionButtons.ToArray();
        Callable.From(() =>
        {
            if (!Valid(layout) || !options.SequenceEqual(layout.OptionButtons)) return;
            RecenterNativeContent(layout);
            int last = layout.GetNode<VBoxContainer>("%DialogueContainer").GetChildCount() - 1;
            AnimateLine.Invoke(layout, new object[] { Math.Max(0, last) });
        }).CallDeferred();
    }

    private static void RecenterNativeContent(NAncientEventLayout layout)
    {
        ShinGetterContentTweenGuard.Stop(ref ContentTween(layout));
        var viewport = layout.GetNode<Control>("%ContentContainer");
        var content = layout.GetNode<VBoxContainer>("%Content");
        // ResetSize grows minimum widths without recentering old offsets. Correct
        // the clipping viewport AND the content, including after rewards return.
        float width = Math.Max(1160, content.Size.X + 64);
        viewport.OffsetLeft = -width / 2;
        viewport.OffsetRight = width / 2;
        content.Position = new Vector2((viewport.Size.X - content.Size.X) / 2, content.Position.Y);
    }

    private void AddButton(string text, Action action, bool dialogueOption = false)
    {
        // Native nonempty titles are wrapped in gold/bold. Dialogue choices are
        // body text instead: only the existing speaker name tags supply color.
        var option = new EventOption(_model, () => Task.CompletedTask,
            NativeText(dialogueOption ? "" : text), NativeText(dialogueOption ? text : ""),
            "SHIN_GETTER_BOND_LOCAL", Array.Empty<IHoverTip>()).ThatWontSaveToChoiceHistory();
        var button = NEventOptionButton.Create(_model, option, _buttons.Count);
        // Preserve native hover/press visuals, reserving space for the outline
        // outside the button rect. Never shrink/clip the outline to hide overflow.
        var inset = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore, FocusMode = FocusModeEnum.None };
        inset.AddThemeConstantOverride("margin_left", 32);
        inset.AddThemeConstantOverride("margin_right", 32);
        inset.AddThemeConstantOverride("margin_top", 24);
        inset.AddThemeConstantOverride("margin_bottom", 24);
        _column.AddChild(inset);
        _buttonInsets.Add(inset);
        inset.AddChild(button);
        _buttons.Add(button);
        _optionActions.Add(option, action);
        button.FocusMode = FocusModeEnum.All;
        button.Modulate = Colors.White;
        button.Enable();
        button.EnableButton();
    }

    private void ConfigureFocus()
    {
        var controls = _buttons.Where(b => Valid(b) && b.IsVisibleInTree()).Cast<Control>().ToList();
        if (Valid(_hitbox) && _hitbox!.IsEnabled && _hitbox.IsVisibleInTree()) controls.Insert(0, _hitbox);
        for (int i = 0; i < controls.Count; i++)
        {
            var control = controls[i];
            control.FocusMode = FocusModeEnum.All;
            control.FocusNeighborTop = controls[(i + controls.Count - 1) % controls.Count].GetPath();
            control.FocusNeighborBottom = controls[(i + 1) % controls.Count].GetPath();
            control.FocusPrevious = control.FocusNeighborTop;
            control.FocusNext = control.FocusNeighborBottom;
            control.FocusNeighborLeft = control.FocusNeighborTop;
            control.FocusNeighborRight = control.FocusNeighborBottom;
        }
    }

    private static bool Valid(Node? node) => node != null && GodotObject.IsInstanceValid(node)
        && node.IsInsideTree() && !node.IsQueuedForDeletion();

    private void ClearTemporaryOptions()
    {
        _optionActions.Clear();
        foreach (var button in _buttons)
        {
            if (!GodotObject.IsInstanceValid(button)) continue;
            button.Disable();
        }
        foreach (var inset in _buttonInsets)
        {
            if (!GodotObject.IsInstanceValid(inset)) continue;
            inset.GetParent()?.RemoveChild(inset);
            inset.QueueFree(); // Owns its native button and all of that button's signals/tweens.
        }
        _buttonInsets.Clear();
        _buttons.Clear();
    }

    private void ClearNativeDialogue()
    {
        StopSpeechBubble();
        if (!Valid(_ancient)) return;
        ShinGetterContentTweenGuard.Stop(ref ContentTween(_ancient!));
        IsUpdatingNativeUi = true;
        try { _ancient!.ClearDialogue(); }
        finally { IsUpdatingNativeUi = false; }
        _hitbox?.Disable();
        _hitbox?.Hide();
        _ancient!.GetNode<Control>("%FakeNextButtonContainer").Hide();
    }

    private void StopSpeechBubble()
    {
        if (_speechBubble != null && GodotObject.IsInstanceValid(_speechBubble)) _speechBubble.QueueFree();
        _speechBubble = null;
    }

    private void RestoreOptionsVisibility()
    {
        foreach (var snapshot in _originalOptions)
        {
            try
            {
                if (Valid(snapshot.Button) && snapshot.Button.GetParent() == _column)
                {
                    snapshot.Button.Visible = snapshot.Visible;
                    snapshot.Button.FocusMode = snapshot.Focus;
                }
            }
            catch (Exception ex) { GD.PushWarning("Dialogue original option restore: " + ex); }
        }
        _originalOptions.Clear();
    }

    private void RestoreNativeUi()
    {
        if (_nativeRestored) return;
        _nativeRestored = true;
        ++_renderGeneration;
        // A broken story node must not prevent restoring the still-valid native
        // options, nor prevent the continuation from handing input back.
        try { ClearTemporaryOptions(); }
        catch (Exception ex) { GD.PushWarning("Dialogue option cleanup: " + ex); }
        try { ClearNativeDialogue(); }
        catch (Exception ex) { GD.PushWarning("Dialogue bubble cleanup: " + ex); }
        RestoreOptionsVisibility();
        if (Valid(_eventDescription)) _eventDescription!.Visible = _descriptionWasVisible;
    }

    private void ShowFatalError(Exception ex)
    {
        if (_closed || _fatal) return;
        GD.PushWarning("Shin Getter conversation unavailable; restoring legacy dialogue: " + ex);
        _fatal = true;
        _useLegacyDialogue = true;
        _finishingDisplayedDialogue = false;
        // No Skip/Commit: preserve the last successful progress revision, including
        // unreadable or write-protected sidecars. Recovery never marks completion.
        Close();
    }

    private void Next()
    {
        if (!IsEventActive || _voice is { Playing: true }) return;
        Attempt(() => _session.Advance());
    }

    private void TryCue(ShinGetterBondEncounter encounter)
    {
        // Normal dialogue plus approved 010/035 voices only; no action choreography.
        string? filename = (encounter.DialogueId, encounter.Line) switch
        {
            ("TANX_RYOMA_BOND_03", 4) => "ryoma_getter_tomahawk.wav",
            ("TANX_BENKEI_BOND_02", 3) => "musashi_avalanche.wav", // Existing 035 filename; performer is Benkei.
            _ => null,
        };
        if (filename == null || _lastCueLine == encounter.Line) return;
        _lastCueLine = encounter.Line;
        if (encounter.ConsumedCues.Contains(encounter.Line)) return;
        if (!_session.ConsumeCue(encounter.Line))
        {
            ShowFatalError(new InvalidOperationException(_session.Error));
            return;
        }
        ShinGetterChunibyoConfigService.Load();
        if (ShinGetterChunibyoConfigService.Current.VoiceMode == ShinGetterVoiceMode.Silent) return;
        try
        {
            string path = "res://audio/sfx/characters/shin_getter/voices/" + filename;
            if (!ResourceLoader.Exists(path)) return;
            AudioStream? stream = ResourceLoader.Load<AudioStream>(path);
            if (stream == null) return;
            StopVoice();
            _voice = new AudioStreamPlayer { Stream = stream, Bus = "SFX" };
            AddChild(_voice);
            _voice.Play();
            _voiceTimeout = Math.Clamp(stream.GetLength() + 0.5, 0.5, 12);
        }
        catch (Exception ex) { GD.PushWarning("Story voice unavailable: " + ex.Message); StopVoice(); }
    }

    private void StopVoice()
    {
        if (_voice != null && GodotObject.IsInstanceValid(_voice)) { _voice.Stop(); _voice.QueueFree(); }
        _voice = null;
    }

    private void Close()
    {
        if (_closed) return;
        _closed = true;
        _completionPending = false;
        ReleaseConsoleGuard();
        try { StopVoice(); }
        catch (Exception ex) { GD.PushWarning("Dialogue voice cleanup: " + ex); }
        if (_finishingDisplayedDialogue && !string.IsNullOrEmpty(_finalAncientSfx))
        {
            try { SfxCmd.Play(_finalAncientSfx); }
            catch (Exception ex) { GD.PushWarning("Ancient closing voice unavailable: " + ex.Message); }
        }
        _finalAncientSfx = null;
        try { RestoreNativeUi(); }
        catch (Exception ex) { GD.PushWarning("Dialogue UI cleanup: " + ex); }
        Hide();
        var continuation = _useLegacyDialogue ? _fallbackToLegacy : _returnToEvent;
        _returnToEvent = null;
        _fallbackToLegacy = null;
        try { continuation?.Invoke(); }
        finally { QueueFree(); }
    }

    public override void _ExitTree()
    {
        _closed = true;
        ReleaseConsoleGuard();
        try { StopVoice(); }
        catch (Exception ex) { GD.PushWarning("Dialogue exit voice cleanup: " + ex); }
        try { RestoreNativeUi(); }
        catch (Exception ex) { GD.PushWarning("Dialogue exit UI cleanup: " + ex); }
        _returnToEvent = null;
        _fallbackToLegacy = null;
        base._ExitTree();
    }

    private void ReleaseConsoleGuard()
    {
        if (!_consoleGuard) return;
        _consoleGuard = false;
        _openCount--;
    }
}
