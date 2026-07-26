#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace ShinGetterMod.Nodes.Config;

public partial class NShinGetterVoicePaginator : NPaginator
{
    private Color _presentationColor = Colors.White;
    private string _presentationTooltip = string.Empty;

    public event Action<int>? IndexChanged;

    public override void _Ready()
    {
        ConnectSignals();
        RefreshLabel();
        ApplyPresentation();
    }

    public void Configure(IReadOnlyList<string> options, int selectedIndex)
    {
        _options.Clear();
        _options.AddRange(options);
        _currentIndex = Mathf.Clamp(selectedIndex, 0, _options.Count - 1);
        if (IsNodeReady())
            RefreshLabel();
    }

    public void SetPresentation(Color color, string tooltip)
    {
        _presentationColor = color;
        _presentationTooltip = tooltip;
        if (IsNodeReady())
            ApplyPresentation();
    }

    protected override void OnIndexChanged(int index)
    {
        RefreshLabel();
        IndexChanged?.Invoke(index);
    }

    private void RefreshLabel()
    {
        if (_options.Count == 0)
            return;

        _label.SetTextAutoSize(_options[_currentIndex]);
    }

    private void ApplyPresentation()
    {
        _label.Modulate = _presentationColor;
        TooltipText = _presentationTooltip;
    }
}
