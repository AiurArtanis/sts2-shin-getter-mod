using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using ShinGetterMod.Models.Cards;
using ShinGetterMod.Models.Powers;

// Execute production card methods against deterministic command/VFX doubles.
// These checks do not exercise Godot rendering, resource loading or audio playback.
int passed = 0;
async Task Check(string name, Func<Task> test)
{
    H.Reset();
    await test().WaitAsync(TimeSpan.FromSeconds(5));
    Console.WriteLine("PASS " + name);
    passed++;
}
static void Assert(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}
static (SGC_ExpansionStrike card, Creature target) Expansion()
{
    var card = new SGC_ExpansionStrike();
    card.Owner.Form = ShinGetterForm.Getter3;
    card.Owner.Creature.Powers.Add(new VulnerablePower());
    var target = new Creature();
    target.Powers.Add(new FrailPower());
    return (card, target);
}
foreach (var (name, plays, duration, wait) in new[] {
    ("expansion audible lead-in", true, 2.176f, .86f),
    ("expansion silent/consumed voice has no lead-in", false, 0f, 0f),
    ("expansion lead-in bounded by voice", true, .2f, .2f),
    ("expansion zero-duration voice has no lead-in", true, 0f, 0f) })
{
    await Check(name, async () => {
        H.Voice = plays; H.Duration = duration; H.RushGate = new();
        var (card, target) = Expansion();
        Task play = card.Play(target);
        Assert(H.Hits.Count == 0, "Damage before contact");
        Assert(H.Waits.SequenceEqual(wait > 0 ? new[] { wait } : Array.Empty<float>()), "Unexpected voice wait");
        H.RushGate.SetResult(); await play;
        Assert(H.Hits.Single() == (10m, 1), "Damage formula changed");
        Assert(card.Owner.Creature.GetPower<PlatingPower>()?.Amount == 4, "Plating changed");
        Assert(H.Events.IndexOf("hit") < H.Events.IndexOf("rush-recovered"), "Damage after recovery");
        Assert(H.VoiceCalls == 1, "Duplicate custom voice call");
    });
}
await Check("expansion zero debuffs no plating", async () => {
    var card = new SGC_ExpansionStrike(); card.Owner.Form = ShinGetterForm.Getter3;
    await card.Play(new Creature());
    Assert(H.Hits.Single().damage == 0 && card.Owner.Creature.GetPower<PlatingPower>() == null, "Zero-debuff branch changed");
});
await Check("spark contact hit concurrent with impact hold; ki and recovery", async () => {
    var card = new SGC_ShiningSpark(); card.Owner.Creature.Powers.Add(new SGP_ShinForm());
    H.ImpactGate = new();
    Task play = card.Play(new Creature());
    Assert(H.Hits.SequenceEqual(new[] { (11m, 1) }), "Initial hit blocked on impact hold");
    Assert(!play.IsCompleted && !H.Events.Contains("recover"), "Recovery before impact complete");
    H.ImpactGate.SetResult(); await play;
    Assert(H.Hits.SequenceEqual(new[] { (11m, 1), (6m, 3) }), "Ki followup changed");
    Assert(H.Events.IndexOf("recover") > H.Events.LastIndexOf("hit") && H.Events.Last() == "close", "Recovery/cleanup order");
    Assert(card.Owner.Creature.HasPower<VulnerablePower>() && card.Owner.Creature.HasPower<FrailPower>(), "Debuffs missing");
});
await Check("spark non-dragon fallback", async () => {
    var card = new SGC_ShiningSpark(); card.Owner.Creature.Powers.Add(new SGP_Ki { Amount = 2 });
    await card.Play(new Creature());
    Assert(H.Events.Contains("fallback-rush") && !H.Events.Contains("impact"), "Wrong fallback");
    Assert(H.Hits.SequenceEqual(new[] { (11m, 1), (6m, 2) }), "Fallback damage changed");
});
await Check("spark missing sequence still deals damage", async () => {
    H.SequenceAvailable = false;
    var card = new SGC_ShiningSpark(); card.Owner.Creature.Powers.Add(new SGP_ShinForm());
    await card.Play(new Creature());
    Assert(H.Hits.SequenceEqual(new[] { (11m, 1), (6m, 3) }) && !H.Events.Contains("impact"), "Missing visual suppresses damage");
});
await Check("spark damage failure closes sequence", async () => {
    H.FailDamage = true;
    var card = new SGC_ShiningSpark(); card.Owner.Creature.Powers.Add(new SGP_ShinForm());
    try { await card.Play(new Creature()); throw new Exception("Expected damage failure"); }
    catch (InvalidOperationException) { }
    Assert(H.Events.Last() == "close" && !H.Events.Contains("recover"), "Failure cleanup missing");
});
Console.WriteLine($"{passed}/9 PASS; production card methods, simulated dependencies; not native GPU acceptance");
