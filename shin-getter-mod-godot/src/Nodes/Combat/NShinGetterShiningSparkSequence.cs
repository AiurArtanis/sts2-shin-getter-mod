#nullable enable
using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace ShinGetterMod.Nodes.Combat;

internal partial class NShinGetterShiningSparkSequence : Node2D
{
    // Zero-based indices verified against the selected source frames, not the old DashV2.
    private const int DiscardComplete = 18;
    private const int ChargeHold = 26;
    private const int DashPeak = 31;
    private const int EndHold = 33;
    private const string ManualOwnerMeta = "shin_getter_shining_spark_owner";
    private readonly TaskCompletionSource<bool> _ended = new();
    private AnimatedSprite2D _sprite = null!;
    private Creature _owner = null!;
    private Vector2 _origin;
    private Vector2 _lunge;
    private Sprite2D _shell = null!;
    private readonly Sprite2D[] _tails = new Sprite2D[3];
    private TaskCompletionSource<bool>? _stageCompletion;
    private Action<float>? _stageUpdate;
    private float _stageDuration;
    private float _stageTime;
    private float _energy;
    private bool _closed;

    public static NShinGetterShiningSparkSequence? TryCreate(Creature owner, Creature target)
    {
        if (NonInteractiveMode.IsActive || SaveManager.Instance.PrefsSave.FastMode == FastModeType.Instant)
            return null;

        NCombatRoom? room = NCombatRoom.Instance;
        var node = room?.GetCreatureNode(owner);
        var sprite = node?.Visuals.GetNodeOrNull<AnimatedSprite2D>("Visuals/ShinDragon");
        if (room == null || sprite == null || !IsActuallyVisible(sprite)
            || IsControlling(sprite) || sprite.GetParent() is not Node2D parent)
            return null;

        if (!NShinGetterSpriteAnimationStateMachine.TryPlay(sprite, "ShiningSpark", NShinGetterSpriteSequence.EnsureShinDragonLoaded)
            || sprite.Animation != NShinGetterSpriteSequence.ShiningSparkAnimationName)
            return null;

        sprite.Pause();
        var sequence = new NShinGetterShiningSparkSequence
        {
            Name = "ShinGetterShiningSparkSequence",
            _sprite = sprite,
            _owner = owner,
            _origin = sprite.Position,
        };
        sprite.SetMeta(ManualOwnerMeta, sequence.GetInstanceId());
        Vector2 source = sprite.GlobalPosition;
        Vector2 destination = room.GetCreatureNode(target)?.VfxSpawnPosition ?? source;
        Vector2 direction = (destination - source).Normalized();
        Vector2 globalLunge = direction * Math.Max(0f, source.DistanceTo(destination) - 110f);
        sequence._lunge = parent.ToLocal(source + globalLunge) - parent.ToLocal(source);
        room.CombatVfxContainer.AddChild(sequence);
        return sequence;
    }

    public override void _Ready()
    {
        var shader = new Shader
        {
            Code = @"shader_type canvas_item;
render_mode unshaded;
void fragment() {
    float a = texture(TEXTURE, UV).a;
    COLOR = vec4(0.18, 1.0, 0.65, a * COLOR.a);
}",
        };
        _shell = new Sprite2D { Material = new ShaderMaterial { Shader = shader } };
        AddChild(_shell);
        for (int i = 0; i < _tails.Length; i++)
        {
            _tails[i] = new Sprite2D { Material = _shell.Material };
            AddChild(_tails[i]);
        }
        UpdateEnergy();
    }

    public async Task PlayToImpact(Func<Task> intro, Func<Task> release)
    {
        // The dropped axe is already baked into these frames: never create a second prop.
        await Stage(0.4f, u => SetFrame(0, DiscardComplete, u));
        if (_closed) return;
        Task voice = intro();
        await Stage(1f, u =>
        {
            SetFrame(DiscardComplete, ChargeHold, u);
            _energy = u * 0.35f;
        });
        await Task.WhenAny(voice, _ended.Task);
        if (_closed) return;
        await voice;
        await Task.WhenAny(CombatManager.Instance.WaitForUnpause(), _ended.Task);
        if (_closed) return;
        _ = release(); // Playback starts now; impact does not wait for the line to finish.
        await Stage(0.35f, u =>
        {
            SetFrame(ChargeHold, DashPeak, u);
            _sprite.Position = _origin + _lunge * (1f - (1f - u) * (1f - u));
            _energy = 0.35f + u * 0.3f;
        });
        await Stage(0.1f, u => SetFrame(DashPeak, EndHold, u));
    }

    public async Task Recover()
    {
        if (_closed || !OwnsSprite()) return;
        Vector2 start = _sprite.Position;
        await Stage(0.5f, u =>
        {
            _sprite.Position = start.Lerp(_origin, u);
            _energy = 0.65f * (1f - u);
        });
    }

    private Task Stage(float duration, Action<float> update)
    {
        if (_closed) return Task.CompletedTask;
        if (!OwnsSprite() || !IsActuallyVisible(_sprite) || _owner.IsDead
            || CombatManager.Instance.IsOverOrEnding)
        {
            Close();
            return Task.CompletedTask;
        }
        _stageTime = 0f;
        _stageDuration = duration;
        _stageUpdate = update;
        _stageCompletion = new TaskCompletionSource<bool>();
        update(0f);
        return _stageCompletion.Task;
    }

    public override void _Process(double delta)
    {
        if (_closed) return;
        if (!OwnsSprite() || !_sprite.IsInsideTree()
            || !IsActuallyVisible(_sprite) || _owner.IsDead
            || _sprite.Animation != NShinGetterSpriteSequence.ShiningSparkAnimationName
            || CombatManager.Instance.IsOverOrEnding)
        {
            Close();
            return;
        }
        if (CombatManager.Instance.IsPaused) return;
        if (SaveManager.Instance.PrefsSave.FastMode == FastModeType.Instant)
        {
            Close();
            return;
        }
        if (_stageUpdate != null)
        {
            _stageTime += (float)delta;
            float progress = Math.Min(1f, _stageTime / _stageDuration);
            _stageUpdate(progress);
            if (progress >= 1f)
            {
                _stageUpdate = null;
                _stageCompletion?.TrySetResult(true);
            }
        }
        UpdateEnergy();
    }

    private void SetFrame(int first, int last, float progress) =>
        _sprite.Frame = first + (int)Math.Round((last - first) * progress);

    private void UpdateEnergy()
    {
        Texture2D? texture = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        _shell.Texture = texture;
        _shell.GlobalTransform = _sprite.GlobalTransform;
        _shell.Offset = _sprite.Offset;
        _shell.Centered = _sprite.Centered;
        _shell.FlipH = _sprite.FlipH;
        _shell.FlipV = _sprite.FlipV;
        _shell.Modulate = new Color(1f, 1f, 1f, _energy * 0.45f);
        for (int i = 0; i < _tails.Length; i++)
        {
            Sprite2D tail = _tails[i];
            tail.Texture = texture;
            tail.GlobalTransform = _sprite.GlobalTransform;
            tail.Offset = _sprite.Offset;
            tail.Centered = _sprite.Centered;
            tail.FlipH = _sprite.FlipH;
            tail.FlipV = _sprite.FlipV;
            tail.GlobalPosition -= _sprite.GlobalTransform.BasisXform(_lunge.Normalized()) * (i + 1) * 18f;
            tail.Modulate = new Color(1f, 1f, 1f, _energy > 0.35f ? _energy * 0.12f / (i + 1) : 0f);
        }
    }

    public void Close()
    {
        End();
        if (GodotObject.IsInstanceValid(this) && IsInsideTree()) QueueFree();
    }

    public override void _ExitTree() => End();

    internal static bool IsControlling(AnimatedSprite2D sprite) => sprite.HasMeta(ManualOwnerMeta);

    private bool OwnsSprite() => GodotObject.IsInstanceValid(_sprite)
        && _sprite.HasMeta(ManualOwnerMeta)
        && _sprite.GetMeta(ManualOwnerMeta).AsUInt64() == GetInstanceId();

    private static bool IsActuallyVisible(AnimatedSprite2D sprite)
    {
        if (!sprite.IsVisibleInTree()) return false;
        float alpha = sprite.SelfModulate.A;
        for (Node? node = sprite; node != null; node = node.GetParent())
        {
            if (node is CanvasItem item) alpha *= item.Modulate.A;
        }
        return alpha > 0.01f;
    }

    private void End()
    {
        if (_closed) return;
        _closed = true;
        _stageUpdate = null;
        if (OwnsSprite())
        {
            _sprite.RemoveMeta(ManualOwnerMeta);
            _sprite.Position = _origin;
            if (_sprite.IsInsideTree() && IsActuallyVisible(_sprite) && !_owner.IsDead
                && _sprite.Animation == NShinGetterSpriteSequence.ShiningSparkAnimationName
                && CombatManager.Instance.IsInProgress)
            {
                NShinGetterSpriteAnimationStateMachine.PlayIdle(_sprite, NShinGetterSpriteSequence.EnsureShinDragonIdleLoaded);
            }
        }
        _stageCompletion?.TrySetResult(false);
        _ended.TrySetResult(false);
    }
}
