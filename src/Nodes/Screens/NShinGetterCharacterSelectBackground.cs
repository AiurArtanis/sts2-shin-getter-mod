#nullable enable
using Godot;

namespace ShinGetterMod.Nodes.Screens;

public partial class NShinGetterCharacterSelectBackground : Control
{
    private Viewport? _viewport;
    private Callable _sizeChangedCallback;

    public override void _Ready()
    {
        _viewport = GetViewport();
        _sizeChangedCallback = Callable.From(OnViewportSizeChanged);
        _viewport.Connect(Viewport.SignalName.SizeChanged, _sizeChangedCallback);
        QueueLayoutRefresh();
    }

    public override void _ExitTree()
    {
        if (_viewport != null
            && GodotObject.IsInstanceValid(_viewport)
            && _viewport.IsConnected(Viewport.SignalName.SizeChanged, _sizeChangedCallback))
        {
            _viewport.Disconnect(Viewport.SignalName.SizeChanged, _sizeChangedCallback);
        }
    }

    private void OnViewportSizeChanged() => QueueLayoutRefresh();

    private void QueueLayoutRefresh() => Callable.From(ApplyViewportLayout).CallDeferred();

    private void ApplyViewportLayout()
    {
        if (_viewport == null || !IsInsideTree() || GetParent() is not CanvasItem parent)
            return;

        Rect2 viewportRect = _viewport.GetVisibleRect();
        Transform2D inverseParentTransform = parent.GetGlobalTransformWithCanvas().AffineInverse();
        Vector2 localTopLeft = inverseParentTransform * viewportRect.Position;
        Vector2 localBottomRight = inverseParentTransform * (viewportRect.Position + viewportRect.Size);

        SetAnchorsPreset(LayoutPreset.TopLeft);
        PivotOffset = Vector2.Zero;
        Rotation = 0f;
        Scale = Vector2.One;
        Position = localTopLeft;
        Size = localBottomRight - localTopLeft;
    }
}
