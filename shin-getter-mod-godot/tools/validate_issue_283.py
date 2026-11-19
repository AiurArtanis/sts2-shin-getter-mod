"""Source wiring for host-committed event combat (CPU fixture tests ordering)."""
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = (root / "src/Events/ShinGetterEventInvasionService.cs").read_text(encoding="utf-8")
assert "ShinGetterTrialCombatSync.Request" in source, "event combat needs a replicated acceptance boundary"
assert "StartedTrialBattles" not in source, "an entry-attempt marker is not a replicated setup boundary"
committed = source.split("internal static void StartCommittedEventBattle", 1)[1].split("internal static void ClearPendingEventBattleSetup", 1)[0]
assert committed.index("PendingBattleSetups[owner] =") < committed.index("EnterEventCombat("), "install setup before native sync/first hand"
assert "Trial => (PendingBattleSetup.Trial, ModelDb.Encounter<KnightsElite>(), false)" in committed
assert "ByrdonisNest => (PendingBattleSetup.ByrdonisNest, ModelDb.Encounter<ByrdonisElite>(), true)" in committed
assert "foreach (EventModel playerEvent in events)" in committed
assert "EnterEventCombat(playerEvent, owner, battle.Encounter, Array.Empty<Reward>(), battle.ShouldResume)" in committed
request = source.split("private static Task StartEventBattle", 1)[1].split("internal static void StartCommittedEventBattle", 1)[0]
assert request.index("ShinGetterTrialCombatSync.Request") < request.index("StartCommittedEventBattle(")
assert "PendingBattleSetups[" not in request, "unaccepted requests must not write setup"
assert "NetGameType.Host or NetGameType.Client" in request and "players.Count == 1" in request, "singleplayer must keep native entry"
sync = (root / "src/Events/ShinGetterTrialCombatSync.cs").read_text(encoding="utf-8")
patch = (root / "src/Patches/ShinGetterTrialCombatSyncPatch.cs").read_text(encoding="utf-8")
message = (root / "src/Events/ShinGetterTrialChallengeMessage.cs").read_text(encoding="utf-8")
for token in (
    "_netService.Type != NetGameType.Host", "_acceptedOwner.HasValue",
    "client.NetClient?.HostNetId != senderId", "message.Location != _location",
    "owner.RunState.RunLocation == _location",
    "RegisterMessageHandler<ShinGetterTrialChallengeMessage>",
    "UnregisterMessageHandler<ShinGetterTrialChallengeMessage>",
    "ShinGetterEventInvasionService.StartCommittedEventBattle",
    "ReferenceEquals(sync._choosingOwner, owner)",
    "message.RunSeed != _runSeed",
    "synchronizer.GetEventForPlayer(player)",
    "ReferenceEquals(playerEvent.Owner, player)",
    "ReferenceEquals(playerEvent.CanonicalInstance, activeEvent.CanonicalInstance)",
):
    assert token in sync, token
assert sync.index("sync._acceptedOwner = owner.NetId;") < sync.index("sync._netService.SendMessage(message);")
assert sync.index("sync._netService.SendMessage(message);") < sync.index("sync.StartCommittedBattle();")
assert "_entryAttempted = true;" in sync, "claim entry before invoking native fire-and-forget"
for token in ("MethodType.Constructor", "nameof(EventSynchronizer.BeginEvent)", "nameof(EventSynchronizer.Dispose)", '"ChooseOptionForEvent"', "nameof(RunManager.CleanUp)", "nameof(EventRoom.Exit)"):
    assert token in patch, token
initializer = patch.split("[HarmonyPatch(typeof(EventSynchronizer), MethodType.Constructor,", 1)[1].split("internal static class ShinGetterTrialSyncInitPatch", 1)[0]
signature = ["RunLocationTargetedMessageBuffer", "INetGameService", "IPlayerCollection", "IRunState", "ulong", "ulong"]
assert re.findall(r"typeof\((\w+)\)", initializer) == signature, "constructor needs the exact six argument types in native order"
for token in ("[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.OptionButtonClicked))]", "TryRouteProceed(RunManager.Instance.EventSynchronizer, option, index)"):
    assert token in patch, token
for token in ("option.IsProceed", "option.IsLocked", "option.WasChosen", "option.TextKey != GetStartFightOptionKey(localEvent)", "ReferenceEquals(localEvent.CurrentOptions[index], option)", "synchronizer.ChooseLocalOption(index)"):
    assert token in sync, token
assert "isProceed: true" in source, "keep native Proceed semantics outside the narrow multiplayer routing patch"
assert "Player? owner = eventModel.Owner;" in sync and "Player? owner = _localEvent?.Owner;" in sync
for token in ("ShouldBroadcast => false", "NetTransferMode.Reliable", "ShouldBuffer => true", "writer.WriteULong(OwnerId)", "writer.WriteULong(RunSeed)", "RunSeed = reader.ReadULong()"):
    assert token in message, token
assert "IsShared => true" not in source + sync, "do not replace individual choices with shared votes"
print("issue#283 source wiring PASS; run Issue283TrialSync for CPU behavior and Issue283HarmonyTargets for real-library target resolution; gameplay NOT_RUN")
