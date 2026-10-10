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
        var outlines = polygons.Select((polygon, index) => (polygon, index))
            .ToDictionary(item => item.index, item => item.polygon);
        Rect2[] bounds = polygons.Select(OutlineBounds).ToArray();
        Rect2[][] edges = polygons.Select(OutlineEdgeBounds).ToArray();
        bool[] rectangles = polygons.Select(IsAxisAlignedRectangle).ToArray();
        // -1: provably disjoint; 1: proved intersection; 0: native test needed.
        var relations = new int[polygons.Length, polygons.Length];
        var revisions = new int[polygons.Length];
        var pending = new PriorityQueue<(int Left, int Right, int LeftRevision, int RightRevision),
            (int Left, int Right)>();
        void Enqueue(int left, int right)
        {
            int relation = relations[left, right];
            if (relation == 0)
            {
                if (!bounds[left].Intersects(bounds[right], includeBorders: true)) relation = -1;
                else if (rectangles[left]) relation = RectangleRelation(bounds[left], outlines[right], edges[right]);
                else if (rectangles[right]) relation = RectangleRelation(bounds[right], outlines[left], edges[left]);
                relations[left, right] = relations[right, left] = relation;
            }
            if (relation == -1) return;
            pending.Enqueue((left, right, revisions[left], revisions[right]), (left, right));
        }
        for (int left = 0; left < polygons.Length; left++)
        for (int right = left + 1; right < polygons.Length; right++) Enqueue(left, right);
        // Godot4.5.1 MergePolygons performs Union, retaining a concave outer boundary.
        // Disjoint regions are kept separate; metadata must not be concealed with a broad hull.
        // Stable original indices preserve the old left/right order without rescanning unchanged pairs.
        while (pending.TryDequeue(out var pair, out _))
        {
            int left = pair.Left, right = pair.Right;
            if (!outlines.ContainsKey(left) || !outlines.ContainsKey(right)
                || revisions[left] != pair.LeftRevision || revisions[right] != pair.RightRevision) continue;
            var union = Geometry2D.MergePolygons(outlines[left], outlines[right]);
            using var nativeUnion = (Godot.Collections.Array)union;
            if (union.Count != 1) continue;
            // Union retains containment, and cannot reach a region separated from both operands.
            foreach (int other in outlines.Keys)
            {
                if (other == left || other == right) continue;
                int a = relations[left, other], b = relations[right, other];
                int relation = a == 1 || b == 1 ? 1 : a == -1 && b == -1 ? -1 : 0;
                relations[left, other] = relations[other, left] = relation;
            }
            Vector2[] contour = union[0];
            outlines[left] = contour;
            bounds[left] = OutlineBounds(contour);
            edges[left] = OutlineEdgeBounds(contour);
            rectangles[left] = IsAxisAlignedRectangle(contour);
            revisions[left]++;
            outlines.Remove(right);
            foreach (int other in outlines.Keys)
                if (other != left) Enqueue(Math.Min(left, other), Math.Max(left, other));
        }
        return outlines.OrderBy(item => item.Key).Select(item => item.Value).ToArray();
    }

    private static bool IsAxisAlignedRectangle(Vector2[] polygon)
    {
        if (polygon.Length != 4) return false;
        Rect2 rectangle = PolygonBounds(polygon);
        Vector2 end = rectangle.End;
        int corners = 0;
        foreach (Vector2 point in polygon)
        {
            int x = point.X == rectangle.Position.X ? 0 : point.X == end.X ? 1 : -1;
            int y = point.Y == rectangle.Position.Y ? 0 : point.Y == end.Y ? 1 : -1;
            if (x < 0 || y < 0) return false;
            corners |= 1 << (x + y * 2);
        }
        return corners == 15;
    }

    private static Rect2[] OutlineEdgeBounds(Vector2[] polygon)
    {
        var edges = new Rect2[polygon.Length];
        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 a = polygon[index], b = polygon[(index + 1) % polygon.Length];
            Vector2 minimum = a.Min(b), maximum = a.Max(b);
            edges[index] = new Rect2(minimum, maximum - minimum).Grow(0.001f);
        }
        return edges;
    }

    private static int RectangleRelation(Rect2 rectangle, Vector2[] polygon, Rect2[] edges)
    {
        // No boundary near this rectangle: it is either fully contained or fully disjoint.
        // Inflated edge boxes are conservative; the native union still handles all contacts.
        foreach (Rect2 edge in edges)
            if (rectangle.Intersects(edge, includeBorders: true)) return 0;
        return Geometry2D.IsPointInPolygon(rectangle.GetCenter(), polygon) ? 1 : -1;
    }

    private static Rect2 OutlineBounds(Vector2[] polygon) => PolygonBounds(polygon).Grow(0.001f);

    private static Rect2 PolygonBounds(Vector2[] polygon)
    {
        Vector2 minimum = polygon[0], maximum = polygon[0];
        foreach (Vector2 point in polygon)
        {
            minimum = minimum.Min(point);
            maximum = maximum.Max(point);
        }
        return new Rect2(minimum, maximum - minimum);
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
