#nullable enable
using Godot;

namespace ShinGetterMod.Nodes.Screens;

internal static class NShinGetterCharacterSelectBackground
{
    private static readonly StringName LayoutMetadata = "shin_getter_full_bleed";

    internal static void RefreshMarkedBackgrounds(Control? backgroundContainer)
    {
        if (backgroundContainer == null || !GodotObject.IsInstanceValid(backgroundContainer))
            return;

        foreach (Node child in backgroundContainer.GetChildren())
        {
            if (child is not Control background || !background.HasMeta(LayoutMetadata))
                continue;

            ApplyViewportLayout(background);
            Callable.From(() => ApplyViewportLayout(background)).CallDeferred();
        }
    }

    private static void ApplyViewportLayout(Control background)
    {
        if (!GodotObject.IsInstanceValid(background)
            || !background.IsInsideTree()
            || background.GetParent() is not CanvasItem parent)
        {
            return;
        }

        Rect2 viewportRect = background.GetViewport().GetVisibleRect();
        if (viewportRect.Size.X <= 0f || viewportRect.Size.Y <= 0f)
            return;

        Transform2D inverseParentTransform = parent.GetGlobalTransformWithCanvas().AffineInverse();
        Vector2 localTopLeft = inverseParentTransform * viewportRect.Position;
        Vector2 localBottomRight = inverseParentTransform * viewportRect.End;

        background.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        background.PivotOffset = Vector2.Zero;
        background.Rotation = 0f;
        background.Scale = Vector2.One;
        background.Position = localTopLeft;
        background.Size = localBottomRight - localTopLeft;
    }
}
