#nullable enable
using Godot;

namespace ShinGetterMod.Nodes.Events;

/// <summary>Cancel stale full-position targets before correcting native layout.</summary>
internal static class ShinGetterContentTweenGuard
{
    internal static void Stop(ref Tween? tween)
    {
        if (tween != null && GodotObject.IsInstanceValid(tween)) tween.Kill();
        tween = null; // Native animation must not CustomStep the old Vector2 target.
    }
}
