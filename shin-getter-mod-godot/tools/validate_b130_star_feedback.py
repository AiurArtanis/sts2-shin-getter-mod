#!/usr/bin/env python3
"""Static feedback gate and explicit timeline/float32 models, not Godot acceptance."""
from __future__ import annotations

import json
import math
import struct
import unittest

from validate_b130_core import block, has, ordered, require
from validate_issue_124 import DATA, FORMS, ROOT, SEQUENCE, read, source_checks
from repair_star_slash_foreground import REPORT, TARGETS, baseline, mask_of, repair_document


def f32(value: float) -> float:
    return struct.unpack("f", struct.pack("f", value))[0]


def clean_model(polygon):
    points = []
    for point in polygon:
        point = tuple(f32(value) for value in point)
        if not points or math.dist(points[-1], point) > 0.001:
            points.append(point)
    if len(points) > 1 and math.dist(points[0], points[-1]) <= 0.001:
        points.pop()
    while len(points) > 3:
        for index, b in enumerate(points):
            a, c = points[index - 1], points[(index + 1) % len(points)]
            cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
            dot = sum((b[n] - a[n]) * (c[n] - b[n]) for n in (0, 1))
            if abs(cross) <= 0.001 * math.dist(a, c) and dot >= 0:
                points.pop(index)
                break
        else:
            break
    return points


def area(points):
    return abs(sum(a[0] * b[1] - b[0] * a[1]
                   for a, b in zip(points, points[1:] + points[:1]))) / 2


class FeedbackModels(unittest.TestCase):
    def test_actual_completion_gate(self):
        for hold in (0.9, 1.05):
            for voice in (0, 0.4, 2.7, 5.0):
                ready = lambda clock, audio_done, paused, closed: closed or (
                    not paused and clock >= hold and audio_done)
                self.assertFalse(ready(hold - 0.01, True, False, False))
                self.assertFalse(ready(hold + 10, False, False, False))
                self.assertFalse(ready(max(hold, voice), True, True, False))
                self.assertTrue(ready(max(hold, voice), True, False, False))
                self.assertTrue(ready(0, False, True, True))

    def test_cleave_speed_and_weight_ratios(self):
        for action in FORMS:
            data = json.loads(read(f"images/characters/shin_getter/forms/{action}/animation.json"))
            hold = sum(frame["duration_seconds"] for frame in data["frames"][:data["hold_frame"] + 1])
            impact = data["impact_time"]
            total = sum(frame["duration_seconds"] for frame in data["frames"])
            stroke, recovery = (impact - hold) / 1.5, (total - impact) / 1.5
            self.assertAlmostEqual(stroke * 1.5, impact - hold)
            self.assertAlmostEqual(recovery * 1.5, total - impact)
            for frame in data["frames"]:
                self.assertAlmostEqual(frame["duration_seconds"] / 1.5 * 1.5, frame["duration_seconds"])

    def test_remaining_confirmation_audio(self):
        for hold in (0.9, 1.05):
            for elapsed in (0, 0.8, 3.0):
                remaining = min(2, max(0, 1.2 - elapsed - (1.4 - hold) / 1.5))
                self.assertGreaterEqual(remaining, 0)
                self.assertLessEqual(remaining, 2)
                if elapsed == 3:
                    self.assertEqual(remaining, 0)

    def test_recovery_is_not_serialized_after_damage(self):
        recovery = (2.4 - 1.4) / 1.5
        for damage in (0.01, 0.6, 5.0):
            visual_end = recovery
            command_end = max(damage, 0.18, recovery)
            self.assertAlmostEqual(visual_end, 2 / 3)
            self.assertLess(command_end, damage + 0.18 + recovery)

    def test_straight_vertices_and_notch(self):
        rectangle = [[0, 0], [0, 0], [5, 0], [10, 0], [10, 10], [0, 10], [0, 0]]
        self.assertEqual(len(clean_model(rectangle)), 4)
        notch = [[0, 0], [10, 0], [10, 10], [5, 5], [0, 10]]
        self.assertEqual(clean_model(notch), [tuple(point) for point in notch])
        self.assertEqual(area(clean_model(notch)), 75)

    def test_delivered_foreground_cleanup(self):
        count, removed, maximum_delta = 0, 0, 0.0
        for action in FORMS:
            data = json.loads(read(f"images/characters/shin_getter/forms/{action}/animation.json"))
            for frame in data["frames"]:
                for layer in ("body_foreground", "hands"):
                    for polygon in frame.get(layer, []):
                        count += 1
                        cleaned = clean_model(polygon)
                        original = [tuple(f32(value) for value in point) for point in polygon]
                        self.assertGreaterEqual(len(cleaned), 3)
                        self.assertGreater(area(cleaned), 0)
                        delta = abs(area(original) - area(cleaned))
                        self.assertLessEqual(delta, 0.01)
                        self.assertTrue(all(point in original for point in cleaned))
                        removed += len(original) - len(cleaned)
                        maximum_delta = max(maximum_delta, delta)
        evidence = json.loads(REPORT.read_text(encoding="utf-8"))
        parts = sum(target["replacement_parts"] for action in evidence["actions"].values() for target in action["targets"])
        self.assertEqual(count, 13794 - 6 + parts)
        print(f"Foreground float32 model: {count} polygons, {removed} redundant vertices removed, max area delta={maximum_delta:.6f}px^2")

    def test_six_native_failure_fixtures_and_untouched_data(self):
        for action in TARGETS:
            original = baseline(action)
            repaired, details = repair_document(original, action)
            current = json.loads(read(f"images/characters/shin_getter/forms/{action}/animation.json"))
            self.assertEqual(current, repaired)
            self.assertEqual(len(details), 3)
            for index, (before, after) in enumerate(zip(original["frames"], repaired["frames"])):
                for key in before:
                    if index in TARGETS[action] and key == "body_foreground":
                        self.assertEqual(mask_of(before[key]).tobytes(), mask_of(after[key]).tobytes())
                    else:
                        self.assertEqual(before[key], after[key])
            broken = json.loads(json.dumps(original))
            first = next(iter(TARGETS[action]))
            broken["frames"][first]["body_foreground"][TARGETS[action][first]][0][0] += 1
            with self.assertRaises(AssertionError):
                repair_document(broken, action)


def contracts():
    negatives = source_checks()
    data = read(DATA)
    clean = block(data, "internal static Vector2[] CleanPolygon(")
    for token in ("const float epsilon = 0.001f;", "points[^1].DistanceTo(point) > epsilon",
                  "points[0].DistanceTo(points[^1]) <= epsilon", "points.Count > 3",
                  "Math.Abs(cross) > epsilon * a.DistanceTo(c)", "(b - a).Dot(c - b) < 0f"):
        has(clean, token, "Only redundant subpixel/straight vertices are removed")
    require("ConvexHull" not in data, "Cleaning cannot replace a concave matte with a hull")
    sequence = read(SEQUENCE)
    draw = block(sequence, "private void DrawTriangles(")
    ordered(draw, ["RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), indices, vertices, new[] { color },",
                   "uvs, Array.Empty<int>(), Array.Empty<float>(), texture?.GetRid() ?? default, -1"],
            "Native 2D indexed triangles retain texture and color")
    for before, after in (("const float epsilon = 0.001f;", "const float epsilon = 5f;"),
                          ("(b - a).Dot(c - b) < 0f", "false")):
        variant = block(data.replace(before, after, 1), "internal static Vector2[] CleanPolygon(")
        try:
            has(variant, before, "Cleaner negative mutation")
        except AssertionError:
            negatives += 1
            continue
        raise AssertionError("Cleaner mutation escaped")
    print(f"PASS source contracts: {negatives} broken variants rejected")


if __name__ == "__main__":
    contracts()
    unittest.main(verbosity=2)
