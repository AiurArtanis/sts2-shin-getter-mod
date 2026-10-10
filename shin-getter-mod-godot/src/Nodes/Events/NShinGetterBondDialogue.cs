#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
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
    private VBoxContainer _column = null!;
    private readonly List<NEventOptionButton> _buttons = new();
    private readonly Dictionary<EventOption, Action> _optionActions = new();
    private readonly List<(NEventOptionButton Button, bool Visible, FocusModeEnum Focus)> _originalOptions = new();
    private NAncientDialogueHitbox? _hitbox;
    private Control? _nativeContent;
    private float _readingHeight;
    private NBackButton? _skipButton;
    private Callable _skipReleased;
    private NSpeechBubbleVfx? _speechBubble;
    private Control? _eventDescription;
    private bool _descriptionWasVisible;
    private string _dialogueLayoutKey = "";
    private int _renderGeneration;
    private bool _nativeRestored;
    private Action? _retry;
    private AudioStreamPlayer? _voice;
    private double _voiceTimeout;
    private bool _closed;
    private bool _ready;
    private bool _fatal;
    private string _language = "";
    private int _lastCueLine = -1;
    private Control? _emergencyFocus;
    internal bool IsUpdatingNativeUi { get; private set; }
    internal Control? DefaultFocusedControl => _emergencyFocus
        ?? (Control?)_buttons.FirstOrDefault(b => b.IsEnabled && b.IsVisibleInTree())
        ?? (_hitbox is { IsEnabled: true } && _hitbox.IsVisibleInTree() ? _hitbox : _skipButton);
    private static bool IsEventActive => NEventRoom.Instance != null
        && ActiveScreenContext.Instance.IsCurrent(NEventRoom.Instance);

    internal static NShinGetterBondDialogue Create(ShinGetterBondSession session, EventModel model, NEventLayout layout, Action returnToEvent) => new()
    {
        Name = "ShinGetterBondDialogue", _session = session, _model = model,
        _layout = layout, _returnToEvent = returnToEvent,
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
        _skipButton = PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("ui/back_button")).Instantiate<NBackButton>();
        _skipButton.Name = "SkipBondConversation";
        _skipButton.FocusMode = FocusModeEnum.All; // native ConnectSignals captures navigability in _Ready
        _skipReleased = Callable.From<NClickableControl>(_ => { if (!_closed && IsEventActive) Skip(); });
        _skipButton.Connect(NClickableControl.SignalName.Released, _skipReleased);
        AddChild(_skipButton);
        _skipButton.FocusMode = FocusModeEnum.All;
        _skipButton.Enable();
        Attempt(() => _session.Begin());
    }

    public override void _Process(double delta)
    {
        if (_closed || _fatal) return;
        if (_voice != null)
        {
            _voiceTimeout -= delta;
            if (_voiceTimeout <= 0 || !_voice.Playing || ShinGetterChunibyoConfigService.Current.VoiceMode == ShinGetterVoiceMode.Silent)
                StopVoice();
        }
        if (_language != LocManager.Instance.Language) Render();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!_closed && IsEventActive && input.IsActionPressed("ui_cancel"))
        {
            Skip();
            GetViewport().SetInputAsHandled();
        }
    }

    // Native OnRelease owns input; intercept only our exact scene-local options.
    internal bool TryChooseOption(EventOption option)
    {
        if (_closed || _fatal || !IsEventActive || !_optionActions.TryGetValue(option, out var action)) return false;
        var button = _buttons.FirstOrDefault(b => ReferenceEquals(b.Option, option));
        if (button == null || !GodotObject.IsInstanceValid(button) || !button.IsEnabled
            || !button.IsVisibleInTree() || !button.IsInsideTree() || button.IsQueuedForDeletion()) return false;
        action();
        return true;
    }

    internal void AdvanceFromNativeHitbox() { if (!_fatal && _retry == null) Next(); }

    private void Attempt(Func<bool> operation)
    {
        if (_closed) return;
        if (operation())
        {
            _retry = null;
            if (_session.Closed) { Close(); return; }
            Render();
        }
        else { _retry = () => Attempt(operation); Render(); }
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
        _language = LocManager.Instance.Language;
        ClearTemporaryOptions();
        if (_skipButton != null)
            _skipButton.TooltipText = ShinGetterDialogueCatalog.Ui("跳过本次交谈", "Skip this conversation", "今回の会話をスキップ");
        var encounter = _session.Encounter;
        string[] lines;
        int current;
        if (encounter == null || encounter.DialogueId.Length == 0)
        {
            lines = new[] { ShinGetterDialogueCatalog.Ui("这一次，由谁来开口？", "Who will speak this time?", "今度は、誰が話す？") };
            current = 0;
            if (_retry == null)
                foreach (string id in _session.Choices)
                {
                    string selected = id;
                    AddButton(ShinGetterDialogueCatalog.Localize(id).Option, () => Attempt(() => _session.Select(selected)));
                }
        }
        else
        {
            lines = _session.CurrentLines(_language);
            current = encounter.Line;
            if (_retry == null && (current == lines.Length - 1 || _ancient == null))
                AddButton(current == lines.Length - 1
                    ? ShinGetterDialogueCatalog.Ui("结束交谈", "Finish conversation", "会話を終える")
                    : ShinGetterDialogueCatalog.Ui("继续", "Continue", "続ける"), Next);
        }
        if (_retry != null)
        {
            lines = lines.Take(current + 1).Append(ShinGetterDialogueCatalog.Ui(
                "交谈进度未能保存。原有进度没有改变。可重试，或跳过本次交谈。",
                "Conversation progress could not be saved. Your previous progress is unchanged. Retry, or skip this conversation.",
                "会話の進行を保存できませんでした。以前の進行は変わりません。再試行するか、今回の会話をスキップしてください。")).ToArray();
            current = lines.Length - 1;
            AddButton(ShinGetterDialogueCatalog.Ui("重试保存", "Retry", "再試行"), () => _retry?.Invoke());
        }
        if (_ancient != null) RenderAncient(lines, current, generation);
        else RenderArchitect(lines[current]);
        Callable.From(() =>
        {
            if (_closed || _fatal || generation != _renderGeneration || !IsInsideTree() || !IsVisibleInTree()) return;
            if (_retry == null && !_session.MarkDisplayed())
            {
                _retry = () => Attempt(() => _session.MarkDisplayed());
                Render();
                return;
            }
            ConfigureFocus();
            if (_retry == null && encounter is { DialogueId.Length: > 0 }) TryCue(encounter);
            if (IsEventActive) DefaultFocusedControl?.TryGrabFocus();
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

    private void AddButton(string text, Action action)
    {
        var option = new EventOption(_model, () => Task.CompletedTask, NativeText(text), NativeText(""),
            "SHIN_GETTER_BOND_LOCAL", Array.Empty<IHoverTip>()).ThatWontSaveToChoiceHistory();
        var button = NEventOptionButton.Create(_model, option, _buttons.Count);
        _column.AddChild(button);
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
        if (Valid(_skipButton)) controls.Add(_skipButton!);
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
            button.GetParent()?.RemoveChild(button);
            button.QueueFree();
        }
        _buttons.Clear();
    }

    private void ClearNativeDialogue()
    {
        StopSpeechBubble();
        if (!Valid(_ancient)) return;
        ContentTween(_ancient!)?.Kill();
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
            if (Valid(snapshot.Button) && snapshot.Button.GetParent() == _column)
            {
                snapshot.Button.Visible = snapshot.Visible;
                snapshot.Button.FocusMode = snapshot.Focus;
            }
        _originalOptions.Clear();
    }

    private void RestoreNativeUi()
    {
        if (_nativeRestored) return;
        _nativeRestored = true;
        ++_renderGeneration;
        ClearTemporaryOptions();
        ClearNativeDialogue();
        RestoreOptionsVisibility();
        if (Valid(_eventDescription)) _eventDescription!.Visible = _descriptionWasVisible;
        if (_skipButton != null && GodotObject.IsInstanceValid(_skipButton))
        {
            _skipButton.Disable();
            if (_skipButton.IsConnected(NClickableControl.SignalName.Released, _skipReleased))
                _skipButton.Disconnect(NClickableControl.SignalName.Released, _skipReleased);
        }
        _skipButton = null;
    }

    private void ShowFatalError(Exception ex)
    {
        GD.PushWarning("Shin Getter conversation unavailable: " + ex.Message);
        _fatal = true;
        ++_renderGeneration;
        StopVoice();
        ClearTemporaryOptions();
        ClearNativeDialogue();
        _skipButton?.Disable();
        // Only asset-failure recovery uses built-ins; normal UI never draws a panel.
        foreach (Node child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        var box = new VBoxContainer { Position = new Vector2(80, 160), Size = new Vector2(900, 240) };
        AddChild(box);
        box.AddChild(new Label
        {
            Text = ShinGetterDialogueCatalog.Ui("交谈暂时无法显示，已有羁绊进度不变。可跳过并继续原事件。",
                "Conversation unavailable. Existing bond progress is unchanged. Skip to continue the event.",
                "会話を表示できません。以前の絆の進行は変わりません。スキップしてイベントを続けられます。"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var skip = new Button
        {
            Text = ShinGetterDialogueCatalog.Ui("跳过本次交谈", "Skip this conversation", "今回の会話をスキップ"),
            FocusMode = FocusModeEnum.All, CustomMinimumSize = new Vector2(0, 80),
        };
        skip.Pressed += Skip;
        box.AddChild(skip);
        _emergencyFocus = skip;
        Callable.From(() => { if (!_closed && IsInsideTree() && IsEventActive) skip.TryGrabFocus(); }).CallDeferred();
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
        if (!_session.ConsumeCue(encounter.Line)) return;
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

    private void Skip()
    {
        if (_closed) return;
        StopVoice();
        _session.Skip();
        Close();
    }

    private void Close()
    {
        if (_closed) return;
        _closed = true;
        ReleaseConsoleGuard();
        StopVoice();
        RestoreNativeUi();
        Hide();
        var continuation = _returnToEvent;
        _returnToEvent = null;
        continuation?.Invoke();
        QueueFree();
    }

    public override void _ExitTree()
    {
        _closed = true;
        ReleaseConsoleGuard();
        StopVoice();
        RestoreNativeUi();
        _retry = null;
        _returnToEvent = null;
        _emergencyFocus = null;
        base._ExitTree();
    }

    private void ReleaseConsoleGuard()
    {
        if (!_consoleGuard) return;
        _consoleGuard = false;
        _openCount--;
    }
}
