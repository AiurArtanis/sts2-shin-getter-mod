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
    private Vector2 _recoil;
    private Vector2 _direction;
    private float _travelDistance;
    private Sprite2D _shell = null!;
    private readonly Sprite2D[] _tails = new Sprite2D[5];
    private readonly float[] _tailAges = new float[5];
    private int _tailCursor;
    private float _sampleTime;
    private bool _rushing;
    private float _impactPulse;
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
        sequence._direction = direction == Vector2.Zero ? Vector2.Right : direction;
        sequence._travelDistance = globalLunge.Length();
        sequence._recoil = parent.ToLocal(source - sequence._direction * Math.Min(30f, globalLunge.Length() * 0.12f))
            - parent.ToLocal(source);
        room.CombatVfxContainer.AddChild(sequence);
        return sequence;
    }

    public override void _Ready()
    {
        var shader = new Shader
        {
            Code = @"shader_type canvas_item;
render_mode unshaded, blend_add;
void fragment() {
    float a = texture(TEXTURE, UV).a;
    COLOR = vec4(0.18, 1.0, 0.65, a * COLOR.a);
}",
        };
        var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        for (int i = 0; i < _tails.Length; i++)
        {
            _tailAges[i] = 1f;
            _tails[i] = new Sprite2D { Material = additive, Visible = false };
            AddChild(_tails[i]);
        }
        _shell = new Sprite2D { Material = new ShaderMaterial { Shader = shader } };
        AddChild(_shell);
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
            float retreat = Mathf.Clamp((u - 0.65f) / 0.35f, 0f, 1f);
            _sprite.Position = _origin + _recoil * retreat * retreat;
        });
        await Task.WhenAny(voice, _ended.Task);
        if (_closed) return;
        await voice;
        await Task.WhenAny(CombatManager.Instance.WaitForUnpause(), _ended.Task);
        if (_closed) return;
        _ = release(); // Playback starts now; impact does not wait for the line to finish.
        _rushing = true;
        _sampleTime = 0.04f;
        await Stage(0.28f, u =>
        {
            SetFrame(ChargeHold, DashPeak, u);
            _sprite.Position = (_origin + _recoil).Lerp(_origin + _lunge, u * u * u);
            _energy = 0.35f + u * 0.4f;
        });
        _rushing = false;
        await Stage(0.08f, u =>
        {
            SetFrame(DashPeak, EndHold, u);
            _impactPulse = Mathf.Sin(u * Mathf.Pi);
        });
    }

    public async Task Recover()
    {
        if (_closed || !OwnsSprite()) return;
        Vector2 start = _sprite.Position;
        _impactPulse = 0f;
        await Stage(0.35f, u =>
        {
            float smooth = u * u * (3f - 2f * u);
            _sprite.Position = start.Lerp(_origin, smooth);
            _energy = 0.75f * (1f - u);
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
        UpdateTailHistory((float)delta);
        UpdateEnergy();
        QueueRedraw();
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
        _shell.Modulate = new Color(1f, 1f, 1f, _energy * 0.18f);
    }

    private void UpdateTailHistory(float delta)
    {
        for (int i = 0; i < _tails.Length; i++)
        {
            _tailAges[i] += delta;
            _tails[i].Visible = _tailAges[i] < 0.16f;
            _tails[i].Modulate = new Color(0.45f, 1f, 0.85f, Math.Max(0f, 1f - _tailAges[i] / 0.16f) * 0.24f);
        }
        if (!_rushing || _travelDistance < 20f) return;
        _sampleTime += delta;
        if (_sampleTime < 0.04f) return;
        _sampleTime %= 0.04f;
        Sprite2D tail = _tails[_tailCursor];
        tail.Texture = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        tail.GlobalTransform = _sprite.GlobalTransform;
        tail.Offset = _sprite.Offset;
        tail.Centered = _sprite.Centered;
        tail.FlipH = _sprite.FlipH;
        tail.FlipV = _sprite.FlipV;
        tail.Visible = true;
        tail.Modulate = new Color(0.45f, 1f, 0.85f, 0.24f);
        _tailAges[_tailCursor] = 0f;
        _tailCursor = (_tailCursor + 1) % _tails.Length;
    }

    public override void _Draw()
    {
        if (_closed || _energy <= 0f || !OwnsSprite()) return;
        Texture2D? texture = _shell.Texture;
        if (texture == null) return;
        Vector2 center = _sprite.ToGlobal(_sprite.Offset + new Vector2(0f, -30f));
        Vector2 normal = new(-_direction.Y, _direction.X);
        float width = texture.GetWidth() * _sprite.GlobalTransform.BasisXform(Vector2.Right).Length() * 0.31f;
        float height = texture.GetHeight() * _sprite.GlobalTransform.BasisXform(Vector2.Down).Length() * 0.35f;
        float extension = _rushing ? Math.Min(_travelDistance * 0.18f, 100f) : 0f;
        // Open side arcs surround the body without painting over its central details.
        foreach (float side in new[] { -1f, 1f })
        {
            var points = new Vector2[25];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i / 24f * Mathf.Pi;
                float along = Mathf.Cos(angle) * width - extension * (1f - Mathf.Cos(angle)) * 0.5f;
                float across = Mathf.Sin(angle) * height * side;
                points[i] = ToLocal(center + _direction * along + normal * across);
            }
            DrawPolyline(points, new Color(0.266667f, 0.988235f, 0.772549f, _energy * 0.28f), 15f, true);
            DrawPolyline(points, new Color(0.52f, 1f, 0.84f, _energy * 0.8f), 5f, true);
            DrawPolyline(points, new Color(0.94f, 1f, 0.96f, _energy * 0.9f), 1.5f, true);
        }
        if (_impactPulse > 0f)
        {
            var ring = new Vector2[33];
            Vector2 nose = center + _direction * width;
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = i / 32f * Mathf.Tau;
                ring[i] = ToLocal(nose + _direction * Mathf.Cos(angle) * height * 0.3f
                    + normal * Mathf.Sin(angle) * height * (0.6f + _impactPulse * 0.4f));
            }
            DrawPolyline(ring, new Color(0.85f, 1f, 0.94f, _impactPulse), 5f, true);
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
