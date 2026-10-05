#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

namespace ShinGetterMod.Nodes.Combat;

internal sealed class NShinGetterStarSlashData
{
    private static readonly Dictionary<string, NShinGetterStarSlashData> Loaded = new(StringComparer.Ordinal);

    internal sealed record Frame(float Duration, Vector2 Grip, Vector2 Axis,
        Vector2[][] WeaponCover, Vector2[][] BladeCover, Vector2[][] HandleCover,
        Vector2[][] BodyForeground, Vector2[][] Hands);

    internal sealed record WeaponGeometry(Vector2[][] Blades, Vector2[][] BladeLights,
        Vector2[] Shaft, Vector2[] Centerline);

    public Frame[] Frames { get; }
    public float HoldTime { get; }
    public float ImpactTime { get; }
    public float TotalTime { get; }
    public int HoldFrame { get; }

    private NShinGetterStarSlashData(JsonElement root)
    {
        HoldFrame = root.GetProperty("hold_frame").GetInt32();
        ImpactTime = root.GetProperty("impact_time").GetSingle();
        Frames = root.GetProperty("frames").EnumerateArray().Select(frame => new Frame(
            frame.GetProperty("duration_seconds").GetSingle(),
            Point(frame.GetProperty("grip")), Point(frame.GetProperty("axis")),
            Polygons(frame.GetProperty("weapon_cover")), Polygons(frame.GetProperty("blade_cover")),
            Polygons(frame.GetProperty("handle_cover")),
            OptionalPolygons(frame, "body_foreground"),
            Polygons(frame.GetProperty("hands")))).ToArray();
        if (Frames.Length is not (76 or 71) || HoldFrame < 0 || HoldFrame >= Frames.Length
            || Frames.Any(frame => frame.Duration <= 0f || !float.IsFinite(frame.Duration)
                || !IsFinite(frame.Grip) || !IsFinite(frame.Axis)
                || frame.Grip.DistanceTo(frame.Axis) < 1f || frame.WeaponCover.Length == 0 || frame.BladeCover.Length == 0
                || frame.HandleCover.Length == 0 || frame.Hands.Length == 0))
            throw new InvalidOperationException("Invalid Star Slash frame/weapon contract.");
        HoldTime = Frames.Take(HoldFrame + 1).Sum(frame => frame.Duration);
        TotalTime = Frames.Sum(frame => frame.Duration);
        if (Math.Abs(TotalTime - 2.4f) > 0.0001f || !float.IsFinite(ImpactTime)
            || ImpactTime <= HoldTime || ImpactTime >= TotalTime)
            throw new InvalidOperationException("Invalid Star Slash phase times.");
    }

    public static NShinGetterStarSlashData? Load(string directory)
    {
        string path = directory + "/animation.json";
        if (!FileAccess.FileExists(path)) return null;
        // Parsed metadata is read-only for the process; resource replacement requires a restart.
        lock (Loaded)
        {
            if (Loaded.TryGetValue(path, out NShinGetterStarSlashData? cached)) return cached;
            try
            {
                using JsonDocument document = JsonDocument.Parse(FileAccess.GetFileAsString(path));
                var data = new NShinGetterStarSlashData(document.RootElement);
                Loaded.Add(path, data);
                return data;
            }
            catch (Exception error)
            {
                GD.PushWarning($"Star Slash metadata rejected: {path}: {error.Message}");
                return null;
            }
        }
    }

    public int FrameAt(float time)
    {
        float end = 0f;
        for (int index = 0; index < Frames.Length; index++)
        {
            end += Frames[index].Duration;
            if (time < end - 0.00001f) return index;
        }
        return Frames.Length - 1;
    }

    private static Vector2 Point(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle());

    internal static Vector2 ForegroundUv(Vector2 point, Vector2 origin, Vector2 textureSize) =>
        (point + origin) / textureSize;

    internal static Vector2[] CleanPolygon(Vector2[] polygon)
    {
        const float epsilon = 0.001f;
        var points = new List<Vector2>();
        foreach (Vector2 point in polygon)
            if (points.Count == 0 || points[^1].DistanceTo(point) > epsilon) points.Add(point);
        if (points.Count > 1 && points[0].DistanceTo(points[^1]) <= epsilon) points.RemoveAt(points.Count - 1);
        // Remove only subpixel duplicate/straight vertices, never hull the concave blade or matte.
        bool removed;
        do
        {
            removed = false;
            for (int index = 0; index < points.Count && points.Count > 3; index++)
            {
                Vector2 a = points[(index + points.Count - 1) % points.Count];
                Vector2 b = points[index];
                Vector2 c = points[(index + 1) % points.Count];
                double cross = ((double)b.X - a.X) * ((double)c.Y - a.Y)
                    - ((double)b.Y - a.Y) * ((double)c.X - a.X);
                // A distance tolerance on a long edge can remove real contour area.
                if (cross != 0d || (b - a).Dot(c - b) < 0f) continue;
                points.RemoveAt(index);
                removed = true;
                break;
            }
        } while (removed);
        return points.ToArray();
    }

    private static bool IsFinite(Vector2 point) => float.IsFinite(point.X) && float.IsFinite(point.Y);

    private static Vector2[][] OptionalPolygons(JsonElement frame, string property) =>
        frame.TryGetProperty(property, out JsonElement value) ? Polygons(value) : Array.Empty<Vector2[]>();

    // Scene-403-01 reference: asymmetric curved lobes and a smoothly tapered shaft.
    // Keep concavity: a convex hull turns the crescent into the rejected flat placeholder.
    // Pure managed geometry also drives the offline diagnostic; no Creature layout mutation.
    internal static WeaponGeometry BuildWeapon(Frame frame, float poseTime, float holdTime)
    {
        Vector2 grip = frame.Grip;
        Vector2 axis = (frame.Axis - grip).Normalized();
        Vector2 normal = new(-axis.Y, axis.X);
        Vector2[] head = frame.BladeCover.SelectMany(polygon => polygon).ToArray();
        float growth = Math.Clamp((poseTime - 0.4f) / Math.Max(0.01f, holdTime - 0.4f), 0f, 1f);
        float recovery = Math.Clamp((poseTime - 1.55f) / 0.85f, 0f, 1f);
        float length = (360f + growth * 620f) * (1f - recovery * 0.65f);
        float root = Math.Max(32f, head.Min(point => (point - grip).Dot(axis)) - 8f);
        float span = Math.Max(140f, length - root);
        float width = Math.Max(Math.Clamp(0.22f * length, 80f, 210f),
            head.Max(point => Math.Abs((point - grip).Dot(normal))) + 12f);
        Vector2 Map(float along, float across) => grip + axis * along + normal * across;

        Vector2[][] segments =
        {
            new[] { new Vector2(0.08f, 1.2f), new Vector2(0.36f, 1.18f), new Vector2(0.9f, 0.45f) },
            new[] { new Vector2(0.975f, 0.12f), new Vector2(1.03f, 0.025f), new Vector2(1.05f, 0f) },
            new[] { new Vector2(0.88f, 0.12f), new Vector2(0.64f, 0.47f), new Vector2(0.45f, 0.50f) },
            new[] { new Vector2(0.26f, 0.40f), new Vector2(0.10f, 0.08f), Vector2.Zero },
        };
        Vector2[] Lobe(float lengthScale, float widthScale, float side)
        {
            var points = new List<Vector2> { Map(root, 0f) };
            Vector2 start = Vector2.Zero;
            foreach (Vector2[] segment in segments)
            {
                for (int step = 1; step <= 18; step++)
                {
                    float t = step / 18f, inverse = 1f - t;
                    Vector2 point = start * (inverse * inverse * inverse)
                        + segment[0] * (3f * inverse * inverse * t)
                        + segment[1] * (3f * inverse * t * t) + segment[2] * (t * t * t);
                    points.Add(Map(root + point.X * span * lengthScale, point.Y * width * widthScale * side));
                }
                start = segment[2];
            }
            points.RemoveAt(points.Count - 1); // implicit polygon closure; no duplicate root vertex.
            return points.ToArray();
        }

        float tail = Math.Min(-90f, frame.HandleCover.SelectMany(polygon => polygon)
            .Min(point => (point - grip).Dot(axis)));
        var stations = new (float X, float Width)[] { (tail, 0f), (tail + 18f, 2f), (tail + 55f, 4.2f),
            (-45f, 5.2f), (0f, 5.5f), (root + 28f, 6.3f), (length * 0.84f, 2.8f), (length + 10f, 0f) }
            .Where(station => station.X >= tail && station.X <= length + 10f)
            .GroupBy(station => station.X).Select(group => (X: group.Key, Width: group.Max(station => station.Width)))
            .OrderBy(station => station.X).ToArray();
        var upper = new List<Vector2> { Map(tail, 0f) };
        var lower = new List<Vector2>();
        var centerline = new List<Vector2> { Map(tail + 18f, 0f) };
        for (int index = 1; index < stations.Length; index++)
        {
            var previous = stations[index - 1];
            var next = stations[index];
            for (int step = 1; step <= 8; step++)
            {
                float t = step / 8f;
                float along = previous.X + (next.X - previous.X) * t;
                float across = previous.Width + (next.Width - previous.Width) * (t * t * (3f - 2f * t));
                upper.Add(Map(along, across));
                lower.Add(Map(along, -across));
            }
            if (next.X < length + 10f) centerline.Add(Map(next.X, 0f));
        }
        lower.RemoveAt(lower.Count - 1); // same pointed tip as upper; do not duplicate it.
        lower.Reverse();
        Vector2[][] blades = { Lobe(1f, 1f, -1f), Lobe(0.8f, 0.82f, 1f) };
        return new WeaponGeometry(blades, blades.Select(blade => LightBand(blade, axis)).ToArray(),
            upper.Concat(lower).ToArray(), centerline.ToArray());
    }

    // Pair the arcs at the same axial coordinate, not the same Bezier parameter:
    // deeper crescent notches have different inner/outer control-point x distributions.
    // Light remains inside the ribbon without scaling about a centroid or filling its notch.
    private static Vector2[] LightBand(Vector2[] blade, Vector2 axis)
    {
        var outer = new List<Vector2> { blade[0] };
        var inner = new List<Vector2>();
        Vector2[] innerCurve = new[] { blade[0] }.Concat(blade.Skip(37).Reverse()).Append(blade[36]).ToArray();
        for (int index = 1; index < 36; index++)
        {
            Vector2 outerPoint = blade[index];
            float along = outerPoint.Dot(axis);
            int segment = 1;
            while (segment < innerCurve.Length - 1 && innerCurve[segment].Dot(axis) < along) segment++;
            Vector2 a = innerCurve[segment - 1], b = innerCurve[segment];
            float distance = (b - a).Dot(axis);
            float t = distance > 0.00001f ? Math.Clamp((along - a.Dot(axis)) / distance, 0f, 1f) : 0f;
            Vector2 innerPoint = a.Lerp(b, t);
            outer.Add(outerPoint.Lerp(innerPoint, 0.28f));
            inner.Add(outerPoint.Lerp(innerPoint, 0.72f));
        }
        outer.Add(blade[36]); // shared pointed tip
        inner.Reverse();
        return outer.Concat(inner).ToArray();
    }

    private static Vector2[][] Polygons(JsonElement value)
    {
        var polygons = new List<Vector2[]>();
        foreach (JsonElement polygon in value.EnumerateArray())
        {
            Vector2[] points = polygon.EnumerateArray().Select(Point).ToArray();
            if (points.Length < 3 || points.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y)))
                throw new InvalidOperationException("Invalid Star Slash coverage polygon.");
            polygons.Add(points);
        }
        return polygons.ToArray();
    }
}
