#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ShinGetterMod.Nodes.Combat;

namespace ShinGetterMod.Nodes.Vfx;

internal static partial class ShinGetterCombatVfx
{
    private const float TacticalRetreatDistance = 480f;

    public static async Task PlayTacticalRetreat(Creature owner, Func<Task> transform)
    {
        NCreature? ownerNode = NCombatRoom.Instance?.GetCreatureNode(owner);
        if (ownerNode == null)
        {
            await transform();
            return;
        }

        Vector2 origin = ownerNode.GlobalPosition;
        Vector2 originCenter = ownerNode.VfxSpawnPosition;
        Vector2 retreatDirection = owner.IsEnemy ? Vector2.Right : Vector2.Left;
        Vector2 retreatPosition = origin + retreatDirection * TacticalRetreatDistance;
        using var lifetime = new CancellationTokenSource();
        void Exiting() => lifetime.Cancel();
        ownerNode.TreeExiting += Exiting;
        bool ShouldAbortAction() => CombatManager.Instance.IsEnding || !owner.IsAlive;
        Tween? retreatTween = null;
        try
        {
            try
            {
                await NShinGetterStaticVisuals.PlayCreatureActionAnimationAndWait(
                    owner, "Block", fallbackDuration: 0.4f, cancellationToken: lifetime.Token);
                lifetime.Token.ThrowIfCancellationRequested();
                AddAfterimageLines(originCenter, originCenter + retreatDirection * TacticalRetreatDistance, RushLine, 6, 18f);
                retreatTween = ownerNode.CreateTween();
                retreatTween.Pause();
                retreatTween.TweenProperty(ownerNode, "global_position", retreatPosition, 0.24f)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                await TacticalRetreatTween.Wait(ownerNode, retreatTween, lifetime.Token);
            }
            catch (OperationCanceledException) when (!ShouldAbortAction())
            {
                // The model transform has not started yet. Invoke it once without the obsolete visual scope.
                await transform();
                return;
            }

            bool returnStarted = false;
            using var sync = new TacticalRetreatSync(owner, progress =>
            {
                if (!GodotObject.IsInstanceValid(ownerNode) || !ownerNode.IsInsideTree())
                    throw new OperationCanceledException("Tactical retreat owner left the scene.");
                if (!returnStarted)
                {
                    returnStarted = true;
                    AddAfterimageLines(originCenter + retreatDirection * TacticalRetreatDistance, originCenter, GetterRay, 6, 18f);
                }
                float eased = MathF.Sin((float)progress * MathF.PI * 0.5f);
                ownerNode.GlobalPosition = retreatPosition.Lerp(origin, eased);
            }, lifetime.Token, ShouldAbortAction);
            await transform();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(retreatTween)) retreatTween!.Kill();
            if (GodotObject.IsInstanceValid(ownerNode))
            {
                ownerNode.TreeExiting -= Exiting;
                if (ownerNode.IsInsideTree()) ownerNode.GlobalPosition = origin;
            }
        }
    }
}
