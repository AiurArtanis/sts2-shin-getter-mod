#nullable enable
using System.Collections.Generic;
using Godot;

namespace ShinGetterMod.Nodes.Screens;

internal static class NShinGetterCharacterSelectBackground
{
    private sealed class LayoutSubscription
    {
        internal required Viewport Viewport { get; init; }
        internal required Callable SizeChangedCallback { get; init; }
    }

    private static readonly StringName LayoutMetadata = "shin_getter_full_bleed";
    private static readonly Dictionary<ulong, LayoutSubscription> LayoutSubscriptions = new();

    internal static void RefreshMarkedBackgrounds(Control? backgroundContainer)
    {
        if (backgroundContainer == null || !GodotObject.IsInstanceValid(backgroundContainer))
            return;

        foreach (Node child in backgroundContainer.GetChildren())
        {
            if (child is not Control background || !background.HasMeta(LayoutMetadata))
                continue;

            TrackViewportChanges(background);
            RefreshBackground(background);
        }
    }

    private static void TrackViewportChanges(Control background)
    {
        ulong instanceId = background.GetInstanceId();
        if (LayoutSubscriptions.ContainsKey(instanceId))
            return;

        Viewport viewport = background.GetViewport();
        Callable sizeChangedCallback = Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(background))
                RefreshBackground(background);
            else
                StopTrackingViewport(instanceId);
        });
        LayoutSubscriptions.Add(instanceId, new LayoutSubscription
        {
            Viewport = viewport,
            SizeChangedCallback = sizeChangedCallback,
        });

        viewport.Connect(Viewport.SignalName.SizeChanged, sizeChangedCallback);
        background.Connect(
            Node.SignalName.TreeExiting,
            Callable.From(() => StopTrackingViewport(instanceId)),
            (uint)GodotObject.ConnectFlags.OneShot);
    }

    private static void StopTrackingViewport(ulong instanceId)
    {
        if (!LayoutSubscriptions.Remove(instanceId, out LayoutSubscription? subscription))
            return;

        if (GodotObject.IsInstanceValid(subscription.Viewport)
            && subscription.Viewport.IsConnected(Viewport.SignalName.SizeChanged, subscription.SizeChangedCallback))
        {
            subscription.Viewport.Disconnect(Viewport.SignalName.SizeChanged, subscription.SizeChangedCallback);
        }
    }

    private static void RefreshBackground(Control background)
    {
        ApplyViewportLayout(background);
        Callable.From(() => ApplyViewportLayout(background)).CallDeferred();
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
