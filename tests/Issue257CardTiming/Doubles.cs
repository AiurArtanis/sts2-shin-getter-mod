using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

internal static class H
{
    internal static readonly List<string> Events = new();
    internal static readonly List<float> Waits = new();
    internal static readonly List<(decimal damage, int count)> Hits = new();
    internal static bool Voice, FailDamage, SequenceAvailable;
    internal static float Duration;
    internal static int VoiceCalls;
    internal static TaskCompletionSource? RushGate, ImpactGate;
    internal static void Reset()
    {
        Events.Clear(); Waits.Clear(); Hits.Clear(); Voice = FailDamage = false;
        SequenceAvailable = true; Duration = 0; VoiceCalls = 0; RushGate = ImpactGate = null;
    }
}
namespace MegaCrit.Sts2.Core.Entities.Cards
{
    public enum CardTag { Strike }
    public enum CardType { Attack }
    public enum CardRarity { Uncommon, Rare }
    public enum TargetType { AnyEnemy }
    public sealed class CardPlay { public Creature? Target { get; init; } }
}
namespace MegaCrit.Sts2.Core.Entities.Powers { public enum PowerType { Buff, Debuff } }
namespace MegaCrit.Sts2.Core.ValueProps { public enum ValueProp { Move } }
namespace MegaCrit.Sts2.Core.GameActions.Multiplayer { public sealed class PlayerChoiceContext { } }
namespace MegaCrit.Sts2.Core.Models
{
    public class PowerModel { public int Amount = 1; public PowerType Type; }
    public class CardModel { public Player Owner { get; } = new(); }
    public sealed class Player
    {
        public Creature Creature { get; } = new();
        public ShinGetterMod.Models.Cards.ShinGetterForm Form;
    }
}
namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    public sealed class Creature
    {
        public List<PowerModel> Powers { get; } = new();
        public object? CombatState { get; } = new();
        public T? GetPower<T>() where T : PowerModel => Powers.OfType<T>().FirstOrDefault();
        public bool HasPower<T>() where T : PowerModel => GetPower<T>() != null;
    }
}
namespace MegaCrit.Sts2.Core.Localization.DynamicVars
{
    public class DynamicVar(string name, decimal value)
    {
        public string Name = name; public decimal BaseValue = value;
        public void UpgradeValueBy(decimal amount) => BaseValue += amount;
    }
    public sealed class DamageVar(decimal value, MegaCrit.Sts2.Core.ValueProps.ValueProp prop) : DynamicVar("Damage", value)
    { public MegaCrit.Sts2.Core.ValueProps.ValueProp Props { get; } = prop; }
    public sealed class Vars(IEnumerable<DynamicVar> items) : Dictionary<string, DynamicVar>(items.ToDictionary(x => x.Name))
    { public DynamicVar Damage => this["Damage"]; }
}
namespace MegaCrit.Sts2.Core.Models.Powers
{
    public sealed class PlatingPower : PowerModel { }
    public sealed class VulnerablePower : PowerModel { public VulnerablePower() { Type = PowerType.Debuff; } }
    public sealed class FrailPower : PowerModel { public FrailPower() { Type = PowerType.Debuff; } }
}
namespace ShinGetterMod.Models.Powers
{
    public sealed class SGP_Ki : PowerModel { }
    public sealed class SGP_ShinForm : PowerModel { }
}
namespace MegaCrit.Sts2.Core.Commands
{
    public static class Cmd
    {
        public static Task Wait(float duration) { H.Waits.Add(duration); return Task.CompletedTask; }
        public static Task CustomScaledWait(float fast, float normal) => Wait(normal);
    }
    public static class PowerCmd
    {
        public static Task Apply<T>(PlayerChoiceContext context, Creature target, decimal amount, Creature source, CardModel card) where T : PowerModel, new()
        {
            var power = target.GetPower<T>();
            if (power == null) target.Powers.Add(new T { Amount = (int)amount });
            else power.Amount += (int)amount;
            return Task.CompletedTask;
        }
    }
    public static class DamageCmd { public static Attack Attack(decimal damage) => new(damage); }
    public sealed class Attack(decimal damage)
    {
        private int count = 1;
        public Attack FromCard(CardModel card) => this;
        public Attack Targeting(Creature target) => this;
        public Attack TargetingRandomOpponents(object state) => this;
        public Attack WithNoAttackerAnim() => this;
        public Attack WithHitFx(string path) => this;
        public Attack WithHitCount(int value) { count = value; return this; }
        public Attack AfterAttackerAnim(Func<Task> callback) => this;
        public Task Execute(PlayerChoiceContext context)
        {
            H.Events.Add("hit"); H.Hits.Add((damage, count));
            return H.FailDamage ? Task.FromException(new InvalidOperationException("Injected damage failure")) : Task.CompletedTask;
        }
    }
}
namespace ShinGetterMod.Models.Cards
{
    public enum ShinGetterForm { Getter1, Getter3 }
    public abstract class ShinGetterCardBase : CardModel
    {
        protected ShinGetterCardBase(int cost, CardType type, CardRarity rarity, TargetType target) { DynamicVars = new(CanonicalVars); }
        protected virtual HashSet<CardTag> CanonicalTags => new();
        protected abstract IEnumerable<DynamicVar> CanonicalVars { get; }
        protected Vars DynamicVars { get; }
        protected abstract Task OnPlay(PlayerChoiceContext context, CardPlay play);
        protected virtual void OnUpgrade() { }
        protected bool HasForm(Player player, ShinGetterForm form) => player.Form == form;
        protected Task PlayMovementVfx(Func<Task> visual) => visual();
        protected Func<Task> AccelerateFollowupAnimations(int count) => () => Task.CompletedTask;
        public Task Play(Creature target) => OnPlay(new(), new() { Target = target });
    }
}
namespace ShinGetterMod.Audio
{
    public static class ShinGetterVoiceService
    {
        public static bool TryPlayCardVoiceAtCustomTiming(CardModel card, out float duration)
        { H.VoiceCalls++; duration = H.Duration; return H.Voice; }
        public static Task PlayShiningSparkIntro(Player player) => Task.CompletedTask;
        public static Task PlayShiningSparkFollowUp(Player player) => Task.CompletedTask;
    }
}
namespace ShinGetterMod.Nodes.Vfx
{
    public static class ShinGetterCombatVfx
    {
        public static async Task PlayExpansionRush(Creature owner, Creature target, Func<Task> impact)
        {
            if (H.RushGate != null) await H.RushGate.Task;
            await impact(); H.Events.Add("rush-recovered");
        }
        public static Task PlayWhiteFlash(Creature owner) => Task.CompletedTask;
        public static Task PlayRush(Creature owner, Creature target, bool whiteFlash)
        { H.Events.Add("fallback-rush"); return Task.CompletedTask; }
    }
}
namespace ShinGetterMod.Nodes.Combat
{
    public sealed class NShinGetterShiningSparkSequence
    {
        public static NShinGetterShiningSparkSequence? TryCreate(Creature owner, Creature target) => H.SequenceAvailable ? new() : null;
        public async Task PlayToImpact(Func<Task> intro, Func<Task> followup)
        { await intro(); await followup(); H.Events.Add("contact"); }
        public Task PlayImpact() { H.Events.Add("impact"); return H.ImpactGate?.Task ?? Task.CompletedTask; }
        public Task Recover() { H.Events.Add("recover"); return Task.CompletedTask; }
        public void Close() => H.Events.Add("close");
    }
}
