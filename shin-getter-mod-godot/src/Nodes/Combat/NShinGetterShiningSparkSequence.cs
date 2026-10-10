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
    // Disjoint, equally timed stage ranges from the delivered 47-frame timing map.
    private const int DiscardComplete = 11;
    private const int ChargeStart = 12;
    private const int ChargeHold = 35;
    private const int RushStart = 36;
    private const int DashPeak = 44;
    private const int ImpactStart = 45;
    private const int EndHold = 46;
    private const string ManualOwnerMeta = "shin_getter_shining_spark_owner";
    private const string EnergyShaderPath = "res://shaders/shin_getter_shining_shell.gdshader";
    // Alpha >= 128 bounds from the approved 47 PNGs, in unflipped source pixels.
    private static readonly Vector4[] FrameBounds =
    {
        new(42, 117, 570, 685), new(50, 98, 571, 685), new(104, 29, 573, 685),
        new(120, 0, 563, 685), new(122, 0, 534, 685), new(123, 56, 570, 685),
        new(125, 156, 617, 685), new(123, 158, 562, 685), new(122, 158, 501, 702),
        new(119, 159, 501, 720), new(0, 159, 501, 720), new(0, 162, 501, 720),
        new(96, 165, 501, 685), new(90, 176, 511, 685), new(85, 193, 525, 685),
        new(85, 198, 526, 685), new(90, 180, 526, 682), new(114, 120, 543, 662),
        new(149, 54, 564, 628), new(152, 58, 559, 632), new(161, 64, 555, 638),
        new(164, 69, 553, 642), new(165, 75, 552, 648), new(165, 80, 553, 652),
        new(165, 86, 553, 659), new(165, 89, 553, 663), new(165, 90, 553, 663),
        new(165, 90, 553, 663), new(165, 90, 553, 663), new(165, 90, 553, 663),
        new(165, 90, 553, 663), new(165, 90, 553, 663), new(162, 94, 555, 664),
        new(158, 101, 556, 664), new(146, 131, 556, 660), new(138, 141, 555, 652),
        new(135, 143, 553, 648), new(132, 140, 550, 637), new(146, 127, 567, 634),
        new(173, 117, 612, 627), new(208, 114, 653, 606), new(119, 114, 664, 576),
        new(69, 114, 667, 559), new(66, 114, 667, 558), new(66, 114, 667, 558),
        new(66, 114, 667, 558), new(75, 159, 675, 556),
    };
    private readonly TaskCompletionSource<bool> _ended = new();
    private AnimatedSprite2D _sprite = null!;
    private Creature _owner = null!;
    private Vector2 _origin;
    private Vector2 _lunge;
    private Vector2 _recoil;
    private Vector2 _direction;
    private float _travelDistance;
    private readonly Sprite2D[] _tails = new Sprite2D[5];
    private readonly float[] _tailAges = new float[5];
    private int _tailCursor;
    private float _sampleTime;
    private bool _rushing;
    private float _impactPulse;
    private Polygon2D? _energy;
    private ShaderMaterial? _energyMaterial;
    private float _energyStrength;
    private TaskCompletionSource<bool>? _stageCompletion;
    private Action<float>? _stageUpdate;
    private float _stageDuration;
    private float _stageTime;
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
        var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        for (int i = 0; i < _tails.Length; i++)
        {
            _tailAges[i] = 1f;
            _tails[i] = new Sprite2D { Material = additive, Visible = false };
            AddChild(_tails[i]);
        }
        Shader? shader = ResourceLoader.Load<Shader>(EnergyShaderPath);
        if (shader != null)
        {
            _energyMaterial = new ShaderMaterial { Shader = shader };
            _energy = new Polygon2D
            {
                Name = "ShiningSparkEnergyShell",
                Material = _energyMaterial,
                ShowBehindParent = true,
                Visible = false,
            };
            _sprite.AddChild(_energy);
        }
    }

    public async Task PlayToImpact(Func<Task> intro, Func<Task> release)
    {
        // The dropped axe is already baked into these frames: never create a second prop.
        await Stage(0.4f, u => SetFrame(0, DiscardComplete, u));
        if (_closed) return;
        Task voice = intro();
        await Stage(1f, u =>
        {
            SetFrame(ChargeStart, ChargeHold, u);
            _energyStrength = 0.65f * u * u * (3f - 2f * u);
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
        _energyStrength = 1f;
        _sampleTime = 0.04f;
        await Stage(0.28f, u =>
        {
            SetFrame(RushStart, DashPeak, u);
            _sprite.Position = (_origin + _recoil).Lerp(_origin + _lunge, u * u * u);
        });
        _rushing = false;
        _energyStrength = 0.95f;
        await Stage(0.08f, u =>
        {
            SetFrame(ImpactStart, EndHold, u);
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
            SetFrame(EndHold, EndHold, 0f);
            _energyStrength = 0.95f * (1f - u);
            float smooth = u * u * (3f - 2f * u);
            _sprite.Position = start.Lerp(_origin, smooth);
        });
    }

    private Task Stage(float duration, Action<float> update)
    {
        if (_closed) return Task.CompletedTask;
        if (!OwnsSprite() || !IsActuallyVisible(_sprite) || _owner.IsDead
            || _sprite.Animation != NShinGetterSpriteSequence.ShiningSparkAnimationName
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
        UpdateEnergyShell();
        QueueRedraw();
    }

    private void UpdateEnergyShell()
    {
        if (_energy == null || _energyMaterial == null) return;
        Texture2D? frame = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        if (frame == null) return;
        Texture2D atlas = frame is AtlasTexture atlasFrame ? atlasFrame.Atlas : frame;
        Rect2 region = frame is AtlasTexture selected ? selected.Region : new Rect2(Vector2.Zero, frame.GetSize());
        Rect2 drawRect = GetFrameLocalRect(_sprite, opaqueBounds: false);
        Rect2 padded = drawRect.Grow(28f);
        _energy.Polygon = new[] { padded.Position, new Vector2(padded.End.X, padded.Position.Y),
            padded.End, new Vector2(padded.Position.X, padded.End.Y) };
        _energyMaterial.SetShaderParameter("frame_atlas", atlas);
        _energyMaterial.SetShaderParameter("atlas_size", atlas.GetSize());
        _energyMaterial.SetShaderParameter("frame_region", new Vector4(region.Position.X, region.Position.Y,
            region.Size.X, region.Size.Y));
        _energyMaterial.SetShaderParameter("frame_origin", drawRect.Position);
        _energyMaterial.SetShaderParameter("frame_size", drawRect.Size);
        _energyMaterial.SetShaderParameter("flipped", new Vector2(_sprite.FlipH ? 1f : 0f, _sprite.FlipV ? 1f : 0f));
        _energyMaterial.SetShaderParameter("strength", _energyStrength);
        _energy.Visible = _energyStrength > 0f;
    }

    internal static Rect2 GetFrameLocalRect(AnimatedSprite2D sprite, bool opaqueBounds = true)
    {
        Vector2 size = sprite.SpriteFrames?.GetFrameTexture(sprite.Animation, sprite.Frame)?.GetSize()
            ?? new Vector2(720f, 720f);
        Rect2 rect = new(Vector2.Zero, size);
        if (opaqueBounds && sprite.Animation == NShinGetterSpriteSequence.ShiningSparkAnimationName
            && sprite.Frame >= 0 && sprite.Frame < FrameBounds.Length)
        {
            Vector4 bounds = FrameBounds[sprite.Frame];
            rect = new Rect2(bounds.X, bounds.Y, bounds.Z - bounds.X, bounds.W - bounds.Y);
        }
        if (sprite.FlipH) rect.Position = new Vector2(size.X - rect.End.X, rect.Position.Y);
        if (sprite.FlipV) rect.Position = new Vector2(rect.Position.X, size.Y - rect.End.Y);
        rect.Position += sprite.Offset - (sprite.Centered ? size * 0.5f : Vector2.Zero);
        return rect;
    }

    internal static Rect2 TransformRect(Rect2 rect, Transform2D transform)
    {
        Rect2 result = new(transform * rect.Position, Vector2.Zero);
        result = result.Expand(transform * new Vector2(rect.End.X, rect.Position.Y));
        result = result.Expand(transform * rect.End);
        return result.Expand(transform * new Vector2(rect.Position.X, rect.End.Y));
    }

    private void SetFrame(int first, int last, float progress) =>
        _sprite.Frame = first + Math.Min(last - first, (int)Math.Floor((last - first + 1) * progress));

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
        if (_closed || _impactPulse <= 0f || !OwnsSprite()) return;
        Texture2D? texture = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        if (texture == null) return;
        Vector2 center = _sprite.ToGlobal(_sprite.Offset + new Vector2(0f, -30f));
        Vector2 normal = new(-_direction.Y, _direction.X);
        float width = texture.GetWidth() * _sprite.GlobalTransform.BasisXform(Vector2.Right).Length() * 0.31f;
        float height = texture.GetHeight() * _sprite.GlobalTransform.BasisXform(Vector2.Down).Length() * 0.35f;
        // Green energy and the jump are baked into the new clip; add only the impact cue.
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

    internal static bool IsActuallyVisible(AnimatedSprite2D sprite)
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
        if (_energy != null && GodotObject.IsInstanceValid(_energy))
        {
            _energy.Hide();
            _energy.QueueFree();
        }
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
