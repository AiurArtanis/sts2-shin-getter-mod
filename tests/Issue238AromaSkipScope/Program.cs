using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using ShinGetterMod.Events;

const string Aroma = "AROMA_OF_CHAOS";
const string Hayato = "SHIN_GETTER_EVENT_INVASION.AROMA_OF_CHAOS.pages.INITIAL.options.HAYATO";
int passed = 0;
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
async Task Check(string name, Func<Task> action)
{
    await action();
    Require(!ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "selection scope leaked after test");
    Console.WriteLine("PASS " + name);
    passed++;
}
static ShinGetterEventInteractionCheckpoint Make(string entry, string option, Func<bool>? current = null)
    => new(new EventModel(entry), new EventSynchronizer(), new Player(), new EventOption(option),
        current ?? (() => true), () => Task.CompletedTask, _ => { });

await Check("ordinary selectors have no owned Aroma presentation", () =>
{
    Require(!ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "unowned selector matched");
    return Task.CompletedTask;
});
await Check("current exact Aroma/Hayato scope flows through await", async () =>
{
    using var session = Make(Aroma, Hayato);
    using var scope = session.Select();
    Require(ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "owned scope not matched");
    await Task.Yield();
    Require(ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "owned scope lost at native async boundary");
});
await Check("other event with a misleading option does not match", () =>
{
    using var session = Make("BRAIN_LEECH", Hayato);
    using var scope = session.Select();
    Require(!ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "other event matched");
    return Task.CompletedTask;
});
await Check("native and different Aroma routes do not match", () =>
{
    foreach (string key in new[] { "AROMA_OF_CHAOS.pages.INITIAL.options.LET_GO", Hayato + "_OTHER", Hayato.Replace("HAYATO", "RYOMA") })
    {
        using var session = Make(Aroma, key);
        using var scope = session.Select();
        Require(!ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "different route matched");
    }
    return Task.CompletedTask;
});
await Check("stale or departed event does not match", () =>
{
    bool current = true;
    using var session = Make(Aroma, Hayato, () => current);
    using var scope = session.Select();
    Require(ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "current event missing");
    current = false;
    Require(!ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "departed event still matched");
    return Task.CompletedTask;
});
await Check("disposed checkpoint rejects an inherited live scope", () =>
{
    using var session = Make(Aroma, Hayato);
    using var scope = session.Select();
    session.Dispose();
    Require(!ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "disposed checkpoint matched");
    return Task.CompletedTask;
});
await Check("nested other selection restores the owning Aroma scope", async () =>
{
    using var outer = Make(Aroma, Hayato);
    using var outerScope = outer.Select();
    using (var inner = Make("BRAIN_LEECH", "OTHER"))
    using (inner.Select())
    {
        await Task.Yield();
        Require(!ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "nested selector changed");
    }
    Require(ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "parent scope not restored");
});
await Check("parallel async selections do not leak localization ownership", async () =>
{
    async Task Observe(bool aroma)
    {
        using var session = Make(aroma ? Aroma : "BRAIN_LEECH", aroma ? Hayato : "OTHER");
        using var scope = session.Select();
        await Task.Yield();
        Require(ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection == aroma, "another flow's scope leaked");
    }
    await Task.WhenAll(Observe(true), Observe(false));
});
await Check("localization predicate performs no save or completion action", () =>
{
    int saves = 0, reports = 0;
    using var session = new ShinGetterEventInteractionCheckpoint(new EventModel(Aroma), new EventSynchronizer(),
        new Player(), new EventOption(Hayato), () => true, () => { saves++; return Task.CompletedTask; }, _ => reports++);
    using var scope = session.Select();
    for (int i = 0; i < 3; i++)
        Require(ShinGetterEventInteractionCheckpoint.IsAromaCandidateSelection, "owned predicate unstable");
    Require(saves == 0 && reports == 0 && !session.HistoryReady.IsCompleted, "localization changed interaction state");
    return Task.CompletedTask;
});
Console.WriteLine($"issue#238 scoped localization: {passed} CPU checks PASS; UI/locale rendering/cancel/payment runtime NOT_RUN");
