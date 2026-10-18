#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace ShinGetterMod.Nodes.Combat;

internal static class TacticalRetreatTween
{
    internal static async Task PlayClock(Node owner, Tween tween, float duration,
        Action<float> advance, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (NonInteractiveMode.IsActive || SaveManager.Instance.PrefsSave.FastMode == FastModeType.Instant)
            {
                advance(1f);
                return;
            }
            advance(0);
            tween.TweenMethod(Callable.From<float>(progress =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (CombatManager.Instance.IsEnding)
                        throw new OperationCanceledException("Combat ended during tactical retreat.");
                    advance(progress);
                }
                catch (OperationCanceledException) { completion.TrySetCanceled(); }
                catch (Exception exception) { completion.TrySetException(exception); }
            }), 0f, 1f, duration).SetTrans(Tween.TransitionType.Linear);
            await Wait(owner, tween, cancellationToken, completion);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(tween)) tween.Kill();
        }
    }

    // Killing a Tween does not emit Finished. Tree exit and cancellation must settle the wait explicitly.
    internal static async Task Wait(Node owner, Tween tween, CancellationToken cancellationToken,
        TaskCompletionSource? completion = null)
    {
        completion ??= new TaskCompletionSource();
        if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree() || owner.IsQueuedForDeletion())
        {
            if (GodotObject.IsInstanceValid(tween)) tween.Kill();
            throw new OperationCanceledException("Tactical retreat node left the scene.");
        }
        SceneTree tree = owner.GetTree();
        void Finished() => completion.TrySetResult();
        void Exiting() => completion.TrySetCanceled();
        void Frame()
        {
            // An externally killed Tween also never emits Finished.
            if (!GodotObject.IsInstanceValid(tween) || !tween.IsValid() || CombatManager.Instance.IsEnding)
                completion.TrySetCanceled();
        }
        owner.TreeExiting += Exiting;
        tween.Finished += Finished;
        tree.ProcessFrame += Frame;
        using CancellationTokenRegistration registration = cancellationToken.Register(() => completion.TrySetCanceled());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GodotObject.IsInstanceValid(owner) || !owner.IsInsideTree())
                throw new OperationCanceledException("Tactical retreat node left the scene.");
            tween.Play();
            await completion.Task;
        }
        finally
        {
            if (GodotObject.IsInstanceValid(owner)) owner.TreeExiting -= Exiting;
            if (GodotObject.IsInstanceValid(tree)) tree.ProcessFrame -= Frame;
            if (GodotObject.IsInstanceValid(tween))
            {
                tween.Finished -= Finished;
                tween.Kill();
            }
        }
    }
}
