#!/usr/bin/env python3
"""Offline mirror of the C# placement decision; no C# execution or font/rendering claim."""

import math
from pathlib import Path

from validate_b130_core import block, has, ordered, read, require

ROOT = Path(__file__).resolve().parents[1]
VIEW = (16.0, 16.0, 1248.0, 688.0)
BODY = (450.0, 140.0, 400.0, 420.0)
GAP = 24.0


def usable(rect):
    return all(math.isfinite(v) for v in rect) and rect[2] > 0 and rect[3] > 0


def fits(rect, body, view, gap=GAP):
    if not all(usable(r) for r in (rect, body, view)):
        return False
    x, y, w, h = rect
    bx, by, bw, bh = body
    vx, vy, vw, vh = view
    return (x >= vx and y >= vy and x + w <= vx + vw and y + h <= vy + vh
            and (y + h <= by - gap or x >= bx + bw + gap
                 or x + w <= bx - gap or y >= by + bh + gap))


def clamp(value, low, high):
    return max(low, min(high, value))


def place(size, body, view, gap=GAP):
    """The same size guards, four candidate equations, order and Fits predicate as C#."""
    w, h = size
    if (not all(math.isfinite(v) and v > 0 for v in size)
            or not usable(body) or not usable(view) or w > view[2] or h > view[3]):
        return None
    bx, by, bw, bh = body
    vx, vy, vw, vh = view
    x = clamp(bx + bw / 2 - w / 2, vx, vx + vw - w)
    y = clamp(by + bh / 2 - h / 2, vy, vy + vh - h)
    for px, py in ((x, by - gap - h), (bx + bw + gap, y),
                   (bx - gap - w, y), (x, by + bh + gap)):
        candidate = (px, py, w, h)
        if fits(candidate, body, view, gap):
            return candidate
    return None


def bands(body, view, gap=GAP):
    if not usable(body) or not usable(view):
        return []
    bx, by, bw, bh = body
    vx, vy, vw, vh = view
    top = clamp(by - gap, vy, vy + vh)
    right = clamp(bx + bw + gap, vx, vx + vw)
    left = clamp(bx - gap, vx, vx + vw)
    bottom = clamp(by + bh + gap, vy, vy + vh)
    return [(vx, vy, vw, top - vy), (right, vy, vx + vw - right, vh),
            (vx, vy, left - vx, vh), (vx, bottom, vw, vy + vh - bottom)]


def compact(tokens, body, view, scale, glyph_width=24.0, line_height=32.0):
    """Model fixed-font wrapping only. Runtime measures shaped strings and Label.GetMinimumSize."""
    sx, sy = scale
    if not all(math.isfinite(v) and v >= 0.001 for v in scale):
        return None
    for band in bands(body, view):
        if not usable(band):
            continue
        width = band[2] / sx - 24.0
        if width < glyph_width:
            continue
        lengths = [0.0]
        for token in tokens:
            if token == "\n":
                lengths.append(0.0)
                continue
            if lengths[-1] > 0 and lengths[-1] + glyph_width > width:
                lengths.append(0.0)
            lengths[-1] += glyph_width
        size = ((max(lengths) + 24.0) * sx, (len(lengths) * line_height + 24.0) * sy)
        result = place(size, body, view)
        if result is not None:
            return result
    return None


def source_contract(source, follower):
    selector = block(source, "internal static bool TryPlace(")
    ordered(selector, ["size.X > viewport.Size.X || size.Y > viewport.Size.Y",
                       "body.Position.Y - gap - size.Y", "body.End.X + gap, y",
                       "body.Position.X - gap - size.X, y", "body.End.Y + gap",
                       "if (!Fits(candidate, body, viewport, gap)) continue;",
                       "result = candidate;", "return true;"],
            "Python decision mirror is tied to the four real production candidates and size guards")
    fit = block(source, "internal static bool Fits(")
    for fragment in ("rect.Position.X >= viewport.Position.X", "rect.Position.Y >= viewport.Position.Y",
                     "rect.End.X <= viewport.End.X", "rect.End.Y <= viewport.End.Y",
                     "rect.End.Y <= body.Position.Y - gap", "rect.Position.X >= body.End.X + gap",
                     "rect.End.X <= body.Position.X - gap", "rect.Position.Y >= body.End.Y + gap"):
        has(fit, fragment, "Every complete candidate must fit and clear the body")
    free = block(source, "internal static Rect2[] FreeBands(")
    for fragment in ("body.Position.Y - gap", "body.End.X + gap", "body.Position.X - gap", "body.End.Y + gap"):
        has(free, fragment, "Fallback measures text against all four complete free bands")
    wrap = block(follower, "private static bool TryWrapText(")
    for fragment in ("StringInfo.GetTextElementEnumerator", "font.GetStringSize(element",
                     "font.GetStringSize(line.ToString() + element", 'string.Join("\\n", lines)', "return false;"):
        has(wrap, fragment, "Wrap at grapheme boundaries without reducing the font or dropping content")
    fallback = block(follower, "private bool TryCompactLayout(")
    for fragment in ("_nativeText.MaxFontSize", "_compactText.GetMinimumSize()", "localSize * scale",
                     "canvas.AffineInverse() * desired", 'GetParsedText()', "scale.IsFinite()", "canvas.Determinant()"):
        has(fallback, fragment, "Actual measured text, finite Canvas basis, unchanged font and complete placement")
    require("_subtitle.Scale =" not in fallback and "fontSize *" not in fallback,
            "No fallback root/font shrinking")
    has(follower, "return result ?? default;", "Missing/zero content does not get a fictional bubble size")


def geometry_cases(selector=place):
    # Deterministic review reproduction: no native candidate can fit, not just the left one.
    require(selector((640, 280), BODY, VIEW) is None, "640x280 review counterexample must request compact fallback")
    cases = [
        ((640, 280), (450, 400, 400, 200), "top"),
        ((300, 200), (40, 20, 200, 600), "right"),
        ((300, 200), (1000, 20, 200, 600), "left"),
        ((600, 250), (400, 20, 400, 100), "bottom"),
    ]
    for size, body, direction in cases:
        result = selector(size, body, VIEW)
        require(result is not None and fits(result, body, VIEW), f"Complete {direction} candidate")
        x, y, w, h = result
        bx, by, bw, bh = body
        expected = {"top": y + h <= by - GAP, "right": x >= bx + bw + GAP,
                    "left": x + w <= bx - GAP, "bottom": y >= by + bh + GAP}
        require(expected[direction], f"Actual {direction} branch is exercised")
    for size in ((0, 20), (20, 0), (-1, 20), (1300, 20), (20, 800), (math.nan, 20), (20, math.inf)):
        require(selector(size, BODY, VIEW) is None, f"Invalid/oversized native size: {size}")
    for view in ((0, 0, 0, 0), (0, 0, -1, 720), (math.inf, 0, 1280, 720)):
        require(selector((100, 50), BODY, view) is None, "Invalid viewport is explicit failure")
    require(selector((100, 50), (0, 0, 0, 0), VIEW) is None, "Zero body is explicit failure")


def main():
    source = read(ROOT / "src/Nodes/Vfx/ShinGetterSubtitleLayout.cs")
    follower = read(ROOT / "src/Nodes/Vfx/NShinGetterSparkSubtitleFollower.cs")
    source_contract(source, follower)
    geometry_cases()
    fallback = compact(list("Spaaaaaark !"), BODY, VIEW, (1.95, 1.95))
    require(fallback is not None and fits(fallback, BODY, VIEW), "Review case has a readable fixed-1.95 compact result")
    for native in ((0, 0), (1400, 100), (100, 1000)):
        require(place(native, BODY, VIEW) is None and fallback is not None, "Zero/too-wide/too-high backgrounds all use text layout")
    require(compact(list("Spark"), VIEW, VIEW, (1.95, 1.95)) is None,
            "Body fills the viewport: no overlap/cropping is reported as readable success")
    require(compact(list("Spark"), BODY, VIEW, (0, 1.95)) is None, "Singular Canvas is not inverted")
    require(compact(list("X" * 1000), BODY, VIEW, (1.95, 1.95)) is None,
            "Uncontainably tall text is explicit failure, not silently clipped")
    require(compact(["X"], BODY, VIEW, (1.95, 1.95), glyph_width=1400) is None,
            "Uncontainably wide glyph is explicit failure, not silently shrunk")
    rejected = 0
    for old, new in (("rect.End.X <= viewport.End.X", "true"),
                     ("rect.End.Y <= viewport.End.Y", "true"),
                     ("if (!Fits(candidate, body, viewport, gap)) continue;", ""),
                     ("body.End.Y + gap", "body.End.Y")):
        try:
            source_contract(source.replace(old, new, 1), follower)
        except AssertionError:
            rejected += 1
        else:
            raise AssertionError("Invalid layout source passed: " + old)
    # The old roomier-side algorithm must actually fail the executable geometry suite.
    def old_roomier_side(size, body, view, gap=GAP):
        bx, by, bw, bh = body
        vx, vy, vw, vh = view
        w, h = size
        right, left = vx + vw - bx - bw - gap, bx - vx - gap
        return (bx + bw + gap if right >= left else bx - gap - w,
                clamp(by + bh / 2 - h / 2, vy, vy + vh - h), w, h)
    try:
        geometry_cases(old_roomier_side)
    except AssertionError:
        rejected += 1
    else:
        raise AssertionError("Old side fallback unexpectedly passed")
    print(f"Subtitle placement gate passed: all 4 native directions, review counterexample, "
          f"fixed-font wrap and invalid/oversized fallbacks; {rejected} negative variants rejected.")
    print("Mirrored Python geometry/source contracts only; C# binary, shaped fonts and Godot rendering NOT executed.")


if __name__ == "__main__":
    main()
