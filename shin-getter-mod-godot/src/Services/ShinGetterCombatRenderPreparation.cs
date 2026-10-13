#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.TestSupport;
using ShinGetterMod.Models.Characters;

namespace ShinGetterMod.Services;

/// <summary>
/// Moves a fixed set of first-use GPU work into combat loading. This does not
/// eliminate compilation cost, alter live effects, or enlarge the sprite cache.
/// </summary>
internal static class ShinGetterCombatRenderPreparation
{
    // Shared by all stages/draws, not a fresh timeout per draw. Normal measured
    // preparation is about 3.3s; 10s allows cold-driver variation without infinity.
    private static readonly TimeSpan PreparationBudget = TimeSpan.FromSeconds(10);
    private static readonly string[] ScenePaths =
    {
        "res://scenes/combat/energy_counters/shin_getter_energy_counter.tscn",
        "res://scenes/vfx/ui/vfx_buff_applied.tscn",
        "res://scenes/vfx/hit_spark_vfx.tscn",
    };
    private static readonly ConditionalWeakTable<IRunState, Preparation> Preparations = new();

    private sealed class Preparation
    {
        public Task? Pending;
        public bool Completed;
        // Keep only these three scenes/materials alive for this run. No Node is retained.
        public readonly List<Resource> Resources = new();
    }

    public static Task PrepareAsync(IRunState runState)
    {
        if (TestMode.IsOn || DisplayServer.GetName() == "headless" || !RenderingServer.RenderLoopEnabled
            || !runState.Players.Any(p => p.Character is ShinGetter)
            || NGame.Instance is not { } game || !game.IsInsideTree())
            return Task.CompletedTask;

        Preparation state = Preparations.GetOrCreateValue(runState);
        if (state.Completed)
            return Task.CompletedTask;
        return state.Pending ??= PrepareCoreAsync(game, state);
    }

    private static async Task PrepareCoreAsync(NGame game, Preparation state)
    {
        // Install Pending before any synchronous failure/completion can clear it.
        // Godot's captured synchronization context resumes this on the main thread.
        await Task.Yield();
        var total = Stopwatch.StartNew();
        SubViewport? host = null;
        try
        {
            if (!RenderingServer.RenderLoopEnabled || !GodotObject.IsInstanceValid(game) || !game.IsInsideTree())
                return;
            Viewport mainViewport = game.GetViewport();
            host = new SubViewport
            {
                Name = "ShinGetterCombatRenderPreparation",
                Size = new Vector2I(256, 256),
                Disable3D = true,
                TransparentBg = true,
                UseHdr2D = mainViewport.UseHdr2D,
                Msaa2D = mainViewport.Msaa2D,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            };
            // No ViewportTexture/Control is exposed: preparation cannot appear,
            // receive player input, or steal focus, even with Instant transitions.
            game.AddChild(host);
            foreach (string path in ScenePaths)
            {
                if (!RenderingServer.RenderLoopEnabled || total.Elapsed >= PreparationBudget)
                    return;
                var stage = Stopwatch.StartNew();
                PackedScene scene = PreloadManager.Cache.GetScene(path);
                state.Resources.Add(scene);
                Node template = scene.Instantiate(); // Never enters the tree/native Ready.
                var particles = new List<GpuParticles2D>();
                try
                {
                    foreach (GpuParticles2D source in EnumerateParticles(template))
                    {
                        if (!source.Visible)
                            continue; // Hidden, unused alternate native sparks are not a budget.
                        var particle = (GpuParticles2D)source.Duplicate(0); // No scripts/signals.
                        particle.ProcessMaterial = source.ProcessMaterial;
                        particle.Material = source.Material;
                        particle.Texture = source.Texture;
                        particle.Position = new Vector2(128, 128);
                        state.Resources.Add(source.ProcessMaterial);
                        if (source.Material != null)
                            state.Resources.Add(source.Material);
                        particles.Add(particle);
                        host.AddChild(particle);
                        particle.Restart();
                    }
                    // Resource loading alone is insufficient. Wait for actual draws.
                    if (!await AwaitDrawWithinBudgetAsync(host, total) || !await AwaitDrawWithinBudgetAsync(host, total))
                        return;
                    GD.Print($"Shin Getter render preparation: {path}, particles={particles.Count}, ms={stage.Elapsed.TotalMilliseconds:F1}");
                }
                finally
                {
                    template.Free();
                    foreach (GpuParticles2D particle in particles)
                        if (GodotObject.IsInstanceValid(particle))
                            particle.QueueFree();
                }
                if (!await AwaitDrawWithinBudgetAsync(host, total))
                    return; // Flush stage-node disposal before the next scene.
            }
            state.Completed = true;
            GD.Print($"Shin Getter render preparation complete: ms={total.Elapsed.TotalMilliseconds:F1}; first-use work scheduled during loading, not eliminated.");
        }
        catch (Exception error)
        {
            // Optional render preparation must never block the original battle.
            GD.PushWarning("Shin Getter render preparation skipped: " + error.Message);
        }
        finally
        {
            if (host != null && GodotObject.IsInstanceValid(host))
                host.QueueFree();
            if (!state.Completed)
                state.Resources.Clear();
            state.Pending = null;
        }
    }

    private static IEnumerable<GpuParticles2D> EnumerateParticles(Node node)
    {
        if (node is GpuParticles2D particle)
            yield return particle;
        foreach (Node child in node.GetChildren())
            foreach (GpuParticles2D nested in EnumerateParticles(child))
                yield return nested;
    }

    // Kept as a single-wait lifecycle probe; production stages pass one shared clock.
    private static Task<bool> AwaitDrawAsync(SubViewport host) =>
        AwaitDrawWithinBudgetAsync(host, Stopwatch.StartNew());

    private static async Task<bool> AwaitDrawWithinBudgetAsync(SubViewport host, Stopwatch preparationClock)
    {
        if (!RenderingServer.RenderLoopEnabled || preparationClock.Elapsed >= PreparationBudget
            || !GodotObject.IsInstanceValid(host) || !host.IsInsideTree() || host.IsQueuedForDeletion())
            return false;
        SceneTree tree = host.GetTree();
        var completed = new TaskCompletionSource<bool>();
        bool CanContinue() => RenderingServer.RenderLoopEnabled
            && preparationClock.Elapsed < PreparationBudget
            && GodotObject.IsInstanceValid(host) && host.IsInsideTree() && !host.IsQueuedForDeletion();
        void OnDraw() => completed.TrySetResult(CanContinue());
        void OnExit() => completed.TrySetResult(false);
        // ProcessFrame is emitted by engine logic even when rendering is disabled.
        // All Godot checks/cleanup therefore stay on the main thread; no abandoned
        // WhenAny task or worker-thread timer can retain callbacks/temporary nodes.
        void OnProcess()
        {
            if (!CanContinue())
                completed.TrySetResult(false);
        }
        RenderingServer.FramePostDraw += OnDraw;
        host.TreeExiting += OnExit;
        tree.ProcessFrame += OnProcess;
        try { return await completed.Task; }
        finally
        {
            RenderingServer.FramePostDraw -= OnDraw;
            if (GodotObject.IsInstanceValid(tree))
                tree.ProcessFrame -= OnProcess;
            if (GodotObject.IsInstanceValid(host))
                host.TreeExiting -= OnExit;
        }
    }
}
