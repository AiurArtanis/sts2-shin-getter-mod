#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Nodes.Vfx;

internal partial class NShinGetterSparkSubtitleFollower : Node
{
    private NSpeechBubbleVfx _subtitle = null!;
    private AnimatedSprite2D _sprite = null!;
    private Creature _owner = null!;
    private const string FollowerName = "ShinGetterSparkSubtitleFollower";
    private const float BodyGap = 24f;

    internal static void Attach(NSpeechBubbleVfx subtitle, Creature owner, bool enlarge)
    {
        if (subtitle.HasNode(FollowerName)) return;
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (creatureNode == null) return;
        foreach (string form in new[] { "ShinDragon", "GetterOne", "GetterTwo", "GetterThree" })
        {
            var sprite = creatureNode.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/" + form);
            if (sprite == null || !NShinGetterShiningSparkSequence.IsActuallyVisible(sprite)) continue;
            if (!enlarge && sprite.Animation != NShinGetterSpriteSequence.ShiningSparkAnimationName) return;
            Vector2 baseScale = subtitle.Scale;
            subtitle.Scale = baseScale * (enlarge ? 1.3f * 1.5f : 1f);
            // Do not show a frame at the old, stationary TalkPosition before layout is ready.
            subtitle.Hide();
            subtitle.AddChild(new NShinGetterSparkSubtitleFollower
            {
                Name = FollowerName,
                ProcessPriority = 100,
                _subtitle = subtitle,
                _sprite = sprite,
                _owner = owner,
            });
            return;
        }
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(_sprite) || !_sprite.IsInsideTree()
            || !NShinGetterShiningSparkSequence.IsActuallyVisible(_sprite)
            || _owner.IsDead || CombatManager.Instance.IsOverOrEnding)
        {
            _subtitle.Hide();
            QueueFree();
            return;
        }
        if (CombatManager.Instance.IsPaused) return;
        if (_subtitle.GetParent() is not CanvasItem parent) return;
        Rect2 body = NShinGetterShiningSparkSequence.TransformRect(
            NShinGetterShiningSparkSequence.GetFrameLocalRect(_sprite), _sprite.GetGlobalTransformWithCanvas());
        Rect2 bubble = GetBubbleViewportRect();
        Rect2 viewport = _subtitle.GetViewport().GetVisibleRect().Grow(-16f);
        Vector2 shift = new(body.GetCenter().X - bubble.GetCenter().X, body.Position.Y - BodyGap - bubble.End.Y);
        if (bubble.Position.Y + shift.Y < viewport.Position.Y)
        {
            // Clamping the top would put the bubble back on the head: use the roomier side.
            float rightRoom = viewport.End.X - body.End.X - BodyGap;
            float leftRoom = body.Position.X - viewport.Position.X - BodyGap;
            shift.X = rightRoom >= leftRoom
                ? body.End.X + BodyGap - bubble.Position.X
                : body.Position.X - BodyGap - bubble.End.X;
            shift.Y = Mathf.Clamp(body.GetCenter().Y - bubble.Size.Y * 0.5f,
                viewport.Position.Y, Mathf.Max(viewport.Position.Y, viewport.End.Y - bubble.Size.Y)) - bubble.Position.Y;
        }
        else
        {
            shift.X = Mathf.Clamp(bubble.Position.X + shift.X, viewport.Position.X,
                Mathf.Max(viewport.Position.X, viewport.End.X - bubble.Size.X)) - bubble.Position.X;
        }
        Vector2 viewportPosition = _subtitle.GetGlobalTransformWithCanvas().Origin + shift;
        _subtitle.Position = parent.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
        _subtitle.Show();
    }

    private Rect2 GetBubbleViewportRect()
    {
        Rect2? result = null;
        foreach (string name in new[] { "%Bubble", "%Shadow", "%Text" })
        {
            var item = _subtitle.GetNodeOrNull<CanvasItem>(name);
            Rect2 local;
            if (item is Sprite2D sprite) local = sprite.GetRect();
            else if (item is Control control) local = new Rect2(Vector2.Zero, control.Size);
            else continue;
            Rect2 rect = NShinGetterShiningSparkSequence.TransformRect(local, item.GetGlobalTransformWithCanvas());
            result = result?.Merge(rect) ?? rect;
        }
        return result ?? NShinGetterShiningSparkSequence.TransformRect(
            new Rect2(-110f, -165f, 340f, 185f), _subtitle.GetGlobalTransformWithCanvas());
    }
}
