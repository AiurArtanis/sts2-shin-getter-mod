using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

// Exercises the production struct, not a reimplementation of its mapping.
Assembly mod = Assembly.Load("ShinGetterMod");
Type cue = mod.GetType("ShinGetterMod.Audio.ShinGetterVoiceCue", true)!;
Type masks = mod.GetType("ShinGetterMod.Audio.ShinGetterVoiceService+VoiceHistoryMasks", true)!;
MethodInfo add = masks.GetMethod("Add")!;
MethodInfo contains = masks.GetMethod("Contains")!;
PropertyInfo low = masks.GetProperty("Low")!, high = masks.GetProperty("High")!;
object Empty() => Activator.CreateInstance(masks, 0, 0)!;
object Cue(int index) => Enum.ToObject(cue, index);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
object all = Empty();
for (int index = 0; index < 64; index++)
{
    object single = add.Invoke(Empty(), new[] { Cue(index) })!;
    int actualLow = (int)low.GetValue(single)!, actualHigh = (int)high.GetValue(single)!;
    if (index < 31) Check(actualLow == 1 << index && actualHigh == 0, $"Released low {index}");
    else if (index < 62) Check(actualLow == 0 && actualHigh == 1 << (index - 31), $"Released high {index}");
    else Check(index == 62 ? actualLow == int.MinValue && actualHigh == 0
        : actualLow == 0 && actualHigh == int.MinValue, $"New sign bit {index}");
    for (int other = 0; other < 64; other++)
        Check((bool)contains.Invoke(single, new[] { Cue(other) })! == (index == other), $"Cue collision {index}/{other}");
    all = add.Invoke(all, new[] { Cue(index) })!;
}
Check((int)low.GetValue(all)! == -1 && (int)high.GetValue(all)! == -1, "All 64 bits preserved as signed ints");
for (int index = 0; index < 64; index++) Check((bool)contains.Invoke(all, new[] { Cue(index) })!, "Signed Contains");
foreach (int index in new[] { -1, 64, 95, int.MaxValue })
foreach (MethodInfo method in new[] { add, contains })
{
    try { method.Invoke(all, new[] { Cue(index) }); throw new Exception($"Range accepted {index}"); }
    catch (TargetInvocationException error) when (error.InnerException is ArgumentOutOfRangeException) { }
}
Console.WriteLine("PASS production VoiceHistoryMasks: 4096 pairwise checks, released mapping, signed masks, Add/Contains bounds.");

// Production metadata constructor and frame lookup: pure managed code, no Godot scene/native init.
Type dataType = mod.GetType("ShinGetterMod.Nodes.Combat.NShinGetterStarSlashData", true)!;
ConstructorInfo dataConstructor = dataType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
    null, new[] { typeof(JsonElement) }, null)!;
MethodInfo frameAt = dataType.GetMethod("FrameAt")!;
MethodInfo buildWeapon = dataType.GetMethod("BuildWeapon", BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo foregroundUv = dataType.GetMethod("ForegroundUv", BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo cleanPolygon = dataType.GetMethod("CleanPolygon", BindingFlags.Static | BindingFlags.NonPublic)!;
Vector2[] Clean(Vector2[] polygon) => (Vector2[])cleanPolygon.Invoke(null, new object[] { polygon })!;
double Area(Vector2[] polygon) => Math.Abs(Enumerable.Range(0, polygon.Length).Sum(i =>
    (double)polygon[i].X * polygon[(i + 1) % polygon.Length].Y
    - (double)polygon[(i + 1) % polygon.Length].X * polygon[i].Y)) / 2;
Vector2[] rectangle = { new(0, 0), new(0, 0), new(5, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0) };
Check(Clean(rectangle).Length == 4 && Area(Clean(rectangle)) == 100, "Duplicate/straight cleanup preserves rectangle");
Vector2[] notch = { new(0, 0), new(10, 0), new(10, 10), new(5, 5), new(0, 10) };
Check(Clean(notch).SequenceEqual(notch) && Area(Clean(notch)) == 75, "Cleanup retains genuine concave notch");
foreach (int index in new[] { 0, 9, 10, 26, 38, 61, 70, 75 })
{
    Vector2 origin = new(index % 10 * 720, index / 10 * 720);
    Vector2 size = new(7200, 5760), point = new(315, 287);
    Vector2 uv = (Vector2)foregroundUv.Invoke(null, new object[] { point, origin, size })!;
    Check((uv * size - origin).DistanceTo(point) < 0.001f, "Atlas foreground roundtrip uses this frame's region");
}
Check((Vector2)foregroundUv.Invoke(null, new object[] { new Vector2(315, 287), Vector2.Zero, new Vector2(720, 720) })!
    == new Vector2(315f / 720f, 287f / 720f), "Standalone texture fallback keeps frame-local UVs");
Console.WriteLine("PASS production foreground UVs: 8 atlas regions plus standalone texture fallback.");
Vector2 Point(JsonNode node) => new(JsonSerializer.Deserialize<float>(node[0]!.ToJsonString()),
    JsonSerializer.Deserialize<float>(node[1]!.ToJsonString()));
float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d) =>
    Cross(a, b, c) * Cross(a, b, d) < -0.0001f && Cross(c, d, a) * Cross(c, d, b) < -0.0001f;
void CheckSimplePolygon(Vector2[] polygon, string label = "polygon")
{
    for (int left = 0; left < polygon.Length; left++)
    for (int right = left + 2; right < polygon.Length; right++)
    {
        if (left == 0 && right == polygon.Length - 1) continue;
        Check(!SegmentsCross(polygon[left], polygon[(left + 1) % polygon.Length],
            polygon[right], polygon[(right + 1) % polygon.Length]), $"{label}: silhouette intersects segments {left}/{right}");
    }
}
bool InsidePolygon(Vector2 point, Vector2[] polygon)
{
    bool inside = false;
    for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
    {
        Vector2 a = polygon[j], b = polygon[i];
        if (Math.Abs(Cross(a, b, point)) < 0.1f && (point - a).Dot(point - b) <= 0.1f) return true;
        if ((a.Y > point.Y) != (b.Y > point.Y)
            && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
    }
    return inside;
}
object LoadData(JsonNode node)
{
    using JsonDocument document = JsonDocument.Parse(node.ToJsonString());
    return dataConstructor.Invoke(new object[] { document.RootElement });
}
float FloatProperty(object data, string name) => (float)dataType.GetProperty(name)!.GetValue(data)!;
JsonNode SyntheticData(int count)
{
    var frames = new JsonArray();
    for (int index = 0; index < count; index++)
        frames.Add(new JsonObject
        {
            ["duration_seconds"] = 2.4 / count,
            ["grip"] = new JsonArray(300, 300), ["axis"] = new JsonArray(300, 100),
            ["weapon_cover"] = new JsonArray(new JsonArray(new JsonArray(200, 100), new JsonArray(400, 100), new JsonArray(300, 300))),
            ["blade_cover"] = new JsonArray(new JsonArray(new JsonArray(200, 100), new JsonArray(400, 100), new JsonArray(300, 300))),
            ["handle_cover"] = new JsonArray(new JsonArray(new JsonArray(294, 250), new JsonArray(306, 250), new JsonArray(300, 420))),
            ["hands"] = new JsonArray(new JsonArray(new JsonArray(290, 290), new JsonArray(310, 290), new JsonArray(300, 310))),
        });
    return new JsonObject { ["hold_frame"] = 25, ["impact_time"] = 1.4, ["frames"] = frames };
}
var documents = new List<(string Name, JsonNode Document)>
    { ("synthetic76", SyntheticData(76)), ("synthetic71", SyntheticData(71)) };
documents.AddRange(args.Select(path => (Path.GetFileName(Path.GetDirectoryName(path)) ?? path, JsonNode.Parse(File.ReadAllText(path))!)));
foreach ((string name, JsonNode original) in documents)
{
    object data = LoadData(original);
    JsonArray frames = original["frames"]!.AsArray();
    Check(frames.Count is 76 or 71, "Delivered frame count");
    float total = FloatProperty(data, "TotalTime");
    float hold = FloatProperty(data, "HoldTime");
    Check(Math.Abs(total - 2.4f) < 0.0001f, "Weighted total duration");
    float elapsed = 0f;
    for (int index = 0; index < frames.Count; index++)
    {
        float duration = JsonSerializer.Deserialize<float>(frames[index]!["duration_seconds"]!.ToJsonString());
        Check((int)frameAt.Invoke(data, new object[] { elapsed + duration * 0.5f })! == index,
            $"Actual FrameAt midpoint {index}");
        elapsed += duration;
    }
    Check((int)frameAt.Invoke(data, new object[] { total })! == frames.Count - 1, "Last frame clamp");
    Array productionFrames = (Array)dataType.GetProperty("Frames")!.GetValue(data)!;
    elapsed = 0f;
    for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
    {
        JsonNode frame = frames[frameIndex]!;
        foreach (string layer in new[] { "weapon_cover", "blade_cover", "handle_cover", "body_foreground", "hands" })
        {
            if (frame[layer] is not JsonArray polygons) continue;
            for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
            {
                Vector2[] sourcePolygon = polygons[polygonIndex]!.AsArray().Select(p => Point(p!)).ToArray();
                CheckSimplePolygon(sourcePolygon, $"{name} frame{frameIndex} {layer}{polygonIndex}");
                Vector2[] cleaned = Clean(sourcePolygon);
                Check(cleaned.Length >= 3 && Area(cleaned) > 0, "Cleaner retains drawable coverage");
                Check(Math.Abs(Area(sourcePolygon) - Area(cleaned)) <= 0.01, "Cleaner preserves contour area");
                Check(cleaned.All(sourcePolygon.Contains), "Cleaner never invents hull vertices");
                CheckSimplePolygon(cleaned, $"{name} frame{frameIndex} {layer}{polygonIndex} cleaned");
            }
        }
        Vector2[] head = frame!["blade_cover"]!.AsArray().SelectMany(polygon => polygon!.AsArray().Select(p => Point(p!))).ToArray();
        Vector2 grip = Point(frame["grip"]!), axis = (Point(frame["axis"]!) - grip).Normalized();
        Vector2 normal = new(-axis.Y, axis.X);
        float duration = JsonSerializer.Deserialize<float>(frame["duration_seconds"]!.ToJsonString());
        foreach (float poseTime in new[] { elapsed, elapsed + duration * 0.5f, elapsed + duration, hold })
        {
            object geometry = buildWeapon.Invoke(null, new[] { productionFrames.GetValue(frameIndex)!, poseTime, hold })!;
            Type geometryType = geometry.GetType();
            Vector2[][] blades = (Vector2[][])geometryType.GetProperty("Blades")!.GetValue(geometry)!;
            Vector2[][] lights = (Vector2[][])geometryType.GetProperty("BladeLights")!.GetValue(geometry)!;
            Vector2[] shaft = (Vector2[])geometryType.GetProperty("Shaft")!.GetValue(geometry)!;
            Vector2[] centerline = (Vector2[])geometryType.GetProperty("Centerline")!.GetValue(geometry)!;
            Check(blades.Length == 2 && blades.All(blade => blade.Length == 72), "Two reference Bezier contours, 4x18 samples");
            for (int bladeIndex = 0; bladeIndex < blades.Length; bladeIndex++)
                CheckSimplePolygon(blades[bladeIndex], $"{name} frame{frameIndex} blade{bladeIndex}");
            Check(lights.Length == 2 && lights.All(light => light.Length == 72), "Paired inside-ribbon energy bands");
            for (int bladeIndex = 0; bladeIndex < blades.Length; bladeIndex++)
            {
                Check(lights[bladeIndex].All(point => InsidePolygon(point, blades[bladeIndex])),
                    "Energy layer must not fill the concave notch or leave the blade");
                CheckSimplePolygon(lights[bladeIndex], $"{name} frame{frameIndex} light{bladeIndex}");
            }
            foreach (Vector2[] blade in blades)
            {
                Check(blade.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y)), "Finite reference blade geometry");
                float[] turns = Enumerable.Range(0, blade.Length).Select(i =>
                    Cross(blade[i], blade[(i + 1) % blade.Length], blade[(i + 2) % blade.Length])).ToArray();
                Check(turns.Any(turn => turn < -0.001f) && turns.Any(turn => turn > 0.001f),
                    "Reference crescent stays concave; no rejected convex plate");
                Check(blade[0] != blade[^1], "No duplicated polygon root");
                CheckSimplePolygon(blade);
            }
            Check(shaft.Length >= 64 && centerline.Length >= 2, "Smoothly sampled taper plus thin center highlight");
            Check(shaft.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y)), "Finite tapered shaft");
            float tail = shaft.Min(point => (point - grip).Dot(axis));
            float tip = shaft.Max(point => (point - grip).Dot(axis));
            Check(Math.Abs((shaft[0] - grip).Dot(normal)) < 0.001f, "Pointed shaft butt, not rectangular cap");
            Check(shaft.Any(point => Math.Abs((point - grip).Dot(axis) - tip) < 0.01f
                && Math.Abs((point - grip).Dot(normal)) < 0.01f), "Pointed shaft tip");
            Check(tail <= -90f + 0.01f && tip > 100f, "Source handle tail and growing axe forward extent");
            CheckSimplePolygon(shaft);
        }
        elapsed += duration;
    }
    Action<JsonNode>[] invalid =
    {
        node => node["frames"]!.AsArray().RemoveAt(0),
        node => node["hold_frame"] = -1,
        node => node["hold_frame"] = frames.Count,
        node => node["impact_time"] = hold,
        node => node["impact_time"] = total,
        node => node["impact_time"] = 1e100,
        node => node["frames"]![0]!["duration_seconds"] = 0,
        node => node["frames"]![0]!["duration_seconds"] = -1,
        node => node["frames"]![0]!["duration_seconds"] = 1e100,
        node => node["frames"]![0]!["grip"]![0] = 1e100,
        node => node["frames"]![0]!["axis"] = node["frames"]![0]!["grip"]!.DeepClone(),
        node => node["frames"]![0]!["weapon_cover"] = new JsonArray(),
        node => node["frames"]![0]!["hands"] = new JsonArray(),
        node => node["frames"]![0]!["weapon_cover"]![0]!.AsArray().RemoveAt(0),
        node => node["frames"]![0]!["hands"]![0]![0]![0] = 1e100,
        node => node["frames"]![0]!["blade_cover"] = new JsonArray(),
        node => node["frames"]![0]!["handle_cover"] = new JsonArray(),
        node => node["frames"]![0]!["body_foreground"] =
            new JsonArray(new JsonArray(new JsonArray(0, 0), new JsonArray(1, 1))),
    };
    // Make the polygon-count mutation unambiguously invalid even for large delivered polygons.
    invalid[13] = node => node["frames"]![0]!["weapon_cover"]![0] =
        new JsonArray(new JsonArray(0, 0), new JsonArray(1, 1));
    foreach (Action<JsonNode> mutation in invalid)
    {
        JsonNode variant = original.DeepClone();
        mutation(variant);
        bool rejected = false;
        try { LoadData(variant); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { rejected = true; }
        Check(rejected, "Production metadata accepted an invalid variant");
    }
    Console.WriteLine($"PASS production StarSlash metadata {name}: "
        + $"{frames.Count} weighted lookups + concave Bezier/taper geometry; {invalid.Length} rejected variants.");
}
Console.WriteLine("No game/Godot initialization; native SavedProperties roundtrip NOT executed.");
