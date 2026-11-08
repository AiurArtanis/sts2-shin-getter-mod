#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;

namespace ShinGetterMod.Nodes.Events;

// Event-only overlay: no synthetic event page, no payment until the returned choice is confirmed.
public partial class NShinGetterEventItemChoice : Control, IOverlayScreen
{
    internal readonly record struct Item(string Title, string Description, string IconPath);
    private readonly TaskCompletionSource<int> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<Button> _buttons = new();
    private IReadOnlyList<Item> _items = Array.Empty<Item>();
    private string _prompt = "";
    private bool _cancelable;
    private bool _built;
    public NetScreenType ScreenType => NetScreenType.Rewards;
    public bool UseSharedBackstop => true;
    public Control? DefaultFocusedControl => _buttons.Count == 0 ? null : _buttons[0];

    internal static async Task<int> Select(Player owner, LocString prompt, IReadOnlyList<Item> items, bool cancelable)
    {
        if (items.Count == 0) return -1;
        if (NonInteractiveMode.IsActive) return 0;
        NOverlayStack? stack = NOverlayStack.Instance;
        if (stack == null) return -1;
        var context = new BlockingPlayerChoiceContext();
        var synchronizer = RunManager.Instance.PlayerChoiceSynchronizer;
        uint id = synchronizer.ReserveChoiceId(owner);
        await context.SignalPlayerChoiceBegun(PlayerChoiceOptions.None);
        NShinGetterEventItemChoice screen = new() { _items = items, _prompt = prompt.GetFormattedText(), _cancelable = cancelable };
        try
        {
            stack.Push(screen);
            int selected = await screen._completion.Task;
            synchronizer.SyncLocalChoice(owner, id, PlayerChoiceResult.FromIndex(selected));
            return selected;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(screen) && screen.IsInsideTree()) stack.Remove(screen);
            else if (GodotObject.IsInstanceValid(screen)) screen.QueueFree();
            await context.SignalPlayerChoiceEnded();
        }
    }

    public override void _Ready()
    {
        if (_built) return;
        _built = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        PanelContainer panel = new() { AnchorLeft = 0.22f, AnchorRight = 0.78f, AnchorTop = 0.10f, AnchorBottom = 0.88f };
        AddChild(panel);
        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);
        MegaRichTextLabel header = new() { Text = _prompt, BbcodeEnabled = true, FitContent = true, MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeFontSizeOverride("normal_font_size", 26);
        column.AddChild(header);
        ScrollContainer scroll = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
        column.AddChild(scroll);
        VBoxContainer rows = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(rows);
        for (int i = 0; i < _items.Count; i++)
        {
            int index = i;
            Item item = _items[i];
            Button button = new() { CustomMinimumSize = new Vector2(500, 105), FocusMode = FocusModeEnum.All };
            rows.AddChild(button);
            HBoxContainer content = new() { MouseFilter = MouseFilterEnum.Ignore };
            content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 8);
            button.AddChild(content);
            TextureRect icon = new() { Texture = ResourceLoader.Load<Texture2D>(item.IconPath),
                CustomMinimumSize = new Vector2(72, 72), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
            content.AddChild(icon);
            MegaRichTextLabel text = new() { Text = item.Title + "\n" + item.Description, BbcodeEnabled = true,
                FitContent = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            text.AddThemeFontSizeOverride("normal_font_size", 22);
            content.AddChild(text);
            button.Pressed += () => _completion.TrySetResult(index);
            _buttons.Add(button);
        }
        if (_cancelable)
        {
            Button cancel = new() { Text = new LocString("events", "SHIN_GETTER_EVENT_INVASION.CANCEL").GetFormattedText(),
                CustomMinimumSize = new Vector2(0, 54), FocusMode = FocusModeEnum.All };
            column.AddChild(cancel);
            cancel.Pressed += () => _completion.TrySetResult(-1);
            _buttons.Add(cancel);
        }
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].FocusNeighborTop = _buttons[(i - 1 + _buttons.Count) % _buttons.Count].GetPath();
            _buttons[i].FocusNeighborBottom = _buttons[(i + 1) % _buttons.Count].GetPath();
        }
        DefaultFocusedControl?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _ExitTree() => _completion.TrySetResult(-1);
    public void AfterOverlayOpened() { }
    public void AfterOverlayClosed() { _completion.TrySetResult(-1); QueueFree(); }
    public void AfterOverlayShown() => Show();
    public void AfterOverlayHidden() => Hide();
}
