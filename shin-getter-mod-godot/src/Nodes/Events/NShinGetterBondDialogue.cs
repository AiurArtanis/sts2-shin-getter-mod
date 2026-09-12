#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Config;
using ShinGetterMod.Services;

namespace ShinGetterMod.Nodes.Events;

/// <summary>Scene-local event reading/choice layer. It never owns a modal slot or gameplay command.</summary>
internal sealed partial class NShinGetterBondDialogue : Control
{
    public NShinGetterBondDialogue() { }
    private ShinGetterBondSession _session = null!;
    private Action? _returnToEvent;
    private VBoxContainer _column = null!;
    private readonly List<NShinGetterBondButton> _buttons = new();
    private MegaRichTextLabel _body = null!;
    private MegaRichTextLabel _error = null!;
    private Action? _retry;
    private AudioStreamPlayer? _voice;
    private double _voiceTimeout;
    private bool _closed;
    private bool _ready;
    private bool _fatal;
    private string _language = "";
    private int _lastCueLine = -1;
    private Control? _emergencyFocus;
    internal Control? DefaultFocusedControl => _emergencyFocus ?? _buttons.FirstOrDefault();
    private static bool IsEventActive => NEventRoom.Instance != null
        && ActiveScreenContext.Instance.IsCurrent(NEventRoom.Instance);

    internal static NShinGetterBondDialogue Create(ShinGetterBondSession session, Action returnToEvent) => new()
    {
        Name = "ShinGetterBondDialogue",
        _session = session,
        _returnToEvent = returnToEvent,
    };

    public override void _Ready()
    {
        if (_ready) return;
        _ready = true;
        try { BuildUi(); }
        catch (Exception ex) { ShowFatalError(ex); }
    }

    private void BuildUi()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        // The ancient/background stays visible to the left. Inherit the game's text and colors.
        var panel = new PanelContainer { Name = "ConversationPanel", MouseFilter = MouseFilterEnum.Stop };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.AnchorLeft = 0.44f;
        panel.AnchorTop = 0.18f;
        panel.AnchorRight = 0.95f;
        panel.AnchorBottom = 0.88f;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.052f, 0.064f, 0.97f),
            ContentMarginLeft = 32, ContentMarginRight = 32,
            ContentMarginTop = 28, ContentMarginBottom = 28,
        });
        AddChild(panel);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddChild(scroll);
        _column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _column.AddThemeConstantOverride("separation", 22);
        scroll.AddChild(_column);
        _body = Label(30);
        _column.AddChild(_body);
        _error = Label(24);
        _column.AddChild(_error);
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
        if (_language != MegaCrit.Sts2.Core.Localization.LocManager.Instance.Language)
            Render();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!_closed && IsEventActive && input.IsActionPressed("ui_cancel"))
        {
            Skip();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Attempt(Func<bool> operation)
    {
        if (_closed) return;
        if (operation())
        {
            _retry = null;
            if (_session.Closed) { Close(); return; }
            Render();
        }
        else
        {
            _retry = () => Attempt(operation);
            Render();
        }
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
        _language = MegaCrit.Sts2.Core.Localization.LocManager.Instance.Language;
        foreach (var button in _buttons)
        {
            _column.RemoveChild(button);
            button.QueueFree();
        }
        _buttons.Clear();
        _error.SetText(_retry == null ? "" : ShinGetterDialogueCatalog.Ui(
            "交谈进度未能保存。原有进度没有改变。可重试，或跳过本次交谈。",
            "Conversation progress could not be saved. Your previous progress is unchanged. Retry, or skip this conversation.",
            "会話の進行を保存できませんでした。以前の進行は変わっていません。再試行するか、今回の会話をスキップしてください。"));
        _error.Visible = _retry != null;
        _error.TooltipText = _session.Error;
        var encounter = _session.Encounter;
        if (encounter == null || encounter.DialogueId.Length == 0)
        {
            _body.SetText(ShinGetterDialogueCatalog.Ui("这一次，由谁来开口？", "Who will speak this time?", "今度は、誰が話す？"));
            if (_retry == null)
                foreach (string id in _session.Choices)
                {
                    string selected = id;
                    AddButton(ShinGetterDialogueCatalog.Localize(id).Option, () => Attempt(() => _session.Select(selected)));
                }
        }
        else
        {
            var lines = encounter.LinesByLanguage?.GetValueOrDefault(_language is "zhs" or "jpn" ? _language : "eng");
            _body.SetText(lines != null && lines.Length > 0
                ? lines[Math.Clamp(encounter.Line, 0, lines.Length - 1)]
                : ShinGetterDialogueCatalog.Ui("本次交谈记录不可读取。", "This conversation record cannot be read.", "この会話記録を読み込めません。"));
            if (_retry == null)
            {
                bool last = encounter.Line == lines!.Length - 1;
                AddButton(last ? ShinGetterDialogueCatalog.Ui("结束交谈", "Finish conversation", "会話を終える")
                    : ShinGetterDialogueCatalog.Ui("继续", "Continue", "続ける"), Next);
                TryCue(encounter);
            }
        }
        if (_retry != null) AddButton(ShinGetterDialogueCatalog.Ui("重试保存", "Retry", "再試行"), () => _retry?.Invoke());
        AddButton(ShinGetterDialogueCatalog.Ui("跳过本次交谈", "Skip this conversation", "今回の会話をスキップ"), Skip);
        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            button.FocusNeighborTop = _buttons[(i + _buttons.Count - 1) % _buttons.Count].GetPath();
            button.FocusNeighborBottom = _buttons[(i + 1) % _buttons.Count].GetPath();
            button.FocusPrevious = button.FocusNeighborTop;
            button.FocusNext = button.FocusNeighborBottom;
            button.FocusNeighborLeft = button.GetPath();
            button.FocusNeighborRight = button.GetPath();
        }
        Callable.From(() =>
        {
            if (!_closed && !_fatal && IsInsideTree() && IsVisibleInTree() && _retry == null
                && !_session.MarkDisplayed())
            {
                _retry = () => Attempt(() => _session.MarkDisplayed());
                Render();
            }
            if (!_closed && IsInsideTree() && IsEventActive) DefaultFocusedControl?.TryGrabFocus();
        }).CallDeferred();
    }

    private void ShowFatalError(Exception ex)
    {
        // A malformed catalogue or unavailable native UI asset must not trap the run.
        // The emergency control uses only Godot built-ins, not the failed rich-text path.
        GD.PushWarning("Shin Getter conversation unavailable: " + ex.Message);
        _fatal = true;
        StopVoice();
        foreach (Node child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        _buttons.Clear();
        var box = new VBoxContainer { Position = new Vector2(80, 160), Size = new Vector2(900, 240) };
        AddChild(box);
        var message = new Godot.Label
        {
            Text = ShinGetterDialogueCatalog.Ui("交谈暂时无法显示，已有羁绊进度不变。可跳过并继续原事件。",
                "Conversation unavailable. Existing bond progress is unchanged. Skip to continue the event.",
                "会話を表示できません。以前の絆の進行は変わりません。スキップしてイベントを続けられます。"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        box.AddChild(message);
        var skip = new Button
        {
            Text = ShinGetterDialogueCatalog.Ui("跳过本次交谈", "Skip this conversation", "今回の会話をスキップ"),
            FocusMode = FocusModeEnum.All, CustomMinimumSize = new Vector2(0, 80),
        };
        skip.Pressed += Skip;
        box.AddChild(skip);
        _emergencyFocus = skip;
        Callable.From(() => { if (!_closed && IsInsideTree() && IsEventActive) skip.GrabFocus(); }).CallDeferred();
    }

    private void Next()
    {
        if (!IsEventActive || _voice is { Playing: true }) return;
        Attempt(() => _session.Advance());
    }

    private void TryCue(ShinGetterBondEncounter encounter)
    {
        // issue#206 scope confirmed 2026-09-12: normal dialogue plus these two voices
        // only. No paired-action scene, temporary form switch or combat/VFX commands.
        string? filename = (encounter.DialogueId, encounter.Line) switch
        {
            ("TANX_RYOMA_BOND_03", 4) => "ryoma_getter_tomahawk.wav",
            ("TANX_BENKEI_BOND_02", 3) => "musashi_avalanche.wav", // Existing 035 filename; performer is Benkei.
            _ => null,
        };
        if (filename == null || _lastCueLine == encounter.Line) return;
        _lastCueLine = encounter.Line;
        // Persist before sound, including silent mode. A failed cue write is a silent fallback.
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
        if (_voice != null && GodotObject.IsInstanceValid(_voice))
        {
            _voice.Stop();
            _voice.QueueFree();
        }
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
        StopVoice();
        Hide();
        var continuation = _returnToEvent;
        _returnToEvent = null;
        continuation?.Invoke();
        QueueFree();
    }

    public override void _ExitTree()
    {
        _closed = true;
        StopVoice();
        _retry = null;
        _returnToEvent = null;
        _buttons.Clear();
        _emergencyFocus = null;
    }

    private void AddButton(string text, Action action)
    {
        var button = new NShinGetterBondButton(text, () => { if (!_closed && IsEventActive) action(); });
        _column.AddChild(button);
        _buttons.Add(button);
    }

    internal static MegaRichTextLabel Label(int size)
    {
        var label = new MegaRichTextLabel
        {
            AutoSizeEnabled = false, BbcodeEnabled = true, FitContent = true, ScrollActive = false,
            MouseFilter = MouseFilterEnum.Ignore, FocusMode = FocusModeEnum.None,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Font font = PreloadManager.Cache.GetAsset<Font>("res://themes/kreon_regular_shared.tres");
        foreach (string key in new[] { "normal_font", "bold_font", "italics_font", "bold_italics_font" })
            label.AddThemeFontOverride(key, font);
        foreach (string key in new[] { "normal_font_size", "bold_font_size", "italics_font_size", "bold_italics_font_size" })
            label.AddThemeFontSizeOverride(key, size);
        label.AddThemeColorOverride("default_color", new Color(0.95f, 0.91f, 0.83f));
        label.AddThemeConstantOverride("line_separation", 8);
        return label;
    }
}

internal sealed partial class NShinGetterBondButton : NSettingsButton
{
    private Action? _action;
    private bool _ready;
    public NShinGetterBondButton() : this("", () => { }) { }
    internal NShinGetterBondButton(string text, Action action)
    {
        _action = action;
        FocusMode = FocusModeEnum.All;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0, 76);
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.20f, 0.22f),
            ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 12, ContentMarginBottom = 12,
        });
        AddChild(panel);
        var label = NShinGetterBondDialogue.Label(26);
        label.SetText(text);
        panel.AddChild(label);
        label.Resized += () => CustomMinimumSize = new Vector2(0, Math.Max(76, label.Size.Y + 24));
        var reticle = PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("ui/selection_reticle")).Instantiate<NSelectionReticle>();
        reticle.Name = "SelectionReticle";
        reticle.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(reticle);
    }
    public override void _Ready() { if (_ready) return; _ready = true; ConnectSignals(); }
    protected override void OnRelease() { base.OnRelease(); _action?.Invoke(); }
    public override void _ExitTree()
    {
        _action = null;
        _tween?.Kill();
        base._ExitTree(); // NButton owns controller/rebind subscriptions and hotkeys.
    }
}
