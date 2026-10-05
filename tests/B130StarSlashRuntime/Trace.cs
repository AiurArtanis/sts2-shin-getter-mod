using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using ShinGetterMod.Models.Cards;

namespace B130StarSlashRuntime;

internal static class Trace
{
    internal sealed record Span(string Case, string Method, long Start, long Allocation, int[] Gc);
    internal static readonly ConcurrentQueue<object> Events = new();
    internal static readonly ConcurrentDictionary<string, long> Completed = new();
    internal static readonly List<object> Frames = new();
    internal static string Current = "startup";
    internal static bool Active;
    internal static Node? Sequence;
    internal static AnimatedSprite2D? Sprite;
    internal static double MaxFrameMs;
    internal static double MaxDrawMs;
    internal static double MaxWarmFrameMs;
    internal static double MaxMergeMs;
    internal static double MaxMetadataLoadMs;
    internal static long MetadataAllocationBytes;
    private static long _lastFrame;
    private static long _sequenceCreated;
    private static int _sequenceId;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly HashSet<MethodBase> Patched = new();
    private static readonly HashSet<string> Frequent = new() { "_Draw", "MoveNext", "MergeOutline", "UpdateForeground", "BuildWeapon" };
    private static readonly object Gate = new();
    internal static long Now => Stopwatch.GetTimestamp();
    internal static double Ms(long ticks) => ticks * 1000d / Stopwatch.Frequency;

    internal static void Mark(string kind, object? data = null, string? caseName = null)
    {
        Events.Enqueue(new { kind, data, @case = caseName ?? Current, ticks = Now,
            thread = System.Environment.CurrentManagedThreadId });
    }

    internal static object? Field(object obj, string name) => obj.GetType().GetField(name, Flags)?.GetValue(obj);

    internal static void Begin(string name)
    {
        Current = name;
        Frames.Clear();
        Sequence = null;
        Sprite = null;
        MaxFrameMs = MaxDrawMs = MaxWarmFrameMs = MaxMergeMs = MaxMetadataLoadMs = 0;
        MetadataAllocationBytes = 0;
        _sequenceCreated = 0;
        _lastFrame = Now;
        Active = true;
        Mark("observe_begin");
    }

    internal static void Sample(double delta)
    {
        if (!Active) return;
        long now = Now;
        double gap = Ms(now - _lastFrame);
        _lastFrame = now;
        MaxFrameMs = Math.Max(MaxFrameMs, gap);
        bool valid = Sequence != null && GodotObject.IsInstanceValid(Sequence);
        bool spriteValid = Sprite != null && GodotObject.IsInstanceValid(Sprite);
        bool warm = valid && spriteValid && _sequenceCreated != 0 && Ms(now - _sequenceCreated) > 500
            && Sprite!.Frame > 0 && Field(Sequence!, "_closed") is false;
        if (warm) MaxWarmFrameMs = Math.Max(MaxWarmFrameMs, gap);
        Frames.Add(new {
            @case = Current, ticks = now, frame = Engine.GetProcessFrames(), wallMs = gap,
            delta, timeScale = Engine.TimeScale,
            pose = valid ? Field(Sequence!, "_poseTime") : null,
            phaseFrom = valid ? Field(Sequence!, "_phaseFrom") : null,
            phaseTo = valid ? Field(Sequence!, "_phaseTo") : null,
            phaseElapsed = valid ? Field(Sequence!, "_phaseElapsed") : null,
            closed = valid ? Field(Sequence!, "_closed") : null,
            spriteFrame = spriteValid ? Sprite!.Frame : -1,
            animation = spriteValid ? Sprite!.Animation.ToString() : null,
            gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2),
            allocated = GC.GetTotalAllocatedBytes(false)
        });
    }

    internal static void Install()
    {
        var harmony = new Harmony("test.b130.star-slash.performance");
        Assembly product = typeof(SGC_StarSlash).Assembly;
        Assembly game = typeof(NGame).Assembly;
        void Add(Assembly assembly, string name, params string[] methods)
        {
            Type type = assembly.GetType(name) ?? throw new Exception("Missing probe type: " + name);
            foreach (string method in methods)
            {
                MethodInfo[] found = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static).Where(m => m.Name == method &&
                        m.DeclaringType == type && !m.IsGenericMethodDefinition).ToArray();
                if (found.Length == 0) throw new Exception("Missing probe method: " + name + "." + method);
                foreach (MethodInfo info in found)
                {
                    Patch(info);
                    if (info.GetCustomAttribute<AsyncStateMachineAttribute>() is { } attribute)
                    {
                        MethodInfo? move = attribute.StateMachineType.GetMethod("MoveNext",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (move != null) Patch(move);
                    }
                }
            }
        }
        void Patch(MethodInfo method)
        {
            if (!Patched.Add(method)) return;
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(Trace), nameof(Prefix)),
                postfix: method.ReturnType == typeof(void) ? null : new HarmonyMethod(typeof(Trace), nameof(Postfix)),
                finalizer: new HarmonyMethod(typeof(Trace), nameof(Finalizer)));
            Mark("probe_installed", new { type = method.DeclaringType?.FullName, method = method.Name });
        }
        Add(product, "ShinGetterMod.Models.Cards.SGC_StarSlash", "OnPlay");
        Add(product, "ShinGetterMod.Nodes.Combat.NShinGetterStarSlashSequence", "TryCreate", "Phase",
            "WaitForSelection", "Confirm", "PlayToImpact", "Recover", "Close", "End", "_ExitTree", "_Draw", "MergeOutline", "UpdateForeground");
        Add(product, "ShinGetterMod.Nodes.Combat.NShinGetterStarSlashData", "Load", "BuildWeapon");
        Add(product, "ShinGetterMod.Nodes.Vfx.ShinGetterCombatVfx", "PlayHeavyCleave");
        Add(product, "ShinGetterMod.Audio.ShinGetterVoiceService", "TryPlayStarSlashPreparation",
            "FinishStarSlashPreparation", "TryPlayCardVoiceAtCustomTiming", "OnAfterDamageGiven", "PlaySubtitle");
        Add(product, "ShinGetterMod.Audio.ShinGetterExecutionMusicService", "TryStart", "StartPlayback",
            "StopAndRestore", "StopStateAndRestore", "CompleteStop");
        Add(product, "ShinGetterMod.Nodes.Combat.NShinGetterSpriteAnimationStateMachine", "PlayIdle");
        Add(game, "MegaCrit.Sts2.Core.Commands.Builders.AttackCommand", "Execute");
        Add(game, "MegaCrit.Sts2.Core.Commands.CreatureCmd", "Damage", "Kill", "KillWithoutCheckingWinCondition");
        Add(game, "MegaCrit.Sts2.Core.Commands.CardCmd", "Exhaust");
        Add(game, "MegaCrit.Sts2.Core.Nodes.Combat.NCreature", "StartDeathAnim", "AnimDie", "_ExitTree");
        Add(game, "MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom", "RemoveCreatureNode", "RemoveCreatureWhenGone");
        Add(game, "MegaCrit.Sts2.Core.Combat.CombatManager", "CheckWinCondition", "EndCombatInternal");
        Add(game, "MegaCrit.Sts2.Core.Nodes.Combat.NCombatUi", "OnCombatWon", "ShowRewards");
        Add(game, "MegaCrit.Sts2.Core.Commands.Cmd", "Wait");
        Add(game, "MegaCrit.Sts2.Core.Hooks.Hook", "BeforeDeath", "AfterDeath", "AfterAttack", "AfterCombatEnd", "AfterCombatVictory");
    }

    private static string Label(MethodBase method) => method.DeclaringType!.FullName + "." + method.Name;

    private static void Prefix(MethodBase __originalMethod, out Span? __state)
    {
        __state = Active ? new Span(Current, Label(__originalMethod), Now,
            GC.GetAllocatedBytesForCurrentThread(), new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) }) : null;
        if (__state != null && !Frequent.Contains(__originalMethod.Name))
            Mark("call_begin", new { method = __state.Method }, __state.Case);
    }

    private static void Postfix(MethodBase __originalMethod, object? __result, Span? __state)
    {
        if (__state == null) return;
        if (__originalMethod.Name == "TryCreate" && __result is Node sequence)
        {
            Sequence = sequence;
            Sprite = Field(sequence, "_sprite") as AnimatedSprite2D;
            _sequenceCreated = Now;
            ++_sequenceId;
            Mark("sequence_created", new { id = _sequenceId, nativeId = sequence.GetInstanceId() }, __state.Case);
        }
        if (__result is Task task)
        {
            string label = __state.Method;
            Span span = __state;
            int taskId = task.Id;
            Mark("task_returned", new { method = label, taskId, task.IsCompleted }, span.Case);
            _ = task.ContinueWith(done => {
                long end = Now;
                Completed[span.Case + "|" + label] = end;
                Mark("task_complete", new { method = label, taskId, status = done.Status.ToString(),
                    elapsedMs = Ms(end - span.Start), error = done.Exception?.ToString() }, span.Case);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    private static void Finalizer(MethodBase __originalMethod, Exception? __exception, Span? __state)
    {
        if (__state == null) return;
        double duration = Ms(Now - __state.Start);
        if (__originalMethod.Name == "MergeOutline") MaxMergeMs = Math.Max(MaxMergeMs, duration);
        if (__originalMethod.DeclaringType?.Name == "NShinGetterStarSlashData" && __originalMethod.Name == "Load")
        {
            MaxMetadataLoadMs = Math.Max(MaxMetadataLoadMs, duration);
            MetadataAllocationBytes += GC.GetAllocatedBytesForCurrentThread() - __state.Allocation;
        }
        if (__originalMethod.Name == "_Draw")
        {
            lock (Gate) MaxDrawMs = Math.Max(MaxDrawMs, duration);
            if (duration < 25 && __exception == null) return;
        }
        if (Frequent.Contains(__originalMethod.Name) && duration < 25 && __exception == null) return;
        Mark("sync_end", new { method = __state.Method, elapsedMs = duration,
            spriteFrame = Sprite != null && GodotObject.IsInstanceValid(Sprite) ? Sprite.Frame : -1,
            poseTime = Sequence != null && GodotObject.IsInstanceValid(Sequence) ? Field(Sequence, "_poseTime") : null,
            allocationBytes = GC.GetAllocatedBytesForCurrentThread() - __state.Allocation,
            gcDelta = new[] { GC.CollectionCount(0) - __state.Gc[0], GC.CollectionCount(1) - __state.Gc[1], GC.CollectionCount(2) - __state.Gc[2] },
            error = __exception?.ToString() }, __state.Case);
    }

    internal static void Save(string root, string attempt, string caseName)
    {
        Active = false;
        var options = new JsonSerializerOptions { WriteIndented = true };
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "evidence", attempt + "-" + caseName + "-frames.json"), JsonSerializer.Serialize(Frames, options));
        System.IO.File.WriteAllText(System.IO.Path.Combine(root, "evidence", attempt + "-timeline.json"), JsonSerializer.Serialize(Events.ToArray(), options));
    }
}
