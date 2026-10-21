using Godot;
using MegaCrit.Sts2.Core.Entities.Players;

internal static class DrawProbe
{
    internal static int Triangulations, Unions;
    internal static readonly List<string> Draws = new();
    internal static string Points(Vector2[] points) => string.Join(";", points.Select(p => $"{p.X:R},{p.Y:R}"));
    internal static void Clear() { Triangulations = Unions = 0; Draws.Clear(); }
}

namespace Godot
{
    public class GodotObject { public static bool IsInstanceValid(object? item) => item != null; }
    public class Node2D : GodotObject
    {
        private static ulong next;
        private readonly ulong id = ++next;
        private readonly Dictionary<string, ulong> meta = new();
        public string Name { get; set; } = "";
        public virtual void _Process(double delta) { }
        public virtual void _Draw() { }
        public virtual void _ExitTree() { }
        public bool IsInsideTree() => true;
        public bool IsVisibleInTree() => true;
        public void QueueFree() { }
        public void QueueRedraw() { }
        public ulong GetInstanceId() => id;
        public Rid GetCanvasItem() => default;
        public void SetMeta(string key, ulong value) => meta[key] = value;
        public bool HasMeta(string key) => meta.ContainsKey(key);
        public ProbeVariant GetMeta(string key) => new(meta[key]);
        public void RemoveMeta(string key) => meta.Remove(key);
        public void AddChild(Node2D child) { }
        public T? GetNodeOrNull<T>(string path) where T : class => null;
        public void DrawPolyline(Vector2[] points, Color color, float width, bool antialiased) =>
            DrawProbe.Draws.Add("line:" + DrawProbe.Points(points) + ":" + color + ":" + width);
    }
    public readonly record struct ProbeVariant(ulong Value) { public ulong AsUInt64() => Value; }
    public class AnimatedSprite2D : Node2D
    {
        public int Frame { get; set; }
        public bool FlipH { get; set; }
        public bool FlipV { get; set; }
        public bool Centered { get; set; } = true;
        public Vector2 Offset { get; set; }
        public string Animation { get; set; } = "StarSlash";
        public SpriteFrames SpriteFrames { get; set; } = new();
        public void SetFrameAndProgress(int frame, float progress) => Frame = frame;
        public void Pause() { }
    }
    public class Texture2D { public Vector2 GetSize() => new(7200, 7200); public Rid GetRid() => default; }
    public class AtlasTexture : Texture2D
    {
        public Texture2D? Atlas { get; set; } = new();
        public Rect2 Region { get; set; }
    }
    public class SpriteFrames
    {
        public Texture2D GetFrameTexture(string animation, int frame) => new AtlasTexture
        { Region = new Rect2(new Vector2(frame % 10 * 720, frame / 10 * 720), new Vector2(720, 720)) };
    }
    public static class GD { public static void PushWarning(string text) { } }
    public static class RenderingServer
    {
        public static void CanvasItemAddTriangleArray(Rid item, int[] indices, Vector2[] vertices,
            Color[] colors, Vector2[] uvs, int[] bones, float[] weights, Rid texture, int count) =>
            DrawProbe.Draws.Add("tri:" + string.Join(',', indices) + ":" + DrawProbe.Points(vertices)
                + ":" + string.Join(';', colors) + ":uv:" + DrawProbe.Points(uvs));
    }
    public static class Geometry2D
    {
        // Instrumentation only: fake native results, not triangulation/union acceptance.
        public static int[] TriangulatePolygon(Vector2[] polygon)
        {
            DrawProbe.Triangulations++;
            return Enumerable.Range(1, Math.Max(0, polygon.Length - 2)).SelectMany(n => new[] { 0, n, n + 1 }).ToArray();
        }
        public static Godot.Collections.Array<Vector2[]> MergePolygons(Vector2[] a, Vector2[] b)
        { DrawProbe.Unions++; return new(a, b); }
        public static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }
    }
}
namespace Godot.Collections
{
    public class Array : IDisposable { public void Dispose() { } }
    public class Array<T>(params T[] values) : Array
    { public int Count => values.Length; public T this[int index] => values[index]; }
}
namespace MegaCrit.Sts2.Core.Entities.Players
{
    public class Creature { public bool IsDead { get; set; } public bool HasPower<T>() => false; }
    public class Player { public Creature Creature { get; } = new(); }
}
namespace MegaCrit.Sts2.Core.Combat
{
    public class CombatManager
    {
        public static CombatManager Instance { get; } = new();
        public bool IsOverOrEnding => false;
        public bool IsPaused => false;
        public bool IsInProgress => true;
    }
}
namespace MegaCrit.Sts2.Core.Helpers { public static class NonInteractiveMode { public static bool IsActive => false; } }
namespace MegaCrit.Sts2.Core.Settings { public enum FastModeType { Normal, Instant } }
namespace MegaCrit.Sts2.Core.Saves
{
    public class PrefsSave { public MegaCrit.Sts2.Core.Settings.FastModeType FastMode => default; }
    public class SaveManager { public static SaveManager Instance { get; } = new(); public PrefsSave PrefsSave { get; } = new(); }
}
namespace MegaCrit.Sts2.Core.Nodes.Rooms
{
    public class CreatureNode { public Node2D Visuals { get; } = new(); }
    public class NCombatRoom { public static NCombatRoom? Instance => null; public CreatureNode? GetCreatureNode(Creature c) => null; }
}
namespace ShinGetterMod.Models.Powers { public class SGP_ShinForm { } public class SGP_ShinGetterOne { } }
namespace ShinGetterMod.Audio { public static class ShinGetterVoiceService { public static void FinishStarSlashPreparation(Player p) { } } }
namespace ShinGetterMod.Nodes.Combat
{
    public static class NShinGetterShiningSparkSequence
    { public static bool IsActuallyVisible(AnimatedSprite2D s) => true; public static bool IsControlling(AnimatedSprite2D s) => false; }
    public static class NShinGetterSpriteSequence
    {
        public const string ShinDragonStarSlashFrameDirectory = "dragon", GetterOneStarSlashFrameDirectory = "one", StarSlashAnimationName = "StarSlash";
        public static void EnsureShinDragonLoaded(AnimatedSprite2D s, string action) { }
        public static void EnsureLoaded(AnimatedSprite2D s, string action) { }
        public static void EnsureShinDragonIdleLoaded(AnimatedSprite2D s) { }
        public static void EnsureIdleLoaded(AnimatedSprite2D s) { }
    }
    public static class NShinGetterSpriteAnimationStateMachine
    {
        public static void QueueNextActionSpeed(AnimatedSprite2D s, float speed) { }
        public static bool TryPlay(AnimatedSprite2D s, string action, Action<AnimatedSprite2D, string> loader) => true;
        public static void PlayIdle(AnimatedSprite2D s, Action<AnimatedSprite2D> loader) { }
    }
}
