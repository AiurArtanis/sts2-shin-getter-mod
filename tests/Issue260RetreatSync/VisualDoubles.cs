using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using ShinGetterMod.Models.Cards;

namespace MegaCrit.Sts2.Core.Entities.Creatures { public sealed class Creature { public bool IsEnemy; public bool IsAlive = true; } }
namespace MegaCrit.Sts2.Core.Nodes.Combat
{
    public sealed class NCreatureVisuals : Node2D { }
    public sealed class NCreature : Node2D { public Vector2 VfxSpawnPosition => GlobalPosition; }
}
namespace MegaCrit.Sts2.Core.Nodes.Rooms
{
    public sealed class NCombatRoom
    {
        public static NCombatRoom? Instance;
        public readonly Dictionary<Creature, NCreature> Nodes = new();
        public NCreature? GetCreatureNode(Creature creature) => Nodes.GetValueOrDefault(creature);
    }
}
namespace ShinGetterMod.Models.Cards { public enum ShinGetterForm { None, Getter1, Getter2, Getter3 } }
namespace ShinGetterMod.Nodes.Combat
{
    public static class NShinGetterSpriteSequence
    {
        public const string FusionAnimationName = "fusion";
        public static bool EnsureFusionLoaded(AnimatedSprite2D sprite, ShinGetterForm form) => sprite.FusionAvailable;
        public static void ReleaseActionAnimations(AnimatedSprite2D sprite) { }
    }
    public static partial class NShinGetterStaticVisuals
    {
        private const float FusionTransitionHoldSeconds = .2f;
        public static TaskCompletionSource? BlockGate;
        public static int OriginalFusionCalls;
        public static bool OriginalFusionSucceeds = true;
        public static Task PlayCreatureActionAnimationAndWait(Creature creature, string trigger, float fallbackDuration,
            CancellationToken cancellationToken = default) => BlockGate?.Task.WaitAsync(cancellationToken) ?? Task.CompletedTask;
        private static bool TryGetAtomicForm(FormVisual visual, out ShinGetterForm form)
        {
            form = visual.Node.Name switch
            {
                "GetterOne" => ShinGetterForm.Getter1, "GetterTwo" => ShinGetterForm.Getter2,
                "GetterThree" => ShinGetterForm.Getter3, _ => ShinGetterForm.None,
            };
            return form != ShinGetterForm.None;
        }
        private static void ActivateIdleAnimation(FormVisual visual)
        {
            var sprite = (AnimatedSprite2D)visual.Node; sprite.IdleCalls++; sprite.Animation = "idle";
        }
        private static Task<bool> TryPlayFusionTransition(FormSprites sprites, FormVisual? previous,
            FormVisual next, float speed) { OriginalFusionCalls++; return Task.FromResult(OriginalFusionSucceeds); }
        private static void HideInactive(FormSprites sprites, FormVisual next, FormVisual? previous, Vector2 previousScale)
        {
            foreach (var visual in sprites.All)
                if (visual.Item != next.Item) visual.Item.Visible = false;
        }
        private readonly record struct FormVisual(CanvasItem Item, Node2D Node);
        private readonly record struct FormSprites(FormVisual GetterOne, FormVisual GetterTwo,
            FormVisual GetterThree, FormVisual ShinDragon)
        {
            public FormVisual[] All => new[] { GetterOne, GetterTwo, GetterThree, ShinDragon };
        }
        internal static Task RunSwitch(NCreatureVisuals visuals, AnimatedSprite2D[] sprites, int next,
            float speed, TacticalRetreatSync? sync)
        {
            var forms = sprites.Select(sprite => new FormVisual(sprite, sprite)).ToArray();
            return SwitchTo(visuals, new(forms[0], forms[1], forms[2], forms[3]), forms[next], true, speed, sync);
        }
    }
}
namespace ShinGetterMod.Nodes.Vfx
{
    internal static partial class ShinGetterCombatVfx
    {
        private static readonly Color RushLine = new(1), GetterRay = new(1);
        private static void AddAfterimageLines(Vector2 from, Vector2 to, Color color, int count, float width) { }
    }
}
