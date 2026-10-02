#nullable enable
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace ShinGetterMod.Nodes.Vfx;

internal partial class NShinGetterSparkSubtitleFollower : Node
{
    private NSpeechBubbleVfx _subtitle = null!;
    private AnimatedSprite2D _sprite = null!;
    private Creature _owner = null!;
    private Vector2 _viewportOffset;

    internal static void Attach(NSpeechBubbleVfx subtitle, Creature owner)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (creatureNode == null) return;
        foreach (string form in new[] { "ShinDragon", "GetterOne", "GetterTwo", "GetterThree" })
        {
            var sprite = creatureNode.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/" + form);
            if (sprite == null || !sprite.IsVisibleInTree() || sprite.Modulate.A <= 0.01f) continue;
            subtitle.Scale *= 1.3f;
            subtitle.AddChild(new NShinGetterSparkSubtitleFollower
            {
                _subtitle = subtitle,
                _sprite = sprite,
                _owner = owner,
                _viewportOffset = subtitle.GetGlobalTransformWithCanvas().Origin
                    - sprite.GetGlobalTransformWithCanvas().Origin,
            });
            return;
        }
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(_sprite) || !_sprite.IsInsideTree()
            || !_sprite.IsVisibleInTree() || _sprite.Modulate.A <= 0.01f
            || _owner.IsDead || CombatManager.Instance.IsOverOrEnding)
        {
            _subtitle.Hide();
            QueueFree();
            return;
        }
        if (CombatManager.Instance.IsPaused) return;
        if (_subtitle.GetParent() is not CanvasItem parent) return;
        Vector2 viewportPosition = _sprite.GetGlobalTransformWithCanvas().Origin + _viewportOffset;
        _subtitle.Position = parent.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition;
    }
}
