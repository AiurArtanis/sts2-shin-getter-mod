using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using ShinGetterMod.Nodes.Combat;
using ShinGetterMod.Nodes.Vfx;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

int passed = 0;
async Task Check(string name, Func<Task> test)
{
    SaveManager.Instance.PrefsSave.FastMode = FastModeType.Normal;
    CombatManager.Instance.IsEnding = false;
    NonInteractiveMode.IsActive = false;
    NShinGetterStaticVisuals.BlockGate = null;
    NShinGetterStaticVisuals.OriginalFusionCalls = 0;
    NShinGetterStaticVisuals.OriginalFusionSucceeds = true;
    await test().WaitAsync(TimeSpan.FromSeconds(3));
    Console.WriteLine("PASS " + name);
    passed++;
}
static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
static void Near(double actual, double expected) => Assert(Math.Abs(actual - expected) < 1e-8, $"{actual} != {expected}");
static void Clean(Node node, Tween tween) => Assert(node.Subscribers == 0 && node.Tree.Subscribers == 0
    && tween.Subscribers == 0 && !tween.IsValid(), "Signals or Tween survived completion");
static async Task Canceled(Task task)
{
    try { await task; throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { }
}
var oldFrames = new TacticalRetreatFrames(new[] { 1d, 2d, .5d }, 10, .5);
var nextFrames = new TacticalRetreatFrames(new[] { 3d, 1d }, 8, .75);
var timeline = new TacticalRetreatTimeline(oldFrames, .2, nextFrames);

await Check("real weighted frame units, FPS and speed determine the whole duration", () =>
{
    Near(timeline.Duration, .7 + .2 + 4d / 8 / .75);
    Assert(oldFrames.FrameAt(0, true) == 2 && oldFrames.FrameAt(.101, true) == 1
        && oldFrames.FrameAt(.501, true) == 0, "Reverse ignored per-frame durations");
    return Task.CompletedTask;
});
await Check("reverse, hold and forward boundaries share one progressing movement clock", () =>
{
    Assert(timeline.Sample(0).Phase == TacticalRetreatPhase.Reverse, "Missing reverse");
    var hold = timeline.Sample(.8);
    Assert(hold.Phase == TacticalRetreatPhase.Hold && hold.Frame == 0, "Missing existing hold");
    Near(hold.Progress, .8 / timeline.Duration);
    Assert(timeline.Sample(.901).Phase == TacticalRetreatPhase.Forward, "Missing forward");
    var end = timeline.Sample(timeline.Duration);
    Assert(end.Frame == 1 && end.Progress == 1, "Movement and last fusion frame did not end together");
    return Task.CompletedTask;
});
await Check("no old form means no invented reverse or hold", () =>
{
    Near(new TacticalRetreatTimeline(null, .2, nextFrames).Duration, nextFrames.Duration);
    Near(new TacticalRetreatFrames(new[] { 3d, 1d }, 8, 1.5).Duration, nextFrames.Duration / 2);
    return Task.CompletedTask;
});
await Check("invalid animation data cannot start an endless clock", () =>
{
    foreach (double fps in new[] { 0, double.NaN, double.PositiveInfinity })
    {
        try { _ = new TacticalRetreatFrames(new[] { 1d }, fps, 1); throw new Exception("Invalid FPS accepted"); }
        catch (ArgumentException) { }
    }
    try { _ = new TacticalRetreatFrames(new[] { -1d }, 60, 1); throw new Exception("Invalid frame accepted"); }
    catch (ArgumentException) { }
    return Task.CompletedTask;
});
await Check("scope follows awaits and matches only the same creature", async () =>
{
    var owner = new object(); var other = new object();
    using (var scope = new TacticalRetreatSync(owner, _ => { }, default))
    {
        await Task.Yield();
        Assert(ReferenceEquals(TacticalRetreatSync.For(owner), scope) && TacticalRetreatSync.For(other) == null, "Scope leaked");
        using (var child = new TacticalRetreatSync(other, _ => { }, default))
            Assert(ReferenceEquals(TacticalRetreatSync.For(owner), scope), "Nested owner hid parent");
    }
    Assert(TacticalRetreatSync.For(owner) == null && TacticalRetreatSync.For(other) == null, "Scope not released");
});
await Check("parallel creatures cannot see each other's synchronization scope", async () =>
{
    var a = new object(); var b = new object();
    async Task Child(object own, object other)
    {
        using var scope = new TacticalRetreatSync(own, _ => { }, default);
        await Task.Yield();
        Assert(ReferenceEquals(TacticalRetreatSync.For(own), scope) && TacticalRetreatSync.For(other) == null, "Parallel leak");
    }
    await Task.WhenAll(Child(a, b), Child(b, a));
});
await Check("one production Tween clock updates visual and return samples; waits until complete", async () =>
{
    var node = new Node(); var tween = new Tween(); var samples = new List<TacticalRetreatSample>();
    double movement = -1;
    Task play = TacticalRetreatTween.PlayClock(node, tween, (float)timeline.Duration, progress =>
    {
        var sample = timeline.Sample(timeline.Duration * progress);
        samples.Add(sample); movement = sample.Progress;
    }, default);
    Assert(!play.IsCompleted && samples.Count == 1 && movement == 0, "Clock did not start at zero");
    tween.Tick(.5f);
    Near(movement, .5);
    Assert(!play.IsCompleted && samples.Last().Progress == movement, "Separate clocks");
    tween.Complete(); await play;
    Assert(samples.Last().Frame == 1 && movement == 1, "Incomplete terminal sample"); Clean(node, tween);
});
foreach (string reason in new[] { "node exit", "token", "external kill", "combat end" })
    await Check(reason + " settles wait and disconnects every signal", async () =>
    {
        var node = new Node(); var tween = new Tween(); using var cts = new CancellationTokenSource();
        Task play = TacticalRetreatTween.PlayClock(node, tween, 1, _ => { }, cts.Token);
        switch (reason)
        {
            case "node exit": node.Exit(); break;
            case "token": cts.Cancel(); break;
            case "external kill": tween.Kill(); node.Tree.Pulse(); break;
            case "combat end": CombatManager.Instance.IsEnding = true; node.Tree.Pulse(); break;
        }
        await Canceled(play); Clean(node, tween);
    });
await Check("asynchronous update faults are observed, not swallowed by the native callback", async () =>
{
    var node = new Node(); var tween = new Tween();
    Task play = TacticalRetreatTween.PlayClock(node, tween, 1, progress =>
    { if (progress > 0) throw new InvalidOperationException("frame-update"); }, default);
    tween.Tick(.1f);
    try { await play; throw new Exception("Fault swallowed"); }
    catch (InvalidOperationException ex) when (ex.Message == "frame-update") { }
    Clean(node, tween);
});
await Check("initial update faults kill a not-yet-started Tween", async () =>
{
    var node = new Node(); var tween = new Tween();
    try { await TacticalRetreatTween.PlayClock(node, tween, 1, _ => throw new InvalidOperationException("initial"), default); }
    catch (InvalidOperationException ex) when (ex.Message == "initial") { }
    Clean(node, tween);
});
await Check("already-exited owner cannot leave a Finished wait suspended", async () =>
{
    var node = new Node { Inside = false }; var tween = new Tween();
    await Canceled(TacticalRetreatTween.Wait(node, tween, default)); Clean(node, tween);
});
await Check("instant mode commits one common terminal state without starting a timer", async () =>
{
    SaveManager.Instance.PrefsSave.FastMode = FastModeType.Instant;
    var node = new Node(); var tween = new Tween(); var samples = new List<float>();
    await TacticalRetreatTween.PlayClock(node, tween, 10, samples.Add, default);
    Assert(samples.SequenceEqual(new[] { 1f }) && !tween.Running, "Instant mode retained an animation wait"); Clean(node, tween);
});
await Check("pre-canceled clock does not update visuals or leave signals", async () =>
{
    var node = new Node(); var tween = new Tween(); using var cts = new CancellationTokenSource();
    cts.Cancel(); int updates = 0;
    await Canceled(TacticalRetreatTween.PlayClock(node, tween, 1, _ => updates++, cts.Token));
    Assert(updates == 0, "Canceled clock moved visuals"); Clean(node, tween);
});
await Check("disposed or canceled scopes cannot move another animation", async () =>
{
    var owner = new object(); using var cts = new CancellationTokenSource();
    var scope = new TacticalRetreatSync(owner, _ => throw new Exception("Canceled move"), cts.Token);
    cts.Cancel(); await Canceled(Task.Run(() => scope.Advance(.5)));
    scope.Dispose();
    try { scope.Advance(1); throw new Exception("Disposed scope active"); }
    catch (ObjectDisposedException) { }
    Assert(TacticalRetreatSync.For(owner) == null, "Disposed scope retained");
});
static (Creature owner, NCreature node, NCreatureVisuals visuals, AnimatedSprite2D[] sprites) Scene(int? previous = 0)
{
    var owner = new Creature(); var node = new NCreature { GlobalPosition = new(12, 34) };
    NCombatRoom.Instance = new(); NCombatRoom.Instance.Nodes.Add(owner, node);
    string[] names = { "GetterOne", "GetterTwo", "GetterThree", "ShinDragon" };
    var sprites = names.Select((name, index) => new AnimatedSprite2D
    {
        Name = name, Visible = previous == index, Modulate = new(previous == index ? 1 : 0),
        SpriteFrames = new() { Units = new[] { 1d, 2d + index, .5d }, Fps = 60 + index * 10 },
    }).ToArray();
    return (owner, node, new NCreatureVisuals(), sprites);
}
static void Terminal(NCreature node, NCreatureVisuals visuals, AnimatedSprite2D[] sprites, int target, bool idleExpected = true)
{
    Assert(node.GlobalPosition == new Vector2(12, 34), "Position was not restored");
    Assert(sprites.Select((sprite, index) => sprite.Visible == (index == target)).All(value => value), "Wrong final visible form");
    if (idleExpected) Assert(sprites[target].Animation.Value == "idle" && sprites[target].IdleCalls > 0, "Idle was not restored");
    else Assert(sprites[target].IdleCalls == 0, "Canceled/dead action restarted Idle");
    Assert(node.Subscribers == 0 && visuals.Subscribers == 0 && visuals.Tree.Subscribers == 0, "Lifecycle signal leaked");
    Assert(node.Tweens.Concat(visuals.Tweens).All(tween => !tween.IsValid() && tween.Subscribers == 0), "Tween survived");
}
foreach (var (oldForm, nextForm) in new[] { (0, 1), (1, 2), (2, 0) })
    await Check($"production retreat + fusion {oldForm + 1}->{nextForm + 1} starts after retreat and ends at origin/Idle", async () =>
    {
        var (owner, node, visuals, sprites) = Scene(oldForm); int transforms = 0;
        Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner, () =>
        {
            transforms++;
            return NShinGetterStaticVisuals.RunSwitch(visuals, sprites, nextForm, .75f, TacticalRetreatSync.For(owner));
        });
        Assert(transforms == 0 && visuals.Tweens.Count == 0, "Fusion started before retreat finished");
        node.Tweens.Single().Complete();
        Assert(transforms == 1 && !play.IsCompleted && visuals.Tweens.Count == 1, "Missing fusion clock");
        var clock = visuals.Tweens.Single();
        double expected = sprites[oldForm].SpriteFrames!.Units.Sum() / sprites[oldForm].SpriteFrames!.Fps / .75
            + (double).2f + sprites[nextForm].SpriteFrames!.Units.Sum() / sprites[nextForm].SpriteFrames!.Fps / .75;
        Assert(Math.Abs(clock.Duration - expected) < 1e-6, "Clock duration differs from loaded frame data/hold");
        Assert(node.GlobalPosition.X == 12 - 480 && sprites[oldForm].Frame == 2, "Different start boundary");
        clock.Tick(.5f);
        Assert(node.GlobalPosition.X > 12 - 480 && node.GlobalPosition.X < 12, "Return did not share the clock");
        clock.Tick(1);
        Assert(node.GlobalPosition == new Vector2(12, 34) && sprites[nextForm].Frame == 2, "Different end boundary");
        clock.Complete(); await play;
        Assert(transforms == 1, "Duplicate transformation"); Terminal(node, visuals, sprites, nextForm);
    });
await Check("missing previous visual still completes next fusion and returns", async () =>
{
    var (owner, node, visuals, sprites) = Scene(null);
    Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner,
        () => NShinGetterStaticVisuals.RunSwitch(visuals, sprites, 1, .75f, TacticalRetreatSync.For(owner)));
    node.Tweens.Single().Complete();
    Assert(Math.Abs(visuals.Tweens.Single().Duration - sprites[1].SpriteFrames!.Units.Sum() / 70 / .75) < 1e-6, "Invented missing phases");
    visuals.Tweens.Single().Complete(); await play; Terminal(node, visuals, sprites, 1);
});
foreach (string fallback in new[] { "non-atomic previous", "missing fusion data" })
    await Check("production crossfade fallback: " + fallback, async () =>
    {
        var (owner, node, visuals, sprites) = Scene(fallback == "non-atomic previous" ? 3 : 0);
        if (fallback == "missing fusion data") sprites[1].FusionAvailable = false;
        Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner,
            () => NShinGetterStaticVisuals.RunSwitch(visuals, sprites, 1, .75f, TacticalRetreatSync.For(owner)));
        node.Tweens.Single().Complete();
        Assert(visuals.Tweens.Count == 1 && !play.IsCompleted, "Fallback lost the common clock");
        Assert(Math.Abs(visuals.Tweens.Single().Duration - .36f / .75f) < 1e-6, "Fallback duration invented");
        visuals.Tweens.Single().Tick(.5f);
        Assert(node.GlobalPosition.X > 12 - 480 && node.GlobalPosition.X < 12, "Fallback did not return");
        visuals.Tweens.Single().Complete(); await play; Terminal(node, visuals, sprites, 1);
    });
await Check("same creature nested scope restores parent instead of retaining a child", () =>
{
    var owner = new object(); double parentProgress = 0, childProgress = 0;
    using var parent = new TacticalRetreatSync(owner, value => parentProgress = value, default);
    using (var child = new TacticalRetreatSync(owner, value => childProgress = value, default))
        TacticalRetreatSync.For(owner)!.Advance(.4);
    TacticalRetreatSync.For(owner)!.Advance(.8);
    Near(parentProgress, .8); Near(childProgress, .4); return Task.CompletedTask;
});
await Check("visual exit before block completes skips old-node motion but transforms exactly once", async () =>
{
    var (owner, node, _, _) = Scene(); var gate = new TaskCompletionSource();
    NShinGetterStaticVisuals.BlockGate = gate; int transforms = 0;
    Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner, () => { transforms++; return Task.CompletedTask; });
    node.Exit(); await play; gate.SetResult();
    Assert(transforms == 1 && node.Tweens.Count == 0 && node.Subscribers == 0, "Visual cancellation lost or duplicated model work");
});
await Check("production frame-update exception restores position, visible target and Idle", async () =>
{
    var (owner, node, visuals, sprites) = Scene();
    Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner,
        () => NShinGetterStaticVisuals.RunSwitch(visuals, sprites, 1, .75f, TacticalRetreatSync.For(owner)));
    node.Tweens.Single().Complete(); sprites[1].ThrowOnFrameUpdate = true;
    visuals.Tweens.Single().Tick(1);
    try { await play; throw new Exception("Frame fault swallowed"); }
    catch (InvalidOperationException ex) when (ex.Message == "frame-write") { }
    Terminal(node, visuals, sprites, 1);
});
await Check("production visual-only external Kill restores state and preserves the model tail", async () =>
{
    var (owner, node, visuals, sprites) = Scene();
    int modelTail = 0;
    Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner,
        async () =>
        {
            await NShinGetterStaticVisuals.RunSwitch(visuals, sprites, 1, .75f, TacticalRetreatSync.For(owner));
            modelTail++;
        });
    node.Tweens.Single().Complete(); visuals.Tweens.Single().Kill(); visuals.Tree.Pulse();
    await play; Assert(modelTail == 1, "Visual interruption skipped remaining model effects"); Terminal(node, visuals, sprites, 1);
});
await Check("owner visual exits during fusion while combat lives: model tail still runs once", async () =>
{
    var (owner, node, visuals, sprites) = Scene(); int tail = 0;
    Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner, async () =>
    {
        await NShinGetterStaticVisuals.RunSwitch(visuals, sprites, 1, .75f, TacticalRetreatSync.For(owner)); tail++;
    });
    node.Tweens.Single().Complete(); node.Exit(); await play;
    Assert(tail == 1, "Visual rebuild canceled a living model action"); Terminal(node, visuals, sprites, 1);
});
foreach (bool ownerDead in new[] { false, true })
    await Check(ownerDead ? "owner death retains action cancellation" : "combat end retains action cancellation", async () =>
    {
        var (owner, node, visuals, sprites) = Scene(); int tail = 0;
        Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner, async () =>
        {
            await NShinGetterStaticVisuals.RunSwitch(visuals, sprites, 1, .75f, TacticalRetreatSync.For(owner)); tail++;
        });
        node.Tweens.Single().Complete();
        if (ownerDead) { owner.IsAlive = false; node.Exit(); }
        else { CombatManager.Instance.IsEnding = true; visuals.Tree.Pulse(); }
        await Canceled(play); Assert(tail == 0, "Real action cancellation swallowed"); Terminal(node, visuals, sprites, 1, idleExpected: false);
    });
await Check("combat end during initial block does not restart or duplicate transformation", async () =>
{
    var (owner, node, _, _) = Scene(); var gate = new TaskCompletionSource(); int transforms = 0;
    NShinGetterStaticVisuals.BlockGate = gate;
    Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner, () => { transforms++; return Task.CompletedTask; });
    CombatManager.Instance.IsEnding = true; node.Exit(); await Canceled(play); gate.SetResult();
    Assert(transforms == 0 && node.Subscribers == 0, "Canceled model action restarted");
});
await Check("blocked/no-visual transform executes once and returns without waiting for a nonexistent clock", async () =>
{
    var (owner, node, visuals, _) = Scene(); int transforms = 0;
    Task play = ShinGetterCombatVfx.PlayTacticalRetreat(owner, () => { transforms++; return Task.CompletedTask; });
    node.Tweens.Single().Complete(); await play;
    Assert(transforms == 1 && visuals.Tweens.Count == 0 && node.GlobalPosition == new Vector2(12, 34), "No-visual transform hung");
});
await Check("ordinary SwitchTo routes to original fusion and does not acquire tactical sync", async () =>
{
    var (_, _, visuals, sprites) = Scene();
    await NShinGetterStaticVisuals.RunSwitch(visuals, sprites, 1, 1, null);
    Assert(NShinGetterStaticVisuals.OriginalFusionCalls == 1 && visuals.Tweens.Count == 0, "Ordinary path entered tactical clock");
});
Console.WriteLine($"PASS {passed} CPU source-linked cases; native Godot/game/rendering NOT tested.");
