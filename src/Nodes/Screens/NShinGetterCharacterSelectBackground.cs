#nullable enable
using Godot;

namespace ShinGetterMod.Nodes.Screens;

[GlobalClass]
public partial class NShinGetterCharacterSelectBackground : Control
{
    private Rect2 _lastLocalRect;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        FitToGameViewport();
    }

    public override void _Process(double delta)
    {
        FitToGameViewport();
    }

    private void FitToGameViewport()
    {
        if (GetParent() is not CanvasItem parent)
            return;

        Rect2 viewportRect = GetViewport().GetVisibleRect();
        Transform2D inverseParentTransform = parent.GetGlobalTransformWithCanvas().AffineInverse();
        Vector2 localTopLeft = inverseParentTransform * viewportRect.Position;
        Vector2 localBottomRight = inverseParentTransform * viewportRect.End;
        Rect2 localRect = new(localTopLeft, localBottomRight - localTopLeft);
        if (localRect.IsEqualApprox(_lastLocalRect))
            return;

        _lastLocalRect = localRect;
        Position = localRect.Position;
        Size = localRect.Size;
    }
}
