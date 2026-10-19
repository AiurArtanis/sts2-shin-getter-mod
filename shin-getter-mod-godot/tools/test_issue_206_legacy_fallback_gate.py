"""Source and rejected-mutation gate for the 2026-10-19 recovery contract.

CPU bridge/session fixtures cover dispatch and transactions separately. This gate
does not execute the Godot UI, physical input, ending animations or score flow.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UI = (ROOT / "src/Nodes/Events/NShinGetterBondDialogue.cs").read_text(encoding="utf-8-sig")
BRIDGE = (ROOT / "src/Patches/ShinGetterBondDialoguePatch.cs").read_text(encoding="utf-8-sig")
SESSION = (ROOT / "src/Services/ShinGetterBondSession.cs").read_text(encoding="utf-8-sig")


def check(ui, bridge, session):
    identity = session[session.index("private static string BuildEncounterKey"):session.index("internal bool Begin()")]
    assert all(token in identity for token in ("current.ModelId != model.Id", "model is TheArchitect", "state.MapPointHistory[act]"))
    assert identity.index("current.ModelId != model.Id") < identity.index("model is TheArchitect") < identity.index("state.MapPointHistory[act]")
    assert 'encounter-v2:{run}:{state.Rng.Seed}:terminal:{model.Id}' in identity
    assert "Session => _session ??= new(_model)" in bridge
    play = bridge[bridge.index("internal static class ShinGetterBondArchitectPlayPatch"):bridge.index("internal static class ShinGetterBondArchitectSetupPatch")]
    assert "Attach(" not in play and "state.LegacyFallback) return true" in play
    assert '[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.OnSetupComplete))]' in bridge
    recovery = ui[ui.index("private void ShowFatalError"):ui.index("private void Next()")]
    assert "_useLegacyDialogue = true" in recovery and "Close();" in recovery
    assert "_session.Skip(" not in ui and "交谈进度未能保存" not in ui
    attempt = ui[ui.index("private void Attempt("):ui.index("private void Render()")]
    assert "if (!operation())" in attempt and "catch (Exception ex) { ShowFatalError(ex); }" in attempt
    assert "_useLegacyDialogue ? _fallbackToLegacy : _returnToEvent" in ui
    assert "state.Returned = true" in bridge and "state.LegacyFallback = true" in bridge
    assert "legacy.SetDialogue(lines)" in bridge
    assert "LoadDialogue.Invoke(model, null)" in bridge and "await (Task)PlayCurrentLine.Invoke(model, null)!" in bridge
    assert "if (model.CurrentOptions.Count == 0 || previousOptions.SequenceEqual(model.CurrentOptions)) RestoreProceed(model)" in bridge
    restore = bridge[bridge.index("private static async Task RestoreLegacy"):bridge.index("private static void Postfix(NEventLayout")]
    assert "Array.Empty<EventOption>()" not in restore and "_isFinished" not in bridge
    assert "state.LegacyLoading = true" in restore and "finally { state.LegacyLoading = false; }" in restore
    assert "(!state.Returned || state.LegacyLoading)" in bridge
    proceed = bridge[bridge.index("private static void RestoreProceed"):bridge.index("private static async Task RestoreLegacy")]
    assert 'Dialogue(model) = new AncientDialogue("") { EndAttackers = ArchitectAttackers.Player }' in proceed
    assert "RestoreProceed(model)" in bridge and "finally { QueueFree(); }" in ui
    assert "SetLocalPlayerReady" not in bridge and '"WinRun"' not in bridge


check(UI, BRIDGE, SESSION)
cases = {
    "terminal identity removed": (UI, BRIDGE, SESSION.replace("model is TheArchitect", "false")),
    "session constructed early": (UI, BRIDGE.replace("Session => _session ??= new(_model)", "Session => new(_model)"), SESSION),
    "early UI attach returns": (UI, BRIDGE.replace("__result = Task.CompletedTask;", "Attach(model, layout, resume); __result = Task.CompletedTask;"), SESSION),
    "legacy native play blocked": (UI, BRIDGE.replace("state.LegacyFallback) return true", "state.Returned) return false"), SESSION),
    "transaction false ignored": (UI.replace("if (!operation())", "if (false)"), BRIDGE, SESSION),
    "transaction exception ignored": (UI.replace("catch (Exception ex) { ShowFatalError(ex); }", "catch (Exception) { }"), BRIDGE, SESSION),
    "fallback writes skip": (UI + "\n_session.Skip();", BRIDGE, SESSION),
    "legacy recovery disabled": (UI.replace("_useLegacyDialogue = true", "_useLegacyDialogue = false"), BRIDGE, SESSION),
    "original lines not restored": (UI, BRIDGE.replace("legacy.SetDialogue(lines)", "legacy.ClearDialogue()"), SESSION),
    "native early return leaves no options": (UI, BRIDGE.replace("if (model.CurrentOptions.Count == 0 || previousOptions.SequenceEqual(model.CurrentOptions)) RestoreProceed(model)", "if (false) RestoreProceed(model)"), SESSION),
    "empty model intro finishes event": (UI, BRIDGE.replace("state.LegacyLoading = true;", 'state.LegacyLoading = true; SetState.Invoke(model, new object[] { null!, Array.Empty<EventOption>() });'), SESSION),
    "null dialogue payload unguarded": (UI, BRIDGE.replace('Dialogue(model) = new AncientDialogue("") { EndAttackers = ArchitectAttackers.Player };\n        var option', 'Dialogue(model) = null;\n        var option'), SESSION),
    "intro input not guarded": (UI, BRIDGE.replace("(!state.Returned || state.LegacyLoading)", "!state.Returned"), SESSION),
    "intro input never released": (UI, BRIDGE.replace("finally { state.LegacyLoading = false; }", "finally { }"), SESSION),
}
for name, sources in cases.items():
    try:
        check(*sources)
    except AssertionError:
        continue
    raise AssertionError("Regression accepted: " + name)
print(f"issue#206 legacy fallback source gate PASS: positive + {len(cases)} rejected mutations; NOT native input acceptance")
