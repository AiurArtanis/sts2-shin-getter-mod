#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ShinGetterMod.Nodes.Combat;

// Engine-free timing data, also linked directly into the CPU regression executable.
internal sealed class TacticalRetreatFrames
{
    private readonly double[] _seconds;
    internal int Count => _seconds.Length;
    internal double Duration { get; }

    internal TacticalRetreatFrames(IEnumerable<double> frameUnits, double fps, double speed)
    {
        if (!double.IsFinite(fps) || fps <= 0 || !double.IsFinite(speed) || speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(fps));
        _seconds = frameUnits.Select(units => units / fps / speed).ToArray();
        if (_seconds.Length == 0 || _seconds.Any(seconds => !double.IsFinite(seconds) || seconds <= 0))
            throw new ArgumentException("Fusion requires positive, finite frame durations.", nameof(frameUnits));
        Duration = _seconds.Sum();
        if (!double.IsFinite(Duration))
            throw new ArgumentException("Fusion duration overflowed.", nameof(frameUnits));
    }

    internal int FrameAt(double elapsed, bool backwards)
    {
        elapsed = Math.Max(0, elapsed);
        for (int step = 0; step < Count; step++)
        {
            int frame = backwards ? Count - 1 - step : step;
            if (elapsed < _seconds[frame] || step == Count - 1)
                return frame;
            elapsed -= _seconds[frame];
        }
        throw new InvalidOperationException();
    }
}

internal enum TacticalRetreatPhase { Reverse, Hold, Forward }
internal readonly record struct TacticalRetreatSample(TacticalRetreatPhase Phase, int Frame, double Progress);

internal sealed class TacticalRetreatTimeline
{
    private readonly TacticalRetreatFrames? _previous;
    private readonly TacticalRetreatFrames _next;
    private readonly double _hold;
    internal double Duration { get; }

    internal TacticalRetreatTimeline(TacticalRetreatFrames? previous, double hold, TacticalRetreatFrames next)
    {
        if (!double.IsFinite(hold) || hold < 0)
            throw new ArgumentOutOfRangeException(nameof(hold));
        _previous = previous;
        _next = next;
        _hold = previous == null ? 0 : hold;
        Duration = (previous?.Duration ?? 0) + _hold + next.Duration;
        if (!double.IsFinite(Duration))
            throw new ArgumentOutOfRangeException(nameof(hold));
    }

    internal TacticalRetreatSample Sample(double elapsed)
    {
        double progress = Math.Clamp(elapsed / Duration, 0, 1);
        if (_previous != null)
        {
            if (elapsed < _previous.Duration)
                return new(TacticalRetreatPhase.Reverse, _previous.FrameAt(elapsed, backwards: true), progress);
            elapsed -= _previous.Duration;
            if (elapsed < _hold)
                return new(TacticalRetreatPhase.Hold, 0, progress);
            elapsed -= _hold;
        }
        return new(TacticalRetreatPhase.Forward, _next.FrameAt(elapsed, backwards: false), progress);
    }
}

internal sealed class TacticalRetreatSync : IDisposable
{
    private static readonly AsyncLocal<TacticalRetreatSync?> Active = new();
    private readonly TacticalRetreatSync? _parent;
    private readonly object _owner;
    private readonly Action<double> _advance;
    private readonly Func<bool> _shouldAbortAction;
    private bool _disposed;
    internal CancellationToken CancellationToken { get; }
    internal bool ShouldAbortAction => _shouldAbortAction();

    internal TacticalRetreatSync(object owner, Action<double> advance, CancellationToken cancellationToken,
        Func<bool>? shouldAbortAction = null)
    {
        _owner = owner;
        _advance = advance;
        _shouldAbortAction = shouldAbortAction ?? (() => false);
        CancellationToken = cancellationToken;
        _parent = Active.Value;
        Active.Value = this;
    }

    internal static TacticalRetreatSync? For(object owner)
    {
        for (TacticalRetreatSync? scope = Active.Value; scope != null; scope = scope._parent)
            if (!scope._disposed && ReferenceEquals(scope._owner, owner))
                return scope;
        return null;
    }

    internal void Advance(double progress)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CancellationToken.ThrowIfCancellationRequested();
        _advance(Math.Clamp(progress, 0, 1));
    }

    public void Dispose()
    {
        _disposed = true;
        if (ReferenceEquals(Active.Value, this))
            Active.Value = _parent;
    }
}
