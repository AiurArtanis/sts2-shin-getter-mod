#!/usr/bin/env python3
"""Read-only wiring guards for issue#206 console controls; no Godot/game launch."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def check():
    command = ROOT / "src/Diagnostics/ShinGetterDialogueConsoleCmd.cs"
    assert command.exists(), "RED: sgd command has not been implemented"
    cmd = command.read_text(encoding="utf-8-sig")
    session = (ROOT / "src/Services/ShinGetterBondSession.cs").read_text(encoding="utf-8-sig")
    plan = (ROOT / "src/Services/ShinGetterBondConsolePlan.cs").read_text(encoding="utf-8-sig")
    ui = (ROOT / "src/Nodes/Events/NShinGetterBondDialogue.cs").read_text(encoding="utf-8-sig")
    bridge = (ROOT / "src/Patches/ShinGetterConsoleCommandPatch.cs").read_text(encoding="utf-8-sig")
    assert 'CmdName => "sgd"' in cmd and "IsNetworked => false" in cmd
    assert "TryParse" in cmd and "GetArgumentCompletions" in cmd
    assert "IsOpen" in cmd and "GameMode.Standard" in cmd and "Players.Count != 1" in cmd
    assert "TryConsole" in cmd and 'GetProfileScopedPath("shin_getter_bonds.json")' in session
    assert "Read(migrateIfMissing: false)" in session and "plan.Apply(next)" in session
    begin = session[session.index("internal bool Begin()"):session.index("internal bool MarkDisplayed()")]
    assert 'next.DebugNextDialogues.Remove(_npc)' in begin
    assert "Encounter != null" in begin and "Commit(next =>" in begin
    select = session[session.index("private ShinGetterBondEncounter SelectEncounter()"):session.index("private List<RunHistory>")]
    assert select.index("DebugNextDialogues.TryGetValue") < select.index("IsAcquainted") < select.index("RandomNumberGenerator")
    assert "ShinGetterBondConsolePlan.ChoicesToken" in select
    assert "ValidatePending(save)" in session
    assert "File.Replace" in session and "file.Flush(flushToDisk: true)" in session
    assert session.index("File.Replace") < session.index("_save = next")
    assert "LastMetRun.Remove(npc)" in plan and "RespondedResults.Remove(npc)" in plan
    assert "LegacyAcquaintances.Remove(npc)" in plan and "save.Encounters.Remove(key)" in plan
    assert "Completed.RemoveWhere" in plan and "ShinGetterDialogueCatalog.BondNpcs" in plan
    assert "int.TryParse" in plan and "number is < 0 or > 3" in plan
    assert "private static int _openCount" in ui and "ReleaseConsoleGuard()" in ui
    assert "_openCount++" in ui and "_openCount--" in ui
    assert "ReleaseConsoleGuard();" in ui[ui.index("private void Close()"):ui.index("public override void _ExitTree()")]
    assert "ReleaseConsoleGuard();" in ui[ui.index("public override void _ExitTree()"):ui.index("private void ReleaseConsoleGuard()")]
    assert 'ShinGetterDialogueCommandName = "sgd"' in bridge and "new ShinGetterDialogueConsoleCmd().Process(player, args)" in bridge
    assert "TestNext" not in cmd  # no direct scene enter/reward command
    for bad in ("SaveRun(", "LoadRunHistory(", "EnterRoom(", "WinRun(", "AttackCommand", "PowerCmd"):
        assert bad not in cmd and bad not in plan, bad
    print("issue#206 console structural checks PASS (not game acceptance)")


if __name__ == "__main__":
    check()
