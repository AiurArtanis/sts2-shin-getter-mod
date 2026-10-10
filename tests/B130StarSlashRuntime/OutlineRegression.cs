using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using ShinGetterMod.Models.Cards;

namespace B130StarSlashRuntime;

internal static class OutlineRegression
{
    private static bool _profile;
    private static long _calls;
    private static long _vertices;
    private static long _nativeTicks;
    private static long _maxNativeTicks;
    private static void NativeBegin(Vector2[] __0, Vector2[] __1, out long __state)
    {
        __state = _profile ? Stopwatch.GetTimestamp() : 0;
        if (__state == 0) return;
        _calls++;
        _vertices += __0.Length + __1.Length;
    }
    private static void NativeEnd(long __state)
    {
        if (__state == 0) return;
        long ticks = Stopwatch.GetTimestamp() - __state;
        _nativeTicks += ticks;
        _maxNativeTicks = Math.Max(_maxNativeTicks, ticks);
    }
    private static void StartProfile()
    {
        _calls = _vertices = _nativeTicks = _maxNativeTicks = 0;
        _profile = true;
    }
    private static object EndProfile()
    {
        _profile = false;
        return new { calls = _calls, inputVertices = _vertices, nativeMs = _nativeTicks * 1000d / Stopwatch.Frequency,
            maxNativeMs = _maxNativeTicks * 1000d / Stopwatch.Frequency };
    }
    // Frozen d9367484 reference; invokes the same native Union as the production algorithm.
    private static Vector2[][] Reference(Vector2[][] polygons)
    {
        var outlines = polygons.ToList();
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

    internal static void Run(string root, string attempt, bool spot)
    {
        var profiler = new Harmony("test.b130.native-merge-profile");
        profiler.Patch(AccessTools.Method(typeof(Geometry2D), nameof(Geometry2D.MergePolygons), new[] { typeof(Vector2[]), typeof(Vector2[]) }),
            prefix: new HarmonyMethod(typeof(OutlineRegression), nameof(NativeBegin)),
            postfix: new HarmonyMethod(typeof(OutlineRegression), nameof(NativeEnd)));
        Assembly product = typeof(SGC_StarSlash).Assembly;
        Type dataType = product.GetType("ShinGetterMod.Nodes.Combat.NShinGetterStarSlashData", true)!;
        Type sequenceType = product.GetType("ShinGetterMod.Nodes.Combat.NShinGetterStarSlashSequence", true)!;
        MethodInfo load = dataType.GetMethod("Load")!;
        MethodInfo build = dataType.GetMethod("BuildWeapon", BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo merge = sequenceType.GetMethod("MergeOutline", BindingFlags.NonPublic | BindingFlags.Static)!;
        var results = new List<object>();
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Outline regression: " + message);
        }
        Vector2[][] Fixed(Vector2[][] polygons) => (Vector2[][])merge.Invoke(null, new object[] { polygons })!;
        void Compare(string label, Vector2[][] polygons)
        {
            Vector2[][] snapshot = polygons.Select(p => p.ToArray()).ToArray();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            StartProfile();
            Vector2[][] expected = Reference(polygons);
            object referenceProfile = EndProfile();
            double referenceMs = timer.Elapsed.TotalMilliseconds;
            long referenceBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            allocated = GC.GetAllocatedBytesForCurrentThread();
            timer.Restart();
            StartProfile();
            Vector2[][] actual = Fixed(polygons);
            object fixedProfile = EndProfile();
            double fixedMs = timer.Elapsed.TotalMilliseconds;
            long fixedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            if (expected.Length != actual.Length || expected.Where((path, index) => index >= actual.Length || !path.SequenceEqual(actual[index])).Any())
                System.IO.File.WriteAllText(System.IO.Path.Combine(root, "evidence", attempt + "-geometry-mismatch.json"),
                    JsonSerializer.Serialize(new { label, expected, actual }, new JsonSerializerOptions { IncludeFields = true }));
            Check(expected.Length == actual.Length, label + " contour count");
            for (int i = 0; i < expected.Length; i++)
                Check(expected[i].SequenceEqual(actual[i]), label + " exact native vertex/order equality at " + i);
            for (int i = 0; i < polygons.Length; i++)
                Check(polygons[i].SequenceEqual(snapshot[i]), label + " input mutation");
            results.Add(new { label, inputPolygons = polygons.Length, contours = actual.Length,
                referenceMs, fixedMs, referenceBytes, fixedBytes, referenceProfile, fixedProfile, status = "PASS" });
        }

        string[] directories = {
            "res://images/characters/shin_getter/forms/getter_one_star_slash",
            "res://images/characters/shin_getter/forms/shin_getter_dragon_star_slash",
        };
        foreach (string directory in directories)
        {
            object data = load.Invoke(null, new object[] { directory })!;
            Check(data != null, directory + " metadata load");
            Check(ReferenceEquals(data, load.Invoke(null, new object[] { directory })), directory + " cache hit");
            float hold = (float)dataType.GetProperty("HoldTime")!.GetValue(data)!;
            Array frames = (Array)dataType.GetProperty("Frames")!.GetValue(data)!;
            float elapsed = 0;
            for (int index = 0; index < frames.Length; index++)
            {
                object frame = frames.GetValue(index)!;
                Type frameType = frame.GetType();
                float duration = (float)frameType.GetProperty("Duration")!.GetValue(frame)!;
                Vector2[][] cover = (Vector2[][])frameType.GetProperty("WeaponCover")!.GetValue(frame)!;
                if (spot && (!directory.EndsWith("shin_getter_dragon_star_slash") || index != 60))
                {
                    elapsed += duration;
                    continue;
                }
                foreach (float pose in new[] { elapsed, elapsed + duration * .5f, elapsed + duration })
                {
                    if (spot && pose != elapsed + duration * .5f) continue;
                    object geometry = build.Invoke(null, new[] { frame, (object)pose, hold })!;
                    Type geometryType = geometry.GetType();
                    Vector2[][] blades = (Vector2[][])geometryType.GetProperty("Blades")!.GetValue(geometry)!;
                    Vector2[] shaft = (Vector2[])geometryType.GetProperty("Shaft")!.GetValue(geometry)!;
                    Vector2[][] opaque = cover.Concat(blades).Append(shaft).ToArray();
                    foreach (Vector2 mirror in new[] { new Vector2(1, 1), new Vector2(-1, 1),
                        new Vector2(1, -1), new Vector2(-1, -1) })
                    {
                        if (spot && mirror != new Vector2(1, 1)) continue;
                        Vector2 offset = new(113.25f, -360f);
                        Compare($"{directory.Split('/')[^1]}:{index}:{pose:R}:{mirror.X},{mirror.Y}",
                            opaque.Select(p => p.Select(v => v * mirror + offset).ToArray()).ToArray());
                    }
                }
                elapsed += duration;
            }
        }
        Vector2[] Rectangle(float x, float y, float width, float height) =>
            new[] { new Vector2(x, y), new Vector2(x + width, y), new Vector2(x + width, y + height), new Vector2(x, y + height) };
        foreach (float gap in new[] { 0f, .000001f, .000009f, .0005f, .001f, .0021f, 1f })
            Compare("native_boundary_gap:" + gap.ToString("R"), new[] { Rectangle(0, 0, 10, 10), Rectangle(10 + gap, 0, 10, 10) });
        Compare("contained", new[] { Rectangle(0, 0, 10, 10), Rectangle(2, 2, 2, 2) });
        Compare("vertex_touch", new[] { Rectangle(0, 0, 10, 10), Rectangle(10, 10, 10, 10) });
        Compare("updated_union_bounds", new[] { Rectangle(0, 0, 10, 10), Rectangle(9, 0, 10, 10), Rectangle(18, 0, 10, 10) });
        Compare("ring_hole", new[] { Rectangle(0, 0, 30, 5), Rectangle(0, 25, 30, 5), Rectangle(0, 0, 5, 30), Rectangle(25, 0, 5, 30) });
        Compare("concave", new[] { new[] { new Vector2(0, 0), new Vector2(20, 0), new Vector2(20, 20), new Vector2(10, 10), new Vector2(0, 20) }, Rectangle(15, 5, 10, 10) });
        Vector2[] notch = { new(0, 0), new(30, 0), new(30, 30), new(20, 30), new(20, 10), new(10, 10), new(10, 30), new(0, 30) };
        Compare("corners_inside_but_crosses_notch", new[] { notch, Rectangle(5, 20, 20, 5) });
        Compare("rectangle_in_empty_notch", new[] { notch, Rectangle(12, 20, 4, 4) });
        Compare("disjoint_region_reached_after_union", new[] { notch, Rectangle(12, 20, 4, 4), Rectangle(8, 18, 14, 8) });
        var random = new Random(124215);
        for (int sample = 0; sample < 192; sample++)
        {
            Vector2[][] rectangles = Enumerable.Range(0, 8).Select(_ => Rectangle(random.Next(-30, 30) + .00001f,
                random.Next(-30, 30) + .0005f, random.Next(1, 20), random.Next(1, 20))).ToArray();
            Compare("seeded_rectangles:" + sample, rectangles.Append(notch).ToArray());
        }

        string temporary = "user://outline-cache-regression-" + attempt;
        string physical = ProjectSettings.GlobalizePath(temporary);
        System.IO.Directory.CreateDirectory(physical);
        Check(load.Invoke(null, new object[] { temporary }) == null, "missing metadata must not cache failure");
        string path = System.IO.Path.Combine(physical, "animation.json");
        System.IO.File.WriteAllText(path, "{}");
        Check(load.Invoke(null, new object[] { temporary }) == null, "invalid metadata must be rejected");
        System.IO.File.WriteAllText(path, Godot.FileAccess.GetFileAsString(directories[0] + "/animation.json"));
        object repaired = load.Invoke(null, new object[] { temporary })!;
        Check(repaired != null && ReferenceEquals(repaired, load.Invoke(null, new object[] { temporary })), "repaired file loads and is cached");
        System.IO.File.Delete(path);
        Check(load.Invoke(null, new object[] { temporary }) == null, "removed metadata must not return cached success");
        Check(!ReferenceEquals(load.Invoke(null, new object[] { directories[0] }), load.Invoke(null, new object[] { directories[1] })), "form caches must not alias");

        string output = System.IO.Path.Combine(root, "evidence", attempt + "-geometry.json");
        System.IO.File.WriteAllText(output, JsonSerializer.Serialize(new { cases = results, status = "PASS",
            cache = "valid-hit; missing/invalid-not-cached; removal-checked; two-form-isolation" }, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print("B130_OUTLINE_REGRESSION PASS cases=" + results.Count);
    }
}
