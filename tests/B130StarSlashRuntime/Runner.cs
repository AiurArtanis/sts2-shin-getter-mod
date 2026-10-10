using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using ShinGetterMod.Config;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Powers;

namespace B130StarSlashRuntime;

[ModInitializer("Init")]
public static class TestInit
{
    public static void Init()
    {
        if (System.Environment.GetEnvironmentVariable("B130_KILL_TEST") != "1") return;
        ((SceneTree)Engine.GetMainLoop()).Root.CallDeferred(Node.MethodName.AddChild, new Runner());
    }
}

public partial class Runner : Node
{
    private string Root => System.Environment.GetEnvironmentVariable("B130_KILL_ROOT")!;
    private string Attempt => System.Environment.GetEnvironmentVariable("B130_KILL_ATTEMPT") ?? "baseline1";
    private readonly List<object> _results = new();
    private DevConsole _console = null!;
    private Player _player = null!;
    private bool _started;
    private bool _godMode;

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless")
        {
            if (DisplayServer.WindowGetPosition() != new Vector2I(-10000, -10000))
                DisplayServer.WindowSetPosition(new Vector2I(-10000, -10000));
            if (DisplayServer.WindowGetSize() != new Vector2I(1280, 720))
                DisplayServer.WindowSetSize(new Vector2I(1280, 720));
        }
        Trace.Sample(delta);
        if (!_started && NGame.Instance?.MainMenu is { } menu && menu.IsNodeReady()
            && menu.IsVisibleInTree() && !NGame.Instance.Transition.InTransition)
        {
            _started = true;
            _ = Run();
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("ASSERT: " + message);
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Until(Func<bool> condition, double seconds, string description)
    {
        long begin = Trace.Now;
        while (!condition())
        {
            if (Trace.Ms(Trace.Now - begin) > seconds * 1000) throw new TimeoutException(description);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    private async Task Await(Task task, double timeout = 40)
    {
        await Until(() => task.IsCompleted, timeout, "native task completion");
        await task;
    }
    private async Task Cmd(string command)
    {
        var result = _console.ProcessNetCommand(_player, command);
        Trace.Mark("console", new { command, result.success, result.msg });
        Check(result.success, result.msg);
        if (result.task != null) await Await(result.task);
        await Wait(.05);
    }
    private async Task Clear(PowerModel? power)
    {
        if (power != null) await Cmd($"power {power.Id.Entry} {-power.Amount} 0");
    }
    private static IEnumerable<Node> Nodes(Node root)
    {
        yield return root;
        foreach (Node child in root.GetChildren()) foreach (Node node in Nodes(child)) yield return node;
    }
    private async Task Prepare(string form, string outcome, ShinGetterVoiceMode voice, bool bgm, bool turnTwo)
    {
        Trace.Active = false;
        SaveManager.Instance.PrefsSave.FastMode = FastModeType.Normal;
        ShinGetterChunibyoConfigService.Current.VoiceMode = ShinGetterVoiceMode.Silent;
        ShinGetterChunibyoConfigService.Current.BgmEnabled = bgm;
        await Cmd("fight BOWLBUGS_WEAK");
        await Until(() => CombatManager.Instance.IsInProgress && _player.PlayerCombatState?.Phase.ToString() == "Play"
            && NCombatRoom.Instance is { } room && room.IsNodeReady(), 40, "combat ready");
        await Wait(.4);
        Check(NModalContainer.Instance?.OpenModal == null, "FTUE/modal must not block test");
        if (!_godMode) { await Cmd("godmode"); _godMode = true; }
        await Clear(_player.Creature.GetPower<StrengthPower>());
        foreach (PowerModel power in _player.Creature.Powers.Where(p => p.GetType().Name is
            "SGP_ShinGetterOne" or "SGP_ShinGetterTwo" or "SGP_ShinGetterThree" or "SGP_ShinForm").ToArray()) await Clear(power);
        await Cmd($"power {form} 1 0");
        await Clear(_player.Creature.GetPower<VigorPower>());
        await Clear(_player.Creature.GetPower<SGP_HotBlood>());
        foreach (Creature enemy in CombatManager.Instance.DebugOnlyGetState()!.Enemies)
        {
            enemy.SetMaxHpInternal(1000);
            enemy.SetCurrentHpInternal(1000);
        }
        if (turnTwo)
        {
            PlayerCmd.EndTurn(_player, canBackOut: false);
            await Until(() => _player.PlayerCombatState is { TurnNumber: >= 2 } state && state.Phase.ToString() == "Play", 40, "native turn two");
        }
        var state = CombatManager.Instance.DebugOnlyGetState()!;
        if (outcome == "last")
        {
            Creature[] extras = state.Enemies.Skip(1).ToArray();
            await CreatureCmd.Kill(extras);
            await Until(() => state.Enemies.Count == 1, 15, "last-target fixture native kill");
            await Wait(3);
        }
        Creature target = state.Enemies.First();
        target.SetCurrentHpInternal(outcome == "nonlethal" ? 1000 : 1);
        // Ensure the real selection screen has multiple options and is not auto-selected.
        if (_player.PlayerCombatState!.DrawPile.Cards.Count < 2)
        {
            await Cmd("card S_G_C_STRIKE draw");
            await Cmd("card S_G_C_DEFEND draw");
        }
        await Cmd("energy 20");
        ShinGetterChunibyoConfigService.Current.VoiceMode = voice;
    }

    private async Task<object> Play(string name, string outcome)
    {
        await Cmd("card S_G_C_STAR_SLASH");
        CardModel card = _player.PlayerCombatState!.Hand.Cards.Last(c => c is SGC_StarSlash);
        Creature target = CombatManager.Instance.DebugOnlyGetState()!.Enemies.First();
        int energy = _player.PlayerCombatState.Energy;
        var hits = new List<int>();
        var exhausted = new List<CardModel>();
        int exhaustBefore = _player.PlayerCombatState.ExhaustPile.Cards.Count;
        Action<int, int> hp = (before, after) => {
            if (after < before) { hits.Add(before - after); Trace.Mark("target_hp_changed", new { before, after, amount = before - after }); }
        };
        target.CurrentHpChanged += hp;
        var action = new PlayCardAction(card, target);
        Trace.Begin(name);
        long start = Trace.Now;
        try
        {
            Check(card.CanPlay(out _, out _), "Star Slash playable");
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
            Trace.Mark("play_enqueued", new { action.Id, card = card.Id.Entry, target = target.CombatId,
                outcome, _player.PlayerCombatState.TurnNumber, hp = target.CurrentHp });
            await Until(() => Nodes(GetTree().Root).OfType<NCombatPileCardSelectScreen>().Any(), 30, "real Star Slash selection");
            var screen = Nodes(GetTree().Root).OfType<NCombatPileCardSelectScreen>().First();
            Check(Trace.Completed.ContainsKey(name + "|ShinGetterMod.Audio.ShinGetterVoiceService.TryPlayStarSlashPreparation"),
                "preparation audio completed before real selection");
            Trace.Mark("selection_visible", new { frame = Engine.GetProcessFrames(),
                preparationComplete = Trace.Completed.ContainsKey(name + "|ShinGetterMod.Audio.ShinGetterVoiceService.TryPlayStarSlashPreparation") });
            CardModel selected = _player.PlayerCombatState.DrawPile.Cards.First();
            exhausted.Add(selected);
            AccessTools.Method(typeof(NCombatPileCardSelectScreen), "OnCardClicked").Invoke(screen, new object[] { selected });
            Trace.Mark("selection_clicked", new { card = selected.Id.Entry });
            await Await(action.CompletionTask, 45);
            Trace.Mark("action_complete", new { state = action.State.ToString(), exception = action.Exception?.ToString() });
            Check(action.Exception == null, "native action exception");
            Check(hits.Count == 1, "exactly one native target HP reduction");
            if (outcome != "last")
            {
                Check(_player.PlayerCombatState.Energy == energy - 3, "energy cost once");
                Check(_player.PlayerCombatState.ExhaustPile.Cards.Count == exhaustBefore + 1
                    && _player.PlayerCombatState.ExhaustPile.Cards.Contains(selected), "one selected draw card exhausted");
                Check(outcome == "nonlethal" ? target.IsAlive : target.IsDead, "expected target life result");
                await Until(() => Trace.Sprite != null && GodotObject.IsInstanceValid(Trace.Sprite)
                    && !Trace.Sprite.HasMeta("shin_getter_star_slash_owner"), 10, "sprite owner released");
                Trace.Mark("interactable", new { _player.PlayerCombatState.Energy, phase = _player.PlayerCombatState.Phase.ToString(),
                    animation = Trace.Sprite!.Animation.ToString() });
                Check(Trace.Sprite.Animation.ToString() == "idle", "idle restored after ongoing combat");
                await Wait(.5);
            }
            else
            {
                await Until(() => !CombatManager.Instance.IsInProgress, 20, "native last-kill end combat");
                await Until(() => Trace.Completed.ContainsKey(name + "|MegaCrit.Sts2.Core.Nodes.Combat.NCombatUi.ShowRewards"), 20, "native rewards interactable");
                Trace.Mark("rewards_interactable");
                await Wait(.3);
            }
            Check(!Nodes(GetTree().Root).Any(n => n.GetType().Name == "NShinGetterStarSlashSequence"), "sequence cleanup");
            if (System.Environment.GetEnvironmentVariable("B130_KILL_PERFORMANCE") == "1")
            {
                Check(Trace.MaxDrawMs < 100, "production draw exceeds 100ms: " + Trace.MaxDrawMs);
                Check(Trace.MaxWarmFrameMs < 250, "warm animation frame exceeds 250ms: " + Trace.MaxWarmFrameMs);
            }
            return new { name, status = "PASS", elapsedMs = Trace.Ms(Trace.Now - start), hits = hits.ToArray(),
                maxFrameMs = Trace.MaxFrameMs, maxDrawMs = Trace.MaxDrawMs, maxWarmFrameMs = Trace.MaxWarmFrameMs,
                maxMergeMs = Trace.MaxMergeMs, maxMetadataLoadMs = Trace.MaxMetadataLoadMs,
                metadataAllocationBytes = Trace.MetadataAllocationBytes, frameCount = Trace.Frames.Count };
        }
        finally
        {
            target.CurrentHpChanged -= hp;
            Trace.Save(Root, Attempt, name);
        }
    }

    private async Task Case(string form, string outcome, ShinGetterVoiceMode voice, int repeat, bool bgm = false, bool turnTwo = false, bool sameCombat = false)
    {
        string stem = (form.Contains("SHIN_FORM") ? "dragon" : "one") + "_" + outcome + "_" + voice + "_bgm" + bgm + "_turn" + (turnTwo ? 2 : 1);
        for (int iteration = 0; iteration < repeat; iteration++)
        {
            string name = stem + "_r" + iteration;
            try
            {
                if (iteration == 0 || !sameCombat) await Prepare(form, outcome, voice, bgm, turnTwo);
                else { await Clear(_player.Creature.GetPower<SGP_HotBlood>()); await Cmd("energy 20"); }
                object result = await Play(name, outcome);
                _results.Add(result);
                GD.Print("B130_KILL_CASE " + JsonSerializer.Serialize(result));
            }
            catch (Exception exception)
            {
                Trace.Mark("case_failure", new { error = exception.ToString() });
                Trace.Save(Root, Attempt, name);
                var failure = new { name, status = "FAIL", error = exception.ToString() };
                _results.Add(failure);
                GD.Print("B130_KILL_CASE " + JsonSerializer.Serialize(failure));
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(Root, "evidence", Attempt + "-results.json"), JsonSerializer.Serialize(_results, new JsonSerializerOptions { WriteIndented = true }));
            if (!sameCombat || iteration == repeat - 1)
            {
                Trace.Active = false;
                if (CombatManager.Instance.IsInProgress) await Cmd("win");
                await Wait(.4);
                if (_godMode && RunManager.Instance.IsInProgress) { await Cmd("godmode"); _godMode = false; }
            }
        }
    }

    private async Task Run()
    {
        int exit = 1;
        try
        {
            string data = ProjectSettings.GlobalizePath("user://");
            Check(data.Replace('\\', '/').StartsWith(Root.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase), "isolated user data root: " + data);
            Engine.MaxFps = 60;
            Trace.Mark("runtime_identity", new { productMvid = typeof(SGC_StarSlash).Assembly.ManifestModule.ModuleVersionId,
                productPath = typeof(SGC_StarSlash).Assembly.Location,
                productSha256 = Convert.ToHexString(SHA256.HashData(System.IO.File.ReadAllBytes(typeof(SGC_StarSlash).Assembly.Location))),
                officialMvid = typeof(NGame).Assembly.ManifestModule.ModuleVersionId, display = DisplayServer.GetName(), userData = data,
                maxFps = Engine.MaxFps, attempt = Attempt });
            string mode = System.Environment.GetEnvironmentVariable("B130_KILL_SET") ?? "matrix";
            if (mode.StartsWith("geometry"))
            {
                OutlineRegression.Run(Root, Attempt, mode == "geometry-spot");
                exit = 0;
                return;
            }
            Trace.Install();
            ShinGetterChunibyoConfigService.Load();
            ShinGetterChunibyoConfigService.Current.VoiceMode = ShinGetterVoiceMode.Silent;
            ShinGetterChunibyoConfigService.Current.BgmEnabled = false;
            ShinGetterChunibyoConfigService.Current.LastReadUpdateVersion = "v1.2.2";
            Check(SaveManager.Instance.SeenFtue("combat_rules_ftue"), "isolated FTUE fixture");
            var character = ModelDb.AllCharacters.Single(c => c.GetType().Name == "ShinGetter");
            var run = await NGame.Instance!.StartNewSingleplayerRun(character, false, ModelDb.Acts.ToList(), Array.Empty<ModifierModel>(), "B130STARPERF", GameMode.Standard, 0);
            _player = run.Players.Single();
            _console = new DevConsole(true);
            CombatManager.Instance.CombatWon += _ => Trace.Mark("combat_won_event");
            CombatManager.Instance.CombatEnded += _ => Trace.Mark("combat_ended_event");
            foreach (string form in new[] { "S_G_P_SHIN_GETTER_ONE", "S_G_P_SHIN_FORM" })
            {
                foreach (string outcome in new[] { "nonlethal", "survivor", "last" })
                    await Case(form, outcome, ShinGetterVoiceMode.Silent, mode == "smoke" ? 1 : 2);
                if (mode == "smoke") continue;
                await Case(form, "survivor", ShinGetterVoiceMode.Always, 1);
                await Case(form, "last", ShinGetterVoiceMode.Always, 1);
                await Case(form, "nonlethal", ShinGetterVoiceMode.OncePerCombat, 2, sameCombat: true);
                await Case(form, "last", ShinGetterVoiceMode.Silent, 1, bgm: false, turnTwo: true);
                await Case(form, "last", ShinGetterVoiceMode.Silent, 1, bgm: true, turnTwo: true);
            }
            Trace.Mark("suite_completed", new { count = _results.Count });
            Trace.Save(Root, Attempt, "final");
            exit = _results.Any(r => JsonSerializer.Serialize(r).Contains("\"FAIL\"")) ? 1 : 0;
        }
        catch (Exception exception)
        {
            Trace.Mark("suite_failure", new { error = exception.ToString() });
            Trace.Save(Root, Attempt, "startup_failure");
            GD.PushError(exception.ToString());
        }
        finally
        {
            GD.Print("B130_KILL_SUITE_EXIT " + exit);
            await Wait(.25);
            GetTree().Quit(exit);
        }
    }
}
