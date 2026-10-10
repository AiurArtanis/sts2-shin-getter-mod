#!/usr/bin/env python3
"""Read-only structural guard for issue#206; never launches Godot/the game.

Not a runtime or visual acceptance test. Written but intentionally NOT RUN during
the 2026-09-08 checkpoint or 2026-09-12 review handoff at the user's request. Optional source-root
checks use the read-only, decompiled official 109 source, not a running process.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
NPCS = ("OROBAS", "TANX", "VAKUU", "DARV", "PAEL", "TEZCATARA", "NONUPEIPE")
DRIVERS = {"RYOMA": "red", "HAYATO": "white", "BENKEI": "yellow"}
LANGUAGES = ("zhs", "eng", "jpn")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read(relative: str) -> str:
    return (ROOT / relative).read_text(encoding="utf-8-sig")


def check_tags(text: str, context: str) -> None:
    stack = []
    for close, name in re.findall(r"\[(/?)([a-z_]+)\]", text):
        if close:
            require(bool(stack) and stack.pop() == name, f"Unbalanced tag: {context}")
        else:
            stack.append(name)
    require(not stack, f"Unclosed tags: {context}")


def catalogue() -> None:
    locales = {}
    for language in LANGUAGES:
        rows = json.loads(read(f"data/dialogues/{language}.json"))
        indexed = {row["Id"]: row for row in rows}
        require(len(rows) == len(indexed) == 119, f"{language}: expected 119 unique dialogues")
        locales[language] = indexed
    source = locales["zhs"]
    require(sum(row["Stage"] > 0 for row in source.values()) == 63, "Expected 63 bond segments")
    for npc in NPCS:
        require(sum(row["Npc"] == npc for row in source.values()) == 14, f"NPC catalogue: {npc}")
        for driver in DRIVERS:
            for stage in range(1, 4):
                key = f"{npc}_{driver}_BOND_{stage:02}"
                require(key in source, f"Missing bond: {key}")
    for npc, count in (("NEOW", 13), ("THE_ARCHITECT", 8)):
        require(sum(row["Npc"] == npc for row in source.values()) == count, f"NPC catalogue: {npc}")
    require("NEOW_RETURN_01" not in source and "NEOW_CHAT_03" in source, "Neow roll-call/return regression")
    for key, original in source.items():
        require(original["Scene"], f"Missing storyboard constraint: {key}")
        unhashed = {k: v for k, v in original.items() if k != "Version"}
        version = hashlib.sha256(json.dumps(unhashed, ensure_ascii=False, sort_keys=True).encode()).hexdigest()[:16]
        require(original["Version"] == version, f"Source version drift: {key}")
        for language, rows in locales.items():
            require(set(rows) == set(source), f"Locale ID drift: {language}")
            row = rows[key]
            lines = row["Lines"]
            require(4 <= len(lines) <= 9 and len(lines) == len(original["Lines"]), f"Line-count drift: {language}/{key}")
            for line in lines:
                require(isinstance(line, str) and bool(line.strip()), f"Empty line: {language}/{key}")
                check_tags(line, f"{language}/{key}")
            if original["Stage"]:
                color = DRIVERS[original["Driver"]]
                option = row.get("Option", "")
                require(option.startswith(f"[{color}]") and f"[/{color}]" in option, f"Choice name color: {language}/{key}")
                check_tags(option, f"{language}/{key}/Option")


def encounter_priority(session: str) -> None:
    start = session.index("private ShinGetterBondEncounter SelectEncounter()")
    end = session.index("private List<RunHistory> ReadReliableHistories()", start)
    body = session[start:end]
    require("using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;" in session,
            "Story draws must explicitly use the cryptographic, non-gameplay RNG")
    tokens = (
        "ShinGetterBondSave save = _save!;", "if (!IsAcquainted(save, _npc))",
        "if (Choices.Count != 0)", "bool longAbsence", "if (longAbsence)", "RunHistory? latest",
    )
    for token in tokens:
        require(body.count(token) == 1, f"SelectEncounter contract missing/duplicated: {token}")
    loaded, first, choices, gap, absence, result = (body.index(token) for token in tokens)
    require(loaded < first < choices < gap < absence < result,
            "Loaded save/first/bonds/absence/outcome priority regression")
    require("Rng.Next" not in body and "Rng.Chaotic" not in body, "Story draws must not use gameplay RNG")


def contracts() -> None:
    session = read("src/Services/ShinGetterBondSession.cs")
    ui = read("src/Nodes/Events/NShinGetterBondDialogue.cs")
    patch = read("src/Patches/ShinGetterBondDialoguePatch.cs")
    catalog = read("src/Services/ShinGetterDialogueCatalog.cs")
    for token in (
        'GetProfileScopedPath("shin_getter_bonds.json")', "owner.RunState.Players.Count == 1",
        "GameMode.Standard", "Modifiers.Count == 0", "RunManager.Instance.ShouldSave",
        "!TestMode.IsOn", "!NonInteractiveMode.IsActive", "!RunManager.Instance.DailyTime.HasValue",
        "ReadSaveStatus.Success", "!latest.WasAbandoned", "latest.KilledByEncounter", "latest.KilledByEvent",
        "h.StartTime > lastMet && h.StartTime < _run", ">= 5", "lastMet != _run", '_npc != "NEOW"',
        "RandomNumberGenerator.GetInt32", "RespondedResults", "TextVersion", "LinesByLanguage",
        "FileShare.None", "FileOptions.DeleteOnClose", "file.Flush(flushToDisk: true)", "File.Replace",
        "CurrentProfileId != _profile", "Read().Revision != _save.Revision", "next.Revision++",
        "Enumerable.Range(1, dialogue.Stage - 1)", "WasShown", "MarkDisplayed",
    ):
        require(token in session, f"Missing persistence/mode boundary: {token}")
    require("Rng.Next" not in session and "Rng.Chaotic" not in session, "Story draws must not use gameplay RNG")
    encounter_priority(session)
    advance = session[session.index("internal bool Advance()"):session.index("internal bool ConsumeCue")]
    require(advance.index("Encounter.Line + 1 <") < advance.index("next.Completed.Add"), "Must confirm beyond final line")
    require("next.RespondedResults[_npc]" in advance, "Result consumed only after completion")
    skip = session[session.index("internal void Skip()"):session.index("private static bool IsAcquainted")]
    require("Completed.Add" not in skip and "_locallySkipped = true" in skip, "Skip must not grant progress or trap on save failure")
    identity = session[session.index("private static string BuildEncounterKey"):session.index("internal bool Begin()")]
    for token in ("state.MapPointHistory[act]", "state.CurrentMapCoord", "mapCoord.col", "mapCoord.row",
                  "ReferenceEquals(current, state.BaseRoom) ? 0", "rooms.FindLastIndex",
                  "entry.RoomType == RoomType.Event && entry.ModelId == model.Id",
                  "points.Count - 1", "encounter-v2:{run}:{state.Rng.Seed}:{act}:{coord}:{points.Count - 1}:{room}:{model.Id}"):
        require(token in identity, f"Persistent encounter history identity: {token}")
    identity_code = re.sub(r"//[^\n]*", "", identity)
    for token in ("RunLocation", "current.Id", "NextRoomId", "GetAndIncrementNextRoomId", "Guid.NewGuid"):
        require(token not in identity_code, f"Reload must not create a new encounter identity: {token}")
    require('!pair.Key.StartsWith("encounter-v2:", StringComparison.Ordinal)' in session,
            "Old unpublished roomId snapshots must fail safely, not replay as new encounters")
    # Artanis 2026-10-10 overrides pre-release acquaintance migration: all NPCs
    # start at zero once for v1.3.0, then keep legitimately earned release progress.
    require('ReleaseProgressEpoch = "v1.3.0"' in session and "CreateWithLegacyAcquaintances" not in session,
            "First release must not import prior visits/test progress")
    loader = session[session.index("private ShinGetterBondSave Read()"):session.index("private bool Commit(")]
    require("save.ProgressEpoch.Length == 0" in loader and "CreateReleaseSave(save.Revision)" in loader,
            "Pre-release saves must prepare an empty release epoch, retaining transaction revision")
    require("ReadReliableHistories" not in loader and "Read().Revision != _save.Revision" in session,
            "Initialization must not read encounter history and must commit atomically")
    require('history.Players[0].Character != ModelDb.Character<ShinGetter>().Id' in session
            and "history.StartTime <= 0 || history.StartTime >= _run" in session,
            "Do not migrate other characters, current/future runs or invalid timestamps")
    require(session.index("if (File.Exists(_path)) File.Replace") < session.index("_save = next"), "Publish memory only after atomic write")
    for source, echo in (
        ("VAKUU_RYOMA_BOND_03", "THE_ARCHITECT_STORY_VAKUU_01"),
        ("PAEL_BENKEI_BOND_03", "THE_ARCHITECT_STORY_PAEL_01"),
        ("OROBAS_RYOMA_BOND_03", "THE_ARCHITECT_STORY_OROBAS_01"),
    ):
        require(f'("{source}", "{echo}")' in session, f"Architect echo source: {echo}")
    for token in ("FocusMode = FocusModeEnum.All", "FocusNeighborTop", "FocusNeighborBottom",
                  "FocusNext", "TryGrabFocus", "base._ExitTree()", "IsEventActive", "_returnToEvent = null",
                  "ShowFatalError", "skip.Pressed += Skip", "StopVoice", "ResourceLoader.Exists"):
        require(token in ui, f"Missing UI lifecycle/input boundary: {token}")
    require("NModalContainer" not in ui and "NErrorPopup" not in ui, "Errors must not contend for a modal slot")
    require('("TANX_RYOMA_BOND_03", 4)' in ui and '("TANX_BENKEI_BOND_02", 3)' in ui, "Voice cue binding drift")
    require('"ryoma_getter_tomahawk.wav"' in ui and '"musashi_avalanche.wav"' in ui,
            "Only approved 010/035 voice assets; 035 historical filename remains unchanged")
    for forbidden in ("AnimatedSprite2D", "CreatureCmd.", "TriggerAnim(", "VfxCmd.", "NShinGetterStaticVisuals"):
        require(forbidden not in ui, f"2026-09-12 scope is dialogue/voice only, not action choreography: {forbidden}")
    cue = ui[ui.index("private void TryCue"):ui.index("private void StopVoice")]
    require(cue.index("ConsumeCue") < cue.index("VoiceMode ==") < cue.index("_voice.Play()"), "Consume before silence/play")
    for forbidden in ("AttackCommand", "DamageCmd", "PowerCmd", "CombatStart", "CardPlay"):
        require(forbidden not in cue, f"Story cue must not fake gameplay: {forbidden}")
    for token in ("ConditionalWeakTable<EventModel, State>", "WeakReference<NShinGetterBondDialogue>",
                  "OptionButtonClicked", "state.Returned", "CreateProceedOption", "ArchitectAttackers.Player"):
        require(token in patch, f"Missing original-event bridge boundary: {token}")
    require("SetLocalPlayerReady" not in patch and '"WinRun"' not in patch, "Keep native ending/score consumer")
    require('Read("zhs")' in catalog and "GetManifestResourceStream" in catalog, "Embedded catalogue source")
    require('EmbeddedResource Include="data\\dialogues\\*.json"' in read("ShinGetterMod.csproj"), "Catalogue embedding missing")


def official_api(source: Path) -> None:
    # Signature probes are textual and read-only. A later approved build must still
    # verify CLR signatures, and a later isolated run must verify Harmony binding.
    targets = {
        "src/Core/Nodes/Rooms/NEventRoom.cs": ("private EventModel _event;", "void OptionButtonClicked(EventOption option, int index)", "Control? DefaultFocusedControl"),
        "src/Core/Nodes/Events/NEventLayout.cs": ("protected EventModel _event;", "DisableEventOptions()", "void OnSetupComplete()"),
        "src/Core/Nodes/Events/NAncientEventLayout.cs": ("void SetDialogue(IReadOnlyList<AncientDialogueLine> lines)", "void ClearDialogue()"),
        "src/Core/Models/Events/TheArchitect.cs": ("AncientDialogue? _dialogue;", "void LoadDialogue()", "Task PlayCurrentLine()", "EventOption CreateProceedOption()"),
        "src/Core/Models/EventModel.cs": ("SetEventState(LocString", "IEnumerable<EventOption>"),
        "src/Core/Runs/RunManager.cs": ("long _startTime;", "bool ShouldSave"),
        "src/Core/Runs/RunState.cs": ("runState._mapPointHistory.AddRange", "_mapPointHistory[CurrentActIndex].Add(mapPointHistoryEntry);"),
        "src/Core/Runs/IRunState.cs": ("IReadOnlyList<IReadOnlyList<MapPointHistoryEntry>> MapPointHistory", "AbstractRoom? BaseRoom", "MapCoord? CurrentMapCoord"),
        "src/Core/Runs/History/MapPointRoomHistoryEntry.cs": ('JsonPropertyName("room_type")', 'JsonPropertyName("model_id")'),
        "src/Core/Saves/SerializableRun.cs": ('JsonPropertyName("map_point_history")', 'JsonPropertyName("visited_map_coords")'),
        "src/Core/Runs/RunHistory.cs": ('JsonPropertyName("map_point_history")', "List<List<MapPointHistoryEntry>> MapPointHistory"),
        "src/Core/Saves/SaveManager.cs": ("GetProfileScopedPath(string userData)", "GetAllRunHistoryNames()", "LoadRunHistory(string"),
        "src/Core/Nodes/GodotExtensions/NButton.cs": ("public override void _ExitTree()", "UnregisterHotkeys();"),
    }
    for path, signatures in targets.items():
        code = (source / path).read_text(encoding="utf-8-sig")
        for signature in signatures:
            require(signature in code, f"Official 109 API requires re-audit: {path}: {signature}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-root", type=Path)
    args = parser.parse_args()
    catalogue()
    contracts()
    if args.source_root:
        official_api(args.source_root)
    print("issue#206 structural checks PASS (not runtime/visual acceptance)")


if __name__ == "__main__":
    main()
