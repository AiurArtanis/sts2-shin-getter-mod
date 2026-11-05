"""Source wiring for the host-committed Trial transition (CPU fixture tests ordering)."""
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = (root / "src/Events/ShinGetterEventInvasionService.cs").read_text(encoding="utf-8")
assert "ShinGetterTrialCombatSync.Request" in source, "Trial needs a replicated acceptance boundary"
assert "StartedTrialBattles" not in source, "an entry-attempt marker is not a replicated setup boundary"
committed = source.split("internal static void StartCommittedTrialBattle", 1)[1].split("internal static void ClearPendingTrialSetup", 1)[0]
assert committed.index("PendingBattleSetups[owner] =") < committed.index("EnterEventCombat("), "install setup before native sync/first hand"
assert "ModelDb.Encounter<KnightsElite>().ToMutable()" in committed
assert "Array.Empty<Reward>(), false" in committed, "keep original Trial rewards/resume policy"
request = source.split("private static Task StartEventBattle", 1)[1].split("internal static void StartCommittedTrialBattle", 1)[0]
assert request.index("ShinGetterTrialCombatSync.Request") < request.index("PendingBattleSetups[owner] ="), "unaccepted requests must not write setup"
assert "NetGameType.Host or NetGameType.Client" in request, "singleplayer must keep native entry"
sync = (root / "src/Events/ShinGetterTrialCombatSync.cs").read_text(encoding="utf-8")
patch = (root / "src/Patches/ShinGetterTrialCombatSyncPatch.cs").read_text(encoding="utf-8")
message = (root / "src/Events/ShinGetterTrialChallengeMessage.cs").read_text(encoding="utf-8")
for token in (
    "_netService.Type != NetGameType.Host", "_acceptedOwner.HasValue",
    "client.NetClient?.HostNetId != senderId", "message.Location != _location",
    "_localEvent.Owner.RunState.RunLocation == _location",
    "RegisterMessageHandler<ShinGetterTrialChallengeMessage>",
    "UnregisterMessageHandler<ShinGetterTrialChallengeMessage>",
    "ShinGetterEventInvasionService.StartCommittedTrialBattle",
    "ReferenceEquals(sync._choosingOwner, eventModel.Owner)",
    "message.RunSeed != _runSeed",
):
    assert token in sync, token
assert sync.index("sync._acceptedOwner = owner.NetId;") < sync.index("sync._netService.SendMessage(message);")
assert sync.index("sync._netService.SendMessage(message);") < sync.index("sync.StartCommittedBattle();")
assert "_entryAttempted = true;" in sync, "claim entry before invoking native fire-and-forget"
for token in ("MethodType.Constructor", "nameof(EventSynchronizer.BeginEvent)", "nameof(EventSynchronizer.Dispose)", '"ChooseOptionForEvent"', "nameof(RunManager.CleanUp)", "nameof(EventRoom.Exit)"):
    assert token in patch, token
for token in ("ShouldBroadcast => false", "NetTransferMode.Reliable", "ShouldBuffer => true", "writer.WriteULong(OwnerId)", "reader.ReadULong()"):
    assert token in message, token
assert "IsShared => true" not in source + sync, "do not replace individual choices with shared votes"
print("issue#283 source wiring PASS; run tests/Issue283TrialSync for ordered CPU checks; gameplay NOT_RUN")
