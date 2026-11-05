"""Source contract for relaying an individual Trial fight to every peer."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = (root / "src/Events/ShinGetterEventInvasionService.cs").read_text(encoding="utf-8")
start = source.split("private static Task StartEventBattle", 1)[1].split("private static Task LoseHp", 1)[0]
for token in (
    "setup == PendingBattleSetup.Trial && owner.RunState.Players.Count > 1",
    "RunManager.Instance.EventSynchronizer.GetLocalEvent()",
    "StartedTrialBattles.TryGetValue(combatEvent, out EncounterModel? startedEncounter)",
    "PendingBattleSetups[owner] = (setup, startedEncounter);",
    "StartedTrialBattles.Add(combatEvent, encounter);",
    "EventsEnteringSinglePlayerCombat.Add(combatEvent);",
    "new object[] { encounter, extraRewards, shouldResumeAfterCombat }",
    "StartedTrialBattles.Remove(combatEvent);",
    "EventsEnteringSinglePlayerCombat.Remove(combatEvent);",
):
    assert token in start, token
assert "ConditionalWeakTable<EventModel, EncounterModel> StartedTrialBattles" in source
assert start.index("StartedTrialBattles.TryGetValue") < start.index("EnterCombatWithoutExitingEventMethod.Invoke")
assert "combatEvent is not Trial" in start, "do not route other event types through a Trial relay"
assert "IsShared => true" not in source, "do not turn all Trial choices into shared votes"
print("issue#283 source gate PASS; mixed/all-Getter multiplayer gameplay NOT_RUN")
