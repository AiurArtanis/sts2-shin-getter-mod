using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ShinGetterMod.Nodes.Events;

// Actual Godot Controls/Tweens + linked production guard. No sts2/Harmony/mod DLL.
public partial class Probe : Node
{
    private int _assertions;

    public override void _Ready()
    {
        bool legacy = OS.GetCmdlineUserArgs().Contains("--legacy");
        try
        {
            var failures = new List<string>();
            foreach (var scenario in new[] { ("fresh-resume", 80f, 1064f, false), ("reading-width-growth", 80f, 1328f, true), ("reading-width-shrink", 16f, 1000f, true) })
            {
                try { Scenario(scenario.Item1, scenario.Item2, scenario.Item3, scenario.Item4, legacy); }
                catch (Exception error) { failures.Add(error.Message); }
            }
            if (failures.Count != 0) throw new InvalidOperationException(string.Join("; ", failures));
            Tween? empty = null;
            ShinGetterContentTweenGuard.Stop(ref empty);
            Check(empty == null, "null guard must remain null");
            GD.Print($"ISSUE206_TWEEN_API_PASS assertions={_assertions} linked_production_guard=true game_loaded=false");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.Print("ISSUE206_TWEEN_API_RED " + error.Message);
            GetTree().Quit(1);
        }
    }

    private void Check(bool passed, string message)
    {
        _assertions++;
        if (!passed) throw new InvalidOperationException(message);
    }

    private void Scenario(string name, float staleX, float contentWidth, bool alreadyRunning, bool legacy)
    {
        var content = new Control { Position = new Vector2(staleX, 720), Size = new Vector2(contentWidth, 200) };
        AddChild(content);
        bool staleCallback = false;
        Tween? tween = content.CreateTween();
        tween.TweenProperty(content, "position", new Vector2(staleX, 100), 1.0)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tween.Parallel().TweenCallback(Callable.From(() => staleCallback = true)).SetDelay(0.8);
        if (alreadyRunning) tween.CustomStep(0.25);

        float viewportWidth = Math.Max(1160, contentWidth + 64);
        float desiredX = (viewportWidth - contentWidth) / 2;
        var previous = tween;
        if (!legacy)
        {
            ShinGetterContentTweenGuard.Stop(ref tween); // Exact production source, not a copied guard.
            Check(tween == null, name + ": stale native field not cleared");
            Check(!previous.IsValid(), name + ": old tween target still active");
            Check(!staleCallback, name + ": stale native focus callback fast-forwarded");
        }
        content.Position = new Vector2(desiredX, content.Position.Y);
        float correctedX = content.Position.X;
        // Exact relevant official109 native order (245-250 + 288), without the game assembly.
        if (tween != null)
        {
            tween.Pause();
            tween.CustomStep(1.0);
            tween.Kill();
            tween = null;
        }
        float capturedX = content.Position.X;
        tween = content.CreateTween();
        tween.TweenProperty(content, "position", new Vector2(capturedX, 80), 1.0)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tween.CustomStep(0.5);
        float midpointX = content.Position.X;
        tween.CustomStep(0.5);
        float finalX = content.Position.X;
        GD.Print($"ISSUE206_TWEEN_CASE name={name} legacy={legacy} desired_x={desiredX} corrected_x={correctedX} captured_x={capturedX} midpoint_x={midpointX} final_x={finalX}");
        ShinGetterContentTweenGuard.Stop(ref tween);
        content.Free();
        Check(Math.Abs(capturedX - desiredX) < 0.001, name + ": native captured stale X");
        Check(Math.Abs(midpointX - desiredX) < 0.001, name + ": next Tween overwrote X mid-animation");
        Check(Math.Abs(finalX - desiredX) < 0.001, name + ": next Tween overwrote X at completion");
    }
}
