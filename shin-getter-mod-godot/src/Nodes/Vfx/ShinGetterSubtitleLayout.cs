#nullable enable
using Godot;

namespace ShinGetterMod.Nodes.Vfx;

internal static class ShinGetterSubtitleLayout
{
    internal static bool IsUsable(Rect2 rect) => rect.Position.IsFinite() && rect.Size.IsFinite()
        && rect.Size.X > 0f && rect.Size.Y > 0f;

    internal static bool Fits(Rect2 rect, Rect2 body, Rect2 viewport, float gap)
    {
        if (!IsUsable(rect) || !IsUsable(body) || !IsUsable(viewport)) return false;
        return rect.Position.X >= viewport.Position.X && rect.Position.Y >= viewport.Position.Y
            && rect.End.X <= viewport.End.X && rect.End.Y <= viewport.End.Y
            && (rect.End.Y <= body.Position.Y - gap || rect.Position.X >= body.End.X + gap
                || rect.End.X <= body.Position.X - gap || rect.Position.Y >= body.End.Y + gap);
    }

    internal static bool TryPlace(Vector2 size, Rect2 body, Rect2 viewport, float gap, out Rect2 result)
    {
        result = default;
        if (!size.IsFinite() || size.X <= 0f || size.Y <= 0f
            || !IsUsable(body) || !IsUsable(viewport)
            || size.X > viewport.Size.X || size.Y > viewport.Size.Y) return false;
        float x = Mathf.Clamp(body.GetCenter().X - size.X * 0.5f,
            viewport.Position.X, viewport.End.X - size.X);
        float y = Mathf.Clamp(body.GetCenter().Y - size.Y * 0.5f,
            viewport.Position.Y, viewport.End.Y - size.Y);
        // Clamp only the axis perpendicular to the separation, then validate the whole rectangle.
        foreach (Vector2 position in new[]
        {
            new Vector2(x, body.Position.Y - gap - size.Y),
            new Vector2(body.End.X + gap, y),
            new Vector2(body.Position.X - gap - size.X, y),
            new Vector2(x, body.End.Y + gap),
        })
        {
            var candidate = new Rect2(position, size);
            if (!Fits(candidate, body, viewport, gap)) continue;
            result = candidate;
            return true;
        }
        return false;
    }

    internal static Rect2[] FreeBands(Rect2 body, Rect2 viewport, float gap)
    {
        if (!IsUsable(body) || !IsUsable(viewport)) return System.Array.Empty<Rect2>();
        float top = Mathf.Clamp(body.Position.Y - gap, viewport.Position.Y, viewport.End.Y);
        float right = Mathf.Clamp(body.End.X + gap, viewport.Position.X, viewport.End.X);
        float left = Mathf.Clamp(body.Position.X - gap, viewport.Position.X, viewport.End.X);
        float bottom = Mathf.Clamp(body.End.Y + gap, viewport.Position.Y, viewport.End.Y);
        return new[]
        {
            new Rect2(viewport.Position, new Vector2(viewport.Size.X, top - viewport.Position.Y)),
            new Rect2(new Vector2(right, viewport.Position.Y), new Vector2(viewport.End.X - right, viewport.Size.Y)),
            new Rect2(viewport.Position, new Vector2(left - viewport.Position.X, viewport.Size.Y)),
            new Rect2(new Vector2(viewport.Position.X, bottom), new Vector2(viewport.Size.X, viewport.End.Y - bottom)),
        };
    }
}
