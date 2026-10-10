#!/usr/bin/env python3
"""Source and pause/lifetime decision model; no C#/Godot or external fixture modifications."""

from pathlib import Path

from validate_b130_core import block, has, ordered, read, require
from validate_b130_subtitle_layout import fits, place

ROOT = Path(__file__).resolve().parents[1]


def source_contract(source):
    ready = block(source, "public override void _Ready()")
    ordered(ready, ["_subtitle.Ready += CaptureEntryTween;", "CaptureEntryTween();",
                    "RenderingServer.FramePreDraw += OnFramePreDraw;"], "Capture a deferred root's native entry only")
    has(block(source, "private void CaptureEntryTween()"),
        "_entryTween ??= NativeTweenField?.GetValue(_subtitle) as Tween;", "Never recapture a replacement AnimOut tween")
    process = block(source, "public override void _Process(")
    ordered(process, ["RestoreNativePause();", "_subtitle.Hide();", "QueueFree();", "FreezeFontSize();",
                      "bool paused = CombatManager.Instance.IsPaused;", "SuspendNativeSpeech();",
                      "if (_entryTween == null)", "HideUnplaceableSubtitle();", "if (_pauseLayoutReady) return;",
                      "RestoreNativePause();", "_pauseLayoutReady = false;", "ShinGetterSubtitleLayout.TryPlace"],
            "Freeze native mutations before the paused early return; correct the transition once")
    require(process.count("_pauseLayoutReady = paused;") == 2, "Native and compact placement both finish the transition")
    suspend = block(source, "private void SuspendNativeSpeech()")
    for part in ("_nativeWasProcessing = _subtitle.IsProcessing();", "_subtitle.SetProcess(false);",
                 "_textProcessMode = _nativeText.ProcessMode;", "_nativeText.ProcessMode = ProcessModeEnum.Disabled;",
                 "_entryTween!.IsValid()", "ReferenceEquals(NativeTweenField?.GetValue(_subtitle), _entryTween)",
                 "_entryTween.IsRunning()", "_entryTween.Pause();", "_entryTweenPaused = true;"):
        has(suspend, part, "Scope suspension to this root, label and original entry tween")
    restore = block(source, "private void RestoreNativePause()")
    for part in ("!_subtitle.IsQueuedForDeletion()", "_subtitle.SetProcess(_nativeWasProcessing)",
                 "_nativeText!.ProcessMode = _textProcessMode", "_entryTweenPaused",
                 "ReferenceEquals(NativeTweenField?.GetValue(_subtitle), _entryTween)", "_entryTween.Play();",
                 "_nativeProcessingSuspended = false;", "_textProcessingSuspended = false;", "_entryTweenPaused = false;"):
        has(restore, part, "Restore previous flags and resume only the entry tween paused by this follower")
    exit_tree = block(source, "public override void _ExitTree()")
    for part in ("RenderingServer.FramePreDraw -= OnFramePreDraw", "_subtitle.Ready -= CaptureEntryTween",
                 "RestoreNativePause();"):
        has(exit_tree, part, "Detachment releases callbacks and any owned suspension")
    has(source, 'AccessTools.Field(typeof(NSpeechBubbleVfx), "_tween")', "Read the inspected native tween field only")
    require("BodyGap = 24f;" in source and "1.3f * 1.5f" in source, "Do not weaken layout gap or scale")
    for forbidden in (".Kill()", ".Stop()", "CreateTween", "Engine.TimeScale", "SecondsToDisplay ="):
        require(forbidden not in source, "Do not replace the native lifetime or global time")


class TweenModel:
    def __init__(self, running=True):
        self.running = running
        self.valid = True
        self.elapsed = 0.0

    def tick(self, delta):
        if self.running and self.valid:
            self.elapsed += delta


class PauseModel:
    """Mirror of captured-entry identity, owned suspension, one-time layout and resume decisions."""
    def __init__(self, entry_running=True, processing=True, text_mode="inherit"):
        self.entry = self.current = TweenModel(entry_running)
        self.processing = processing
        self.text_mode = text_mode
        self.old_processing = None
        self.old_text_mode = None
        self.entry_paused = False
        self.layout_ready = False
        self.bob_time = 0.0
        self.text_time = 0.0
        self.alive = True
        self.queued = False
        self.rect = None

    def restore(self):
        if self.alive and not self.queued:
            if self.old_processing is not None:
                self.processing = self.old_processing
            if self.old_text_mode is not None:
                self.text_mode = self.old_text_mode
            if self.entry_paused and self.entry.valid and self.entry is self.current:
                self.entry.running = True
        self.old_processing = self.old_text_mode = None
        self.entry_paused = False

    def follower(self, paused, size, body, view):
        if paused:
            if self.old_processing is None:
                self.old_processing = self.processing
                self.processing = False
            if self.old_text_mode is None:
                self.old_text_mode = self.text_mode
                self.text_mode = "disabled"
            if self.entry.valid and self.entry is self.current and self.entry.running:
                self.entry.running = False
                self.entry_paused = True
            if self.layout_ready:
                return
        else:
            self.restore()
            self.layout_ready = False
        self.rect = place(size, body, view)
        self.layout_ready = paused

    def frame(self, paused, size, body, view):
        # Real order: native node, follower node, native Tween, final pre-draw follower.
        if self.processing:
            self.bob_time += 1 / 60
        if self.text_mode != "disabled":
            self.text_time += 1 / 60
        self.follower(paused, size, body, view)
        self.current.tick(1 / 60)
        self.follower(paused, size, body, view)


def model_tests():
    view = (16, 16, 1888, 1048)
    # Captured xywh AABBs from native3; use the unchanged 24-unit production predicate.
    samples = [
        ((271.03445, 105.616684, 327.9559, 220.88353), (278.2954, 349.68127, 309.43317, 397.29688)),
        ((590.13403, 311.32605, 638.24915, 429.87097), (278.39758, 328.60654, 319.36554, 385.8364)),
    ]
    cases = 0
    for raw_rect, body in samples:
        require(not fits(raw_rect, body, view), "The unchanged gap assertion detects the real old failure")
        model = PauseModel()
        model.entry.elapsed, model.bob_time, model.text_time = 0.19, 0.31, 0.11
        model.frame(True, raw_rect[2:], body, view)
        snapshot = (model.rect, model.entry.elapsed, model.bob_time, model.text_time)
        require(model.rect is not None and fits(model.rect, body, view), "Pause-entry correction repairs the captured geometry")
        for _ in range(120):
            model.frame(True, raw_rect[2:], body, view)
            require((model.rect, model.entry.elapsed, model.bob_time, model.text_time) == snapshot,
                    "Two seconds of paused frames keep placement/entry tween/Contents/text time frozen")
            cases += 1
        model.frame(False, raw_rect[2:], body, view)
        model.frame(False, raw_rect[2:], body, view)
        require(model.entry.elapsed > snapshot[1] and model.bob_time > snapshot[2] and model.text_time > snapshot[3],
                "Resume continues the previous clocks instead of restarting or skipping their elapsed state")
    model = PauseModel()
    model.frame(True, samples[1][0][2:], samples[1][1], view)
    model.entry.valid = False  # Native AnimOut kills the old entry and creates a fade-only replacement.
    outro = model.current = TweenModel()
    for _ in range(30):
        model.frame(True, samples[1][0][2:], samples[1][1], view)
    require(outro.elapsed >= 0.4 and not model.processing, "Outro lifetime can finish during pause while geometry stays frozen")
    model.restore()
    require(not model.entry.running and outro.running, "Restore never resurrects a killed entry or alters the replacement outro")
    for prior_processing, prior_mode, entry_running in ((False, "disabled", False), (True, "always", True)):
        model = PauseModel(entry_running, prior_processing, prior_mode)
        model.frame(True, (300, 200), samples[1][1], view)
        model.restore()  # Follower detachment/hidden/form/death/end uses the same restoration.
        require(model.processing == prior_processing and model.text_mode == prior_mode
                and model.entry.running == entry_running, "Restore only the flags/tween this follower changed")
    model = PauseModel()
    model.frame(True, (300, 200), samples[1][1], view)
    model.queued = True
    model.restore()
    require(not model.entry.running, "Queued-for-deletion roots are not resumed")
    return cases


def main():
    source = read(ROOT / "src/Nodes/Vfx/NShinGetterSparkSubtitleFollower.cs")
    source_contract(source)
    cases = model_tests()
    rejected = 0
    for old, new in (("_subtitle.SetProcess(false);", ""),
                     ("_nativeText.ProcessMode = ProcessModeEnum.Disabled;", ""),
                     ("_entryTween.Pause();", ""),
                     ("ReferenceEquals(NativeTweenField?.GetValue(_subtitle), _entryTween)", "true"),
                     ("if (_pauseLayoutReady) return;", "if (paused) return;"),
                     ("_entryTween ??=", "_entryTween ="),
                     ("_subtitle.SetProcess(_nativeWasProcessing)", "_subtitle.SetProcess(true)"),
                     ("_entryTween.Play();", ""),
                     ("_subtitle.Ready -= CaptureEntryTween", ""),
                     ("!_subtitle.IsQueuedForDeletion()", "true")):
        require(old in source, "Mutation must target actual pause code")
        try:
            source_contract(source.replace(old, new, 1))
        except AssertionError:
            rejected += 1
        else:
            raise AssertionError("Bad pause source passed: " + old)
    print(f"Subtitle pause gate passed: {cases} frozen-frame model checks, captured charge/rush AABBs, "
          f"resume/outro/previous-flags/deletion cases; {rejected} negative source variants rejected.")
    print("Source/Python model only. Native Tween, custom text FX, real pause rendering and teardown NOT executed.")


if __name__ == "__main__":
    main()
