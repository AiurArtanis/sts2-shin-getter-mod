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
                      "UpdateTailHistory((float)delta);", "UpdateEnergy();", "QueueRedraw();"],
            "Pause freezes frame/position/energy and actual history ages")
    history = block(text, "private void UpdateTailHistory(")
    for fragment in ("_tailAges[i] += delta;", "if (!_rushing || _travelDistance < 20f) return;",
                     "if (_sampleTime < 0.04f) return;", "tail.GlobalTransform = _sprite.GlobalTransform;",
                     "GetFrameTexture(_sprite.Animation, _sprite.Frame)", "_tailAges[_tailCursor] = 0f;"):
        has(history, fragment, f"Bounded actual-frame motion snapshots: {fragment}")
    for forbidden in ("AddChild", "new Sprite2D", "tail.GlobalPosition -="):
        require(forbidden not in history, "History uses a fixed pool, not current-frame offset copies")
    draw = block(text, "public override void _Draw()")
    has(draw, "DrawPolyline(points", "Energy shell has independent outer geometry")
    has(draw, "Math.Min(_travelDistance * 0.18f, 100f)", "Short movement has a proportionally short tail")
    for forbidden in ("TIME", "Engine.TimeScale", "Random", "Rng", "ownerNode.GlobalPosition ="):
        require(forbidden not in text, f"Visual clock must not use {forbidden}")


def subtitle_contract(text: str) -> None:
    attach = block(text, "internal static void Attach(")
    has(attach, "subtitle.Scale *= 1.3f;", "Scale only the Spark bubble by 30 percent")
    has(attach, "subtitle.AddChild(new NShinGetterSparkSubtitleFollower", "Follower lifetime belongs to this bubble")
    has(attach, "subtitle.GetGlobalTransformWithCanvas().Origin - sprite.GetGlobalTransformWithCanvas().Origin",
        "Capture anchor relative to the moving sprite in viewport space")
    process = block(text, "public override void _Process(")
    ordered(process, ["_owner.IsDead || CombatManager.Instance.IsOverOrEnding", "_subtitle.Hide();",
                      "QueueFree();", "if (CombatManager.Instance.IsPaused) return;",
                      "_sprite.GetGlobalTransformWithCanvas().Origin + _viewportOffset",
                      "parent.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition"],
            "Follow the actual sprite across canvas transforms; stop on death/exit")
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
                     "QueueNextActionSpeed(sprite, 1f)", 'TryPlayVisibleActionAnimation(sprite, "Attack",',
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


def main() -> None:
    clock = read(ROOT / "src/Nodes/Combat/NShinGetterShiningSparkSequence.cs")
    subtitle = read(ROOT / "src/Nodes/Vfx/NShinGetterSparkSubtitleFollower.cs")
    icon = read(ROOT / "src/Nodes/Vfx/NShinGetterHotBloodIconFlash.cs")
    attack = read(ROOT / "src/Nodes/Combat/NShinGetterStaticVisuals.cs")
    for validator, text in ((clock_contract, clock), (subtitle_contract, subtitle),
                            (icon_contract, icon), (full_attack_contract, attack)):
        validator(text)
    voice = read(ROOT / "src/Audio/ShinGetterVoiceService.cs")
    has(voice, 'if (localizationKey == "SHIN_GETTER.voice.spark") '
        'NShinGetterSparkSubtitleFollower.Attach(subtitle, player.Creature);', "Only Spark opts into the follower")
    base = read(ROOT / "src/Models/Cards/ShinGetterCardBase.cs")
    timing = base.split("AttackTimingHandledByVfxCards =", 1)[1].split("};", 1)[0]
    require('"SGC_ShiftStrike"' in timing, "Shift owns its attack timing, without a separate ordinary delay")
    cases = [
        (clock_contract, clock, "u * u * u", "1f - (1f - u) * (1f - u)"),
        (clock_contract, clock, "_origin + _recoil * retreat * retreat", "_origin"),
        (clock_contract, clock, "if (CombatManager.Instance.IsPaused) return;", ""),
        (clock_contract, clock, "tail.GlobalTransform = _sprite.GlobalTransform;", ""),
        (subtitle_contract, subtitle, "subtitle.Scale *= 1.3f;", "subtitle.Scale *= 2f;"),
        (subtitle_contract, subtitle, "parent.GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition", "viewportPosition"),
        (icon_contract, icon, "ModelDb.Power<SGP_HotBlood>().BigIcon", "null"),
        (icon_contract, icon, "var container = owner.GetVfxContainer();",
         "if (!owner.HasPower<SGP_HotBlood>()) return; var container = owner.GetVfxContainer();"),
        (full_attack_contract, attack, "frameUnits, duration);", "frameUnits * 0.5d, duration);"),
        (full_attack_contract, attack, "budget -= 0.02f;", ""),
        (full_attack_contract, attack, "if (CombatManager.Instance.IsPaused) continue;", ""),
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
