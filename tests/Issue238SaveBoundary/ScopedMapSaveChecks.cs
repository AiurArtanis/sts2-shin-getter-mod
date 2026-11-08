using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using MegaCrit.Sts2.Core.Saves.Migrations;
using MegaCrit.Sts2.Core.Saves.Runs;

internal static class ScopedMapSaveChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static object? Call(Type t, string name, params object?[] args) => t.GetMethod(name, Static)!.Invoke(null, args);
    private static object? Property(object obj, string name) => obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(obj);
    private static void Field(object obj, string name, object? value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(obj, value);

    internal static async Task Run(Assembly mod, Harmony harmony, RunState run, SerializableRun save, EventRoom checkpoint)
    {
        var scopeType = mod.GetType("ShinGetterMod.Events.ShinGetterEventMapSaveScope", true)!;
        var verifier = mod.GetType("ShinGetterMod.Events.ShinGetterEventSaveVerification", true)!;
        var service = mod.GetType("ShinGetterMod.Events.ShinGetterEventInvasionService", true)!;
        IDisposable Scope(SerializableRun s) => (IDisposable)Call(scopeType, "InspectSnapshot", s)!;
        string Serialize(SerializableRun s) => JsonSerializer.Serialize(s, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
        SerializableRun Copy(SerializableRun s) { using var scope = Scope(s); return JsonSerializer.Deserialize(Serialize(s), JsonSerializationUtility.GetTypeInfo<SerializableRun>())!; }
        object Snapshot(SerializableRun s) => Call(verifier, "Capture", s)!;
        string Point(SerializableMapPoint p) => JsonSerializer.Serialize(p, JsonSerializationUtility.GetTypeInfo<SerializableMapPoint>());
        SerializableMapPoint P(bool value) => new() { Coord = new(1, 2), PointType = MapPointType.Treasure, CanBeModified = value };
        save.Acts[0].SavedMap = new() { Points = Enumerable.Range(0, 1500).Select(i => P(i % 2 == 0)).ToList(),
            BossPoint = P(false), StartingPoint = P(false), SecondBossPoint = P(false) };
        var owned = save.Acts[0].SavedMap!.BossPoint;
        var foreign = P(false);
        string nativePoint = Point(owned), ordinary = Serialize(save);
        Check(!nativePoint.Contains("can_modify") && JsonSerializer.Deserialize(nativePoint, JsonSerializationUtility.GetTypeInfo<SerializableMapPoint>())!.CanBeModified, "Native default asymmetry missing");
        var pointInfo = JsonSerializationUtility.GetTypeInfo<SerializableMapPoint>();
        Check(pointInfo.IsReadOnly && ReferenceEquals(pointInfo, JsonSerializationUtility.GetTypeInfo<SerializableMapPoint>()), "Native cached metadata not reused");
        bool cachedRejected = false;
        var uncached = new JsonSerializerOptions(JsonSerializationUtility.Options) { TypeInfoResolver = JsonSerializationUtility.DefaultResolver.WithAddedModifier(_ => { }) };
        var frozenNative = uncached.GetTypeInfo(typeof(SerializableMapPoint));
        JsonSerializer.Serialize(foreign, frozenNative);
        try { Call(scopeType, "AddJsonRule", frozenNative); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { cachedRejected = true; }
        Check(cachedRejected, "Already configured foreign metadata silently retrofitted");
        foreach (Func<object,object?,bool>? previous in new Func<object,object?,bool>?[] {
            null, (_,_)=>true, (_,_)=>false, (obj,_)=>((SerializableMapPoint)obj).Coord.col == 1 })
        {
            JsonTypeInfo<SerializableMapPoint> Info()
            {
                var info = JsonTypeInfo.CreateJsonTypeInfo<SerializableMapPoint>(new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() });
                var prop = info.CreateJsonPropertyInfo(typeof(bool), "can_modify");
                prop.Get = obj => ((SerializableMapPoint)obj).CanBeModified;
                prop.Set = (obj,value) => ((SerializableMapPoint)obj).CanBeModified = (bool)value!;
                prop.ShouldSerialize = previous ?? ((_,value)=>value is true);
                info.Properties.Add(prop); return info;
            }
            var baselineInfo = Info(); var adaptedInfo = Info();
            Call(scopeType,"AddJsonRule",adaptedInfo);
            foreach (bool value in new[] {false,true})
            {
                var point = P(value);
                Check(JsonSerializer.Serialize(point,baselineInfo) == JsonSerializer.Serialize(point,adaptedInfo), "Prior native ShouldSerialize callback changed outside scope");
            }
        }

        ExecutionContext? captured;
        Task<string> delayed;
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using (Scope(save))
        {
            Check(Point(owned).Contains("can_modify") && Point(foreign) == nativePoint, "Point reference ownership broadened");
            captured = ExecutionContext.Capture();
            delayed = Task.Run(async () => { await release.Task; return Point(owned); });
            var inner = Copy(save);
            using (Scope(inner)) Check(Point(owned) == nativePoint, "Nested scope acquired foreign graph");
            Check(Point(owned).Contains("can_modify"), "Outer scope not restored");
            try { using (Scope(inner)) throw new IOException("injected"); } catch (IOException) { }
            Check(Point(owned).Contains("can_modify"), "Nested exception lost outer scope");
        }
        string? late = null;
        ExecutionContext.Run(captured!, _ => late = Point(owned), null);
        release.SetResult();
        Check(late == nativePoint && await delayed == nativePoint, "Disposed token survived captured context/task");
        Check(Serialize(save) == ordinary, "Ordinary full run bytes changed outside scope");

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async i =>
        {
            var local = Copy(save);
            using var scope = Scope(local);
            using var stream = new YieldStream();
            await JsonSerializer.SerializeAsync(stream, local, JsonSerializationUtility.GetTypeInfo<SerializableRun>());
            Check(stream.Writes > 1, "Actual asynchronous serialization did not yield multiple writes");
            var read = JsonSerializer.Deserialize(stream.ToArray(), JsonSerializationUtility.GetTypeInfo<SerializableRun>())!;
            Check(!read.Acts[0].SavedMap!.BossPoint.CanBeModified && Point(owned) == nativePoint, "Parallel scope leaked/lost flags");
        }).Append(Task.Run(async () => { await Task.Yield(); Check(Serialize(save) == ordinary, "Ordinary concurrent run changed"); })));
        bool canceled = false;
        try { using var scope = Scope(save); using var stream = new YieldStream(); await JsonSerializer.SerializeAsync(stream, save, JsonSerializationUtility.GetTypeInfo<SerializableRun>(), new CancellationToken(true)); }
        catch (OperationCanceledException) { canceled = true; }
        Check(canceled && Point(owned) == nativePoint, "Cancellation left serializer scope active");

        var expected = Snapshot(save);
        var readback = Copy(save);
        Call(verifier, "Compare", expected, Snapshot(readback));
        readback.Acts[0].SavedMap!.BossPoint.CanBeModified = true;
        readback.Acts[0].SerializableRooms.EventsVisited++;
        readback.SerializableRng.Counters[MegaCrit.Sts2.Core.Entities.Rngs.RunRngType.UpFront] = 17;
        readback.PreFinishedRoom!.IsPreFinished = false;
        var capture = new DiagnosticCapture();
        var sink = verifier.GetProperty("DiagnosticSink", Static)!;
        var sinkMethod = typeof(DiagnosticCapture).GetMethod(nameof(DiagnosticCapture.Accept))!;
        sink.SetValue(null, Delegate.CreateDelegate(sink.PropertyType, capture, sinkMethod));
        bool different = false;
        try { Call(verifier, "Compare", expected, Snapshot(readback)); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { different = true; }
        finally { sink.SetValue(null, null); }
        Check(different && capture.Last != null, "Mismatch diagnostics not captured");
        var differences = (Array)Property(capture.Last!, "Differences")!;
        Check(differences.Length == 4, "Complete difference paths not retained");
        var paths = differences.Cast<object>().Select(x => (string)Property(x, "Path")!).ToArray();
        Check(paths.Any(p => p.Contains("can_modify")) && paths.Any(p => p.Contains("events_visited"))
            && paths.Any(p => p.Contains("rng")) && paths.Any(p => p.Contains("is_pre_finished")), "Full map/pool/RNG/room differences missing");

        // Actual production ToSave postfix is exercised with an explicit DTO fixture.
        // The scene-dependent ToSave body itself is left to real-host acceptance.
        typeof(RunManager).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(RunManager.Instance, run);
        using (var entered = (IDisposable)Call(scopeType, "BeginCheckpoint", run, checkpoint)!)
        {
            Call(scopeType, "CaptureNativeToSave", RunManager.Instance, new EventRoom(checkpoint.CanonicalEvent), save);
            Check(Property(entered, "Expected") == null, "Wrong checkpoint fixture registered");
            Call(scopeType, "CaptureNativeToSave", RunManager.Instance, checkpoint, save);
            Check(Property(entered, "Expected") != null && Point(owned).Contains("can_modify"), "Actual snapshot capture rejected owned DTO");
        }
        Console.WriteLine("production map scope: cached metadata / reference ownership / async8 + ordinary / nested fault / cancellation / captured-context revocation / complete four-path diagnostics PASS");

        // Invoke the actual native async setup and its patched MoveNext. Stop only
        // after native IncrementNumReloads has serialized to an in-memory store,
        // before scene-dependent shared initialization; never a disk/game proof.
        Check((bool)Call(service, "OwnsFourthReloadCheckpoint", run, save)!, "Exact reload checkpoint rejected");
        foreach (Action<SerializableRun> mutate in new Action<SerializableRun>[] {
            s => s.Players[0].NetId++, s => s.SerializableRng.Seed = "other", s => s.PreFinishedRoom = null,
            s => s.VisitedMapCoords.Add(new(2,2)), s => s.MapPointHistory[0].Add(new()) })
        {
            var bad = Copy(save); mutate(bad);
            Check(!(bool)Call(service, "OwnsFourthReloadCheckpoint", run, bad)!, "Wrong reload identity admitted");
        }
        var store = DispatchProxy.Create<ISaveStore, InMemoryStore>();
        var backend = (InMemoryStore)(object)store;
        var native = new RunSaveManager(1, store, (MigrationManager)RuntimeHelpers.GetUninitializedObject(typeof(MigrationManager)));
        var manager = (SaveManager)RuntimeHelpers.GetUninitializedObject(typeof(SaveManager));
        Field(manager, "_saveStore", store); Field(manager, "_runSaveManager", native);
        typeof(SaveManager).GetMethod("MockInstanceForTesting")!.Invoke(null, new object[] { manager });
        new PatchClassProcessor(harmony, mod.GetType("ShinGetterMod.Patches.ShinGetterEventReloadSavePatch",true)!).Patch();
        Type peer = typeof(RunManager).Assembly.GetType("MegaCrit.Sts2.Core.Multiplayer.Game.PeerInput.PeerInputSynchronizer", true)!;
        harmony.Patch(peer.GetConstructors().Single(), prefix: new HarmonyMethod(typeof(StopAfterReload), nameof(StopAfterReload.PeerCtorPrefix)));
        harmony.Patch(AccessTools.Method(typeof(RunManager), "InitializeShared"), prefix: new HarmonyMethod(typeof(StopAfterReload), nameof(StopAfterReload.Prefix)));
        MethodInfo setup = AccessTools.Method(typeof(RunManager), nameof(RunManager.SetUpSavedSinglePlayer));
        foreach (bool stamped in new[] { true, false })
        {
            var current = Copy(save);
            if (!stamped) current.PreFinishedRoom = null;
            int before = current.NumReloads;
            typeof(RunManager).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(RunManager.Instance, null);
            bool stopped = false;
            try { await (Task)setup.Invoke(RunManager.Instance, new object[] { run, current })!; }
            catch (InvalidOperationException ex) when (ex.Message == "stop-after-native-reload") { stopped = true; }
            Check(stopped && current.NumReloads == before + 1 && backend.Bytes != null, "Native reload order/save not executed");
            var loaded = JsonSerializer.Deserialize(backend.Bytes!, JsonSerializationUtility.GetTypeInfo<SerializableRun>())!;
            Check(loaded.Acts[0].SavedMap!.BossPoint.CanBeModified == !stamped, "Scoped reload bytes differ from ownership boundary");
            Check(Point(owned) == nativePoint, "Reload async wrapper leaked into caller");
        }
        SaveManager.ClearInstanceForTesting();
        Check(Serialize(save) == ordinary, "Native ordinary bytes changed after all scopes");
        Console.WriteLine("actual native singleplayer MoveNext -> IncrementNumReloads -> SaveRun(SerializableRun) in-memory serialization: owned false preserved, unowned native omission, reload count once, caller scope clean PASS; REAL_CONTINUE_NOT_RUN");
    }
}

public sealed class DiagnosticCapture
{
    public object? Last;
    public void Accept(object value) => Last = value;
}
public sealed class StopAfterReload
{
    // Its constructor initializes the Godot logger before InitializeShared is called.
    // No peer behavior is executed by this bounded reload-serialization probe.
    public static bool PeerCtorPrefix() => false;
    public static void Prefix() => throw new InvalidOperationException("stop-after-native-reload");
}
public class InMemoryStore : DispatchProxy
{
    public byte[]? Bytes;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "WriteFileAsync") { Bytes = args![1] is byte[] b ? b.ToArray() : Encoding.UTF8.GetBytes((string)args[1]!); return Task.CompletedTask; }
        if (method.Name == "WriteFile") { Bytes = args![1] is byte[] b ? b.ToArray() : Encoding.UTF8.GetBytes((string)args[1]!); return null; }
        throw new NotSupportedException("Unexpected native store call: " + method.Name);
    }
}
internal sealed class YieldStream : MemoryStream
{
    internal int Writes;
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    { Interlocked.Increment(ref Writes); await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); await base.WriteAsync(buffer, cancellationToken); }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
}
