#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace ShinGetterMod.Events;

// A lifetime for one real owned native option invocation. History completion is
// signaled by the native history method's postfix, never by a delay or a guessed
// collection-append timestamp. Selection scopes flow across native async calls.
internal sealed class ShinGetterEventInteractionCheckpoint : IDisposable
{
    private static readonly ConditionalWeakTable<EventModel, ShinGetterEventInteractionCheckpoint> Sessions = new();
    private static readonly ConditionalWeakTable<EventOption, ShinGetterEventInteractionCheckpoint> Options = new();
    private static readonly AsyncLocal<ShinGetterEventInteractionCheckpoint?> Selection = new();
    private readonly EventModel model;
    private readonly EventSynchronizer source;
    private readonly Player player;
    private readonly EventOption option;
    private readonly Func<bool> isCurrent;
    private readonly Func<Task> save;
    private readonly Action<Exception> report;
    private readonly TaskCompletionSource historyReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ExceptionDispatchInfo? presentationFailure;
    private int active = 1;

    internal ShinGetterEventInteractionCheckpoint(EventModel model, EventSynchronizer source, Player player,
        EventOption option, Func<bool> isCurrent, Func<Task> save, Action<Exception> report)
    {
        this.model = model; this.source = source; this.player = player; this.option = option;
        this.isCurrent = isCurrent; this.save = save; this.report = report;
        Sessions.Add(model, this);
        try { Options.Add(option, this); }
        catch { Sessions.Remove(model); throw; }
    }

    internal Task HistoryReady => historyReady.Task;
    internal bool IsCurrent => Volatile.Read(ref active) != 0 && isCurrent();
    internal static ShinGetterEventInteractionCheckpoint? Find(EventModel model)
        => Sessions.TryGetValue(model, out var session) && Volatile.Read(ref session.active) != 0 ? session : null;

    internal static void HistoryCompleted(EventModel model, EventSynchronizer source, Player player, EventOption option)
    {
        var session = Find(model);
        if (session != null && ReferenceEquals(session.source, source) && ReferenceEquals(session.player, player)
            && ReferenceEquals(session.option, option)) session.historyReady.TrySetResult();
    }

    internal static void NotifyHistoryCompleted(EventSynchronizer source, Player player, EventOption option)
    {
        // Ordinary native options require no event lookup or other extra behavior.
        if (Options.TryGetValue(option, out var session)) HistoryCompleted(session.model, source, player, option);
    }

    internal void HistoryFailed(Exception error) => historyReady.TrySetException(error);

    internal IDisposable Select()
    {
        var previous = Selection.Value;
        Selection.Value = this;
        return new SelectionScope(this, previous);
    }

    private sealed class SelectionScope : IDisposable
    {
        private readonly ShinGetterEventInteractionCheckpoint session;
        private readonly ShinGetterEventInteractionCheckpoint? previous;
        private bool disposed;
        internal SelectionScope(ShinGetterEventInteractionCheckpoint session, ShinGetterEventInteractionCheckpoint? previous)
        { this.session = session; this.previous = previous; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!ReferenceEquals(Selection.Value, session)) throw new InvalidOperationException("Event selection scope disposed out of order.");
            Selection.Value = previous;
        }
    }

    // Native command call sites invoke this only after the screen has been pushed
    // and (for choose-a-card) the native MarkCardAsSeen loop has finished.
    internal static Task<T> BeforeSelectionResult<T>(Task<T> nativeSelection)
    {
        var session = Selection.Value;
        return session?.IsCurrent == true ? session.SavePresentation(nativeSelection) : nativeSelection;
    }

    private async Task<T> SavePresentation<T>(Task<T> nativeSelection)
    {
        try
        {
            await HistoryReady;
            if (IsCurrent) await save();
        }
        catch (Exception error)
        {
            presentationFailure ??= ExceptionDispatchInfo.Capture(error);
            // Keep the native command running so its own choice-ended/overlay
            // cleanup completes. The owner rejects the result before any cost.
            try { report(error); } catch { /* Diagnostics cannot replace the save failure. */ }
        }
        return await nativeSelection;
    }

    internal void ThrowPresentationFailure() => presentationFailure?.Throw();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref active, 0) == 0) return;
        historyReady.TrySetCanceled();
        if (Sessions.TryGetValue(model, out var current) && ReferenceEquals(current, this)) Sessions.Remove(model);
        if (Options.TryGetValue(option, out var bound) && ReferenceEquals(bound, this)) Options.Remove(option);
    }
}
