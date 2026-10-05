#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using ShinGetterMod.Audio;
using ShinGetterMod.Models.Powers;

namespace ShinGetterMod.Nodes.Combat;

internal partial class NShinGetterStarSlashSequence : Node2D
{
    private const string OwnerMeta = "shin_getter_star_slash_owner";
    private const float CleaveSpeed = 1.5f;
    private readonly TaskCompletionSource<bool> _ended = new();
    private AnimatedSprite2D _sprite = null!;
    private Player _player = null!;
    private NShinGetterStarSlashData _data = null!;
    private Action<AnimatedSprite2D> _ensureIdle = null!;
    private Task _prepared = Task.CompletedTask;
    private Task _preparationVoice = Task.CompletedTask;
    private TaskCompletionSource<bool>? _selectionReady;
    private TaskCompletionSource<bool>? _phaseDone;
    private float _phaseFrom;
    private float _phaseTo;
    private float _phaseElapsed;
    private float _phaseDuration;
    private float _clock;
    private float _confirmedAt;
    private float _voiceDuration;
    private float _poseTime;
    private bool _closed;
    private bool _warnedInvalidPolygon;
    private int _foregroundFrame = -1;
    private bool _foregroundFlipH;
    private bool _foregroundFlipV;
    private bool _foregroundCentered;
    private Vector2 _foregroundOffset;
    private Texture2D? _foregroundTexture;
    private Vector2[] _foregroundVertices = Array.Empty<Vector2>();
    private Vector2[] _foregroundUvs = Array.Empty<Vector2>();
    private int[] _foregroundIndices = Array.Empty<int>();

    public static NShinGetterStarSlashSequence? TryCreate(Player player)
    {
        if (NonInteractiveMode.IsActive || CombatManager.Instance.IsOverOrEnding
            || player.Creature.IsDead || SaveManager.Instance.PrefsSave.FastMode == FastModeType.Instant)
            return null;
        bool dragon = player.Creature.HasPower<SGP_ShinForm>();
        if (!dragon && !player.Creature.HasPower<SGP_ShinGetterOne>()) return null;
        var node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        var sprite = node?.Visuals.GetNodeOrNull<AnimatedSprite2D>(dragon ? "Visuals/ShinDragon" : "Visuals/GetterOne");
        if (sprite == null || !NShinGetterShiningSparkSequence.IsActuallyVisible(sprite)
            || IsControlling(sprite) || NShinGetterShiningSparkSequence.IsControlling(sprite)) return null;
        string directory = dragon ? NShinGetterSpriteSequence.ShinDragonStarSlashFrameDirectory
            : NShinGetterSpriteSequence.GetterOneStarSlashFrameDirectory;
        NShinGetterStarSlashData? data = NShinGetterStarSlashData.Load(directory);
        if (data == null) return null;
        Action<AnimatedSprite2D, string> loader = dragon
            ? NShinGetterSpriteSequence.EnsureShinDragonLoaded : NShinGetterSpriteSequence.EnsureLoaded;
        NShinGetterSpriteAnimationStateMachine.QueueNextActionSpeed(sprite, 1f);
        if (!NShinGetterSpriteAnimationStateMachine.TryPlay(sprite, "StarSlash", loader)
            || sprite.Animation != NShinGetterSpriteSequence.StarSlashAnimationName) return null;
        sprite.SetFrameAndProgress(0, 0f);
        sprite.Pause();
        var sequence = new NShinGetterStarSlashSequence
        {
            Name = "ShinGetterStarSlashSequence", _sprite = sprite, _player = player, _data = data,
            _ensureIdle = dragon ? NShinGetterSpriteSequence.EnsureShinDragonIdleLoaded
                : NShinGetterSpriteSequence.EnsureIdleLoaded,
        };
        sprite.SetMeta(OwnerMeta, sequence.GetInstanceId());
        sprite.AddChild(sequence); // Visual-only ownership; never move Creature or its layout node.
        sequence._prepared = sequence.Phase(0f, data.HoldTime);
        return sequence;
    }

    public void Confirm(float voiceDuration)
    {
        _confirmedAt = _clock;
        _voiceDuration = Math.Max(0f, voiceDuration);
    }

    public Task WaitForSelection(Task preparationVoice)
    {
        if (_closed) return Task.CompletedTask;
        _preparationVoice = preparationVoice;
        _selectionReady = new TaskCompletionSource<bool>();
        return _selectionReady.Task;
    }

    public async Task PlayToImpact()
    {
        await Task.WhenAny(_prepared, _ended.Task);
        if (_closed) return;
        await _prepared; // A fast confirmation still completes the real raise once.
        // Confirmation audio started before Exhaust hooks. Account for their elapsed visual time.
        float hold = Mathf.Clamp(_voiceDuration - (_clock - _confirmedAt)
            - (_data.ImpactTime - _data.HoldTime) / CleaveSpeed, 0f, 2f);
        if (hold > 0f) await Hold(hold);
        if (!_closed) await Phase(_data.HoldTime, _data.ImpactTime,
            (_data.ImpactTime - _data.HoldTime) / CleaveSpeed);
    }

    public async Task Recover()
    {
        if (_closed) return;
        await Phase(_data.ImpactTime, _data.TotalTime, (_data.TotalTime - _data.ImpactTime) / CleaveSpeed);
        Close(); // Return to idle even if damage hooks are still completing.
    }

    private Task Phase(float from, float to, float duration = -1f)
    {
        if (_closed || !CanContinue()) { Close(); return Task.CompletedTask; }
        _phaseFrom = from;
        _phaseTo = to;
        _phaseElapsed = 0f;
        _phaseDuration = duration >= 0f ? duration : Math.Max(0.001f, to - from);
        _phaseDone = new TaskCompletionSource<bool>();
        SetPose(from);
        return _phaseDone.Task;
    }

    private Task Hold(float seconds) => Phase(_data.HoldTime, _data.HoldTime, seconds);

    public override void _Process(double delta)
    {
        if (_closed) return;
        if (!CanContinue() || SaveManager.Instance.PrefsSave.FastMode == FastModeType.Instant)
        { Close(); return; }
        if (CombatManager.Instance.IsPaused) return;
        _clock += (float)delta;
        if (_phaseDone is { Task.IsCompleted: false })
        {
            _phaseElapsed += (float)delta;
            float progress = Math.Min(1f, _phaseElapsed / _phaseDuration);
            float time = Mathf.Lerp(_phaseFrom, _phaseTo, progress);
            SetPose(time);
            if (progress >= 1f)
            {
                if (_phaseTo == _data.HoldTime) _sprite.Frame = _data.HoldFrame;
                _phaseDone.TrySetResult(true);
            }
        }
        if (_prepared.IsCompleted && _preparationVoice.IsCompleted)
            _selectionReady?.TrySetResult(true);
    }

    private void SetPose(float time)
    {
        _poseTime = time;
        int frame = Math.Abs(time - _data.HoldTime) < 0.00001f ? _data.HoldFrame : _data.FrameAt(time);
        _sprite.SetFrameAndProgress(frame, 0f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_closed || !OwnsSprite()) return;
        var frame = _data.Frames[_sprite.Frame];
        // Opaque normal blending covers the original weapon; additive-only light cannot replace it.
        Color core = new(37f / 255f, 219f / 255f, 103f / 255f, 1f);
        NShinGetterStarSlashData.WeaponGeometry geometry =
            NShinGetterStarSlashData.BuildWeapon(frame, _poseTime, _data.HoldTime);
        Vector2[][] opaque = frame.WeaponCover.Concat(geometry.Blades).Append(geometry.Shaft)
            .Select(polygon => polygon.Select(Local).ToArray()).ToArray();
        foreach (Vector2[] polygon in opaque) DrawTriangulated(polygon, core);
        foreach (Vector2[] light in geometry.BladeLights)
            DrawTriangulated(light.Select(Local).ToArray(), new Color(128f / 255f, 1f, 183f / 255f, 1f));
        // Union is only for the outer edge; concave crescent cores stay opaque and are not convex-hulled.
        foreach (Vector2[] outline in MergeOutline(opaque))
            DrawPolyline(outline.Append(outline[0]).ToArray(), new Color(61f / 255f, 1f, 144f / 255f, 70f / 255f), 9f, true);
        DrawPolyline(geometry.Centerline.Select(Local).ToArray(),
            new Color(222f / 255f, 1f, 229f / 255f, 1f), 3.2f, true);
        UpdateForeground(frame);
        if (_foregroundTexture != null && _foregroundIndices.Length > 0)
            DrawTriangles(_foregroundVertices, _foregroundIndices, Colors.White, _foregroundUvs, _foregroundTexture);
    }

    private void UpdateForeground(NShinGetterStarSlashData.Frame frame)
    {
        if (_foregroundFrame == _sprite.Frame && _foregroundFlipH == _sprite.FlipH
            && _foregroundFlipV == _sprite.FlipV && _foregroundCentered == _sprite.Centered
            && _foregroundOffset == _sprite.Offset) return;
        _foregroundFrame = _sprite.Frame;
        _foregroundFlipH = _sprite.FlipH;
        _foregroundFlipV = _sprite.FlipV;
        _foregroundCentered = _sprite.Centered;
        _foregroundOffset = _sprite.Offset;
        _foregroundTexture = null;
        _foregroundIndices = Array.Empty<int>();
        Texture2D? texture = _sprite.SpriteFrames?.GetFrameTexture(_sprite.Animation, _sprite.Frame);
        if (texture == null) return;
        // Triangle arrays sample a texture RID: unwrap the AtlasTexture and map the frame's pixels
        // into its sheet region explicitly, rather than treating frame-local UVs as whole-sheet UVs.
        Texture2D foregroundTexture = texture;
        Vector2 uvOrigin = Vector2.Zero;
        if (texture is AtlasTexture atlas && atlas.Atlas != null)
        {
            foregroundTexture = atlas.Atlas;
            uvOrigin = atlas.Region.Position;
        }
        Vector2 foregroundSize = foregroundTexture.GetSize();
        var vertices = new List<Vector2>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();
        // Armor that was in front of the source handle stays in front; fingers are last.
        foreach (Vector2[] foreground in frame.BodyForeground.Concat(frame.Hands))
        {
            Vector2[] polygon = NShinGetterStarSlashData.CleanPolygon(foreground);
            int[] triangles = Triangulate(polygon);
            if (triangles.Length == 0) continue;
            int offset = vertices.Count;
            vertices.AddRange(polygon.Select(Local));
            uvs.AddRange(polygon.Select(point => NShinGetterStarSlashData.ForegroundUv(
                point, uvOrigin, foregroundSize)));
            indices.AddRange(triangles.Select(index => index + offset));
        }
        _foregroundTexture = foregroundTexture;
        _foregroundVertices = vertices.ToArray();
        _foregroundUvs = uvs.ToArray();
        _foregroundIndices = indices.ToArray();
    }

    private int[] Triangulate(Vector2[] polygon)
    {
        int[] indices = polygon.Length >= 3 ? Geometry2D.TriangulatePolygon(polygon) : Array.Empty<int>();
        if (indices.Length == 0 && !_warnedInvalidPolygon)
        {
            _warnedInvalidPolygon = true;
            GD.PushWarning($"Star Slash polygon rejected at frame {_sprite.Frame}; native render acceptance required.");
        }
        return indices;
    }

    private void DrawTriangulated(Vector2[] polygon, Color color)
    {
        Vector2[] vertices = NShinGetterStarSlashData.CleanPolygon(polygon);
        int[] indices = Triangulate(vertices);
        if (indices.Length > 0) DrawTriangles(vertices, indices, color, Array.Empty<Vector2>(), null);
    }

    private void DrawTriangles(Vector2[] vertices, int[] indices, Color color, Vector2[] uvs, Texture2D? texture)
    {
        RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), indices, vertices, new[] { color },
            uvs, Array.Empty<int>(), Array.Empty<float>(), texture?.GetRid() ?? default, -1);
    }

    private static Vector2[][] MergeOutline(Vector2[][] polygons)
    {
        var outlines = polygons.ToList();
        // Godot4.5.1 MergePolygons performs Union, retaining a concave outer boundary.
        // Disjoint regions are kept separate; metadata must not be concealed with a broad hull.
        bool merged;
        do
        {
            merged = false;
            for (int left = 0; left < outlines.Count && !merged; left++)
            for (int right = left + 1; right < outlines.Count; right++)
            {
                var union = Geometry2D.MergePolygons(outlines[left], outlines[right]);
                if (union.Count != 1) continue;
                outlines[left] = union[0];
                outlines.RemoveAt(right);
                merged = true;
                break;
            }
        } while (merged);
        return outlines.ToArray();
    }

    private Vector2 Local(Vector2 point)
    {
        if (_sprite.FlipH) point.X = 720f - point.X;
        if (_sprite.FlipV) point.Y = 720f - point.Y;
        return point + _sprite.Offset - (_sprite.Centered ? new Vector2(360f, 360f) : Vector2.Zero);
    }

    private bool CanContinue() => OwnsSprite() && _sprite.IsInsideTree()
        && NShinGetterShiningSparkSequence.IsActuallyVisible(_sprite) && !_player.Creature.IsDead
        && _sprite.Animation == NShinGetterSpriteSequence.StarSlashAnimationName
        && !CombatManager.Instance.IsOverOrEnding;

    internal static bool IsControlling(AnimatedSprite2D sprite) => sprite.HasMeta(OwnerMeta);
    private bool OwnsSprite() => GodotObject.IsInstanceValid(_sprite) && _sprite.HasMeta(OwnerMeta)
        && _sprite.GetMeta(OwnerMeta).AsUInt64() == GetInstanceId();

    public void Close()
    {
        End();
        if (GodotObject.IsInstanceValid(this) && IsInsideTree()) QueueFree();
    }

    public override void _ExitTree() => End();

    private void End()
    {
        if (_closed) return;
        _closed = true;
        ShinGetterVoiceService.FinishStarSlashPreparation(_player);
        if (OwnsSprite())
        {
            _sprite.RemoveMeta(OwnerMeta);
            if (_sprite.IsInsideTree() && _sprite.IsVisibleInTree() && !_player.Creature.IsDead
                && _sprite.Animation == NShinGetterSpriteSequence.StarSlashAnimationName
                && CombatManager.Instance.IsInProgress)
                NShinGetterSpriteAnimationStateMachine.PlayIdle(_sprite, _ensureIdle);
        }
        _phaseDone?.TrySetResult(false);
        _selectionReady?.TrySetResult(false);
        _ended.TrySetResult(false);
        _foregroundTexture = null;
        _foregroundVertices = Array.Empty<Vector2>();
        _foregroundUvs = Array.Empty<Vector2>();
        _foregroundIndices = Array.Empty<int>();
    }
}
