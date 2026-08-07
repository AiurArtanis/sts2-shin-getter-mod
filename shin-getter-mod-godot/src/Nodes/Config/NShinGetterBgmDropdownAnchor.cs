#nullable enable
using Godot;

namespace ShinGetterMod.Nodes.Config;

/// <summary>
/// Keeps an overlaid settings dropdown aligned with its row inside the scroll view.
/// The original settings screen uses the same anchor/overlay split for dropdowns.
/// </summary>
public partial class NShinGetterBgmDropdownAnchor : Control
{
    private NShinGetterBgmDropdown? _dropdown;

    public NShinGetterBgmDropdownAnchor()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    internal void Bind(NShinGetterBgmDropdown dropdown)
    {
        _dropdown = dropdown;
        if (IsNodeReady())
            SyncDropdown();
    }

    public override void _Ready()
    {
        Connect(Control.SignalName.FocusEntered, Callable.From(ForwardFocus));
        Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(SyncDropdown));
        SyncDropdown();
    }

    public override void _Process(double delta)
    {
        SyncDropdown();
    }

    public override void _ExitTree()
    {
        if (IsDropdownValid())
            _dropdown!.Visible = false;
    }

    private void ForwardFocus()
    {
        if (IsDropdownValid() && _dropdown!.Visible)
            _dropdown.GrabFocus();
    }

    private void SyncDropdown()
    {
        if (!IsDropdownValid())
            return;

        NShinGetterBgmDropdown dropdown = _dropdown!;
        bool shouldBeVisible = IsVisibleInTree();
        if (dropdown.Visible != shouldBeVisible)
            dropdown.Visible = shouldBeVisible;
        if (!shouldBeVisible || Size.X <= 0f || Size.Y <= 0f)
            return;

        dropdown.GlobalPosition = GlobalPosition;
        dropdown.Size = Size;
        dropdown.FocusNeighborBottom = FocusNeighborBottom;
        dropdown.FocusNeighborTop = FocusNeighborTop;
        dropdown.FocusNeighborLeft = FocusNeighborLeft;
        dropdown.FocusNeighborRight = FocusNeighborRight;
    }

    private bool IsDropdownValid() =>
        _dropdown != null && GodotObject.IsInstanceValid(_dropdown);
}
