#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace ShinGetterMod.Events;

// Native can_modify=false is omitted although its constructor default is true.
// Preserve it only for the exact DTO graph owned by an entered fourth-batch save.
// A shared, revocable token also invalidates already-captured ExecutionContexts.
internal sealed class ShinGetterEventMapSaveScope : IDisposable
{
    private static readonly AsyncLocal<ShinGetterEventMapSaveScope?> Ambient = new();
    private static readonly ConditionalWeakTable<JsonTypeInfo, object> InstalledMetadata = new();
    private static readonly object MetadataGate = new();
    private readonly ShinGetterEventMapSaveScope? previous;
    private readonly IRunState? run;
    private readonly EventRoom? checkpoint;
    private FrozenSet<SerializableMapPoint>? points;
    private int active = 1;
    internal ShinGetterEventSaveVerification.Snapshot? Expected { get; private set; }

    private ShinGetterEventMapSaveScope(IRunState? run, EventRoom? checkpoint)
    {
        this.run = run;
        this.checkpoint = checkpoint;
        previous = Ambient.Value;
        Ambient.Value = this;
    }

    internal static ShinGetterEventMapSaveScope BeginCheckpoint(IRunState run, EventRoom checkpoint)
        => new(run, checkpoint);

    // Also used while taking a diagnostic snapshot of the direct native readback.
    // No deserialize/normalization roundtrip is allowed before this capture.
    internal static ShinGetterEventMapSaveScope InspectSnapshot(SerializableRun save)
    {
        ShinGetterEventMapSaveScope scope = new(null, null);
        try { scope.RegisterPoints(save); return scope; }
        catch { scope.Dispose(); throw; }
    }

    private void RegisterPoints(SerializableRun save)
    {
        var registered = MapPoints(save).Select(p => p.Point)
            .ToFrozenSet<SerializableMapPoint>(ReferenceEqualityComparer.Instance);
        if (Interlocked.CompareExchange(ref points, registered, null) != null)
            throw new InvalidOperationException("Event save DTO graph registered twice.");
    }

    internal static IEnumerable<(string Path, SerializableMapPoint Point)> MapPoints(SerializableRun save)
    {
        for (int act = 0; act < save.Acts.Count; act++)
        {
            SerializableActMap? map = save.Acts[act].SavedMap;
            if (map == null) continue;
            string prefix = $"$.acts[{act}].saved_map";
            for (int point = 0; point < map.Points.Count; point++)
                yield return ($"{prefix}.points[{point}].can_modify", map.Points[point]);
            if (map.BossPoint != null) yield return (prefix + ".boss.can_modify", map.BossPoint);
            if (map.StartingPoint != null) yield return (prefix + ".start.can_modify", map.StartingPoint);
            if (map.SecondBossPoint != null) yield return (prefix + ".second_boss.can_modify", map.SecondBossPoint);
        }
    }

    internal static void CaptureNativeToSave(RunManager manager, AbstractRoom? preFinishedRoom, SerializableRun save)
    {
        var scope = Ambient.Value;
        if (scope == null || Volatile.Read(ref scope.active) == 0 || scope.run == null
            || !ReferenceEquals(manager.DebugOnlyGetState(), scope.run)
            || !ReferenceEquals(preFinishedRoom, scope.checkpoint)) return;
        if (save.PreFinishedRoom is not { RoomType: RoomType.Event, IsPreFinished: true } room
            || room.EventId != scope.checkpoint!.ModelId)
            throw new InvalidOperationException("Native ToSave returned another event checkpoint.");
        scope.RegisterPoints(save);
        // This is the actual DTO passed on to native SaveRun, before any serialization.
        scope.Expected = ShinGetterEventSaveVerification.Capture(save);
    }

    internal static bool Owns(SerializableMapPoint point)
    {
        var scope = Ambient.Value;
        return scope != null && Volatile.Read(ref scope.active) != 0
            && Volatile.Read(ref scope.points)?.Contains(point) == true;
    }

    internal static void AddJsonRule(JsonTypeInfo info)
    {
        if (info.Type != typeof(SerializableMapPoint)) return;
        lock (MetadataGate)
        {
            if (InstalledMetadata.TryGetValue(info, out _)) return;
            if (info.IsReadOnly || info.Kind != JsonTypeInfoKind.Object)
                throw new InvalidOperationException("Map point metadata was cached before event save patching.");
            JsonPropertyInfo property = info.Properties.Single(p => p.Name == "can_modify");
            if (property.PropertyType != typeof(bool) || property.Get == null || property.Set == null)
                throw new InvalidOperationException("Unsupported native map boolean metadata.");
            var nativeCondition = property.ShouldSerialize;
            property.ShouldSerialize = (obj, value) => Owns((SerializableMapPoint)obj)
                || (nativeCondition != null ? nativeCondition(obj, value) : value is true);
            InstalledMetadata.Add(info, new object());
        }
    }

    internal static void VerifyJsonMetadata()
    {
        JsonTypeInfo info = JsonSerializationUtility.GetTypeInfo<SerializableMapPoint>();
        if (!InstalledMetadata.TryGetValue(info, out _))
            throw new InvalidOperationException("Map point metadata does not support scoped event save preservation.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref active, 0) == 0) return;
        if (!ReferenceEquals(Ambient.Value, this))
            throw new InvalidOperationException("Event save scopes disposed out of order.");
        Ambient.Value = previous;
    }
}
