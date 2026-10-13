using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using ShinGetterMod.Services;

internal static class ThreadGuard
{
    internal static int Owner;
    internal static int Checks;
    internal static void Check()
    {
        if (Environment.CurrentManagedThreadId != Owner)
            throw new InvalidOperationException("Godot operation left the captured main thread");
        Checks++;
    }
}

internal sealed class MainThreadContext : SynchronizationContext
{
    private readonly Queue<(SendOrPostCallback Callback, object? State)> pending = new();
    public override void Post(SendOrPostCallback callback, object? state) => pending.Enqueue((callback, state));
    internal void Drain()
    {
        while (pending.TryDequeue(out var item)) item.Callback(item.State);
    }
}

internal static class Program
{
    private static int assertions;
    private static readonly List<object> measurements = new();
    private static readonly MainThreadContext context = new();
    private static bool legacyMode;
    private static NGame Game => NGame.Instance!;
    private static readonly MethodInfo Wrap = typeof(ShinGetterMod.Patches.ShinGetterCombatRenderPreparationPatch)
        .GetMethod("AfterOriginalAsync", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static int Main(string[] args)
    {
        legacyMode = args.Contains("--legacy", StringComparer.Ordinal);
        ThreadGuard.Owner = Environment.CurrentManagedThreadId;
        SynchronizationContext.SetSynchronizationContext(context);
        NGame.Instance = new NGame();
        Console.WriteLine($"Production-linked render-wait fixture; legacy={legacyMode}; real monotonic 10s budget, fake logic/draw events; NOT GPU or visual acceptance.");
        try
        {
            NoDrawDeadline();
            if (legacyMode)
                throw new InvalidOperationException("legacy no-draw regression unexpectedly passed");
            MidPreparationDisableAndRetry();
            ExitAndRetry();
            GatesAndOriginalResults();
            SuccessfulBudgetAndNoOp();
            Check(ThreadGuard.Checks > 100, "Godot checks and cleanup stayed on the captured main thread");
            Console.WriteLine(JsonSerializer.Serialize(new { pass = true, assertions, godotMainThreadChecks = ThreadGuard.Checks, measurements, nativeGpuAcceptance = false }));
            return 0;
        }
        catch (Exception error)
        {
            foreach (var host in Game.GetChildren()) host.QueueFree();
            context.Drain();
            Console.WriteLine(JsonSerializer.Serialize(new { pass = false, assertions, error = error.Message, measurements, nativeGpuAcceptance = false }));
            return 1;
        }
    }

    private static void NoDrawDeadline()
    {
        var run = new FakeRun(true);
        var elapsed = Stopwatch.StartNew();
        Task wrapper = InvokeWrap(Task.CompletedTask, run);
        WaitForDraw();
        var host = Game.GetChildren().Single();
        Task pending = ShinGetterCombatRenderPreparation.PrepareAsync(run);
        Check(ReferenceEquals(pending, ShinGetterCombatRenderPreparation.PrepareAsync(run)), "same-run Pending shared while no draw arrives");
        Check(!wrapper.IsCompleted && host.IsInsideTree(), "original success plus live host genuinely reaches no-draw wait");
        PumpUntil(() => wrapper.IsCompleted, false, 12.0);
        measurements.Add(new { caseName = "original-success-live-host-no-draw", elapsedMs = elapsed.Elapsed.TotalMilliseconds, wrapperCompleted = wrapper.IsCompleted, hostLive = host.IsInsideTree() });
        Check(wrapper.IsCompleted, $"real deadline completes original-success/no-draw wrapper; elapsed={elapsed.Elapsed.TotalMilliseconds:F1}ms");
        wrapper.GetAwaiter().GetResult();
        Check(elapsed.Elapsed.TotalSeconds is >= 9.8 and < 12.0, "real elapsed cutoff is approximately the production 10s whole-preparation budget");
        CheckClean(run, false, host);
        Task retry = ShinGetterCombatRenderPreparation.PrepareAsync(run);
        Check(!ReferenceEquals(retry, pending), "deadline failure clears Pending and permits retry");
        CompleteWithDraws(retry);
        CheckClean(run, true);
    }

    private static void MidPreparationDisableAndRetry()
    {
        var run = new FakeRun(true);
        Task original = InvokeWrap(Task.CompletedTask, run);
        WaitForDraw();
        RenderingServer.EmitFramePostDraw(); // One real completion source, then stop drawing.
        context.Drain();
        var host = Game.GetChildren().Single();
        Check(!original.IsCompleted, "partial draw does not mark whole preparation Completed");
        RenderingServer.RenderLoopEnabled = false;
        var elapsed = Stopwatch.StartNew();
        PumpUntil(() => original.IsCompleted, false, 1);
        original.GetAwaiter().GetResult();
        Check(elapsed.Elapsed.TotalSeconds < 1, "middle render-loop disable completes without another draw");
        CheckClean(run, false, host);
        RenderingServer.RenderLoopEnabled = true;
        CompleteWithDraws(ShinGetterCombatRenderPreparation.PrepareAsync(run));
        CheckClean(run, true);
    }

    private static void ExitAndRetry()
    {
        var run = new FakeRun(true);
        Task pending = ShinGetterCombatRenderPreparation.PrepareAsync(run);
        WaitForDraw();
        var host = Game.GetChildren().Single();
        host.QueueFree();
        PumpUntil(() => pending.IsCompleted, false, 1);
        pending.GetAwaiter().GetResult();
        CheckClean(run, false, host);
        CompleteWithDraws(ShinGetterCombatRenderPreparation.PrepareAsync(run));
        CheckClean(run, true);
    }

    private static void GatesAndOriginalResults()
    {
        int loads = PreloadManager.Cache.Loads;
        var disabled = new FakeRun(true);
        RenderingServer.RenderLoopEnabled = false;
        Check(ShinGetterCombatRenderPreparation.PrepareAsync(disabled).IsCompletedSuccessfully, "render-loop disabled on entry is a direct no-op");
        Check(InvokeWrap(Task.CompletedTask, disabled).IsCompletedSuccessfully, "successful original load remains successful when rendering unavailable on entry");
        Check(PreloadManager.Cache.Loads == loads && Game.GetChildren().Count == 0, "entry no-op loads nothing and creates no host");
        RenderingServer.RenderLoopEnabled = true;
        CompleteWithDraws(ShinGetterCombatRenderPreparation.PrepareAsync(disabled));
        CheckClean(disabled, true);
        Check(ShinGetterCombatRenderPreparation.PrepareAsync(new FakeRun(false)).IsCompletedSuccessfully, "non-Getter run still no-op");
        DisplayServer.Name = "headless";
        Check(ShinGetterCombatRenderPreparation.PrepareAsync(new FakeRun(true)).IsCompletedSuccessfully, "headless remains no-op");
        DisplayServer.Name = "Windows";

        loads = PreloadManager.Cache.Loads;
        Task failed = InvokeWrap(Task.FromException(new InvalidOperationException("original-load-fault")), new FakeRun(true));
        try { failed.GetAwaiter().GetResult(); throw new Exception("original fault suppressed"); }
        catch (InvalidOperationException error) when (error.Message == "original-load-fault") { Check(true, "original loading fault propagates unchanged"); }
        Check(PreloadManager.Cache.Loads == loads, "original fault never begins optional work");
        Task cancelled = InvokeWrap(Task.FromCanceled(new CancellationToken(true)), new FakeRun(true));
        try { cancelled.GetAwaiter().GetResult(); throw new Exception("original cancellation suppressed"); }
        catch (OperationCanceledException) { Check(true, "original loading cancellation propagates unchanged"); }

        var broken = new FakeRun(true);
        PreloadManager.Cache.Fail = true;
        Task optionalFailure = InvokeWrap(Task.CompletedTask, broken);
        PumpUntil(() => optionalFailure.IsCompleted, false, 1);
        optionalFailure.GetAwaiter().GetResult();
        Check(optionalFailure.IsCompletedSuccessfully, "optional scene failure does not change original loading success");
        CheckClean(broken, false);
        PreloadManager.Cache.Fail = false;
        CompleteWithDraws(ShinGetterCombatRenderPreparation.PrepareAsync(broken));
        CheckClean(broken, true);
    }

    private static void SuccessfulBudgetAndNoOp()
    {
        var run = new FakeRun(true);
        Task ready = ShinGetterCombatRenderPreparation.PrepareAsync(run);
        WaitForDraw();
        Thread.Sleep(3400); // Real clock; deliberately cover observed normal ~3.3s GPU work.
        CompleteWithDraws(ready);
        CheckClean(run, true);
        int loads = PreloadManager.Cache.Loads;
        Check(ShinGetterCombatRenderPreparation.PrepareAsync(run).IsCompletedSuccessfully, "successful same-run request remains direct no-op");
        Check(PreloadManager.Cache.Loads == loads, "success no-op does not rebuild or preload again");
    }

    private static Task InvokeWrap(Task original, IRunState run) => (Task)Wrap.Invoke(null, new object[] { original, run })!;

    private static void CompleteWithDraws(Task task)
    {
        PumpUntil(() => task.IsCompleted, true, 1);
        Check(task.IsCompleted, "restored draw completes optional preparation");
        task.GetAwaiter().GetResult();
    }

    private static void WaitForDraw()
    {
        PumpUntil(() => RenderingServer.ListenerCount > 0, false, 1);
        Check(RenderingServer.ListenerCount == 1 && SceneTree.Instance.ListenerCount == (legacyMode ? 0 : 1),
            legacyMode ? "legacy live wait attaches draw only (no logic-frame subscription)" : "live wait attaches one draw and one logic-frame subscription");
    }

    private static void PumpUntil(Func<bool> condition, bool draw, double seconds)
    {
        var clock = Stopwatch.StartNew();
        while (!condition() && clock.Elapsed.TotalSeconds < seconds)
        {
            context.Drain();
            SceneTree.Instance.EmitProcessFrame();
            if (draw && RenderingServer.RenderLoopEnabled) RenderingServer.EmitFramePostDraw();
            context.Drain();
            Thread.Sleep(5);
        }
    }

    private static void CheckClean(IRunState run, bool success, Node? oldHost = null)
    {
        Check(Game.GetChildren().Count == 0, "temporary host and particles released");
        Check(RenderingServer.ListenerCount == 0 && SceneTree.Instance.ListenerCount == 0, "draw and non-draw polling subscriptions released");
        if (oldHost != null) Check(oldHost.ExitListenerCount == 0, "host TreeExiting subscription released");
        object table = typeof(ShinGetterCombatRenderPreparation).GetField("Preparations", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        object?[] args = { run, null };
        Check((bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, args)!, "weak run state remains available for assertions");
        object state = args[1]!;
        const BindingFlags fields = BindingFlags.Public | BindingFlags.Instance;
        Check(state.GetType().GetField("Pending", fields)!.GetValue(state) == null, "Pending actually cleared after completion");
        Check((bool)state.GetType().GetField("Completed", fields)!.GetValue(state)! == success, "only complete preparation sets Completed");
        if (!success)
            Check(((ICollection)state.GetType().GetField("Resources", fields)!.GetValue(state)!).Count == 0, "failed preparation resources actually cleared");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        assertions++;
        Console.WriteLine("PASS: " + name);
    }
}
