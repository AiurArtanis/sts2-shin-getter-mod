#nullable enable
using System;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Nodes.Vfx;

internal partial class NShinGetterHotBloodIconFlash : Sprite2D
{
    private Creature _owner = null!;
    private float _age;

    internal static void Play(Creature owner)
    {
        var node = NCombatRoom.Instance?.GetCreatureNode(owner);
        var container = owner.GetVfxContainer();
        if (node == null || container == null || owner.IsDead) return;
        var flash = new NShinGetterHotBloodIconFlash
        {
            _owner = owner,
            Texture = ModelDb.Power<SGP_HotBlood>().BigIcon,
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        container.AddChild(flash);
        flash.GlobalPosition = node.VfxSpawnPosition;
    }

    public override void _Process(double delta)
    {
        if (_owner.IsDead || CombatManager.Instance.IsOverOrEnding)
        {
            QueueFree();
            return;
        }
        if (CombatManager.Instance.IsPaused) return;
        _age += (float)delta;
        float u = Math.Min(1f, _age / 0.5f);
        Modulate = new Color(1f, 1f, 1f, Math.Min(1f, u * 5f) * (1f - u));
        Scale = Vector2.One * (1f + u * 0.35f);
        if (u >= 1f) QueueFree();
    }
}
