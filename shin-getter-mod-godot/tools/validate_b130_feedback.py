#!/usr/bin/env python3
"""B1.3.0 visual-feedback source contracts, not a rendering/gameplay test."""

from pathlib import Path

from validate_b130_core import block, compact, has, ordered, read, require

ROOT = Path(__file__).resolve().parents[1]


def clock_contract(text: str) -> None:
    play = block(text, "public async Task PlayToImpact(")
    ordered(play, ["Task voice = intro();", "_sprite.Position = _origin + _recoil * retreat * retreat;",
                   "await voice;", "_ = release();", "_rushing = true;", "await Stage(0.28f,",
                   "(_origin + _recoil).Lerp(_origin + _lunge, u * u * u)", "_rushing = false;",
                   "await Stage(0.08f,"], "Recoil/voice/accelerating dash/impact share the same stage clock")
    process = block(text, "public override void _Process(")
    ordered(process, ["if (CombatManager.Instance.IsPaused) return;", "_stageTime += (float)delta;",
                      "UpdateTailHistory((float)delta);", "UpdateEnergyShell();", "QueueRedraw();"],
            "Pause freezes frame/position/baked energy and actual history ages")
    history = block(text, "private void UpdateTailHistory(")
    for fragment in ("_tailAges[i] += delta;", "if (!_rushing || _travelDistance < 20f) return;",
                     "if (_sampleTime < 0.04f) return;", "tail.GlobalTransform = _sprite.GlobalTransform;",
                     "GetFrameTexture(_sprite.Animation, _sprite.Frame)", "_tailAges[_tailCursor] = 0f;"):
        has(history, fragment, f"Bounded actual-frame motion snapshots: {fragment}")
    for forbidden in ("AddChild", "new Sprite2D", "tail.GlobalPosition -="):
        require(forbidden not in history, "History uses a fixed pool, not current-frame offset copies")
    draw = block(text, "public override void _Draw()")
    has(draw, "_impactPulse <= 0f", "Immediate drawing only handles the brief impact ring")
    has(draw, "DrawPolyline(ring", "Impact cue remains separate from the baked body energy")
    require("_sprite.Modulate =" not in text and "DrawPolyline(points" not in text,
            "Keep the original armor and avoid detached body arcs")
    has(text, "ShowBehindParent = true", "Soft shell is behind the actual sprite, not a body tint")
    has(text, "new ShaderMaterial { Shader = shader }", "Each owner has a separate shell material")
    energy = block(text, "private void UpdateEnergyShell()")
    for fragment in ("GetFrameTexture(_sprite.Animation, _sprite.Frame)", "selected.Region",
                     "drawRect.Grow(28f)", 'SetShaderParameter("flipped"',
                     'SetShaderParameter("strength", _energyStrength)'):
        has(energy, fragment, "Current-frame, padded, owner-local energy shell")
    require("GetImage" not in text, "No per-frame GPU readback")
    has(play, "_energyStrength = 0.65f * u * u * (3f - 2f * u);", "Charge builds a continuous shell")
    has(play, "_energyStrength = 1f;", "Rush reinforces the shell")
    has(block(text, "public async Task Recover()"), "_energyStrength = 0.95f * (1f - u);",
        "Recovery fades the independent shell, without changing the baked pose")
    has(block(text, "private void End()"), "_energy.QueueFree();", "Owner exits free their sprite-child shell")
    for forbidden in ("TIME", "Engine.TimeScale", "Random", "Rng", "ownerNode.GlobalPosition ="):
        require(forbidden not in text, f"Visual clock must not use {forbidden}")


def subtitle_contract(text: str) -> None:
    attach = block(text, "internal static void Attach(")
    ordered(attach, ["if (subtitle.HasNode(FollowerName)) return;", "Vector2 baseScale = subtitle.Scale;",
                     "subtitle.Scale = baseScale * (enlarge ? 1.3f * 1.5f : 1f);"],
            "Idempotent Spark-only scaling: original 1.3 times another 50 percent, not cumulative")
    has(attach, "subtitle.AddChild(new NShinGetterSparkSubtitleFollower", "Follower lifetime belongs to this bubble")
    has(attach, "ProcessPriority = 100", "Follow after the sequence updates its frame and transform")
    ordered(attach, ["subtitle.Hide();", "subtitle.AddChild("], "Do not flash at the stationary anchor on entry")
    process = block(text, "public override void _Process(")
    ordered(process, ["_owner.IsDead || CombatManager.Instance.IsOverOrEnding", "_subtitle.Hide();",
                      "QueueFree();", "bool paused = CombatManager.Instance.IsPaused;",
                      "SuspendNativeSpeech();", "if (_pauseLayoutReady) return;", "RestoreNativePause();",
                      "NShinGetterShiningSparkSequence.GetFrameLocalRect(_sprite)",
                      "ShinGetterSubtitleLayout.TryPlace(bubble.Size, body, viewport, BodyGap, out Rect2 placed)",
                      "TryCompactLayout(body, viewport)",
                      "Vector2 shift = placed.Position - bubble.Position;",
                      "parent.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition",
                      "_subtitle.Show();"],
            "Use a fully validated viewport/body placement, with a fixed-font compact fallback")
    has(process, "NShinGetterShiningSparkSequence.IsActuallyVisible(_sprite)",
        "Follower visibility includes SelfModulate and ancestor alpha")
    has(process, "_subtitle.Hide();", "No clipped/overlapping success when no readable slot exists")
    has(text, "_nativeText.AutoSizeEnabled = false;", "Do not silently shrink the requested font")
    has(text, "RenderingServer.FramePreDraw += OnFramePreDraw;",
        "Correct the native post-Process Tween before rendering")
    has(block(text, "public override void _ExitTree()"), "RenderingServer.FramePreDraw -= OnFramePreDraw;",
        "Release the rendering callback with this bubble's follower")
    render = block(text, "private void OnFramePreDraw()")
    ordered(render, ["IsInsideTree()", "IsQueuedForDeletion()", "GodotObject.IsInstanceValid(_subtitle)",
                     "_subtitle.IsQueuedForDeletion()", "return;", "_Process(0d);"],
            "The final render pass reuses pause/visibility/body/layout guards and skips dying bubbles")
    fallback = block(text, "private bool TryCompactLayout(")
    for fragment in ("ShinGetterSubtitleLayout.FreeBands(body, viewport, BodyGap)",
                     "TryWrapText(plainText, font, fontSize, width, out string wrapped)",
                     "_compactText.GetMinimumSize()", "ShinGetterSubtitleLayout.TryPlace(localSize * scale",
                     "canvas.AffineInverse() * desired", "_nativeText.MaxFontSize"):
        has(fallback, fragment, "Measure complete fixed-font text and validate its final viewport rectangle")
    dimensions = block(text, "private Rect2 GetBubbleViewportRect()")
    for fragment in ('"%Bubble", "%Shadow", "%Text"', "sprite.GetRect()", "control.Size", "Merge(rect)"):
        has(dimensions, fragment, "Measure real speech contents, not the zero-size root Control")
    for forbidden in ("TalkPosition", "Scale", "CreateTween"):
        require(forbidden not in process, "No stationary anchor or repeated scaling")


def icon_contract(text: str) -> None:
    play = block(text, "internal static void Play(")
    has(play, "Texture = ModelDb.Power<SGP_HotBlood>().BigIcon", "Use the real Hot Blood icon without creating a buff")
    for forbidden in ("HasPower", "GetPower", "PowerCmd", "SetAmount", "CreateMutable"):
        require(forbidden not in text, f"Flash must be buff-independent visual only: {forbidden}")
    process = block(text, "public override void _Process(")
    ordered(process, ["if (CombatManager.Instance.IsPaused) return;", "_age += (float)delta;",
                      "if (u >= 1f) QueueFree();"], "Icon flash is pause-aware and finite")


def full_attack_contract(text: str) -> None:
    attack = block(text, "public static async Task PlayCompleteCreatureAttack(")
    ordered(attack, ["NonInteractiveMode.IsActive", "FastModeType.Instant", "await onImpact();",
                     "if (!await WaitForPreviousSpecialAttack(creature, sprite))",
                     "await onImpact();", "QueueNextActionSpeed(sprite, 1f)",
                     'TryStartFreshAttack(sprite, form.EnsureLoaded)',
                     "frames.GetFrameDuration(animation, index)", "frameUnits * 0.5d, duration);",
                     "await onImpact();", "frameUnits, duration);"],
            "Full normal-speed animation: impact in the middle, recovery before returning")
    wait = block(text, "private static async Task WaitForAttackPhase(")
    for fragment in ("budget = duration + 0.25f", "sprite.Animation == animation", "sprite.IsPlaying()",
                     "!creature.IsDead", "!CombatManager.Instance.IsOverOrEnding", "FastModeType.Instant",
                     "if (CombatManager.Instance.IsPaused) continue;", "sprite.FrameProgress",
                     "if (completed >= phaseUnits) return;", "budget -= 0.02f"):
        has(wait, fragment, f"Finite actual-progress attack wait: {fragment}")
    require("AnimationFinished" not in wait and "ToSignal" not in wait,
            "No unconditional wait on a signal that interruption may never emit")


def previous_special_contract(text: str) -> None:
    wait = block(text, "private static async Task<bool> WaitForPreviousSpecialAttack(")
    ordered(wait, ["float budget = 6f;", "while (IsAttackVisualAvailable(creature, sprite)",
                   "FastModeType.Instant", "if (!CombatManager.Instance.IsPaused)",
                   "if (!NShinGetterSpriteAnimationStateMachine.IsKeepingAttack(sprite)) return true;",
                   "if (budget <= 0f) return false;", "await Cmd.Wait(0.02f, ignoreCombatEnd: true);",
                   "if (!CombatManager.Instance.IsPaused) budget -= 0.02f;", "return false;"],
            "Old protected action must finish before a fresh request, with a finite pause-aware budget")
    for forbidden in ("FrameProgress", "sprite.Frame", "SetFrame", "PlayIdle", "Stop(", "ToSignal"):
        require(forbidden not in wait, "An old action past its midpoint cannot count as the new attack")
    has(text, "GodotObject.IsInstanceValid(sprite) && sprite.IsInsideTree() && sprite.IsVisibleInTree()"
        " && sprite.Modulate.A > 0.01f && !creature.IsDead && !CombatManager.Instance.IsOverOrEnding",
        "Waiting interruption must exit on death/hidden/exit/combat ending")


def fresh_attack_contract(text: str) -> None:
    has(text, 'ShouldKeepActiveSpecialAnimation(sprite, States.GetOrCreateValue(sprite), "Attack");',
        "Old-action check must reuse the actual state-machine suppression predicate")
    start = block(text, "internal static bool TryStartFreshAttack(")
    ordered(start, ["if (IsKeepingAttack(sprite)", '!TryPlay(sprite, "Attack", ensureLoaded)',
                    "sprite.Animation != NShinGetterSpriteSequence.AttackAnimationName",
                    "!sprite.IsPlaying()", "return false;", "sprite.SetFrameAndProgress(0, 0f);",
                    "return sprite.Frame == 0 && sprite.FrameProgress == 0f;"],
            "A true TryPlay is insufficient: require Attack and reset both frame and progress")


def main() -> None:
    clock = read(ROOT / "src/Nodes/Combat/NShinGetterShiningSparkSequence.cs")
    subtitle = read(ROOT / "src/Nodes/Vfx/NShinGetterSparkSubtitleFollower.cs")
    icon = read(ROOT / "src/Nodes/Vfx/NShinGetterHotBloodIconFlash.cs")
    attack = read(ROOT / "src/Nodes/Combat/NShinGetterStaticVisuals.cs")
    machine = read(ROOT / "src/Nodes/Combat/NShinGetterSpriteAnimationStateMachine.cs")
    for validator, text in ((clock_contract, clock), (subtitle_contract, subtitle),
                            (icon_contract, icon), (full_attack_contract, attack),
                            (previous_special_contract, attack), (fresh_attack_contract, machine)):
        validator(text)
    voice = read(ROOT / "src/Audio/ShinGetterVoiceService.cs")
    has(voice, 'if (localizationKey is "SHIN_GETTER.voice.shining" or "SHIN_GETTER.voice.spark") '
        'NShinGetterSparkSubtitleFollower.Attach(subtitle, player.Creature, '
        'enlarge: localizationKey == "SHIN_GETTER.voice.spark");', "Both lines follow; only Spark is enlarged")
    base = read(ROOT / "src/Models/Cards/ShinGetterCardBase.cs")
    timing = base.split("AttackTimingHandledByVfxCards =", 1)[1].split("};", 1)[0]
    require('"SGC_ShiftStrike"' in timing, "Shift owns its attack timing, without a separate ordinary delay")
    cases = [
        (clock_contract, clock, "u * u * u", "1f - (1f - u) * (1f - u)"),
        (clock_contract, clock, "_origin + _recoil * retreat * retreat", "_origin"),
        (clock_contract, clock, "if (CombatManager.Instance.IsPaused) return;", ""),
        (clock_contract, clock, "tail.GlobalTransform = _sprite.GlobalTransform;", ""),
        (subtitle_contract, subtitle, "1.3f * 1.5f", "1.3f"),
        (subtitle_contract, subtitle, "parent.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition", "viewportPosition"),
        (icon_contract, icon, "ModelDb.Power<SGP_HotBlood>().BigIcon", "null"),
        (icon_contract, icon, "var container = owner.GetVfxContainer();",
         "if (!owner.HasPower<SGP_HotBlood>()) return; var container = owner.GetVfxContainer();"),
        (full_attack_contract, attack, "frameUnits, duration);", "frameUnits * 0.5d, duration);"),
        (full_attack_contract, attack, "if (completed >= phaseUnits) return;\n            budget -= 0.02f;",
         "if (completed >= phaseUnits) return;"),
        (full_attack_contract, attack, "if (CombatManager.Instance.IsPaused) continue;", ""),
        (full_attack_contract, attack, "if (!await WaitForPreviousSpecialAttack(creature, sprite))",
         "if (false)"),
        (previous_special_contract, attack, "IsKeepingAttack(sprite)) return true;", "IsKeepingAttack(sprite)) return false;"),
        (previous_special_contract, attack, "if (budget <= 0f) return false;", ""),
        (previous_special_contract, attack, "!creature.IsDead", "true"),
        (previous_special_contract, attack, "sprite.IsInsideTree()", "true"),
        (fresh_attack_contract, machine, "|| sprite.Animation != NShinGetterSpriteSequence.AttackAnimationName", ""),
        (fresh_attack_contract, machine, "sprite.SetFrameAndProgress(0, 0f);", ""),
        (subtitle_contract, subtitle, "if (subtitle.HasNode(FollowerName)) return;", ""),
        (subtitle_contract, subtitle, "NShinGetterShiningSparkSequence.GetFrameLocalRect(_sprite)", "new Rect2()"),
        (subtitle_contract, subtitle, "ShinGetterSubtitleLayout.TryPlace(bubble.Size, body, viewport, BodyGap, out Rect2 placed)", "false"),
        (subtitle_contract, subtitle, "ProcessPriority = 100", "ProcessPriority = -100"),
        (subtitle_contract, subtitle, "RenderingServer.FramePreDraw += OnFramePreDraw;", ""),
        (subtitle_contract, subtitle, "RenderingServer.FramePreDraw -= OnFramePreDraw;", ""),
        (subtitle_contract, subtitle, "_subtitle.IsQueuedForDeletion()", "false"),
        (subtitle_contract, subtitle, "_Process(0d);", ""),
        (clock_contract, clock, "ShowBehindParent = true", "ShowBehindParent = false"),
        (clock_contract, clock, "drawRect.Grow(28f)", "drawRect"),
        (clock_contract, clock, "_energyStrength = 0.95f * (1f - u);", "_energyStrength = 1f;"),
        (clock_contract, clock, "_energy.QueueFree();", ""),
    ]
    for validator, text, old, new in cases:
        require(old in text, "Negative mutation must target actual source")
        try:
            validator(text.replace(old, new, 1))
        except AssertionError:
            continue
        raise AssertionError(f"Negative source variant unexpectedly passed: {old}")
    print(f"B1.3.0 feedback source gate passed ({len(cases)} negative variants rejected)")
    print("Static contracts only; visual quality, subtitle sizing and real combat NOT executed.")


if __name__ == "__main__":
    main()
