global using FileAccess = Godot.FileAccess;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using ShinGetterMod.Nodes.Combat;

internal static class Program
{
    private static int assertions;
    private static T Parse<T>(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { document.RootElement }, null)!;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        assertions++;
    }
    private static IEnumerable<Vector2> Points(NShinGetterStarSlashData.WeaponGeometry geometry) =>
        geometry.Blades.SelectMany(p => p).Concat(geometry.BladeLights.SelectMany(p => p))
            .Concat(geometry.Shaft).Concat(geometry.Centerline);
    private static IEnumerable<Vector2> Points(LegacyStarSlashData.WeaponGeometry geometry) =>
        geometry.Blades.SelectMany(p => p).Concat(geometry.BladeLights.SelectMany(p => p))
            .Concat(geometry.Shaft).Concat(geometry.Centerline);
    private static (double Ms, long Bytes) Measure(Action action)
    {
        long start = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        action();
        return (clock.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - start);
    }
    private static object ParseMeasurements<T>(string json)
    {
        _ = Parse<T>(json);
        var samples = Enumerable.Range(0, 7).Select(_ => Measure(() => Parse<T>(json))).OrderBy(x => x.Ms).ToArray();
        return new { medianMs = samples[3].Ms, allocatedBytes = samples[3].Bytes,
            scope = "JsonDocument plus actual production constructor; excludes engine GetFileAsString/native GPU" };
    }
    private static object DrawChecks(LegacyStarSlashData old, NShinGetterStarSlashData current)
    {
        var a = new LegacyStarSlashSequence(); var b = new NShinGetterStarSlashSequence();
        var oldSprite = new AnimatedSprite2D(); var sprite = new AnimatedSprite2D();
        void Set(object sequence, string field, object value) => sequence.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(sequence, value);
        void Setup(Node2D sequence, AnimatedSprite2D target, object data)
        {
            Set(sequence, "_sprite", target); Set(sequence, "_data", data);
            Set(sequence, "_player", new MegaCrit.Sts2.Core.Entities.Players.Player());
            Set(sequence, "_ensureIdle", (Action<AnimatedSprite2D>)(_ => { }));
            target.SetMeta("shin_getter_star_slash_owner", sequence.GetInstanceId());
        }
        Setup(a, oldSprite, old); Setup(b, sprite, current);
        int cases = 0, referenceTriangles = 0, cachedTriangles = 0, referenceUnions = 0, cachedUnions = 0;
        long referenceDrawBytes = 0, cachedDrawBytes = 0;
        (int Triangles, int Unions) Compare()
        {
            DrawProbe.Clear(); long start = GC.GetAllocatedBytesForCurrentThread(); a._Draw();
            referenceDrawBytes += GC.GetAllocatedBytesForCurrentThread() - start;
            string[] expected = DrawProbe.Draws.ToArray();
            referenceTriangles += DrawProbe.Triangulations; referenceUnions += DrawProbe.Unions;
            DrawProbe.Clear(); start = GC.GetAllocatedBytesForCurrentThread(); b._Draw();
            cachedDrawBytes += GC.GetAllocatedBytesForCurrentThread() - start;
            Check(expected.SequenceEqual(DrawProbe.Draws), "actual production draw submissions/order/UVs equal exact Git source (fake native results)");
            cachedTriangles += DrawProbe.Triangulations; cachedUnions += DrawProbe.Unions; cases++;
            return (DrawProbe.Triangulations, DrawProbe.Unions);
        }
        foreach (bool centered in new[] { true, false })
        foreach (Vector2 offset in new[] { Vector2.Zero, new Vector2(113.25f, -37.75f) })
        foreach (var flip in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            sprite.Centered = oldSprite.Centered = centered;
            sprite.Offset = oldSprite.Offset = offset;
            sprite.FlipH = oldSprite.FlipH = flip.Item1; sprite.FlipV = oldSprite.FlipV = flip.Item2;
            float elapsed = 0;
            for (int index = 0; index < current.Frames.Length; index++)
            {
                sprite.Frame = oldSprite.Frame = index;
                foreach (float pose in new[] { elapsed, elapsed + current.Frames[index].Duration * .5f })
                {
                    Set(a, "_poseTime", pose); Set(b, "_poseTime", pose);
                    Compare(); // Same PNG frame, different continuous pose must recompute geometry.
                    var repeat = Compare();
                    Check(repeat.Triangles == 0 && repeat.Unions == 0, "unchanged pose redraw reuses all prepared geometry");
                }
                elapsed += current.Frames[index].Duration;
            }
        }
        var cacheCounts = new Dictionary<string, int>();
        long cachedArrayPayloadBytes = 0;
        foreach (string field in new[] { "_staticMeshes", "_foregroundMeshes" })
        {
            object cache = b.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(b)!;
            cacheCounts[field] = (int)cache.GetType().GetProperty("Count")!.GetValue(cache)!;
            foreach (object mesh in (System.Collections.IEnumerable)cache.GetType().GetProperty("Values")!.GetValue(cache)!)
            {
                var t = mesh.GetType();
                var source = (Vector2[])t.GetProperty("Source")!.GetValue(mesh)!;
                var vertices = (Vector2[])t.GetProperty("Vertices")!.GetValue(mesh)!;
                var indices = (int[])t.GetProperty("Indices")!.GetValue(mesh)!;
                cachedArrayPayloadBytes += source.Length * 8L + vertices.Length * 8L + indices.Length * 4L;
            }
        }
        b.Close();
        foreach (string field in new[] { "_staticMeshes", "_foregroundMeshes" })
        {
            object cache = b.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(b)!;
            Check((int)cache.GetType().GetProperty("Count")!.GetValue(cache)! == 0, "exit clears sequence-owned polygon cache");
        }
        foreach (string field in new[] { "_weaponCore", "_weaponLight", "_weaponOutline", "_weaponCenterline" })
            Check(((Array)b.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(b)!).Length == 0,
                "exit releases prepared draw arrays");
        DrawProbe.Clear(); b._Draw(); Check(DrawProbe.Draws.Count == 0, "closed sequence draws nothing");
        return new { cases, referenceTriangles, cachedTriangles, referenceUnions, cachedUnions,
            referenceDrawBytes, cachedDrawBytes, cacheCounts, cachedArrayPayloadBytes,
            scope = "production-linked sequence, actual GodotSharp vectors; fake native triangulation/union/draw API, not GPU acceptance" };
    }
    public static int Main(string[] args)
    {
        try
        {
            var results = new List<object>();
            foreach (string action in new[] { "getter_one_star_slash", "shin_getter_dragon_star_slash" })
            {
                string oldJson = File.ReadAllText($"tests/Issue255StarSlashMetadata/obj/{action}.json");
                string json = File.ReadAllText($"shin-getter-mod-godot/images/characters/shin_getter/forms/{action}/animation.json");
                var old = Parse<LegacyStarSlashData>(oldJson);
                var current = Parse<NShinGetterStarSlashData>(json);
                Check(old.HoldFrame == current.HoldFrame && old.HoldTime == current.HoldTime
                    && old.TotalTime == current.TotalTime && old.ImpactTime == current.ImpactTime, "exact phase/timing equality");
                Check(old.Frames.Length == current.Frames.Length, "exact frame count");
                int poses = 0, changedInsideFrame = 0, removableVertices = 0;
                float elapsed = 0;
                for (int index = 0; index < current.Frames.Length; index++)
                {
                    var a = old.Frames[index]; var b = current.Frames[index];
                    Check(a.Duration == b.Duration && a.Grip == b.Grip && a.Axis == b.Axis, "frame anchors and weight");
                    var legacyLayers = new[] { a.WeaponCover, a.BladeCover, a.HandleCover, a.BodyForeground, a.Hands };
                    var layers = new[] { b.WeaponCover, b.BladeCover, b.HandleCover, b.BodyForeground, b.Hands };
                    for (int layer = 0; layer < layers.Length; layer++)
                    {
                        Check(legacyLayers[layer].Length == layers[layer].Length, "polygon count/order");
                        for (int p = 0; p < layers[layer].Length; p++)
                        {
                            Check(legacyLayers[layer][p].SequenceEqual(layers[layer][p]), "bit-exact parsed float32 contour");
                            Vector2[] cleaned = LegacyStarSlashData.CleanPolygon(legacyLayers[layer][p]);
                            removableVertices += legacyLayers[layer][p].Length - cleaned.Length;
                            Check(cleaned.SequenceEqual(NShinGetterStarSlashData.CleanPolygon(layers[layer][p])), "exact foreground cleaner inputs/outputs");
                            foreach (var mirror in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                            {
                                Vector2 shift = new(113.25f, -360f);
                                var x = legacyLayers[layer][p].Select(v => v * mirror + shift).ToArray();
                                var y = layers[layer][p].Select(v => v * mirror + shift).ToArray();
                                Check(LegacyStarSlashData.CleanPolygon(x).SequenceEqual(NShinGetterStarSlashData.CleanPolygon(y)),
                                    "exact local-before-clean weapon inputs across H/V/HV");
                            }
                        }
                    }
                    Vector2[]? first = null;
                    for (int sample = 0; sample <= 8; sample++)
                    {
                        float pose = elapsed + b.Duration * (sample / 8f);
                        Check(old.FrameAt(pose) == current.FrameAt(pose), "weighted lookup unchanged");
                        Vector2[] expected = Points(LegacyStarSlashData.BuildWeapon(a, pose, old.HoldTime)).ToArray();
                        Vector2[] actual = Points(NShinGetterStarSlashData.BuildWeapon(b, pose, current.HoldTime)).ToArray();
                        Check(expected.SequenceEqual(actual), $"exact continuous weapon geometry {action}:{index}:{sample}");
                        if (sample == 0) first = actual;
                        if (sample == 8 && !first!.SequenceEqual(actual)) changedInsideFrame++;
                        poses++;
                    }
                    elapsed += b.Duration;
                }
                Check(changedInsideFrame > 0, "continuous growth/recovery is not a frame-only cache");
                var refs = current.Frames.SelectMany(f => new[] { f.WeaponCover, f.BladeCover, f.HandleCover, f.BodyForeground, f.Hands })
                    .SelectMany(layer => layer).ToArray();
                Check(refs.Distinct(ReferenceEqualityComparer.Instance).Count() < refs.Length, "reader really interns polygon arrays");
                // Same production kernel on both sides; measurements are managed CPU/allocation only.
                var oldBuild = Measure(() => { foreach (var frame in old.Frames) for (int n = 0; n < 10; n++)
                    LegacyStarSlashData.BuildWeapon(frame, 0.75f + n * .07f, old.HoldTime); });
                var newBuild = Measure(() => { foreach (var frame in current.Frames) for (int n = 0; n < 10; n++)
                    NShinGetterStarSlashData.BuildWeapon(frame, 0.75f + n * .07f, current.HoldTime); });
                Check(newBuild.Bytes < oldBuild.Bytes, "static projections remove per-pose flattening allocation");
                foreach (Action<JsonObject> mutate in new Action<JsonObject>[] {
                    d => d["schema_version"] = 99,
                    d => d["frames"]![0]!["weapon_cover"]![0] = -1,
                    d => d["frames"]![0]!["hands"]![0] = int.MaxValue,
                    d => d["polygon_pool"]![0] = new JsonArray(new JsonArray(0, 0)),
                    d => d["frames"]![0]!["duration_seconds"] = 0,
                    d => d["hold_frame"] = -1,
                    d => d["impact_time"] = .1,
                    d => d["frames"]![0]!["handle_cover"] = new JsonArray(),
                    d => d["frames"]![0]!["blade_cover"] = new JsonArray(),
                    d => d["frames"]![0]!["weapon_cover"] = new JsonArray(),
                    d => d["frames"]![0]!["hands"] = new JsonArray(),
                })
                {
                    var broken = JsonNode.Parse(json)!.AsObject(); mutate(broken);
                    bool rejected = false;
                    try { Parse<NShinGetterStarSlashData>(broken.ToJsonString()); }
                    catch (TargetInvocationException) { rejected = true; }
                    Check(rejected, "actual reader rejects invalid schema/data");
                }
                object drawChecks = DrawChecks(old, current);
                results.Add(new { action, poses, changedInsideFrame, removableVertices, drawChecks,
                    oldParse = ParseMeasurements<LegacyStarSlashData>(oldJson),
                    currentParse = ParseMeasurements<NShinGetterStarSlashData>(json),
                    oldBuild = new { oldBuild.Ms, oldBuild.Bytes }, currentBuild = new { newBuild.Ms, newBuild.Bytes } });
            }
            string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            string output = JsonSerializer.Serialize(new { pass = true, assertions, results, nativeGpuAcceptance = false,
                sources = new { baselineRef = "02d97982c90d9741f7a98ec5ee16fdf9bb0c2bc9",
                    currentData = Hash("shin-getter-mod-godot/src/Nodes/Combat/NShinGetterStarSlashData.cs"),
                    currentSequence = Hash("shin-getter-mod-godot/src/Nodes/Combat/NShinGetterStarSlashSequence.cs") } });
            Console.WriteLine(output);
            if (args.Length == 1) File.WriteAllText(args[0], output + "\n");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
